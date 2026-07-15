using HumanOS.Models.People;
using HumanOS.Models.Practice;
using HumanOS.Models.Recall;

namespace HumanOS.Models.Capabilities;

public sealed class PersonCapability
{
    public Guid PersonCapabilityId { get; set; }

    public Guid PersonId { get; set; }

    public Guid CapabilityId { get; set; }

    public int CurrentLevel { get; set; }

    public int TargetLevel { get; set; } = 5;

    public decimal ProgressPercentage { get; set; }

    public decimal MasteryScore { get; set; }

    public string Status { get; set; } = "NotStarted";

    public int IndependenceLevel { get; set; }

    public decimal? RetentionScore { get; set; }

    public decimal? ConfidenceScore { get; set; }

    /// <summary>0-100. Factual/conceptual knowledge of the capability.</summary>
    public int KnowledgeScore { get; set; }

    /// <summary>0-100. Unassisted recall strength (anti AI-lookup paradox).</summary>
    public int RecallScore { get; set; }

    /// <summary>0-100. Ability to apply the capability in real situations.</summary>
    public int ApplicationScore { get; set; }

    public DateTime? StartedDate { get; set; }

    public DateTime? LastActivityDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    /*
     * Navigation properties
     */

    public Person Person { get; set; } = null!;

    public Capability Capability { get; set; } = null!;

    public ICollection<CapabilityPractice> Practices { get; set; } = [];

    public ICollection<RecallAttempt> RecallAttempts { get; set; } = [];

    public ICollection<CapabilityEvidence> CapabilityEvidence { get; set; } = [];
}
