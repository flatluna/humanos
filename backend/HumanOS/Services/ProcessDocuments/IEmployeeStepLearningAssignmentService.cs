using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface IEmployeeStepLearningAssignmentService
{
    bool IsConfigured { get; }
    string? DeploymentName { get; }

    Task<EmployeeStepLearningAssignmentResponse> CreateEmployeeAssignmentAsync(
        Guid employeeId,
        string documentId,
        string stepId,
        string organizationId,
        string? language = null,
        CancellationToken cancellationToken = default);

    Task<EmployeeStepLearningAssignmentResponse?> GetAssignmentByIdAsync(
        string assignmentId,
        string? organizationId = null,
        CancellationToken cancellationToken = default);
}
