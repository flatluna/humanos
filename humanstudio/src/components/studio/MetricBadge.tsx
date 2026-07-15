import React from 'react';

interface MetricBadgeProps {
  metric: string;
}

const MetricBadge: React.FC<MetricBadgeProps> = ({ metric }) => {
  return (
    <span className="inline-flex items-center px-4 py-1.5 bg-green-100 border-2 border-green-600 rounded-full">
      <span className="font-semibold text-green-900 text-sm">
        Métrica: {metric}
      </span>
    </span>
  );
};

export default MetricBadge;
