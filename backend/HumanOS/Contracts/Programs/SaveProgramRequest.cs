namespace HumanOS.Contracts.Programs;

/// <summary>Body for POST /programs and PUT /programs/{id} (basic info step
/// of the wizard — name/description/objectives/requirements). Capability
/// sequencing is a separate call, see <see cref="UpdateProgramCapabilitiesRequest"/>.</summary>
public sealed class SaveProgramRequest
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Objectives { get; set; }

    public string? Requirements { get; set; }
}
