using HumanOS.Models.Localization;

namespace HumanOS.Models.Projects;

public sealed class ProjectTranslation
{
    public Guid ProjectId { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Project Project { get; set; } = null!;

    public Language Language { get; set; } = null!;
}
