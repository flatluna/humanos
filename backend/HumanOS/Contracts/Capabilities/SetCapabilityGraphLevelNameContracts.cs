namespace HumanOS.Contracts.Capabilities;

/// <summary>
/// Request body for PUT /capabilities/{capabilityId}/graph/levels/{levelIndex}
/// — Capability Studio's "professor" tool for giving a computed graph
/// "climb level" (see capabilitystudio/PreviewGraphPage.tsx's
/// groupIntoLevels) a friendly display name (e.g. "Capítulo 1: Vocales",
/// "Unit 1: Learn Vowels") instead of the generic "Level N" fallback.
/// </summary>
public sealed class SetCapabilityGraphLevelNameRequest
{
    /// <summary>The professor-given name. Sending an empty/whitespace value
    /// deletes any existing name for this level (reverts to the generic
    /// "Level N" fallback).</summary>
    public string Name { get; set; } = string.Empty;
}
