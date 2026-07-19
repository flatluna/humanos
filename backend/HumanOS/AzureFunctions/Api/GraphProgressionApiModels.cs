using HumanOS.Services;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Runtime V1, Paso 5 (2026-07-17) — request/response DTOs for the HTTP API
/// that exposes <see cref="GraphProgressionEngine"/>. Kept as thin,
/// subject-agnostic wire types (never node/topic-specific) so the same
/// contract works for any Capability domain.
/// </summary>
public sealed class GraphNodeDto
{
    public Guid CapabilityGraphNodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class CanStartNodeResponseDto
{
    public bool CanStart { get; set; }
    public List<string> BlockedReasons { get; set; } = new();
}

internal static class GraphProgressionApiMappers
{
    public static GraphNodeDto ToDto(GraphProgressionEngine.GraphNodeInfo node) => new()
    {
        CapabilityGraphNodeId = node.CapabilityGraphNodeId,
        Name = node.Name,
        SortOrder = node.SortOrder
    };

    public static List<GraphNodeDto> ToDto(IEnumerable<GraphProgressionEngine.GraphNodeInfo> nodes) =>
        nodes.Select(ToDto).ToList();

    public static CanStartNodeResponseDto ToDto(GraphProgressionEngine.CanStartNodeResult result) => new()
    {
        CanStart = result.CanStart,
        BlockedReasons = result.BlockedReasons
    };
}
