using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// PHASE B analyzer — derives business-process attributes (role, systems, trigger, business rules,
/// exceptions) for ONE process step at a time, using only that step's own verbatim
/// <see cref="ProcessStep.SourceContent"/> captured during the fast Phase A index extraction. Intended to
/// be invoked one step at a time (e.g. as the user reviews each step in the wizard), never for the whole
/// document in a single call.
/// </summary>
public interface IStepBusinessRequirementsAnalyzer
{
    bool IsConfigured { get; }
    string? DeploymentName { get; }

    Task<StepBusinessRequirementsAnalysisResult> AnalyzeStepAsync(
        string documentId,
        string processName,
        string chapterOrPhase,
        ProcessStep step,
        CancellationToken cancellationToken = default);
}
