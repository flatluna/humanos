import { useEffect, useRef, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Loader2, CheckCircle2, AlertTriangle, ArrowLeft, PlayCircle, Sparkles, Layers, Hash } from 'lucide-react';
import {
  getStructuredLearningDocument,
  type StructuredLearningDocumentDetail,
} from '../lib/api/structuredLearningApi';
import {
  startVideoStructuredLearningPedagogy,
  getVideoStructuredLearningPedagogyStatus,
  getActiveVideoStructuredLearningPedagogyRun,
  type VideoStructuredLearningPedagogyRunStatus,
} from '../lib/api/videoStructuredLearningApi';
import KnowledgeGraphSection from '../components/KnowledgeGraphSection';

const POLL_INTERVAL_MS = 2500;

/** "New Learning From Video" (2026-11-06) — Phase 3 progress page, shown
 * right after a Structured Learning Document has been created from a
 * video's LLM-grouped chapter/Engram outline (Step 2, synchronous — no
 * polling needed, unlike the PDF flow). Mirrors
 * StructuredLearningRunProgressPage.tsx's UX/ordering for the PDF flow:
 * show the Knowledge Graph (entities/connections) first, THEN let the
 * designer start/track Step 3 (pedagogy generation) — same order the user
 * explicitly asked to match. Uses the VIDEO-specific pedagogy endpoints so
 * real extracted frames are reused as illustrations instead of generating
 * new AI images. */
export default function VideoStructuredLearningRunProgressPage() {
  const { documentId } = useParams<{ documentId: string }>();
  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!documentId) return;
    let cancelled = false;
    getStructuredLearningDocument(documentId)
      .then((detail) => {
        if (!cancelled) setDocument(detail);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : String(err));
      });
    return () => {
      cancelled = true;
    };
  }, [documentId]);

  if (!documentId) return null;

  return (
    <div className="mx-auto max-w-2xl px-6 py-16 text-left">
      <Link to="/" className="mb-6 inline-flex items-center gap-1.5 text-sm font-medium text-slate-400 hover:text-slate-200">
        <ArrowLeft className="h-4 w-4" /> Volver al inicio
      </Link>

      {error && (
        <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-red-300">{error}</div>
      )}

      {!error && (
        <div className="space-y-8">
          <div className="text-center">
            <CheckCircle2 className="mx-auto h-14 w-14 text-emerald-400" />
            <h2 className="mt-4 text-2xl font-bold text-white">
              {document ? document.DocumentName : 'Curso creado'}
            </h2>
            <p className="mt-2 text-slate-400">
              El curso ya fue creado a partir del video, con sus capítulos/Engrams e imágenes reales.
              Primero revisa el Knowledge Graph y luego genera la pedagogía de cada Engram.
            </p>
          </div>

          {document && (
            <div className="grid grid-cols-2 gap-3">
              <ResultStat icon={Layers} label="Secciones" value={document.Sections.length} />
              <ResultStat icon={Hash} label="Engrams" value={document.Sections.length} />
            </div>
          )}

          {document && document.Sections.some((s) => s.VideoStartSeconds != null) && (
            <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
              <h3 className="mb-3 text-sm font-semibold text-slate-200">Tiempos de cada clip en el video</h3>
              <ul className="space-y-1.5">
                {document.Sections.slice()
                  .sort((a, b) => a.SortOrder - b.SortOrder)
                  .map((s) => (
                    <li
                      key={s.StructuredLearningDocumentSectionId}
                      className="flex items-center justify-between gap-3 rounded-lg bg-slate-950/30 px-3 py-2 text-sm"
                    >
                      <span className="min-w-0 flex-1 truncate text-slate-300">
                        <span className="text-slate-500">{s.ChapterTitle} · </span>
                        {s.SubchapterTitle}
                      </span>
                      {s.VideoStartSeconds != null && (
                        <span className="flex-shrink-0 font-mono text-xs text-brand-300">
                          {formatTimestamp(s.VideoStartSeconds)}
                          {s.VideoEndSeconds != null && <> – {formatTimestamp(s.VideoEndSeconds)}</>}
                        </span>
                      )}
                    </li>
                  ))}
              </ul>
            </div>
          )}

          <KnowledgeGraphSection documentId={documentId} canRegenerateFacts />

          <VideoPedagogySection documentId={documentId} courseLanguageCode={document?.CourseLanguageCode ?? null} />
        </div>
      )}
    </div>
  );
}

function ResultStat({ icon: Icon, label, value }: { icon: typeof Layers; label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 text-center">
      <Icon className="mx-auto h-5 w-5 text-brand-400" />
      <div className="mt-1 text-lg font-semibold text-white">{value}</div>
      <div className="text-xs text-slate-500">{label}</div>
    </div>
  );
}

