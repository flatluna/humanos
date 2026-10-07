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

/// <summary>Thrown by <see cref="StructuredLearningWorkplaceApplicationService.GetOrCreateAsync"/>
/// when a passive GET finds no cached application and was given no instructions to
/// generate one with — signals the caller (the Function) to return 404 instead of
/// silently auto-generating with empty instructions (2026-10-03 regression fix for
/// "In your role" skipping the user's own prompt on first visit).</summary>
public sealed class WorkplaceApplicationNotGeneratedException : Exception
{
}

public sealed class StructuredLearningWorkplaceApplicationService
{
    // v7: user-directed applications and worked artifacts for employees and students, without CQI.
    private const int CurrentSchemaVersion = 7;
    public const int MaxUserInstructionsLength = 4000;

    private readonly HumanOsDbContext _dbContext;
    private readonly EmployeeScoringProfileService _profileService;
    private readonly StructuredLearningWorkplaceApplicationAgent _agent;
    private readonly StructuredLearningWorkplaceMentorAgent _mentorAgent;
    private readonly AzureSpeechService _speechService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly StructuredLearningEngramUsageService _usageService;
    private readonly ILogger<StructuredLearningWorkplaceApplicationService> _logger;

    public StructuredLearningWorkplaceApplicationService(
        HumanOsDbContext dbContext,
        EmployeeScoringProfileService profileService,
        StructuredLearningWorkplaceApplicationAgent agent,
        StructuredLearningWorkplaceMentorAgent mentorAgent,
        AzureSpeechService speechService,
        CapabilityGraphIllustrationStorageService illustrationStorage,
        StructuredLearningEngramUsageService usageService,
        ILogger<StructuredLearningWorkplaceApplicationService> logger)
    {
        _dbContext = dbContext;
        _profileService = profileService;
        _agent = agent;
        _mentorAgent = mentorAgent;
        _speechService = speechService;
        _illustrationStorage = illustrationStorage;
        _usageService = usageService;
        _logger = logger;
    }

    public async Task<StructuredLearningWorkplaceApplicationResponse> GetOrCreateAsync(
        Guid sectionId,
        Guid? requestedStudentId = null,
        string? targetLanguage = null,
        bool forceRegenerate = false,
        Guid? engramSessionId = null,
        string? userInstructions = null,
        CancellationToken cancellationToken = default)
    {
        if (userInstructions?.Length > MaxUserInstructionsLength)
            throw new ArgumentException($"Instructions cannot exceed {MaxUserInstructionsLength} characters.", nameof(userInstructions));
        if (!_illustrationStorage.IsConfigured)
            throw new InvalidOperationException("Workplace application storage is not configured. Results cannot be saved.");

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

        EmployeeScoringProfile? profile = null;
        try
        {
            profile = await _profileService.BuildProfileAsync(studentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build employee profile for {StudentId}", studentId);
        }

        var isStudent = !string.IsNullOrWhiteSpace(profile?.StudentAcademicProfileSummary);

        // Read the previous request even when regenerating, so a schema/language update retains it.
        if (_illustrationStorage.IsConfigured)
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
                        userInstructions ??= cached.UserInstructions;
                        var cachedLang = cached.LanguageCode?.Trim().ToLowerInvariant().StartsWith("es") == true ? "es" : "en";
                        var isStaleCache = cached.SchemaVersion < CurrentSchemaVersion
                            || string.IsNullOrWhiteSpace(cached.WhyThisMatters)
                            || cached.WorkedExamples is null || cached.WorkedExamples.Count == 0;
                        if (!forceRegenerate && cachedLang == resolvedLanguage && !isStaleCache
                            && cached.UserInstructions == userInstructions)
                        {
                            if (!string.IsNullOrWhiteSpace(cached.AudioUrl))
                                cached.AudioUrl = BuildAudioUrl(sectionId, studentId);
                            return cached;
                        }

                        _logger.LogInformation(
                            "Regenerating workplace application for section {SectionId} (request, language or schema changed).",
                            sectionId);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid cached workplace application JSON for section {SectionId} and student {StudentId}", sectionId, studentId);
            }
        }

        // No usable cache, and the caller never supplied instructions (i.e. this is the
        // passive GET the frontend fires on page load, not an explicit "Generate" POST) —
        // never auto-generate one using empty instructions. The user must get the chance
        // to type their own prompt first (2026-10-03 regression fix).
        if (userInstructions is null)
        {
            throw new WorkplaceApplicationNotGeneratedException();
        }

