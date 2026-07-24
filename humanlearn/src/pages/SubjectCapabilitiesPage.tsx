import { Link, useParams } from 'react-router-dom';
import { useEffect, useState } from 'react';
import {
  getCapabilities,
  getCapabilityCoverImageUrl,
  BackendCapabilitySummary,
} from '../lib/api/capabilitiesApi';
import { getCapabilityGraph, type BackendCapabilityGraph } from '../lib/api/capabilityGraphApi';
import { MOCK_USER } from '../components/layout/AppShell';
import { useI18n } from '../i18n';
import { BookOpen, Info, Layers, Sparkles, Star, X } from 'lucide-react';

/**
 * Subject → Capabilities — Paso 3. Conectado a GET /capabilities real,
 * filtrado server-side por Subject (Capability.SubjectId, ver
 * /memories/repo/student-graph-ui-redesign-final-design.md).
 *
 * Card redesign (2026-07-21, Coursera-style): cover image (real, generated
 * once per capability and stored in Data Lake — see
 * GET /capabilities/{id}/cover-image — falling back to a colorful gradient
 * placeholder while it's missing/loading), a "total nodos" stat pill, and
 * a "Ver resumen" button that lazily fetches the course's ExecutiveSummary/
 * KeyEntities (GET /capabilities/{id}/graph) into a modal, without
 * navigating away from the subject grid.
 */

/** Deterministic color accent per capability (no design-time color field on
 * the backend yet) — same capability always gets the same gradient. */
const ACCENT_GRADIENTS = [
  'from-indigo-500 to-blue-600',
  'from-emerald-500 to-teal-600',
  'from-rose-500 to-pink-600',
  'from-amber-500 to-orange-600',
  'from-violet-500 to-purple-600',
  'from-sky-500 to-cyan-600',
];

function accentFor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return ACCENT_GRADIENTS[hash % ACCENT_GRADIENTS.length];
}

/** PLACEHOLDER rating (no real stats/evaluation feature yet — TODO: replace
 * with an actual average once course ratings/evaluations are implemented).
 * Deterministic per capability (4.3–5.0) so it doesn't jump around on
 * re-render, purely cosmetic in the meantime. */
function placeholderRatingFor(id: string): number {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 17 + id.charCodeAt(i)) >>> 0;
  return 4.3 + (hash % 8) / 10;
}

function StarRating({ rating }: { rating: number }) {
  return (
    <div className="flex items-center gap-1" title="Calificación (placeholder)">
      <Star className="h-3.5 w-3.5 fill-amber-400 text-amber-400" />
      <span className="text-xs font-semibold text-slate-300">{rating.toFixed(1)}</span>
    </div>
  );
}

/** "Provider" byline shown above the title, mirroring Coursera's
 * university/company logo row. App/company name isn't finalized yet, so
 * this reuses the same "HO" gradient badge + "Human OS" wordmark as the
 * TopBar, with a fixed caption crediting the AI generation pipeline
 * (TODO: revisit once the product/company name and branding are final). */
function ProviderBadge() {
  return (
    <div className="flex items-center gap-1.5">
      <div className="flex h-5 w-5 flex-none items-center justify-center rounded bg-gradient-to-br from-brand-500 to-accent-500">
        <span className="text-[9px] font-bold text-[#fff]">HO</span>
      </div>
      <span className="truncate text-xs font-medium text-slate-500">
        Human OS · Agente de IA Experto
      </span>
    </div>
  );
}

function CoverImage({ capability }: { capability: BackendCapabilitySummary }) {
  const [failed, setFailed] = useState(false);
  const accent = accentFor(capability.CapabilityId);
  const showPlaceholder = !capability.HasCoverImage || failed;

  return (
    <div className={`relative h-36 w-full overflow-hidden bg-gradient-to-br ${accent}`}>
      {!showPlaceholder && (
        <img
          src={getCapabilityCoverImageUrl(capability.CapabilityId)}
          alt={capability.Name}
          className="h-full w-full object-cover"
          onError={() => setFailed(true)}
        />
      )}
      {showPlaceholder && (
        <div className="flex h-full w-full items-center justify-center">
          <BookOpen className="h-12 w-12 text-white/80" strokeWidth={1.5} />
        </div>
      )}
      {typeof capability.NodeCount === 'number' && capability.NodeCount > 0 && (
        <div className="absolute bottom-2 right-2 flex items-center gap-1 rounded-full bg-black/55 px-2.5 py-1 text-xs font-semibold text-[#fff] backdrop-blur-sm">
          <Layers className="h-3.5 w-3.5" />
          {capability.NodeCount} nodos
        </div>
      )}
    </div>
  );
}

