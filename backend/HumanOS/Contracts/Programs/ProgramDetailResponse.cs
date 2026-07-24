namespace HumanOS.Contracts.Programs;

/// <summary>Full Program detail for GET /programs/{id} — adds the ordered
/// capability sequence on top of the catalog fields in <see cref="ProgramResponse"/>.</summary>
public sealed class ProgramDetailResponse : ProgramResponse
{
    public List<ProgramCapabilityResponse> Capabilities { get; set; } = [];
}
