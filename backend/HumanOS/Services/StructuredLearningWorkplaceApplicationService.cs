using System.Text;
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
    // Bump this whenever the CQI-aware agent's output rules change materially (e.g. numbering,
    // bullet grounding) so all previously cached non-student responses regenerate exactly once.
    // v3 (2026-09-22): added CqiLeadingPatternType/CqiExampleImageUrls (Type 6 reveal modal).
    // v4 (2026-09-22): removed the reveal modal in favor of inline CQI content; audio now narrates
    // the full application (practical actions, why this matters, reflection, growth, connections,
    // and CQI worked-example explanations) instead of only the intro paragraph.
    // v5 (2026-09-22): tightened ExampleImageExplanations grounding — must depict only what the
    // engram itself literally describes, never invented technical scenarios loosely tied by label words.
    // v6 (2026-09-22): forbid naming the employee's own specific real project/system as the depicted
    // action unless the engram itself is literally about that kind of system.
    private const int CurrentSchemaVersion = 6;

    private readonly HumanOsDbContext _dbContext;
    private readonly EmployeeScoringProfileService _profileService;
    private readonly PersonCqiProfileService _cqiProfileService;
    private readonly StructuredLearningWorkplaceApplicationAgent _agent;
    private readonly StructuredLearningWorkplaceApplicationCqiAgent _cqiAgent;
    private readonly StructuredLearningWorkplaceMentorAgent _mentorAgent;
    private readonly AzureSpeechService _speechService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly HumanOS.Agents.Studio.GraphIllustrationImageService _imageService;
    private readonly ILogger<StructuredLearningWorkplaceApplicationService> _logger;

    public StructuredLearningWorkplaceApplicationService(
        HumanOsDbContext dbContext,
        EmployeeScoringProfileService profileService,
        PersonCqiProfileService cqiProfileService,
        StructuredLearningWorkplaceApplicationAgent agent,
        StructuredLearningWorkplaceApplicationCqiAgent cqiAgent,
        StructuredLearningWorkplaceMentorAgent mentorAgent,
        AzureSpeechService speechService,
        CapabilityGraphIllustrationStorageService illustrationStorage,
        HumanOS.Agents.Studio.GraphIllustrationImageService imageService,
        ILogger<StructuredLearningWorkplaceApplicationService> logger)
    {
        _dbContext = dbContext;
        _profileService = profileService;
        _cqiProfileService = cqiProfileService;
        _agent = agent;
        _cqiAgent = cqiAgent;
        _mentorAgent = mentorAgent;
        _speechService = speechService;
        _illustrationStorage = illustrationStorage;
        _imageService = imageService;
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

        // Build profile/isStudent up front so the cache check below can invalidate stale
        // pre-CQI-sections cached responses for non-student (business/employee) tenants.
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
                        // Non-student cached responses generated before the CQI-aware sections existed,
                        // or before a schema-affecting prompt fix (numbering, bullet grounding, etc.),
                        // are treated as stale and regenerated.
                        var isStaleNonStudentCache = !isStudent
                            && (cached.SchemaVersion < CurrentSchemaVersion
                                || string.IsNullOrWhiteSpace(cached.WhyThisMatters)
                                || cached.ReflectionQuestions is null
                                || cached.ReflectionQuestions.Count == 0);
                        if (cachedLang == resolvedLanguage && !isStaleNonStudentCache)
                        {
                            cached.AudioUrl = BuildAudioUrl(sectionId, studentId);
                            return cached;
                        }

                        _logger.LogInformation(
                            "Cached workplace application for section {SectionId} is stale (language mismatch, missing CQI sections, or outdated schema version). Invalidating cache.",
                            sectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read cached workplace application for section {SectionId} and student {StudentId}", sectionId, studentId);
            }
        }

        // When regenerating (forced or stale/mismatched cache), purge EVERYTHING from the previous
        // run — audio, cached JSON, and both CQI example images — so no stale artifact from an old
        // engram/content version can ever be served again, even if this run's generation fails partway.
        if (_illustrationStorage.IsConfigured)
        {
            var pathsToPurge = new[]
            {
                _illustrationStorage.GetWorkplaceAudioStoragePath(section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId),
                _illustrationStorage.GetWorkplaceJsonStoragePath(section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId),
                _illustrationStorage.GetWorkplaceExampleImageStoragePath(section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId, 1),
                _illustrationStorage.GetWorkplaceExampleImageStoragePath(section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId, 2),
            };
            foreach (var path in pathsToPurge)
            {
                try
                {
                    await _illustrationStorage.DeleteIllustrationAsync(path, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old workplace artifact {Path} before regeneration for section {SectionId}", path, sectionId);
                }
            }
        }

        // 2. Build student context (profile already resolved above)
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

        // CQI Workspace profile only applies to business/employee tenants, never students.
        HumanOS.Contracts.People.CqiProfileData? cqiProfile = null;
        if (!isStudent)
        {
            try
            {
                var cqiResponse = await _cqiProfileService.GetByPersonIdAsync(studentId, cancellationToken);
                cqiProfile = cqiResponse.Profile;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load CQI profile for {StudentId}", studentId);
            }
        }

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
            CqiProfile = cqiProfile
        };

        // 3. Generate scenario with agent — students always use the plain agent; business/employee
        // tenants use the CQI-aware agent (works fine even when the employee has no CQI profile yet).
        _logger.LogInformation("Generating workplace application scenario for section {SectionId} and student {StudentName}", sectionId, studentName);
        var scenario = isStudent
            ? await _agent.GenerateScenarioAsync(context, cancellationToken)
            : await _cqiAgent.GenerateScenarioAsync(context, cancellationToken);

        // Ensure 100% exact verbatim match between the on-screen text and what is spoken by Azure Speech
        var textToSpeakAndDisplay = !string.IsNullOrWhiteSpace(scenario.SpokenAudioScript)
            ? scenario.SpokenAudioScript.Trim()
            : scenario.ScenarioDescription.Trim();

        // 3b. CQI Type 6 ("The Verifier") recognition — employees/business tenants only, never students.
        // Generated BEFORE audio synthesis so the two worked-example explanations can also be narrated.
        string? cqiTypeLabel = null;
        var cqiExampleImageUrls = new List<string>();
        var cqiExampleImageExplanations = new List<string>();
        if (!isStudent && cqiProfile is not null &&
            cqiProfile.LeadingPatternType.Contains('6', StringComparison.Ordinal))
        {
            cqiTypeLabel = $"{cqiProfile.LeadingPatternType}: {cqiProfile.LeadingPatternName}";
            cqiExampleImageExplanations = scenario.ExampleImageExplanations;
            cqiExampleImageUrls = await GenerateCqiExampleImagesAsync(
                section.TenantId, section.StructuredLearningDocumentId, sectionId, studentId,
                effectiveRoleTitle, cqiExampleImageExplanations, resolvedLanguage, cancellationToken);
        }

        // 4. Synthesize spoken audio with Azure Speech and upload to Data Lake — narrates the FULL
        // application (scenario, practical actions, why this matters, reflection, growth, knowledge
        // connections, and the CQI worked-example explanations when present), not just the intro.
        var isEnglishNarration = resolvedLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var fullNarrationScript = BuildFullNarrationScript(
            textToSpeakAndDisplay,
            scenario.PracticalActions,
            scenario.WhyThisMatters,
            scenario.ReflectionQuestions,
            scenario.GrowthOpportunity,
            scenario.KnowledgeConnections,
            cqiExampleImageExplanations,
            isEs: !isEnglishNarration);

        if (_speechService.IsConfigured && _illustrationStorage.IsConfigured && !string.IsNullOrWhiteSpace(fullNarrationScript))
        {
            try
            {
                var voiceName = isEnglishNarration ? "en-US-AndrewNeural" : "es-MX-CecilioNeural";

                var audioBytes = await _speechService.SynthesizeAsync(fullNarrationScript, voiceName, cancellationToken);
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
            LanguageCode = resolvedLanguage,
            WhyThisMatters = scenario.WhyThisMatters,
            ReflectionQuestions = scenario.ReflectionQuestions,
            GrowthOpportunity = scenario.GrowthOpportunity,
            KnowledgeConnections = scenario.KnowledgeConnections,
            SchemaVersion = CurrentSchemaVersion,
            CqiLeadingPatternType = cqiProfile?.LeadingPatternType,
            CqiLeadingPatternName = cqiProfile?.LeadingPatternName,
            CqiTypeLabel = cqiTypeLabel,
            CqiLearningAndWorkingPreferences = cqiTypeLabel is not null ? cqiProfile?.LearningAndWorkingPreferences : null,
            CqiExampleImageUrls = cqiExampleImageUrls,
            CqiExampleImageExplanations = cqiExampleImageExplanations
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

    private static string BuildCqiExampleImageUrl(Guid sectionId, Guid studentId, int imageIndex) =>
        $"http://localhost:7071/api/studio/structured-learning/sections/{sectionId:D}/workplace-application/cqi-example-image/{imageIndex}?personId={studentId:D}";

    /// <summary>Generates 2 additional example illustrations (concrete depictions of this engram
    /// applied to the employee's own job role) for CQI Type 6 ("The Verifier") employees, one per
    /// grounded explanation paragraph the CQI agent already wrote (so the image and its caption
    /// always match). Best-effort: any failure just leaves that image URL out, never fails the request.</summary>
    private async Task<List<string>> GenerateCqiExampleImagesAsync(
        Guid tenantId,
        Guid capabilityId,
        Guid sectionId,
        Guid studentId,
        string roleTitle,
        List<string> explanations,
        string targetLanguage,
        CancellationToken cancellationToken)
    {
        var urls = new List<string>();

        if (!_imageService.IsConfigured || !_illustrationStorage.IsConfigured || explanations.Count == 0)
        {
            return urls;
        }

        var isEs = targetLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < explanations.Count; i++)
        {
            var prompt = isEs
                ? $"Ilustraci\u00f3n realista y concreta para el rol de {roleTitle} que represente exactamente esta escena de trabajo (sin texto ni pizarrones): {explanations[i]}"
                : $"Realistic, concrete illustration for the role of {roleTitle} depicting exactly this workplace scene (no text or blackboards): {explanations[i]}";

            try
            {
                var generated = await _imageService.GenerateAsync(prompt, cancellationToken: cancellationToken);
                using var imageStream = generated.ImageBytes.ToStream();
                await _illustrationStorage.UploadWorkplaceExampleImageAsync(
                    tenantId, capabilityId, sectionId, studentId, imageIndex: i + 1, imageStream, cancellationToken: cancellationToken);
                urls.Add(BuildCqiExampleImageUrl(sectionId, studentId, i + 1));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate CQI Type 6 example image {Index} for section {SectionId}", i + 1, sectionId);
            }
        }

        return urls;
    }

    /// <summary>Builds the full audio narration script: the greeting/scenario paragraph PLUS every
    /// other section (practical actions, why this matters, reflection questions, growth opportunity,
    /// knowledge connections, and — for CQI Type 6 — the two worked-example explanations) read aloud
    /// as natural prose, so the recorded voice covers everything on the page, not just the intro.</summary>
    private static string BuildFullNarrationScript(
        string scenarioParagraph,
        List<string> practicalActions,
        string whyThisMatters,
        List<string> reflectionQuestions,
        string growthOpportunity,
        List<string> knowledgeConnections,
        List<string> cqiExampleExplanations,
        bool isEs)
    {
        var sb = new StringBuilder();
        sb.AppendLine(scenarioParagraph.Trim());

        static IEnumerable<string> StripBullets(string block) =>
            block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.TrimStart('-', ' ').Trim())
                .Where(line => line.Length > 0);

        if (practicalActions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(isEs ? "Acciones pr\u00e1cticas sugeridas en tus proyectos:" : "Suggested practical actions in your projects:");
            foreach (var action in practicalActions) sb.AppendLine(action.Trim());
        }

        if (!string.IsNullOrWhiteSpace(whyThisMatters))
        {
            sb.AppendLine();
            sb.AppendLine(isEs ? "Por qu\u00e9 esto importa:" : "Why this matters:");
            foreach (var line in StripBullets(whyThisMatters)) sb.AppendLine(line);
        }

        if (reflectionQuestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(isEs ? "Reflexi\u00f3n personalizada:" : "Personalized reflection:");
            foreach (var question in reflectionQuestions) sb.AppendLine(question.Trim());
        }

        if (!string.IsNullOrWhiteSpace(growthOpportunity))
        {
            sb.AppendLine();
            sb.AppendLine(isEs ? "Oportunidad de crecimiento:" : "Growth opportunity:");
            foreach (var line in StripBullets(growthOpportunity)) sb.AppendLine(line);
        }

        if (knowledgeConnections.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(isEs ? "Conexiones de conocimiento: " + string.Join(", ", knowledgeConnections) + "."
                : "Knowledge connections: " + string.Join(", ", knowledgeConnections) + ".");
        }

        if (cqiExampleExplanations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(isEs ? "Dos ejemplos pr\u00e1cticos adicionales para tu puesto:" : "Two additional practical examples for your role:");
            foreach (var explanation in cqiExampleExplanations) sb.AppendLine(explanation.Trim());
        }

        return sb.ToString().Trim();
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
