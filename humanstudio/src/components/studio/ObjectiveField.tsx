import React from 'react';

interface ObjectiveFieldProps {
  value: string;
  onChange: (value: string) => void;
  error?: string;
}

const ObjectiveField: React.FC<ObjectiveFieldProps> = ({
  value,
  onChange,
  error,
}) => {
  const minLength = 20;
  const isValid = value.length >= minLength;

  return (
    <div className="mb-8">
      <label className="block text-lg font-semibold text-gray-900 mb-2">
        ¿Qué quieres dominar?
      </label>
      <textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="Ejemplo: Quiero aprender a diagnosticar conflictos de equipo y elegir una intervención adecuada sin depender de la IA."
        className={`w-full h-32 p-4 border-2 rounded-lg font-sans resize-none transition-colors focus:outline-none ${
          error
            ? 'border-red-500 focus:border-red-600 bg-red-50'
            : isValid
            ? 'border-green-500 focus:border-green-600 bg-white'
            : 'border-gray-300 focus:border-purple-600 bg-white'
        }`}
      />
      <div className="mt-2 flex items-center justify-between">
        <span className="text-sm text-gray-500">
          {value.length} / {minLength} caracteres mínimo
        </span>
        {isValid && !error && (
          <span className="text-sm text-green-600 font-medium">✓ Válido</span>
        )}
      </div>
      {error && (
        <p className="mt-2 text-sm text-red-600 font-medium">{error}</p>
      )}
    </div>
  );
};

export default ObjectiveField;
