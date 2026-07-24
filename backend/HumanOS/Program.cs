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
        services.AddScoped<PersonProfileService>();
        services.AddScoped<GrowthPlanService>();
        services.AddScoped<HumanProfileService>();
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
        services.AddScoped<CapabilityCostService>();
        services.AddScoped<HumanOS.Storage.ProgramLogoStorageService>();
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
        services.AddScoped<JobDescriptionExtractionAgent>();
        services.AddScoped<HumanOS.Storage.CapabilityGraphIllustrationStorageService>();

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
        services.AddSingleton<ArquitectoAgent>();
        services.AddSingleton<GraphArchitectAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.DocumentContextAgent>();
        services.AddSingleton<GraphIllustrationImageService>();
        services.AddSingleton<ExperienceDesignerAgent>();
        services.AddSingleton<BlueprintValidatorAgent>();
        services.AddSingleton<HumanOS.Agents.Studio.BlueprintStepEditorAgent>();
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
        services.AddSingleton<PdfCapabilityGraphPipelineService>();
        services.AddSingleton<PdfCapabilityGraphOrchestrator>();

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
else if (args.Contains("--seed-catalog"))
{
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<HumanOsDbContext>();
        await HumanOS.Data.CatalogSeeder.SeedGoalsAndMotivationsAsync(dbContext);
    }
}
else
{
    host.Run();
}
