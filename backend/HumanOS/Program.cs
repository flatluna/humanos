using System.Text.Json;
using Azure.Identity;
using HumanOS.Agentic.Runtime;
using HumanOS.Agentic.Studio;
using HumanOS.Agents;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Crash diagnostics (2026-07-23): the host process has been observed to die
// silently — no exception ever printed, no WER/Application-Error event, no
// Windows resource-exhaustion event — reliably during the illustration
// image-generation stage (PdfCapabilityGraphPipelineService), even with the
// per-image call already wrapped in try/catch there. If .NET itself sees an
// unhandled exception on ANY thread (including a background/unobserved task
// continuation the normal try/catch never touches), these two events fire
// right before the runtime fail-fasts the process — logging them here is the
// only way to capture what actually killed it next time it happens.
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    try
    {
        File.AppendAllText(
            "fatal-crash.log",
            $"[{DateTime.UtcNow:O}] AppDomain.UnhandledException IsTerminating={e.IsTerminating}{Environment.NewLine}{e.ExceptionObject}{Environment.NewLine}{Environment.NewLine}");
    }
    catch
    {
        // Never let crash logging itself throw.
    }
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    try
    {
        File.AppendAllText(
            "fatal-crash.log",
            $"[{DateTime.UtcNow:O}] TaskScheduler.UnobservedTaskException{Environment.NewLine}{e.Exception}{Environment.NewLine}{Environment.NewLine}");
        e.SetObserved();
    }
    catch
    {
        // Never let crash logging itself throw.
    }
};

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((context, config) =>
    {
        // ConfigureFunctionsWorkerDefaults() alone does NOT load
        // appsettings.json (isolated worker only auto-loads
        // local.settings.json's "Values" + environment variables) — confirmed
        // 2026-07-23 via a real bug: AgentPricing:Models:* rates in
        // appsettings.json were silently never applied, TokenCostEstimator
        // always fell back to its hardcoded defaults. Explicitly add it here.
        config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices(services =>
    {
        services.AddHttpClient();
        services.AddOptions<EntraInvitationOptions>()
            .BindConfiguration(EntraInvitationOptions.SectionName);

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY")))
        {
            services.AddApplicationInsightsTelemetryWorkerService();
        }

        var connectionString =
            Environment.GetEnvironmentVariable("HumanOSDatabase");

        if (!string.IsNullOrEmpty(connectionString))
        {
            // Registered via AddDbContextFactory (Singleton-safe) rather than
            // AddDbContext, so the Singleton CapabilityCreationOrchestrator can
            // inject IDbContextFactory<HumanOsDbContext> directly and create
            // short-lived DbContext instances on demand (PublishExecutor)
            // without a captive-dependency violation. AddDbContext would ALSO
            // register DbContextOptions<HumanOsDbContext> as Scoped, which a
            // Singleton IDbContextFactory<HumanOsDbContext> cannot consume —
            // confirmed via a real DI-validation startup failure. The
            // AddScoped<HumanOsDbContext> below keeps every existing Scoped
            // service (TenantService, PersonService, etc.) working exactly as
            // before, just resolving through the factory internally.
            services.AddDbContextFactory<HumanOsDbContext>(options =>
            {
                // For debugging - use connection string directly without token
                // TODO: Implement lazy token loading
                options.UseSqlServer(
                    connectionString,
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                    });
            });

            services.AddScoped(sp =>
                sp.GetRequiredService<IDbContextFactory<HumanOsDbContext>>().CreateDbContext());
        }

        services.AddScoped<TenantService>();
        services.AddScoped<PersonService>();
        services.AddSingleton<EntraInvitationProvisioningService>();
        services.AddScoped<TenantInvitationService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<JobRoleService>();
        services.AddScoped<SkillService>();
        services.AddScoped<ResponsibilityService>();
        services.AddScoped<CertificationService>();
        services.AddScoped<ToolService>();
        services.AddScoped<PersonProfileService>();
        services.AddScoped<GrowthPlanService>();
        services.AddScoped<CapabilityEnrollmentService>();
        services.AddScoped<StructuredLearningDocumentEnrollmentService>();
        services.AddScoped<HumanProfileService>();
        services.AddScoped<HumanStateService>();
        services.AddScoped<LanguageService>();
        services.AddScoped<CapabilityDomainService>();
        services.AddScoped<SubjectService>();
        services.AddScoped<CapabilityGraphPersistenceService>();
        services.AddScoped<NodeExperienceBlueprintPersistenceService>();
        services.AddScoped<BlueprintValidationPersistenceService>();
        services.AddScoped<InstructorRuntimeOrchestrator>();
        services.AddScoped<AssessmentEvaluator>();
        services.AddScoped<AdaptiveAssessmentEngine>();
        services.AddScoped<BlueprintReviewService>();
        services.AddScoped<TutorService>();
        services.AddScoped<ProductionEvaluationService>();
        services.AddScoped<SessionRecoveryEngine>();
        services.AddScoped<GraphProgressionEngine>();
        services.AddScoped<CapabilityService>();
        services.AddScoped<PersonCapabilityService>();
        services.AddScoped<ProgramService>();
        services.AddScoped<ProgramEnrollmentService>();
        services.AddScoped<CapabilityCostService>();
        services.AddScoped<StructuredLearningCostService>();
        services.AddScoped<HumanOS.Storage.ProgramLogoStorageService>();
        services.AddScoped<HumanOS.Storage.PersonProfilePhotoStorageService>();
        services.AddScoped<GoalService>();
        services.AddScoped<MotivationService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<PracticeService>();
        services.AddScoped<RecallService>();
        services.AddScoped<EvidenceService>();
        services.AddScoped<AssessmentService>();
        services.AddScoped<TranslationService>();
        services.AddScoped<RoleDocumentStorageService>();
        services.AddScoped<CapabilityMaterialStorageService>();
        services.AddScoped<HumanOS.Storage.SkillDocumentStorageService>();
        services.AddScoped<JobDescriptionExtractionAgent>();
        services.AddScoped<HumanOS.Storage.CapabilityGraphIllustrationStorageService>();
        // Enterprise console "Importar organigrama (IA)" (2026-07-31) —
        // singleton, same rationale as every other Studio agent (no
        // per-request state; holds only a configured AIAgent instance).
        services.AddSingleton<OrgChartExtractionAgent>();
        services.AddScoped<OrgChartImportService>();
        // Roles y Puestos "Generar con IA" (2026-07-31) — drafts/improves a JobRole's
        // full description from real company/department/title context, using a genuine
        // Tools (function-calling) search_similar_job_roles call — see
        // JobRoleDescriptionAgent's doc comment for why this is the one agent in this
        // codebase with a real AITool instead of Harness. Singleton, same rationale as
        // OrgChartExtractionAgent above (no per-request state; only holds a configured
        // AzureOpenAIClient — its AIAgent wrapper and tools are rebuilt per request).
        services.AddSingleton<HumanOS.Agents.JobRoleDescriptionAgent>();

        // Enterprise console "Personal" employee dashboard — "Extraer con IA" from a
        // pasted résumé/CV (2026-07-31). Purely extractive, no tools (see
        // ResumeExtractionAgent's doc comment for why it differs from
        // JobRoleDescriptionAgent). Singleton, same rationale as every other
        // Studio/agent-framework agent (no per-request state).
        services.AddSingleton<HumanOS.Agents.ResumeExtractionAgent>();
        services.AddScoped<PersonResumeService>();

        // Enterprise console "Rutas de Carrera" — per-employee AI career
        // development plan (2026-08-01): compares a person's résumé skills
        // against their JobRole's required skills and recommends real
        // catalog Programs to close the gap. Singleton, same rationale as
        // every other Studio/agent-framework agent (no per-request state).
        services.AddSingleton<HumanOS.Agents.CareerDevelopmentPlanAgent>();
        services.AddScoped<PersonCareerPlanService>();

        // Employee SMART Objective Recommendation Agent (Microsoft Agent Framework) —
        // Recommends individual SMART objectives and selects role skills based on aligned
        // department objectives and job role competencies.
        services.AddSingleton<HumanOS.Agents.SmartObjectiveRecommendationAgent>();

        // Employee professional profile consolidation service for AI scoring
        services.AddScoped<EmployeeScoringProfileService>();
        services.AddScoped<ILearnerContextBuilder, LearnerContextBuilder>();
        services.AddSingleton<HumanOS.Agents.EngramImpactScoringAgent>();
        services.AddScoped<EngramImpactScorePersistenceService>();
        services.AddScoped<EngramImpactOrchestrator>();
        services.AddScoped<RewardEngineService>();
        services.AddScoped<StreakService>();
        services.AddScoped<BadgeService>();
        services.AddScoped<ReviewQueueService>();
        services.AddScoped<GamificationOrchestrator>();

        // Growth Plan Step 3 ("Planeemos Juntos tu Desarrollo", merged
        // Steps 3+4, 2026-07-22): recommends a Program or individual
        // Capabilities from a free-text goal, scoped to the person's
        // Step 1 Subjects. Singleton, same rationale as the Studio
        // pipeline agents below (no per-request state).
        services.AddSingleton<GrowthPathRecommenderAgent>();

        // Human OS Studio — capability-creation pipeline agents (Microsoft
        // Agent Framework, ChatClientAgents with structured output; see
        // /memories/repo/humanstudio-multiagent-vision.md). Singleton: they
        // hold no per-request state, and CapabilityCreationOrchestrator (also
        // a singleton, since it keeps in-memory runs alive across HTTP calls)
        // depends on them directly.
        services.AddSingleton<CuradorAgent>();
        services.AddSingleton<TocExtractionAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.OutlineTitleRefinementAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.EngramRelevanceRerankerAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.SectionContentExpansionAgent>();
        services.AddSingleton<ArquitectoAgent>();
        services.AddSingleton<GraphArchitectAgent>();
        // Studio "professor" tool (2026-07-24): designs ONE new node to be
        // inserted into an already-published CapabilityGraph from a
        // free-text prompt — see AddCapabilityGraphNodeFunction. Same
        // Singleton rationale as every other Studio agent (no per-request
        // state).
        services.AddSingleton<HumanOS.Agents.Studio.GraphNodeAdditionAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.DocumentContextAgent>();
        services.AddSingleton<GraphIllustrationImageService>();
        // Deterministic (non-AI) coordinate-plane diagram renderer
        // (2026-07-25) — used instead of GraphIllustrationImageService
        // whenever an illustration prompt is a precise math/geometry graph
        // (circle/line/parabola/points) that a generative image model can't
        // guarantee pixel-accurate coordinates for. See
        // Services/MathDiagramRenderService.cs's doc comment.
        services.AddSingleton<HumanOS.Services.MathDiagramRenderService>();
        services.AddSingleton<ExperienceDesignerAgent>();
        // Generates a real illustration per ExampleCard of the optional
        // Examples step (2026-07-29), upgrading the original emoji-only
        // cards. Singleton, same rationale as GraphIllustrationImageService
        // (no per-request state; builds its own storage-service instance).
        services.AddSingleton<HumanOS.Services.ExampleCardIllustrationService>();
        services.AddSingleton<BlueprintValidatorAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.BlueprintStepEditorAgent>();
        // Deep Dive card-by-card review edits (2026-07-30) — see
        // Agents/Studio/ExampleCardEditorAgent.cs's doc comment for why this
        // is a SEPARATE agent from BlueprintStepEditorAgent (which has no
        // concept of ExampleCards at all).
        services.AddSingleton<HumanOS.Agents.Studio.ExampleCardEditorAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.WebGroundingService>();
        services.AddSingleton<InstructorAgent>();
        services.AddSingleton<MetricoAgent>();
        services.AddSingleton<ExperienciaAgent>();
        services.AddSingleton<CapabilityEmbeddingService>();
        services.AddSingleton<CapabilityCreationOrchestrator>();

        // Per-node RAG index for the V2 Graph/Blueprint pipeline (2026-07-20,
        // see /memories/repo/tutor-document-wide-context-gap.md) — lets the
        // Tutor answer cross-node factual questions at runtime. Singleton,
        // same rationale as CapabilityEmbeddingService (no per-request
        // state; reused by the PDF pipeline, KnowledgeExpansionService, and
        // TutorService).
        services.AddSingleton<NodeKnowledgeIndexService>();

        // V2 "PDF → CapabilityGraph" pipeline (2026-07-19): uploads a real
        // PDF directly and turns it into a fully persisted CapabilityGraph
        // + per-node Memory Paradox blueprints, reusing the Studio agents
        // above. Singleton, same rationale as CapabilityCreationOrchestrator
        // (keeps in-memory runs alive across HTTP calls) — see
        // /memories/repo/humanstudio-multiagent-vision.md.
        services.AddSingleton<HumanOS.Agents.Studio.IdeaToDocumentAgent>();
        // Describes embedded page images (scanned pages, diagrams, photos)
        // in detailed text so image-only/image-heavy PDF pages still reach
        // Curador/GraphArchitect with real content (2026-07-23) — see
        // /memories/repo/pdf-to-capability-graph-v2-pipeline.md.
        services.AddSingleton<HumanOS.Agents.Studio.PdfImageDescriptionAgent>();
        // Caches extraction (text + described images) by content hash so a
        // real run doesn't re-describe images already described during the
        // Paso 5 preview of the SAME upload (2026-08-05) — see
        // PdfContentExtractionService's doc comment.
        services.AddMemoryCache();
        services.AddSingleton<PdfContentExtractionService>();
        services.AddSingleton<PdfCapabilityGraphPipelineService>();
        services.AddSingleton<PdfCapabilityGraphOrchestrator>();

        // Traditional-course pipeline (2026-08-09) — Studio's second,
        // simpler course-creation method: linear Chapters -> Lessons made
        // of reading Cards (no Recall/Production), ending each chapter with
        // ONE deterministic multiple-choice exam. Reuses every extraction/
        // grounding/illustration agent above; only LessonCardAgent/
        // ChapterExamAgent are new. Same Singleton rationale as the Graph
        // pipeline above (keeps in-memory runs alive across HTTP calls).
        services.AddSingleton<HumanOS.Agents.Studio.LessonCardAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.ChapterExamAgent>();
        services.AddSingleton<TraditionalCoursePipelineService>();
        services.AddSingleton<TraditionalCoursePipelineOrchestrator>();

        // Structured Learning Document pipeline (2026-08-10) — Studio's
        // THIRD course-creation method, Step 1/2 only so far: deterministic
        // (no-LLM-generation) Chapter->Section extraction with a semantic
        // embedding per section, for content whose own TOC/index should be
        // walked as-is rather than turned into a concept/skill graph (e.g.
        // policy/compliance documents). Reuses PdfContentExtractionService/
        // DocumentChapterSplitter/CapabilityEmbeddingService as-is; brand
        // new persistence + its own separate in-memory run slot — never
        // touches the Graph or Traditional pipelines above.
        services.AddSingleton<StructuredLearningExtractionService>();
        services.AddSingleton<StructuredLearningExtractionOrchestrator>();

        // Work Step Learning Assignment (Step 1-8: Ingestion, Step Requirements, Catalog Search, Engram Selection, Coverage, Content Gap Drafts, Step Assignment & Employee Personalization)
        services.AddSingleton<IProcessDocumentStorageService, ProcessDocumentStorageService>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.IProcessDocumentExtractionService, HumanOS.Services.ProcessDocuments.ProcessDocumentExtractionService>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.ProcessDocumentPipelineService>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.ProcessDocumentOrchestrator>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.IWorkStepRequirementsAnalyzer, HumanOS.Services.ProcessDocuments.WorkStepRequirementsAnalyzer>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.IEngramStudioCatalogService, HumanOS.Services.ProcessDocuments.EngramStudioCatalogService>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.IEngramStudioClassCandidateFinder, HumanOS.Services.ProcessDocuments.EngramStudioClassCandidateFinder>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.IEngramStudioEngramSelector, HumanOS.Services.ProcessDocuments.EngramStudioEngramSelector>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.ILearningContentCoverageAnalyzer, HumanOS.Services.ProcessDocuments.LearningContentCoverageAnalyzer>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.ILearningContentGapDraftGenerator, HumanOS.Services.ProcessDocuments.LearningContentGapDraftGenerator>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.IStepLearningAssignmentGenerator, HumanOS.Services.ProcessDocuments.StepLearningAssignmentGenerator>();
        services.AddSingleton<HumanOS.Services.ProcessDocuments.IEmployeeStepLearningAssignmentService, HumanOS.Services.ProcessDocuments.EmployeeStepLearningAssignmentService>();

        // Knowledge Graph (2026-08-14) — entity extraction + entity-to-
        // engram linking, chained automatically at the end of Step 2 (see
        // StructuredLearningExtractionService.GenerateKnowledgeGraphAsync).
        // Singleton (uses IDbContextFactory internally, same as every other
        // Structured Learning service above).
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningEntityExtractionAgent>();
        services.AddSingleton<StructuredLearningGenerationUsageService>();
        services.AddSingleton<StructuredLearningEntityExtractionService>();
        services.AddSingleton<StructuredLearningKnowledgeRetrievalService>();

        // AI Agents chat window (2026-08-18) — /ai-agents/:subject/:topic
        // free-form Q&A tutor grounded in the Knowledge Graph retrieval
        // package above. Singleton, same rationale as every other Studio
        // agent (no per-request state).
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningTutorAgent>();
        services.AddSingleton<StructuredLearningChatService>();

        // Master de Clases de Entidad (2026-08-14, Fase 1) — plain catalog
        // CRUD only, not yet wired into extraction (see the service's own
        // doc comment for the staged rollout plan).
        services.AddSingleton<StructuredLearningEntityClassService>();
        services.AddSingleton<StructuredLearningTopicService>();

        // Master de Clases de Arista (2026-08-15, Fase 3) — same pattern
        // as above, for reusable relationship labels/categories.
        services.AddSingleton<StructuredLearningRelationshipClassService>();

        // Document Type Detection Engine (2026-08-17, Paso 1) — deterministic,
        // no LLM/embeddings; runs BEFORE chapter/engram extraction to decide
        // which extraction strategy a document should use downstream. Each
        // IDocumentClassifier is stateless/pure, registered individually so
        // DocumentClassificationEngine (which takes IEnumerable<IDocumentClassifier>)
        // resolves every one of them automatically.
        services.AddSingleton<HumanOS.Services.DocumentClassification.IDocumentClassifier,
            HumanOS.Services.DocumentClassification.Classifiers.ContractClassifier>();
        services.AddSingleton<HumanOS.Services.DocumentClassification.IDocumentClassifier,
            HumanOS.Services.DocumentClassification.Classifiers.LawClassifier>();
        services.AddSingleton<HumanOS.Services.DocumentClassification.IDocumentClassifier,
            HumanOS.Services.DocumentClassification.Classifiers.PolicyClassifier>();
        services.AddSingleton<HumanOS.Services.DocumentClassification.IDocumentClassifier,
            HumanOS.Services.DocumentClassification.Classifiers.ProcedureClassifier>();
        services.AddSingleton<HumanOS.Services.DocumentClassification.IDocumentClassifier,
            HumanOS.Services.DocumentClassification.Classifiers.AcademicWithIndexClassifier>();
        services.AddSingleton<HumanOS.Services.DocumentClassification.IDocumentClassifier,
            HumanOS.Services.DocumentClassification.Classifiers.AcademicNarrativeClassifier>();
        services.AddSingleton<HumanOS.Services.DocumentClassification.IDocumentClassifier,
            HumanOS.Services.DocumentClassification.Classifiers.ResearchPaperClassifier>();
        services.AddSingleton<HumanOS.Services.DocumentClassification.DocumentClassificationEngine>();

        // Knowledge Graph edges (2026-08-14) — relationship extraction
        // between entities already linked to the same section; must be
        // registered after StructuredLearningEntityExtractionService above
        // since it depends on that step having already run for a section.
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningConnectionExtractionAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningKnowledgeFactGenerationAgent>();
        services.AddSingleton<StructuredLearningConnectionExtractionService>();
        services.AddSingleton<StructuredLearningKnowledgeFactGenerationService>();

        // Step 3 (2026-08-10) — designs the 6-step pedagogical sequence for
        // ONE section at a time, grounded ONLY in that section's own text.
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningSectionDesignerAgent>();
        services.AddSingleton<StructuredLearningPedagogyService>();
        services.AddSingleton<StructuredLearningPedagogyOrchestrator>();

        // Google Veo video generation for physical/gestural Learn content
        // (2026-08-21) — mirrors GraphIllustrationImageService's Singleton
        // rationale (stateless, only needs config + a shared HttpClient).
        services.AddSingleton<GoogleVeoVideoService>();
        services.AddSingleton<GoogleOmniFlashVideoService>();
        services.AddSingleton<StructuredLearningVideoOrchestrator>();

        // Real-time Recall/Assessment (2026-08-10) — replaces the earlier
        // design where the section designer above baked a FIXED list of
        // Recall items/Assessment questions once at generation time (same
        // static content shown to every student, every time). Mirrors the
        // proven Graph/Memory Paradox pattern (AdaptiveAssessmentAgent/
        // TutorAgentV2 + RecallLoopGate) — see StructuredLearningAssessmentEngine.cs/
        // StructuredLearningRecallEngine.cs doc comments. Agents singleton
        // (no per-request state, same as every other agent); engines
        // scoped (take a DbContext per call, same as AdaptiveAssessmentEngine/
        // TutorService above).
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningAssessmentAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningRecallAgent>();

        // Film Productions wizard (2026-08-24) — reuses GraphIllustrationImageService
        // and CapabilityGraphIllustrationStorageService (already registered above).
        services.AddSingleton<HumanOS.Agents.Studio.FilmSceneContentAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.FilmScriptBreakdownAgent>();
        // Scoped (not Singleton like other orchestrators): depends on the
        // Scoped CapabilityGraphIllustrationStorageService, and this host
        // has DI scope validation enabled, unlike whatever allowed
        // StructuredLearningVideoOrchestrator's same Singleton->Scoped
        // dependency to slip through.
        services.AddScoped<HumanOS.Services.FilmProductionOrchestrator>();
        // Fase 2 (2026-08-24) — per-scene review/edit video generation via
        // Gemini Omni Flash image-to-video. Same Scoped rationale as above.
        services.AddScoped<HumanOS.Services.FilmProductionVideoOrchestrator>();
        services.AddScoped<StructuredLearningAssessmentEngine>();
        services.AddScoped<StructuredLearningRecallEngine>();

        // Hypothesis pre-check answer persistence (2026-08-13) — fixes a
        // real gap: these answers were only ever kept in the browser's
        // localStorage, never reaching the backend. Fully deterministic
        // (no LLM), scoped (takes a DbContext per call).
        services.AddScoped<StructuredLearningHypothesisService>();

        // Real-time Deep Dive (2026-08-10) — same rationale as Recall/
        // Assessment above, ported from Graph's TutorService.SubmitDeepDiveAttemptAsync
        // + DeepDiveLoopGate (reused as-is).
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningDeepDiveAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningVideoPromptAgent>();
        services.AddScoped<StructuredLearningDeepDiveEngine>();

        // "Edit mode" review tools (2026-08-10) — mirrors Graph's
        // BlueprintReviewService/BlueprintStepEditorAgent/ExampleCardEditorAgent.
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningStepEditorAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningExampleCardEditorAgent>();
        services.AddSingleton<HumanOS.Services.PerplexitySearchService>();
        services.AddScoped<StructuredLearningReviewService>();

        // Professor Lecture Agent (2026-09-06) — generates a personalized, spoken-word
        // masterclass lecture delivered by a professor directly to the student for an Engram.
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningProfessorLectureAgent>();
        services.AddScoped<StructuredLearningProfessorLectureService>();

        // Workplace Application Agent (2026-09-06) — generates a personalized real-world
        // workplace case and pre-recorded neural audio tailored to the student's role and SMART objectives.
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningWorkplaceApplicationAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningWorkplaceMentorAgent>();
        services.AddScoped<HumanOS.Services.StructuredLearningWorkplaceApplicationService>();

        // Traditional course edit tools (2026-08-12) — same review pattern as
        // the structured-learning tools: AI rewrite and direct manual save for
        // chapter titles, lesson titles, and lesson card text.
        services.AddSingleton<HumanOS.Agents.Studio.TraditionalCourseReviewAgent>();
        services.AddScoped<TraditionalCourseReviewService>();
        services.AddSingleton<TraditionalCourseDeletionService>();

        // "Edit mode" add-section feature (2026-08-11) — mirrors Graph's
        // GraphNodeAdditionAgent/AddCapabilityGraphNodeFunction. Singleton
        // (uses IDbContextFactory internally, same as StructuredLearningPedagogyService
        // above, whose own RunAsync it reuses as-is to generate the new
        // section's steps).
        services.AddSingleton<HumanOS.Agents.Studio.StructuredLearningSectionAdditionAgent>();
        services.AddSingleton<StructuredLearningSectionAdditionService>();

        // "Edit mode" delete-section feature (2026-08-11) — mirrors Graph's
        // CapabilityGraphPersistenceService.DeleteNodeAsync. Singleton (uses
        // IDbContextFactory internally, same as the addition service above).
        services.AddSingleton<StructuredLearningSectionDeletionService>();

        // "Edit mode" delete-document feature (2026-08-11) — permanently
        // removes a structured-learning document and all of its generated
        // content. Singleton (uses IDbContextFactory internally to handle
        // the cross-table cascade cleanup atomically).
        services.AddSingleton<StructuredLearningDocumentDeletionService>();

        // On-demand "Profundizar" (Knowledge Expansion) feature (2026-07-20,
        // see /memories/repo/... knowledge-expansion notes): learner-triggered
        // deeper explanation combining the LLM's own knowledge with a live
        // Bing Grounding search, plus an optional diagram. Singleton, same
        // rationale as the other Studio/pipeline services (no per-request
        // state; reuses the already-singleton WebGroundingService/
        // GraphIllustrationImageService).
        services.AddSingleton<HumanOS.Agents.Runtime.KnowledgeExpansionAgent>();
        services.AddSingleton<KnowledgeExpansionService>();

        // Interactive Learning Runtime — Tutor Agent (Paso 4, 2026-07-14,
        // see /memories/repo/human-os-runtime-design.md). Singleton, same
        // pattern as the Studio pipeline agents (no per-request state).
        services.AddSingleton<HumanOS.Agents.Runtime.TutorAgent>();

        // Instructor Runtime — Assessment Evaluator (Runtime Paso 3,
        // 2026-07-17, see /memories/repo/runtime-v1-learning-session-model.md).
        // Singleton, same pattern as the Studio pipeline agents (no
        // per-request state).
        services.AddSingleton<HumanOS.Agents.Runtime.AssessmentEvaluatorAgent>();

        // Instructor Runtime — Adaptive Assessment Agent (2026-07-18): the
        // new dynamic, one-question-at-a-time Assessment redesign. Simple
        // ChatClientAgent pattern (user-confirmed choice, NOT the native
        // Agent Framework Workflow mandate) — coexists with, and does NOT
        // replace, AssessmentEvaluatorAgent above (that one stays wired to
        // the OLD single-free-text evaluate endpoint, grandfathered).
        // Singleton, same pattern as every other agent (no per-request state).
        services.AddSingleton<HumanOS.Agents.Runtime.AdaptiveAssessmentAgent>();

        // Instructor Runtime — Production Evaluator Agent (2026-07-18):
        // formative (non-scoring) grading for the "Aplícalo" (Production)
        // step — see ProductionEvaluationService.cs/ProductionEvaluationGate.
        // Same simple ChatClientAgent pattern. Singleton, same pattern as
        // every other agent (no per-request state).
        services.AddSingleton<HumanOS.Agents.Runtime.ProductionEvaluatorAgent>();

        // TutorAgent V2 — the first agent built under the frozen "Version 2"
        // Agent Framework Workflow mandate (see
        // /memories/repo/agent-framework-native-architecture-mandate.md).
        // Distinct from the OLD HumanOS.Agents.Runtime.TutorAgent above
        // (Runtime #1, still live) — do NOT touch that registration.
        // Singleton, same pattern as every other agent (no per-request
        // state); TutorService (scoped, above) builds a fresh Workflow via
        // TutorWorkflowFactory per call.
        services.AddSingleton<HumanOS.Agents.Runtime.TutorAgentV2>();

        // Real neural Text-to-Speech via Azure AI Speech (fixed
        // 2026-07-16 — explicit user requirement: direct call to the
        // Azure Speech service's neural voices, no LLM/chatbot involved).
        // Singleton: holds no per-request state, same rationale as
        // TutorAgent above.
        services.AddSingleton<AzureSpeechService>();

        // Voice Tutor — ephemeral Azure OpenAI Realtime (gpt-realtime-mini
        // or similar) session minting (2026-07-21, see
        // /memories/repo/human-os-runtime-design.md for the surrounding
        // design discussion). Singleton: holds no per-request state, only
        // wraps IConfiguration + IHttpClientFactory (registered below).
        services.AddSingleton<RealtimeVoiceSessionService>();

        // Interactive Learning Runtime — Workflow checkpointing (Paso 3,
        // 2026-07-14, see /memories/repo/human-os-runtime-design.md).
        // SqlRuntimeCheckpointStore is Singleton-safe (only holds
        // IDbContextFactory<HumanOsDbContext>, same pattern as
        // CapabilityCreationOrchestrator). CheckpointManager wraps it with
        // JSON serialization for use by InProcessExecution.RunStreamingAsync/
        // ResumeStreamingAsync when a Runtime session is actually started
        // (a later Paso — not wired to any endpoint yet).
        services.AddSingleton<SqlRuntimeCheckpointStore>();
        services.AddSingleton<ICheckpointStore<JsonElement>>(sp =>
            sp.GetRequiredService<SqlRuntimeCheckpointStore>());
        services.AddSingleton(sp => CheckpointManager.CreateJson(
            sp.GetRequiredService<ICheckpointStore<JsonElement>>(),
            new JsonSerializerOptions()));

        // Add HTTP client factory
        services.AddHttpClient();
    })
    .Build();

// Check if running a test (e.g., `dotnet run -- --run-test`)
if (args.Contains("--run-test"))
{
    using (var scope = host.Services.CreateScope())
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var test = new HumanOS.Tests.TestCuradorGraphArchitectFlow(config);
        await test.RunAsync();
    }
}
else if (args.Contains("--seed-catalog") || args.Contains("--seed-badges"))
{
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<HumanOsDbContext>();
        if (args.Contains("--seed-catalog"))
        {
            await HumanOS.Data.CatalogSeeder.SeedGoalsAndMotivationsAsync(dbContext);
        }
        await HumanOS.Data.CatalogSeeder.SeedBadgesAsync(dbContext);
    }
}
else
{
    host.Run();
}
