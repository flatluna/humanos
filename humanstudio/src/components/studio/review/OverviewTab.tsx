import { FinalReviewPackage, CapabilityMetric } from '../../../types';

export interface OverviewTabProps {
  package: FinalReviewPackage;
}

export function OverviewTab({ package: pkg }: OverviewTabProps) {
  const metricsMap = new Map<CapabilityMetric, number>();
  pkg.levels.forEach((level) => {
    level.modules.forEach((module) => {
      metricsMap.set(module.targetMetric, (metricsMap.get(module.targetMetric) || 0) + 1);
    });
  });

  return (
    <div className="space-y-6">
      {/* Objective */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Objetivo original</h3>
        <p className="text-gray-700">{pkg.objective}</p>
      </div>

      {/* Scope */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Scope aprobado</h3>
        <p className="text-gray-700">{pkg.scopeDeclaration}</p>
      </div>

      {/* Transformation */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Transformación esperada</h3>
        <div className="space-y-2">
          {pkg.levels.map((level) => (
            <div key={level.id} className="text-sm">
              <span className="font-semibold text-blue-600">{level.level}:</span>
              <span className="text-gray-700 ml-2">{level.transformation}</span>
            </div>
          ))}
        </div>
      </div>

      {/* Levels included */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Niveles incluidos</h3>
        <div className="flex flex-wrap gap-2">
          {pkg.levels.map((level) => (
            <span
              key={level.id}
              className="inline-block bg-blue-100 text-blue-800 px-3 py-1 rounded-full text-sm font-semibold"
            >
              {level.level}
            </span>
          ))}
        </div>
      </div>

      {/* Module count */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Módulos por nivel</h3>
        <div className="space-y-1">
          {pkg.levels.map((level) => (
            <div key={level.id} className="text-sm text-gray-700">
              <strong>{level.level}:</strong> {level.modules.length} módulos
            </div>
          ))}
        </div>
      </div>

      {/* Metrics distribution */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Distribución de métricas</h3>
        <div className="grid grid-cols-2 gap-2">
          {Array.from(metricsMap.entries()).map(([metric, count]) => (
            <div key={metric} className="text-sm text-gray-700">
              <strong>{metric}:</strong> {count}
            </div>
          ))}
        </div>
      </div>

      {/* General warnings */}
      {pkg.warnings.length > 0 && (
        <div>
          <h3 className="text-lg font-semibold text-gray-900 mb-2">Advertencias generales</h3>
          <div className="space-y-2">
            {pkg.warnings.map((warning) => (
              <div
                key={warning.id}
                className="text-sm p-2 bg-amber-50 border border-amber-200 rounded text-amber-900"
              >
                <strong>! {warning.title}:</strong> {warning.description}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Tutor KB */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Preparación de base del tutor</h3>
        <p className="text-sm text-gray-700">
          <strong>Estado:</strong>{' '}
          <span className="text-green-600 font-semibold">
            {pkg.tutorKnowledgeBase.status === 'Prepared'
              ? 'Preparada'
              : pkg.tutorKnowledgeBase.status === 'Incomplete'
                ? 'Incompleta'
                : 'Falló'}
          </span>
        </p>
      </div>
    </div>
  );
}
