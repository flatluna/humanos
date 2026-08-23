import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { getStructuredLearningDocuments, type StructuredLearningDocumentSummary } from '../api/structuredLearningApi';
import {
  enrollStructuredLearningDocument,
  getEnrolledStructuredLearningDocuments,
  unenrollStructuredLearningDocument,
} from '../api/enrolledStructuredLearningApi';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '../i18n/useI18n';
import {
  BookOpen,
  Calendar,
  CheckCircle2,
  Info,
  Search,
  UserPlus,
  X,
} from 'lucide-react';

// Structured Learning's real student-facing course/section UI now lives
// in this app too (2026-08-14) — /structured-learning/:documentId, same
// as Capability Graph's own /capabilities/:capabilityId route.

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

/** PLACEHOLDER rating (no real stats/evaluation feature yet — TODO: replace
 * with an actual average once course ratings/evaluations are implemented).
 * Deterministic per capability (4.3–5.0) so it doesn't jump around on
 * re-render, purely cosmetic in the meantime. */
/** Formats Capability.CreatedDate (ISO-8601 UTC string) as a short local
 * date + time, e.g. "12 mar 2026, 14:05". Returns null when missing/invalid
 * so callers can skip rendering the badge entirely. */
function formatCreatedDate(createdDate?: string): string | null {
  if (!createdDate) return null;
  const parsed = new Date(createdDate);
  if (Number.isNaN(parsed.getTime())) return null;
  const datePart = parsed.toLocaleDateString('es', { day: '2-digit', month: 'short', year: 'numeric' });
  const timePart = parsed.toLocaleTimeString('es', { hour: '2-digit', minute: '2-digit' });
  return `${datePart}, ${timePart}`;
}

function CreatedDateBadge({ createdDate, hint }: { createdDate?: string; hint: string }) {
  const formatted = formatCreatedDate(createdDate);
  if (!formatted) return null;
  return (
    <div
      className="flex items-center gap-1 text-xs text-slate-500 dark:text-slate-400"
      title={hint}
    >
      <Calendar className="h-3.5 w-3.5" />
      {formatted}
    </div>
  );
}

/** "Provider" byline shown above the title, mirroring Coursera's
 * university/company logo row. App/company name isn't finalized yet, so
 * this reuses the same "HO" gradient badge + "Human OS" wordmark as the
 * TopBar, with a fixed caption crediting the AI generation pipeline
 * (TODO: revisit once the product/company name and branding are final). */
