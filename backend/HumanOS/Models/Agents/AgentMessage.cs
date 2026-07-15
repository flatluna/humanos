using HumanOS.Models.People;
using HumanOS.Models.Tenancy;

namespace HumanOS.Models.Agents;

/// <summary>
/// A message an agent sends to a person, with the reason behind it
/// (e.g. "You're ready for a bigger challenge" / "because your Recall
/// score went up 20%"). Person/Tenant-scoped, even though the Agent
/// definition itself is universal.
/// </summary>
public sealed class AgentMessage
{
    public Guid AgentMessageId { get; set; }

    public Guid AgentId { get; set; }

    /// <summary>The universal "user id" — recipient of this message.</summary>
    public Guid PersonId { get; set; }

    public Guid TenantId { get; set; }

    public string Message { get; set; } = null!;

    public string? Reason { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Agent Agent { get; set; } = null!;

    public Person Person { get; set; } = null!;

    public Tenant Tenant { get; set; } = null!;
}
