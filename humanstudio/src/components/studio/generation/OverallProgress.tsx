interface OverallProgressProps {
  verifiedModules: number;
  totalModules: number;
}

export default function OverallProgress({
  verifiedModules,
  totalModules,
}: OverallProgressProps) {
  const percentage = Math.round((verifiedModules / totalModules) * 100);

  return (
    <div className="bg-white rounded-lg shadow p-6 mb-6">
      <div className="mb-4">
        <p className="text-lg font-semibold text-gray-900">
          {verifiedModules} de {totalModules} módulos completados
        </p>
      </div>

      {/* Progress Bar */}
      <div className="mb-3">
        <div className="w-full h-3 bg-gray-200 rounded-full overflow-hidden">
          <div
            className="h-full bg-green-600 transition-all duration-300"
            style={{ width: `${percentage}%` }}
          ></div>
        </div>
      </div>

      {/* Percentage */}
      <p className="text-right text-sm font-bold text-gray-700">{percentage}%</p>
    </div>
  );
}
