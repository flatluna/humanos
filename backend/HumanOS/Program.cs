using Azure.Identity;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();

        var connectionString =
            Environment.GetEnvironmentVariable("HumanOSDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'HumanOSDatabase' was not found.");

        services.AddDbContext<HumanOsDbContext>(options =>
        {
            var connection = new SqlConnection(connectionString);
            
            // Get access token from Managed Identity (works in Azure automatically)
            var tokenProvider = new DefaultAzureCredential();
            var token = tokenProvider.GetToken(
                new Azure.Core.TokenRequestContext(
                    new[] { "https://database.windows.net/.default" }));
            
            connection.AccessToken = token.Token;
            
            options.UseSqlServer(
                connection,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
        });

        // Add Services
        services.AddScoped<PersonService>();
        services.AddScoped<HumanProfileService>();
        services.AddScoped<CapabilityService>();
        services.AddScoped<PersonCapabilityService>();
        services.AddScoped<PracticeService>();
        services.AddScoped<RecallService>();
        services.AddScoped<GoalService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<EvidenceService>();
        services.AddScoped<AssessmentService>();
        services.AddScoped<TranslationService>();

        // Add HTTP client factory
        services.AddHttpClient();
    })
    .Build();

host.Run();
