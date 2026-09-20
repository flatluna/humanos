using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

public sealed class StructuredLearningWorkplaceApplicationService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly EmployeeScoringProfileService _profileService;
    private readonly StructuredLearningWorkplaceApplicationAgent _agent;
    private readonly StructuredLearningWorkplaceMentorAgent _mentorAgent;
    private readonly AzureSpeechService _speechService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly ILogger<StructuredLearningWorkplaceApplicationService> _logger;

    public StructuredLearningWorkplaceApplicationService(
        HumanOsDbContext dbContext,
        EmployeeScoringProfileService profileService,
        StructuredLearningWorkplaceApplicationAgent agent,
        StructuredLearningWorkplaceMentorAgent mentorAgent,
        AzureSpeechService speechService,
        CapabilityGraphIllustrationStorageService illustrationStorage,
        ILogger<StructuredLearningWorkplaceApplicationService> logger)
    {
        _dbContext = dbContext;
        _profileService = profileService;
        _agent = agent;
        _mentorAgent = mentorAgent;
        _speechService = speechService;
        _illustrationStorage = illustrationStorage;
        _logger = logger;
    }

    public async Task<StructuredLearningWorkplaceApplicationResponse> GetOrCreateAsync(
        Guid sectionId,
        Guid? requestedStudentId = null,
        string? targetLanguage = null,
        bool forceRegenerate = false,
        CancellationToken cancellationToken = default)
    {
        var section = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Section '{sectionId}' was not found.");

        var studentId = await ResolveStudentIdAsync(requestedStudentId, section, cancellationToken);

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
        var teachingContent = !string.IsNullOrWhiteSpace(teachingStep?.Content)
            ? teachingStep.Content
            : section.Text;

        var resolvedLanguage = ResolveLanguage(targetLanguage, section.Document?.ConfigSnapshotJson, section.ConfigSnapshotJson, teachingContent);

        // 1. Check Data Lake cache (skip if forceRegenerate requested)
        if (!forceRegenerate && _illustrationStorage.IsConfigured)
        {
            try
            {
                var cachedJson = await _illustrationStorage.DownloadWorkplaceJsonAsync(
                    section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(cachedJson))
                {
                    var cached = JsonSerializer.Deserialize<StructuredLearningWorkplaceApplicationResponse>(
                        cachedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cached is not null)
                    {
                        var cachedLang = cached.LanguageCode?.Trim().ToLowerInvariant().StartsWith("es") == true ? "es" : "en";
                        if (cachedLang == resolvedLanguage)
                        {
                            cached.AudioUrl = BuildAudioUrl(sectionId, studentId);
                            return cached;
                        }

                        _logger.LogInformation(
                            "Cached workplace application for section {SectionId} has language '{CachedLang}', but resolved language is '{ResolvedLanguage}'. Invalidating stale cache.",
                            sectionId, cachedLang, resolvedLanguage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read cached workplace application for section {SectionId} and student {StudentId}", sectionId, studentId);
            }
        }

        // When regenerating (forced or stale/mismatched cache), clear previous audio
        if (_illustrationStorage.IsConfigured)
        {
            try
            {
                // Explicitly delete any old audio and old cached json so recreate starts completely clean
                await _illustrationStorage.DeleteIllustrationAsync(
                    _illustrationStorage.GetWorkplaceAudioStoragePath(section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old workplace audio before regeneration for section {SectionId}", sectionId);
            }
        }

        // 2. Build profile and student context
        EmployeeScoringProfile? profile = null;
        try
        {
            profile = await _profileService.BuildProfileAsync(studentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build employee profile for {StudentId}", studentId);
        }

        var studentName = !string.IsNullOrWhiteSpace(profile?.EmployeeName)
            ? profile.EmployeeName.Trim()
            : "Alejandro Díaz";
        // A raw email (no PersonProfile display name on file) is a poor fallback for the AI
        // greeting/on-screen name — derive a readable "Sofia Ruiz"-style name from it instead.
        if (studentName.Contains('@'))
        {
            var localPart = studentName[..studentName.IndexOf('@')];
            studentName = string.Join(' ', localPart.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Length > 0 ? char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant() : part));
        }

        var isStudent = !string.IsNullOrWhiteSpace(profile?.StudentAcademicProfileSummary);
        // Never fabricate a workplace job role for a real student — this whole step frames itself
        // as "apply this at your job", which doesn't apply to someone with no real employer.
        var effectiveRoleTitle = isStudent
            ? "Estudiante de Ingeniería en Sistemas Computacionales, Tecnológico de Monterrey"
            : (profile?.RoleTitle ?? "Software Engineering");

        var activeObjectivesList = profile?.ActiveObjectives
            .Take(2)
            .Select(o => $"{o.Title} {(string.IsNullOrWhiteSpace(o.Metric) ? "" : $"[{o.Metric}]")}".Trim())
            .ToList() ?? [];

        // Pick at most 2-3 key tools so the prompt stays clean and doesn't dump an overwhelming 20-tool catalog
        var keyTools = profile?.Tools != null && profile.Tools.Count > 0
            ? profile.Tools.Take(3).ToList()
            : new List<string>();

        var context = new StructuredLearningWorkplaceApplicationContext
        {
            CourseId = section.StructuredLearningDocumentId,
            CourseTitle = section.Document?.DocumentName ?? string.Empty,
            SectionId = section.StructuredLearningDocumentSectionId,
            ChapterTitle = section.ChapterTitle,
            SubchapterTitle = section.SubchapterTitle,
            Capability = section.Capability,
            TeachingContent = teachingContent,
            TargetLanguage = resolvedLanguage,

            StudentId = studentId,
            StudentName = studentName,
            RoleTitle = effectiveRoleTitle,
            RoleMission = isStudent ? string.Empty : (profile?.RolePurpose ?? string.Empty),
            YearsOfExperience = isStudent ? null : profile?.YearsOfExperience,
            ToolsAndTechnologies = isStudent ? [] : keyTools,
            ActiveObjectives = isStudent ? [] : activeObjectivesList,
            Kpis = isStudent ? [] : (profile?.Kpis?.Take(2).ToList() ?? []),
            TechnicalSkills = isStudent ? [] : (profile?.TechnicalSkills?.Take(3).ToList() ?? []),
            IsStudent = isStudent,
            StudentAcademicProfileSummary = profile?.StudentAcademicProfileSummary
        };

        // 3. Generate scenario with agent
        _logger.LogInformation("Generating workplace application scenario for section {SectionId} and student {StudentName}", sectionId, studentName);
        var scenario = await _agent.GenerateScenarioAsync(context, cancellationToken);

        // Ensure 100% exact verbatim match between the on-screen text and what is spoken by Azure Speech
        var textToSpeakAndDisplay = !string.IsNullOrWhiteSpace(scenario.SpokenAudioScript)
            ? scenario.SpokenAudioScript.Trim()
            : scenario.ScenarioDescription.Trim();

        // 4. Synthesize spoken audio with Azure Speech and upload to Data Lake
        if (_speechService.IsConfigured && _illustrationStorage.IsConfigured && !string.IsNullOrWhiteSpace(textToSpeakAndDisplay))
        {
            try
            {
                var isEnglish = resolvedLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
                var voiceName = isEnglish ? "en-US-AndrewNeural" : "es-MX-CecilioNeural";

                var audioBytes = await _speechService.SynthesizeAsync(textToSpeakAndDisplay, voiceName, cancellationToken);
                using var audioStream = new MemoryStream(audioBytes);

                await _illustrationStorage.UploadWorkplaceAudioAsync(
                    section.TenantId,
                    section.StructuredLearningDocumentId,
                    sectionId,
                    studentId,
                    audioStream,
                    contentType: "audio/mpeg",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to synthesize/upload workplace audio for section {SectionId}", sectionId);
            }
        }

        var response = new StructuredLearningWorkplaceApplicationResponse
        {
            SectionId = sectionId,
            PersonId = studentId,
            StudentName = studentName,
            RoleTitle = effectiveRoleTitle,
            Tools = isStudent ? [] : (profile?.Tools ?? []),
            TargetObjective = isStudent ? null : activeObjectivesList.FirstOrDefault(),
            ScenarioTitle = scenario.ScenarioTitle,
            ScenarioText = textToSpeakAndDisplay,
            PracticalActions = scenario.PracticalActions,
            AudioUrl = BuildAudioUrl(sectionId, studentId),
            LanguageCode = resolvedLanguage
        };

        // 5. Cache response JSON in Data Lake
        if (_illustrationStorage.IsConfigured)
        {
            try
            {
                var json = JsonSerializer.Serialize(response);
                await _illustrationStorage.UploadWorkplaceJsonAsync(
                    section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId, json, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache workplace application JSON for section {SectionId}", sectionId);
            }
        }

        return response;
    }

    public async Task<StructuredLearningWorkplaceMentorChatResponse> ChatWithMentorAsync(
        Guid sectionId,
        Guid? requestedStudentId,
        string message,
        string? existingSessionId,
        string? targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (!_mentorAgent.IsConfigured)
        {
            throw new InvalidOperationException("StructuredLearningWorkplaceMentorAgent is not configured.");
        }

        var workplace = await GetOrCreateAsync(sectionId, requestedStudentId, targetLanguage, forceRegenerate: false, cancellationToken);

        var section = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Section '{sectionId}' was not found.");

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
        var teachingContent = !string.IsNullOrWhiteSpace(teachingStep?.Content)
            ? teachingStep.Content
            : section.Text;

        var sessionId = !string.IsNullOrWhiteSpace(existingSessionId)
            ? existingSessionId
            : $"workplace_{sectionId}_{workplace.PersonId}_{Guid.NewGuid():N}";

        var resolvedLanguage = !string.IsNullOrWhiteSpace(targetLanguage)
            ? targetLanguage
            : workplace.LanguageCode;

        var turnContext = new StructuredLearningWorkplaceMentorTurnContext
        {
            CourseTitle = section.Document?.DocumentName ?? string.Empty,
            ChapterTitle = section.ChapterTitle,
            EngramTitle = section.SubchapterTitle,
            TeachingContent = teachingContent,

            StudentName = workplace.StudentName,
            RoleTitle = workplace.RoleTitle,
            KeyTools = workplace.Tools.Take(3).ToList(),
            ActiveObjectives = string.IsNullOrWhiteSpace(workplace.TargetObjective) ? [] : [workplace.TargetObjective],

            WorkplaceScenarioTitle = workplace.ScenarioTitle,
            WorkplaceScenarioText = workplace.ScenarioText,
            PracticalActions = workplace.PracticalActions,

            TargetLanguage = resolvedLanguage
        };

        var reply = await _mentorAgent.AskAsync(sessionId, message, turnContext, cancellationToken);

        return new StructuredLearningWorkplaceMentorChatResponse
        {
            Reply = reply,
            SessionId = sessionId
        };
    }

    private async Task<Guid> ResolveStudentIdAsync(Guid? requestedStudentId, StructuredLearningDocumentSection section, CancellationToken cancellationToken)
    {
        if (requestedStudentId.HasValue && requestedStudentId.Value != Guid.Empty)
        {
            return requestedStudentId.Value;
        }

        // Try creator email
        if (!string.IsNullOrWhiteSpace(section.Document?.CreatedByEmail))
        {
            var p = await _dbContext.People.AsNoTracking().FirstOrDefaultAsync(x => x.Email == section.Document.CreatedByEmail, cancellationToken);
            if (p is not null) return p.PersonId;
        }

        // Fallback to Alejandro Díaz or primary active profile
        var alejandro = await _dbContext.PersonProfiles.AsNoTracking()
            .Where(pp => (pp.DisplayName != null && pp.DisplayName.Contains("Alejandro")) || (pp.FirstName != null && pp.FirstName.Contains("Alejandro")))
            .Select(pp => (Guid?)pp.PersonId)
            .FirstOrDefaultAsync(cancellationToken);

        if (alejandro.HasValue) return alejandro.Value;

        var defaultPerson = await _dbContext.People.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.CreatedDate)
            .Select(p => p.PersonId)
            .FirstOrDefaultAsync(cancellationToken);

        return defaultPerson != Guid.Empty ? defaultPerson : Guid.NewGuid();
    }

    private static string BuildAudioUrl(Guid sectionId, Guid studentId) =>
        $"http://localhost:7071/api/studio/structured-learning/sections/{sectionId:D}/workplace-application/audio?personId={studentId:D}";

    private static string ResolveLanguage(
        string? requestedLanguage,
        string? documentConfigJson,
        string? sectionConfigJson,
        string sampleText)
    {
        // 1. Authoritative check: course/section config snapshot
        foreach (var configJson in new[] { sectionConfigJson, documentConfigJson })
        {
            if (string.IsNullOrWhiteSpace(configJson)) continue;
            try
            {
                var snapshot = JsonSerializer.Deserialize<JsonElement>(configJson);
                foreach (var prop in snapshot.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "languageCode", StringComparison.OrdinalIgnoreCase) &&
                        prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var code = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            return code.Trim().ToLowerInvariant().StartsWith("es") ? "es" : "en";
                        }
                    }
                }
            }
            catch { }
        }

        // 2. Explicit client requested language if config is not present
        if (!string.IsNullOrWhiteSpace(requestedLanguage))
        {
            return requestedLanguage.Trim().ToLowerInvariant().StartsWith("es") ? "es" : "en";
        }

        // 3. Fallback: text heuristic
        if (!string.IsNullOrWhiteSpace(sampleText))
        {
            var padded = $" {sampleText.ToLowerInvariant()} ";
            string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
            string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu ", " en ", " del ", " por ", " una ", " uno "];
            var englishHits = englishWords.Count(w => padded.Contains(w));
            var spanishHits = spanishWords.Count(w => padded.Contains(w));
            if (spanishHits > 0 || englishHits > 0)
            {
                return englishHits > spanishHits ? "en" : "es";
            }
        }

        return "es";
    }
}
