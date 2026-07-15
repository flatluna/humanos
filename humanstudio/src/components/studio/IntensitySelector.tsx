import React from 'react';
import { Intensity } from '../../types';

interface IntensitySelectorProps {
  value: Intensity;
  onChange: (value: Intensity) => void;
}

const intensityOptions: { value: Intensity; label: string; description: string }[] = [
  {
    value: 'modest',
    label: 'Modesto',
    description: 'Una introducción o repaso con pocos módulos.',
  },
  {
    value: 'serious',
    label: 'Serio',
    description: 'Desarrollo estructurado con práctica y dominio demostrado.',
  },
  {
    value: 'transformative',
    label: 'Transformador',
    description: 'Desarrollo profundo orientado a autonomía y aplicación real.',
  },
];

const IntensitySelector: React.FC<IntensitySelectorProps> = ({
  value,
  onChange,
}) => {
  return (
    <div className="mb-8">
      <label className="block text-lg font-semibold text-gray-900 mb-4">
        Intensidad
      </label>
      <div className="space-y-3">
        {intensityOptions.map((option) => (
          <div key={option.value} className="flex items-start gap-3">
            <input
              type="radio"
              id={`intensity-${option.value}`}
              name="intensity"
              value={option.value}
              checked={value === option.value}
              onChange={(e) => onChange(e.target.value as Intensity)}
              className="mt-1 w-4 h-4 text-purple-600 cursor-pointer"
            />
            <label
              htmlFor={`intensity-${option.value}`}
              className="flex-1 cursor-pointer"
            >
              <div className="font-medium text-gray-900">{option.label}</div>
              <p className="text-sm text-gray-600">{option.description}</p>
            </label>
          </div>
        ))}
      </div>
    </div>
  );
};

export default IntensitySelector;
