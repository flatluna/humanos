import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { BookOpen, GraduationCap, CheckCircle2, Clock, RefreshCw, Network, Trash2, ArrowRight, Loader2 } from 'lucide-react';
import {
  listAcademicCourseDocuments,
  deleteAcademicCourseDocument,
  type AcademicCourseDocumentSummaryDto,
} from '../lib/api/academicCourseApi';
import {
  getStructuredLearningDocuments,
  getActiveStructuredLearningPedagogyRun,
  type StructuredLearningDocumentSummary,
  type StructuredLearningPedagogyRunStatus,
} from '../lib/api/structuredLearningApi';
import StructuredLearningDocumentCard from '../components/StructuredLearningDocumentCard';

/**
 * "Catálogo Académico" (2026-10-08) — shows BOTH:
 * 1. Saved family.AcademicCourseDocument drafts (ProfesorExpert text, not yet sent
 *    to Learning Path Design) — "Borradores".
 * 2. Full StructuredLearningDocument courses (knowledge graph, engrams, etc.)
 *    generated THROUGH the Academic bridge — filtered strictly to
 *    PedagogicalAudience === 'YoungLearner' so Enterprise courses NEVER appear
 *    here (mirrors /catalog's own card/behavior exactly — same
 *    StructuredLearningDocumentCard, same "click to resume/view progress" link
 *    to /structured-learning/{id} — but scoped to only the Family/Academic market).
 */