function ProviderBadge({ caption }: { caption: string }) {
  return (
    <div className="flex items-center gap-1.5">
      <div className="flex h-5 w-5 flex-none items-center justify-center rounded bg-gradient-to-br from-brand-500 to-accent-500">
        <span className="text-[9px] font-bold text-[#fff]">HO</span>
      </div>
      <span className="truncate text-xs font-medium text-slate-500">
        {caption}
      </span>
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
  const { t } = useI18n();
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const [structuredLearningDocuments, setStructuredLearningDocuments] = useState<StructuredLearningDocumentSummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [enrolledStructuredLearningIds, setEnrolledStructuredLearningIds] = useState<Set<string>>(new Set());
  const [enrollingId, setEnrollingId] = useState<string | null>(null);
  const [summaryDocument, setSummaryDocument] = useState<StructuredLearningDocumentSummary | null>(null);

  useEffect(() => {
    setIsLoading(true);
    getStructuredLearningDocuments()
      .then((structuredDocs) => setStructuredLearningDocuments(structuredDocs))
      .catch((err) => setError(err instanceof Error ? err.message : t.loadCapabilitiesError))
      .finally(() => setIsLoading(false));
  }, [t.loadCapabilitiesError]);

  useEffect(() => {
    if (!personId) return;
    getEnrolledStructuredLearningDocuments(personId)
      .then((enrolled) => setEnrolledStructuredLearningIds(new Set(enrolled.map((d) => d.structuredLearningDocumentId))))
      .catch(() => setEnrolledStructuredLearningIds(new Set()));
  }, [personId]);

  async function handleToggleEnrollStructuredLearning(document: StructuredLearningDocumentSummary) {
    if (!personId || enrollingId) return;
    const isEnrolled = enrolledStructuredLearningIds.has(document.structuredLearningDocumentId);
    setEnrollingId(document.structuredLearningDocumentId);
    try {
      if (isEnrolled) {
        await unenrollStructuredLearningDocument(personId, document.structuredLearningDocumentId);
        setEnrolledStructuredLearningIds((prev) => {
          const next = new Set(prev);
          next.delete(document.structuredLearningDocumentId);
          return next;
        });
      } else {
        await enrollStructuredLearningDocument(personId, document.structuredLearningDocumentId);
        setEnrolledStructuredLearningIds((prev) => new Set(prev).add(document.structuredLearningDocumentId));
      }
    } catch {
      // Silently ignore — the button just stays in its previous state.
    } finally {
      setEnrollingId(null);
    }
  }

  const filteredStructuredLearningDocuments = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    const documents = structuredLearningDocuments.filter((document) => {
      if (!query) return true;
      return document.documentName.toLowerCase().includes(query);
    });
    return documents;
  }, [structuredLearningDocuments, searchQuery]);

  const unifiedCatalog = useMemo(() => {
    return filteredStructuredLearningDocuments.map((document) => ({
      kind: 'structured-learning' as const,
      sortDate: document.createdDate ?? new Date(0).toISOString(),
      item: document,
    }));
  }, [filteredStructuredLearningDocuments]);

  return (
    <div className="mx-auto max-w-5xl p-4 sm:p-8">
      <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">{t.capabilitiesHomeTitle}</h1>
      <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{t.capabilitiesHomeSubtitle}</p>

      <div className="relative mt-4">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
        <input
          type="search"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder={t.searchCapabilitiesPlaceholder}
          className="input pl-9"
        />
      </div>

      {isLoading && <p className="mt-4 text-slate-500 dark:text-slate-400">{t.loadingEllipsis}</p>}
      {error && <p className="mt-4 text-red-600 dark:text-red-400">{error}</p>}
      {!isLoading && !error && unifiedCatalog.length === 0 && (
        <p className="mb-6 mt-4 text-sm text-slate-500">{t.noCapabilitiesMatchSearch}</p>
      )}

      <div className="mt-4 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {unifiedCatalog.map((entry) => {
          if (entry.kind === 'structured-learning') {
            const document = entry.item as StructuredLearningDocumentSummary;
            const isEnrolled = enrolledStructuredLearningIds.has(document.structuredLearningDocumentId);
            return (
              <div
                key={document.structuredLearningDocumentId}
                className="group flex flex-col overflow-hidden rounded-2xl border border-slate-200 dark:border-white/10 bg-white dark:bg-white/[0.03] transition-all hover:-translate-y-1 hover:border-slate-300 dark:hover:border-white/20 hover:bg-slate-100 dark:hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-brand-500/10"
              >
                <Link to={`/structured-learning/${document.structuredLearningDocumentId}`} className="block w-full text-left">
                  <div className="relative h-36 w-full overflow-hidden bg-gradient-to-br from-emerald-500 to-teal-600">
                    {document.coverIllustrationId ? (
                      <img
                        src={`${import.meta.env.VITE_API_BASE_URL ?? '/api'}/illustrations/structured-learning/${document.coverIllustrationId}/image`}
                        alt={document.documentName}
                        className="h-full w-full object-cover"
                      />
                    ) : (
                      <div className="flex h-full w-full items-center justify-center">
                        <BookOpen className="h-12 w-12 text-white/80" strokeWidth={1.5} />
                      </div>
                    )}
                    <div className="absolute inset-0 bg-gradient-to-t from-black/50 via-transparent to-transparent" />
                    <div className="absolute left-2 top-2 rounded-full bg-black/55 px-2.5 py-1 text-[11px] font-bold uppercase tracking-wide text-[#fff] backdrop-blur-sm">
                      ENG
                    </div>
                  </div>
                </Link>

                <div className="flex flex-1 flex-col gap-2 p-4">
                  <ProviderBadge caption="Structured Learning" />
                  <Link to={`/structured-learning/${document.structuredLearningDocumentId}`} className="group/title text-left">
                    <h3 className="font-semibold text-slate-900 dark:text-white group-hover/title:text-brand-700 dark:group-hover/title:text-brand-300">
                      {document.documentName}
                    </h3>
                  </Link>
                  <CreatedDateBadge createdDate={document.createdDate} hint={t.createdDateHint} />
                  <p className="line-clamp-3 text-sm text-slate-500 dark:text-slate-400">
                    {document.sectionCount} {t.summaryLearningNodesSuffix}
                  </p>
                  <div className="mt-auto flex flex-wrap items-center gap-2 pt-3">
                    <button
                      type="button"
                      onClick={() => setSummaryDocument(document)}
                      className="flex items-center gap-1.5 rounded-lg border border-slate-200 dark:border-white/10 px-3 py-1.5 text-xs font-medium text-slate-600 dark:text-slate-300 transition hover:border-slate-300 dark:hover:border-white/20 hover:bg-slate-100 dark:hover:bg-white/5 hover:text-slate-900 dark:hover:text-white"
                    >
                      <Info className="h-3.5 w-3.5" />
                      {t.viewSummaryButton}
                    </button>

                    {personId && (
                      <button
                        type="button"
                        onClick={() => handleToggleEnrollStructuredLearning(document)}
                        disabled={enrollingId === document.structuredLearningDocumentId}
                        title={t.enrollHint}
                        className={`flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs font-medium transition disabled:opacity-60 ${
                          isEnrolled
                            ? 'border-emerald-300 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300'
                            : 'border-slate-200 dark:border-white/10 text-slate-600 dark:text-slate-300 hover:border-slate-300 dark:hover:border-white/20 hover:bg-slate-100 dark:hover:bg-white/5 hover:text-slate-900 dark:hover:text-white'
                        }`}
                      >
                        {isEnrolled ? (
                          <>
                            <CheckCircle2 className="h-3.5 w-3.5" />
                            {t.enrolledLabel}
                          </>
                        ) : (
                          <>
                            <UserPlus className="h-3.5 w-3.5" />
                            {t.enrollButton}
                          </>
                        )}
                      </button>
                    )}
                  </div>
                </div>
              </div>
            );
          }

        })}
      </div>

      {summaryDocument && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
          onClick={() => setSummaryDocument(null)}
        >
          <div
            className="w-full max-w-lg rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-950"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wide text-emerald-600 dark:text-emerald-300">
                  Structured Learning
                </p>
                <h2 className="mt-1 text-xl font-semibold text-slate-900 dark:text-white">
                  {summaryDocument.documentName}
                </h2>
              </div>
              <button
                type="button"
                onClick={() => setSummaryDocument(null)}
                className="rounded-full p-1 text-slate-500 hover:bg-slate-100 hover:text-slate-900 dark:hover:bg-white/10 dark:hover:text-white"
                aria-label="Cerrar resumen"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="mt-6 grid grid-cols-2 gap-3">
              <div className="rounded-xl bg-slate-100 p-4 dark:bg-white/[0.05]">
                <p className="text-xs text-slate-500 dark:text-slate-400">Secciones</p>
                <p className="mt-1 text-2xl font-semibold text-slate-900 dark:text-white">{summaryDocument.sectionCount}</p>
              </div>
              <div className="rounded-xl bg-slate-100 p-4 dark:bg-white/[0.05]">
                <p className="text-xs text-slate-500 dark:text-slate-400">Creado</p>
                <p className="mt-1 text-sm font-semibold text-slate-900 dark:text-white">
                  {new Date(summaryDocument.createdDate).toLocaleDateString()}
                </p>
              </div>
            </div>
            <p className="mt-5 whitespace-pre-wrap text-sm leading-6 text-slate-600 dark:text-slate-300">
              {summaryDocument.executiveSummary ?? 'Este Engram todavía no tiene un resumen disponible.'}
            </p>
          </div>
        </div>
      )}
    </div>
  );
}

