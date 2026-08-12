using System.Collections.Concurrent;
using HumanOS.Services;

namespace HumanOS.Services;

/// <summary>
/// Owns in-memory, in-process runs of the Structured Learning Document
/// extraction step (Step 2) — same non-blocking start/poll pattern as
/// <see cref="TraditionalCoursePipelineOrchestrator"/>/
/// <see cref="PdfCapabilityGraphOrchestrator"/>, kept as its OWN separate
/// run store/slot so it never contends with either existing pipeline.
/// </summary>
public sealed class StructuredLearningExtractionOrchestrator
{
    private readonly StructuredLearningExtractionService _extraction;
    private readonly ConcurrentDictionary<Guid, RunSession> _runs = new();

    private readonly object _activeRunLock = new();
    private Guid? _activeRunId;

    public StructuredLearningExtractionOrchestrator(StructuredLearningExtractionService extraction)
    {
        _extraction = extraction;
    }

    public bool IsConfigured => _extraction.IsConfigured;

    public sealed class ActiveRunConflictException(Guid activeRunId) : Exception(
        $"Ya hay una extracción de Structured Learning Document en curso (RunId: {activeRunId}). Espera a que termine antes de iniciar otra.")
    {
        public Guid ActiveRunId { get; } = activeRunId;
    }

    private void ClaimActiveRunSlotOrThrow(Guid runId)
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid existingRunId &&
                _runs.TryGetValue(existingRunId, out var existingSession) &&
                existingSession.LatestStatus.Stage == StructuredLearningExtractionStage.Running)
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

    public StructuredLearningExtractionRunStatus Start(
        byte[] pdfBytes,
        string fileName,
        Guid tenantId,
        List<CapabilityOutlineChapterDto> approvedOutline)
    {
        var runId = Guid.NewGuid();
        ClaimActiveRunSlotOrThrow(runId);

        var session = new RunSession();
        _runs[runId] = session;
        session.UpdateStatus(StructuredLearningExtractionRunStatus.Running(runId, "Iniciando"));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                var result = await _extraction.RunFromPdfAsync(
                    pdfBytes,
                    fileName,
                    tenantId,
                    runId,
                    approvedOutline,
                    step => session.UpdateStatus(StructuredLearningExtractionRunStatus.Running(runId, step)),
                    CancellationToken.None);

                session.UpdateStatus(StructuredLearningExtractionRunStatus.Completed(runId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(StructuredLearningExtractionRunStatus.Failed(runId, ex.Message));
            }
            finally
            {
                ReleaseActiveRunSlot(runId);
            }
        }, CancellationToken.None);

        return session.LatestStatus;
    }

    public StructuredLearningExtractionRunStatus StartFromDescription(
        string description,
        string documentName,
        Guid tenantId,
        List<CapabilityOutlineChapterDto> approvedOutline,
        string? preExpandedDocumentText,
        string languageCode,
        bool strictSyllabusMode)
    {
        var runId = Guid.NewGuid();
        ClaimActiveRunSlotOrThrow(runId);

        var session = new RunSession();
        _runs[runId] = session;
        session.UpdateStatus(StructuredLearningExtractionRunStatus.Running(runId, "Iniciando"));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                var result = await _extraction.RunFromDescriptionAsync(
                    description,
                    documentName,
                    tenantId,
                    runId,
                    approvedOutline,
                    preExpandedDocumentText,
                    languageCode,
                    strictSyllabusMode,
                    step => session.UpdateStatus(StructuredLearningExtractionRunStatus.Running(runId, step)),
                    CancellationToken.None);

                session.UpdateStatus(StructuredLearningExtractionRunStatus.Completed(runId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(StructuredLearningExtractionRunStatus.Failed(runId, ex.Message));
            }
            finally
            {
                ReleaseActiveRunSlot(runId);
            }
        }, CancellationToken.None);

        return session.LatestStatus;
    }

    public StructuredLearningExtractionRunStatus GetStatus(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var session))
        {
            throw new InvalidOperationException($"No structured-learning-extraction run found with id '{runId}'.");
        }

        return session.LatestStatus;
    }

    private sealed class RunSession
    {
        private readonly object _lock = new();
        private StructuredLearningExtractionRunStatus _latestStatus = null!;

        public Task? BackgroundTask { get; set; }

        public StructuredLearningExtractionRunStatus LatestStatus
        {
            get { lock (_lock) { return _latestStatus; } }
        }

        public void UpdateStatus(StructuredLearningExtractionRunStatus status)
        {
            lock (_lock) { _latestStatus = status; }
        }
    }
}
