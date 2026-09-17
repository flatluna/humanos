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

/// <summary>
/// Service that orchestrates gathering all Engram information and the student's complete
/// professional context to generate a personalized spoken lecture via <see cref="StructuredLearningProfessorLectureAgent"/>.
/// </summary>
public sealed class StructuredLearningProfessorLectureService
{
    private readonly HumanOsDbContext _dbContext;
    private readonly EmployeeScoringProfileService _profileService;
    private readonly StructuredLearningProfessorLectureAgent _agent;
    private readonly AzureSpeechService _speechService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly ILogger<StructuredLearningProfessorLectureService> _logger;

    public StructuredLearningProfessorLectureService(
        HumanOsDbContext dbContext,
        EmployeeScoringProfileService profileService,
        StructuredLearningProfessorLectureAgent agent,
        AzureSpeechService speechService,
        CapabilityGraphIllustrationStorageService illustrationStorage,
        ILogger<StructuredLearningProfessorLectureService> logger)
    {
        _dbContext = dbContext;
        _profileService = profileService;
        _agent = agent;
        _speechService = speechService;
        _illustrationStorage = illustrationStorage;
        _logger = logger;
    }

    /// <summary>
    /// Generates a personalized spoken lecture for an Engram (section) tailored to a specific employee/student.
    /// </summary>
    public async Task<StructuredLearningProfessorLectureResult> GenerateLectureForSectionAsync(
        Guid sectionId,
        Guid? employeeId = null,
        string? targetLanguage = null,
        CancellationToken cancellationToken = default)
    {
        var section = await _dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken);

        if (section is null)
        {
            throw new KeyNotFoundException($"Structured learning section with ID '{sectionId}' was not found.");
        }

        // 1. Identify the Teaching content (the core lesson of the engram)
        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
        var teachingContent = !string.IsNullOrWhiteSpace(teachingStep?.Content)
            ? teachingStep.Content
            : section.Text;

        if (string.IsNullOrWhiteSpace(teachingContent))
        {
            teachingContent = $"Lesson on {section.SubchapterTitle} ({section.ChapterTitle}).";
        }

        // 2. Identify Visual/Illustration elements (whiteboard, diagram, etc.)
        var illustration = await _dbContext.StructuredLearningIllustrations
            .AsNoTracking()
            .Where(i => i.StructuredLearningDocumentSectionId == sectionId)
            .OrderByDescending(i => i.Purpose == StructuredLearningIllustrationPurpose.Teaching)
            .FirstOrDefaultAsync(cancellationToken);

        string? illustrationDescription = null;
        if (illustration is not null)
        {
            illustrationDescription = !string.IsNullOrWhiteSpace(illustration.Caption)
                ? illustration.Caption
                : illustration.Prompt;
        }

        // 3. Resolve Target Language
        var resolvedLanguage = targetLanguage;
        if (string.IsNullOrWhiteSpace(resolvedLanguage))
        {
            resolvedLanguage = ResolveLanguageCode(section.Document?.ConfigSnapshotJson, teachingContent);
        }

        // 4. Construct Universal Context for the Agent (Generic for all learners)
        var context = new StructuredLearningProfessorLectureContext
        {
            CourseId = section.StructuredLearningDocumentId,
            CourseTitle = section.Document?.DocumentName ?? string.Empty,
            SectionId = section.StructuredLearningDocumentSectionId,
            ChapterTitle = section.ChapterTitle,
            SubchapterTitle = section.SubchapterTitle,
            Capability = section.Capability,
            TeachingContent = teachingContent,
            IllustrationDescription = illustrationDescription,
            DomainOrTopic = section.Document?.Category ?? section.Document?.TargetSkill,
            TargetLanguage = resolvedLanguage
        };

        // 5. Invoke the Agent
        _logger.LogInformation(
            "Invoking StructuredLearningProfessorLectureAgent for section {SectionId} ({SubchapterTitle}) in language '{Language}'",
            sectionId, section.SubchapterTitle, resolvedLanguage);

        var lectureResult = await _agent.GenerateLectureAsync(context, cancellationToken);

