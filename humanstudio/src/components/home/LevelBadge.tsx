import React from 'react';
import { HumanEvolutionLevel } from '../../types';

interface LevelBadgeProps {
  level: HumanEvolutionLevel;
}

const LevelBadge: React.FC<LevelBadgeProps> = ({ level }) => {
  return (
    <span className="inline-flex items-center gap-2 px-3 py-1 bg-blue-100 text-blue-700 rounded-full text-sm font-medium">
      <span>🟦</span>
      <span>Nivel: {level}</span>
    </span>
  );
};

export default LevelBadge;
