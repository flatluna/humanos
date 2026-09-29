using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using HumanOS.Storage;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Structured Learning's "Edit mode" review service (2026-08-10) — mirrors
/// <see cref="BlueprintReviewService"/> (Graph's equivalent) exactly in
/// spirit: lets a reviewer tweak an already-generated section's step
/// Content or Deep Dive cards via a free-text instruction before the
/// document is published. Never touches a real student's progress (no
/// session/attempt state involved) — pre-publish review only, same as
/// Graph's own review tool. Deliberately simpler than Graph's version — no
/// illustration regeneration yet (plain text edits only).
/// </summary>
public sealed class StructuredLearningReviewService
{
    private readonly StructuredLearningStepEditorAgent _stepAgent;
    private readonly StructuredLearningExampleCardEditorAgent _cardAgent;
    private readonly GraphIllustrationImageService _imageService;
    private readonly CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly AzureSpeechService _speechService;
    private readonly PerplexitySearchService _perplexityService;
    private readonly CapabilityEmbeddingService _embeddingService;
    private readonly StructuredLearningEntityExtractionService _entityExtraction;
    private readonly StructuredLearningConnectionExtractionService _connectionExtraction;
    private readonly StructuredLearningKnowledgeFactGenerationService _knowledgeFactGeneration;
    private readonly StructuredLearningGenerationUsageService _usageService;

    public StructuredLearningReviewService(
        StructuredLearningStepEditorAgent stepAgent,
        StructuredLearningExampleCardEditorAgent cardAgent,
        GraphIllustrationImageService imageService,
        CapabilityGraphIllustrationStorageService illustrationStorage,
        AzureSpeechService speechService,
        PerplexitySearchService perplexityService,
        CapabilityEmbeddingService embeddingService,
        StructuredLearningEntityExtractionService entityExtraction,
        StructuredLearningConnectionExtractionService connectionExtraction,
        StructuredLearningKnowledgeFactGenerationService knowledgeFactGeneration,
        StructuredLearningGenerationUsageService usageService)
    {
        _stepAgent = stepAgent;
        _cardAgent = cardAgent;
        _imageService = imageService;
        _illustrationStorage = illustrationStorage;
        _speechService = speechService;
        _perplexityService = perplexityService;
        _embeddingService = embeddingService;
        _entityExtraction = entityExtraction;
        _connectionExtraction = connectionExtraction;
        _knowledgeFactGeneration = knowledgeFactGeneration;
        _usageService = usageService;
    }

    public sealed class StepResult
    {
        public Guid StructuredLearningSectionStepId { get; set; }
        public StructuredLearningStepType StepType { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? StepDataJson { get; set; }
        public bool IsHumanApproved { get; set; }
        public DateTime? HumanApprovedAtUtc { get; set; }
        public List<string> WebSearchCitations { get; set; } = [];
        public string? WebSearchSnippet { get; set; }
    }

