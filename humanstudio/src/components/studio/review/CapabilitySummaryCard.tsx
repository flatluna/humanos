import { FinalReviewPackage } from '../../../types';

export interface CapabilitySummaryCardProps {
  package: FinalReviewPackage;
}

export function CapabilitySummaryCard({ package: pkg }: CapabilitySummaryCardProps) {
  const totalModules = pkg.levels.reduce((acc, level) => acc + level.modules.length, 0);
  const metricSet = new Set(
    pkg.levels.flatMap((level) => level.modules.map((m) => m.targetMetric))
  );

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
      <div className="flex items-start justify-between mb-4">
        <div>
          <h3 className="text-2xl font-bold text-gray-900">{pkg.title}</h3>
          <p className="text-gray-600 mt-1">{pkg.description}</p>
        </div>
        <div className="text-right">
          <div className="text-sm text-gray-500">
            {pkg.levels.length} niveles · {totalModules} módulos · {metricSet.size} métricas
          </div>
          <div className="text-sm font-semibold text-blue-600 mt-1">
            Intensidad: {pkg.intensity === 'modest' ? 'Modesto' : pkg.intensity === 'serious' ? 'Serio' : 'Transformativo'}
          </div>
        </div>
      </div>

      <div className="border-t border-gray-200 pt-4 mt-4">
        <div className="mb-3">
          <label className="text-sm font-semibold text-gray-700">Objetivo original</label>
          <p className="text-gray-600 mt-1">{pkg.objective}</p>
        </div>

        <div>
          <label className="text-sm font-semibold text-gray-700">Scope aprobado</label>
          <p className="text-gray-600 mt-1">{pkg.scopeDeclaration}</p>
        </div>
      </div>

      <div className="bg-blue-50 border border-blue-200 rounded p-3 mt-4">
        <p className="text-sm text-blue-800">
          <strong>Estado:</strong> Esperando aprobación final
        </p>
      </div>
    </div>
  );
}
