import React from 'react';

interface ModuleInformationProps {
  number: number;
  title: string;
  description: string;
}

const ModuleInformation: React.FC<ModuleInformationProps> = ({
  number,
  title,
  description,
}) => {
  return (
    <div className="flex-1">
      <h4 className="font-semibold text-gray-900 mb-1">
        {number}. {title}
      </h4>
      <p className="text-sm text-gray-600">{description}</p>
    </div>
  );
};

export default ModuleInformation;