        // Synthesize spoken audio and persist to Data Lake and section Teaching step
        if (_speechService.IsConfigured && _illustrationStorage.IsConfigured && !string.IsNullOrWhiteSpace(lectureResult.SpokenLectureScript))
        {
            try
            {
                var speechText = lectureResult.SpokenLectureScript;
                var padded = $" {speechText.ToLowerInvariant()} ";
                string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
                string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu "];
                var isEnglish = englishWords.Count(w => padded.Contains(w)) > spanishWords.Count(w => padded.Contains(w));

                var voiceName = isEnglish
                    ? "en-US-AndrewNeural"
                    : "es-MX-CecilioNeural";

                var audioBytes = await _speechService.SynthesizeAsync(speechText, voiceName, cancellationToken);
                using var audioStream = new MemoryStream(audioBytes);

                var storagePath = await _illustrationStorage.UploadLectureAudioAsync(
                    section.TenantId,
                    section.StructuredLearningDocumentId,
                    section.StructuredLearningDocumentSectionId,
                    audioStream,
                    contentType: "audio/mpeg",
                    cancellationToken: cancellationToken);

                // Update the Teaching step in database with ProfessorLecture and SpeechStoragePath
                var teachingStepEntity = await _dbContext.StructuredLearningSectionSteps
                    .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Teaching, cancellationToken);

                if (teachingStepEntity is not null)
                {
                    if (!string.IsNullOrWhiteSpace(lectureResult.SpokenLectureScript))
                    {
                        teachingStepEntity.Content = lectureResult.SpokenLectureScript;
                    }

                    Guid? illustrationId = null;
                    List<string> existingCitations = [];
                    if (!string.IsNullOrWhiteSpace(teachingStepEntity.StepDataJson))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(teachingStepEntity.StepDataJson);
                            if (doc.RootElement.TryGetProperty("IllustrationId", out var ilProp) && ilProp.ValueKind == JsonValueKind.String)
                            {
                                if (Guid.TryParse(ilProp.GetString(), out var parsedId)) illustrationId = parsedId;
                            }
                            if (doc.RootElement.TryGetProperty("WebSearchCitations", out var citProp) && citProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var c in citProp.EnumerateArray())
                                {
                                    var str = c.GetString();
                                    if (!string.IsNullOrWhiteSpace(str)) existingCitations.Add(str);
                                }
                            }
                        }
                        catch { }
                    }

                    teachingStepEntity.StepDataJson = JsonSerializer.Serialize(new
                    {
                        IllustrationId = illustrationId,
                        SpeechStoragePath = storagePath,
                        ProfessorLecture = lectureResult,
                        WebSearchCitations = existingCitations
                    });

                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to synthesize/upload professor lecture audio for section {SectionId}", sectionId);
            }
        }

        return lectureResult;
    }

    private static string ResolveLanguageCode(string? configSnapshotJson, string sampleText)
    {
        if (!string.IsNullOrWhiteSpace(configSnapshotJson))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<JsonElement>(configSnapshotJson);
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

        return DetectLanguageCode(sampleText);
    }

    private static string DetectLanguageCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "es";
        }

        var padded = $" {text.ToLowerInvariant()} ";
        string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
        string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu "];

        var englishHits = englishWords.Count(w => padded.Contains(w));
        var spanishHits = spanishWords.Count(w => padded.Contains(w));

        return englishHits > spanishHits ? "en" : "es";
    }

    private sealed class HypothesisQuestionData
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = [];
        public int? CorrectIndex { get; set; }
    }

    public static string FormatHypothesisIntuitions(
        List<StructuredLearningHypothesisResponse> responses,
        string? stepDataJson)
    {
        List<HypothesisQuestionData> questions = [];
        if (!string.IsNullOrWhiteSpace(stepDataJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(stepDataJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    questions = JsonSerializer.Deserialize<List<HypothesisQuestionData>>(stepDataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("HypothesisQuestions", out var arrayProp))
                {
                    questions = JsonSerializer.Deserialize<List<HypothesisQuestionData>>(arrayProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                }
            }
            catch { }
        }

        var parts = new List<string>();
        foreach (var r in responses)
        {
            var qText = (r.QuestionIndex >= 0 && r.QuestionIndex < questions.Count)
                ? questions[r.QuestionIndex].Question
                : $"Question {r.QuestionIndex + 1}";

            var optText = (r.QuestionIndex >= 0 && r.QuestionIndex < questions.Count &&
                           r.SelectedOptionIndex >= 0 && r.SelectedOptionIndex < questions[r.QuestionIndex].Options.Count)
                ? questions[r.QuestionIndex].Options[r.SelectedOptionIndex]
                : $"Option {r.SelectedOptionIndex}";

            var correctness = r.IsCorrect.HasValue
                ? (r.IsCorrect.Value ? "Correct prediction" : "Incorrect initial guess")
                : "Submitted intuition";

            parts.Add($"{qText} -> Selected: \"{optText}\" ({correctness})");
        }

        return string.Join("; ", parts);
    }
}
