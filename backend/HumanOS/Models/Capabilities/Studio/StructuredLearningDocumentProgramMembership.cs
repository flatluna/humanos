namespace HumanOS.Models.Capabilities.Studio;

using HumanOS.Models.Programs;

/// <summary>
/// Join row linking a structured-learning document to a curated Program.
/// This is separate from the legacy ProgramCapability join because the
/// structured-learning flow is its own SQL-based document system and must
/// not be mixed with the older capability/program catalog tables.
/// </summary>
public sealed class StructuredLearningDocumentProgramMembership
{
    public Guid StructuredLearningDocumentProgramMembershipId { get; set; }

    public Guid ProgramId { get; set; }

    public Guid StructuredLearningDocumentId { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedDate { get; set; }

    public LearningProgram Program { get; set; } = null!;

    public StructuredLearningDocument Document { get; set; } = null!;
}
