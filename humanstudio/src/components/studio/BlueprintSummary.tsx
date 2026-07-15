import React from 'react';
import { Blueprint } from '../../types';

interface BlueprintSummaryProps {
  blueprint: Blueprint;
}

const BlueprintSummary: React.FC<BlueprintSummaryProps> = ({ blueprint }) => {
  const totalModules = blueprint.levels.reduce(
    (sum, level) => sum + level.modules.length,
    0
  );
  const uniqueMetrics = new Set(
    blueprint.levels.flatMap((level) =>
      level.modules.map((mod) => mod.targetMetric)
    )
  );

  return (
    <div className="grid grid-cols-3 gap-4 mb-8 md:grid-cols-3 sm:grid-cols-2">
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <div className="text-2xl font-bold text-blue-700">
          {blueprint.levels.length}
        </div>
        <p className="text-sm text-blue-600 font-medium">Niveles</p>
      </div>

      <div className="bg-green-50 border border-green-200 rounded-lg p-4">
        <div className="text-2xl font-bold text-green-700">{totalModules}</div>
        <p className="text-sm text-green-600 font-medium">Módulos</p>
      </div>

      <div className="bg-purple-50 border border-purple-200 rounded-lg p-4">
        <div className="text-2xl font-bold text-purple-700">
          {uniqueMetrics.size}
        </div>
        <p className="text-sm text-purple-600 font-medium">Métricas únicas</p>
      </div>
    </div>
  );
};

export default BlueprintSummary;
