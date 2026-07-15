import { useParams } from 'react-router-dom';
import { Loader } from 'lucide-react';

export function GenerationPage() {
  const { runId } = useParams<{ runId: string }>();

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-purple-50 py-12 px-4">
      <div className="max-w-2xl mx-auto">
        {/* Header */}
        <div className="mb-12">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">
            Studio — Generación del curso
          </h1>
          <p className="text-gray-600">
            Tu blueprint ha sido aprobado. Estamos generando el contenido del
            curso.
          </p>
        </div>

        {/* Generation Status Card */}
        <div className="bg-white rounded-lg shadow-lg p-8 border-2 border-blue-200">
          <div className="flex flex-col items-center justify-center gap-8">
            {/* Spinner */}
            <div className="relative">
              <div className="w-16 h-16 border-4 border-blue-100 rounded-full"></div>
              <div className="absolute top-0 left-0 w-16 h-16 border-4 border-transparent border-t-blue-600 rounded-full animate-spin"></div>
            </div>

            {/* Status Message */}
            <div className="text-center">
              <p className="text-lg font-semibold text-gray-900 mb-2">
                Generando 12 módulos...
              </p>
              <p className="text-sm text-gray-600 mb-6">
                Este proceso puede tomar unos minutos. No cierres esta ventana.
              </p>

              {/* Run ID Display */}
              <div className="bg-gray-50 rounded p-4 border border-gray-200">
                <p className="text-xs text-gray-500 mb-1">ID de ejecución</p>
                <p className="font-mono text-sm font-medium text-gray-800">
                  {runId || 'N/A'}
                </p>
              </div>
            </div>

            {/* Progress Details */}
            <div className="w-full bg-blue-50 rounded p-4 border border-blue-200">
              <ul className="space-y-3 text-sm text-gray-700">
                <li className="flex items-center gap-3">
                  <span className="text-blue-600">✓</span>
                  <span>Blueprint aprobado</span>
                </li>
                <li className="flex items-center gap-3">
                  <Loader size={16} className="text-blue-600 animate-spin" />
                  <span>Inicializando módulos...</span>
                </li>
                <li className="flex items-center gap-3">
                  <span className="text-gray-400">○</span>
                  <span className="text-gray-400">Procesando contenido</span>
                </li>
                <li className="flex items-center gap-3">
                  <span className="text-gray-400">○</span>
                  <span className="text-gray-400">GATE 2: revisión final</span>
                </li>
              </ul>
            </div>
          </div>
        </div>

        {/* Info Footer */}
        <div className="mt-8 p-4 bg-purple-50 rounded border border-purple-200">
          <p className="text-xs text-purple-700">
            <strong>Nota:</strong> Este es un proceso asincrónico. El Instructor
            está preparando cada módulo según tu blueprint.
          </p>
        </div>
      </div>
    </div>
  );
}
