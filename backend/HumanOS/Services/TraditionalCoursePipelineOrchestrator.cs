using System.Collections.Concurrent;
using HumanOS.Agents.Studio;

namespace HumanOS.Services;

/// <summary>
/// Owns in-memory, in-process runs of the Traditional Course pipeline
/// (<see cref="TraditionalCoursePipelineService"/>) — same non-blocking
/// start/poll/"only one run at a time" pattern as
/// <see cref="PdfCapabilityGraphOrchestrator"/>, kept as a SEPARATE run
/// store/slot so a Traditional-course run and a Graph-course run can
/// proceed independently without contending for the same "only one at a
/// time" slot.
/// </summary>
public sealed class TraditionalCoursePipelineOrchestrator
{
    private readonly TraditionalCoursePipelineService _pipeline;
    private readonly ConcurrentDictionary<Guid, RunSession> _runs = new();

    private readonly object _activeRunLock = new();
    private Guid? _activeRunId;

    public TraditionalCoursePipelineOrchestrator(TraditionalCoursePipelineService pipeline)
    {
        _pipeline = pipeline;
    }

    public bool IsConfigured => _pipeline.IsConfigured;

    public sealed class ActiveRunConflictException(Guid activeRunId) : Exception(
        $"Ya hay un curso tradicional generándose (RunId: {activeRunId}). Espera a que termine antes de iniciar otro.")
    {
        public Guid ActiveRunId { get; } = activeRunId;
    }

    private void ClaimActiveRunSlotOrThrow(Guid runId)
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid existingRunId &&
                _runs.TryGetValue(existingRunId, out var existingSession) &&
                existingSession.LatestStatus.Stage == TraditionalCourseStage.Running)
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

    public TraditionalCourseRunStatus? GetActiveRun()
    {
        lock (_activeRunLock)
        {
            if (_activeRunId is Guid runId &&
                _runs.TryGetValue(runId, out var session) &&
                session.LatestStatus.Stage == TraditionalCourseStage.Running)
            {
                return session.LatestStatus;
            }

            return null;
        }
    }

    public TraditionalCourseRunStatus Start(
        byte[] pdfBytes,
        string fileName,
        Guid capabilityDomainId,
        string capabilityName,
        Guid tenantId,
        bool enableWebEnrichment = false,
        Guid? subjectId = null,
        string? capabilityDescription = null,
        string languageCode = "es",
        string? pedagogicalAudience = null,
        int questionsPerChapterExam = 5,
        bool strictSyllabusMode = false,
        List<CapabilityOutlineChapterDto>? approvedOutline = null,
        byte[]? brandLogoBytes = null,
        byte[]? uploadedReferenceImageBytes = null,
        string? referenceImageUsageInstruction = null,
        Guid? ownerPersonId = null,
        Guid? ownerTenantId = null,
        DateTime? consentAcceptedDate = null,
        Guid? consentAcceptedByPersonId = null,
        string? consentAcceptedByName = null)
    {
        var runId = Guid.NewGuid();
        ClaimActiveRunSlotOrThrow(runId);

        var session = new RunSession();
        _runs[runId] = session;
        session.UpdateStatus(TraditionalCourseRunStatus.Running(runId, "Iniciando"));

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
                    step => session.UpdateStatus(TraditionalCourseRunStatus.Running(runId, step)),
                    CancellationToken.None,
                    enableWebEnrichment,
                    subjectId,
                    capabilityDescription,
                    languageCode,
                    pedagogicalAudience,
                    questionsPerChapterExam,
                    strictSyllabusMode,
                    approvedOutline,
                    brandLogoBytes,
                    uploadedReferenceImageBytes,
                    referenceImageUsageInstruction,
                    ownerPersonId,
                    ownerTenantId,
                    consentAcceptedDate,
                    consentAcceptedByPersonId,
                    consentAcceptedByName);

                session.UpdateStatus(TraditionalCourseRunStatus.Completed(runId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(TraditionalCourseRunStatus.Failed(runId, ex.Message));
            }
            finally
            {
                ReleaseActiveRunSlot(runId);
            }
        }, CancellationToken.None);

        return session.LatestStatus;
    }

    public TraditionalCourseRunStatus StartFromDescription(
        string description,
        string capabilityName,
        Guid capabilityDomainId,
        Guid tenantId,
        bool enableWebEnrichment = false,
        Guid? subjectId = null,
        string languageCode = "es",
        string? pedagogicalAudience = null,
        int questionsPerChapterExam = 5,
        bool strictSyllabusMode = false,
        List<CapabilityOutlineChapterDto>? approvedOutline = null,
        string? preExpandedDocumentText = null,
        byte[]? brandLogoBytes = null,
        byte[]? uploadedReferenceImageBytes = null,
        string? referenceImageUsageInstruction = null,
        Guid? ownerPersonId = null,
        Guid? ownerTenantId = null,
        DateTime? consentAcceptedDate = null,
        Guid? consentAcceptedByPersonId = null,
        string? consentAcceptedByName = null)
    {
        var runId = Guid.NewGuid();
        ClaimActiveRunSlotOrThrow(runId);

        var session = new RunSession();
        _runs[runId] = session;
        session.UpdateStatus(TraditionalCourseRunStatus.Running(runId, "Iniciando"));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                var result = await _pipeline.RunFromDescriptionAsync(
                    description,
                    capabilityName,
                    capabilityDomainId,
                    tenantId,
                    step => session.UpdateStatus(TraditionalCourseRunStatus.Running(runId, step)),
                    CancellationToken.None,
                    enableWebEnrichment,
                    subjectId,
                    languageCode,
                    pedagogicalAudience,
                    questionsPerChapterExam,
                    strictSyllabusMode,
                    approvedOutline,
                    preExpandedDocumentText,
                    brandLogoBytes,
                    uploadedReferenceImageBytes,
                    referenceImageUsageInstruction,
                    ownerPersonId,
                    ownerTenantId,
                    consentAcceptedDate,
                    consentAcceptedByPersonId,
                    consentAcceptedByName);

                session.UpdateStatus(TraditionalCourseRunStatus.Completed(runId, result));
            }
            catch (Exception ex)
            {
                session.UpdateStatus(TraditionalCourseRunStatus.Failed(runId, ex.Message));
            }
            finally
            {
                ReleaseActiveRunSlot(runId);
            }
        }, CancellationToken.None);

        return session.LatestStatus;
    }

    public TraditionalCourseRunStatus GetStatus(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var session))
        {
            throw new InvalidOperationException($"No traditional-course run found with id '{runId}'.");
        }

        return session.LatestStatus;
    }

    private sealed class RunSession
    {
        private readonly object _lock = new();
        private TraditionalCourseRunStatus _latestStatus = null!;

        public Task? BackgroundTask { get; set; }

        public TraditionalCourseRunStatus LatestStatus
        {
            get { lock (_lock) { return _latestStatus; } }
        }

        public void UpdateStatus(TraditionalCourseRunStatus status)
        {
            lock (_lock) { _latestStatus = status; }
        }
    }
}
