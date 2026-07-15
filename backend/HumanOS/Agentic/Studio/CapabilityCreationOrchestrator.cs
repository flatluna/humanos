using System.Collections.Concurrent;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Agentic.Studio;

/// <summary>
/// Owns in-memory, in-process runs of the Human OS Studio
/// capability-creation Workflow, including the two human-in-the-loop
/// gates (see CapabilityCreationWorkflowFactory — UNCHANGED by this file:
/// GATE 1/GATE 2 still use RequestPort + RequestInfoEvent exactly as
/// documented in the Agent Framework's Human-in-the-loop guide).
///
/// NON-BLOCKING CHANGE (2026-07-13): StartAsync/RespondAsync used to
/// `await` the whole event-drain loop before returning to the HTTP
/// caller — meaning the HTTP request blocked for the ENTIRE
/// Instructor/Métrico per-module loop (or the entire Publish step),
/// which can take minutes and risks the Functions host timeout. Now the
/// drain loop runs as a background Task per run, continuously updating
/// an in-memory <see cref="CapabilityCreationRunStatus"/> snapshot (built
/// from the pipeline's own progress events — see StudioProgressEvents.cs
/// — which use IWorkflowContext.AddEventAsync and do NOT pause the
/// workflow, unlike RequestInfoEvent). StartAsync/RespondAsync now return
/// immediately with Stage.Running; the frontend polls the new
/// GetStatus(runId) (exposed via GetCapabilityCreationStatusFunction,
/// GET studio/capability-creation/{runId}/status) the same way it already
/// polls the mock APIs today.
///
/// Prototype-scoped, per user decision 2026-07-13: runs live only in this
/// process's memory — no Durable Functions, no persistence of in-flight
/// runs. See /memories/repo/humanstudio-multiagent-vision.md.
/// </summary>
public sealed class CapabilityCreationOrchestrator
{
    private readonly CuradorAgent _curador;
    private readonly ArquitectoAgent _arquitecto;
    private readonly InstructorAgent _instructor;
    private readonly MetricoAgent _metrico;
    private readonly ExperienciaAgent _experiencia;
    private readonly IDbContextFactory<HumanOsDbContext> _dbContextFactory;
    private readonly CapabilityEmbeddingService _embeddingService;
    private readonly ConcurrentDictionary<Guid, RunSession> _runs = new();

    public CapabilityCreationOrchestrator(
        CuradorAgent curador,
        ArquitectoAgent arquitecto,
        InstructorAgent instructor,
        MetricoAgent metrico,
        ExperienciaAgent experiencia,
        IDbContextFactory<HumanOsDbContext> dbContextFactory,
        CapabilityEmbeddingService embeddingService)
    {
        _curador = curador;
        _arquitecto = arquitecto;
        _instructor = instructor;
        _metrico = metrico;
        _experiencia = experiencia;
        _dbContextFactory = dbContextFactory;
        _embeddingService = embeddingService;
    }

    public bool IsConfigured =>
        _curador.IsConfigured && _arquitecto.IsConfigured && _instructor.IsConfigured &&
        _metrico.IsConfigured && _experiencia.IsConfigured && _embeddingService.IsConfigured;

