namespace HumanOS.Models.Capabilities.Studio;

/// <summary>What a <see cref="StructuredLearningIllustration"/> is used for
/// — mirrors <see cref="HumanOS.Models.Capabilities.Traditional.TraditionalIllustrationPurpose"/>'s
/// simplicity (this pipeline has no Hypothesis/Teaching illustrations today,
/// only DeepDive example cards).</summary>
public enum StructuredLearningIllustrationPurpose
{
    /// <summary>Document-level cover image.</summary>
    Cover = 3,

    /// <summary>One DeepDive example card's illustration.</summary>
    DeepDiveCard = 0,

    /// <summary>The Hypothesis step's illustration.</summary>
    Hypothesis = 1,

    /// <summary>The Teaching step's illustration — always present.</summary>
    Teaching = 2,

    /// <summary>One Static-mode Recall item's pre-generated card illustration
    /// (2026-10-26) — see <see cref="HumanOS.Services.StructuredLearningRecallEngine.EnsureStaticItemsAsync"/>.
    /// Generated once at authoring time (same engram-generation pass as
    /// everything else), then shown to every student who reaches that
    /// Recall item — never regenerated per-student/per-attempt.</summary>
    RecallCard = 4,

    /// <summary>A REAL image extracted from the source HumanOS process
    /// document (e.g. a diagram/screenshot captured in a process step),
    /// as opposed to every other value here which is AI-generated. Shown
    /// alongside the Teaching illustration so the student sees the actual
    /// source figure the professor is narrating, not just an AI
    /// approximation of it (2026-11-03).</summary>
    SourceDocument = 5
}