function SummaryModal({
  capability,
  onClose,
}: {
  capability: BackendCapabilitySummary;
  onClose: () => void;
}) {
  const [graph, setGraph] = useState<BackendCapabilityGraph | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getCapabilityGraph(capability.CapabilityId, MOCK_USER.oid)
      .then((data) => {
        if (!cancelled) setGraph(data);
      })
      .catch(() => {
        if (!cancelled) setError('No se pudo cargar el resumen de este curso.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [capability.CapabilityId]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        className="max-h-[80vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-white/10 bg-slate-950 p-6 shadow-2xl shadow-black/40"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3">
          <h2 className="text-lg font-semibold text-white">{capability.Name}</h2>
          <button
            onClick={onClose}
            className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white"
            aria-label="Cerrar"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {loading && <p className="mt-4 text-sm text-slate-400">Cargando resumen...</p>}
        {error && <p className="mt-4 text-sm text-red-400">{error}</p>}

        {!loading && !error && graph && (
          <div className="mt-4 space-y-4">
            {graph.ExecutiveSummary ? (
              <p className="text-sm leading-relaxed text-slate-300">{graph.ExecutiveSummary}</p>
            ) : (
              <p className="text-sm italic text-slate-500">
                Este curso todavía no tiene un resumen ejecutivo generado.
              </p>
            )}

            {graph.KeyEntities && graph.KeyEntities.length > 0 && (
              <div>
                <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
                  Conceptos clave
                </p>
                <div className="flex flex-wrap gap-1.5">
                  {graph.KeyEntities.map((entity) => (
                    <span
                      key={entity.Name}
                      className="rounded-full bg-brand-500/10 px-2.5 py-1 text-xs font-medium text-brand-300"
                      title={entity.Note}
                    >
                      {entity.Name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            <div className="flex items-center gap-1.5 pt-1 text-xs font-medium text-slate-500">
              <Layers className="h-3.5 w-3.5" />
              {graph.Nodes.length} nodos de aprendizaje
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default function SubjectCapabilitiesPage() {
  const { subjectCode } = useParams();
  const { t } = useI18n();
  const [capabilities, setCapabilities] = useState<BackendCapabilitySummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [summaryFor, setSummaryFor] = useState<BackendCapabilitySummary | null>(null);

  useEffect(() => {
    setIsLoading(true);
    getCapabilities(subjectCode)
      .then(setCapabilities)
      .catch((err) => setError(err instanceof Error ? err.message : 'Error al cargar capabilities.'))
      .finally(() => setIsLoading(false));
  }, [subjectCode]);

  return (
    <div className="mx-auto max-w-5xl p-8">
      <Link to="/" className="text-sm text-slate-400 hover:text-white hover:underline">
        ← {t.backToSubjects}
      </Link>
      <h1 className="mt-2 text-2xl font-semibold capitalize tracking-tight text-white">{subjectCode}</h1>

      {isLoading && <p className="text-slate-400">Cargando...</p>}
      {error && <p className="text-red-400">{error}</p>}
      {!isLoading && !error && capabilities.length === 0 && (
        <p className="mb-6 mt-1 text-sm text-slate-500">
          Todavía no hay capabilities en este subject.
        </p>
      )}

      <div className="mt-4 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {capabilities.map((capability) => (
          <div
            key={capability.CapabilityId}
            className="group flex flex-col overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03] transition-all hover:-translate-y-1 hover:border-white/20 hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-brand-500/10"
          >
            <Link to={`/capabilities/${capability.CapabilityId}`}>
              <CoverImage capability={capability} />
            </Link>

            <div className="flex flex-1 flex-col gap-2 p-4">
              <ProviderBadge />
              <Link to={`/capabilities/${capability.CapabilityId}`} className="group/title">
                <h3 className="font-semibold text-white group-hover/title:text-brand-300">
                  {capability.Name}
                </h3>
              </Link>
              <StarRating rating={placeholderRatingFor(capability.CapabilityId)} />
              <p className="line-clamp-3 text-sm text-slate-400">
                {capability.LearningSummary ?? 'Descubre los conceptos clave de este curso, paso a paso.'}
              </p>

              <div className="mt-auto flex items-center justify-between pt-3">
                <button
                  onClick={() => setSummaryFor(capability)}
                  className="flex items-center gap-1.5 rounded-lg border border-white/10 px-3 py-1.5 text-xs font-medium text-slate-300 transition hover:border-white/20 hover:bg-white/5 hover:text-white"
                >
                  <Info className="h-3.5 w-3.5" />
                  Ver resumen
                </button>
                <Link
                  to={`/capabilities/${capability.CapabilityId}`}
                  className={`flex items-center gap-1.5 rounded-lg bg-gradient-to-r px-3 py-1.5 text-xs font-semibold text-[#fff] shadow-sm transition hover:opacity-90 ${accentFor(
                    capability.CapabilityId
                  )}`}
                >
                  <Sparkles className="h-3.5 w-3.5" />
                  Empezar
                </Link>
              </div>
            </div>
          </div>
        ))}
      </div>

      {summaryFor && <SummaryModal capability={summaryFor} onClose={() => setSummaryFor(null)} />}
    </div>
  );
}

