namespace HumanOS.Models.Capabilities;

/// <summary>
/// A short, ordered sub-section of a module's real teaching content —
/// added 2026-07-16 so the Instructor's content is already segmented for
/// a future turn-based/voice Runtime presentation (e.g. "Capítulo 2:
/// calentar el agua a ~94°C") instead of one long monolithic Script. Not
/// yet consumed by the Interactive Learning Runtime (which still uses
/// <see cref="CapabilityModule.Script"/> as a whole) — this exists so the
/// data is ready and waiting once that Runtime work happens.
/// </summary>
public sealed class CapabilityModuleChapter
{
    public Guid CapabilityModuleChapterId { get; set; }

    public Guid CapabilityModuleId { get; set; }

    public int SortOrder { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>Real, specific declarative content for THIS chapter only
    /// (a coherent segment of the module's overall teaching content) — not
    /// a task description or a restatement of the LearnerTask.</summary>
    public string TeachingContent { get; set; } = null!;

    /// <summary>Exactly ONE chapter per module has this set to
    /// <see langword="true"/> (fixed 2026-07-16 — the "⭐ aquí va todo el
    /// peso" phase) — the chapter carrying the module's most complex
    /// concept(s), the cumulative recall, the single strong prediction,
    /// and the mini-practice.</summary>
    public bool IsPrimaryWeight { get; set; }

    /// <summary>This chapter's own retrieval prompt (fixed 2026-07-16) —
    /// every chapter has one.</summary>
    public string RecallPrompt { get; set; } = null!;

    /// <summary><see langword="false"/> = asks only about this chapter's
    /// own content; <see langword="true"/> = asks the learner to recall
    /// everything taught so far, cumulatively — used by the primary-weight
    /// chapter and the closing chapter only.</summary>
    public bool IsCumulativeRecall { get; set; }

    /// <summary>Set ONLY on the chapter with <see cref="IsPrimaryWeight"/>
    /// = <see langword="true"/> — this module's single strong anticipation
    /// question (e.g. "¿qué domina más la extracción, la temperatura o el
    /// tiempo?"). <see langword="null"/> on every other chapter.</summary>
    public string? PredictionPrompt { get; set; }

    /// <summary>Optional, set ONLY on the primary-weight chapter: a short
    /// practice exercise solved before moving on.</summary>
    public string? MiniPracticePrompt { get; set; }

    public DateTime CreatedDate { get; set; }

    public CapabilityModule CapabilityModule { get; set; } = null!;
}
