import React from 'react';
import { ScopeDeclaration } from '../../types';

interface ScopeDeclarationCardProps {
  scopeDeclaration: ScopeDeclaration;
}

const ScopeDeclarationCard: React.FC<ScopeDeclarationCardProps> = ({
  scopeDeclaration,
}) => {
  const intensityLabels = {
    modest: 'Modesto',
    serious: 'Serio',
    transformative: 'Transformador',
  };

  return (
    <div className="bg-gradient-to-r from-purple-50 to-purple-100 border-2 border-purple-200 rounded-lg p-6 mb-8">
      <h2 className="text-xl font-bold text-purple-900 mb-4">
        Alcance de la capability
      </h2>

      <div className="space-y-4">
        <div>
          <label className="block text-sm font-semibold text-purple-800 mb-1">
            Objetivo
          </label>
          <p className="text-purple-900">{scopeDeclaration.objective}</p>
        </div>

        <div>
          <label className="block text-sm font-semibold text-purple-800 mb-1">
            Intensidad
          </label>
          <p className="text-purple-900">
            {
              intensityLabels[
                scopeDeclaration.intensity as keyof typeof intensityLabels
              ]
            }
          </p>
        </div>

        <div>
          <label className="block text-sm font-semibold text-purple-800 mb-1">
            Descripción
          </label>
          <p className="text-purple-900 text-sm">{scopeDeclaration.description}</p>
        </div>
      </div>
    </div>
  );
};

export default ScopeDeclarationCard;
