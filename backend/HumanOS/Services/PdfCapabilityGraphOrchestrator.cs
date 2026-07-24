using System.Collections.Concurrent;

namespace HumanOS.Services;

/// <summary>
/// Owns in-memory, in-process runs of the V2 PDF→CapabilityGraph pipeline
/// (<see cref="PdfCapabilityGraphPipelineService"/>). Same non-blocking
/// start/poll pattern as HumanOS.Agentic.Studio.CapabilityCreationOrchestrator
/// (V1) — but simpler: this pipeline is a straight-line sequence with no
/// human-in-the-loop gates, so there is no RequestPort/GateDecision
/// machinery here, just "start in the background, poll GetStatus".
///
/// Prototype-scoped, same as V1: runs live only in this process's memory —
/// no Durable Functions, no persistence of in-flight runs. See
/// /memories/repo/humanstudio-multiagent-vision.md.
/// </summary>
public sealed class PdfCapabilityGraphOrchestrator
{
    private readonly PdfCapabilityGraphPipelineService _pipeline;
    private readonly ConcurrentDictionary<Guid, RunSession> _runs = new();

    // Enforces "only one capability generation at a time" (2026-07-21, per
    // explicit product decision — this pipeline is expensive/slow enough,
    // and this is a single-tenant prototype, that running two at once adds
    // confusion without real benefit). Guards a single "claimed" slot: a
    // new Start()/StartFromDescription() call is rejected with
    // <see cref="ActiveRunConflictException"/> while another run's Stage is
    // still Running. Released as soon as that run reaches Completed/Failed.
    private readonly object _activeRunLock = new();
    private Guid? _activeRunId;

    public PdfCapabilityGraphOrchestrator(PdfCapabilityGraphPipelineService pipeline)
    {
        _pipeline = pipeline;
    }

    public bool IsConfigured => _pipeline.IsConfigured;

    /// <summary>Thrown by <see cref="Start"/>/<see cref="StartFromDescription"/>
    /// when another run is still <see cref="PdfCapabilityGraphStage.Running"/> —
    /// this prototype allows only one capability generation at a time.</summary>
    public sealed class ActiveRunConflictException(Guid activeRunId) : Exception(
        $"Ya hay una capability generándose (RunId: {activeRunId}). Espera a que termine antes de iniciar otra.")
    {
        public Guid ActiveRunId { get; } = activeRunId;
    }

