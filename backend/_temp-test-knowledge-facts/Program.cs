using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.Studio;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var settingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HumanOS", "local.settings.json");
var raw = new ConfigurationBuilder().AddJsonFile(settingsPath, optional: false).Build();
var values = raw.GetSection("Values").GetChildren().ToDictionary(item => item.Key, item => item.Value);
var configuration = new ConfigurationBuilder().AddInMemoryCollection(values!).Build();

var inputPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "_temp-test-knowledge-facts", "knowledge-graph-input.json");
var sourceTextPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "_temp-test-knowledge-facts", "source-text.txt");
var graph = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(inputPath));
var entities = graph.GetProperty("entities").EnumerateArray().Select(entity => new StructuredLearningKnowledgeFactEntityInput
{
    EntityId = Guid.Parse(entity.GetProperty("EntityId").GetString()!),
    CanonicalName = entity.GetProperty("CanonicalName").GetString() ?? string.Empty,
    EntityType = entity.GetProperty("EntityType").GetString() ?? string.Empty,
    Description = entity.TryGetProperty("Description", out var description) && description.ValueKind != JsonValueKind.Null ? description.GetString() : null,
    Aliases = entity.TryGetProperty("Aliases", out var aliases) ? aliases.EnumerateArray().Select(alias => alias.GetString() ?? string.Empty).ToList() : []
}).ToList();
var connections = graph.GetProperty("connections").EnumerateArray().Select(connection => new StructuredLearningKnowledgeFactConnectionInput
{
    ConnectionId = Guid.Parse(connection.GetProperty("ConnectionId").GetString()!),
    SourceEntityId = Guid.Parse(connection.GetProperty("SourceEntityId").GetString()!),
    SourceEntityName = connection.GetProperty("SourceEntityName").GetString() ?? string.Empty,
    TargetEntityId = Guid.Parse(connection.GetProperty("TargetEntityId").GetString()!),
    TargetEntityName = connection.GetProperty("TargetEntityName").GetString() ?? string.Empty,
    RelationshipCategory = connection.GetProperty("RelationshipCategory").GetString() ?? string.Empty,
    RelationshipLabel = connection.GetProperty("RelationshipLabel").GetString() ?? string.Empty,
    Evidence = connection.TryGetProperty("Evidence", out var evidence) && evidence.ValueKind != JsonValueKind.Null ? evidence.GetString() : null,
    EpistemicStatus = connection.GetProperty("EpistemicStatus").GetString() ?? "explicit",
    Confidence = connection.TryGetProperty("Confidence", out var confidence) && confidence.ValueKind != JsonValueKind.Null ? confidence.GetDouble() : null
}).ToList();

var request = new StructuredLearningKnowledgeFactGenerationRequest
{
    EngramId = Guid.Parse("073080c1-68a1-42d8-8e70-b422a2cdc2d7"),
    EngramName = "Understand the foundations of AI",
    Language = "en",
    SourceText = await File.ReadAllTextAsync(sourceTextPath),
    Entities = entities,
    Connections = connections
};

var agent = new StructuredLearningKnowledgeFactGenerationAgent(configuration);
var response = await agent.GenerateAsync(request, CancellationToken.None);
var outputPath = Path.Combine(AppContext.BaseDirectory, "knowledge-facts-result.json");
await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Input: {entities.Count} reduced entities, {connections.Count} reduced connections");
Console.WriteLine($"Model response: {response.KnowledgeFacts.Count} facts, {response.MissingGraphFacts.Count} missing facts");
Console.WriteLine($"JSON saved to: {outputPath}");
Console.WriteLine(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Persistence: none (no DbContext used).");
