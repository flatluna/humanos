import { ReviewWarning as ReviewWarningType } from '../../../types';
import { AlertTriangle, AlertCircle } from 'lucide-react';

export interface ReviewWarningsProps {
  warnings: ReviewWarningType[];
  onReviewModule?: (moduleId: string) => void;
}

export function ReviewWarnings({ warnings, onReviewModule }: ReviewWarningsProps) {
  if (warnings.length === 0) {
    return null;
  }

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
      <h3 className="text-xl font-bold text-gray-900 mb-4">Advertencias</h3>

      <div className="space-y-3">
        {warnings.map((warning) => (
          <div
            key={warning.id}
            className={`flex items-start gap-3 p-3 rounded-lg ${
              warning.severity === 'Blocking'
                ? 'bg-red-50 border border-red-200'
                : 'bg-amber-50 border border-amber-200'
            }`}
          >
            <div className="flex-shrink-0 mt-0.5">
              {warning.severity === 'Blocking' ? (
                <AlertTriangle className="w-5 h-5 text-red-600" />
              ) : (
                <AlertCircle className="w-5 h-5 text-amber-600" />
              )}
            </div>

            <div className="flex-1 min-w-0">
              <p
                className={`font-semibold ${
                  warning.severity === 'Blocking' ? 'text-red-900' : 'text-amber-900'
                }`}
              >
                {warning.title}
              </p>
              <p
                className={`text-sm mt-1 ${
                  warning.severity === 'Blocking' ? 'text-red-800' : 'text-amber-800'
                }`}
              >
                {warning.description}
              </p>
              {warning.moduleId && onReviewModule && (
                <button
                  onClick={() => onReviewModule(warning.moduleId!)}
                  className={`text-sm font-semibold mt-2 underline ${
                    warning.severity === 'Blocking'
                      ? 'text-red-600 hover:text-red-700'
                      : 'text-amber-600 hover:text-amber-700'
                  }`}
                >
                  Revisar módulo →
                </button>
              )}
            </div>

            <div className="text-xs font-semibold px-2 py-1 rounded flex-shrink-0">
              {warning.severity === 'Blocking' ? (
                <span className="bg-red-200 text-red-800 px-2 py-1 rounded">BLOQUEA</span>
              ) : (
                <span className="bg-amber-200 text-amber-800 px-2 py-1 rounded">REVISAR</span>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
