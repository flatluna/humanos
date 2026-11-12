namespace HumanOS.Contracts.Family;

/// <summary>Original Family recommendation metadata (country/education system/
/// grade-stage/subject/age-range) a Studio document was created from — see
/// <see cref="HumanOS.Services.Family.FamilyCapabilityService.GetRecommendationOriginByCodeAsync"/>.</summary>
public sealed class FamilyCapabilityOriginResponse
{
    public string CountryCode { get; set; } = string.Empty;

    public string CountryNameEs { get; set; } = string.Empty;

    public string CountryNameEn { get; set; } = string.Empty;

    public string EducationSystemTypeCode { get; set; } = string.Empty;

    public string EducationSystemTypeNameEs { get; set; } = string.Empty;

    public string EducationSystemTypeNameEn { get; set; } = string.Empty;

    public string StageKey { get; set; } = string.Empty;

    public string StageNameEs { get; set; } = string.Empty;

    public string StageNameEn { get; set; } = string.Empty;

    public string AgeRange { get; set; } = string.Empty;

    public string SubjectCode { get; set; } = string.Empty;

    public string SubjectNameEs { get; set; } = string.Empty;

    public string SubjectNameEn { get; set; } = string.Empty;
}
