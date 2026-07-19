using HumanOS.Agents.Studio;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// TEST FUNCTION — Validates GraphArchitectAgent on the "Suma Básica" corpus.
/// Endpoint: POST /api/test/graph-architect
/// This function can be deleted after validation is complete.
/// </summary>
public sealed class TestGraphArchitectFunction
{
    private readonly GraphArchitectAgent _graphArchitect;

    public TestGraphArchitectFunction(GraphArchitectAgent graphArchitect)
    {
        _graphArchitect = graphArchitect;
    }

    [Function("TestGraphArchitect")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "test/graph-architect")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger<TestGraphArchitectFunction>();

        if (!_graphArchitect.IsConfigured)
        {
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = "GraphArchitectAgent is not configured." });
            return errorResponse;
        }

        try
        {
            logger.LogInformation("Starting GraphArchitectAgent test (Suma Básica corpus)...");

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

            // Extract graph
            var result = await _graphArchitect.ExtractGraphAsync("Suma Básica", testCorpus);

            logger.LogInformation($"Graph extracted: {result.Graph.Nodes.Count} nodes, {result.Graph.Edges.Count} edges");

            // Validation checks
            var validations = new
            {
                nodeRepresentCapabilities = result.Graph.Nodes.All(n =>
                    !new[] { "video", "capítulo", "chapter", "ejercicio", "exercise", "quiz", "test" }
                        .Any(p => n.Name.ToLowerInvariant().Contains(p))),
                noPedagogicalElements = result.Graph.Nodes.All(n =>
                    !new[] { "video", "capítulo", "chapter", "ejercicio", "exercise", "quiz", "test", "material" }
                        .Any(p => n.Name.ToLowerInvariant().Contains(p))),
                isSmallAndComprehensible = result.Graph.Nodes.Count <= 30,
                noDuplicates = result.Graph.Nodes.DistinctBy(n => n.Name).Count() == result.Graph.Nodes.Count,
                noObviousCycles = !ContainsCycle(result.Graph)
            };

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                graph = result.Graph,
                tokenUsage = result.TokenUsage,
                validations
            });

            logger.LogInformation("TestGraphArchitect completed successfully.");
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError($"TestGraphArchitect failed: {ex.Message}");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message, stackTrace = ex.StackTrace });
            return errorResponse;
        }
    }

    private static bool ContainsCycle(CapabilityGraphResponse graph)
    {
        var adjList = graph.Edges.GroupBy(e => e.SourceNodeId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.TargetNodeId).ToList());

        var visited = new HashSet<Guid>();
        var recStack = new HashSet<Guid>();

        foreach (var nodeId in graph.Nodes.Select(n => n.NodeId))
        {
            if (!visited.Contains(nodeId))
            {
                if (DfsCycleDetect(nodeId, adjList, visited, recStack))
                    return true;
            }
        }

        return false;
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
                    return true;
                }
            }
        }

        recStack.Remove(node);
        return false;
    }
}
