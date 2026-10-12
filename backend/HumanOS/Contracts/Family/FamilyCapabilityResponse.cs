namespace HumanOS.Contracts.Family;

public sealed class FamilyCapabilityResponse
{
    public Guid CapabilityId { get; set; }

    public string Code { get; set; } = null!;

    public string StageKey { get; set; } = null!;

    public string NameEs { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string Domain { get; set; } = null!;

    public string DescriptionEs { get; set; } = null!;

    public string DescriptionEn { get; set; } = null!;

    public string? WhyItMattersEs { get; set; }

    public string? WhyItMattersEn { get; set; }

    public string TargetMasteryLevel { get; set; } = null!;

    public int RecommendedHoursToMaster { get; set; }

    public List<string> SkillsCovered { get; set; } = [];

    public bool IsGlobal { get; set; }

    public Guid? FamilyId { get; set; }

    public DateTime CreatedDate { get; set; }
}