export default function AcademicLearningCatalogPage() {
  const { i18n } = useTranslation();
  const isSpanish = i18n.language?.startsWith('es');
  const [documents, setDocuments] = useState<AcademicCourseDocumentSummaryDto[]>([]);
  const [generatedCourses, setGeneratedCourses] = useState<StructuredLearningDocumentSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deletingCode, setDeletingCode] = useState<string | null>(null);
  // Active pedagogy run (2026-10-09) — lets a Draft card that is CURRENTLY
  // being turned into Engrams (via "Generar Engrams") link straight to its
  // live progress page instead of back into the ProfesorExpert generator
  // wizard, which made it look like the running course "disappeared".
  // Matched by title only (no FK exists yet between AcademicCourseDocument
  // drafts and StructuredLearningDocument rows) — good enough in practice
  // since the extraction step carries the draft's own title over verbatim
  // as the new document's name.
  const [activeRun, setActiveRun] = useState<StructuredLearningPedagogyRunStatus | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    Promise.all([
      listAcademicCourseDocuments(),
      getStructuredLearningDocuments().catch(() => []),
    ])
      .then(([docs, structuredDocs]) => {
        if (cancelled) return;
        // Defensive filter (2026-10-08) — skip any broken/test row with no real
        // courseCode (would otherwise render as a blank, unclickable card).
        setDocuments(docs.filter((d) => d.courseCode && d.courseCode.trim() !== ''));
        // STRICT filter — never show Enterprise ("Standard"/null) courses here.
        setGeneratedCourses(structuredDocs.filter((d) => d.PedagogicalAudience === 'YoungLearner'));
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    let activeRunTimer: ReturnType<typeof setTimeout>;
    async function pollActiveRun() {
      const run = await getActiveStructuredLearningPedagogyRun().catch(() => null);
      if (!cancelled) {
        setActiveRun(run);
        activeRunTimer = setTimeout(pollActiveRun, 4000);
      }
    }
    pollActiveRun();

    return () => {
      cancelled = true;
      clearTimeout(activeRunTimer);
    };
  }, []);

  /** Loose title match (2026-10-09) — see activeRun's comment above for why
   * this isn't a strict FK-based match. */
  function matchesActiveRun(draft: AcademicCourseDocumentSummaryDto): boolean {
    if (!activeRun) return false;
    const draftTitle = (draft.titleEn || draft.titleEs || '').trim().toLowerCase();
    const runTitle = (activeRun.DocumentName || '').trim().toLowerCase();
    if (!draftTitle || !runTitle) return false;
    return runTitle.startsWith(draftTitle) || draftTitle.startsWith(runTitle);
  }

  async function handleDeleteDraft(courseCode: string, event: React.MouseEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (!window.confirm(isSpanish ? `¿Eliminar el borrador "${courseCode}"?` : `Delete draft "${courseCode}"?`)) {
      return;
    }
    setDeletingCode(courseCode);
    try {
      await deleteAcademicCourseDocument(courseCode);
      setDocuments((prev) => prev.filter((d) => d.courseCode !== courseCode));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setDeletingCode(null);
    }
  }

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8 space-y-6 animate-fade-in text-slate-100">
      <div>
        <div className="inline-flex items-center gap-2 rounded-full bg-lime-500/10 px-3 py-1 text-xs font-bold text-lime-400 border border-lime-500/20 mb-2">
          <BookOpen className="h-3.5 w-3.5" />
          <span>{isSpanish ? 'Cursos Académicos / Familiares (ProfesorExpert)' : 'Academic / Family Courses (ProfesorExpert)'}</span>
        </div>
        <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight text-white">
          {isSpanish ? 'Catálogo Académico' : 'Academic Learning Catalog'}
        </h1>
        <p className="mt-1 text-sm text-slate-400 max-w-3xl">
          {isSpanish
            ? 'Todos los borradores de cursos académicos guardados. Haz clic en uno para continuar donde quedaste.'
            : 'Every saved academic course draft. Click one to continue where you left off.'}
        </p>
      </div>

      {loading && (
        <div className="flex items-center gap-2 text-sm text-slate-400">
          <RefreshCw className="h-4 w-4 animate-spin" />
          <span>{isSpanish ? 'Cargando catálogo...' : 'Loading catalog...'}</span>
        </div>
      )}

      {error && (
        <div className="rounded-xl border border-red-500/30 bg-red-950/30 p-4 text-sm font-bold text-red-300">
          {error}
        </div>
      )}

      {/* =========================================================================
          SECTION 1: GENERATED COURSES (full knowledge graph / engrams pipeline) —
          same card + same "click to resume/view progress" behavior as /catalog,
          strictly filtered to PedagogicalAudience === 'YoungLearner' (never shows
          Enterprise courses).
          ========================================================================= */}
      {!loading && !error && (
        <div className="space-y-3">
          <div className="flex items-center gap-2">
            <Network className="h-4 w-4 text-emerald-400" />
            <h2 className="text-sm font-bold text-white uppercase tracking-wide">
              {isSpanish ? 'Cursos Generados (Knowledge Graph & Engrams)' : 'Generated Courses (Knowledge Graph & Engrams)'}
            </h2>
            <span className="text-xs text-slate-500">({generatedCourses.length})</span>
          </div>

          {generatedCourses.length === 0 ? (
            <div className="rounded-2xl border border-white/10 bg-slate-900/40 p-6 text-center">
              <p className="text-xs text-slate-400">
                {isSpanish
                  ? 'Todavía no hay cursos académicos generados con el Knowledge Graph. Termina el Paso 6 (Diseño de Ruta de Aprendizaje) desde un borrador para que aparezca aquí.'
                  : 'No academic courses generated with the Knowledge Graph yet. Finish Step 6 (Learning Path Design) from a draft to see it here.'}
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {generatedCourses.map((doc) => (
                <StructuredLearningDocumentCard key={doc.StructuredLearningDocumentId} document={doc} />
              ))}
            </div>
          )}
        </div>
      )}

      {/* =========================================================================
          SECTION 2: PROFESOREXPERT DRAFTS (plain text, not yet bridged into the
          Structured Learning pipeline) — click to resume/regenerate in
          /new-academic-learning.
          ========================================================================= */}
      <div className="space-y-3 pt-2">
        <div className="flex items-center gap-2">
          <BookOpen className="h-4 w-4 text-lime-400" />
          <h2 className="text-sm font-bold text-white uppercase tracking-wide">
            {isSpanish ? 'Borradores (ProfesorExpert)' : 'Drafts (ProfesorExpert)'}
          </h2>
          <span className="text-xs text-slate-500">({documents.length})</span>
        </div>

      {!loading && !error && documents.length === 0 && (
        <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-10 text-center">
          <GraduationCap className="h-10 w-10 text-slate-600 mx-auto mb-3" />
          <p className="text-sm text-slate-400">
            {isSpanish
              ? 'Todavía no hay cursos académicos guardados. Genera uno desde "Nuevo Aprendizaje Académico" y guárdalo.'
              : 'No academic courses saved yet. Generate one from "New Academic Learning" and save it.'}
          </p>
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {documents.map((doc) => {
          const isGenerating = matchesActiveRun(doc);
          return (
          <Link
            key={doc.courseCode}
            to={
              isGenerating && activeRun
                ? `/structured-learning/${activeRun.DocumentId}`
                : `/new-academic-learning?code=${encodeURIComponent(doc.courseCode)}&stage=kindergarten`
            }
            className={`group flex flex-col gap-3 rounded-2xl border p-5 shadow-md transition-all duration-300 hover:-translate-y-1 hover:shadow-2xl ${
              isGenerating
                ? 'border-emerald-500/40 bg-emerald-950/10 hover:border-emerald-500/60 hover:shadow-emerald-500/10'
                : 'border-white/10 bg-white/[0.03] hover:border-lime-500/40 hover:bg-white/[0.05] hover:shadow-lime-500/10'
            }`}
          >
            {isGenerating && activeRun && (
              <div className="flex items-center gap-2 rounded-xl border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-[11px] font-bold text-emerald-300">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                <span className="flex-1">
                  {isSpanish ? 'Generando Engrams ahora' : 'Generating Engrams now'}
                  {activeRun.TotalSections != null && activeRun.CompletedSections != null &&
                    ` (${activeRun.CompletedSections}/${activeRun.TotalSections})`}
                </span>
                <ArrowRight className="h-3.5 w-3.5" />
              </div>
            )}
            <div className="flex items-start justify-between gap-2">
              <h3 className="text-base font-bold text-white leading-snug line-clamp-2 group-hover:text-lime-400 transition-colors">
                {isSpanish ? doc.titleEs : doc.titleEn || doc.titleEs}
              </h3>
              <div className="flex shrink-0 items-center gap-1.5">
                <span className="font-mono text-[10px] px-2 py-0.5 rounded-lg bg-lime-500/15 text-lime-300 border border-lime-500/30 font-bold">
                  {doc.courseCode}
                </span>
                <button
                  type="button"
                  onClick={(e) => void handleDeleteDraft(doc.courseCode, e)}
                  disabled={deletingCode === doc.courseCode}
                  title={isSpanish ? 'Eliminar este borrador' : 'Delete this draft'}
                  className="rounded-lg p-1.5 text-slate-500 hover:text-red-400 hover:bg-red-500/10 transition disabled:opacity-50"
                >
                  {deletingCode === doc.courseCode ? (
                    <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Trash2 className="h-3.5 w-3.5" />
                  )}
                </button>
              </div>
            </div>

            <p className="text-xs text-slate-400 line-clamp-1">{doc.subject}</p>

            <div className="flex flex-wrap items-center gap-2 text-[11px]">
              <span
                className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-bold border ${
                  doc.isApprovedByEducator
                    ? 'bg-emerald-500/15 text-emerald-300 border-emerald-500/30'
                    : 'bg-amber-500/15 text-amber-300 border-amber-500/30'
                }`}
              >
                <CheckCircle2 className="h-3 w-3" />
                {doc.isApprovedByEducator
                  ? isSpanish
                    ? 'Aprobado'
                    : 'Approved'
                  : isSpanish
                  ? 'Borrador'
                  : 'Draft'}
              </span>
              <span className="inline-flex items-center gap-1 rounded-full bg-white/5 px-2 py-0.5 text-slate-400 border border-white/10">
                <Clock className="h-3 w-3" />
                ~{doc.estimatedHours} {isSpanish ? 'hrs' : 'hrs'}
              </span>
              <span className="inline-flex items-center gap-1 rounded-full bg-white/5 px-2 py-0.5 text-slate-400 border border-white/10 uppercase">
                {doc.language}
              </span>
            </div>

            <div className="text-[11px] text-slate-500 mt-auto">
              {isSpanish ? 'Actualizado' : 'Updated'}: {new Date(doc.updatedDate).toLocaleString(isSpanish ? 'es-MX' : 'en-US')}
            </div>
          </Link>
          );
        })}
      </div>
      </div>
    </div>
  );
}
