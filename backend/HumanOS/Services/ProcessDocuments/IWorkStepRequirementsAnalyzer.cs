using HumanOS.Contracts.ProcessDocuments;

namespace HumanOS.Services.ProcessDocuments;

public interface IWorkStepRequirementsAnalyzer
{
    bool IsConfigured { get; }
    string? DeploymentName { get; }

    Task<WorkStepRequirementsAnalysisResult> AnalyzeStepRequirementsAsync(
        string documentId,
        string processName,
        string chapterOrPhase,
        ProcessStep step,
        List<ProcessTable>? relevantTables = null,
        CancellationToken cancellationToken = default) =>
        AnalyzeStepRequirementsAsync(documentId, processName, chapterOrPhase, step, relevantTables, null, cancellationToken);

    Task<WorkStepRequirementsAnalysisResult> AnalyzeStepRequirementsAsync(
        string documentId,
        string processName,
        string chapterOrPhase,
        ProcessStep step,
        List<ProcessTable>? relevantTables,
        IReadOnlyList<SkillItem>? previouslyIdentifiedSkills,
        CancellationToken cancellationToken = default) =>
        AnalyzeStepRequirementsAsync(documentId, processName, chapterOrPhase, step, relevantTables, previouslyIdentifiedSkills, null, cancellationToken);

    Task<WorkStepRequirementsAnalysisResult> AnalyzeStepRequirementsAsync(
        string documentId,
        string processName,
        string chapterOrPhase,
        ProcessStep step,
        List<ProcessTable>? relevantTables,
        IReadOnlyList<SkillItem>? previouslyIdentifiedSkills,
        IReadOnlyList<SkillItem>? catalogSkills,
        CancellationToken cancellationToken = default);
}
