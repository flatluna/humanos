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

    public string? PhoneNumber { get; set; }

    public string? PhoneCountryCode { get; set; }

    /// <summary>
    /// Azure AD / Entra External ID (CIAM) object id of the actual MSAL account
    /// linked to this member. Null for members without their own login (e.g.
    /// children under 12, per the family login policy) or not yet linked.
    /// </summary>
    public string? AzureOid { get; set; }

    /// <summary>
    /// The real email/UPN the linked MSAL account was created with (captured
    /// at link time from the signed-in account). This can differ from
    /// <see cref="Email"/> — Email is the member's household/contact email,
    /// while this is literally what the person used to sign up in Entra
    /// (e.g. demo accounts reused across multiple business use cases).
    /// </summary>
    public string? AzureLoginEmail { get; set; }

    /// <summary>UTC timestamp of when the MSAL account was linked to this member.</summary>
    public DateTime? AzureLinkedDate { get; set; }

    /// <summary>
    /// Platform-level authorization role for this member: "AdminEngrams" (us,
    /// the EngramsIQ owners/staff — full access incl. the AI agents and
    /// HumanOS Studio), "AdminFamily" (manages their own family's data) or
    /// "User" (regular family member — view-only on premium/admin features).
    /// Defaults to "User".
    /// </summary>
    public string PlatformRole { get; set; } = "User";

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
