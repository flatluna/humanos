import { FinalReviewPackage } from '../../../types';

export interface ValidationTabProps {
  package: FinalReviewPackage;
}

export function ValidationTab({ package: pkg }: ValidationTabProps) {
  const allModules = pkg.levels.flatMap((l) => l.modules);
  const verifiedModules = allModules.filter((m) => m.verification.status === 'Verified').length;
  const warningModules = allModules.filter((m) => m.verification.status === 'Warning').length;
  const failedModules = allModules.filter((m) => m.verification.status === 'Failed').length;

  // P principles analysis
  const principleStats = {
    P1: { pass: 0, warning: 0, fail: 0 },
    P2: { pass: 0, warning: 0, fail: 0 },
    P3: { pass: 0, warning: 0, fail: 0 },
    P4: { pass: 0, warning: 0, fail: 0 },
    P5: { pass: 0, warning: 0, fail: 0 },
    P6: { pass: 0, warning: 0, fail: 0 },
    P7: { pass: 0, warning: 0, fail: 0 },
  };

  allModules.forEach((module) => {
    module.verification.principles.forEach((principle) => {
      const principleKey = principle.principle as keyof typeof principleStats;
      if (principle.status === 'Pass') {
        principleStats[principleKey].pass += 1;
      } else if (principle.status === 'Warning') {
        principleStats[principleKey].warning += 1;
      } else if (principle.status === 'Fail') {
        principleStats[principleKey].fail += 1;
      }
    });
  });

  return (
    <div className="space-y-6">
      {/* Overall summary */}
      <div className="bg-gray-50 border border-gray-200 rounded-lg p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Resumen de validación</h3>

        <div className="grid grid-cols-2 gap-4 mb-4">
          <div>
            <p className="text-sm text-gray-600">Módulos verificados</p>
            <p className="text-2xl font-bold text-green-600">
              {verifiedModules}/{allModules.length}
            </p>
          </div>

          <div>
            <p className="text-sm text-gray-600">Módulos con advertencias</p>
            <p className="text-2xl font-bold text-amber-600">{warningModules}</p>
          </div>

          <div>
            <p className="text-sm text-gray-600">Módulos con fallos</p>
            <p className="text-2xl font-bold text-red-600">{failedModules}</p>
          </div>

          <div>
            <p className="text-sm text-gray-600">Métricas en scope</p>
            <p className="text-2xl font-bold text-blue-600">{pkg.quality.metricsInScope}</p>
          </div>
        </div>

        <p className="text-sm text-gray-700">
          Producción del alumno: {pkg.quality.studentProduction}/{pkg.quality.totalModules}
        </p>
      </div>

      {/* Principles breakdown */}
      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Desglose de principios</h3>

        <div className="space-y-2">
          {Object.entries(principleStats).map(([principle, stats]) => {
            const total = stats.pass + stats.warning + stats.fail;
            const passPercent = total > 0 ? (stats.pass / total) * 100 : 0;

            return (
              <div key={principle}>
                <div className="flex items-center justify-between mb-1">
                  <span className="font-semibold text-gray-900">{principle}</span>
                  <span className="text-sm text-gray-600">
                    {stats.pass}/{total}
                    {stats.warning > 0 && ` (! ${stats.warning})`}
                    {stats.fail > 0 && ` (× ${stats.fail})`}
                  </span>
                </div>
                <div className="h-2 bg-gray-200 rounded-full overflow-hidden">
                  <div
                    className={`h-full ${
                      stats.fail > 0
                        ? 'bg-red-500'
                        : stats.warning > 0
                          ? 'bg-amber-500'
                          : 'bg-green-500'
                    }`}
                    style={{ width: `${passPercent}%` }}
                  />
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Blocking warnings */}
      {pkg.quality.blockingWarningCount > 0 && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-4">
          <p className="text-red-900 font-semibold">
            ✕ Advertencias bloqueantes: {pkg.quality.blockingWarningCount}
          </p>
          <p className="text-red-800 text-sm mt-1">
            Debe regenerar o modificar el contenido antes de publicar.
          </p>
        </div>
      )}
    </div>
  );
}
