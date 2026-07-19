using HumanOS.Agents.Runtime;
using HumanOS.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Agentic.Runtime;

/// <summary>
/// Shared engine behind the Runtime's HTTP API (Paso 9, 2026-07-15, see
/// /memories/repo/human-os-runtime-design.md) — every endpoint is a THIN
/// HTTP translation layer over these operations; none of this class knows
/// about <c>HttpRequestData</c>/routes. Deliberately STATELESS between
/// calls (no in-memory run registry, unlike Studio's
/// <c>CapabilityCreationOrchestrator</c>): a session's only durable state
/// is the SQL-persisted Workflow checkpoint (Paso 3) plus the domain rows
/// a later Paso will add — a fresh <see cref="Microsoft.Agents.AI.Workflows.Workflow"/>
/// graph and <see cref="StreamingRun"/> are built/resumed per HTTP call and
/// disposed immediately after, exactly like the Paso 3/8 smoke tests.
/// </summary>
internal static class RuntimeApiEngine
{
    /// <summary>Looks up the most recently written checkpoint for a
    /// session (fixed Paso 9, 2026-07-15) so an API call can
    /// <c>ResumeStreamingAsync</c> without needing any other durable
    /// session registry. Returns <see langword="null"/> when the session
    /// id is unknown (caller should respond 404).</summary>
    /// <remarks>
    /// Finds the TIP of the checkpoint chain — the row whose
    /// <c>CheckpointId</c> is never referenced as another row's
    /// <c>ParentCheckpointId</c> — rather than just the max
    /// <c>CreatedDate</c>. A single API call can trigger several
    /// supersteps in rapid succession (confirmed live: multiple
    /// checkpoint INSERTs within the same request), and .NET's
    /// <c>DateTime.UtcNow</c> resolution is coarse enough that two of
    /// those rows can share an identical timestamp — <c>ORDER BY
    /// CreatedDate DESC</c> alone is then non-deterministic about which
    /// one sorts last, and picking the wrong one silently resumes from a
    /// STALE point (observed live: a resumed run landing back near
    /// <c>ModuleStarted</c> instead of the actual current stage). The
    /// parent-chain tip is unambiguous regardless of timestamp precision.
    /// </remarks>
    public static async Task<CheckpointInfo?> GetLatestCheckpointAsync(
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        string engineSessionId,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.RuntimeWorkflowCheckpoints
            .AsNoTracking()
            .Where(x => x.SessionId == engineSessionId)
            .Select(x => new { x.CheckpointId, x.ParentCheckpointId })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var referencedAsParent = rows
            .Where(x => x.ParentCheckpointId is not null)
            .Select(x => x.ParentCheckpointId!)
            .ToHashSet();

        var tip = rows.FirstOrDefault(x => !referencedAsParent.Contains(x.CheckpointId));

        // Defensive fallback (should not happen for our strictly linear
        // graph, but never silently resume from an arbitrary row if the
        // chain shape is ever unexpected): fall back to the previous
        // max-CreatedDate behavior rather than throwing.
        if (tip is null)
        {
            var fallback = await db.RuntimeWorkflowCheckpoints
                .AsNoTracking()
                .Where(x => x.SessionId == engineSessionId)
                .OrderByDescending(x => x.CreatedDate)
                .FirstAsync(cancellationToken);

            return new CheckpointInfo(engineSessionId, fallback.CheckpointId);
        }

        return new CheckpointInfo(engineSessionId, tip.CheckpointId);
    }

