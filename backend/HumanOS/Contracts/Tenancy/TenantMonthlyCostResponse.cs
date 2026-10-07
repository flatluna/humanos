namespace HumanOS.Contracts.Tenancy;

/// <summary>One month's total estimated spend across every course this
/// tenant has created (2026-10-01) — bucketed by the actual
/// generation-call timestamp (<c>CreatedDate</c> on the usage/illustration
/// rows), not the course's own CreatedDate, so cost lands in the month the
/// work was actually done (relevant when a course gets edited/regenerated
/// months after its original creation).</summary>
public sealed class TenantMonthlyCostResponse
{
    /// <summary>"2026-08" (year-month, sortable as a plain string).</summary>
    public string YearMonth { get; set; } = string.Empty;

    public decimal EstimatedCostUsd { get; set; }
}
