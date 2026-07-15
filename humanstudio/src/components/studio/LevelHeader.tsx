import React from 'react';

interface LevelHeaderProps {
  levelName: string;
  description: string;
}

const LevelHeader: React.FC<LevelHeaderProps> = ({ levelName, description }) => {
  return (
    <div className="mb-6 mt-8">
      <div className="flex items-center gap-3 mb-2">
        <div className="px-4 py-2 bg-blue-100 border-2 border-blue-600 rounded-sm">
          <span className="font-semibold text-blue-900">Nivel: {levelName}</span>
        </div>
      </div>
      <p className="text-gray-600 ml-0">{description}</p>
    </div>
  );
};

export default LevelHeader;
