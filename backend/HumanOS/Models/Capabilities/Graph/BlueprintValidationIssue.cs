using System;

namespace HumanOS.Models.Capabilities.Graph;

/// <summary>
/// One concrete problem found by <c>BlueprintValidatorAgent</c> during a
/// <see cref="BlueprintValidation"/> run — either a blocking
/// <see cref="BlueprintValidationIssueSeverity.Error"/> ("Issue" in the
/// user-facing spec) or a non-blocking
/// <see cref="BlueprintValidationIssueSeverity.Warning"/> ("Warning" in the
/// user-facing spec). Kept as a single table distinguished by
/// <see cref="Severity"/> rather than two tables, since both share the
/// exact same shape (Area + Message).
/// </summary>
public class BlueprintValidationIssue
{
    /// <summary>Identificador único del issue (GUID).</summary>
    public Guid BlueprintValidationIssueId { get; set; } = Guid.NewGuid();

    /// <summary>FK: BlueprintValidation al que pertenece este issue.</summary>
    public Guid BlueprintValidationId { get; set; }

    /// <summary>Warning (no bloqueante) o Error (bloqueante).</summary>
    public BlueprintValidationIssueSeverity Severity { get; set; }

    /// <summary>Parte del blueprint (o concern transversal) a la que se refiere.</summary>
    public BlueprintValidationArea Area { get; set; }

    /// <summary>Descripción concreta y accionable del problema encontrado.</summary>
    public string Message { get; set; } = string.Empty;

    // === Navigation Properties ===

    /// <summary>Referencia al BlueprintValidation padre.</summary>
    public virtual BlueprintValidation? BlueprintValidation { get; set; }
}
