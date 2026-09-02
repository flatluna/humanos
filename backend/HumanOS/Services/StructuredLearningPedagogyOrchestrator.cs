using System.Collections.Concurrent;
using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

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
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly ConcurrentDictionary<Guid, RunSession> _runs = new();

    private readonly object _activeRunLock = new();
    private Guid? _activeRunId;

    public StructuredLearningPedagogyOrchestrator(StructuredLearningPedagogyService pedagogy, IDbContextFactory<HumanOsDbContext> dbContextFactory)
    {
        _pedagogy = pedagogy;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>Returns the currently in-progress run, if any (2026-08-30) —
    /// same recovery pattern as TraditionalCoursePipelineOrchestrator.GetActiveRun,
    /// used by the global "a course is generating" banner so a designer who
    /// navigated away (or reloaded/lost the in-memory RunId) can still find
    /// their way back to it.</summary>
    public StructuredLearningPedagogyRunStatus? GetActiveRun()
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid runId &&
                _runs.TryGetValue(runId, out var session) &&
                session.LatestStatus.Stage == StructuredLearningPedagogyStage.Running)
            {
                return session.LatestStatus;
            }

            return null;
        }
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
        session.UpdateStatus(StructuredLearningPedagogyRunStatus.Running(runId, documentId, null, "Starting", null, null, null));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                string? documentName = null;
                try
                {
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(CancellationToken.None);
                    documentName = await dbContext.StructuredLearningDocuments
                        .Where(d => d.StructuredLearningDocumentId == documentId)
                        .Select(d => d.DocumentName)
                        .FirstOrDefaultAsync(CancellationToken.None);
                }
                catch
                {
                    // Best-effort only — the banner still works without a name.
                }

                var result = await _pedagogy.RunAsync(
                    documentId,
                    languageCode,
                    (step, completed, total, activeTitles) =>
                        session.UpdateStatus(StructuredLearningPedagogyRunStatus.Running(runId, documentId, documentName, step, total, completed, activeTitles)),
                    CancellationToken.None,
                    forceRegenerateAll,
                    uploadedImageBytes,
                    uploadedImageContentType,
                    referenceImageUsageInstruction);

                session.UpdateStatus(StructuredLearningPedagogyRunStatus.Completed(runId, documentId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(StructuredLearningPedagogyRunStatus.Failed(runId, documentId, ex.Message));
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
