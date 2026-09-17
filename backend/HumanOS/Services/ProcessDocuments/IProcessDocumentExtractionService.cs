using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface IProcessDocumentExtractionService
{
    Task<(RawDocumentExtractionResult Raw, ProcessDocumentStructure Structured)> ExtractAndStructureAsync(
        byte[] pdfBytes,
        string fileName,
        string documentId,
        string? providedProcessName,
        string? processDescription,
        string sourceStoragePath,
        CancellationToken cancellationToken = default);
}
