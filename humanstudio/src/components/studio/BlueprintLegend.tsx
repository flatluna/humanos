import React from 'react';

const BlueprintLegend: React.FC = () => {
  return (
    <div className="bg-gray-50 border border-gray-200 rounded-lg p-6 mb-8 mt-8">
      <h3 className="text-lg font-bold text-gray-900 mb-4">
        Leyenda — Cómo leer el blueprint
      </h3>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Nivel - Blue Rectangle */}
        <div className="flex gap-4">
          <div className="flex-shrink-0">
            <div className="flex items-center justify-center px-3 py-1.5 bg-blue-100 border-2 border-blue-600 rounded-sm">
              <span className="font-semibold text-blue-900 text-sm">Nivel</span>
            </div>
          </div>
          <div>
            <p className="font-semibold text-gray-900 mb-1">🟦 Azul + Rectángulo</p>
            <p className="text-sm text-gray-600">
              Representa la <strong>jerarquía estructural</strong> del aprendizaje
              (Foundation, Exploration, Mastery, etc.). Es la progresión del
              estudiante a través del curso.
            </p>
          </div>
        </div>

        {/* Métrica - Green Pill */}
        <div className="flex gap-4">
          <div className="flex-shrink-0">
            <span className="inline-flex items-center px-3 py-1 bg-green-100 border-2 border-green-600 rounded-full text-sm">
              <span className="font-semibold text-green-900">Métrica</span>
            </span>
          </div>
          <div>
            <p className="font-semibold text-gray-900 mb-1">🟩 Verde + Píldora</p>
            <p className="text-sm text-gray-600">
              Representa el <strong>objetivo específico</strong> del módulo
              (Knowledge, Fluency, Independence, etc.). Es lo que el estudiante
              debe demostrar al completar el módulo.
            </p>
          </div>
        </div>
      </div>

      <div className="mt-4 p-3 bg-yellow-50 border border-yellow-200 rounded text-sm text-gray-700">
        <strong>💡 Recuerda:</strong> El color nunca es el único diferenciador.
        Cada nivel tiene forma rectangular y dice "Nivel:"; cada métrica tiene forma de
        píldora y dice "Métrica:".
      </div>
    </div>
  );
};

export default BlueprintLegend;
