using HumanOS.Models.Capabilities.Graph;
using HumanOS.Services;

namespace HumanOS.AzureFunctions.Api;

/// <summary>One resolved illustration for a blueprint step review.</summary>
public sealed class BlueprintIllustrationDto
{
    public Guid IllustrationId { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? Caption { get; set; }
}

/// <summary>One step of a node's Memory Paradox blueprint, for Studio's Demo/Edición review modes.</summary>
public sealed class BlueprintStepDto
{
    public string StepType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<BlueprintIllustrationDto> Illustrations { get; set; } = [];
}

/// <summary>Output of GetNodeBlueprint — every step of a node's blueprint at once, with no session/progression gating.</summary>
public sealed class NodeBlueprintDto
{
    public Guid CapabilityGraphNodeId { get; set; }
    public Guid NodeExperienceBlueprintId { get; set; }
    public List<BlueprintStepDto> Steps { get; set; } = [];
}

/// <summary>Request body for EditNodeBlueprintStep.</summary>
public sealed class EditBlueprintStepRequest
{
    public string Instruction { get; set; } = string.Empty;
}

internal static class BlueprintReviewApiMappers
{
    public static BlueprintIllustrationDto ToDto(BlueprintReviewService.IllustrationRef illustration) => new()
    {
        IllustrationId = illustration.IllustrationId,
        StoragePath = illustration.StoragePath,
        Caption = illustration.Caption
    };

    public static BlueprintStepDto ToDto(BlueprintReviewService.BlueprintStepResult step) => new()
    {
        StepType = step.StepType.ToString(),
        Content = step.Content,
        Illustrations = step.Illustrations.Select(ToDto).ToList()
    };

    public static NodeBlueprintDto ToDto(Guid capabilityGraphNodeId, BlueprintReviewService.BlueprintResult blueprint) => new()
    {
        CapabilityGraphNodeId = capabilityGraphNodeId,
        NodeExperienceBlueprintId = blueprint.NodeExperienceBlueprintId,
        Steps = blueprint.Steps.Select(ToDto).ToList()
    };
}
