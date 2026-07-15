import { AlertCircle } from 'lucide-react';
import { ModuleGenerationStatus } from '../../../types';

interface ModuleGenerationCardProps {
  module: ModuleGenerationStatus;
  onRetry: (moduleId: string) => Promise<void>;
  isRetrying: boolean;
}

function getStateIcon(state: string): string {
  switch (state) {
    case 'Pending':
      return '○';
    case 'GeneratingScript':
    case 'VerifyingMetric':
      return '⟳';
    case 'ScriptCompleted':
      return '⟳';
    case 'Verified':
      return '✓';
    case 'RequiresReview':
      return '!';
    case 'Failed':
      return '×';
    default:
      return '○';
  }
}

function getStateColor(state: string): string {
  switch (state) {
    case 'Pending':
      return 'text-gray-500';
    case 'GeneratingScript':
    case 'VerifyingMetric':
    case 'ScriptCompleted':
      return 'text-blue-600 animate-spin';
    case 'Verified':
      return 'text-green-600';
    case 'RequiresReview':
      return 'text-amber-600';
    case 'Failed':
      return 'text-red-600';
    default:
      return 'text-gray-500';
  }
}

function getInstructorStatusLabel(status: string): string {
  switch (status) {
    case 'Pending':
      return 'Pendiente';
    case 'Generating':
      return 'Generando guion...';
    case 'Completed':
      return 'Completado';
    case 'Error':
      return 'Error';
    default:
      return status;
  }
}

function getMetricVerificationLabel(moduleState: string): string {
  switch (moduleState) {
    case 'Pending':
    case 'GeneratingScript':
      return 'Pendiente';
    case 'ScriptCompleted':
      return 'Pendiente';
    case 'VerifyingMetric':
      return 'Verificando...';
    case 'Verified':
      return 'Verificada';
    case 'RequiresReview':
      return 'Requiere revisión';
    case 'Failed':
      return 'Error en verificación';
    default:
      return moduleState;
  }
}

export default function ModuleGenerationCard({
  module,
  onRetry,
  isRetrying,
}: ModuleGenerationCardProps) {
  const stateIcon = getStateIcon(module.moduleState);
  const stateColor = getStateColor(module.moduleState);
  const isFailed = module.moduleState === 'Failed';

  return (
    <div
      className={`border rounded-lg p-4 ${
        isFailed ? 'border-red-300 bg-red-50' : 'border-gray-200 bg-white'
      }`}
    >
      {/* Module Header - Title + State */}
      <div className="flex items-start justify-between mb-3">
        <div className="flex-1">
          <h4 className="font-semibold text-gray-900">
            {String(module.order).padStart(2, '0')} {module.title}
          </h4>
        </div>
        <div className={`text-lg font-bold ${stateColor}`}>{stateIcon}</div>
      </div>

      {/* Main Content Grid */}
      <div className="space-y-3 mb-3">
        {/* Instructor Status */}
        <div className="flex items-center justify-between text-sm">
          <span className="text-gray-600">Instructor:</span>
          <span className="font-medium text-gray-900">
            {getInstructorStatusLabel(module.instructorStatus)}
          </span>
        </div>

        {/* Metric Badge + Verification State */}
        <div className="flex items-center justify-between">
          <div className="inline-flex items-center gap-2 px-3 py-1.5 bg-green-100 border-2 border-green-600 rounded-full">
            <span className="text-xs font-bold text-green-900">
              🟩 Métrica: {module.targetMetric}
            </span>
          </div>
          <span className="text-sm font-medium text-gray-700">
            {getMetricVerificationLabel(module.moduleState)}
          </span>
        </div>
      </div>

      {/* Error Section (if failed) */}
      {isFailed && module.errorMessage && (
        <div className="mb-3 p-3 bg-red-100 border border-red-300 rounded flex gap-2">
          <AlertCircle size={16} className="text-red-600 flex-shrink-0 mt-0.5" />
          <p className="text-sm text-red-800">{module.errorMessage}</p>
        </div>
      )}

      {/* Retry Button (if failed) */}
      {isFailed && (
        <button
          onClick={() => onRetry(module.id)}
          disabled={isRetrying}
          className={`w-full px-3 py-2 rounded text-sm font-medium transition-all ${
            isRetrying
              ? 'bg-gray-200 text-gray-500 cursor-not-allowed'
              : 'bg-orange-100 text-orange-700 hover:bg-orange-200 cursor-pointer'
          }`}
        >
          {isRetrying ? 'Reintentando...' : 'Reintentar módulo'}
        </button>
      )}
    </div>
  );
}
