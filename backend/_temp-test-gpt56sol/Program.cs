// Isolated iteration harness (2026-08-15) — runs ONLY the Knowledge Graph
// entity + connection extraction step against a fixed piece of text, using
// the REAL agent classes from the HumanOS backend (not mocks), so the
// prompts can be iterated on quickly without running the whole document
// pipeline (chunking, illustrations, pedagogy, etc). Reads the real
// endpoints/keys from backend/HumanOS/local.settings.json.
using HumanOS.Agents.Studio;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var settingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HumanOS", "local.settings.json");
var rawConfig = new ConfigurationBuilder()
    .AddJsonFile(settingsPath, optional: false)
    .Build();
// local.settings.json wraps everything under "Values" — flatten it so the
// agent constructors (which read configuration["AzureOpenAIEndpoint"] etc.
// directly, no section prefix) see the same keys they do inside the real
// Functions host.
var values = rawConfig.GetSection("Values");
var configDict = values.GetChildren().ToDictionary(c => c.Key, c => c.Value);
IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(configDict!).Build();

var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "_temp-test-gpt56sol", "ai-basic-concepts.txt"));
/*var text = """
    Artículo 1o.- Están obligadas al pago del impuesto al valor agregado establecido en esta Ley, las personas físicas y las morales que, en territorio nacional, realicen los actos o actividades siguientes:
    Párrafo reformado DOF 30-12-1980
    I.- Enajenen bienes.
    II.- Presten servicios independientes.
    III.- Otorguen el uso o goce temporal de bienes.
    IV.- Importen bienes o servicios.
    El impuesto se calculará aplicando a los valores que señala esta Ley, la tasa del 16%. El impuesto al valor agregado en ningún caso se considerará que forma parte de dichos valores.
    Párrafo reformado DOF 31-12-1982, 21-11-1991, 27-03-1995, 07-12-2009
    El contribuyente trasladará dicho impuesto, en forma expresa y por separado, a las personas que adquieran los bienes, los usen o gocen temporalmente, o reciban los servicios. Se entenderá por traslado del impuesto el cobro o cargo que el contribuyente debe hacer a dichas personas de un monto equivalente al impuesto establecido en esta Ley, inclusive cuando se retenga en los términos de
    los artículos 1o.-A, 3o., tercer párrafo o 18-J, fracción II, inciso a) de la misma.
    Párrafo reformado DOF 31-12-1998, 09-12-2019
    El contribuyente pagará en las oficinas autorizadas la diferencia entre el impuesto a su cargo y el que le hubieran trasladado o el que él hubiese pagado en la importación de bienes o servicios, siempre que sean acreditables en los términos de esta Ley. En su caso, el contribuyente disminuirá del impuesto a su cargo, el impuesto que se le hubiere retenido.
    Párrafo reformado DOF 31-12-1998
    El traslado del impuesto a que se refiere este artículo no se considerará violatorio de precios o tarifas, incluyendo los oficiales.
    """;*/

Console.WriteLine("=== Step 1: Entity extraction (gpt-5-mini) ===");
var entityAgent = new StructuredLearningEntityExtractionAgent(configuration);
if (!entityAgent.IsConfigured)
{
    Console.WriteLine("Entity agent not configured — check AzureOpenAIEndpoint/AzureOpenAIDeploymentName/AzureOpenAIApiKey.");
    return;
}
var sw1 = System.Diagnostics.Stopwatch.StartNew();
var entityResult = await entityAgent.ExtractAsync("Artículo 1o.", text, CancellationToken.None);
sw1.Stop();
Console.WriteLine($"  [step1 elapsed: {sw1.Elapsed}]");
foreach (var e in entityResult.Response.Entities)
{
    Console.WriteLine($"  {e.CanonicalName} [{e.EntityType}] <- \"{e.SourceText}\" | resolution={e.ResolutionStatus} confidence={e.Confidence:0.00}");
}
Console.WriteLine($"  ({entityResult.OutputTokens} output tokens)");

Console.WriteLine();
Console.WriteLine("=== Step 1b: Re-run SAME text with itself as the master registry (simulates a 2nd Engram in the same domain) ===");
var masterCandidates = entityResult.Response.Entities
    .Select((e, i) => ($"m{i}", e.CanonicalName, (IReadOnlyList<string>)e.Aliases))
    .ToList();
