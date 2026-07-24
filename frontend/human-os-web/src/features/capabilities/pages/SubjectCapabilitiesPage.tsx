import { Link } from 'react-router';
import { useEffect, useMemo, useState } from 'react';
import {
  getCapabilities,
  getCapabilityCoverImageUrl,
  BackendCapabilitySummary,
} from '../api/capabilitiesApi';
import { getCapabilityGraph, type BackendCapabilityGraph } from '../api/capabilityGraphApi';
import { getSubjects, type Subject } from '../api/subjectsApi';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '../i18n/useI18n';
import {
  BookOpen,
  ChefHat,
  FlaskConical,
  Globe2,
  Info,
  Landmark,
  Layers,
  PawPrint,
  Search,
  Sigma,
  Sparkles,
  Star,
  Users,
  X,
  type LucideIcon,
} from 'lucide-react';

/** Real icon per Subject code (same mapping as humanlearn's HomePage.tsx). */
const SUBJECT_ICONS: Record<string, LucideIcon> = {
  finanzas: Landmark,
  cocina: ChefHat,
  'recursos-humanos': Users,
  animales: PawPrint,
  ciencia: FlaskConical,
  geografia: Globe2,
  matematicas: Sigma,
};

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
      <span className="text-xs font-semibold text-slate-600 dark:text-slate-300">{rating.toFixed(1)}</span>
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
  const accent = accentFor(capability.capabilityId);
  const showPlaceholder = !capability.hasCoverImage || failed;

  return (
    <div className={`relative h-36 w-full overflow-hidden bg-gradient-to-br ${accent}`}>
      {!showPlaceholder && (
        <img
          src={getCapabilityCoverImageUrl(capability.capabilityId)}
          alt={capability.name}
          className="h-full w-full object-cover"
          onError={() => setFailed(true)}
        />
      )}
      {showPlaceholder && (
        <div className="flex h-full w-full items-center justify-center">
          <BookOpen className="h-12 w-12 text-white/80" strokeWidth={1.5} />
        </div>
      )}
      {typeof capability.nodeCount === 'number' && capability.nodeCount > 0 && (
        <div className="absolute bottom-2 right-2 flex items-center gap-1 rounded-full bg-black/55 px-2.5 py-1 text-xs font-semibold text-[#fff] backdrop-blur-sm">
          <Layers className="h-3.5 w-3.5" />
          {capability.nodeCount} nodos
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
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const [graph, setGraph] = useState<BackendCapabilityGraph | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!personId) return;
    let cancelled = false;
    getCapabilityGraph(capability.capabilityId, personId)
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
  }, [capability.capabilityId, personId]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        className="max-h-[80vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-slate-950 p-6 shadow-2xl shadow-black/40"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{capability.name}</h2>
          <button
            onClick={onClose}
            className="rounded-full p-1 text-slate-500 dark:text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10 hover:text-slate-900 dark:hover:text-white"
            aria-label="Cerrar"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {loading && <p className="mt-4 text-sm text-slate-500 dark:text-slate-400">Cargando resumen...</p>}
        {error && <p className="mt-4 text-sm text-red-600 dark:text-red-400">{error}</p>}

        {!loading && !error && graph && (
          <div className="mt-4 space-y-4">
            {graph.executiveSummary ? (
              <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">{graph.executiveSummary}</p>
            ) : (
              <p className="text-sm italic text-slate-500">
                Este curso todavía no tiene un resumen ejecutivo generado.
              </p>
            )}

            {graph.keyEntities && graph.keyEntities.length > 0 && (
              <div>
                <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
                  Conceptos clave
                </p>
                <div className="flex flex-wrap gap-1.5">
                  {graph.keyEntities.map((entity) => (
                    <span
                      key={entity.name}
                      className="rounded-full bg-brand-500/10 px-2.5 py-1 text-xs font-medium text-brand-700 dark:text-brand-300"
                      title={entity.note}
                    >
                      {entity.name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            <div className="flex items-center gap-1.5 pt-1 text-xs font-medium text-slate-500">
              <Layers className="h-3.5 w-3.5" />
              {graph.nodes.length} nodos de aprendizaje
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

/** Capacidades — landing page for the whole learning experience ported
 * from humanlearn (see /memories/repo/multi-frontend-app-landscape.md).
 * Shows every published capability across all subjects; opening a card
 * goes to its graph map (`/capabilities/:id`) and from there into a node
 * (`/capabilities/:id/nodes/:nodeId`) — no separate subject-filtered route,
 * per the "todo bajo /capabilities/*" structure decision. */
export function CapabilitiesHomePage() {
  const { language } = useI18n();
  const [capabilities, setCapabilities] = useState<BackendCapabilitySummary[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [summaryFor, setSummaryFor] = useState<BackendCapabilitySummary | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [activeSubject, setActiveSubject] = useState<string | null>(null);

  useEffect(() => {
    setIsLoading(true);
    getCapabilities()
      .then(setCapabilities)
      .catch((err) => setError(err instanceof Error ? err.message : 'Error al cargar capabilities.'))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    getSubjects(language).then(setSubjects).catch(() => setSubjects([]));
  }, [language]);

  const subjectsWithCapabilities = useMemo(() => {
    const codesInUse = new Set(capabilities.map((c) => c.subjectCode).filter(Boolean));
    return subjects.filter((s) => codesInUse.has(s.code));
  }, [subjects, capabilities]);

  const filteredCapabilities = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return capabilities.filter((capability) => {
      const matchesSubject = !activeSubject || capability.subjectCode === activeSubject;
      const matchesQuery =
        !query ||
        capability.name.toLowerCase().includes(query) ||
        (capability.description ?? '').toLowerCase().includes(query) ||
        (capability.learningSummary ?? '').toLowerCase().includes(query);
      return matchesSubject && matchesQuery;
    });
  }, [capabilities, activeSubject, searchQuery]);

  return (
    <div className="mx-auto max-w-5xl p-4 sm:p-8">
      <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">Capacidades</h1>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Elige una capacidad para empezar a aprender.</p>

      <div className="relative mt-4">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
        <input
          type="search"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="Buscar capacidades..."
          className="input pl-9"
        />
      </div>

      {subjectsWithCapabilities.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2">
          <button
            onClick={() => setActiveSubject(null)}
            className={`flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-medium transition ${
              activeSubject === null
                ? 'border-brand-400/60 bg-brand-500/10 text-brand-700 dark:text-brand-300'
                : 'border-slate-200 dark:border-white/10 text-slate-500 dark:text-slate-400 hover:border-slate-300 dark:hover:border-white/20 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            Todas
          </button>
          {subjectsWithCapabilities.map((subject) => {
            const Icon = SUBJECT_ICONS[subject.code] ?? Globe2;
            const isActive = activeSubject === subject.code;
            return (
              <button
                key={subject.code}
                onClick={() => setActiveSubject(isActive ? null : subject.code)}
                className={`flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-xs font-medium transition ${
                  isActive
                    ? 'border-brand-400/60 bg-brand-500/10 text-brand-700 dark:text-brand-300'
                    : 'border-slate-200 dark:border-white/10 text-slate-500 dark:text-slate-400 hover:border-slate-300 dark:hover:border-white/20 hover:text-slate-900 dark:hover:text-white'
                }`}
              >
                <Icon className="h-3.5 w-3.5" />
                {subject.name}
              </button>
            );
          })}
        </div>
      )}

      {isLoading && <p className="mt-4 text-slate-500 dark:text-slate-400">Cargando...</p>}
      {error && <p className="mt-4 text-red-600 dark:text-red-400">{error}</p>}
      {!isLoading && !error && capabilities.length === 0 && (
        <p className="mb-6 mt-4 text-sm text-slate-500">Todavía no hay capabilities publicadas.</p>
      )}
      {!isLoading && !error && capabilities.length > 0 && filteredCapabilities.length === 0 && (
        <p className="mb-6 mt-4 text-sm text-slate-500">Ninguna capacidad coincide con tu búsqueda.</p>
      )}

      <div className="mt-4 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {filteredCapabilities.map((capability) => (
          <div
            key={capability.capabilityId}
            className="group flex flex-col overflow-hidden rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] transition-all hover:-translate-y-1 hover:border-slate-300 dark:hover:border-white/20 hover:bg-slate-100 dark:hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-brand-500/10"
          >
            <Link to={`/capabilities/${capability.capabilityId}`}>
              <CoverImage capability={capability} />
            </Link>

            <div className="flex flex-1 flex-col gap-2 p-4">
              <ProviderBadge />
              <Link to={`/capabilities/${capability.capabilityId}`} className="group/title">
                <h3 className="font-semibold text-slate-900 dark:text-white group-hover/title:text-brand-700 dark:group-hover/title:text-brand-300">
                  {capability.name}
                </h3>
              </Link>
              <StarRating rating={placeholderRatingFor(capability.capabilityId)} />
              <p className="line-clamp-3 text-sm text-slate-500 dark:text-slate-400">
                {capability.learningSummary ?? 'Descubre los conceptos clave de este curso, paso a paso.'}
              </p>

              <div className="mt-auto flex items-center justify-between pt-3">
                <button
                  onClick={() => setSummaryFor(capability)}
                  className="flex items-center gap-1.5 rounded-lg border border-slate-200 dark:border-white/10 px-3 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-300 transition hover:border-slate-300 dark:hover:border-white/20 hover:bg-slate-100 dark:hover:bg-white/5 hover:text-slate-900 dark:hover:text-white"
                >
                  <Info className="h-3.5 w-3.5" />
                  Ver resumen
                </button>
                <Link
                  to={`/capabilities/${capability.capabilityId}`}
                  className={`flex items-center gap-1.5 rounded-lg bg-gradient-to-r px-3 py-1.5 text-xs font-semibold text-[#fff] shadow-sm transition hover:opacity-90 ${accentFor(
                    capability.capabilityId
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

