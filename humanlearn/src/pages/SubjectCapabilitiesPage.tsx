import { Link, useParams } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { getCapabilities, BackendCapabilitySummary } from '../lib/api/capabilitiesApi';
import { useI18n } from '../i18n';

/**
 * Subject → Capabilities — Paso 3. Conectado a GET /capabilities real.
 * NOTA: el backend todavía no tiene el campo SubjectId (Subject es un
 * stub local, ver subjectsApi.ts) así que por ahora se muestran TODAS las
 * capabilities activas sin filtrar por subject — filtrar de verdad requiere
 * agregar SubjectId al backend (pendiente, ver
 * /memories/repo/student-graph-ui-redesign-final-design.md).
 */
export default function SubjectCapabilitiesPage() {
  const { subjectCode } = useParams();
  const { t } = useI18n();
  const [capabilities, setCapabilities] = useState<BackendCapabilitySummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getCapabilities()
      .then(setCapabilities)
      .catch((err) => setError(err instanceof Error ? err.message : 'Error al cargar capabilities.'))
      .finally(() => setIsLoading(false));
  }, []);

  return (
    <div className="mx-auto max-w-3xl p-8">
      <Link to="/" className="text-sm text-slate-500 hover:underline">
        ← {t.backToSubjects}
      </Link>
      <h1 className="mt-2 text-2xl font-semibold capitalize">{subjectCode}</h1>
      <p className="mb-6 mt-1 text-sm text-slate-400">
        (Todas las capabilities activas — filtrado real por subject pendiente)
      </p>

      {isLoading && <p className="text-slate-500">Cargando...</p>}
      {error && <p className="text-red-500">{error}</p>}

      <div className="flex flex-col gap-3">
        {capabilities.map((capability) => (
          <Link
            key={capability.CapabilityId}
            to={`/capabilities/${capability.CapabilityId}`}
            className="rounded-lg border border-slate-200 p-4 transition hover:border-slate-400 hover:shadow-sm"
          >
            <div className="font-medium">{capability.Name}</div>
            {capability.Description && (
              <div className="mt-1 text-sm text-slate-500">{capability.Description}</div>
            )}
          </Link>
        ))}
      </div>
    </div>
  );
}
