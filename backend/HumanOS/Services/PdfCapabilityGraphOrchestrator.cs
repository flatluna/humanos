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

    public PdfCapabilityGraphOrchestrator(PdfCapabilityGraphPipelineService pipeline)
    {
        _pipeline = pipeline;
    }

    public bool IsConfigured => _pipeline.IsConfigured;

    /// <summary>Starts a new run and returns IMMEDIATELY (Stage.Running) —
    /// does not wait for the pipeline to finish. Poll <see cref="GetStatus"/>
    /// for progress and the eventual Completed/Failed result.</summary>
    public PdfCapabilityGraphRunStatus Start(
        byte[] pdfBytes,
        string fileName,
        Guid capabilityDomainId,
        string capabilityName,
        Guid tenantId,
        bool enableWebEnrichment = false)
    {
        var runId = Guid.NewGuid();
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
                    enableWebEnrichment);

                session.UpdateStatus(PdfCapabilityGraphRunStatus.Completed(runId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(PdfCapabilityGraphRunStatus.Failed(runId, ex.Message));
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