function formatTimestamp(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

function VideoPedagogySection({
  documentId,
  courseLanguageCode,
}: {
  documentId: string;
  courseLanguageCode: string | null;
}) {
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [runId, setRunId] = useState<string | null>(null);
  const [status, setStatus] = useState<VideoStructuredLearningPedagogyRunStatus | null>(null);
  const [checkingActiveRun, setCheckingActiveRun] = useState(true);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  // Recover an already-started run after a reload/navigation-away lost the
  // in-memory RunId (2026-11-06) — otherwise this page silently shows the
  // "start" button again even though a run is actively generating.
  useEffect(() => {
    let cancelled = false;
    getActiveVideoStructuredLearningPedagogyRun(documentId)
      .then((active) => {
        if (cancelled) return;
        if (active) {
          setRunId(active.RunId);
          setStatus(active);
        }
      })
      .catch(() => {
        // Best-effort recovery only — fall back to showing the start button.
      })
      .finally(() => {
        if (!cancelled) setCheckingActiveRun(false);
      });
    return () => {
      cancelled = true;
    };
  }, [documentId]);

  useEffect(() => {
    if (!runId) return;
    let cancelled = false;

    async function poll() {
      try {
        const next = await getVideoStructuredLearningPedagogyStatus(runId!);
        if (cancelled) return;
        setStatus(next);
        if (next.Stage === 'Running') {
          timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
        }
      } catch (err) {
        if (!cancelled) setSubmitError(err instanceof Error ? err.message : String(err));
      }
    }

    poll();
    return () => {
      cancelled = true;
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [runId]);

  async function handleGenerate() {
    setSubmitting(true);
    setSubmitError(null);
    try {
      const run = await startVideoStructuredLearningPedagogy(documentId, (courseLanguageCode as 'es' | 'en') ?? 'es');
      setRunId(run.RunId);
      setStatus(run);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'No se pudo iniciar la generación de pedagogía.');
    } finally {
      setSubmitting(false);
    }
  }

  if (checkingActiveRun) {
    return (
      <div className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
        <Loader2 className="h-5 w-5 animate-spin text-brand-400" />
        <div className="text-sm text-slate-400">Comprobando si ya hay una generación en curso…</div>
      </div>
    );
  }

  if (!runId) {
    return (
      <div className="space-y-4 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
        <h3 className="text-lg font-semibold text-white">Generar pedagogía (Step 3)</h3>
        <p className="text-sm text-slate-400">
          Genera la explicación pedagógica de cada Engram (hipótesis, deep dive, recall, quiz),
          reutilizando las imágenes reales del video como ilustración — sin generar imágenes nuevas con IA.
        </p>
        {submitError && (
          <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-3 text-sm text-red-300">{submitError}</div>
        )}
        <button
          type="button"
          onClick={handleGenerate}
          disabled={submitting}
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 transition-transform enabled:hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-40"
        >
          <Sparkles className="h-4 w-4" />
          {submitting ? 'Iniciando…' : 'Generar pedagogía con IA'}
        </button>
      </div>
    );
  }

  if (status?.Stage === 'Running' || !status) {
    return (
      <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
        <div className="flex items-center gap-3">
          <Loader2 className="h-5 w-5 animate-spin text-brand-400" />
          <div>
            <div className="text-sm font-medium text-white">Generando pedagogía…</div>
            <div className="text-xs text-slate-400">{status?.CurrentStepDescription}</div>
          </div>
        </div>
        {status?.TotalSections != null && status?.CompletedSections != null && (
          <p className="text-xs text-slate-500">
            {status.CompletedSections} de {status.TotalSections} Engrams completados
          </p>
        )}
      </div>
    );
  }

  if (status.Stage === 'Failed') {
    return (
      <div className="flex items-center gap-2 rounded-2xl border border-red-500/20 bg-red-500/5 p-5 text-sm text-red-300">
        <AlertTriangle className="h-4 w-4 flex-shrink-0" />
        <span>{status.ErrorMessage}</span>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <Link
        to={`/structured-learning/${documentId}/course`}
        className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-teal-500 to-emerald-500 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-teal-500/25 transition-transform hover:scale-[1.02]"
      >
        <PlayCircle className="h-4 w-4" />
        Ver el curso
      </Link>
      <div className="grid grid-cols-2 gap-3">
        <ResultStat icon={CheckCircle2} label="Engrams completados" value={status.Result?.SectionsCompleted ?? 0} />
        <ResultStat icon={AlertTriangle} label="Engrams fallidos" value={status.Result?.SectionsFailed ?? 0} />
      </div>
    </div>
  );
}
