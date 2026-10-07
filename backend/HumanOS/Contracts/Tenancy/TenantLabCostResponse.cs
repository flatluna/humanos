namespace HumanOS.Contracts.Tenancy;

/// <summary>One lab's name and AI Lab Builder generation cost, for the
/// tenant dashboard's Labs tab (2026-10-01) — reads directly from
/// LAB_Lab.GEN_EstimatedCostUsd (and related GEN_* columns), which is
/// already a per-lab cost snapshot captured at generation time (see
/// HumanOS.SimulationLabs' LabBuilderCostEstimator) — no further
/// aggregation needed, unlike Capabilities/Structured Learning Documents
/// whose cost lives in a separate per-call usage table.</summary>
public sealed class TenantLabCostResponse
{
    public Guid LabId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsGlobal { get; set; }

    public decimal EstimatedCostUsd { get; set; }
}
