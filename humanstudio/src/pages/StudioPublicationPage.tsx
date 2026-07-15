import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import StudioHeader from '../components/studio/StudioHeader';
import StudioStepIndicator from '../components/studio/StudioStepIndicator';
import PublicationHeader from '../components/studio/publication/PublicationHeader';
import PublicationProgress from '../components/studio/publication/PublicationProgress';
import PublicationTaskList from '../components/studio/publication/PublicationTaskList';
import PublishingNotice from '../components/studio/publication/PublishingNotice';
import SuccessIndicator from '../components/studio/publication/SuccessIndicator';
import PublishedCapabilitySummary from '../components/studio/publication/PublishedCapabilitySummary';
import PublishedActions from '../components/studio/publication/PublishedActions';
import PublicationErrorState from '../components/studio/publication/PublicationErrorState';
import { getCapabilityCreationStatus, BackendCapabilityPackage } from '../lib/api/studioApi';
import { getStudioRun, updateStudioRun, clearStudioRun } from '../lib/studioRunStore';
import { PublicationTask, PublicationTaskKey } from '../types';
import { Home } from 'lucide-react';

const POLL_INTERVAL_MS = 1500;

const TASK_ORDER: { key: PublicationTaskKey; label: string }[] = [
  { key: 'Capability', label: 'Capability' },
  { key: 'Levels', label: 'Niveles' },
  { key: 'Modules', label: 'Módulos' },
  { key: 'Metrics', label: 'Métricas' },
  { key: 'KnowledgeChunks', label: 'Base de conocimiento' },
  { key: 'Embeddings', label: 'Embeddings' },
];

export function StudioPublicationPage() {
  const { runId } = useParams<{ runId: string }>();
  const navigate = useNavigate();

  const [tasks, setTasks] = useState<PublicationTask[]>(
    TASK_ORDER.map((t) => ({ ...t, status: 'Pending' }))
  );
  const [stage, setStage] = useState<'Running' | 'Completed' | 'Failed'>('Running');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [publishedResult, setPublishedResult] = useState<BackendCapabilityPackage | null>(null);
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);

  if (!runId) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-red-600 font-semibold">Error: runId no encontrado</p>
      </div>
    );
  }

  const stopPolling = () => {
    if (pollingRef.current) {
      clearInterval(pollingRef.current);
      pollingRef.current = null;
    }
  };

  const fetchStatus = async () => {
    try {
      const status = await getCapabilityCreationStatus(runId);

      if (status.Progress?.PublishTasks) {
        const statusByKey = new Map(status.Progress.PublishTasks.map((t) => [t.TaskKey, t.Status]));
        setTasks(
          TASK_ORDER.map((t) => ({
            ...t,
            status: (statusByKey.get(t.key) as PublicationTask['status']) ?? 'Pending',
          }))
        );
      }

      if (status.Stage === 'Completed' && status.Payload && typeof status.Payload === 'object') {
        const result = status.Payload as BackendCapabilityPackage;
        setPublishedResult(result);
        setStage('Completed');
        stopPolling();
        // Capability Library reads directly from the real backend (GET /capabilities)
        // — the just-published capability is already there, no client-side
        // registration needed.
        return;
      }

      if (status.Stage === 'Failed') {
        setErrorMessage(status.ErrorMessage ?? 'No pudimos completar la publicación.');
        setStage('Failed');
        stopPolling();
        return;
      }
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : 'Error al consultar el estado de publicación');
    }
  };

  useEffect(() => {
    fetchStatus();
    pollingRef.current = setInterval(fetchStatus, POLL_INTERVAL_MS);
    return () => stopPolling();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runId]);

  const handleGoDashboard = () => navigate('/');

  const handleCreateAnother = () => {
    clearStudioRun();
    navigate('/studio');
  };

  const handleRetry = () => {
    // The real backend has no per-task retry once a run reaches Failed —
    // a failed run has no pending gate left to respond to (see
    // /memories/repo/backend-async-workflow-fix.md). The only real option
    // today is restarting the whole pipeline from Paso 1.
    updateStudioRun({ blueprint: null, gate1SubjectId: null, capabilityPackage: null, gate2SubjectId: null });
    navigate('/studio');
  };

  const completedTasks = tasks.filter((t) => t.status === 'Completed');
  const stepLabel = stage === 'Completed' ? 'Curso listo' : 'Publicando';

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <StudioHeader />

      <main className="flex-1 p-6">
        <div className="max-w-3xl mx-auto">
          <StudioStepIndicator activeStep={3} step3Label={stepLabel} />

          {stage === 'Failed' && (
            <PublicationErrorState
              tasks={tasks}
              errorMessage={errorMessage ?? 'No pudimos completar la publicación.'}
              isRetrying={false}
              onRetry={handleRetry}
              onGoDashboard={handleGoDashboard}
            />
          )}

          {stage === 'Running' && (
            <div className="bg-white rounded-lg shadow p-8">
              <PublicationHeader />
              <PublicationProgress
                completedCount={completedTasks.length}
                totalCount={tasks.length}
                progress={Math.round((completedTasks.length / tasks.length) * 100)}
              />
              <PublicationTaskList tasks={tasks} />
              <PublishingNotice />
              <div className="mt-6">
                <button
                  onClick={handleGoDashboard}
                  className="w-full sm:w-auto inline-flex items-center justify-center gap-2 px-6 py-3 rounded-lg font-medium border-2 border-gray-300 text-gray-700 hover:bg-gray-50 transition-all cursor-pointer"
                >
                  <Home size={18} />
                  Ir al dashboard
                </button>
              </div>
            </div>
          )}

          {stage === 'Completed' && (
            <div className="bg-white rounded-lg shadow p-8 text-center">
              <SuccessIndicator />
              {publishedResult ? (
                <>
                  <PublishedCapabilitySummary
                    title={publishedResult.CapabilityName}
                    levelCount={getStudioRun().blueprint?.Levels.length ?? 0}
                    moduleCount={publishedResult.Modules.length}
                    metricCount={new Set(publishedResult.Modules.flatMap((m) => m.Metrics.Metrics)).size}
                  />
                  <PublishedActions
                    capabilityId={publishedResult.CapabilityId ?? ''}
                    onCreateAnother={handleCreateAnother}
                  />
                </>
              ) : (
                <p className="text-gray-600">Cargando resumen de la capability publicada...</p>
              )}
            </div>
          )}
        </div>
      </main>
    </div>
  );
}

