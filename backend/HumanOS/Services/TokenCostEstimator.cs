using System.Globalization;
using HumanOS.Agents.Studio;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Converts a PDF pipeline run's <see cref="AgentTokenUsage"/> list +
/// illustration count into an estimated USD cost.
///
/// PER-MODEL RATES (2026-07-23): different agents deliberately call
/// different Azure OpenAI deployments — economy tier (Curador,
/// DocumentContext -> "gpt4mini"/gpt-4o-mini) vs. main tier
/// (GraphArchitect, ExperienceDesigner, BlueprintValidator, IdeaToDocument
/// -> "gpt-5-chat") — which have very different real per-token pricing.
/// A single flat rate for every call (the original 2026-07-20 design)
/// systematically over/under-estimates cost depending on which agents ran.
/// Rates are read per-model from the "AgentPricing:Models:{modelName}"
/// config section; a model with no matching section falls back to the
/// "AgentPricing:Default*" rates. Configure real rates in appsettings.json:
/// <code>
/// "AgentPricing": {
///   "DefaultInputPerMillionTokens": "1.25",
///   "DefaultCachedInputPerMillionTokens": "0.625",
///   "DefaultOutputPerMillionTokens": "10.00",
///   "PerImage": "0.04",
///   "Models": {
///     "gpt4mini": { "InputPerMillionTokens": "0.15", "CachedInputPerMillionTokens": "0.075", "OutputPerMillionTokens": "0.60" },
///     "gpt-5-chat": { "InputPerMillionTokens": "1.25", "CachedInputPerMillionTokens": "0.625", "OutputPerMillionTokens": "10.00" }
///   }
/// }
/// </code>
/// Cached-input rates are still a PLACEHOLDER (assumed 50% of input,
/// Azure OpenAI's typical prompt-cache discount) — no official cached rate
/// was confirmed for either model when this was written. Image cost
/// (gpt-image-2) also remains a flat per-image placeholder — images
/// aren't billed per-token, so they're tracked separately regardless.
/// See /memories/repo/pdf-pipeline-token-usage-tracking.md.
/// </summary>
public static class TokenCostEstimator
{
    private const decimal DefaultInputPerMillion = 1.25m;
    private const decimal DefaultCachedInputPerMillion = 0.625m;
    private const decimal DefaultOutputPerMillion = 10.00m;
    private const decimal DefaultImageCost = 0.04m;

    public static PdfCapabilityCostEstimate Estimate(
        IReadOnlyList<AgentTokenUsage> tokenUsage,
        int illustrationsGeneratedCount,
        IConfiguration configuration)
    {
        var defaultInputRate = ReadDecimal(configuration, "AgentPricing:DefaultInputPerMillionTokens", DefaultInputPerMillion);
        var defaultCachedRate = ReadDecimal(configuration, "AgentPricing:DefaultCachedInputPerMillionTokens", DefaultCachedInputPerMillion);
        var defaultOutputRate = ReadDecimal(configuration, "AgentPricing:DefaultOutputPerMillionTokens", DefaultOutputPerMillion);
        var imageRate = ReadDecimal(configuration, "AgentPricing:PerImage", DefaultImageCost);

        long totalBillableInput = 0;
        long totalCachedInput = 0;
        long totalOutput = 0;
        decimal totalInputCost = 0m;
        decimal totalCachedCost = 0m;
        decimal totalOutputCost = 0m;

        foreach (var usage in tokenUsage)
        {
            var modelKey = string.IsNullOrWhiteSpace(usage.ModelName) ? null : $"AgentPricing:Models:{usage.ModelName}";

            var inputRate = modelKey is null ? defaultInputRate : ReadDecimal(configuration, $"{modelKey}:InputPerMillionTokens", defaultInputRate);
            var cachedRate = modelKey is null ? defaultCachedRate : ReadDecimal(configuration, $"{modelKey}:CachedInputPerMillionTokens", defaultCachedRate);
            var outputRate = modelKey is null ? defaultOutputRate : ReadDecimal(configuration, $"{modelKey}:OutputPerMillionTokens", defaultOutputRate);

            var cachedInput = (long)usage.CachedInputTokens;
            var billableInput = Math.Max(0, (long)usage.InputTokens - cachedInput);
            var output = (long)usage.OutputTokens;

            totalCachedInput += cachedInput;
            totalBillableInput += billableInput;
            totalOutput += output;

            totalInputCost += billableInput / 1_000_000m * inputRate;
            totalCachedCost += cachedInput / 1_000_000m * cachedRate;
            totalOutputCost += output / 1_000_000m * outputRate;
        }

        var imageCost = illustrationsGeneratedCount * imageRate;

        return new PdfCapabilityCostEstimate
        {
            BillableInputTokens = totalBillableInput,
            CachedInputTokens = totalCachedInput,
            OutputTokens = totalOutput,
            InputCostUsd = Math.Round(totalInputCost, 4),
            CachedInputCostUsd = Math.Round(totalCachedCost, 4),
            OutputCostUsd = Math.Round(totalOutputCost, 4),
            ImageCostUsd = Math.Round(imageCost, 4),
            TotalCostUsd = Math.Round(totalInputCost + totalCachedCost + totalOutputCost + imageCost, 4)
        };
    }

    private static decimal ReadDecimal(IConfiguration configuration, string key, decimal fallback)
    {
        var raw = configuration[key];
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}

/// <summary>Result of <see cref="TokenCostEstimator.Estimate"/> — see that
/// class's doc comment for the per-model-rate design and remaining
/// placeholder caveats (cached-input ratio, per-image cost).</summary>
public sealed class PdfCapabilityCostEstimate
{
    public long BillableInputTokens { get; set; }

    public long CachedInputTokens { get; set; }

    public long OutputTokens { get; set; }

    public decimal InputCostUsd { get; set; }

    public decimal CachedInputCostUsd { get; set; }

    public decimal OutputCostUsd { get; set; }

    public decimal ImageCostUsd { get; set; }

    public decimal TotalCostUsd { get; set; }

    /// <summary>Always true today — see the "PLACEHOLDER" caveat on
    /// <see cref="TokenCostEstimator"/>'s class doc comment.</summary>
    public bool IsEstimate { get; set; } = true;
}
