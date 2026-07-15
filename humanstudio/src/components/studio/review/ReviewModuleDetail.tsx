import { FinalReviewModule } from '../../../types';
import { X } from 'lucide-react';

export interface ReviewModuleDetailProps {
  module: FinalReviewModule;
  onClose: () => void;
  onRequestChange?: (moduleId: string) => void;
}

export function ReviewModuleDetail({ module, onClose, onRequestChange }: ReviewModuleDetailProps) {
  const verification = module.verification;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 z-50 flex items-start justify-end">
      <div className="bg-white w-full max-w-2xl max-h-screen overflow-y-auto">
        {/* Header */}
        <div className="sticky top-0 bg-white border-b border-gray-200 p-6 flex items-start justify-between">
          <div>
            <h2 className="text-2xl font-bold text-gray-900">
              {String(module.order).padStart(2, '0')} {module.title}
            </h2>
            <p className="text-gray-600 mt-1">{module.description}</p>
          </div>
          <button
            onClick={onClose}
            className="flex-shrink-0 text-gray-400 hover:text-gray-600"
          >
            <X className="w-6 h-6" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-6">
          {/* Metadata */}
          <div>
            <h3 className="text-lg font-semibold text-gray-900 mb-3">Información</h3>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="text-sm text-gray-600">Tipo</label>
                <p className="font-semibold text-gray-900">{module.moduleType}</p>
              </div>
              <div>
                <label className="text-sm text-gray-600">Métrica objetivo</label>
                <p className="font-semibold text-green-600 inline-block bg-green-50 px-2 py-1 rounded">
                  🟩 {module.targetMetric}
                </p>
              </div>
            </div>
          </div>

          {/* Script */}
          <div>
            <h3 className="text-lg font-semibold text-gray-900 mb-3">Guion del Instructor</h3>
            <div className="bg-gray-50 border border-gray-200 rounded p-4">
              <pre className="text-sm text-gray-700 whitespace-pre-wrap font-mono">
                {module.script}
              </pre>
            </div>
          </div>

          {/* Verification */}
          <div>
            <h3 className="text-lg font-semibold text-gray-900 mb-3">Verificación del Métrico</h3>
            <div className="bg-gray-50 border border-gray-200 rounded p-4 mb-3">
              <p className="text-sm font-semibold text-gray-700 mb-2">
                Resumen:{' '}
                <span
                  className={
                    verification.status === 'Verified'
                      ? 'text-green-600'
                      : verification.status === 'Warning'
                        ? 'text-amber-600'
                        : 'text-red-600'
                  }
                >
                  {verification.summary}
                </span>
              </p>
            </div>

            {/* Principles */}
            <div className="space-y-2">
              {verification.principles.map((principle) => (
                <div
                  key={principle.principle}
                  className={`p-3 rounded border ${
                    principle.status === 'Pass'
                      ? 'bg-green-50 border-green-200'
                      : principle.status === 'Warning'
                        ? 'bg-amber-50 border-amber-200'
                        : principle.status === 'Fail'
                          ? 'bg-red-50 border-red-200'
                          : 'bg-gray-50 border-gray-200'
                  }`}
                >
                  <div className="flex items-start gap-2">
                    <span
                      className={`font-semibold font-mono text-sm flex-shrink-0 ${
                        principle.status === 'Pass'
                          ? 'text-green-600'
                          : principle.status === 'Warning'
                            ? 'text-amber-600'
                            : principle.status === 'Fail'
                              ? 'text-red-600'
                              : 'text-gray-600'
                      }`}
                    >
                      {principle.principle}{' '}
                      {principle.status === 'Pass'
                        ? '✓'
                        : principle.status === 'Warning'
                          ? '!'
                          : principle.status === 'Fail'
                            ? '×'
                            : '◯'}
                    </span>
                    <p className="text-sm text-gray-700">{principle.explanation}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-4 border-t border-gray-200">
            <button
              onClick={onClose}
              className="flex-1 px-4 py-2 text-gray-700 border border-gray-300 rounded-lg hover:bg-gray-50 font-semibold"
            >
              Cerrar
            </button>
            {onRequestChange && (
              <button
                onClick={() => onRequestChange(module.id)}
                className="flex-1 px-4 py-2 bg-amber-500 text-white rounded-lg hover:bg-amber-600 font-semibold"
              >
                Solicitar cambio
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
