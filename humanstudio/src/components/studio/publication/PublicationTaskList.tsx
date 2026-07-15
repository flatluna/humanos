import { PublicationTask, PublicationTaskKey, PublicationTaskStatus } from '../../../types';

const STATUS_ICON: Record<PublicationTaskStatus, string> = {
  Pending: '○',
  Processing: '⟳',
  Completed: '✓',
  Failed: '×',
};

/** Status phrase per task, matching the grammatical gender/plurality of its label. */
const TASK_STATUS_LABELS: Record<PublicationTaskKey, Record<PublicationTaskStatus, string>> = {
  Capability: { Pending: 'Pendiente', Processing: 'Guardando', Completed: 'Guardada', Failed: 'Falló' },
  Levels: { Pending: 'Pendientes', Processing: 'Guardando', Completed: 'Guardados', Failed: 'Falló' },
  Modules: { Pending: 'Pendientes', Processing: 'Guardando', Completed: 'Guardados', Failed: 'Falló' },
  Metrics: { Pending: 'Pendientes', Processing: 'Guardando', Completed: 'Guardadas', Failed: 'Falló' },
  KnowledgeChunks: { Pending: 'Pendiente', Processing: 'Procesando', Completed: 'Preparada', Failed: 'Falló' },
  Embeddings: { Pending: 'Pendientes', Processing: 'Procesando', Completed: 'Generados', Failed: 'Falló' },
};

function colorClassFor(status: PublicationTaskStatus): string {
  switch (status) {
    case 'Completed':
      return 'text-green-600';
    case 'Processing':
      return 'text-blue-600';
    case 'Failed':
      return 'text-red-600';
    default:
      return 'text-gray-400';
  }
}

interface PublicationTaskListProps {
  tasks: PublicationTask[];
}

export default function PublicationTaskList({ tasks }: PublicationTaskListProps) {
  return (
    <div className="space-y-3 my-6">
      {tasks.map((task) => {
        const color = colorClassFor(task.status);
        return (
          <div
            key={task.key}
            className="flex items-center justify-between border-b border-gray-100 pb-3 last:border-0"
          >
            <div className="flex items-center gap-3">
              <span
                className={`text-lg font-bold w-5 inline-block text-center ${color} ${
                  task.status === 'Processing' ? 'animate-spin' : ''
                }`}
              >
                {STATUS_ICON[task.status]}
              </span>
              <span className="text-gray-900 font-medium">{task.label}</span>
            </div>
            <span className={`text-sm font-semibold ${color}`}>
              {TASK_STATUS_LABELS[task.key][task.status]}
            </span>
          </div>
        );
      })}
    </div>
  );
}
