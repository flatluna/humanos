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
using HumanOS.Models.Motivations;
using HumanOS.Models.OrgStructure;
using HumanOS.Models.People;
using HumanOS.Models.Practice;
using HumanOS.Models.Programs;
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

    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

    /// <summary>Per-Person résumé/CV (address, LinkedIn, objective,
    /// skills, work experience, education, etc.), extracted by
    /// <see cref="HumanOS.Agents.ResumeExtractionAgent"/> from pasted raw
    /// text — see <see cref="PersonResume"/>.</summary>
    public DbSet<PersonResume> PersonResumes => Set<PersonResume>();

    /// <summary>The latest AI-generated career development plan (skill
    /// gap vs. JobRole + recommended catalog Programs) for a Person — see
    /// <see cref="PersonCareerPlan"/> and
    /// <see cref="HumanOS.Agents.CareerDevelopmentPlanAgent"/>.</summary>
    public DbSet<PersonCareerPlan> PersonCareerPlans => Set<PersonCareerPlan>();

    public DbSet<HumanProfile> HumanProfiles => Set<HumanProfile>();

    public DbSet<HumanState> HumanStates => Set<HumanState>();

    public DbSet<PersonCurrentSituation> PersonCurrentSituations =>
        Set<PersonCurrentSituation>();

    public DbSet<PersonFutureDirection> PersonFutureDirections =>
        Set<PersonFutureDirection>();

    public DbSet<PersonStartingPoint> PersonStartingPoints =>
        Set<PersonStartingPoint>();

    public DbSet<GrowthAction> GrowthActions => Set<GrowthAction>();

    public DbSet<Agent> Agents => Set<Agent>();

    public DbSet<AgentMessage> AgentMessages => Set<AgentMessage>();

    public DbSet<Language> Languages => Set<Language>();

    public DbSet<CapabilityDomain> CapabilityDomains =>
        Set<CapabilityDomain>();

    public DbSet<CapabilityDomainTranslation>
        CapabilityDomainTranslations =>
        Set<CapabilityDomainTranslation>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<SubjectTranslation> SubjectTranslations =>
        Set<SubjectTranslation>();

    public DbSet<Capability> Capabilities => Set<Capability>();

    public DbSet<CapabilityTranslation> CapabilityTranslations =>
        Set<CapabilityTranslation>();

    public DbSet<PersonCapability> PersonCapabilities =>
        Set<PersonCapability>();

    /// <summary>Explicit "Inscribirme" record for a standalone Capability
    /// (2026-07-24) — see <see cref="CapabilityEnrollment"/>.</summary>
    public DbSet<CapabilityEnrollment> CapabilityEnrollments =>
        Set<CapabilityEnrollment>();

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

    public DbSet<Motivation> Motivations => Set<Motivation>();

    public DbSet<MotivationTranslation> MotivationTranslations =>
        Set<MotivationTranslation>();

    public DbSet<PersonMotivation> PersonMotivations =>
        Set<PersonMotivation>();

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

    /// <summary>Professor-given names for computed graph "levels"/tiers
    /// (2026-07-30) — see <see cref="CapabilityGraphLevel"/>.</summary>
    public DbSet<CapabilityGraphLevel> CapabilityGraphLevels =>
        Set<CapabilityGraphLevel>();

    public DbSet<CapabilityGraphNodeIllustration> CapabilityGraphNodeIllustrations =>
        Set<CapabilityGraphNodeIllustration>();

    /// <summary>Persisted per-agent-call token usage for the cost-per-
    /// capability dashboard (2026-07-23) — see
    /// <see cref="CapabilityGenerationUsage"/>.</summary>
    public DbSet<CapabilityGenerationUsage> CapabilityGenerationUsages =>
        Set<CapabilityGenerationUsage>();

    /// <summary>On-demand "Profundizar" knowledge expansion cache, one row
    /// per node (2026-07-20) — see
    /// <see cref="CapabilityGraphNodeKnowledgeExpansion"/>.</summary>
    public DbSet<CapabilityGraphNodeKnowledgeExpansion> CapabilityGraphNodeKnowledgeExpansions =>
        Set<CapabilityGraphNodeKnowledgeExpansion>();

    /// <summary>Per-node embedded knowledge chunks used for cross-node RAG
    /// (2026-07-20) — see <see cref="CapabilityGraphNodeKnowledgeChunk"/>.</summary>
    public DbSet<CapabilityGraphNodeKnowledgeChunk> CapabilityGraphNodeKnowledgeChunks =>
        Set<CapabilityGraphNodeKnowledgeChunk>();

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

    /// <summary>Explicit "Inscribirme" record for a Program, created
    /// directly from the catalog (2026-07-25) — see
    /// <see cref="Models.Programs.ProgramEnrollment"/>.</summary>
    public DbSet<Models.Programs.ProgramEnrollment> ProgramEnrollments =>
        Set<Models.Programs.ProgramEnrollment>();

    public DbSet<LearningEvidence> LearningEvidences =>
        Set<LearningEvidence>();

    public DbSet<LearningAssessmentResult> LearningAssessmentResults =>
        Set<LearningAssessmentResult>();

    /// <summary>Completed "Reactivar Engrama" full voice-review sessions —
    /// see <see cref="EngramReviewCompletion"/>.</summary>
    public DbSet<EngramReviewCompletion> EngramReviewCompletions =>
        Set<EngramReviewCompletion>();

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

    /// <summary>Curated learning paths grouping an ordered sequence of
    /// existing Capabilities (2026-07-23) — see <see cref="LearningProgram"/>.</summary>
    public DbSet<LearningProgram> Programs =>
        Set<LearningProgram>();

    public DbSet<ProgramTranslation> ProgramTranslations =>
        Set<ProgramTranslation>();

    public DbSet<ProgramCapability> ProgramCapabilities =>
        Set<ProgramCapability>();

    /// <summary>Tenant org-chart tree (Área/Departamento/Equipo, all the
    /// same self-referencing node) — see <see cref="Department"/>.</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>Org-authored "puesto" templates attached to a Department —
    /// see <see cref="JobRole"/>.</summary>
    public DbSet<JobRole> JobRoles => Set<JobRole>();

    /// <summary>Reusable, catalog-level training requirements shareable
    /// across many JobRoles — see <see cref="Skill"/>.</summary>
    public DbSet<Skill> Skills => Set<Skill>();

    /// <summary>JobRole↔Skill many-to-many join — see
    /// <see cref="JobRoleSkill"/>.</summary>
    public DbSet<JobRoleSkill> JobRoleSkills => Set<JobRoleSkill>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(HumanOsDbContext).Assembly);
    }
}
