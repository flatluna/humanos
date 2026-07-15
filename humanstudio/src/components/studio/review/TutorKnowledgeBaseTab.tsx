import { FinalReviewPackage } from '../../../types';

export interface TutorKnowledgeBaseTabProps {
  package: FinalReviewPackage;
}

export function TutorKnowledgeBaseTab({ package: pkg }: TutorKnowledgeBaseTabProps) {
  const { tutorKnowledgeBase: kb } = pkg;

  return (
    <div className="space-y-6">
      {/* Status */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <p className="text-blue-900 font-semibold">
          Estado:{' '}
          <span
            className={
              kb.status === 'Prepared'
                ? 'text-green-600'
                : kb.status === 'Incomplete'
                  ? 'text-amber-600'
                  : 'text-red-600'
            }
          >
            {kb.status === 'Prepared' ? 'Preparada' : kb.status === 'Incomplete' ? 'Incompleta' : 'Falló'}
          </span>
        </p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-3 gap-4">
        <div className="bg-white border border-gray-200 rounded-lg p-4">
          <p className="text-sm text-gray-600">Fuentes</p>
          <p className="text-2xl font-bold text-gray-900">{kb.sourceCount}</p>
        </div>

        <div className="bg-white border border-gray-200 rounded-lg p-4">
          <p className="text-sm text-gray-600">Módulos</p>
          <p className="text-2xl font-bold text-gray-900">{kb.moduleCount}</p>
        </div>

        <div className="bg-white border border-gray-200 rounded-lg p-4">
          <p className="text-sm text-gray-600">Secciones</p>
          <p className="text-2xl font-bold text-gray-900">{kb.sectionCount}</p>
        </div>
      </div>

      {/* Content preview */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-3">Vista previa del contenido</h3>
        <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
          <pre className="text-sm text-gray-700 whitespace-pre-wrap font-mono">
            {kb.content}
          </pre>
        </div>
      </div>

      {/* Tutor boundaries */}
      <div className="bg-white border border-gray-200 rounded-lg p-4">
        <h3 className="text-lg font-semibold text-gray-900 mb-3">Límites del tutor</h3>

        <div className="space-y-3">
          <div>
            <p className="font-semibold text-green-600 mb-2">El tutor PUEDE:</p>
            <ul className="list-disc list-inside text-sm text-gray-700 space-y-1">
              <li>Explicar conceptos del corpus</li>
              <li>Guiar procedimientos de módulos</li>
              <li>Proporcionar ejemplos del experto</li>
              <li>Revisar contenido generado y verificado</li>
              <li>Ofrecer retroalimentación estructurada</li>
            </ul>
          </div>

          <div>
            <p className="font-semibold text-red-600 mb-2">El tutor NO DEBE:</p>
            <ul className="list-disc list-inside text-sm text-gray-700 space-y-1">
              <li>Presentar hechos fuera del corpus</li>
              <li>Resolver actividades por el alumno</li>
              <li>Entregar respuestas antes de que el alumno produzca</li>
              <li>Afirmar dominio sin evidencia</li>
              <li>Salir del alcance del módulo</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
