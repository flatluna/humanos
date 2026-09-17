using System.Text.Json.Serialization;

namespace HumanOS.Contracts.ProcessDocuments;

/// <summary>
/// Model for JSON-based upload of process documents (alternative to multipart/form-data).
/// </summary>
public sealed class ProcessDocumentUploadRequest
{
    [JsonPropertyName("process_name")]
    public string? ProcessName { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("organization_id")]
    public string? OrganizationId { get; set; }

    [JsonPropertyName("process_description")]
    public string? ProcessDescription { get; set; }

    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("content_base64")]
    public string? ContentBase64 { get; set; }

    [JsonPropertyName("is_async")]
    public bool IsAsync { get; set; }
}
