namespace HumanOS.Contracts.Motivations;

public sealed class PersonMotivationResponse
{
    public Guid PersonMotivationId { get; set; }

    public Guid PersonId { get; set; }

    public Guid MotivationId { get; set; }

    public string MotivationCode { get; set; } = null!;

    public string MotivationName { get; set; } = null!;

    public DateTime CreatedDate { get; set; }
}
