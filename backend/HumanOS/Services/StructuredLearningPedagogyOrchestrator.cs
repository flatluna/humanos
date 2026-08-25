using System.Collections.Concurrent;

namespace HumanOS.Services;

/// <summary>
/// Owns in-memory, in-process runs of Step 3 (pedagogy generation) — same
/// non-blocking start/poll pattern as
/// <see cref="StructuredLearningExtractionOrchestrator"/>, kept as its OWN
/// separate run store/slot so extraction and pedagogy runs never contend
/// with each other.
/// </summary>
public sealed class StructuredLearningPedagogyOrchestrator
{
    private readonly StructuredLearningPedagogyService _pedagogy;
    private readonly ConcurrentDictionary<Guid, RunSession> _runs = new();

    private readonly object _activeRunLock = new();
    private Guid? _activeRunId;

    public StructuredLearningPedagogyOrchestrator(StructuredLearningPedagogyService pedagogy)
    {
        _pedagogy = pedagogy;
    }

    public bool IsConfigured => _pedagogy.IsConfigured;

    public sealed class ActiveRunConflictException(Guid activeRunId) : Exception(
        $"Ya hay una generación de pedagogía en curso (RunId: {activeRunId}). Espera a que termine antes de iniciar otra.")
    {
        public Guid ActiveRunId { get; } = activeRunId;
    }

    private void ClaimActiveRunSlotOrThrow(Guid runId)
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid existingRunId &&
                _runs.TryGetValue(existingRunId, out var existingSession) &&
                existingSession.LatestStatus.Stage == StructuredLearningPedagogyStage.Running)
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

    public StructuredLearningPedagogyRunStatus Start(
        Guid documentId,
        string languageCode,
        bool forceRegenerateAll = false,
        byte[]? uploadedImageBytes = null,
        string? uploadedImageContentType = null,
        string? referenceImageUsageInstruction = null)
    {
        var runId = Guid.NewGuid();
        ClaimActiveRunSlotOrThrow(runId);

        var session = new RunSession();
        _runs[runId] = session;
        session.UpdateStatus(StructuredLearningPedagogyRunStatus.Running(runId, "Starting", null, null, null));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                var result = await _pedagogy.RunAsync(
                    documentId,
                    languageCode,
                    (step, completed, total, activeTitles) =>
                        session.UpdateStatus(StructuredLearningPedagogyRunStatus.Running(runId, step, total, completed, activeTitles)),
                    CancellationToken.None,
                    forceRegenerateAll,
                    uploadedImageBytes,
                    uploadedImageContentType,
                    referenceImageUsageInstruction);

                session.UpdateStatus(StructuredLearningPedagogyRunStatus.Completed(runId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(StructuredLearningPedagogyRunStatus.Failed(runId, ex.Message));
            }
            finally
            {
                ReleaseActiveRunSlot(runId);
            }
        }, CancellationToken.None);

        return session.LatestStatus;
    }

    public StructuredLearningPedagogyRunStatus GetStatus(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var session))
        {
            throw new InvalidOperationException($"No structured-learning-pedagogy run found with id '{runId}'.");
        }

        return session.LatestStatus;
    }

    private sealed class RunSession
    {
        private readonly object _lock = new();
        private StructuredLearningPedagogyRunStatus _latestStatus = null!;

        public Task? BackgroundTask { get; set; }

        public StructuredLearningPedagogyRunStatus LatestStatus
        {
            get { lock (_lock) { return _latestStatus; } }
        }

        public void UpdateStatus(StructuredLearningPedagogyRunStatus status)
        {
            lock (_lock) { _latestStatus = status; }
        }
    }
}
