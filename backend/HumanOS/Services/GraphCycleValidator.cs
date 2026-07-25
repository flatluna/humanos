namespace HumanOS.Services;

/// <summary>
/// Deterministic (non-LLM) DAG-safety check for the Studio "add node to an
/// existing CapabilityGraph" feature — the LLM proposes a node's content, but
/// code alone decides whether the professor's chosen Requires/RequiredBy
/// wiring is structurally safe (same "LLM proposes, code decides" convention
/// as BlueprintValidationGuard/RecallLoopGate — see
/// /memories/repo/graph-node-illustrations-pipeline.md).
/// </summary>
public static class GraphCycleValidator
{
    /// <summary>
    /// Returns true if adding <paramref name="proposedEdges"/> on top of
    /// <paramref name="existingEdges"/> (both "Source requires Target" pairs)
    /// would create a cycle anywhere in the resulting requires-graph.
    /// </summary>
    public static bool WouldCreateCycle(
        IEnumerable<(Guid Source, Guid Target)> existingEdges,
        IEnumerable<(Guid Source, Guid Target)> proposedEdges)
    {
        var adjacency = new Dictionary<Guid, List<Guid>>();

        void AddEdge(Guid source, Guid target)
        {
            if (!adjacency.TryGetValue(source, out var list))
            {
                list = new List<Guid>();
                adjacency[source] = list;
            }
            list.Add(target);
        }

        foreach (var (source, target) in existingEdges) AddEdge(source, target);
        foreach (var (source, target) in proposedEdges) AddEdge(source, target);

        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();

        bool Dfs(Guid node)
        {
            if (visited.Contains(node)) return false;
            if (visiting.Contains(node)) return true; // back-edge found => cycle

            visiting.Add(node);
            if (adjacency.TryGetValue(node, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (Dfs(neighbor)) return true;
                }
            }
            visiting.Remove(node);
            visited.Add(node);
            return false;
        }

        foreach (var node in adjacency.Keys)
        {
            if (Dfs(node)) return true;
        }

        return false;
    }
}
