namespace HumanOS.Contracts.Family;

public sealed class HumanDevelopmentStageResponse
{
    public Guid StageId { get; set; }

    public string StageKey { get; set; } = null!;

    public string StageNameEs { get; set; } = null!;

    public string StageNameEn { get; set; } = null!;

    public string AgeRange { get; set; } = null!;

    public string CategoryGroup { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public string? DescriptionEs { get; set; }

    public string? DescriptionEn { get; set; }
}
