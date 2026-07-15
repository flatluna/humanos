using Azure.Identity;
using HumanOS.Agentic.Studio;
using HumanOS.Agents;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
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
        services.AddScoped<HumanProfileService>();
        services.AddScoped<LanguageService>();
        services.AddScoped<CapabilityDomainService>();
        services.AddScoped<CapabilityService>();
        services.AddScoped<PersonCapabilityService>();
        services.AddScoped<GoalService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<PracticeService>();
        services.AddScoped<RecallService>();
        services.AddScoped<EvidenceService>();
        services.AddScoped<AssessmentService>();
        services.AddScoped<TranslationService>();
        services.AddScoped<RoleDocumentStorageService>();
        services.AddScoped<JobDescriptionExtractionAgent>();

        // Human OS Studio — capability-creation pipeline agents (Microsoft
        // Agent Framework, ChatClientAgents with structured output; see
        // /memories/repo/humanstudio-multiagent-vision.md). Singleton: they
        // hold no per-request state, and CapabilityCreationOrchestrator (also
        // a singleton, since it keeps in-memory runs alive across HTTP calls)
        // depends on them directly.
        services.AddSingleton<CuradorAgent>();
        services.AddSingleton<ArquitectoAgent>();
        services.AddSingleton<InstructorAgent>();
        services.AddSingleton<MetricoAgent>();
        services.AddSingleton<ExperienciaAgent>();
        services.AddSingleton<CapabilityEmbeddingService>();
        services.AddSingleton<CapabilityCreationOrchestrator>();

        // Add HTTP client factory
        services.AddHttpClient();
    })
    .Build();

host.Run();
