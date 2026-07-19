using HumanOS.Agents.Runtime;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Assembles a <see cref="TutorTurnContext"/> for the current turn and
/// computes <see cref="TutorToolPermissions"/> from <see cref="RuntimeStage"/>
/// (fixed 2026-07-14, before Paso 4's actual Tutor Agent — see
/// /memories/repo/human-os-runtime-design.md). This is the ONLY place
/// tool/knowledge permissions are decided — the Tutor Agent (Paso 4+) must
/// never compute or override these itself.
/// </summary>
internal static class TutorTurnContextBuilder
{
    /// <param name="moduleScript">The module's instructional script —
    /// pass <see langword="null"/> except when <paramref name="stage"/> is
    /// <see cref="RuntimeStage.Instruction"/> (caller's responsibility to
    /// fetch it lazily only when needed).</param>
    /// <param name="chapterIndex">Which <see cref="RuntimePedagogicalContract.Chapters"/>
    /// entry is active this turn (fixed 2026-07-16) — pass
    /// <see langword="null"/> except for the <c>Chapter*</c> stages
    /// (caller's responsibility, same lazy convention as
    /// <paramref name="moduleScript"/>).</param>
    /// <param name="chapterSourceTextOverride">Overrides the chapter
    /// field the Tutor would otherwise use as source text (fixed
    /// 2026-07-17 — presents ONE sub-question of a multi-part prompt at a
    /// time). Null means use the chapter's own field as before.</param>
    /// <param name="isMultiPartChapterDialogueTurn">True when this turn is
    /// one sub-question of a multi-part dialogue (fixed 2026-07-17).</param>
    /// <param name="learnerTaskOverride">Overrides the concrete
    /// LearnerTask text used to ground a LearnerProduction turn (fixed
    /// 2026-07-17) — one item of a multi-part task. Null means use the
    /// whole LearnerTask as-is.</param>
    /// <param name="isMultiPartLearnerTaskTurn">True when this
    /// LearnerProduction turn is one item of a multi-part task (fixed
    /// 2026-07-17).</param>
    public static TutorTurnContext Build(
        RuntimeSessionState state,
        RuntimeStage stage,
        string? moduleScript = null,
        int? chapterIndex = null,
        string? chapterSourceTextOverride = null,
        bool isMultiPartChapterDialogueTurn = false,
        string? learnerTaskOverride = null,
        bool isMultiPartLearnerTaskTurn = false)
    {
        return new TutorTurnContext
        {
            RuntimeSessionId = state.Session.RuntimeSessionId,
            CurrentStage = stage,
            Contract = state.Session.Contract,
            AccumulatedEvidence = [.. state.Session.Evidence],
            ModuleScript = stage == RuntimeStage.Instruction ? moduleScript : null,
            CurrentChapterIndex = IsChapterStage(stage) ? chapterIndex : null,
            ChapterSourceTextOverride = IsChapterStage(stage) ? chapterSourceTextOverride : null,
            IsMultiPartChapterDialogueTurn = IsChapterStage(stage) && isMultiPartChapterDialogueTurn,
            LearnerTaskOverride = stage == RuntimeStage.LearnerProduction ? learnerTaskOverride : null,
            IsMultiPartLearnerTaskTurn = stage == RuntimeStage.LearnerProduction && isMultiPartLearnerTaskTurn,
            ActiveSkill = TutorSkillSelector.Select(stage, state.Session.Contract.TargetMetric),
            LastAssessment = state.LastAssessment,
            Permissions = ComputePermissions(stage)
        };
    }

    private static bool IsChapterStage(RuntimeStage stage) => stage is
        RuntimeStage.ChapterTeaching or RuntimeStage.ChapterRecall or
        RuntimeStage.ChapterPrediction or RuntimeStage.ChapterMiniPractice;

    /// <summary>
    /// Per-stage tool/knowledge gating — the structural Memory Paradox
    /// enforcement mechanism (not a prompt instruction).
    /// </summary>
    private static TutorToolPermissions ComputePermissions(RuntimeStage stage) => stage switch
    {
        // The learner must retrieve/predict entirely on their own — ANY
        // knowledge lookup here would let the Tutor leak the answer before
        // the retrieval attempt happens, defeating the entire purpose of
        // these two stages.
        RuntimeStage.RecallRequired or RuntimeStage.PredictionRequired => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = false,
            AllowedTools = []
        },

        // The learner is producing their own artifact — knowledge lookup
        // is still withheld (anti-offloading), but objective computational
        // verification tools (not "answer lookup" tools) are fine: they
        // don't hand over conceptual content, only arithmetic/data checks.
        RuntimeStage.LearnerProduction => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = false,
            AllowedTools = [TutorTool.Calculator, TutorTool.TableReader]
        },

        // Presenting content is the natural home for knowledge lookup
        // (grounding explanations in the real module material / answering
        // an interrupting question).
        RuntimeStage.Instruction => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = true,
            AllowedTools = []
        },

        // ModuleStarted (fixed 2026-07-16): the Tutor's warm introduction
        // grounds itself in the module's Title/Description — this is
        // orientation, not a retrieval attempt, so knowledge lookup is
        // fine (same rationale as Instruction).
        RuntimeStage.ModuleStarted => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = true,
            AllowedTools = []
        },

        // Assessment may need to verify quantitative SuccessCriteria and
        // ground its judgment in real material.
        RuntimeStage.Assessment => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = true,
            AllowedTools = [TutorTool.Calculator, TutorTool.TableReader]
        },

        // Reflection may reference real material when helping the learner
        // connect prediction vs. outcome, but has no computational need.
        RuntimeStage.Reflection => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = true,
            AllowedTools = []
        },

        // ChapterTeaching/ChapterMiniPractice (fixed 2026-07-16): pure
        // presentation, same rationale as Instruction/ModuleStarted —
        // knowledge lookup grounds the phrasing, no anti-offloading risk.
        RuntimeStage.ChapterTeaching or RuntimeStage.ChapterMiniPractice => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = true,
            AllowedTools = []
        },

        // ChapterRecall/ChapterPrediction (fixed 2026-07-16): the learner
        // must retrieve/predict entirely on their own — same withholding
        // rationale as RecallRequired/PredictionRequired above.
        RuntimeStage.ChapterRecall or RuntimeStage.ChapterPrediction => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = false,
            AllowedTools = []
        },

        // Completed: no Tutor turn with pedagogical content happens here —
        // no tools/knowledge needed.
        _ => new TutorToolPermissions
        {
            KnowledgeAccessAllowed = false,
            AllowedTools = []
        }
    };
}
