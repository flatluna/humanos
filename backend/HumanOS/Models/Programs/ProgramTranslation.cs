namespace HumanOS.Models.Programs;

using HumanOS.Models.Localization;

public sealed class ProgramTranslation
{
    public Guid ProgramId { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Objectives { get; set; }

    public string? Requirements { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public LearningProgram Program { get; set; } = null!;

    public Language Language { get; set; } = null!;
}
