import React from 'react';
import { EvolutionProgress } from '../../types';
import LevelBadge from './LevelBadge';
import LevelProgressBar from './LevelProgressBar';

interface CurrentLevelCardProps {
  evolutionProgress: EvolutionProgress;
}

const CurrentLevelCard: React.FC<CurrentLevelCardProps> = ({
  evolutionProgress,
}) => {
  return (
    <div className="bg-white rounded-lg shadow p-6 mb-8">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Tu nivel actual</h2>
      
      <div className="mb-6">
        <LevelBadge level={evolutionProgress.currentLevel} />
      </div>

      <LevelProgressBar
        percentage={evolutionProgress.percentage}
        nextLevel={evolutionProgress.nextLevel}
      />
    </div>
  );
};

export default CurrentLevelCard;