        // 2. Build student context (profile already resolved above)
        var studentName = !string.IsNullOrWhiteSpace(profile?.EmployeeName)
            ? profile.EmployeeName.Trim()
            : (resolvedLanguage == "es" ? "Estudiante" : "Learner");
        // A raw email (no PersonProfile display name on file) is a poor fallback for the AI
        // greeting/on-screen name — derive a readable "Sofia Ruiz"-style name from it instead.
        if (studentName.Contains('@'))
        {
            var localPart = studentName[..studentName.IndexOf('@')];
            studentName = string.Join(' ', localPart.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Length > 0 ? char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant() : part));
        }

        // Never fabricate a workplace job role for a real student — this whole step frames itself
        // as "apply this at your job", which doesn't apply to someone with no real employer.
        var effectiveRoleTitle = profile?.RoleTitle
            ?? (isStudent ? "Student" : "Employee");

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
            StudentAcademicProfileSummary = profile?.StudentAcademicProfileSummary,
            UserInstructions = userInstructions?.Trim() ?? string.Empty
        };

        _logger.LogInformation("Generating workplace application scenario for section {SectionId} and student {StudentName}", sectionId, studentName);
        var agentResult = await _agent.GenerateScenarioAsync(context, cancellationToken);
        var scenario = agentResult.Result;

        // Real token usage from this generation call (2026-10-02) — only counted here, never on a
        // cache hit, so cost reflects actual LLM calls. Rolled up under the real course session when
        // the caller provides one (same per-visit grouping as Voice/Text "Q&A with Professor").
        await _usageService.RecordTextUsageAsync(
            studentId, section.StructuredLearningDocumentId, sectionId, engramSessionId,
            agentResult.ModelName, agentResult.InputTokens, agentResult.OutputTokens, cancellationToken, stepType: "Production");

        // Ensure 100% exact verbatim match between the on-screen text and what is spoken by Azure Speech
        var textToSpeakAndDisplay = !string.IsNullOrWhiteSpace(scenario.SpokenAudioScript)
            ? scenario.SpokenAudioScript.Trim()
            : scenario.ScenarioDescription.Trim();

        ValidateScenario(scenario);

        // Narrate only the overview, never read code or formulas as prose.
        var isEnglishNarration = resolvedLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var audioUrl = string.Empty;
        if (_speechService.IsConfigured && !string.IsNullOrWhiteSpace(textToSpeakAndDisplay))
        {
            try
            {
                var voiceName = isEnglishNarration ? "en-US-AndrewNeural" : "es-MX-CecilioNeural";

                var audioBytes = await _speechService.SynthesizeAsync(textToSpeakAndDisplay, voiceName, cancellationToken);
                try
                {
                    await _usageService.RecordTextToSpeechUsageAsync(
                        studentId,
                        section.StructuredLearningDocumentId,
                        sectionId,
                        engramSessionId,
                        voiceName,
                        textToSpeakAndDisplay.Length,
                        cancellationToken);
                }
                catch (Exception usageException)
                {
                    _logger.LogWarning(
                        usageException,
                        "Failed to record Azure Speech usage for workplace application section {SectionId}",
                        sectionId);
                }
                using var audioStream = new MemoryStream(audioBytes);

                await _illustrationStorage.UploadWorkplaceAudioAsync(
                    section.TenantId,
                    section.StructuredLearningDocumentId,
                    sectionId,
                    studentId,
                    audioStream,
                    contentType: "audio/mpeg",
                    cancellationToken: cancellationToken);
                audioUrl = BuildAudioUrl(sectionId, studentId);
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
            AudioUrl = audioUrl,
            LanguageCode = resolvedLanguage,
            WhyThisMatters = scenario.WhyThisMatters,
            UserInstructions = context.UserInstructions,
            WorkedExamples = scenario.WorkedExamples,
            KnowledgeConnections = scenario.KnowledgeConnections,
            SchemaVersion = CurrentSchemaVersion
        };

        // 5. Cache response JSON in Data Lake
        if (_illustrationStorage.IsConfigured)
        {
            var json = JsonSerializer.Serialize(response);
            await _illustrationStorage.UploadWorkplaceJsonAsync(
                section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId, json, cancellationToken);
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

        var workplace = await GetOrCreateAsync(sectionId, requestedStudentId, targetLanguage, forceRegenerate: false, cancellationToken: cancellationToken);

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
            UserInstructions = workplace.UserInstructions,
            WorkedExamples = workplace.WorkedExamples,

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

    internal static void ValidateScenario(StructuredLearningWorkplaceApplicationResult scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.ScenarioTitle)
            || string.IsNullOrWhiteSpace(scenario.ScenarioDescription)
            || scenario.ScenarioDescription != scenario.SpokenAudioScript
            || string.IsNullOrWhiteSpace(scenario.WhyThisMatters)
            || scenario.WorkedExamples is null || scenario.WorkedExamples.Count is < 1 or > 3)
            throw new InvalidOperationException("The agent returned an incomplete workplace application.");

        foreach (var example in scenario.WorkedExamples)
        {
            if (example is null || string.IsNullOrWhiteSpace(example.Title)
                || string.IsNullOrWhiteSpace(example.Situation)
                || string.IsNullOrWhiteSpace(example.InputsAndAssumptions)
                || example.Steps is null || example.Steps.Count == 0
                || example.Steps.Any(string.IsNullOrWhiteSpace)
                || string.IsNullOrWhiteSpace(example.Artifact)
                || string.IsNullOrWhiteSpace(example.ExpectedResult)
                || string.IsNullOrWhiteSpace(example.Validation))
                throw new InvalidOperationException("The agent returned an incomplete worked example.");
        }
    }

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
