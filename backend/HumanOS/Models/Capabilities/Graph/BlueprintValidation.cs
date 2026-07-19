using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// One quality-gate run of <c>BlueprintValidatorAgent</c> (Paso 4) over a
/// <see cref="NodeExperienceBlueprint"/>. Deliberately APPEND-ONLY — same
/// principle as <c>CapabilityModuleVerification</c> in the old pipeline —
/// no unique constraint on <see cref="NodeExperienceBlueprintId"/>, so a
/// future re-validation (after ExperienceDesigner regenerates a blueprint)
/// adds a new row instead of overwriting history.
///
/// ExperienceDesigner CREATES a blueprint; BlueprintValidator only VERIFIES
/// it — this entity and its agent never mutate
/// <see cref="NodeExperienceBlueprintStep.Content"/> or generate new
/// illustrations.
/// </summary>
public class BlueprintValidation
{
    /// <summary>Identificador único de la validación (GUID).</summary>
    public Guid BlueprintValidationId { get; set; } = Guid.NewGuid();

    /// <summary>FK: NodeExperienceBlueprint que fue validado.</summary>
    public Guid NodeExperienceBlueprintId { get; set; }

    /// <summary>Resultado global de la validación.</summary>
    public BlueprintValidationStatus Status { get; set; }

    /// <summary>Score global de calidad, 0-100.</summary>
    public int Score { get; set; }

    /// <summary>Tokens de entrada de la llamada al BlueprintValidatorAgent.</summary>
    public int InputTokens { get; set; }

    /// <summary>Tokens de salida de la llamada al BlueprintValidatorAgent.</summary>
    public int OutputTokens { get; set; }

    /// <summary>Total de tokens (InputTokens + OutputTokens).</summary>
    public int TotalTokens { get; set; }

    /// <summary>Fecha UTC de creación de la validación.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // === Navigation Properties ===

    /// <summary>Referencia al NodeExperienceBlueprint validado.</summary>
    public virtual NodeExperienceBlueprint? NodeExperienceBlueprint { get; set; }

    /// <summary>Issues (Warning o Error) encontrados durante la validación.</summary>
    public virtual ICollection<BlueprintValidationIssue> Issues { get; set; } = new List<BlueprintValidationIssue>();

    /// <summary>Métricas de calidad generadas durante la validación (p.ej. IllustrationCoverage=100).</summary>
    public virtual ICollection<BlueprintValidationMetric> Metrics { get; set; } = new List<BlueprintValidationMetric>();
}
