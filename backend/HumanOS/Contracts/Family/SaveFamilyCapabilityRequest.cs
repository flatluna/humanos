namespace HumanOS.Contracts.Family;

public sealed class SaveFamilyCapabilityRequest
{
    public string Code { get; set; } = null!;

    public string StageKey { get; set; } = null!; // e.g. "kindergarten", "1st_2nd_grade"

    public string NameEs { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string Domain { get; set; } = "STEM & Math"; // 'STEM & Math', 'Language & Literacy', 'Socio-Emotional', etc.

    public string DescriptionEs { get; set; } = null!;

    public string DescriptionEn { get; set; } = null!;

    public string? WhyItMattersEs { get; set; }

    public string? WhyItMattersEn { get; set; }

    public string TargetMasteryLevel { get; set; } = "Master"; // 'Foundation', 'Proficient', 'Advanced', 'Master'

    public int RecommendedHoursToMaster { get; set; } = 40;

    public List<string> SkillsCovered { get; set; } = [];

    /// <summary>
    /// NULL for global reference capabilities; or FamilyId for a custom family skill.
    /// </summary>
    public Guid? FamilyId { get; set; }

    public bool IsMandatoryForNextStage { get; set; } = true;
}
