interface PublicationProgressProps {
  completedCount: number;
  totalCount: number;
  progress: number;
}

export default function PublicationProgress({
  completedCount,
  totalCount,
  progress,
}: PublicationProgressProps) {
  return (
    <div className="mb-6">
      <div className="w-full h-3 bg-gray-200 rounded-full overflow-hidden mb-2">
        <div
          className="h-full bg-blue-600 transition-all duration-300"
          style={{ width: `${progress}%` }}
        />
      </div>
      <p className="text-right text-sm font-semibold text-gray-700">
        Publicación completada al {progress}% ({completedCount} de {totalCount} tareas)
      </p>
    </div>
  );
}
