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

    /// <summary>Traditional (Coursera-style) course chapters — see
    /// <see cref="Models.Capabilities.Traditional.CapabilityChapter"/>.</summary>
    public DbSet<Models.Capabilities.Traditional.CapabilityChapter> CapabilityChapters =>
        Set<Models.Capabilities.Traditional.CapabilityChapter>();

    public DbSet<Models.Capabilities.Traditional.CapabilityLesson> CapabilityLessons =>
        Set<Models.Capabilities.Traditional.CapabilityLesson>();

    public DbSet<Models.Capabilities.Traditional.CapabilityLessonCard> CapabilityLessonCards =>
        Set<Models.Capabilities.Traditional.CapabilityLessonCard>();

    public DbSet<Models.Capabilities.Traditional.CapabilityChapterExamQuestion> CapabilityChapterExamQuestions =>
        Set<Models.Capabilities.Traditional.CapabilityChapterExamQuestion>();

    public DbSet<Models.Capabilities.Traditional.CapabilityTraditionalIllustration> CapabilityTraditionalIllustrations =>
        Set<Models.Capabilities.Traditional.CapabilityTraditionalIllustration>();

    /// <summary>Step 1 of the new TOC-driven Studio pipeline — see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningDocument"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningDocument> StructuredLearningDocuments =>
        Set<Models.Capabilities.Studio.StructuredLearningDocument>();

    public DbSet<Models.Capabilities.Studio.StructuredLearningDocumentProgramMembership> StructuredLearningDocumentProgramMemberships =>
        Set<Models.Capabilities.Studio.StructuredLearningDocumentProgramMembership>();

    public DbSet<Models.Capabilities.Studio.StructuredLearningDocumentSection> StructuredLearningDocumentSections =>
        Set<Models.Capabilities.Studio.StructuredLearningDocumentSection>();

    public DbSet<Models.Capabilities.Studio.StructuredLearningKnowledgeFact> StructuredLearningKnowledgeFacts =>
        Set<Models.Capabilities.Studio.StructuredLearningKnowledgeFact>();

    public DbSet<Models.Capabilities.Studio.StructuredLearningKnowledgeFactGraphPathStep> StructuredLearningKnowledgeFactGraphPathSteps =>
        Set<Models.Capabilities.Studio.StructuredLearningKnowledgeFactGraphPathStep>();

    /// <summary>Explicit "Inscribirme" record for a Structured Learning
    /// document — see <see cref="Models.Capabilities.Studio.StructuredLearningDocumentEnrollment"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningDocumentEnrollment> StructuredLearningDocumentEnrollments =>
        Set<Models.Capabilities.Studio.StructuredLearningDocumentEnrollment>();

    /// <summary>Knowledge Graph (2026-08-14) — tenant-wide canonical
    /// entity catalog, populated by
    /// <see cref="HumanOS.Services.StructuredLearningEntityExtractionService"/>
    /// right after Step 2 (extraction).</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningEntity> StructuredLearningEntities =>
        Set<Models.Capabilities.Studio.StructuredLearningEntity>();

    /// <summary>Knowledge Graph (2026-08-14) — many-to-many entity-to-
    /// engram links with preserved source evidence, see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningEntitySection"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningEntitySection> StructuredLearningEntitySections =>
        Set<Models.Capabilities.Studio.StructuredLearningEntitySection>();

    public DbSet<Models.Capabilities.Studio.StructuredLearningConnection> StructuredLearningConnections =>
        Set<Models.Capabilities.Studio.StructuredLearningConnection>();

    /// <summary>Knowledge Graph \u2014 Master de Clases de Entidad (2026-08-14,
    /// Fase 1), scoped by (TenantId, SubjectId) domain. See
    /// <see cref="Models.Capabilities.Studio.StructuredLearningEntityClass"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningEntityClass> StructuredLearningEntityClasses =>
        Set<Models.Capabilities.Studio.StructuredLearningEntityClass>();

    /// <summary>Knowledge Graph — Master de Clases de Arista (2026-08-15,
    /// Fase 3), scoped by (TenantId, SubjectId) domain. See
    /// <see cref="Models.Capabilities.Studio.StructuredLearningRelationshipClass"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningRelationshipClass> StructuredLearningRelationshipClasses =>
        Set<Models.Capabilities.Studio.StructuredLearningRelationshipClass>();

    /// <summary>Knowledge Graph — Catálogo de Tipos/Temas (2026-08-16),
    /// scoped by (TenantId, SubjectId) domain. See
    /// <see cref="Models.Capabilities.Studio.StructuredLearningTopic"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningTopic> StructuredLearningTopics =>
        Set<Models.Capabilities.Studio.StructuredLearningTopic>();

    /// <summary>Per-step token usage + elapsed time (2026-08-14) — see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningGenerationUsage"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningGenerationUsage> StructuredLearningGenerationUsages =>
        Set<Models.Capabilities.Studio.StructuredLearningGenerationUsage>();

    /// <summary>Step 3 output — see <see cref="Models.Capabilities.Studio.StructuredLearningSectionStep"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningSectionStep> StructuredLearningSectionSteps =>
        Set<Models.Capabilities.Studio.StructuredLearningSectionStep>();

    /// <summary>Step 3 DeepDive card illustration metadata — see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningIllustration"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningIllustration> StructuredLearningIllustrations =>
        Set<Models.Capabilities.Studio.StructuredLearningIllustration>();

    /// <summary>Cost ledger for Google Veo video generation — see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningVideoGenerationLog"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningVideoGenerationLog> StructuredLearningVideoGenerationLogs =>
        Set<Models.Capabilities.Studio.StructuredLearningVideoGenerationLog>();

    /// <summary>Real-time Assessment (2026-08-10) — see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningAssessmentRound"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningAssessmentRound> StructuredLearningAssessmentRounds =>
        Set<Models.Capabilities.Studio.StructuredLearningAssessmentRound>();

    public DbSet<Models.Capabilities.Studio.StructuredLearningAssessmentQuestionAttempt> StructuredLearningAssessmentQuestionAttempts =>
        Set<Models.Capabilities.Studio.StructuredLearningAssessmentQuestionAttempt>();

    /// <summary>Real-time Recall (2026-08-10) — see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningRecallAttempt"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningRecallAttempt> StructuredLearningRecallAttempts =>
        Set<Models.Capabilities.Studio.StructuredLearningRecallAttempt>();

    /// <summary>Per-student Recall/Deep Dive runtime state (2026-08-13) — see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningPersonSectionState"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningPersonSectionState> StructuredLearningPersonSectionStates =>
        Set<Models.Capabilities.Studio.StructuredLearningPersonSectionState>();

    /// <summary>Per-student Hypothesis pre-check answers (2026-08-13) — see
    /// <see cref="Models.Capabilities.Studio.StructuredLearningHypothesisResponse"/>.</summary>
    public DbSet<Models.Capabilities.Studio.StructuredLearningHypothesisResponse> StructuredLearningHypothesisResponses =>
        Set<Models.Capabilities.Studio.StructuredLearningHypothesisResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(HumanOsDbContext).Assembly);
    }
}
