namespace HumanOS.Contracts.Capabilities;

public sealed class SubjectResponse
{
    public Guid SubjectId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }
}
