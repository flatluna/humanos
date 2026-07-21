using System.Globalization;
using HumanOS.Agents.Studio;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Services;

/// <summary>
/// Converts a PDF pipeline run's <see cref="AgentTokenUsage"/> list +
/// illustration count into an estimated USD cost.
///
/// IMPORTANT: the default rates below are PLACEHOLDERS, not official Azure
/// OpenAI pricing for the "gpt-5-chat" / "gpt-image-2" deployments this
/// pipeline actually uses (see local.settings.json) — no public pricing
/// page for those deployment names was available when this was written.
/// They are modeled on comparable flagship-tier chat + image model
/// pricing so the numbers are in the right ballpark, but MUST be
/// overridden with real invoiced rates once known, via the "AgentPricing"
/// section in appsettings.json:
/// <code>
/// "AgentPricing": {
///   "InputPerMillionTokens": "2.50",
///   "CachedInputPerMillionTokens": "1.25",
///   "OutputPerMillionTokens": "10.00",
///   "PerImage": "0.04"
/// }
/// </code>
/// See /memories/repo/pdf-pipeline-token-usage-tracking.md.
/// </summary>
public static class TokenCostEstimator
{
    private const decimal DefaultInputPerMillion = 2.50m;
    private const decimal DefaultCachedInputPerMillion = 1.25m;
    private const decimal DefaultOutputPerMillion = 10.00m;
    private const decimal DefaultImageCost = 0.04m;

    public static PdfCapabilityCostEstimate Estimate(
        IReadOnlyList<AgentTokenUsage> tokenUsage,
        int illustrationsGeneratedCount,
        IConfiguration configuration)
    {
        var inputRate = ReadDecimal(configuration, "AgentPricing:InputPerMillionTokens", DefaultInputPerMillion);
        var cachedRate = ReadDecimal(configuration, "AgentPricing:CachedInputPerMillionTokens", DefaultCachedInputPerMillion);
        var outputRate = ReadDecimal(configuration, "AgentPricing:OutputPerMillionTokens", DefaultOutputPerMillion);
        var imageRate = ReadDecimal(configuration, "AgentPricing:PerImage", DefaultImageCost);

        var cachedInput = tokenUsage.Sum(u => (long)u.CachedInputTokens);
        var totalInput = tokenUsage.Sum(u => (long)u.InputTokens);
        var billableInput = Math.Max(0, totalInput - cachedInput);
        var totalOutput = tokenUsage.Sum(u => (long)u.OutputTokens);

        var inputCost = billableInput / 1_000_000m * inputRate;
        var cachedCost = cachedInput / 1_000_000m * cachedRate;
        var outputCost = totalOutput / 1_000_000m * outputRate;
        var imageCost = illustrationsGeneratedCount * imageRate;

        return new PdfCapabilityCostEstimate
        {
            BillableInputTokens = billableInput,
            CachedInputTokens = cachedInput,
            OutputTokens = totalOutput,
            InputCostUsd = Math.Round(inputCost, 4),
            CachedInputCostUsd = Math.Round(cachedCost, 4),
            OutputCostUsd = Math.Round(outputCost, 4),
            ImageCostUsd = Math.Round(imageCost, 4),
            TotalCostUsd = Math.Round(inputCost + cachedCost + outputCost + imageCost, 4)
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
/// class's doc comment for the "these are placeholder rates" caveat.</summary>
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
