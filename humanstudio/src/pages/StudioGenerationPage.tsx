import { useParams, useNavigate } from 'react-router-dom';
import { useEffect, useState, useRef } from 'react';
import { getCapabilityCreationStatus, BackendCapabilityPackage } from '../lib/api/studioApi';
import { getStudioRun, updateStudioRun, flattenBlueprintModules } from '../lib/studioRunStore';
import type { GenerationRunStatus, HumanEvolutionLevelName, ModuleGenerationStatus } from '../types';
import StudioHeader from '../components/studio/StudioHeader';
import StudioStepIndicator from '../components/studio/StudioStepIndicator';
import OverallProgress from '../components/studio/generation/OverallProgress';
import GenerationNotice from '../components/studio/generation/GenerationNotice';
import GenerationLevelSection from '../components/studio/generation/GenerationLevelSection';
import GenerationActions from '../components/studio/generation/GenerationActions';

const POLL_INTERVAL_MS = 2000;

export function StudioGenerationPage() {
  const { runId } = useParams<{ runId: string }>();
  const navigate = useNavigate();
  const [status, setStatus] = useState<GenerationRunStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const pollingIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  if (!runId) {
    return <div>Error: runId not found</div>;
  }

  const run = getStudioRun();
  const blueprint = run.blueprint;

  if (!blueprint) {
    // No blueprint in the shared run store (e.g. page opened directly,
    // or the SPA state was lost) — nothing to synthesize progress against.
    navigate('/studio');
    return null;
  }

  const orderedModules = flattenBlueprintModules(blueprint);

  /** Synthesizes the per-module status list the existing UI expects from
   * the backend's counter+current-title-only progress payload — the real
   * pipeline processes modules strictly in blueprint order (confirmed:
   * ModuleQueueInitializerExecutor flattens Levels->Modules the same way),
   * so modules before `completedModules` are Verified, the one at that
   * index is in progress, the rest are Pending. There is no per-module
   * retry in the real backend (only a whole-run Failed state), so no
   * module here is ever synthesized as 'Failed'. */
  const buildModuleStatuses = (completedModules: number): ModuleGenerationStatus[] =>
    orderedModules.map((module, index) => {
      const moduleState =
        index < completedModules ? 'Verified' : index === completedModules ? 'GeneratingScript' : 'Pending';

      return {
        id: `${module.layer}-${index + 1}`,
        order: index + 1,
        title: module.title,
        level: module.layer as HumanEvolutionLevelName,
        targetMetric: module.targetMetric,
        instructorStatus: moduleState === 'Verified' ? 'Completed' : moduleState === 'GeneratingScript' ? 'Generating' : 'Pending',
        moduleState,
      };
    });

  const fetchStatus = async () => {
    try {
      const result = await getCapabilityCreationStatus(runId);

      if (result.Stage === 'PendingGate' && result.Payload && typeof result.Payload === 'object' && 'Modules' in result.Payload) {
        // Gate 2 reached — package is ready for final review.
        const capabilityPackage = result.Payload as BackendCapabilityPackage;
        updateStudioRun({ capabilityPackage, gate2SubjectId: result.PendingSubjectId });
        if (pollingIntervalRef.current) {
          clearInterval(pollingIntervalRef.current);
          pollingIntervalRef.current = null;
        }
        navigate(`/studio/runs/${runId}/review`);
        return;
      }

      if (result.Stage === 'Failed') {
        setError(result.ErrorMessage ?? 'La generación falló.');
        if (pollingIntervalRef.current) {
          clearInterval(pollingIntervalRef.current);
          pollingIntervalRef.current = null;
        }
        return;
      }

      const completedModules = result.Progress?.CompletedModules ?? 0;
      const totalModules = result.Progress?.TotalModules ?? orderedModules.length;

      setStatus({
        runId,
        status: 'Generating',
        totalModules,
        verifiedModules: completedModules,
        modules: buildModuleStatuses(completedModules),
        updatedAt: new Date().toISOString(),
      });
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al cargar el estado');
    }
  };

  // Initial load and polling setup
  useEffect(() => {
    fetchStatus();

    pollingIntervalRef.current = setInterval(fetchStatus, POLL_INTERVAL_MS);

    return () => {
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runId]);

  const handleManualRefresh = async () => {
    setIsRefreshing(true);
    await fetchStatus();
    setIsRefreshing(false);
  };

  if (error) {
    return (
      <div className="max-w-4xl mx-auto py-12 px-4">
        <div className="bg-red-50 border-2 border-red-300 rounded-lg p-6 text-center">
          <p className="text-red-900 font-semibold mb-2">No pudimos completar la generación.</p>
          <p className="text-red-800 text-sm mb-4">{error}</p>
          <button
            onClick={() => navigate('/studio')}
            className="px-4 py-2 bg-red-100 text-red-700 font-medium rounded hover:bg-red-200 transition-all"
          >
            Volver al Paso 1
          </button>
        </div>
      </div>
    );
  }

  if (!status) {
    return (
      <div className="max-w-4xl mx-auto py-12 px-4">
        <div className="text-center">
          <div className="inline-block">
            <div className="w-12 h-12 border-4 border-blue-100 rounded-full"></div>
            <div className="absolute w-12 h-12 border-4 border-transparent border-t-blue-600 rounded-full animate-spin"></div>
          </div>
          <p className="mt-4 text-gray-700">Cargando estado de generación...</p>
        </div>
      </div>
    );
  }

  // Group modules by level
  const modulesByLevel = new Map<HumanEvolutionLevelName, typeof status.modules>();
  status.modules.forEach((mod: typeof status.modules[0]) => {
    if (!modulesByLevel.has(mod.level)) {
      modulesByLevel.set(mod.level, []);
    }
    modulesByLevel.get(mod.level)!.push(mod);
  });

  const isComplete = false; // Navigation to /review happens automatically once Gate 2 is reached.

  return (
    <div className="max-w-4xl mx-auto">
      <StudioHeader />
      <StudioStepIndicator activeStep={3} />

      <div className="bg-white rounded-lg shadow p-8">
        {/* Page Title */}
        <h2 className="text-2xl font-bold text-gray-900 mb-2">
          Generando tu capability
        </h2>
        <p className="text-gray-600 mb-6">
          El Instructor está preparando el contenido y el Métrico lo verifica.
        </p>

        {/* Overall Progress */}
        <OverallProgress
          verifiedModules={status.verifiedModules}
          totalModules={status.totalModules}
        />

        {/* Generation Notice */}
        <GenerationNotice />

        {/* Modules Grouped by Level */}
        <div className="mb-8">
          <h3 className="text-xl font-bold text-gray-900 mb-6">
            Progreso por nivel
          </h3>

          {Array.from(modulesByLevel.entries()).map(([level, modules]) => (
            <GenerationLevelSection
              key={level}
              levelName={level}
              modules={modules}
              onRetryModule={async () => {
                // No per-module retry endpoint in the real backend — only
                // a whole-run Failed state exists. Manual refresh instead.
                await handleManualRefresh();
              }}
              isRetrying={isRefreshing}
            />
          ))}
        </div>

        {/* Actions */}
        <GenerationActions
          onRefresh={handleManualRefresh}
          isRefreshing={isRefreshing}
          isComplete={isComplete}
          runId={runId}
        />
      </div>
    </div>
  );
}