    /// <summary>Starts a new run and returns IMMEDIATELY (Stage.Running) —
    /// does not wait for Curador/Arquitecto to finish. Poll
    /// <see cref="GetStatus"/> for progress and the eventual Gate 1 pause.</summary>
    public Task<CapabilityCreationRunStatus> StartAsync(
        Guid capabilityDomainId,
        string capabilityGoal,
        IReadOnlyList<RawMaterialItem> rawMaterials,
        CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid();
        var workflow = CapabilityCreationWorkflowFactory.Build(
            _curador, _arquitecto, _instructor, _metrico, _experiencia, _dbContextFactory, _embeddingService);

        var session = new RunSession();
        _runs[runId] = session;

        // Set the real runId synchronously BEFORE returning — the
        // background task below runs concurrently and may not have had a
        // chance to update LatestStatus yet by the time this method
        // returns, so without this line the caller would get back the
        // session's placeholder Guid.Empty status instead of the real runId.
        session.UpdateStatus(CapabilityCreationRunStatus.Running(runId));

        // Kick off the actual run + drain loop in the background; do NOT
        // await it here. The workflow's own async machinery starts the
        // instant RunStreamingAsync is awaited inside the background task.
        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                var run = await InProcessExecution.RunStreamingAsync(
                    workflow,
                    new RawMaterialBatch
                    {
                        CapabilityDomainId = capabilityDomainId,
                        CapabilityGoal = capabilityGoal,
                        Materials = [.. rawMaterials]
                    },
                    cancellationToken: cancellationToken);

                session.Run = run;
                await DrainInBackgroundAsync(runId, session, CancellationToken.None);
            }
            catch (Exception ex)
            {
                session.UpdateStatus(CapabilityCreationRunStatus.Failed(runId, ex.Message));
            }
        }, CancellationToken.None);

        return Task.FromResult(session.LatestStatus);
    }

    /// <summary>Submits a human reviewer's gate decision and returns
    /// IMMEDIATELY (Stage.Running) — does not wait for the next phase
    /// (module loop, Experiencia, or Publish) to finish. Poll
    /// <see cref="GetStatus"/> for progress and the eventual next gate or
    /// completion.</summary>
    public Task<CapabilityCreationRunStatus> RespondAsync(
        Guid runId,
        Guid subjectId,
        bool approved,
        string? comments,
        CapabilityBlueprint? revisedBlueprint,
        CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(runId, out var session))
        {
            throw new InvalidOperationException($"No pending capability-creation run found with id '{runId}'.");
        }

        if (!session.PendingRequests.TryRemove(subjectId, out var pendingRequestEvent))
        {
            throw new InvalidOperationException(
                $"No pending gate request with subject id '{subjectId}' for run '{runId}'.");
        }

        var decision = new GateDecision
        {
            SubjectId = subjectId,
            Approved = approved,
            Comments = comments,
            RevisedBlueprint = revisedBlueprint
        };

        session.UpdateStatus(CapabilityCreationRunStatus.Running(runId, session.CurrentProgress()));

        session.BackgroundTask = Task.Run(async () =>
        {
            try
            {
                await session.Run!.SendResponseAsync(pendingRequestEvent.Request.CreateResponse(decision));
                await DrainInBackgroundAsync(runId, session, CancellationToken.None);
            }
            catch (Exception ex)
            {
                session.UpdateStatus(CapabilityCreationRunStatus.Failed(runId, ex.Message));
            }
        }, CancellationToken.None);

        return Task.FromResult(session.LatestStatus);
    }

    /// <summary>Polled by the frontend (GET .../status) to read the
    /// current state of a run: live progress while Running, the pending
    /// gate payload, or the final Completed/Failed result. Never blocks.</summary>
    public CapabilityCreationRunStatus GetStatus(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var session))
        {
            throw new InvalidOperationException($"No capability-creation run found with id '{runId}'.");
        }

        return session.LatestStatus;
    }

    private static async Task DrainInBackgroundAsync(
        Guid runId,
        RunSession session,
        CancellationToken cancellationToken)
    {
        await foreach (var evt in session.Run!.WatchStreamAsync().WithCancellation(cancellationToken))
        {
            if (evt is ExecutorFailedEvent executorFailed)
            {
                session.UpdateStatus(CapabilityCreationRunStatus.Failed(
                    runId, $"Executor '{executorFailed.ExecutorId}' failed: {executorFailed.Data}"));
                return;
            }

            if (evt is WorkflowErrorEvent workflowError)
            {
                session.UpdateStatus(CapabilityCreationRunStatus.Failed(runId, $"Workflow error: {workflowError.Exception}"));
                return;
            }

            if (evt is ModuleQueueStartedEvent queueStarted)
            {
                session.SetTotalModules(queueStarted.TotalModules);
                session.UpdateStatus(CapabilityCreationRunStatus.Running(runId, session.CurrentProgress()));
                continue;
            }

            if (evt is ModuleScriptStartedEvent scriptStarted)
            {
                session.SetCurrentModule(scriptStarted.ModuleTitle);
                session.UpdateStatus(CapabilityCreationRunStatus.Running(runId, session.CurrentProgress()));
                continue;
            }

            if (evt is ModuleVerifiedEvent or ModuleRequiresRevisionEvent or ModuleProcessingFailedEvent or ModuleRetryingEvent)
            {
                // Paso 5 (2026-07-14) / Paso 7 (2026-07-14): these are
                // per-ATTEMPT outcome events, not per-MODULE ones — a
                // module that gets retried (see ModuleRetryingEvent)
                // raises one of Verified/RequiresRevision/Failed on EVERY
                // attempt, including ones that are about to be retried.
                // Progress only advances once per module, via the
                // separate ModuleFinalizedEvent below — do NOT increment
                // here, or a retried module would be double/triple-counted
                // (and CompletedModules could exceed TotalModules).
                session.UpdateStatus(CapabilityCreationRunStatus.Running(runId, session.CurrentProgress()));
                continue;
            }

            if (evt is ModuleFinalizedEvent)
            {
                session.IncrementCompletedModules();
                session.UpdateStatus(CapabilityCreationRunStatus.Running(runId, session.CurrentProgress()));
                continue;
            }

            if (evt is PublishTaskProgressEvent publishTask)
            {
                session.SetPublishTaskStatus(publishTask.TaskKey, publishTask.Status);
                session.UpdateStatus(CapabilityCreationRunStatus.Running(runId, session.CurrentProgress()));
                continue;
            }

            if (evt is RequestInfoEvent requestInfo)
            {
                var (subjectId, payload) = UnwrapRequestPayload(requestInfo.Request);
                session.PendingRequests[subjectId] = requestInfo;
                session.UpdateStatus(CapabilityCreationRunStatus.PendingGate(runId, subjectId, payload));
                return;
            }

            if (evt is WorkflowOutputEvent output)
            {
                session.UpdateStatus(CapabilityCreationRunStatus.Completed(runId, output.Data));
                return;
            }
        }

        session.UpdateStatus(CapabilityCreationRunStatus.Idle(runId));
    }

    /// <summary>
    /// Unwraps an <see cref="ExternalRequest"/>'s <c>PortableValue</c> data
    /// into the concrete gate payload type (either a
    /// <see cref="CapabilityBlueprint"/> for Gate 1 or a
    /// <see cref="CapabilityPackage"/> for Gate 2), plus the correlation id
    /// used to route the human reviewer's response back to the right run.
    /// </summary>
    private static (Guid SubjectId, object Payload) UnwrapRequestPayload(ExternalRequest request)
    {
        if (request.TryGetDataAs<CapabilityBlueprint>(out var blueprint))
        {
            return (blueprint.BlueprintId, blueprint);
        }

        if (request.TryGetDataAs<CapabilityPackage>(out var package))
        {
            return (package.PackageId, package);
        }

        throw new InvalidOperationException(
            "Unrecognized gate request payload for port '" + request.PortInfo + "'.");
    }

    private sealed class RunSession
    {
        private readonly Lock _lock = new();
        private int? _totalModules;
        private int _completedModules;
        private string? _currentModuleTitle;
        private readonly Dictionary<string, string> _publishTaskStatuses = new();

        public StreamingRun? Run { get; set; }

        public Task? BackgroundTask { get; set; }

        public ConcurrentDictionary<Guid, RequestInfoEvent> PendingRequests { get; } = new();

        // Reference reassignment is atomic in .NET; CapabilityCreationRunStatus
        // is effectively immutable (init-only properties), so readers never
        // observe a partially-updated snapshot without needing a lock here.
        public volatile CapabilityCreationRunStatus LatestStatus =
            CapabilityCreationRunStatus.Running(Guid.Empty);

        public void UpdateStatus(CapabilityCreationRunStatus status) => LatestStatus = status;

        public void SetTotalModules(int total)
        {
            lock (_lock)
            {
                _totalModules = total;
            }
        }

        public void SetCurrentModule(string title)
        {
            lock (_lock)
            {
                _currentModuleTitle = title;
            }
        }

        public void IncrementCompletedModules()
        {
            lock (_lock)
            {
                _completedModules++;
            }
        }

        public void SetPublishTaskStatus(string taskKey, string status)
        {
            lock (_lock)
            {
                _publishTaskStatuses[taskKey] = status;
            }
        }

        public CapabilityCreationRunProgress CurrentProgress()
        {
            lock (_lock)
            {
                return new CapabilityCreationRunProgress
                {
                    TotalModules = _totalModules,
                    CompletedModules = _totalModules is null ? null : _completedModules,
                    CurrentModuleTitle = _currentModuleTitle,
                    PublishTasks = _publishTaskStatuses.Count == 0
                        ? null
                        : [.. _publishTaskStatuses.Select(kv => new PublishTaskStatus { TaskKey = kv.Key, Status = kv.Value })]
                };
            }
        }
    }
}

