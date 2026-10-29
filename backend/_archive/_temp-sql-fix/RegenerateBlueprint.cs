using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// One-off repair: node "Control de Edición" (c4f30b4d-19c1-4f21-a111-83870d5323d8)
// was created (2026-08-06 20:08 UTC) but its NodeExperienceBlueprint generation
// failed silently — 0 rows in NodeExperienceBlueprints/Steps for it — mirrors
// exactly what AddCapabilityGraphNodeFunction does right after node creation.
internal static class RegenerateBlueprint
{
    public static async Task RunAsync()
    {
        var targetNodeId = Guid.Parse("c4f30b4d-19c1-4f21-a111-83870d5323d8");

        var settingsPath = Path.Combine("..", "HumanOS", "local.settings.json");
        var json = File.ReadAllText(settingsPath);
        using var doc = JsonDocument.Parse(json);
        var values = doc.RootElement.GetProperty("Values");
        var inMemory = new Dictionary<string, string?>();
        foreach (var prop in values.EnumerateObject())
        {
            inMemory[prop.Name] = prop.Value.GetString();
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var optionsBuilder = new DbContextOptionsBuilder<HumanOsDbContext>();
        optionsBuilder.UseSqlServer(inMemory["HumanOSDatabase"]);
        await using var dbContext = new HumanOsDbContext(optionsBuilder.Options);

        var node = await dbContext.CapabilityGraphNodes
            .FirstOrDefaultAsync(n => n.CapabilityGraphNodeId == targetNodeId);
        if (node is null)
        {
            Console.WriteLine("Node not found.");
            return;
        }

        var capability = await dbContext.CapabilityGraphs
            .Where(g => g.CapabilityGraphId == node.CapabilityGraphId)
            .Select(g => g.CapabilityId)
            .FirstOrDefaultAsync();
        var capabilityMeta = await dbContext.Capabilities
            .Where(c => c.CapabilityId == capability)
            .Select(c => new { c.ContentLanguageCode, c.PedagogicalAudience, c.IncludeExamplesStep, c.IncludeProductionStep })
            .FirstOrDefaultAsync();

        var languageCode = capabilityMeta?.ContentLanguageCode ?? "es";
        var pedagogicalAudience = capabilityMeta?.PedagogicalAudience;
        var includeExamplesStep = capabilityMeta?.IncludeExamplesStep ?? true;
        var includeProductionStep = capabilityMeta?.IncludeProductionStep ?? true;

        var savedIllustrations = await dbContext.CapabilityGraphNodeIllustrations
            .Where(i => i.CapabilityGraphNodeId == node.CapabilityGraphNodeId)
            .OrderBy(i => i.CreatedDate)
            .ToListAsync();

        var availableIllustrations = savedIllustrations
            .Select((illustration, index) => new AvailableIllustrationDto
            {
                Index = index + 1,
                Prompt = illustration.Prompt,
                Caption = illustration.Caption,
                Purpose = illustration.Purpose
            })
            .ToList();

        var experienceDesignerAgent = new ExperienceDesignerAgent(configuration);
        if (!experienceDesignerAgent.IsConfigured)
        {
            Console.WriteLine("ExperienceDesignerAgent is NOT configured.");
            return;
        }

        Console.WriteLine($"Designing blueprint for node '{node.Name}' ({node.CapabilityGraphNodeId})...");
        var designResult = await experienceDesignerAgent.DesignBlueprintAsync(
            node, availableIllustrations, webFindings: null, languageCode: languageCode,
            pedagogicalAudience: pedagogicalAudience, includeExamplesStep: includeExamplesStep,
            includeProductionStep: includeProductionStep, cancellationToken: default);

        Console.WriteLine($"Blueprint designed with {designResult.Blueprint.Steps.Count} steps. Persisting...");

        var persistenceService = new NodeExperienceBlueprintPersistenceService();
        var blueprintEntity = await persistenceService.PersistAsync(
            dbContext, node.CapabilityGraphNodeId, designResult.Blueprint, savedIllustrations,
            includeExamplesStep: includeExamplesStep, includeProductionStep: includeProductionStep);

        Console.WriteLine($"Blueprint persisted: {blueprintEntity.NodeExperienceBlueprintId} with {blueprintEntity.Steps.Count} steps.");
    }

    public static async Task VerifyAsync()
    {
        var targetNodeId = Guid.Parse("c4f30b4d-19c1-4f21-a111-83870d5323d8");
        var settingsPath = Path.Combine("..", "HumanOS", "local.settings.json");
        var json = File.ReadAllText(settingsPath);
        using var doc = JsonDocument.Parse(json);
        var values = doc.RootElement.GetProperty("Values");
        var inMemory = new Dictionary<string, string?>();
        foreach (var prop in values.EnumerateObject())
        {
            inMemory[prop.Name] = prop.Value.GetString();
        }

        var optionsBuilder = new DbContextOptionsBuilder<HumanOsDbContext>();
        optionsBuilder.UseSqlServer(inMemory["HumanOSDatabase"]);
        await using var dbContext = new HumanOsDbContext(optionsBuilder.Options);

        var blueprints = await dbContext.NodeExperienceBlueprints
            .Where(b => b.CapabilityGraphNodeId == targetNodeId)
            .Include(b => b.Steps)
            .ToListAsync();

        foreach (var b in blueprints)
        {
            Console.WriteLine($"Blueprint {b.NodeExperienceBlueprintId}: {b.Steps.Count} steps -> {string.Join(", ", b.Steps.Select(s => s.StepType))}");
        }
    }
}
