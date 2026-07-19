using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// One named quality metric (0-100) produced by <c>BlueprintValidatorAgent</c>
/// for a <see cref="BlueprintValidation"/> run — e.g. "IllustrationCoverage" = 100.
/// Kept as a name/value row pair (rather than fixed columns) so new metric
/// names can be introduced by the agent's prompt without a schema migration.
/// </summary>
public class BlueprintValidationMetric
{
    /// <summary>Identificador único de la métrica (GUID).</summary>
    public Guid BlueprintValidationMetricId { get; set; } = Guid.NewGuid();

    /// <summary>FK: BlueprintValidation al que pertenece esta métrica.</summary>
    public Guid BlueprintValidationId { get; set; }

    /// <summary>Nombre de la métrica, p.ej. "IllustrationCoverage", "RecallStrength".</summary>
    public string MetricName { get; set; } = string.Empty;

    /// <summary>Valor de la métrica, 0-100.</summary>
    public int MetricValue { get; set; }

    // === Navigation Properties ===

    /// <summary>Referencia al BlueprintValidation padre.</summary>
    public virtual BlueprintValidation? BlueprintValidation { get; set; }
}