    /// <summary>Claims the single "in progress" slot for <paramref name="runId"/>,
    /// or throws <see cref="ActiveRunConflictException"/> if another run is
    /// still genuinely Running (self-healing: a stale claim left behind by
    /// a run that already finished is treated as free).</summary>
    private void ClaimActiveRunSlotOrThrow(Guid runId)
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid existingRunId &&
                _runs.TryGetValue(existingRunId, out var existingSession) &&
                existingSession.LatestStatus.Stage == PdfCapabilityGraphStage.Running)
            {
                throw new ActiveRunConflictException(existingRunId);
            }

            _activeRunId = runId;
        }
    }

    private void ReleaseActiveRunSlot(Guid runId)
    {
        lock (_activeRunLock)
        {
            if (_activeRunId == runId)
            {
                _activeRunId = null;
            }
        }
    }

    /// <summary>The currently in-progress run, if any — lets the frontend
    /// recover/display live progress even after navigating away or
    /// reloading, without needing the RunId in the URL (see
    /// GetActiveCapabilityGraphRunFunction). Returns null when no run is
    /// currently Running.</summary>
    public PdfCapabilityGraphRunStatus? GetActiveRun()
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid runId &&
                _runs.TryGetValue(runId, out var session) &&
                session.LatestStatus.Stage == PdfCapabilityGraphStage.Running)
            {
                return session.LatestStatus;
            }

            return null;
        }
    }

    /// <summary>Starts a new run and returns IMMEDIATELY (Stage.Running) —
    /// does not wait for the pipeline to finish. Poll <see cref="GetStatus"/>
    /// for progress and the eventual Completed/Failed result. Throws
    /// <see cref="ActiveRunConflictException"/> if another run is already
    /// in progress (only one at a time is allowed).</summary>
    public PdfCapabilityGraphRunStatus Start(
        byte[] pdfBytes,
        string fileName,
        Guid capabilityDomainId,
        string capabilityName,
        Guid tenantId,
        bool enableWebEnrichment = false,
        Guid? subjectId = null,
        Guid? programId = null,
        int? programSequenceNumber = null,
        string? capabilityObjectives = null,
        string? capabilityRequirements = null)
    {
        var runId = Guid.NewGuid();
        ClaimActiveRunSlotOrThrow(runId);

        var session = new RunSession();
        _runs[runId] = session;

        // Set the real runId synchronously BEFORE returning — the
        // background task below runs concurrently and may not have had a
        // chance to update LatestStatus yet by the time this method
        // returns.
        session.UpdateStatus(PdfCapabilityGraphRunStatus.Running(runId, "Iniciando"));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                var result = await _pipeline.RunAsync(
                    pdfBytes,
                    fileName,
                    capabilityDomainId,
                    capabilityName,
                    tenantId,
                    step => session.UpdateStatus(PdfCapabilityGraphRunStatus.Running(runId, step)),
                    CancellationToken.None,
                    enableWebEnrichment,
                    subjectId,
                    programId,
                    programSequenceNumber,
                    capabilityObjectives,
                    capabilityRequirements);

                session.UpdateStatus(PdfCapabilityGraphRunStatus.Completed(runId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(PdfCapabilityGraphRunStatus.Failed(runId, ex.Message));
            }
            finally
            {
                ReleaseActiveRunSlot(runId);
            }
        }, CancellationToken.None);

        return session.LatestStatus;
    }

    /// <summary>Polled by the frontend (GET .../status) to read the
    /// current state of a run. Never blocks.</summary>
    public PdfCapabilityGraphRunStatus GetStatus(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var session))
        {
            throw new InvalidOperationException($"No PDF-capability-graph run found with id '{runId}'.");
        }

        return session.LatestStatus;
    }

    /// <summary>Same start/poll pattern as <see cref="Start"/>, but for the
    /// "Texto/idea" entry point (2026-07-21): the user provides only a
    /// short description instead of a PDF — see
    /// <see cref="PdfCapabilityGraphPipelineService.RunFromDescriptionAsync"/>
    /// for how that description is expanded into source material before
    /// flowing through the same pipeline. Uses the SAME run store as
    /// <see cref="Start"/>, so <see cref="GetStatus"/> polls both kinds of
    /// runs identically. Also shares the same "only one run at a time"
    /// slot — throws <see cref="ActiveRunConflictException"/> if another
    /// run (PDF- or description-based) is already in progress.</summary>
    public PdfCapabilityGraphRunStatus StartFromDescription(
        string description,
        string capabilityName,
        Guid capabilityDomainId,
        Guid tenantId,
        bool enableWebEnrichment = false,
        Guid? subjectId = null,
        Guid? programId = null,
        int? programSequenceNumber = null,
        string? capabilityObjectives = null,
        string? capabilityRequirements = null)
    {
        var runId = Guid.NewGuid();
        ClaimActiveRunSlotOrThrow(runId);

        var session = new RunSession();
        _runs[runId] = session;

        session.UpdateStatus(PdfCapabilityGraphRunStatus.Running(runId, "Iniciando"));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                var result = await _pipeline.RunFromDescriptionAsync(
                    description,
                    capabilityName,
                    capabilityDomainId,
                    tenantId,
                    step => session.UpdateStatus(PdfCapabilityGraphRunStatus.Running(runId, step)),
                    CancellationToken.None,
                    enableWebEnrichment,
                    subjectId,
                    programId,
                    programSequenceNumber,
                    capabilityObjectives,
                    capabilityRequirements);

                session.UpdateStatus(PdfCapabilityGraphRunStatus.Completed(runId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(PdfCapabilityGraphRunStatus.Failed(runId, ex.Message));
            }
            finally
            {
                ReleaseActiveRunSlot(runId);
            }
        }, CancellationToken.None);

        return session.LatestStatus;
    }

    private sealed class RunSession
    {
        private readonly object _lock = new();
        private PdfCapabilityGraphRunStatus _latestStatus = null!;

        public Task? BackgroundTask { get; set; }

        public PdfCapabilityGraphRunStatus LatestStatus
        {
            get { lock (_lock) { return _latestStatus; } }
        }

        public void UpdateStatus(PdfCapabilityGraphRunStatus status)
        {
            lock (_lock) { _latestStatus = status; }
        }
    }
}