var entityResult2 = await entityAgent.ExtractAsync("Artículo 1o. (segunda pasada)", text, existingClassNames: [], masterCandidates, CancellationToken.None);
foreach (var e in entityResult2.Response.Entities)
{
    Console.WriteLine($"  {e.CanonicalName} [{e.EntityType}] | resolution={e.ResolutionStatus} candidate={e.MasterEntityCandidateId} confidence={e.Confidence:0.00} reason=\"{e.MatchReason}\"");
}


Console.WriteLine("=== Step 2: Connection extraction (gpt-5.6-sol) ===");
var connectionAgent = new StructuredLearningConnectionExtractionAgent(configuration);
if (!connectionAgent.IsConfigured)
{
    Console.WriteLine("Connection agent not configured — check ClaudeConnectionAgentEndpoint/DeploymentName/ApiKey.");
    return;
}
var candidateNames = entityResult.Response.Entities.Select(e => e.CanonicalName).Distinct().ToList();
var candidatePairs = candidateNames.Select((n, i) => ($"e{i}", n)).ToList();
var sw2 = System.Diagnostics.Stopwatch.StartNew();
var connectionResult = await connectionAgent.ExtractAsync("Artículo 1o.", text, candidatePairs, CancellationToken.None);
sw2.Stop();
var resultPath = Path.Combine(AppContext.BaseDirectory, "knowledge-graph-result.json");
await File.WriteAllTextAsync(
    resultPath,
    System.Text.Json.JsonSerializer.Serialize(connectionResult.Response, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"  JSON saved to: {resultPath}");
Console.WriteLine($"  [step2 elapsed: {sw2.Elapsed}]");
var idToName = candidatePairs.ToDictionary(p => p.Item1, p => p.Item2);
var rootName = connectionResult.Response.RootEntityId is string rid && idToName.TryGetValue(rid, out var rn) ? rn : connectionResult.Response.RootEntityId;
Console.WriteLine($"  Model's own RootEntityId (NOT used anymore): {connectionResult.Response.RootEntityId} ({rootName})");
const string sectionTitle = "Artículo 1o.";
var titleRootPair = candidatePairs.FirstOrDefault(p =>
    sectionTitle.Contains(p.Item2, StringComparison.OrdinalIgnoreCase) ||
    p.Item2.Contains(sectionTitle, StringComparison.OrdinalIgnoreCase));
Console.WriteLine($"  ACTUAL root used (forced = Engram's own title entity): {titleRootPair.Item2 ?? "(none matched, would fall back to model's choice)"}");
foreach (var c in connectionResult.Response.Connections)
{
    var srcName = idToName.TryGetValue(c.SourceEntityId, out var s) ? s : c.SourceEntityId;
    var tgtName = idToName.TryGetValue(c.TargetEntityId, out var t) ? t : c.TargetEntityId;
    Console.WriteLine($"  {srcName} --{c.RelationshipLabel}--> {tgtName} ({c.RelationshipCategory}) [{c.EpistemicStatus}, conf={c.Confidence:0.00}]");
}
Console.WriteLine($"  ({connectionResult.OutputTokens} output tokens)");
Console.WriteLine("=== RAW CONNECTION JSON ===");
var rawJson = System.Text.Json.JsonSerializer.Serialize(connectionResult.Response, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
var jsonOutputPath = Path.Combine(AppContext.BaseDirectory, "knowledge-graph-connections.json");
File.WriteAllText(jsonOutputPath, rawJson);
Console.WriteLine(rawJson);
Console.WriteLine($"JSON_FILE={jsonOutputPath}");
Console.WriteLine("=== MERMAID KNOWLEDGE GRAPH ===");
Console.WriteLine("graph LR");
foreach (var c in connectionResult.Response.Connections)
{
    var source = idToName.TryGetValue(c.SourceEntityId, out var sourceName) ? sourceName : c.SourceEntityId;
    var target = idToName.TryGetValue(c.TargetEntityId, out var targetName) ? targetName : c.TargetEntityId;
    var sourceId = c.SourceEntityId.Replace("-", "_");
    var targetId = c.TargetEntityId.Replace("-", "_");
    var label = $"{c.RelationshipCategory}: {c.RelationshipLabel}".Replace("\"", "'");
    Console.WriteLine($"    {sourceId}[\"{source}\"] -->|\"{label}\"| {targetId}[\"{target}\"]");
}

