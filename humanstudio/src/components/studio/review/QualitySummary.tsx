import { QualitySummary as QualitySummaryType } from '../../../types';

export interface QualitySummaryProps {
  quality: QualitySummaryType;
}

export function QualitySummary({ quality }: QualitySummaryProps) {
  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
      <h3 className="text-xl font-bold text-gray-900 mb-4">Calidad general</h3>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <span className="text-gray-700">✓ Módulos completados</span>
          <span className="font-semibold text-gray-900">
            {quality.modulesCompleted} de {quality.totalModules}
          </span>
        </div>

        <div className="flex items-center justify-between">
          <span className="text-gray-700">✓ Métricas verificadas</span>
          <span className="font-semibold text-gray-900">
            {quality.metricsVerified} de {quality.totalMetrics}
          </span>
        </div>

        <div className="flex items-center justify-between">
          <span className="text-gray-700">✓ Métricas dentro del scope</span>
          <span className="font-semibold text-gray-900">
            {quality.metricsInScope} de {quality.totalMetrics}
          </span>
        </div>

        <div className="flex items-center justify-between">
          <span className="text-gray-700">✓ Producción activa del alumno</span>
          <span className="font-semibold text-gray-900">{quality.studentProduction} de {quality.totalModules}</span>
        </div>

        <div className="flex items-center justify-between">
          <span className="text-gray-700">✓ Tutor Knowledge Base</span>
          <span className="font-semibold text-gray-900 capitalize">
            {quality.tutorKnowledgeBaseStatus === 'Prepared'
              ? 'Preparada'
              : quality.tutorKnowledgeBaseStatus === 'Incomplete'
                ? 'Incompleta'
                : 'Falló'}
          </span>
        </div>

        {quality.warningCount > 0 && (
          <div className="flex items-center justify-between pt-2 border-t border-gray-200 mt-2">
            <span className="text-gray-700">! Advertencias</span>
            <span className="font-semibold text-amber-600">{quality.warningCount}</span>
          </div>
        )}

        {quality.blockingWarningCount > 0 && (
          <div className="flex items-center justify-between text-red-600">
            <span>✕ Bloqueos críticos</span>
            <span className="font-semibold">{quality.blockingWarningCount}</span>
          </div>
        )}
      </div>
    </div>
  );
}
