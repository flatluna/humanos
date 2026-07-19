using HumanOS.Models.Agents;
using HumanOS.Models.Assessments;
using HumanOS.Models.Capabilities;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Evidence;
using HumanOS.Models.Goals;
using HumanOS.Models.GrowthActions;
using HumanOS.Models.JobDescriptions;
using HumanOS.Models.Learning;
using HumanOS.Models.Localization;
using HumanOS.Models.People;
using HumanOS.Models.Practice;
using HumanOS.Models.Projects;
using HumanOS.Models.Recall;
using HumanOS.Models.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Data;

public sealed class HumanOsDbContext : DbContext
{
    public HumanOsDbContext(
        DbContextOptions<HumanOsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<PersonProfile> PersonProfiles => Set<PersonProfile>();

    public DbSet<HumanProfile> HumanProfiles => Set<HumanProfile>();

    public DbSet<HumanState> HumanStates => Set<HumanState>();

    public DbSet<GrowthAction> GrowthActions => Set<GrowthAction>();

    public DbSet<Agent> Agents => Set<Agent>();

    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();

    public DbSet<Language> Languages => Set<Language>();

    public DbSet<CapabilityDomain> CapabilityDomains =>
        Set<CapabilityDomain>();

    public DbSet<CapabilityDomainTranslation>
        CapabilityDomainTranslations =>
        Set<CapabilityDomainTranslation>();

    public DbSet<Capability> Capabilities => Set<Capability>();

    public DbSet<CapabilityTranslation> CapabilityTranslations =>
        Set<CapabilityTranslation>();

    public DbSet<PersonCapability> PersonCapabilities =>
        Set<PersonCapability>();

    public DbSet<CapabilityEvidence> CapabilityEvidence =>
        Set<CapabilityEvidence>();

    public DbSet<CapabilityPractice> CapabilityPractices =>
        Set<CapabilityPractice>();

    public DbSet<RecallAttempt> RecallAttempts =>
        Set<RecallAttempt>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<GoalTranslation> GoalTranslations =>
        Set<GoalTranslation>();

    public DbSet<GoalCapability> GoalCapabilities =>
        Set<GoalCapability>();

    public DbSet<PersonGoal> PersonGoals => Set<PersonGoal>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectTranslation> ProjectTranslations =>
        Set<ProjectTranslation>();

    public DbSet<PersonProject> PersonProjects =>
        Set<PersonProject>();

    public DbSet<Evidence> Evidence => Set<Evidence>();

    public DbSet<Assessment> Assessments => Set<Assessment>();

    public DbSet<AssessmentAttempt> AssessmentAttempts =>
        Set<AssessmentAttempt>();

    public DbSet<JobDescriptionRecord> JobDescriptions =>
        Set<JobDescriptionRecord>();

    public DbSet<CapabilityLevel> CapabilityLevels =>
        Set<CapabilityLevel>();

    public DbSet<CapabilityModule> CapabilityModules =>
        Set<CapabilityModule>();

    public DbSet<CapabilityModuleMetric> CapabilityModuleMetrics =>
        Set<CapabilityModuleMetric>();

    public DbSet<CapabilityModuleVerification> CapabilityModuleVerifications =>
        Set<CapabilityModuleVerification>();

    public DbSet<CapabilityModuleSuccessCriterionResult> CapabilityModuleSuccessCriterionResults =>
        Set<CapabilityModuleSuccessCriterionResult>();

    public DbSet<CapabilityKnowledgeChunk> CapabilityKnowledgeChunks =>
        Set<CapabilityKnowledgeChunk>();

    public DbSet<CapabilityModuleChapter> CapabilityModuleChapters =>
        Set<CapabilityModuleChapter>();

    public DbSet<CapabilityGraph> CapabilityGraphs =>
        Set<CapabilityGraph>();

    public DbSet<CapabilityGraphNode> CapabilityGraphNodes =>
        Set<CapabilityGraphNode>();

    public DbSet<CapabilityGraphEdge> CapabilityGraphEdges =>
        Set<CapabilityGraphEdge>();

    public DbSet<CapabilityGraphNodeIllustration> CapabilityGraphNodeIllustrations =>
        Set<CapabilityGraphNodeIllustration>();

    public DbSet<NodeExperienceBlueprint> NodeExperienceBlueprints =>
        Set<NodeExperienceBlueprint>();

    public DbSet<NodeExperienceBlueprintStep> NodeExperienceBlueprintSteps =>
        Set<NodeExperienceBlueprintStep>();

    public DbSet<BlueprintValidation> BlueprintValidations =>
        Set<BlueprintValidation>();

    public DbSet<BlueprintValidationIssue> BlueprintValidationIssues =>
        Set<BlueprintValidationIssue>();

    public DbSet<BlueprintValidationMetric> BlueprintValidationMetrics =>
        Set<BlueprintValidationMetric>();

    public DbSet<LearningSession> LearningSessions =>
        Set<LearningSession>();

    public DbSet<LearningSessionNode> LearningSessionNodes =>
        Set<LearningSessionNode>();

    public DbSet<LearningSessionStep> LearningSessionSteps =>
        Set<LearningSessionStep>();

    public DbSet<LearningEvidence> LearningEvidences =>
        Set<LearningEvidence>();

    public DbSet<LearningAssessmentResult> LearningAssessmentResults =>
        Set<LearningAssessmentResult>();

    /// <summary>Dynamic-assessment attempt cycles (5 questions, one at a
    /// time, never a fixed bank) for a node's Assessment step — see
    /// <see cref="Models.Learning.AssessmentRound"/>.</summary>
    public DbSet<AssessmentRound> AssessmentRounds =>
        Set<AssessmentRound>();

    /// <summary>Individual dynamically-generated questions within an
    /// <see cref="Models.Learning.AssessmentRound"/>.</summary>
    public DbSet<AssessmentQuestion> AssessmentQuestions =>
        Set<AssessmentQuestion>();

    /// <summary>Technical infrastructure table for the Interactive Learning
    /// Runtime's Workflow checkpointing — NOT a domain entity, see
    /// <see cref="RuntimeWorkflowCheckpoint"/>'s doc comment.</summary>
    public DbSet<RuntimeWorkflowCheckpoint> RuntimeWorkflowCheckpoints =>
        Set<RuntimeWorkflowCheckpoint>();

    /// <summary>Technical infrastructure table (fixed Paso 9, 2026-07-15) —
    /// see <see cref="RuntimeSessionStatus"/>'s doc comment for the real
    /// resume-hang bug this closes.</summary>
    public DbSet<RuntimeSessionStatus> RuntimeSessionStatuses =>
        Set<RuntimeSessionStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(HumanOsDbContext).Assembly);
    }
}
