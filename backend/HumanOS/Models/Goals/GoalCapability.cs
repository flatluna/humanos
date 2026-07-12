using HumanOS.Models.Capabilities;

namespace HumanOS.Models.Goals;

public sealed class GoalCapability
{
    public Guid GoalId { get; set; }

    public Guid CapabilityId { get; set; }

    public bool IsRequired { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public Goal Goal { get; set; } = null!;

    public Capability Capability { get; set; } = null!;
}
