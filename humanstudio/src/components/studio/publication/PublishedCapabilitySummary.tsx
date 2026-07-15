interface PublishedCapabilitySummaryProps {
  title: string;
  levelCount: number;
  moduleCount: number;
  metricCount: number;
}

export default function PublishedCapabilitySummary({
  title,
  levelCount,
  moduleCount,
  metricCount,
}: PublishedCapabilitySummaryProps) {
  return (
    <div className="mb-6">
      <h3 className="text-xl font-bold text-gray-900 mb-1">{title}</h3>
      <p className="text-gray-600 mb-2">
        {levelCount} niveles · {moduleCount} módulos · {metricCount} métricas
      </p>
      <p className="text-green-700 font-semibold mb-4">Publicada correctamente</p>
      <p className="text-gray-600 text-sm">La base de conocimiento del tutor está preparada.</p>
    </div>
  );
}
