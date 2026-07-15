import React from 'react';

interface LevelProgressBarProps {
  percentage: number;
  nextLevel: string;
}

const LevelProgressBar: React.FC<LevelProgressBarProps> = ({
  percentage,
  nextLevel,
}) => {
  return (
    <div className="space-y-2">
      <div className="flex justify-between items-center">
        <span className="text-sm text-gray-600">Progreso hacia el siguiente nivel</span>
        <span className="text-sm font-medium text-gray-900">{percentage}%</span>
      </div>
      <div className="w-full bg-gray-200 rounded-full h-3 overflow-hidden">
        <div
          className="bg-blue-600 h-full rounded-full transition-all duration-500"
          style={{ width: `${percentage}%` }}
        />
      </div>
      <p className="text-sm text-gray-600">
        Hacia <span className="font-semibold">{nextLevel}</span>
      </p>
    </div>
  );
};

export default LevelProgressBar;
