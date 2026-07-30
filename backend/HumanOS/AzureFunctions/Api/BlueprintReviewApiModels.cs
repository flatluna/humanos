using HumanOS.Models.Capabilities.Graph;
using HumanOS.Services;

namespace HumanOS.AzureFunctions.Api;

/// <summary>One resolved illustration for a blueprint step review.</summary>
public sealed class BlueprintIllustrationDto
{
    public Guid IllustrationId { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string? Caption { get; set; }

    /// <summary>Raw generation-model tag ("deterministic-skiasharp-v1" or a
    /// generative image model name like "gpt-image-2").</summary>
    public string ImageModel { get; set; } = string.Empty;

    /// <summary>Human-readable provenance label for a small caption in the
    /// UI, e.g. "Generado con .NET (determinístico)" or "Generado con IA
    /// (gpt-image-2)" — computed here (never trusted from the frontend) so
    /// every consumer shows the exact same wording.</summary>
    public string GeneratedByLabel { get; set; } = string.Empty;
}

/// <summary>One example card of an Examples step, for Studio's Demo/Edición review modes.</summary>
public sealed class BlueprintExampleCardDto
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Emoji { get; set; }
    public Guid? IllustrationId { get; set; }
}

/// <summary>One step of a node's Memory Paradox blueprint, for Studio's Demo/Edición review modes.</summary>
public sealed class BlueprintStepDto
{
    public string StepType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<BlueprintIllustrationDto> Illustrations { get; set; } = [];

    /// <summary>Only populated when StepType == "Examples".</summary>
    public List<BlueprintExampleCardDto>? ExampleCards { get; set; }
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
    private const string DeterministicModelTag = "deterministic-skiasharp-v1";

    public static BlueprintIllustrationDto ToDto(BlueprintReviewService.IllustrationRef illustration) => new()
    {
        IllustrationId = illustration.IllustrationId,
        StoragePath = illustration.StoragePath,
        Caption = illustration.Caption,
        ImageModel = illustration.ImageModel,
        GeneratedByLabel = illustration.ImageModel == DeterministicModelTag
            ? "Generado con .NET (determinístico, sin IA)"
            : $"Generado con IA (modelo: {(string.IsNullOrWhiteSpace(illustration.ImageModel) ? "desconocido" : illustration.ImageModel)})"
    };

    public static BlueprintStepDto ToDto(BlueprintReviewService.BlueprintStepResult step) => new()
    {
        StepType = step.StepType.ToString(),
        Content = step.Content,
        Illustrations = step.Illustrations.Select(ToDto).ToList(),
        ExampleCards = step.ExampleCards?.Select(c => new BlueprintExampleCardDto { Title = c.Title, Text = c.Text, Emoji = c.Emoji, IllustrationId = c.IllustrationId }).ToList()
    };

    public static NodeBlueprintDto ToDto(Guid capabilityGraphNodeId, BlueprintReviewService.BlueprintResult blueprint) => new()
    {
        CapabilityGraphNodeId = capabilityGraphNodeId,
        NodeExperienceBlueprintId = blueprint.NodeExperienceBlueprintId,
        Steps = blueprint.Steps.Select(ToDto).ToList()
    };
}
