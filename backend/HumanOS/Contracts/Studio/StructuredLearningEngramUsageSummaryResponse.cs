namespace HumanOS.Contracts.Studio;

/// <summary>Aggregated Voice+Text "Q&A with Professor" runtime usage/cost
/// for one scope (engram/course/person/tenant) — see
/// <see cref="HumanOS.Services.StructuredLearningEngramUsageService"/>.</summary>
public sealed class StructuredLearningEngramUsageSummaryResponse
{
    public int VoiceSessionCount { get; set; }

    public int VoiceTotalDurationSeconds { get; set; }

    public decimal VoiceEstimatedCostUsd { get; set; }

    public int TextTurnCount { get; set; }

    public long TextInputTokens { get; set; }

    public long TextOutputTokens { get; set; }

    public decimal TextEstimatedCostUsd { get; set; }

    public long TextToSpeechCharacterCount { get; set; }

    public decimal TextToSpeechEstimatedCostUsd { get; set; }

    public decimal TotalEstimatedCostUsd { get; set; }
}
