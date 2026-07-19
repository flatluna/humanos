// Test file — can be deleted after validation.
// This demonstrates GraphArchitectAgent usage on the "Suma Básica" corpus.

#if DEBUG_GRAPHARCHITECT_TEST

using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents.Studio;

/// <summary>
/// MANUAL TEST: Validates that GraphArchitectAgent correctly extracts a
/// learning graph from a curated corpus. This test uses the "Suma Básica"
/// (Basic Addition) domain.
///
/// RUN: Uncomment #if DEBUG_GRAPHARCHITECT_TEST above, compile, then run
/// from main() as needed. Delete this file after validation.
/// </summary>
internal sealed class GraphArchitectAgentTestFixture
{
    private readonly GraphArchitectAgent _agent;

    public GraphArchitectAgentTestFixture(IConfiguration configuration)
    {
        _agent = new GraphArchitectAgent(configuration);
    }

    public async Task RunAsync()
    {
        if (!_agent.IsConfigured)
        {
            Console.WriteLine("GraphArchitectAgent is not configured. Check appsettings.");
            return;
        }

        // Test corpus: "Suma Básica" (Basic Addition)
        var testCorpus = new CuratedCorpus
        {
            Summary = """
                La suma es una operación matemática que permite combinar cantidades.
                La suma puede entenderse como juntar grupos de objetos.
                Las personas utilizan la suma para resolver problemas cotidianos.
                """,
            Chunks =
            [
                new CuratedChunk
                {
                    Tag = "DEFINITION",
                    Content = "La suma combina dos o más cantidades para obtener un total."
                },
                new CuratedChunk
                {
                    Tag = "CONCEPT",
                    Content = "La suma representa la unión de cantidades."
                },
                new CuratedChunk
                {
                    Tag = "EXAMPLE",
                    Content = "2 + 3 = 5"
                },
                new CuratedChunk
                {
                    Tag = "EXAMPLE",
                    Content = "4 manzanas + 2 manzanas = 6 manzanas"
                },
                new CuratedChunk
                {
                    Tag = "APPLICATION",
                    Content =
                        "Calcular cuántos objetos hay después de agregar nuevos elementos a un conjunto existente."
                }
            ]
        };

        Console.WriteLine("════════════════════════════════════════════════════════");
        Console.WriteLine("GRAPHARCHITECTAGENT TEST — Suma Básica");
        Console.WriteLine("════════════════════════════════════════════════════════\n");

        Console.WriteLine("INPUT:");
        Console.WriteLine($"  Capability: Suma Básica");
        Console.WriteLine($"  Domain: Matemáticas");
        Console.WriteLine($"  Chunks: {testCorpus.Chunks.Count}");
        Console.WriteLine();

        try
        {
            var result = await _agent.ExtractGraphAsync("Suma Básica", testCorpus);

            Console.WriteLine("OUTPUT:");
            Console.WriteLine($"  Graph ID: {result.Graph.CapabilityGraphId:D}");
            Console.WriteLine($"  Graph Name: {result.Graph.Name}");
            Console.WriteLine($"  Description: {result.Graph.Description}");
            Console.WriteLine();

            Console.WriteLine($"NODES ({result.Graph.Nodes.Count}):");
            foreach (var node in result.Graph.Nodes.OrderBy(n => n.SortOrder))
            {
                Console.WriteLine($"  [{node.SortOrder}] {node.Name}");
                Console.WriteLine($"      Type: {node.NodeType}");
                if (!string.IsNullOrEmpty(node.Description))
                    Console.WriteLine($"      Desc: {node.Description}");
            }

            Console.WriteLine();
            Console.WriteLine($"EDGES ({result.Graph.Edges.Count}):");
            foreach (var edge in result.Graph.Edges)
            {
                var srcName = result.Graph.Nodes.FirstOrDefault(n => n.NodeId == edge.SourceNodeId)?.Name ?? "?";
                var tgtName = result.Graph.Nodes.FirstOrDefault(n => n.NodeId == edge.TargetNodeId)?.Name ?? "?";
                Console.WriteLine($"  {srcName} --[{edge.RelationshipType}]--> {tgtName}");
                if (!string.IsNullOrEmpty(edge.Rationale))
                    Console.WriteLine($"    Rationale: {edge.Rationale}");
            }

            Console.WriteLine();
            Console.WriteLine($"TOKEN USAGE:");
            Console.WriteLine($"  Input: {result.TokenUsage.InputTokens}");
            Console.WriteLine($"  Output: {result.TokenUsage.OutputTokens}");
            Console.WriteLine($"  Cached: {result.TokenUsage.CachedInputTokens}");

            Console.WriteLine();
            Console.WriteLine("VALIDATION CHECKLIST:");
            Console.WriteLine(
                $"  ✓ Nodes represent learnable capabilities: {ValidateNodeSemanticsAsync(result.Graph)}");
            Console.WriteLine($"  ✓ No pedagogical elements (video, chapter, exercise): {ValidateNoPedagogicalNodes(result.Graph)}");
            Console.WriteLine($"  ✓ Graph is small and comprehensible: {result.Graph.Nodes.Count <= 30}");
            Console.WriteLine($"  ✓ No duplicate nodes: {result.Graph.Nodes.DistinctBy(n => n.Name).Count() == result.Graph.Nodes.Count}");
            Console.WriteLine($"  ✓ DAG (no obvious cycles): {ValidateDAGAsync(result.Graph)}");

            Console.WriteLine();
            Console.WriteLine("JSON OUTPUT:");
            Console.WriteLine(JsonSerializer.Serialize(result.Graph, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine();
            Console.WriteLine("════════════════════════════════════════════════════════");
            Console.WriteLine("TEST COMPLETE");
            Console.WriteLine("════════════════════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static bool ValidateNodeSemanticsAsync(CapabilityGraphResponse graph)
    {
        // Check that node names describe concepts/skills, not pedagogical elements
        var nodeNames = graph.Nodes.Select(n => n.Name.ToLowerInvariant()).ToList();

        var conceptualNames = new[]
        {
            "cantidad", "número", "operación", "suma", "agregar", "combinar", "resultado", "total"
        };

        return nodeNames.All(name =>
            conceptualNames.Any(c => name.Contains(c)) || // matches a known concept
            graph.Nodes.Any(n => n.Name.ToLowerInvariant() == name && n.NodeType == NodeType.Skill) || // is a skill
            graph.Nodes.Any(n => n.Name.ToLowerInvariant() == name && n.NodeType == NodeType.Concept) // is a concept
        );
    }

    private static bool ValidateNoPedagogicalNodes(CapabilityGraphResponse graph)
    {
        var forbiddenPatterns = new[]
        {
            "video", "capítulo", "chapter", "ejercicio", "exercise", "quiz", "test", "actividad", "activity", "lección",
            "lesson", "tarea", "task", "pdf", "material", "recurso"
        };

        var nodeNames = graph.Nodes.Select(n => n.Name.ToLowerInvariant()).ToList();

        return nodeNames.All(name => !forbiddenPatterns.Any(pattern => name.Contains(pattern)));
    }

    private static bool ValidateDAGAsync(CapabilityGraphResponse graph)
    {
        // Simple cycle detection via DFS
        var adjList = graph.Edges.GroupBy(e => e.SourceNodeId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.TargetNodeId).ToList());

        var visited = new HashSet<Guid>();
        var recStack = new HashSet<Guid>();

        foreach (var nodeId in graph.Nodes.Select(n => n.NodeId))
        {
            if (!visited.Contains(nodeId))
            {
                if (DfsCycleDetect(nodeId, adjList, visited, recStack))
                    return false; // Cycle found
            }
        }

        return true; // No cycles
    }

    private static bool DfsCycleDetect(Guid node, Dictionary<Guid, List<Guid>> adjList,
        HashSet<Guid> visited, HashSet<Guid> recStack)
    {
        visited.Add(node);
        recStack.Add(node);

        if (adjList.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    if (DfsCycleDetect(neighbor, adjList, visited, recStack))
                        return true;
                }
                else if (recStack.Contains(neighbor))
                {
                    return true; // Back edge, cycle detected
                }
            }
        }

        recStack.Remove(node);
        return false;
    }
}

#endif
