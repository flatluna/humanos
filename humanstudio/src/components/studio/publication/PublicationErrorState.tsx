import { AlertCircle } from 'lucide-react';
import { PublicationTask, PublicationTaskKey } from '../../../types';

const COMPLETED_LABELS: Record<PublicationTaskKey, string> = {
  Capability: 'Capability guardada',
  Levels: 'Niveles guardados',
  Modules: 'Módulos guardados',
  Metrics: 'Métricas guardadas',
  KnowledgeChunks: 'Knowledge chunks creados',
  Embeddings: 'Embeddings generados',
};

const FAILED_LABELS: Record<PublicationTaskKey, string> = {
  Capability: 'No se pudo guardar la capability',
  Levels: 'No se pudieron guardar los niveles',
  Modules: 'No se pudieron guardar los módulos',
  Metrics: 'No se pudieron guardar las métricas',
  KnowledgeChunks: 'No se pudo crear la base de conocimiento',
  Embeddings: 'No se pudieron generar todos los embeddings',
};

interface PublicationErrorStateProps {
  tasks: PublicationTask[];
  errorMessage: string;
  isRetrying: boolean;
  onRetry: () => void;
  onGoDashboard: () => void;
}

export default function PublicationErrorState({
  tasks,
  errorMessage,
  isRetrying,
  onRetry,
  onGoDashboard,
}: PublicationErrorStateProps) {
  return (
    <div className="bg-white rounded-lg shadow p-8 border-2 border-red-200">
      <h2 className="text-xl font-bold text-red-700 mb-6 flex items-center gap-2">
        <AlertCircle className="w-6 h-6" />
        No pudimos completar la publicación
      </h2>

      <div className="space-y-2 mb-6">
        {tasks.map((task) => (
          <div key={task.key} className="flex items-center gap-2">
            {task.status === 'Completed' && (
              <>
                <span className="text-green-600 font-bold w-5 inline-block text-center">✓</span>
                <span className="text-gray-700">{COMPLETED_LABELS[task.key]}</span>
              </>
            )}
            {task.status === 'Failed' && (
              <>
                <span className="text-red-600 font-bold w-5 inline-block text-center">×</span>
                <span className="text-red-700 font-medium">{FAILED_LABELS[task.key]}</span>
              </>
            )}
            {(task.status === 'Pending' || task.status === 'Processing') && (
              <>
                <span className="text-gray-400 font-bold w-5 inline-block text-center">○</span>
                <span className="text-gray-400">{task.label}</span>
              </>
            )}
          </div>
        ))}
      </div>

      <p className="text-gray-900 font-semibold mb-2">El contenido aprobado no se perdió.</p>
      <p className="text-gray-500 text-sm mb-6">{errorMessage}</p>

      <div className="flex flex-col sm:flex-row gap-3">
        <button
          onClick={onRetry}
          disabled={isRetrying}
          className="flex-1 inline-flex items-center justify-center px-6 py-3 rounded-lg font-medium bg-blue-600 text-white hover:bg-blue-700 active:bg-blue-800 transition-all disabled:opacity-50 cursor-pointer"
        >
          {isRetrying ? 'Reintentando...' : 'Reintentar publicación'}
        </button>
        <button
          onClick={onGoDashboard}
          className="flex-1 inline-flex items-center justify-center px-6 py-3 rounded-lg font-medium border-2 border-gray-300 text-gray-700 hover:bg-gray-50 transition-all cursor-pointer"
        >
          Ir al dashboard
        </button>
      </div>
    </div>
  );
}
