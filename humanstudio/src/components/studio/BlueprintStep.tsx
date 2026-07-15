import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import StudioHeader from './StudioHeader';
import StudioStepIndicator from './StudioStepIndicator';
import ScopeDeclarationCard from './ScopeDeclarationCard';
import BlueprintSummary from './BlueprintSummary';
import LevelHeader from './LevelHeader';
import ModuleCard from './ModuleCard';
import BlueprintLegend from './BlueprintLegend';
import GateOneActions from './GateOneActions';
import { getCapabilityCreationStatus, BackendCapabilityBlueprint } from '../../lib/api/studioApi';
import { getStudioRun, updateStudioRun } from '../../lib/studioRunStore';
import { Blueprint } from '../../types';

const POLL_INTERVAL_MS = 2000;

/** Adapts the real backend CapabilityBlueprint into the existing UI's Blueprint shape. */
function mapBackendBlueprintToUi(runId: string, backend: BackendCapabilityBlueprint): Blueprint {
  let moduleNumber = 0;

  return {
    id: runId,
    createdAt: new Date().toISOString(),
    scopeDeclaration: {
      objective: backend.Goal,
      // No real "intensity" concept in the backend pipeline yet — kept as
      // a neutral default so ScopeDeclarationCard (shared with the mock
      // flow) still renders; the objective/description below are 100%
      // real data.
      intensity: 'serious',
      description: backend.ScopeDeclaration,
    },
    levels: backend.Levels.map((level) => ({
      levelName: level.Layer,
      description: level.HumanTransformation,
      modules: level.Modules.map((module) => {
        moduleNumber += 1;
        return {
          id: `${level.Layer}-${moduleNumber}`,
          number: moduleNumber,
          title: module.Title,
          description: module.Description,
          moduleType: module.Type,
          targetMetric: module.TargetMetric,
        };
      }),
    })),
  };
}

const BlueprintStep: React.FC = () => {
  const navigate = useNavigate();
  const run = getStudioRun();
  const [blueprint, setBlueprint] = useState<Blueprint | null>(null);
  const [gate1SubjectId, setGate1SubjectId] = useState<string | null>(run.gate1SubjectId);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (!run.runId) {
      navigate('/studio');
      return;
    }

    // Already have the blueprint from a previous visit to this page in
    // this session (e.g. browser back) — no need to poll again.
    if (run.blueprint && run.gate1SubjectId) {
      setBlueprint(mapBackendBlueprintToUi(run.runId, run.blueprint));
      setGate1SubjectId(run.gate1SubjectId);
      setIsLoading(false);
      return;
    }

    const runId = run.runId;

    const poll = async () => {
      try {
        const status = await getCapabilityCreationStatus(runId);

        if (status.Stage === 'PendingGate' && status.Payload && typeof status.Payload === 'object' && 'Levels' in status.Payload) {
          const backendBlueprint = status.Payload as BackendCapabilityBlueprint;
          updateStudioRun({ blueprint: backendBlueprint, gate1SubjectId: status.PendingSubjectId });
          setBlueprint(mapBackendBlueprintToUi(runId, backendBlueprint));
          setGate1SubjectId(status.PendingSubjectId);
          setIsLoading(false);
          if (pollingRef.current) {
            clearInterval(pollingRef.current);
            pollingRef.current = null;
          }
        } else if (status.Stage === 'Failed') {
          setError(status.ErrorMessage ?? 'La generación del blueprint falló.');
          setIsLoading(false);
          if (pollingRef.current) {
            clearInterval(pollingRef.current);
            pollingRef.current = null;
          }
        }
        // else Stage === 'Running': keep polling.
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Error al consultar el estado del blueprint.');
      }
    };

    poll();
    pollingRef.current = setInterval(poll, POLL_INTERVAL_MS);

    return () => {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [run.runId]);

  const handleBackToObjective = () => {
    navigate('/studio');
  };

  if (!run.runId) {
    return null;
  }

  if (isLoading) {
    return (
      <div className="max-w-4xl mx-auto">
        <StudioHeader />
        <StudioStepIndicator activeStep={2} />
        <div className="bg-white rounded-lg shadow p-8 text-center">
          <p className="text-gray-700 font-semibold">
            Generando el blueprint (Curador + Arquitecto)... esto puede tardar uno o dos minutos.
          </p>
        </div>
      </div>
    );
  }

  if (error || !blueprint || !gate1SubjectId) {
    return (
      <div className="max-w-4xl mx-auto">
        <StudioHeader />
        <StudioStepIndicator activeStep={2} />
        <div className="bg-white rounded-lg shadow p-8 text-center">
          <p className="text-red-600 font-semibold mb-4">{error ?? 'No se pudo cargar el blueprint.'}</p>
          <button
            onClick={handleBackToObjective}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-semibold"
          >
            Volver al Paso 1
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto">
      <StudioHeader />
      <StudioStepIndicator activeStep={2} />

      <div className="bg-white rounded-lg shadow p-8">
        {/* Scope Declaration aparece ANTES que los módulos */}
        <ScopeDeclarationCard scopeDeclaration={blueprint.scopeDeclaration} />

        {/* Blueprint Summary */}
        <BlueprintSummary blueprint={blueprint} />

        {/* Levels and Modules */}
        <div className="mb-8">
          <h2 className="text-2xl font-bold text-gray-900 mb-6">
            Estructura del blueprint
          </h2>

          {blueprint.levels.map((level) => (
            <div key={level.levelName} className="mb-8">
              <LevelHeader
                levelName={level.levelName}
                description={level.description}
              />

              {level.modules.map((module) => (
                <ModuleCard key={module.id} module={module} />
              ))}
            </div>
          ))}
        </div>

        {/* Blueprint Legend */}
        <BlueprintLegend />

        {/* GATE 1 Actions */}
        <GateOneActions
          runId={run.runId}
          gate1SubjectId={gate1SubjectId}
          onBackToObjective={handleBackToObjective}
        />
      </div>
    </div>
  );
};

export default BlueprintStep;

