using HumanOS.Data;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

var connectionString =
    builder.Configuration.GetConnectionString("HumanOSDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'HumanOSDatabase' was not found.");

builder.Services.AddDbContext<HumanOsDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Add Services
builder.Services.AddScoped<PersonService>();
builder.Services.AddScoped<HumanProfileService>();
builder.Services.AddScoped<CapabilityService>();
builder.Services.AddScoped<PersonCapabilityService>();
builder.Services.AddScoped<PracticeService>();
builder.Services.AddScoped<RecallService>();
builder.Services.AddScoped<GoalService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<EvidenceService>();
builder.Services.AddScoped<AssessmentService>();
builder.Services.AddScoped<TranslationService>();

// Add HTTP client factory
builder.Services.AddHttpClient();

builder.Build().Run();
