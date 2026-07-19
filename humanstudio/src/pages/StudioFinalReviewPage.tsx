import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import StudioHeader from '../components/studio/StudioHeader';
import StudioStepIndicator from '../components/studio/StudioStepIndicator';
import { respondToCapabilityCreationGate } from '../lib/api/studioApi';
import { getStudioRun, updateStudioRun } from '../lib/studioRunStore';
import { AlertCircle } from 'lucide-react';

/**
 * GATE 2 review — real backend data. NOTE: the real MetricoAgent produces
 * an assigned-metrics list + a free-text rationale per module, NOT a
 * P1-P7 pass/warning/fail grid (that was mock-only fabrication). This
 * page shows what's actually real: metrics chips + rationale + full
 * script, grouped by level (matched back to the blueprint by module
 * title, since ModuleId is [JsonIgnore]d and never crosses the wire).
 */
export function StudioFinalReviewPage() {
  const { runId } = useParams<{ runId: string }>();
  const navigate = useNavigate();
  const run = getStudioRun();

  const [selectedModuleTitle, setSelectedModuleTitle] = useState<string | null>(null);
  const [showRejectModal, setShowRejectModal] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rejectedMessage, setRejectedMessage] = useState<string | null>(null);

  if (!runId) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-red-600 font-semibold">Error: runId no encontrado</p>
      </div>
    );
  }

  const { blueprint, capabilityPackage, gate2SubjectId } = run;

  if (!capabilityPackage || !gate2SubjectId || !blueprint) {
    return (
      <div className="min-h-screen bg-gray-50 flex flex-col">
        <StudioHeader />
        <main className="flex-1 p-6 flex items-center justify-center">
          <div className="text-center">
            <p className="text-gray-700 font-semibold mb-4">
              No hay un paquete de revisión disponible para este run.
            </p>
            <button
              onClick={() => navigate('/studio')}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-semibold"
            >
              Volver al Paso 1
            </button>
          </div>
        </main>
      </div>
    );
  }

  // Match completed modules back to their blueprint level by title (the
  // only stable common key available on the frontend side of the wire).
  const layerByTitle = new Map<string, string>();
  blueprint.Levels.forEach((level) => {
    level.Modules.forEach((module) => layerByTitle.set(module.Title, level.Layer));
  });

  const modulesByLevel = new Map<string, typeof capabilityPackage.Modules>();
  capabilityPackage.Modules.forEach((completed) => {
    const layer = layerByTitle.get(completed.Module.Title) ?? 'Sin nivel';
    if (!modulesByLevel.has(layer)) {
      modulesByLevel.set(layer, []);
    }
    modulesByLevel.get(layer)!.push(completed);
  });

  const selectedModule = capabilityPackage.Modules.find(
    (m) => m.Module.Title === selectedModuleTitle
  );

  const handleApprove = async () => {
    setIsSubmitting(true);
    setError(null);
    try {
      await respondToCapabilityCreationGate(runId, {
        subjectId: gate2SubjectId,
        approved: true,
      });
      navigate(`/studio/runs/${runId}/publishing`);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al aprobar la publicación.');
      setIsSubmitting(false);
    }
  };

  const handleReject = async () => {
    if (!rejectReason.trim()) {
      setError('Escribe una razón para el rechazo.');
      return;
    }
    setIsSubmitting(true);
    setError(null);
    try {
      const status = await respondToCapabilityCreationGate(runId, {
        subjectId: gate2SubjectId,
        approved: false,
        comments: rejectReason,
      });
      updateStudioRun({ capabilityPackage: null, gate2SubjectId: null });
      setShowRejectModal(false);
      setRejectedMessage(
        typeof status.Payload === 'string' ? status.Payload : 'La capability fue rechazada.'
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al rechazar la publicación.');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (rejectedMessage) {
    return (
      <div className="min-h-screen bg-gray-50 flex flex-col">
        <StudioHeader />
        <main className="flex-1 p-6 flex items-center justify-center">
          <div className="text-center max-w-md">
            <p className="text-amber-700 font-semibold mb-2">Capability rechazada</p>
            <p className="text-gray-700 mb-4">{rejectedMessage}</p>
            <p className="text-gray-500 text-sm mb-4">
              Este run terminó. El backend actual no soporta reanudar un run rechazado —
              inicia una nueva capability desde el Paso 1.
            </p>
            <button
              onClick={() => navigate('/studio')}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-semibold"
            >
              Volver al Paso 1
            </button>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <StudioHeader />

      <main className="flex-1 p-6">
        <div className="max-w-4xl mx-auto">
          <StudioStepIndicator activeStep={3} />

          <div className="mb-6 text-center">
            <p className="text-sm text-gray-600 font-semibold">Revisión final (GATE 2)</p>
          </div>

          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-6 flex items-start gap-3">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
              <p className="text-red-800 text-sm">{error}</p>
            </div>
          )}

          {/* Header */}
          <div className="bg-white rounded-lg shadow p-6 mb-6">
            <h2 className="text-2xl font-bold text-gray-900 mb-1">{capabilityPackage.CapabilityName}</h2>
            <p className="text-gray-600 mb-3">{blueprint.Goal}</p>
            <p className="text-sm text-purple-800 bg-purple-50 border border-purple-200 rounded-lg p-3">
              {blueprint.ScopeDeclaration}
            </p>
            <p className="text-sm text-gray-500 mt-3">
              {blueprint.Levels.length} niveles ·{' '}
              {blueprint.Levels.reduce((acc, l) => acc + l.Modules.length, 0)} módulos
            </p>
          </div>

          {/* Levels + Modules */}
          {Array.from(modulesByLevel.entries()).map(([layer, modules]) => (
            <div key={layer} className="mb-6">
              <div className="px-4 py-2 bg-blue-100 border-2 border-blue-600 rounded-sm mb-4 inline-block">
                <span className="font-semibold text-blue-900">🟦 NIVEL: {layer}</span>
              </div>

              <div className="space-y-3">
                {modules.map((completed) => (
                  <div
                    key={completed.Module.Title}
                    className="bg-white border border-gray-200 rounded-lg p-5 hover:shadow-md transition-shadow"
                  >
                    <div className="flex items-start justify-between gap-4 mb-3">
                      <div>
                        <h4 className="font-bold text-gray-900">{completed.Module.Title}</h4>
                        <p className="text-sm text-gray-600">{completed.Module.Description}</p>
                      </div>
                      <span className="px-3 py-1 bg-gray-200 rounded-full text-xs font-medium text-gray-700 whitespace-nowrap">
                        {completed.Module.Type}
                      </span>
                    </div>

                    <div className="flex flex-wrap gap-1.5 mb-3">
                      {completed.Metrics.Metrics.map((metric) => (
                        <span
                          key={metric}
                          className="inline-flex items-center px-3 py-1 bg-green-100 border border-green-600 rounded-full text-xs font-semibold text-green-900"
                        >
                          🟩 {metric}
                        </span>
                      ))}
                    </div>

                    <p className="text-sm text-gray-600 italic mb-3">{completed.Metrics.Rationale}</p>

                    <button
                      onClick={() => setSelectedModuleTitle(completed.Module.Title)}
                      className="text-sm text-blue-600 hover:underline font-medium"
                    >
                      Ver guion completo →
                    </button>
                  </div>
                ))}
              </div>
            </div>
          ))}

          {/* Tutor Knowledge Base preview */}
          <div className="bg-white rounded-lg shadow p-6 mb-6">
            <h3 className="text-lg font-bold text-gray-900 mb-2">Base de conocimiento del tutor</h3>
            <p className="text-sm text-gray-600 whitespace-pre-wrap max-h-40 overflow-y-auto">
              {capabilityPackage.TutorKnowledgeBase}
            </p>
          </div>

          {/* Actions */}
          <div className="bg-white rounded-lg shadow p-6 flex flex-col sm:flex-row gap-3">
            <button
              onClick={() => setShowRejectModal(true)}
              disabled={isSubmitting}
              className="flex-1 px-6 py-3 rounded-lg font-medium bg-amber-100 text-amber-800 hover:bg-amber-200 transition-all disabled:opacity-50"
            >
              Rechazar
            </button>
            <button
              onClick={handleApprove}
              disabled={isSubmitting}
              className="flex-1 px-6 py-3 rounded-lg font-medium bg-blue-600 text-white hover:bg-blue-700 transition-all disabled:opacity-50"
            >
              {isSubmitting ? 'Procesando...' : '✓ Aprobar y publicar →'}
            </button>
          </div>
        </div>
      </main>

      {/* Module Detail Panel */}
      {selectedModule && (
        <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex justify-end">
          <div className="bg-white w-full max-w-2xl h-full overflow-y-auto p-6">
            <div className="flex items-start justify-between mb-4">
              <h2 className="text-xl font-bold text-gray-900">{selectedModule.Module.Title}</h2>
              <button
                onClick={() => setSelectedModuleTitle(null)}
                className="text-gray-500 hover:text-gray-700"
              >
                ✕
              </button>
            </div>
            <p className="text-gray-600 mb-4">{selectedModule.Module.Description}</p>
            {selectedModule.Script.Chapters?.length > 0 && (
              <div className="mb-4">
                <h3 className="text-sm font-bold text-gray-900 mb-2">
                  Capítulos ({selectedModule.Script.Chapters.length}) — ciclo de fases preparado para presentación futura por turnos/voz
                </h3>
                <div className="space-y-2">
                  {selectedModule.Script.Chapters.map((chapter, index) => (
                    <div
                      key={`${chapter.Title}-${index}`}
                      className={`border rounded-lg p-3 ${
                        chapter.IsPrimaryWeight
                          ? 'border-purple-300 bg-purple-50'
                          : 'border-gray-200 bg-gray-50'
                      }`}
                    >
                      <div className="flex items-center justify-between gap-2 mb-1">
                        <span className="text-sm font-semibold text-gray-900">
                          {index + 1}. {chapter.Title}
                        </span>
                        {chapter.IsPrimaryWeight && (
                          <span className="px-2 py-0.5 bg-purple-200 text-purple-900 text-xs font-medium rounded-full whitespace-nowrap">
                            ⭐ Peso principal
                          </span>
                        )}
                      </div>
                      <p className="text-sm text-gray-700 whitespace-pre-wrap mb-2">{chapter.TeachingContent}</p>

                      <div className="text-xs text-blue-900 bg-blue-50 border border-blue-200 rounded-md p-2 mb-1">
                        <span className="font-semibold">
                          {chapter.IsCumulativeRecall ? '🔁 Recordar acumulativo: ' : '🧠 Recordar rápido: '}
                        </span>
                        {chapter.RecallPrompt}
                      </div>

                      {chapter.PredictionPrompt && (
                        <div className="text-xs text-purple-900 bg-purple-100 border border-purple-300 rounded-md p-2 mb-1">
                          <span className="font-semibold">🔮 Predicción: </span>
                          {chapter.PredictionPrompt}
                        </div>
                      )}

                      {chapter.MiniPracticePrompt && (
                        <div className="text-xs text-green-900 bg-green-50 border border-green-200 rounded-md p-2">
                          <span className="font-semibold">✏️ Mini-práctica: </span>
                          {chapter.MiniPracticePrompt}
                        </div>
                      )}
                    </div>
                  ))}
                </div>

                {selectedModule.Script.ReflectionPrompt && (
                  <div className="text-xs text-amber-900 bg-amber-50 border border-amber-300 rounded-md p-3 mt-3">
                    <span className="font-semibold">🏁 Reflexión final: </span>
                    {selectedModule.Script.ReflectionPrompt}
                  </div>
                )}
              </div>
            )}
            <h3 className="text-sm font-bold text-gray-900 mb-2">Guion del Instructor</h3>
            <pre className="whitespace-pre-wrap text-sm text-gray-700 bg-gray-50 border border-gray-200 rounded-lg p-4">
              {selectedModule.Script.Script}
            </pre>
          </div>
        </div>
      )}

      {/* Reject Modal */}
      {showRejectModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-center justify-center">
          <div className="bg-white rounded-lg p-6 max-w-md w-full mx-4">
            <h2 className="text-xl font-bold text-gray-900 mb-4">Rechazar capability</h2>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="Describe por qué se rechaza..."
              rows={4}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-gray-900 placeholder-gray-400 mb-4"
            />
            <div className="flex gap-3">
              <button
                onClick={() => setShowRejectModal(false)}
                disabled={isSubmitting}
                className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-semibold"
              >
                Cancelar
              </button>
              <button
                onClick={handleReject}
                disabled={isSubmitting}
                className="flex-1 px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-semibold disabled:opacity-50"
              >
                {isSubmitting ? 'Enviando...' : 'Confirmar rechazo'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

