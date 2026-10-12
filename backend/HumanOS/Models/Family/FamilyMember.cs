namespace HumanOS.Models.Family;

public sealed class FamilyMember
{
    public Guid MemberId { get; set; } = Guid.NewGuid();

    public Guid FamilyId { get; set; }

    public string FullName { get; set; } = null!;

    public string PreferredName { get; set; } = null!;

    public string FamilyRole { get; set; } = null!; // Father, Mother, TeenDaughter, Son, Grandmother, Mentor

    public string RoleTitleEs { get; set; } = null!;

    public string RoleTitleEn { get; set; } = null!;

    public DateTime? DateOfBirth { get; set; }

    public int? Age { get; set; }

    public string? EducationLevelEs { get; set; }

    public string? EducationLevelEn { get; set; }

    public string? Institution { get; set; }

    public string? PrimaryLocation { get; set; }

    public string? Email { get; set; }

    public string? AzureOid { get; set; }

    public string AvatarBg { get; set; } = "from-emerald-600 to-teal-700";

    public string? LanguagesJson { get; set; }

    public string? TopFocusJson { get; set; }

    public decimal WeeklyHoursTarget { get; set; } = 8.0m;

    public int LearningStreakDays { get; set; } = 0;

    public string? TargetMilestoneEs { get; set; }

    public string? TargetMilestoneEn { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    public FamilyMaster? Family { get; set; }
}
