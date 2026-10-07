using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Records and rolls up the ACTUAL cost of students USING "Q&amp;A with
/// Professor" (2026-10-01) — distinct from StructuredLearningCostService,
/// which prices AUTHORING an engram (one-time generation), not every
/// student's later use of it. One row per real turn (see
/// <see cref="StructuredLearningEngramRuntimeUsage"/>), so cost can be
/// rolled up by Person/Tenant/Document(course)/Section(engram) — the admin
/// module reads <see cref="GetSummaryAsync"/> for that, this class is the
/// foundation (recording + rollup) built first per explicit request.
/// </summary>
public sealed class StructuredLearningEngramUsageService
{
    private const decimal DefaultRealtimePerMinuteUsd = 0.045m;
    private const decimal DefaultAzureSpeechNeuralPerMillionCharactersUsd = 16m;

    private readonly HumanOsDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public StructuredLearningEngramUsageService(HumanOsDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    /// <summary>Logs one voice "Q&A with Professor" call — duration measured
    /// client-side (the backend never sees the live WebRTC audio stream),
    /// same pattern as BuddySessionUsage.</summary>
    public async Task RecordVoiceUsageAsync(
        Guid? personId,
        Guid? documentId,
        Guid? sectionId,
        Guid? sessionId,
        string? modelName,
        int durationSeconds,
        int inputTokens,
        int outputTokens,
        CancellationToken cancellationToken,
        string? stepType = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        if (durationSeconds <= 0 && inputTokens <= 0 && outputTokens <= 0) return;

        AddUsage(personId, documentId, sectionId, sessionId, "Voice", modelName, stepType,
            inputTokens, outputTokens, durationSeconds);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Logs one text "Q&A with Professor" turn — real token counts
    /// read straight from the Agent's own response.Usage.</summary>
    public async Task RecordTextUsageAsync(
        Guid? personId,
        Guid? documentId,
        Guid? sectionId,
        Guid? sessionId,
        string? modelName,
        int inputTokens,
        int outputTokens,
        CancellationToken cancellationToken,
        string? stepType = null)
    {
        AddUsage(personId, documentId, sectionId, sessionId, "Text", modelName, stepType,
            inputTokens, outputTokens, 0);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordTextToSpeechUsageAsync(
        Guid? personId,
        Guid? documentId,
        Guid? sectionId,
        Guid? sessionId,
        string? voiceName,
        int characterCount,
        CancellationToken cancellationToken,
        string? stepType = "Production")
    {
        ArgumentOutOfRangeException.ThrowIfNegative(characterCount);
        if (characterCount == 0) return;

        AddUsage(personId, documentId, sectionId, sessionId, "TextToSpeech", voiceName, stepType,
            0, 0, 0, characterCount);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Append-only ledger: simultaneous calls cannot overwrite an
    /// accumulated total, and each event retains its own engram and step.</summary>
    private void AddUsage(
        Guid? personId,
        Guid? documentId,
        Guid? sectionId,
        Guid? sessionId,
        string channel,
        string? modelName,
        string? stepType, int inputTokens, int outputTokens, int durationSeconds, int characterCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);
        var usage = new StructuredLearningEngramRuntimeUsage
        {
            PersonId = personId,
            StructuredLearningDocumentId = documentId,
            StructuredLearningDocumentSectionId = sectionId,
            StructuredLearningEngramSessionId = sessionId,
            Channel = channel,
            ModelName = modelName,
            StepType = stepType,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            CharacterCount = characterCount,
            DurationSeconds = durationSeconds,
            CreatedDate = DateTime.UtcNow,
        };
        _dbContext.StructuredLearningEngramRuntimeUsages.Add(usage);
    }

    /// <summary>Rolls up estimated cost for any combination of scope
    /// filters — pass only the ones you want to narrow by (e.g. just
    /// sectionId for one engram, just tenantId for a whole tenant).
    /// tenantId is resolved via a join to Person (same approach as
    /// TenantBuddyUsageService — TenantId is never duplicated onto the
    /// usage row itself).</summary>
    public async Task<StructuredLearningEngramUsageSummaryResponse> GetSummaryAsync(
        Guid? tenantId = null,
        Guid? personId = null,
        Guid? documentId = null,
        Guid? sectionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.StructuredLearningEngramRuntimeUsages.AsNoTracking().AsQueryable();

        if (tenantId.HasValue)
        {
            query = query.Where(u => _dbContext.People.Any(p => p.PersonId == u.PersonId && p.TenantId == tenantId.Value));
        }
        if (personId.HasValue) query = query.Where(u => u.PersonId == personId.Value);
        if (documentId.HasValue) query = query.Where(u => u.StructuredLearningDocumentId == documentId.Value);
        if (sectionId.HasValue) query = query.Where(u => u.StructuredLearningDocumentSectionId == sectionId.Value);

        var rows = await query.ToListAsync(cancellationToken);

        var voiceRows = rows.Where(r => r.Channel == "Voice").ToList();
        var textRows = rows.Where(r => r.Channel == "Text").ToList();
        var speechRows = rows.Where(r => r.Channel == "TextToSpeech").ToList();

        var perMinuteRate = ReadDecimal("AgentPricing:RealtimePerMinuteUsd", DefaultRealtimePerMinuteUsd);
        var voiceTotalSeconds = voiceRows.Sum(r => r.DurationSeconds);
        var voiceCost = Math.Round((decimal)voiceTotalSeconds / 60m * perMinuteRate, 4);

        var textUsage = textRows
            .Select(r => new AgentTokenUsage
            {
                AgentName = "StructuredLearningQaProfessorAgent",
                ModelName = r.ModelName ?? string.Empty,
                InputTokens = r.InputTokens,
                OutputTokens = r.OutputTokens,
            })
            .ToList();
        var textEstimate = TokenCostEstimator.Estimate(textUsage, illustrationsGeneratedCount: 0, _configuration);
        var speechRate = ReadDecimal("AgentPricing:AzureSpeechNeuralPerMillionCharactersUsd", DefaultAzureSpeechNeuralPerMillionCharactersUsd);
        var speechCost = Math.Round(speechRows.Sum(r => (decimal)r.CharacterCount) / 1_000_000m * speechRate, 4);

        return new StructuredLearningEngramUsageSummaryResponse
        {
            VoiceSessionCount = voiceRows.Count(r => r.DurationSeconds > 0),
            VoiceTotalDurationSeconds = voiceTotalSeconds,
            VoiceEstimatedCostUsd = voiceCost,
            TextTurnCount = textRows.Count,
            TextInputTokens = textRows.Sum(r => (long)r.InputTokens),
            TextOutputTokens = textRows.Sum(r => (long)r.OutputTokens),
            TextEstimatedCostUsd = textEstimate.TotalCostUsd,
            TextToSpeechCharacterCount = speechRows.Sum(r => (long)r.CharacterCount),
            TextToSpeechEstimatedCostUsd = speechCost,
            TotalEstimatedCostUsd = Math.Round(voiceCost + textEstimate.TotalCostUsd + speechCost, 4),
        };
    }

    private decimal ReadDecimal(string key, decimal fallback)
    {
        var raw = _configuration[key];
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
