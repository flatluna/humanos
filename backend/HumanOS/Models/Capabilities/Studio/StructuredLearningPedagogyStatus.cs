namespace HumanOS.Models.Capabilities.Studio;

/// <summary>
/// Per-section pedagogy-generation status (Step 3) — lets a resumed run
/// query "which sections still need work" instead of reprocessing
/// already-completed ones after a crash/interruption.
/// </summary>
public enum StructuredLearningPedagogyStatus
{
    NotStarted = 0,
    Generating = 1,
    Completed = 2,
    Failed = 3
}
