import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  getCapabilityContent,
  BackendCapabilityContent,
  BackendCapabilityContentModule,
} from '../lib/api/capabilityContentApi';

/**
 * Read-only view of a REAL published capability's full generated content
 * (levels, modules, instructor scripts, assigned metrics) — fetched live
 * from the backend (GET /capabilities/{id}/content). This is distinct
 * from "Edit in Studio" (Capability Library card click / ⋮ Edit, which
 * always opens the Studio authoring flow, per the Paso 12 design): this
 * screen exists so a designer can actually READ what was generated, which
 * the card-click-opens-Studio flow alone doesn't provide.
 */
export function CapabilityDetailPage() {
  const { capabilityId } = useParams<{ capabilityId: string }>();
  const navigate = useNavigate();

  const [content, setContent] = useState<BackendCapabilityContent | null | undefined>(undefined);
  const [error, setError] = useState<string | null>(null);
  const [selectedModule, setSelectedModule] = useState<BackendCapabilityContentModule | null>(null);

  useEffect(() => {
    if (!capabilityId) return;

    getCapabilityContent(capabilityId)
      .then((result) => {
        setContent(result);
        setSelectedModule(result.Levels[0]?.Modules[0] ?? null);
      })
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'No se pudo cargar el contenido.');
        setContent(null);
      });
  }, [capabilityId]);

  if (content === undefined) {
    return (
      <div className="max-w-5xl mx-auto py-12 text-center">
        <p className="text-gray-600">Cargando contenido...</p>
      </div>
    );
  }

  if (!content) {
    return (
      <div className="max-w-5xl mx-auto py-12 text-center">
        <p className="text-red-600 font-semibold mb-2">
          {error ?? 'Esta capability no tiene contenido real publicado todavía.'}
        </p>
        <p className="text-gray-500 text-sm mb-6">
          Esto ocurre para las capabilities de demostración (mock) que no existen en la base de datos real.
        </p>
        <button
          onClick={() => navigate('/capabilities')}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-semibold"
        >
          ← Volver a Capability Library
        </button>
      </div>
    );
  }

  const totalModules = content.Levels.reduce((acc, l) => acc + l.Modules.length, 0);

  return (
    <div className="max-w-6xl mx-auto">
      <div className="mb-6">
        <button
          onClick={() => navigate('/capabilities')}
          className="text-sm text-blue-600 hover:underline font-medium mb-3"
        >
          ← Capability Library
        </button>
        <h1 className="text-3xl font-bold text-gray-900">{content.Name}</h1>
        <p className="text-gray-600 mt-1">{content.Description}</p>
        <p className="text-sm text-gray-500 mt-2">
          {content.Levels.length} niveles · {totalModules} módulos
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {/* Sidebar: levels + modules */}
        <div className="md:col-span-1 space-y-6">
          {content.Levels.map((level) => (
            <div key={level.CapabilityLevelId}>
              <div className="px-3 py-1.5 bg-blue-100 border-2 border-blue-600 rounded-sm inline-block mb-2">
                <span className="text-sm font-bold text-blue-900">🟦 {level.Layer}</span>
              </div>
              <div className="space-y-1">
                {level.Modules.map((module) => (
                  <button
                    key={module.CapabilityModuleId}
                    onClick={() => setSelectedModule(module)}
                    className={`w-full text-left px-3 py-2 rounded-lg text-sm transition-colors ${
                      selectedModule?.CapabilityModuleId === module.CapabilityModuleId
                        ? 'bg-blue-50 text-blue-900 font-semibold border border-blue-300'
                        : 'text-gray-700 hover:bg-gray-50'
                    }`}
                  >
                    {module.SortOrder + 1}. {module.Title}
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>

        {/* Detail: selected module */}
        <div className="md:col-span-2">
          {selectedModule ? (
            <div className="bg-white rounded-lg shadow p-6">
              <h2 className="text-xl font-bold text-gray-900 mb-1">
                Módulo {selectedModule.SortOrder + 1}: {selectedModule.Title}
              </h2>
              <p className="text-gray-600 mb-3">{selectedModule.Description}</p>

              <div className="flex flex-wrap gap-2 mb-4">
                <span className="px-3 py-1 bg-gray-200 rounded-full text-xs font-medium text-gray-700">
                  {selectedModule.Type}
                </span>
                {selectedModule.Metrics.map((metric) => (
                  <span
                    key={metric}
                    className="px-3 py-1 bg-green-100 border border-green-600 rounded-full text-xs font-semibold text-green-900"
                  >
                    🟩 {metric}
                  </span>
                ))}
              </div>

              <p className="text-sm text-gray-600 italic mb-4">{selectedModule.MetricRationale}</p>

              <h3 className="text-sm font-bold text-gray-900 mb-2">Guion del Instructor</h3>
              <pre className="whitespace-pre-wrap text-sm text-gray-700 bg-gray-50 border border-gray-200 rounded-lg p-4 max-h-[32rem] overflow-y-auto">
                {selectedModule.Script}
              </pre>
            </div>
          ) : (
            <p className="text-gray-500">Selecciona un módulo para ver su contenido.</p>
          )}
        </div>
      </div>
    </div>
  );
}
