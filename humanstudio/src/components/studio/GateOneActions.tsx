import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { CheckCircle, ArrowLeft, AlertCircle } from 'lucide-react';
import { respondToCapabilityCreationGate } from '../../lib/api/studioApi';

interface GateOneActionsProps {
  runId: string;
  gate1SubjectId: string;
  onBackToObjective: () => void;
}

const GateOneActions: React.FC<GateOneActionsProps> = ({
  runId,
  gate1SubjectId,
  onBackToObjective,
}) => {
  const navigate = useNavigate();
  const [isApproving, setIsApproving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showConfirmation, setShowConfirmation] = useState(false);

  const handleConfirmApproval = async () => {
    if (isApproving) return; // Guard against double-click

    setIsApproving(true);
    setError(null);

    try {
      await respondToCapabilityCreationGate(runId, {
        subjectId: gate1SubjectId,
        approved: true,
      });

      // The respond call returns immediately (Stage=Running) — the
      // generation page polls .../status for progress and the eventual
      // Gate 2 pause, same pattern as this page already used for Gate 1.
      navigate(`/studio/runs/${runId}/generation`);
    } catch (err) {
      const errorMessage =
        err instanceof Error ? err.message : 'Error desconocido';
      setError(errorMessage);
      setIsApproving(false);
    }
  };


  const handleApproveClick = () => {
    setShowConfirmation(true);
  };

  const handleCancelConfirmation = () => {
    setShowConfirmation(false);
  };

  const handleRetry = () => {
    setError(null);
    handleConfirmApproval();
  };

  return (
    <>
      {/* Confirmation Modal */}
      {showConfirmation && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-xl p-8 max-w-md mx-4">
            <h3 className="text-xl font-bold text-gray-900 mb-4">
              Confirmar aprobación del blueprint
            </h3>

            <p className="text-gray-700 mb-6">
              ¿Estás seguro? Al aprobar, el blueprint quedará bloqueado y se
              iniciará la generación del curso. No podrás hacer cambios hasta
              que se complete el proceso.
            </p>

            <div className="flex flex-col sm:flex-row gap-3 justify-end">
              <button
                onClick={handleCancelConfirmation}
                className="px-4 py-2 rounded-lg font-medium border-2 border-gray-300 text-gray-700 hover:bg-gray-50 transition-all"
              >
                Cancelar
              </button>

              <button
                onClick={() => {
                  setShowConfirmation(false);
                  handleConfirmApproval();
                }}
                disabled={isApproving}
                className={`px-4 py-2 rounded-lg font-medium flex items-center justify-center gap-2 transition-all ${
                  isApproving
                    ? 'bg-gray-400 text-gray-600 cursor-not-allowed'
                    : 'bg-green-600 text-white hover:bg-green-700 active:bg-green-800'
                }`}
              >
                {isApproving ? (
                  <>
                    <span className="animate-spin">⟳</span>
                    Aprobando...
                  </>
                ) : (
                  <>
                    <CheckCircle size={18} />
                    Sí, aprobar
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Main GATE 1 Section */}
      <div className="bg-gradient-to-r from-blue-50 to-green-50 border-2 border-blue-300 rounded-lg p-8 mt-8 mb-8">
        <h3 className="text-xl font-bold text-gray-900 mb-4 flex items-center gap-2">
          <CheckCircle size={24} className="text-blue-600" />
          GATE 1 — Aprobación requerida
        </h3>

        <p className="text-gray-700 mb-6">
          El blueprint está listo para revisión. Sin tu aprobación explícita,{' '}
          <strong>no se generará el curso</strong>. Revisa el alcance, los
          niveles y módulos. Si algo necesita cambios, vuelve al Paso 1 para
          ajustar.
        </p>

        {/* Error Message */}
        {error && (
          <div className="mb-6 p-4 bg-red-50 border-2 border-red-300 rounded-lg flex gap-3">
            <AlertCircle className="text-red-600 flex-shrink-0 mt-0.5" size={20} />
            <div className="flex-1">
              <p className="text-red-900 font-medium">Error en la aprobación</p>
              <p className="text-red-800 text-sm mt-1">{error}</p>
            </div>
          </div>
        )}

        <div className="flex flex-col sm:flex-row gap-4">
          <button
            onClick={handleApproveClick}
            disabled={isApproving}
            className={`flex-1 inline-flex items-center justify-center gap-2 px-6 py-3 rounded-lg font-medium transition-all ${
              isApproving
                ? 'bg-gray-400 text-gray-600 cursor-not-allowed'
                : 'bg-green-600 text-white hover:bg-green-700 active:bg-green-800 cursor-pointer'
            }`}
          >
            {isApproving ? (
              <>
                <span className="animate-spin">⟳</span>
                Procesando...
              </>
            ) : (
              <>
                <CheckCircle size={18} />
                ✓ Aprobar y generar curso
              </>
            )}
          </button>

          <button
            onClick={onBackToObjective}
            disabled={isApproving}
            className={`flex-1 inline-flex items-center justify-center gap-2 px-6 py-3 rounded-lg font-medium border-2 transition-all ${
              isApproving
                ? 'border-gray-300 text-gray-500 cursor-not-allowed'
                : 'border-blue-600 text-blue-600 hover:bg-blue-50 active:bg-blue-100 cursor-pointer'
            }`}
          >
            <ArrowLeft size={18} />
            ← Volver al Paso 1
          </button>
        </div>

        {/* Retry Button (only on error) */}
        {error && (
          <div className="mt-4">
            <button
              onClick={handleRetry}
              disabled={isApproving}
              className="w-full px-4 py-2 bg-orange-100 text-orange-700 font-medium rounded-lg border border-orange-300 hover:bg-orange-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
            >
              Reintentar aprobación
            </button>
          </div>
        )}

        <p className="text-xs text-gray-500 mt-4">
          Al aprobar, confirmas que el blueprint se ajusta a tu objetivo y estás
          listo para proceder con la generación del curso.
        </p>
      </div>
    </>
  );
};

export default GateOneActions;
