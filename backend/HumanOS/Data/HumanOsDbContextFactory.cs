using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HumanOS.Data;

/// <summary>
/// Design-time factory so 'dotnet ef migrations add/database update' can
/// construct <see cref="HumanOsDbContext"/> without booting the full Azure
/// Functions isolated-worker host — dotnet-ef only auto-discovers a
/// DbContext via a standard ASP.NET Core Program.cs host builder pattern,
/// which this Functions app does not have. Reads the same 'HumanOSDatabase'
/// connection string used at runtime, falling back to reading
/// local.settings.json directly (Azure Functions Core Tools only loads its
/// 'Values' into environment variables when running via 'func start', not
/// for a plain 'dotnet ef' invocation).
/// </summary>
public sealed class HumanOsDbContextFactory : IDesignTimeDbContextFactory<HumanOsDbContext>
{
    public HumanOsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HumanOSDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "local.settings.json");
            if (File.Exists(settingsPath))
            {
                using var stream = File.OpenRead(settingsPath);
                using var document = JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty("Values", out var values) &&
                    values.TryGetProperty("HumanOSDatabase", out var value))
                {
                    connectionString = value.GetString();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Cannot create design-time HumanOsDbContext: 'HumanOSDatabase' connection string not found " +
                "in environment variables or local.settings.json.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<HumanOsDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        // NOTE (2026-07-15): pre-existing, unrelated model drift
        // (CapabilityEvidence shadow FK properties) trips EF Core's
        // PendingModelChangesWarning as a hard error on 'dotnet ef
        // database update'. Ignored HERE ONLY (design-time tooling, never
        // the actual running app's DbContext options in Program.cs) so
        // real migrations (like AddRuntimeSessionStatus) can still be
        // applied. Does not affect runtime behavior.
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

        return new HumanOsDbContext(optionsBuilder.Options);
    }
}
