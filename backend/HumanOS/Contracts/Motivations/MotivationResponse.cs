namespace HumanOS.Contracts.Motivations;

public sealed class MotivationResponse
{
    public Guid MotivationId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
}
