using System.Collections.Concurrent;
using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public sealed class ProcessDocumentStatus
{
    public string DocumentId { get; set; } = string.Empty;
    public string Status { get; set; } = "running"; // running, completed, failed
    public string? ErrorMessage { get; set; }
    public ProcessDocumentUploadResponse? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Tracks in-progress and completed process document extraction jobs,
/// enabling asynchronous polling via GET /api/process-documents/{documentId}/status.
/// </summary>
public sealed class ProcessDocumentOrchestrator
{
    private readonly ProcessDocumentPipelineService _pipeline;
    private readonly ConcurrentDictionary<string, ProcessDocumentStatus> _jobs = new();

    public ProcessDocumentOrchestrator(ProcessDocumentPipelineService pipeline)
    {
        _pipeline = pipeline;
    }

    public ProcessDocumentStatus? GetStatus(string documentId)
    {
        _jobs.TryGetValue(documentId, out var status);
        return status;
    }

    public void RegisterCompleted(string documentId, ProcessDocumentUploadResponse response)
    {
        var status = new ProcessDocumentStatus
        {
            DocumentId = documentId,
            Status = "completed",
            Result = response,
            CompletedAt = DateTime.UtcNow
        };
        _jobs[documentId] = status;
    }

    public string StartAsync(
        byte[] fileBytes,
        string fileName,
        string? contentType,
        string userId,
        string organizationId,
        string? processName = null,
        string? processDescription = null)
    {
        var documentId = Guid.NewGuid().ToString("D").ToLowerInvariant();
        var status = new ProcessDocumentStatus
        {
            DocumentId = documentId,
            Status = "running",
            CreatedAt = DateTime.UtcNow
        };
        _jobs[documentId] = status;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _pipeline.ProcessAsync(
                    fileBytes,
                    fileName,
                    contentType,
                    userId,
                    organizationId,
                    processName,
                    processDescription);

                status.Status = "completed";
                status.Result = result;
                status.CompletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                status.Status = "failed";
                status.ErrorMessage = ex.Message;
                status.CompletedAt = DateTime.UtcNow;
            }
        });

        return documentId;
    }
}
