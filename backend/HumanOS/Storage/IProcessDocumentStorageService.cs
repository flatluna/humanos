using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Storage;

public interface IProcessDocumentStorageService
{
    bool IsConfigured { get; }

    Task<string> UploadSourcePdfAsync(
        string organizationId,
        string documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadEnrichedPdfAsync(
        string organizationId,
        string storagePath,
        CancellationToken cancellationToken = default);

    Task<string> UploadEnrichedPdfAsync(
        string organizationId,
        string documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<string> UploadStepImageAsync(
        string organizationId,
        string documentId,
        string blobFileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<string> SaveRawExtractionAsync(
        string organizationId,
        string documentId,
        RawDocumentExtractionResult rawResult,
        CancellationToken cancellationToken = default);

    Task<string> SaveStructuredResultAsync(
        string organizationId,
        string documentId,
        ProcessDocumentStructure structuredResult,
        CancellationToken cancellationToken = default);

    Task<ProcessDocumentStructure?> GetStructuredResultAsync(
        string organizationId,
        string documentId,
        CancellationToken cancellationToken = default);

    Task<bool> HasDocumentAsync(
        string organizationId,
        string documentId,
        CancellationToken cancellationToken = default);

    Task<string> SaveEmployeeAssignmentAsync(
        string organizationId,
        string assignmentId,
        EmployeeStepLearningAssignmentResponse assignment,
        CancellationToken cancellationToken = default);

    Task<EmployeeStepLearningAssignmentResponse?> GetEmployeeAssignmentAsync(
        string organizationId,
        string assignmentId,
        CancellationToken cancellationToken = default);
}