    /// <summary>Applies a reviewer's free-text instruction to ONE step's
    /// Content — only valid for Hypothesis/Teaching/Production (DeepDive is
    /// edited card-by-card instead; Recall/Assessment have no static
    /// Content to edit — their questions are generated live). A fresh AI
    /// edit always resets any prior human sign-off — it needs its own
    /// review.</summary>
    public async Task<StepResult> EditStepContentAsync(
        HumanOsDbContext dbContext, Guid sectionId, StructuredLearningStepType stepType, string instruction, bool usePerplexitySearch = false, CancellationToken cancellationToken = default)
    {
        if (stepType is StructuredLearningStepType.DeepDive or StructuredLearningStepType.Recall or StructuredLearningStepType.Assessment)
        {
            throw new InvalidOperationException(
                $"StepType {stepType} has no static Content to edit this way — DeepDive is edited card-by-card, Recall/Assessment are generated live during practice.");
        }

        var section = await dbContext.StructuredLearningDocumentSections
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

        var step = section.Steps.FirstOrDefault(s => s.StepType == stepType)
            ?? throw new InvalidOperationException($"Section {sectionId} has no {stepType} step.");

        string? perplexityFindings = null;
        List<string> citations = [];
        if (usePerplexitySearch)
        {
            if (!_perplexityService.IsConfigured)
            {
                throw new InvalidOperationException("PerplexityApiKey no está configurada en local.settings.json. Por favor añade tu clave de Perplexity.");
            }

            try
            {
                var searchQuery = $"{section.ChapterTitle}: {section.SubchapterTitle}. {instruction}";
                var searchResult = await _perplexityService.SearchAsync(searchQuery, cancellationToken);
                if (!string.IsNullOrWhiteSpace(searchResult.Answer))
                {
                    perplexityFindings = searchResult.Answer;
                    citations = searchResult.Citations;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error consultando Perplexity Search: {ex.Message}", ex);
            }
        }

        var prompt = BuildStepEditPrompt(section, stepType, step.Content, instruction, perplexityFindings);
        var editStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var (result, editUsage) = await _stepAgent.EditStepWithUsageAsync(prompt, cancellationToken);
        editStopwatch.Stop();
        try
        {
            await _usageService.RecordAsync(
                section.StructuredLearningDocumentId, sectionId, "Edit", editUsage.AgentName, editUsage.ModelName,
                editUsage.InputTokens, editUsage.OutputTokens, editUsage.CachedInputTokens,
                editStopwatch.ElapsedMilliseconds, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Usage logging must never break the actual edit it measures.
        }

        step.Content = result.UpdatedContent;
        step.IsHumanApproved = false;
        step.HumanApprovedAtUtc = null;

        if (!string.IsNullOrWhiteSpace(result.IllustrationPrompt))
        {
            var purpose = stepType switch
            {
                StructuredLearningStepType.Hypothesis => StructuredLearningIllustrationPurpose.Hypothesis,
                StructuredLearningStepType.Teaching => StructuredLearningIllustrationPurpose.Teaching,
                _ => StructuredLearningIllustrationPurpose.Teaching
            };

            var newIllustrationId = await TryGenerateIllustrationAsync(
                dbContext, section, result.IllustrationPrompt, section.SubchapterTitle, cancellationToken, purpose);

            if (newIllustrationId is not null)
            {
                step.StepDataJson = JsonSerializer.Serialize(new { IllustrationId = newIllustrationId });
            }
        }

        // Regenerate and replace speech audio if Teaching content changed
        if (stepType == StructuredLearningStepType.Teaching)
        {
            await RegenerateTeachingAudioAsync(dbContext, section, step, citations, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (stepType == StructuredLearningStepType.Teaching)
        {
            await ResyncKnowledgeGraphAfterTeachingEditAsync(dbContext, section, step.Content, cancellationToken);
        }

        var finalResult = ToStepResult(step);
        finalResult.WebSearchCitations = citations;
        finalResult.WebSearchSnippet = perplexityFindings;
        return finalResult;
    }

    /// <summary>Directly persists a reviewer's own hand-edited Content for
    /// ONE step — no AI call at all, and marks it as human-approved (the
    /// caller MUST have already obtained the reviewer's explicit
    /// confirmation that they read it and take responsibility for it,
    /// since the AI is not perfect and can alter or invent content). Same
    /// StepType restriction as <see cref="EditStepContentAsync"/>.</summary>
    public async Task<StepResult> SetStepContentManuallyAsync(
        HumanOsDbContext dbContext, Guid sectionId, StructuredLearningStepType stepType, string content, CancellationToken cancellationToken = default)
    {
        if (stepType is StructuredLearningStepType.DeepDive or StructuredLearningStepType.Recall or StructuredLearningStepType.Assessment)
        {
            throw new InvalidOperationException(
                $"StepType {stepType} has no static Content to edit this way — DeepDive is edited card-by-card, Recall/Assessment are generated live during practice.");
        }

        var section = await dbContext.StructuredLearningDocumentSections
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

        var step = section.Steps.FirstOrDefault(s => s.StepType == stepType)
            ?? throw new InvalidOperationException($"Section {sectionId} has no {stepType} step.");

        step.Content = content;
        step.IsHumanApproved = true;
        step.HumanApprovedAtUtc = DateTime.UtcNow;

        // Regenerate and replace speech audio if Teaching content was edited manually
        if (stepType == StructuredLearningStepType.Teaching)
        {
            await RegenerateTeachingAudioAsync(dbContext, section, step, citations: null, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (stepType == StructuredLearningStepType.Teaching)
        {
            await ResyncKnowledgeGraphAfterTeachingEditAsync(dbContext, section, step.Content, cancellationToken);
        }

        return ToStepResult(step);
    }

    /// <summary>Always runs after ANY Teaching edit (AI or manual) — no opt-in, per
    /// explicit product decision: leaving the Knowledge Graph/Facts derived from the OLD
    /// text would silently drift out of sync with what the section now actually teaches.
    /// Syncs the section's own canonical Text (source of truth for embeddings/Knowledge
    /// Graph/Knowledge Facts extraction) to the edited Teaching Content, then fully
    /// deletes and re-extracts entities/connections/facts for this section from it.
    /// Best-effort: a failure here must never undo the text/audio edit that already
    /// succeeded and was saved.</summary>
    private async Task ResyncKnowledgeGraphAfterTeachingEditAsync(
        HumanOsDbContext dbContext, StructuredLearningDocumentSection section, string newTeachingContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newTeachingContent))
        {
            return;
        }

        try
        {
            section.Text = newTeachingContent;
            section.TotalCharacters = newTeachingContent.Length;
            if (_embeddingService.IsConfigured)
            {
                section.Embedding = new Microsoft.Data.SqlTypes.SqlVector<float>(
                    await _embeddingService.GenerateEmbeddingAsync(newTeachingContent, cancellationToken));
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            if (_entityExtraction.IsConfigured)
            {
                await _entityExtraction.ExtractForSectionAsync(section.StructuredLearningDocumentSectionId, cancellationToken);
            }
            if (_connectionExtraction.IsConfigured)
            {
                await _connectionExtraction.ExtractForSectionAsync(section.StructuredLearningDocumentSectionId, cancellationToken);
            }
            if (_knowledgeFactGeneration.IsConfigured)
            {
                await _knowledgeFactGeneration.GenerateValidateAndPersistAsync(section.StructuredLearningDocumentSectionId, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — same graceful-skip rationale as StructuredLearningSectionAdditionService's own Knowledge Graph calls.
        }
    }

    private async Task RegenerateTeachingAudioAsync(
        HumanOsDbContext dbContext,
        StructuredLearningDocumentSection section,
        StructuredLearningSectionStep step,
        List<string>? citations,
        CancellationToken cancellationToken)
    {
        if (!_speechService.IsConfigured || !_illustrationStorage.IsConfigured || string.IsNullOrWhiteSpace(step.Content))
        {
            return;
        }

        try
        {
            // Detect language
            var padded = $" {step.Content.ToLowerInvariant()} ";
            string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this "];
            string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu "];
            var isEnglish = englishWords.Count(w => padded.Contains(w)) > spanishWords.Count(w => padded.Contains(w));
            var voiceName = isEnglish ? "en-US-AndrewNeural" : "es-MX-CecilioNeural";

            var audioBytes = await _speechService.SynthesizeAsync(step.Content, voiceName, cancellationToken);
            using var audioStream = new MemoryStream(audioBytes);

            var storagePath = await _illustrationStorage.UploadLectureAudioAsync(
                section.TenantId,
                section.StructuredLearningDocumentId,
                section.StructuredLearningDocumentSectionId,
                audioStream,
                contentType: "audio/mpeg",
                cancellationToken: cancellationToken);

            // Update StepDataJson with the speech storage path while preserving illustration id
            Guid? illustrationId = null;
            if (!string.IsNullOrWhiteSpace(step.StepDataJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(step.StepDataJson);
                    if (doc.RootElement.TryGetProperty("IllustrationId", out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        if (Guid.TryParse(prop.GetString(), out var parsedId)) illustrationId = parsedId;
                    }
                }
                catch { }
            }

            step.StepDataJson = JsonSerializer.Serialize(new
            {
                IllustrationId = illustrationId,
                SpeechStoragePath = storagePath,
                WebSearchCitations = citations ?? []
            });
        }
        catch
        {
            // Best-effort: synthesis failure does not prevent saving text changes.
        }
    }

    public static StructuredLearningExampleCardDto CreateInstructionAlignedCard(string instruction)
    {
        var trimmed = (instruction ?? string.Empty).Trim();
        var normalized = Regex.Replace(trimmed, @"\s+", " ");
        var subject = string.IsNullOrWhiteSpace(normalized) ? "Lesson concept" : normalized.Trim('.', ' ', ';', ':');

        var title = BuildInstructionTitle(subject);
        var text = $"The requested scene is: {subject}. This card focuses on that exact idea and describes it directly.";
        var illustrationPrompt = $"A clear educational illustration of {subject}. Single focal scene, realistic learning setting, no text, no logo, no watermark, clean composition.";

        return new StructuredLearningExampleCardDto
        {
            Title = title,
            Text = text,
            Emoji = null,
            IllustrationPrompt = illustrationPrompt,
            IllustrationId = null
        };
    }

    private static EditStructuredLearningExampleCardResponse ToAgentResponse(StructuredLearningExampleCardDto card)
    {
        return new EditStructuredLearningExampleCardResponse
        {
            Title = card.Title,
            Text = card.Text,
            Emoji = card.Emoji,
            IllustrationPrompt = card.IllustrationPrompt
        };
    }

    public async Task<StepResult> EditExampleCardAsync(
        HumanOsDbContext dbContext, Guid sectionId, int cardIndex, string instruction, CancellationToken cancellationToken = default)
    {
        var (section, step, cards) = await LoadDeepDiveStepAsync(dbContext, sectionId, cancellationToken);
        if (cardIndex < 0 || cardIndex >= cards.Count)
        {
            throw new InvalidOperationException($"Card index {cardIndex} is out of range (this step has {cards.Count} cards).");
        }

        var current = cards[cardIndex];
        var directCard = ShouldUseDirectInstructionCard(instruction)
            ? CreateInstructionAlignedCard(instruction)
            : null;

        var result = directCard is not null
            ? ToAgentResponse(directCard)
            : await _cardAgent.EditCardAsync(BuildCardEditPrompt(section, cards, cardIndex, current, instruction), cancellationToken);

        var illustrationPrompt = string.IsNullOrWhiteSpace(result.IllustrationPrompt)
            ? BuildFallbackIllustrationPrompt(result.Title, result.Text)
            : result.IllustrationPrompt;

        var newIllustrationId = await TryGenerateIllustrationAsync(
            dbContext, section, illustrationPrompt, result.Title, cancellationToken);

        cards[cardIndex] = new StructuredLearningExampleCardDto
        {
            Title = result.Title,
            Text = result.Text,
            Emoji = result.Emoji,
            IllustrationPrompt = illustrationPrompt,
            IllustrationId = newIllustrationId ?? current.IllustrationId
        };

        step.StepDataJson = JsonSerializer.Serialize(cards);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToStepResult(step);
    }

    public async Task<StepResult> AddExampleCardAsync(
        HumanOsDbContext dbContext, Guid sectionId, string instruction, CancellationToken cancellationToken = default)
    {
        var (section, step, cards) = await LoadDeepDiveStepAsync(dbContext, sectionId, cancellationToken);

        var directCard = ShouldUseDirectInstructionCard(instruction)
            ? CreateInstructionAlignedCard(instruction)
            : null;

        var result = directCard is not null
            ? ToAgentResponse(directCard)
            : await _cardAgent.EditCardAsync(BuildCardEditPrompt(section, cards, cardIndex: null, current: null, instruction), cancellationToken);

        var illustrationPrompt = string.IsNullOrWhiteSpace(result.IllustrationPrompt)
            ? BuildFallbackIllustrationPrompt(result.Title, result.Text)
            : result.IllustrationPrompt;

        var newIllustrationId = await TryGenerateIllustrationAsync(
            dbContext, section, illustrationPrompt, result.Title, cancellationToken);

        cards.Add(new StructuredLearningExampleCardDto
        {
            Title = result.Title,
            Text = result.Text,
            Emoji = result.Emoji,
            IllustrationPrompt = illustrationPrompt,
            IllustrationId = newIllustrationId
        });

        step.StepDataJson = JsonSerializer.Serialize(cards);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToStepResult(step);
    }

    public async Task<StepResult> DeleteExampleCardAsync(
        HumanOsDbContext dbContext, Guid sectionId, int cardIndex, CancellationToken cancellationToken = default)
    {
        var (_, step, cards) = await LoadDeepDiveStepAsync(dbContext, sectionId, cancellationToken);
        if (cardIndex < 0 || cardIndex >= cards.Count)
        {
            throw new InvalidOperationException($"Card index {cardIndex} is out of range (this step has {cards.Count} cards).");
        }

        cards.RemoveAt(cardIndex);
        step.StepDataJson = JsonSerializer.Serialize(cards);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToStepResult(step);
    }

    private static async Task<(StructuredLearningDocumentSection Section, StructuredLearningSectionStep Step, List<StructuredLearningExampleCardDto> Cards)> LoadDeepDiveStepAsync(
        HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var section = await dbContext.StructuredLearningDocumentSections
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

        var step = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.DeepDive)
            ?? throw new InvalidOperationException($"Section {sectionId} has no Deep Dive step.");

        var cards = string.IsNullOrWhiteSpace(step.StepDataJson)
            ? []
            : JsonSerializer.Deserialize<List<StructuredLearningExampleCardDto>>(step.StepDataJson) ?? [];

        return (section, step, cards);
    }

    private async Task<Guid?> TryGenerateIllustrationAsync(
        HumanOsDbContext dbContext,
        StructuredLearningDocumentSection section,
        string illustrationPrompt,
        string caption,
        CancellationToken cancellationToken,
        StructuredLearningIllustrationPurpose purpose = StructuredLearningIllustrationPurpose.DeepDiveCard)
    {
        if (string.IsNullOrWhiteSpace(illustrationPrompt) || !_imageService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            return null;
        }

        try
        {
            var imageIndex = await dbContext.StructuredLearningIllustrations
                .Where(i => i.StructuredLearningDocumentSectionId == section.StructuredLearningDocumentSectionId)
                .CountAsync(cancellationToken);

            var generated = await _imageService.GenerateAsync(illustrationPrompt, cancellationToken: cancellationToken);
            using var stream = generated.ImageBytes.ToStream();
            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                section.TenantId,
                section.StructuredLearningDocumentSectionId,
                section.StructuredLearningDocumentSectionId,
                imageIndex + 1,
                stream,
                cancellationToken: cancellationToken);

            var illustration = new StructuredLearningIllustration
            {
                StructuredLearningIllustrationId = Guid.NewGuid(),
                StructuredLearningDocumentSectionId = section.StructuredLearningDocumentSectionId,
                StoragePath = storagePath,
                Prompt = illustrationPrompt,
                Caption = caption,
                Purpose = purpose,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            dbContext.StructuredLearningIllustrations.Add(illustration);
            await dbContext.SaveChangesAsync(cancellationToken);
            return illustration.StructuredLearningIllustrationId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static bool ShouldUseDirectInstructionCard(string instruction)
    {
        var trimmed = (instruction ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return false;

        var normalized = Regex.Replace(trimmed, @"\s+", " ");
        if (normalized.Length > 220) return false;

        var lower = normalized.ToLowerInvariant();
        return !lower.Contains("how")
            && !lower.Contains("compare")
            && !lower.Contains("explain")
            && !lower.Contains("analyze")
            && !lower.Contains("summarize")
            && !lower.Contains("what is")
            && !lower.Contains("why")
            && !lower.Contains("when")
            && !lower.Contains("list");
    }

    private static string BuildInstructionTitle(string subject)
    {
        var cleaned = Regex.Replace(subject, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return "Lesson Concept";

        return cleaned.Length <= 50
            ? cleaned
            : string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(8));
    }

    private static string BuildFallbackIllustrationPrompt(string title, string text)
    {
        var cleanTitle = title.Trim();
        var cleanText = text.Trim();
        var subject = string.IsNullOrWhiteSpace(cleanTitle)
            ? (string.IsNullOrWhiteSpace(cleanText) ? "the lesson concept" : cleanText)
            : cleanTitle;

        return $"A clear educational illustration showing {subject}. " +
               "Single focal scene, realistic and polished, professional learning app style, no text, no letters, no numbers, no words in the image, clean composition.";
    }

    private static StepResult ToStepResult(StructuredLearningSectionStep step) => new()
    {
        StructuredLearningSectionStepId = step.StructuredLearningSectionStepId,
        StepType = step.StepType,
        Content = step.Content,
        StepDataJson = step.StepDataJson,
        IsHumanApproved = step.IsHumanApproved,
        HumanApprovedAtUtc = step.HumanApprovedAtUtc
    };

    private static string BuildStepEditPrompt(
        StructuredLearningDocumentSection section, StructuredLearningStepType stepType, string currentContent, string instruction, string? perplexityFindings = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Chapter: {section.ChapterTitle}");
        builder.AppendLine($"Section: {section.SubchapterTitle}");
        builder.AppendLine("SECTION SOURCE TEXT (only source of truth):");
        builder.AppendLine(section.Text);
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(perplexityFindings))
        {
            builder.AppendLine("=== PERPLEXITY AI REAL-TIME WEB SEARCH FINDINGS ===");
            builder.AppendLine(perplexityFindings.Trim());
            builder.AppendLine("=================================================");
            builder.AppendLine();
        }

        builder.AppendLine($"STEP TYPE: {stepType}");
        builder.AppendLine("CURRENT CONTENT:");
        builder.AppendLine(currentContent);
        builder.AppendLine();
        builder.AppendLine($"REVIEWER INSTRUCTION: {instruction}");

        if (stepType == StructuredLearningStepType.Teaching)
        {
            builder.AppendLine();
            builder.AppendLine("CRITICAL TEACHING STEP AUDIO & UNIVERSAL DELIVERY REQUIREMENTS:");
            builder.AppendLine("- This Teaching step is delivered as a spoken masterclass lecture for all students and converted directly into audio via Text-to-Speech (TTS).");
            builder.AppendLine("- The lecture is universal and generic: NEVER include individual personal names (NO 'Alejandro', NO 'Student', NO 'Learner').");
            builder.AppendLine("- Address the class warmly and universally (e.g., 'Hola y bienvenidos a esta clase...', 'Hello and welcome to this lecture...').");
            builder.AppendLine("- Write in pure spoken paragraphs suitable for automated voice audio (NO markdown headers #, NO bullet points *, NO brackets).");
        }

        return builder.ToString();
    }

    private static string BuildCardEditPrompt(
        StructuredLearningDocumentSection section,
        List<StructuredLearningExampleCardDto> cards,
        int? cardIndex,
        StructuredLearningExampleCardDto? current,
        string instruction)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Chapter: {section.ChapterTitle}");
        builder.AppendLine($"Section: {section.SubchapterTitle}");
        builder.AppendLine("SECTION SOURCE TEXT (only source of truth):");
        builder.AppendLine(section.Text);
        builder.AppendLine();

        var otherTitles = cards
            .Where((_, i) => i != cardIndex)
            .Select(c => c.Title)
            .ToList();
        if (otherTitles.Count > 0)
        {
            builder.AppendLine("OTHER EXISTING CARD TITLES (for context/dedup):");
            foreach (var title in otherTitles) builder.AppendLine($"- {title}");
            builder.AppendLine();
        }

        if (current is not null)
        {
            builder.AppendLine("MODE: EDIT");
            builder.AppendLine($"CURRENT CARD TITLE: {current.Title}");
            builder.AppendLine($"CURRENT CARD TEXT: {current.Text}");
            builder.AppendLine($"CURRENT CARD EMOJI: {current.Emoji ?? "(none)"}");
        }
        else
        {
            builder.AppendLine("MODE: ADD (no current card — invent a brand-new one)");
        }
        builder.AppendLine();
        builder.AppendLine("CRITICAL PRIORITY RULE: the reviewer's instruction is the highest-priority requirement for this card.");
        builder.AppendLine("Treat the reviewer's instruction as the exact card topic. The card must follow that topic even if the overall section discusses something broader or different.");
        builder.AppendLine("Do not default to generic section-language summaries. If the reviewer provides a concrete scene or subject, this card should be about that concrete scene, not a general overview of the chapter.");
        builder.AppendLine("Keep the card simple and literal: short title, one clear sentence of text, and one matching illustration scene.");
        builder.AppendLine("The instruction may be about any topic (math, science, law, history, sign language, technology, etc.) — never assume a fixed default domain.");
        builder.AppendLine("OUTPUT FORMAT EXPECTED: 1) Title with the exact subject, 2) Text describing that exact scene, 3) IllustrationPrompt matching the same subject and scene.");
        builder.AppendLine($"REVIEWER INSTRUCTION: {instruction}");
        return builder.ToString();
    }
}
