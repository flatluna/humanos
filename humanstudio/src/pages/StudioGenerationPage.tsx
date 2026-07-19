import { useParams, useNavigate } from 'react-router-dom';
import { useEffect, useState, useRef } from 'react';
import { getCapabilityCreationStatus, BackendCapabilityPackage, BackendModuleGenerationOutcome } from '../lib/api/studioApi';
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
  const [belowThreshold, setBelowThreshold] = useState<BackendModuleGenerationOutcome | null>(null);
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
   * the backend's authoritative completed/active-titles progress payload.
   * Module generation is bounded-concurrency PARALLEL (see
   * ParallelModuleGenerationExecutor), so modules can finish in a
   * DIFFERENT order than the blueprint's own order — status is looked up
   * by TITLE membership in the backend's completed/active sets, never
   * guessed from a position/count. There is no per-module retry surfaced
   * here (only a whole-run Failed state), so no module here is ever
   * synthesized as 'Failed'. */
  const buildModuleStatuses = (
    completedModuleTitles: string[],
    activeModuleTitles: string[]
  ): ModuleGenerationStatus[] =>
    orderedModules.map((module, index) => {
      const moduleState = completedModuleTitles.includes(module.title)
        ? 'Verified'
        : activeModuleTitles.includes(module.title)
          ? 'GeneratingScript'
          : 'Pending';

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

      // Fixed 2026-07-16: Stage can also reach 'Completed' WITHOUT a real
      // CapabilityPackage — ModuleRevisionRequiredExecutor's terminal
      // outcome, when fewer than the required ratio of modules reached
      // Verified. Its payload shape is {BlueprintId, Modules}, no
      // PackageId/CapabilityId (unlike a Gate-2-ready package). Without
      // this check the polling loop below silently continues forever,
      // stuck showing 0%/0 modules (Progress is null once terminal).
      if (result.Stage === 'Completed' && result.Payload && typeof result.Payload === 'object' && 'Modules' in result.Payload && !('PackageId' in result.Payload)) {
        setBelowThreshold(result.Payload as BackendModuleGenerationOutcome);
        if (pollingIntervalRef.current) {
          clearInterval(pollingIntervalRef.current);
          pollingIntervalRef.current = null;
        }
        return;
      }

      const completedModules = result.Progress?.CompletedModules ?? 0;
      const totalModules = result.Progress?.TotalModules ?? orderedModules.length;
      const activeModuleTitles = result.Progress?.ActiveModuleTitles ?? [];
      const completedModuleTitles = result.Progress?.CompletedModuleTitles ?? [];

      setStatus({
        runId,
        status: 'Generating',
        totalModules,
        verifiedModules: completedModules,
        modules: buildModuleStatuses(completedModuleTitles, activeModuleTitles),
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

  if (belowThreshold) {
    const verifiedCount = belowThreshold.Modules.filter((m) => m.Status === 4).length;
    return (
      <div className="max-w-4xl mx-auto py-12 px-4">
        <div className="bg-amber-50 border-2 border-amber-300 rounded-lg p-6">
          <p className="text-amber-900 font-semibold mb-2">
            La generación terminó, pero no alcanzó el mínimo de módulos verificados.
          </p>
          <p className="text-amber-800 text-sm mb-4">
            {verifiedCount} de {belowThreshold.Modules.length} módulos quedaron Verificados — se necesita al
            menos el 85% para continuar a la revisión final (Gate 2). Este proceso no reintenta automáticamente
            desde aquí: tendrás que iniciar una corrida nueva.
          </p>
          <div className="space-y-2 mb-4 text-left">
            {belowThreshold.Modules.map((m) => (
              <div
                key={m.Module.Title}
                className={`border rounded-lg p-3 text-sm ${
                  m.Status === 4 ? 'border-green-200 bg-green-50' : 'border-amber-200 bg-white'
                }`}
              >
                <p className="font-semibold text-gray-900">
                  {m.Status === 4 ? '✅' : m.Status === 6 ? '⛔' : '⚠️'} {m.Module.Title}
                </p>
                {m.Status !== 4 && (
                  <p className="text-gray-600 mt-1">
                    {m.FailureReason ?? m.Metrics?.Rationale ?? 'No se pudo verificar el TargetMetric.'}
                  </p>
                )}
              </div>
            ))}
          </div>
          <button
            onClick={() => navigate('/studio')}
            className="px-4 py-2 bg-amber-100 text-amber-800 font-medium rounded hover:bg-amber-200 transition-all"
          >
            Iniciar una corrida nueva
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

