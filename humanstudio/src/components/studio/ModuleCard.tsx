import React from 'react';
import { Module } from '../../types';
import ModuleInformation from './ModuleInformation';
import ModuleType from './ModuleType';
import MetricBadge from './MetricBadge';

interface ModuleCardProps {
  module: Module;
}

const ModuleCard: React.FC<ModuleCardProps> = ({ module }) => {
  return (
    <div className="bg-white border border-gray-200 rounded-lg p-5 mb-4 hover:shadow-md transition-shadow">
      <div className="flex items-start justify-between gap-4 mb-3">
        <ModuleInformation
          number={module.number}
          title={module.title}
          description={module.description}
        />
        <ModuleType type={module.moduleType} />
      </div>

      <div className="flex items-center justify-between mt-4 pt-3 border-t border-gray-100">
        <MetricBadge metric={module.targetMetric} />
      </div>
    </div>
  );
};

export default ModuleCard;
