import React from 'react';

interface ModuleTypeProps {
  type: string;
}

const ModuleType: React.FC<ModuleTypeProps> = ({ type }) => {
  return (
    <div className="px-3 py-1 bg-gray-200 rounded-full text-xs font-medium text-gray-700 whitespace-nowrap">
      {type}
    </div>
  );
};

export default ModuleType;
