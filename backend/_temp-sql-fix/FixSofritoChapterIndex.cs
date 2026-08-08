using System.Text.Json;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

// One-off repair (2026-08-06): node "Uso de Sofrito Comercial"
// (72fb1cd5-2440-4ffa-98b2-229587c74126, capability "Curso de Paella")
// was created via AddCapabilityGraphNodeFunction BEFORE the ChapterIndex fix
// — it was persisted with a null ChapterIndex, which groupByChapterIndex on
// the frontend buckets into "unassigned" and always renders LAST, instead of
// alongside its sibling "Preparación del Sofrito" in chapter 2 ("Sofrito y
// construcción del sabor"). Backfills the correct ChapterIndex directly.
internal static class FixSofritoChapterIndex
{
    public static async Task RunAsync()
    {
        var targetNodeId = Guid.Parse("72fb1cd5-2440-4ffa-98b2-229587c74126");
        const int correctChapterIndex = 2;

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

        var node = await dbContext.CapabilityGraphNodes
            .FirstOrDefaultAsync(n => n.CapabilityGraphNodeId == targetNodeId);
        if (node is null)
        {
            Console.WriteLine("Node not found.");
            return;
        }

        Console.WriteLine($"Before: Name={node.Name}, ChapterIndex={node.ChapterIndex}");
        node.ChapterIndex = correctChapterIndex;
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"After: Name={node.Name}, ChapterIndex={node.ChapterIndex}");
    }
}
