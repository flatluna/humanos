using HumanOS.Models.Capabilities;

namespace HumanOS.Models.Agents;

/// <summary>
/// AI agent — universal/global catalog, shared across all tenants (same
/// pattern as <see cref="Capability"/> and <see cref="CapabilityDomain"/>).
/// Every capability requires exactly one agent (its dedicated coach).
/// </summary>
public sealed class Agent
{
    public Guid AgentId { get; set; }

    /// <summary>The single capability this agent is dedicated to coaching.</summary>
    public Guid CapabilityId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>coach, mentor, evaluator, etc.</summary>
    public string? Role { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    /*
     * Navigation property
     */

    public Capability Capability { get; set; } = null!;
}