    /// <summary>Records that a session reached a terminal
    /// <c>RuntimeStage</c> (fixed Paso 9, 2026-07-15) — see
    /// <see cref="Data.RuntimeSessionStatus"/>'s doc comment for the real
    /// resume-hang bug this closes. Every caller that observes
    /// <see cref="RuntimeDrainResult.Output"/> MUST call this before
    /// returning, so a LATER call never attempts to resume a run that can
    /// no longer produce any event.</summary>
    public static async Task MarkTerminalAsync(
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        string engineSessionId,
        string finalStage,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await db.RuntimeSessionStatuses.FindAsync(
            [engineSessionId], cancellationToken);

        if (existing is null)
        {
            db.RuntimeSessionStatuses.Add(new Data.RuntimeSessionStatus
            {
                SessionId = engineSessionId,
                IsTerminal = true,
                FinalStage = finalStage,
                UpdatedDate = DateTime.UtcNow
            });
        }
        else
        {
            existing.IsTerminal = true;
            existing.FinalStage = finalStage;
            existing.UpdatedDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Checks whether a session was already recorded as terminal
    /// — callers MUST check this BEFORE calling
    /// <c>InProcessExecution.ResumeStreamingAsync</c>, never after.</summary>
    public static async Task<Data.RuntimeSessionStatus?> GetTerminalStatusAsync(
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        string engineSessionId,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.RuntimeSessionStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SessionId == engineSessionId, cancellationToken);
    }

    /// <summary>
    /// Drains a <see cref="StreamingRun"/> until it either pauses at a
    /// known pending request (<see cref="EvidenceRequest"/> or
    /// <see cref="InstructionPresentation"/>) or yields its terminal
    /// <see cref="RuntimeSessionState"/> output (<c>Completed</c>/
    /// <c>RequiresRevision</c>). Every Runtime API call ends with exactly
    /// one of these two outcomes — never both, never neither.
    /// </summary>
    public static async Task<RuntimeDrainResult> DrainAsync(StreamingRun run, CancellationToken cancellationToken)
    {
        await foreach (var evt in run.WatchStreamAsync(cancellationToken))
        {
            if (evt is ExecutorFailedEvent failed)
            {
                throw new InvalidOperationException($"Executor '{failed.ExecutorId}' failed: {failed.Data}");
            }

            if (evt is WorkflowErrorEvent error)
            {
                throw new InvalidOperationException($"Workflow error: {error.Exception}");
            }

            if (evt is RequestInfoEvent requestInfo)
            {
                // NOTE (fixed 2026-07-15, real bug found via live testing):
                // ExternalRequest.TryGetDataAs<T> deserializes the stored
                // JSON payload as T via a permissive JSON deserializer —
                // it does NOT check which concrete type the payload was
                // originally created as. Missing properties are silently
                // defaulted (e.g. deserializing an InstructionPresentation
                // payload — {RuntimeSessionId, Content} — AS an
                // EvidenceRequest "succeeds" with Stage=default(RuntimeStage)
                // and Prompt="" instead of failing). Checking type order
                // alone is NOT enough — BOTH checks can "succeed" on the
                // wrong payload. Every real EvidenceRequest/InstructionPresentation
                // this Runtime ever creates always has a non-empty
                // Prompt/Content (the Tutor Agent's real phrased text), so
                // requiring that as well reliably discriminates the two.
                if (requestInfo.Request.TryGetDataAs<EvidenceRequest>(out var evidenceRequest) &&
                    !string.IsNullOrEmpty(evidenceRequest.Prompt))
                {
                    return new RuntimeDrainResult
                    {
                        PendingRequest = requestInfo.Request,
                        EvidenceRequest = evidenceRequest
                    };
                }

                if (requestInfo.Request.TryGetDataAs<InstructionPresentation>(out var instructionPresentation) &&
                    !string.IsNullOrEmpty(instructionPresentation.Content))
                {
                    return new RuntimeDrainResult
                    {
                        PendingRequest = requestInfo.Request,
                        InstructionPresentation = instructionPresentation
                    };
                }

                if (requestInfo.Request.TryGetDataAs<IntroductionPresentation>(out var introductionPresentation) &&
                    !string.IsNullOrEmpty(introductionPresentation.IntroductionText))
                {
                    return new RuntimeDrainResult
                    {
                        PendingRequest = requestInfo.Request,
                        IntroductionPresentation = introductionPresentation
                    };
                }

                // Fixed 2026-07-16: ChapterPresentation/ChapterMiniPracticePresentation
                // use uniquely-named text properties (TeachingContent/
                // MiniPracticeContent, never "Content"/"IntroductionText")
                // for the exact same permissive-deserialization reason
                // documented above.
                if (requestInfo.Request.TryGetDataAs<ChapterPresentation>(out var chapterPresentation) &&
                    !string.IsNullOrEmpty(chapterPresentation.TeachingContent))
                {
                    return new RuntimeDrainResult
                    {
                        PendingRequest = requestInfo.Request,
                        ChapterPresentation = chapterPresentation
                    };
                }

                if (requestInfo.Request.TryGetDataAs<ChapterMiniPracticePresentation>(out var chapterMiniPracticePresentation) &&
                    !string.IsNullOrEmpty(chapterMiniPracticePresentation.MiniPracticeContent))
                {
                    return new RuntimeDrainResult
                    {
                        PendingRequest = requestInfo.Request,
                        ChapterMiniPracticePresentation = chapterMiniPracticePresentation
                    };
                }

                throw new InvalidOperationException("Unrecognized pending request type paused the Runtime Workflow.");
            }

            if (evt is WorkflowOutputEvent output && output.Data is RuntimeSessionState finalState)
            {
                return new RuntimeDrainResult { Output = finalState };
            }
        }

        throw new InvalidOperationException("Workflow stream ended without pausing or yielding output.");
    }

    /// <summary>Maps a stage that pauses for evidence to the
    /// <see cref="StudentEvidenceOrigin"/> it produces — computed by the
    /// Runtime, never trusted from an API caller (see
    /// <see cref="MapStudentEvidence"/>).</summary>
    public static StudentEvidenceOrigin MapStageToOrigin(RuntimeStage stage) => stage switch
    {
        RuntimeStage.RecallRequired => StudentEvidenceOrigin.Recall,
        RuntimeStage.PredictionRequired => StudentEvidenceOrigin.Prediction,
        RuntimeStage.ChapterRecall => StudentEvidenceOrigin.Recall,
        RuntimeStage.ChapterPrediction => StudentEvidenceOrigin.Prediction,
        RuntimeStage.LearnerProduction => StudentEvidenceOrigin.Production,
        RuntimeStage.Reflection => StudentEvidenceOrigin.Reflection,
        _ => throw new InvalidOperationException($"RuntimeStage '{stage}' never produces StudentEvidence.")
    };

    /// <summary>
    /// Builds a <see cref="StudentEvidence"/> for the CURRENTLY pending
    /// <see cref="EvidenceRequest"/> (fixed Paso 9, 2026-07-15) —
    /// <see cref="StudentEvidence.Origin"/> and
    /// <see cref="StudentEvidence.CapturedBeforeAssistance"/> (for Recall/
    /// Prediction) are computed by the Runtime from the pending request's
    /// <see cref="EvidenceRequest.Stage"/>, never taken from the API
    /// caller — a client cannot mislabel its own evidence.
    /// </summary>
    public static StudentEvidence BuildEvidence(
        EvidenceRequest pending,
        List<StudentEvidencePart> parts,
        EvidenceAssistanceLevel assistanceLevel,
        bool capturedBeforeAssistanceFromCaller,
        Guid? comparesToEvidenceId)
    {
        var origin = MapStageToOrigin(pending.Stage);

        return new StudentEvidence
        {
            RuntimeSessionId = pending.RuntimeSessionId,
            CapabilityModuleId = pending.CapabilityModuleId,
            Origin = origin,
            Parts = parts,
            AssistanceLevel = assistanceLevel,
            CapturedBeforeAssistance = origin is StudentEvidenceOrigin.Recall or StudentEvidenceOrigin.Prediction
                ? true
                : capturedBeforeAssistanceFromCaller,
            ComparesToEvidenceId = comparesToEvidenceId
        };
    }
}

/// <summary>
/// Result of <see cref="RuntimeApiEngine.DrainAsync"/> — exactly one of
/// <see cref="EvidenceRequest"/>/<see cref="InstructionPresentation"/>/
/// <see cref="IntroductionPresentation"/>/<see cref="Output"/> is populated.
/// </summary>
internal sealed class RuntimeDrainResult
{
    public ExternalRequest? PendingRequest { get; init; }

    public EvidenceRequest? EvidenceRequest { get; init; }

    public InstructionPresentation? InstructionPresentation { get; init; }

    public IntroductionPresentation? IntroductionPresentation { get; init; }

    /// <summary>Fixed 2026-07-16 — the phase-based replacement for
    /// <see cref="InstructionPresentation"/> when the module has Chapters.</summary>
    public ChapterPresentation? ChapterPresentation { get; init; }

    /// <summary>Fixed 2026-07-16 — presented only for the primary-weight
    /// chapter, right after its <see cref="RuntimeStage.ChapterPrediction"/> turn.</summary>
    public ChapterMiniPracticePresentation? ChapterMiniPracticePresentation { get; init; }

    public RuntimeSessionState? Output { get; init; }
}
