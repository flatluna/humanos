import { FinalReviewModule } from '../../../types';
import { ChevronRight } from 'lucide-react';

export interface ReviewModuleCardProps {
  module: FinalReviewModule;
  onReview?: (moduleId: string) => void;
}

export function ReviewModuleCard({ module, onReview }: ReviewModuleCardProps) {
  const verification = module.verification;

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-4 mb-3 hover:shadow-md transition-shadow">
      <div className="flex items-start gap-3">
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <h4 className="font-semibold text-gray-900">
              {String(module.order).padStart(2, '0')} {module.title}
            </h4>
          </div>

          <p className="text-sm text-gray-600 mb-2">{module.description}</p>

          <div className="flex flex-wrap items-center gap-3 mb-2">
            <div className="text-xs">
              <span className="text-gray-500">Tipo:</span>
              <span className="font-semibold text-gray-700 ml-1">{module.moduleType}</span>
            </div>
            <div className="text-xs">
              <span className="inline-block bg-green-100 text-green-800 px-2 py-1 rounded">
                🟩 Métrica: {module.targetMetric}
              </span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-sm font-semibold text-gray-700">Estado:</span>
            <span
              className={`text-sm font-semibold ${
                verification.status === 'Verified'
                  ? 'text-green-600'
                  : verification.status === 'Warning'
                    ? 'text-amber-600'
                    : 'text-red-600'
              }`}
            >
              {verification.status === 'Verified'
                ? 'Verificada ✓'
                : verification.status === 'Warning'
                  ? 'Advertencia !'
                  : 'Falló ×'}
            </span>
          </div>
        </div>

        <button
          onClick={() => onReview?.(module.id)}
          className="flex-shrink-0 text-blue-600 hover:text-blue-700 font-semibold text-sm flex items-center gap-1 whitespace-nowrap"
        >
          Revisar <ChevronRight className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}
