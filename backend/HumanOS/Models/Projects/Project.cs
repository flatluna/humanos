using HumanOS.Models.Capabilities;

namespace HumanOS.Models.Projects;

public sealed class Project
{
    public Guid ProjectId { get; set; }

    public Guid CapabilityId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int DifficultyLevel { get; set; } = 1;

    public decimal? EstimatedHours { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    public Capability Capability { get; set; } = null!;

    public ICollection<ProjectTranslation> Translations { get; set; } = [];

    public ICollection<PersonProject> PersonProjects { get; set; } = [];
}
