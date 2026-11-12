using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// One classified item with a stable, document-unique identifier in the form
/// [ProcessCode]-[StepCode]-[CategoryPrefix][NN] (e.g. "CCR-03-02-IN01"). The ID is assigned by the backend
/// endpoint when mapping the AI's raw category output, never by the AI itself, so IDs stay stable across
/// re-analysis. If nothing in the source document supports a category, a single placeholder item with
/// Label = "None identified in the source" is kept instead of leaving the list empty, so the absence is
/// explicit rather than ambiguous.
/// </summary>
public sealed class CodedListItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}
