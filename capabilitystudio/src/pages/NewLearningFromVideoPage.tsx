import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, FileVideo, Loader2, UploadCloud, CheckCircle2, AlertTriangle, Sparkles, X } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { DEFAULT_STUDIO_TENANT_ID } from '../lib/api/pdfPipelineApi';
import {
  createStructuredLearningDocumentFromVideo,
  type VideoEngramInput,
} from '../lib/api/videoStructuredLearningApi';

/**
 * "New Learning From Video" (2026-11-05) — entry page for the video-sourced
 * Structured Learning flow: upload a local MP4, index it with Azure AI
 * Video Indexer (via the separate LearnWitVideos backend), and preview the
 * extracted transcript/topics/keywords before continuing to frame
 * extraction + Engram creation. Completely separate from the traditional
 * PDF-based "New learning path" flow — does not touch any of its code.
 */
const VIDEO_API_BASE = 'http://localhost:7072/api';

type ProcessingState = 'idle' | 'uploading' | 'indexing' | 'ready' | 'error';

// NOTE: the LearnWitVideos backend (unlike the main HumanOS backend) serializes
// its DTOs with plain System.Text.Json defaults, which preserve the C# record's
// PascalCase property names as-is (no camelCase policy). These interfaces
// intentionally mirror that exact casing.
interface VideoProcessingStatus {
  VideoId: string;
  Name: string;
  State: string;
  ProcessingProgress: number;
}

interface TranscriptLine {
  Text: string;
  StartSeconds: number;
  EndSeconds: number;
  Speaker: string;
}

interface VideoLearningInsights {
  VideoId: string;
  Name: string;
  DurationInSeconds: number;
  Summary: string;
  Topics: string[];
  Keywords: string[];
  Labels: string[];
  Transcript: TranscriptLine[];
}

interface EngramFrame {
  ImageUrl: string;
  TimestampSeconds: number;
  SceneText: string;
}

interface Engram {
  ChapterTitle: string;
  SubchapterTitle: string;
  Text: string;
  StartSeconds: number;
  EndSeconds: number;
  Frames: EngramFrame[];
}

interface VideoChapterOutline {
  VideoId: string;
  ChapterCount: number;
  EngramCount: number;
  Engrams: Engram[];
  ExecutiveSummary?: string;
}

type OutlineState = 'idle' | 'building' | 'ready' | 'error';
type CourseCreationState = 'idle' | 'creating' | 'pedagogy' | 'ready' | 'error';

export default function NewLearningFromVideoPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { user } = useAuth();

  const [file, setFile] = useState<File | null>(null);
  const [name, setName] = useState('');
  const [language, setLanguage] = useState<'es-ES' | 'en-US'>('es-ES');
  const [state, setState] = useState<ProcessingState>('idle');
  const [videoId, setVideoId] = useState<string | null>(null);
  const [resumeVideoId, setResumeVideoId] = useState('');
  const [status, setStatus] = useState<VideoProcessingStatus | null>(null);
  const [insights, setInsights] = useState<VideoLearningInsights | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [outlineState, setOutlineState] = useState<OutlineState>('idle');
  const [outline, setOutline] = useState<VideoChapterOutline | null>(null);
  const [outlineError, setOutlineError] = useState<string | null>(null);
  const [summaryRegenerating, setSummaryRegenerating] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [courseState, setCourseState] = useState<CourseCreationState>('idle');
  const [courseError, setCourseError] = useState<string | null>(null);
  const [courseStep, setCourseStep] = useState<string>('');
  const [lightboxFrame, setLightboxFrame] = useState<{ url: string; caption: string } | null>(null);
  const [expandedEngrams, setExpandedEngrams] = useState<Set<number>>(new Set());
  // Per-Engram "informational only" toggle (2026-11-07) — checked Engrams
  // will only generate their Teaching/Learn step (no exams/recall/etc).
  const [informationalEngrams, setInformationalEngrams] = useState<Set<number>>(new Set());

  // Essential course origin metadata (2026-11-09), asked right before
  // course creation — see matching StructuredLearningDocument fields.
  const [skillType, setSkillType] = useState<'SoftSkill' | 'TechnicalSkill'>('SoftSkill');
  const [authorName, setAuthorName] = useState('');
  const [authorCourseName, setAuthorCourseName] = useState('');
  const [presentedDate, setPresentedDate] = useState('');
  const [presentedEntityType, setPresentedEntityType] = useState('Universidad');
  const [presentedEntityName, setPresentedEntityName] = useState('');
  const [courseLanguageCode, setCourseLanguageCode] = useState<'es' | 'en'>('es');

  function toggleEngramExpanded(idx: number) {
    setExpandedEngrams((prev) => {
      const next = new Set(prev);
      if (next.has(idx)) {
        next.delete(idx);
      } else {
        next.add(idx);
      }
      return next;
    });
  }

  function toggleEngramInformationalOnly(idx: number) {
    setInformationalEngrams((prev) => {
      const next = new Set(prev);
      if (next.has(idx)) {
        next.delete(idx);
      } else {
        next.add(idx);
      }
      return next;
    });
  }
  const [createdDocumentId, setCreatedDocumentId] = useState<string | null>(null);
  const isCourseLocked = courseState === 'creating' || courseState === 'pedagogy';

  async function handleBuildOutline() {
    if (!videoId) return;
    setOutlineState('building');
    setOutlineError(null);
    try {
      const res = await fetch(`${VIDEO_API_BASE}/videos/${videoId}/build-outline?intervalSeconds=5`, {
        method: 'POST',
      });
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.error ?? `HTTP ${res.status}`);
      }
      const data = (await res.json()) as VideoChapterOutline;
      setOutline(data);
      setOutlineState('ready');
    } catch (err) {
      setOutlineError(
        err instanceof Error ? err.message : 'No se pudieron generar los capítulos/Engrams.',
      );
      setOutlineState('error');
    }
  }

  async function handleRegenerateSummary() {
    if (!videoId) return;
    setSummaryRegenerating(true);
    setSummaryError(null);
    try {
      const res = await fetch(`${VIDEO_API_BASE}/videos/${videoId}/executive-summary`, {
        method: 'POST',
      });
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.error ?? `HTTP ${res.status}`);
      }
      const data = (await res.json()) as { ExecutiveSummary: string };
      setOutline((prev) => (prev ? { ...prev, ExecutiveSummary: data.ExecutiveSummary } : prev));
    } catch (err) {
      setSummaryError(err instanceof Error ? err.message : 'No se pudo regenerar el resumen.');
    } finally {
      setSummaryRegenerating(false);
    }
  }

  function handleRemoveEngram(engramIdx: number) {
    if (isCourseLocked) return;
    setOutline((prev) => {
      if (!prev) return prev;
      const engrams = prev.Engrams.filter((_, idx) => idx !== engramIdx);
      const chapterCount = new Set(engrams.map((e) => e.ChapterTitle)).size;
      return {
        ...prev,
        Engrams: engrams,
        EngramCount: engrams.length,
        ChapterCount: chapterCount,
      };
    });
  }

  function handleRemoveFrame(engramIdx: number, frameIdx: number) {
    if (isCourseLocked) return;
    setOutline((prev) => {
      if (!prev) return prev;
      const engrams = prev.Engrams.map((engram, idx) => {
        if (idx !== engramIdx) return engram;
        return { ...engram, Frames: engram.Frames.filter((_, fIdx) => fIdx !== frameIdx) };
      });
      return { ...prev, Engrams: engrams };
    });
  }

  async function fetchFrameAsBase64(imageUrl: string): Promise<{ base64: string; contentType: string } | null> {
    try {
      const res = await fetch(`${VIDEO_API_BASE.replace(/\/api$/, '')}${imageUrl}`);
      if (!res.ok) return null;
      const blob = await res.blob();
      const base64 = await new Promise<string>((resolve, reject) => {
        const reader = new FileReader();
        reader.onloadend = () => {
          const result = reader.result as string;
          resolve(result.split(',')[1] ?? '');
        };
        reader.onerror = reject;
        reader.readAsDataURL(blob);
      });
      return { base64, contentType: blob.type || 'image/jpeg' };
    } catch {
      return null;
    }
  }

  function validateMetadata(): string | null {
    if (!authorName.trim()) return 'Falta el nombre del autor del curso.';
    if (!authorCourseName.trim()) return 'Falta el nombre del curso dado por el autor.';
    if (!presentedEntityType.trim()) return 'Falta la entidad donde se presentó el video.';
    return null;
  }

  async function handleCreateCourse() {
    if (!outline) return;
    const metadataError = validateMetadata();
    if (metadataError) {
      setCourseError(metadataError);
      setCourseState('error');
      return;
    }
    setCourseState('creating');
    setCourseError(null);
    try {
      setCourseStep('Descargando frames reales del video…');
      const engrams: VideoEngramInput[] = [];
      for (const [idx, engram] of outline.Engrams.entries()) {
        const frames = [];
        for (const frame of engram.Frames) {
          const img = await fetchFrameAsBase64(frame.ImageUrl);
          frames.push({
            TimestampSeconds: frame.TimestampSeconds,
            SceneText: frame.SceneText,
            ImageBase64: img?.base64 ?? null,
            ImageContentType: img?.contentType ?? 'image/jpeg',
          });
        }
        engrams.push({
          ChapterTitle: engram.ChapterTitle,
          SubchapterTitle: engram.SubchapterTitle,
          Text: engram.Text,
          StartSeconds: engram.StartSeconds,
          EndSeconds: engram.EndSeconds,
          IsInformationalOnly: informationalEngrams.has(idx),
          Frames: frames,
        });
      }

      setCourseStep('Creando el documento de Structured Learning…');
      const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
      const documentName = name.trim() || insights?.Name || 'Curso desde video';
      const creationResult = await createStructuredLearningDocumentFromVideo({
        tenantId,
        documentName,
        createdByOid: user?.oid ?? null,
        createdByEmail: user?.email ?? null,
        isGlobal: false,
        includeKnowledgeGraphEntities: true,
        includeKnowledgeGraphConnections: true,
        sourceVideoId: videoId,
        executiveSummary: outline?.ExecutiveSummary ?? null,
        skillType,
        authorName: authorName.trim() || null,
        authorCourseName: authorCourseName.trim() || null,
        presentedDate: presentedDate || null,
        presentedEntityType: presentedEntityType.trim() || null,
        presentedEntityName: presentedEntityName.trim() || null,
        courseLanguageCode,
        engrams,
      });
      setCreatedDocumentId(creationResult.StructuredLearningDocumentId);
      setCourseState('ready');
      // Mirrors the PDF flow: show the Knowledge Graph (entities/connections)
      // first, and only then let the designer start Step 3 (pedagogy), on
      // a dedicated progress page — instead of silently running pedagogy here.
      navigate(`/structured-learning-video-runs/${creationResult.StructuredLearningDocumentId}`);
    } catch (err) {
      setCourseError(err instanceof Error ? err.message : 'No se pudo crear el curso desde el video.');
      setCourseState('error');
    }
  }

  // Poll processing status every 5s while indexing is in progress.
  // Auto-resume (2026-11-07): "Videos en Proceso" links here with ?resume={videoId}
  // so the user never has to type/know the Azure Video Indexer id manually.
  useEffect(() => {
    const resumeParam = searchParams.get('resume');
    if (resumeParam && !videoId) {
      setVideoId(resumeParam);
      setState('indexing');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (state !== 'indexing' || !videoId) return;
    let cancelled = false;

    const poll = async () => {
      try {
        const res = await fetch(`${VIDEO_API_BASE}/videos/${videoId}/status`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = (await res.json()) as VideoProcessingStatus;
        if (cancelled) return;
        setStatus(data);

        const normalizedState = data.State?.toLowerCase() ?? '';
        if (normalizedState === 'processed') {
          const insightsRes = await fetch(`${VIDEO_API_BASE}/videos/${videoId}/insights`);
          if (!insightsRes.ok) throw new Error(`HTTP ${insightsRes.status}`);
          const insightsData = (await insightsRes.json()) as VideoLearningInsights;
          if (cancelled) return;
          setInsights(insightsData);
          setState('ready');
        } else if (normalizedState === 'failed') {
          setErrorMessage('Azure AI Video Indexer no pudo procesar el video.');
          setState('error');
        }
      } catch (err) {
        if (!cancelled) {
          setErrorMessage(err instanceof Error ? err.message : 'Error al consultar el estado del video.');
          setState('error');
        }
      }
    };

    const interval = setInterval(poll, 5000);
    void poll();
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [state, videoId]);

  async function handleUpload() {
    if (!file) return;
    setErrorMessage(null);
    setState('uploading');
    try {
      const effectiveName = name.trim() || file.name.replace(/\.[^/.]+$/, '');
      const params = new URLSearchParams({
        name: effectiveName,
        language,
        fileName: file.name,
      });
      const res = await fetch(`${VIDEO_API_BASE}/videos/index-file?${params.toString()}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/octet-stream' },
        body: file,
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const data = (await res.json()) as { VideoId: string; Name: string; State: string };
      setVideoId(data.VideoId);
      setState('indexing');
    } catch (err) {
      setErrorMessage(
        err instanceof Error
          ? `No se pudo subir el video: ${err.message}`
          : 'No se pudo subir el video.',
      );
      setState('error');
    }
  }

  return (
    <div className="mx-auto max-w-4xl px-4 py-8 sm:px-6">
      <button
        type="button"
        onClick={() => navigate('/')}
        className="mb-6 inline-flex items-center gap-1.5 text-sm font-medium text-brand-400 hover:text-brand-300"
      >
        <ArrowLeft className="h-4 w-4" /> Volver
      </button>

      <div className="mb-8 flex items-center gap-3">
        <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-r from-rose-500 via-orange-500 to-amber-500 text-white shadow-lg shadow-rose-500/25">
          <FileVideo className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-xl font-bold text-white">New Learning From Video</h1>
          <p className="text-sm text-slate-400">
            Sube un video (MP4), extráelo con Azure AI Video Indexer y previsualiza el transcript, temas y palabras clave antes de crear los Engrams.
          </p>
        </div>
      </div>

      {state === 'idle' || state === 'uploading' || state === 'error' ? (
        <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-6 space-y-5">
          <div className="rounded-lg border border-amber-400/30 bg-amber-500/10 p-3">
            <label className="mb-1.5 block text-xs font-medium text-amber-200">
              ¿Ya procesaste este video antes? Reanuda con su Video ID (evita resubir el archivo)
            </label>
            <div className="flex gap-2">
              <input
                type="text"
                value={resumeVideoId}
                onChange={(e) => setResumeVideoId(e.target.value)}
                placeholder="ej. g8yxdoxtju"
                className="flex-1 rounded-lg border border-white/10 bg-slate-950/50 px-3 py-1.5 text-sm text-white placeholder:text-slate-500 focus:border-amber-400 focus:outline-none"
              />
              <button
                type="button"
                disabled={!resumeVideoId.trim()}
                onClick={() => {
                  setVideoId(resumeVideoId.trim());
                  setState('indexing');
                }}
                className="rounded-lg bg-amber-500/90 px-3 py-1.5 text-xs font-semibold text-white hover:bg-amber-500 disabled:cursor-not-allowed disabled:opacity-50"
              >
                Reanudar
              </button>
            </div>
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-300">Archivo de video (.mp4)</label>
            <input
              ref={fileInputRef}
              type="file"
              accept="video/mp4,video/*"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              className="block w-full text-sm text-slate-300 file:mr-3 file:rounded-lg file:border-0 file:bg-brand-500 file:px-4 file:py-2 file:text-sm file:font-semibold file:text-white hover:file:bg-brand-400"
            />
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-300">Nombre de la lección</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="ej. Introducción al Aprendizaje Automático"
              className="w-full rounded-lg border border-white/10 bg-slate-950/50 px-3 py-2 text-sm text-white placeholder:text-slate-500 focus:border-brand-400 focus:outline-none"
            />
          </div>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-300">Idioma del video</label>
            <select
              value={language}
              onChange={(e) => setLanguage(e.target.value as 'es-ES' | 'en-US')}
              className="w-full rounded-lg border border-white/10 bg-slate-950/50 px-3 py-2 text-sm text-white focus:border-brand-400 focus:outline-none"
            >
              <option value="es-ES">Español</option>
              <option value="en-US">English</option>
            </select>
          </div>

          {errorMessage && (
            <div className="flex items-start gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-300">
              <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
              <span>{errorMessage}</span>
            </div>
          )}

          <button
            type="button"
            disabled={!file || state === 'uploading'}
            onClick={handleUpload}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-rose-500 via-orange-500 to-amber-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-rose-500/25 transition-all hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-50"
          >
            {state === 'uploading' ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" /> Subiendo video...
              </>
            ) : (
              <>
                <UploadCloud className="h-4 w-4" /> Subir y procesar video
              </>
            )}
          </button>
        </div>
      ) : null}

      {state === 'indexing' && (
        <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-6">
          <div className="flex items-center gap-3">
            <Loader2 className="h-5 w-5 animate-spin text-brand-400" />
            <div>
              <p className="text-sm font-semibold text-white">Procesando video con Azure AI Video Indexer...</p>
              <p className="text-xs text-slate-400">
                Estado: {status?.State ?? 'Enviado'} {status?.ProcessingProgress ? `· ${status.ProcessingProgress}%` : ''}
              </p>
            </div>
          </div>
          <p className="mt-4 text-xs text-slate-500">
            Esto puede tardar varios minutos dependiendo de la duración del video. Puedes dejar esta página abierta.
          </p>
        </div>
      )}

      {state === 'ready' && insights && (
        <div className="space-y-5">
          <div className="flex items-center gap-2 rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-3 text-sm text-emerald-300">
            <CheckCircle2 className="h-4 w-4" />
            <span>Video procesado correctamente ({Math.round(insights.DurationInSeconds)}s).</span>
          </div>

          <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-6">
            <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-slate-400">Resumen</h2>
            <p className="text-sm text-slate-200">{insights.Summary || 'Sin resumen disponible.'}</p>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <InsightChipList title="Temas" items={insights.Topics} />
            <InsightChipList title="Palabras clave" items={insights.Keywords} />
            <InsightChipList title="Etiquetas" items={insights.Labels} />
          </div>

          <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-6">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">Transcript</h2>
            <div className="max-h-80 space-y-2 overflow-y-auto pr-2">
              {insights.Transcript.map((line, idx) => (
                <div key={idx} className="flex gap-3 text-sm">
                  <span className="w-16 flex-shrink-0 font-mono text-xs text-slate-500">
                    {formatTimestamp(line.StartSeconds)}
                  </span>
                  <span className="text-slate-300">{line.Text}</span>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-6">
            <div className="mb-3 flex items-center justify-between">
              <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-400">
                Capítulos y Engrams (IA)
              </h2>
              {outlineState !== 'building' && (
                <button
                  onClick={handleBuildOutline}
                  className="rounded-lg bg-rose-500/90 px-3 py-1.5 text-xs font-semibold text-white hover:bg-rose-500"
                >
                  {outlineState === 'ready' ? 'Regenerar' : 'Generar capítulos con IA'}
                </button>
              )}
            </div>

            {outlineState === 'idle' && (
              <p className="text-xs text-slate-500">
                Extrae frames reales del video, los sincroniza con el transcript y deja que un LLM lea
                todo el transcript para decidir capítulos y subcapítulos (Engrams), cada uno con sus
                imágenes reales en el tiempo correcto.
              </p>
            )}

            {outlineState === 'building' && (
              <div className="flex items-center gap-2 text-sm text-slate-300">
                <Loader2 className="h-4 w-4 animate-spin" />
                <span>Extrayendo frames y generando capítulos con IA… esto puede tardar uno o dos minutos.</span>
              </div>
            )}

            {outlineState === 'error' && outlineError && (
              <div className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-300">
                <AlertTriangle className="h-4 w-4" />
                <span>{outlineError}</span>
              </div>
            )}

            {outlineState === 'ready' && outline && (
              <div className="space-y-4">
                <p className="text-xs text-slate-500">
                  {outline.ChapterCount} capítulo(s) · {outline.EngramCount} Engram(s) ·{' '}
                  {outline.Engrams.reduce((sum, e) => sum + e.Frames.length, 0)} imagen(es) generada(s)
                </p>

                <div className="rounded-xl border border-white/10 bg-slate-950/40 p-4">
                  <div className="mb-2 flex items-center justify-between">
                    <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                      Resumen del curso
                    </h3>
                    {!isCourseLocked && (
                      <button
                        onClick={handleRegenerateSummary}
                        disabled={summaryRegenerating}
                        className="flex items-center gap-1.5 rounded-lg bg-rose-500/90 px-3 py-1 text-xs font-semibold text-white hover:bg-rose-500 disabled:opacity-60"
                      >
                        {summaryRegenerating && <Loader2 className="h-3 w-3 animate-spin" />}
                        Regenerar resumen
                      </button>
                    )}
                  </div>
                  {summaryError && (
                    <p className="mb-2 text-xs text-red-300">{summaryError}</p>
                  )}
                  {outline.ExecutiveSummary ? (
                    <p className="whitespace-pre-line text-xs text-slate-300">{outline.ExecutiveSummary}</p>
                  ) : (
                    <p className="text-xs text-slate-500">
                      Aún no hay resumen generado para este curso.
                    </p>
                  )}
                </div>

                {outline.Engrams.map((engram, idx) => (
                  <div key={idx} className="group relative rounded-xl border border-white/10 bg-slate-950/40 p-4">
                    {!isCourseLocked && (
                      <button
                        onClick={() => handleRemoveEngram(idx)}
                        title="Eliminar este Engram"
                        className="absolute right-2 top-2 rounded-full bg-slate-800/80 p-1 text-slate-400 opacity-0 transition hover:bg-red-500/80 hover:text-white group-hover:opacity-100"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    )}
                    <div className="mb-1 text-xs font-semibold uppercase tracking-wide text-rose-300">
                      {engram.ChapterTitle}
                    </div>
                    <div className="mb-2 flex items-center justify-between pr-6">
                      <h3 className="text-sm font-semibold text-slate-100">{engram.SubchapterTitle}</h3>
                      <span className="flex items-center gap-2 font-mono text-xs text-slate-500">
                        {engram.Frames.length} img
                        <span>
                          {formatTimestamp(engram.StartSeconds)}–{formatTimestamp(engram.EndSeconds)}
                        </span>
                      </span>
                    </div>
                    <label className="mb-2 flex w-fit items-center gap-2 rounded-lg bg-slate-900/60 px-2 py-1 text-xs text-slate-300">
                      <input
                        type="checkbox"
                        checked={informationalEngrams.has(idx)}
                        disabled={isCourseLocked}
                        onChange={() => toggleEngramInformationalOnly(idx)}
                        className="h-3.5 w-3.5 rounded border-white/20 bg-slate-800 text-rose-500 focus:ring-rose-400"
                      />
                      Solo informativo (sin exámenes ni recall — solo Learn)
                    </label>
                    {engram.Frames.length > 0 && (
                      <div className="mb-2 flex gap-2 overflow-x-auto pb-1">
                        {engram.Frames.map((frame, fIdx) => {
                          const url = `${VIDEO_API_BASE.replace(/\/api$/, '')}${frame.ImageUrl}`;
                          return (
                            <div key={fIdx} className="group/frame relative flex-shrink-0">
                              <img
                                src={url}
                                alt={`Frame ${formatTimestamp(frame.TimestampSeconds)}`}
                                title={frame.SceneText}
                                onClick={() =>
                                  setLightboxFrame({
                                    url,
                                    caption: `${formatTimestamp(frame.TimestampSeconds)} — ${frame.SceneText ?? ''}`,
                                  })
                                }
                                className="h-20 w-32 cursor-zoom-in rounded-lg border border-white/10 object-cover transition hover:border-rose-400/60"
                              />
                              {!isCourseLocked && (
                                <button
                                  onClick={() => handleRemoveFrame(idx, fIdx)}
                                  title="Eliminar esta imagen"
                                  className="absolute right-1 top-1 rounded-full bg-slate-900/80 p-0.5 text-slate-300 opacity-70 transition hover:bg-red-500/80 hover:text-white hover:opacity-100 group-hover/frame:opacity-100"
                                >
                                  <X className="h-3 w-3" />
                                </button>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    )}
                    <p className={expandedEngrams.has(idx) ? 'whitespace-pre-line text-xs text-slate-400' : 'line-clamp-3 text-xs text-slate-400'}>
                      {engram.Text}
                    </p>
                    <button
                      onClick={() => toggleEngramExpanded(idx)}
                      className="mt-1 text-xs font-semibold text-rose-300 hover:text-rose-200"
                    >
                      {expandedEngrams.has(idx) ? 'Ver menos ▲' : 'Ver texto completo ▼'}
                    </button>
                  </div>
                ))}
              </div>
            )}
          </div>

          {outlineState === 'ready' && outline && (
            <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-6">
              <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">
                Crear el curso
              </h2>

              {(courseState === 'idle' || courseState === 'error') && (
                <div className="space-y-3">
                  <p className="text-xs text-slate-500">
                    Crea el curso de Structured Learning con estos capítulos/Engrams (imágenes reales
                    incluidas). En la siguiente pantalla podrás revisar el Knowledge Graph y luego
                    generar la explicación pedagógica de cada Engram.
                  </p>

                  <div className="grid grid-cols-1 gap-3 rounded-xl border border-white/10 bg-slate-950/40 p-4 sm:grid-cols-2">
                    <div className="sm:col-span-2">
                      <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-400">
                        Metadata del curso
                      </h3>
                    </div>

                    <label className="block text-xs text-slate-400">
                      Tipo de curso
                      <select
                        value={skillType}
                        onChange={(e) => setSkillType(e.target.value as 'SoftSkill' | 'TechnicalSkill')}
                        className="mt-1 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-slate-200"
                      >
                        <option value="SoftSkill">Soft Skill</option>
                        <option value="TechnicalSkill">Technical Skill</option>
                      </select>
                    </label>

                    <label className="block text-xs text-slate-400">
                      Idioma del curso
                      <select
                        value={courseLanguageCode}
                        onChange={(e) => setCourseLanguageCode(e.target.value as 'es' | 'en')}
                        className="mt-1 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-slate-200"
                      >
                        <option value="es">Español</option>
                        <option value="en">Inglés</option>
                      </select>
                    </label>

                    <label className="block text-xs text-slate-400">
                      Autor del curso
                      <input
                        type="text"
                        value={authorName}
                        onChange={(e) => setAuthorName(e.target.value)}
                        placeholder="Nombre, apellido, título"
                        className="mt-1 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-slate-200"
                      />
                    </label>

                    <label className="block text-xs text-slate-400">
                      Nombre del curso (dado por el autor)
                      <input
                        type="text"
                        value={authorCourseName}
                        onChange={(e) => setAuthorCourseName(e.target.value)}
                        placeholder="Ej. Introducción a Algoritmos"
                        className="mt-1 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-slate-200"
                      />
                    </label>

                    <label className="block text-xs text-slate-400">
                      Fecha en que se presentó el video
                      <input
                        type="date"
                        value={presentedDate}
                        onChange={(e) => setPresentedDate(e.target.value)}
                        className="mt-1 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-slate-200"
                      />
                    </label>

                    <label className="block text-xs text-slate-400">
                      Entidad donde se presentó
                      <select
                        value={presentedEntityType}
                        onChange={(e) => setPresentedEntityType(e.target.value)}
                        className="mt-1 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-slate-200"
                      >
                        <option value="Universidad">Universidad</option>
                        <option value="Salon de clases">Salón de clases</option>
                        <option value="Empresa">Empresa</option>
                        <option value="Conferencia">Conferencia</option>
                        <option value="Online">Online</option>
                        <option value="Otro">Otro</option>
                      </select>
                    </label>

                    <label className="block text-xs text-slate-400 sm:col-span-2">
                      Nombre específico de la entidad (opcional)
                      <input
                        type="text"
                        value={presentedEntityName}
                        onChange={(e) => setPresentedEntityName(e.target.value)}
                        placeholder="Ej. MIT, Acme Corp"
                        className="mt-1 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-slate-200"
                      />
                    </label>
                  </div>

                  {courseState === 'error' && courseError && (
                    <div className="flex items-center gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-300">
                      <AlertTriangle className="h-4 w-4" />
                      <span>{courseError}</span>
                    </div>
                  )}
                  <button
                    onClick={handleCreateCourse}
                    className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-rose-500 via-orange-500 to-amber-500 px-4 py-2 text-sm font-semibold text-white hover:opacity-90"
                  >
                    <Sparkles className="h-4 w-4" />
                    Crear curso con IA
                  </button>
                </div>
              )}

              {courseState === 'creating' && (
                <div className="flex items-center gap-2 text-sm text-slate-300">
                  <Loader2 className="h-4 w-4 animate-spin" />
                  <span>{courseStep}</span>
                </div>
              )}

              {courseState === 'ready' && createdDocumentId && (
                <div className="flex items-center gap-2 text-sm text-emerald-300">
                  <CheckCircle2 className="h-4 w-4" />
                  <span>¡Documento creado! Abriendo el Knowledge Graph…</span>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {lightboxFrame && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-6"
          onClick={() => setLightboxFrame(null)}
        >
          <div className="relative max-h-full max-w-5xl" onClick={(e) => e.stopPropagation()}>
            <button
              onClick={() => setLightboxFrame(null)}
              className="absolute -right-3 -top-3 rounded-full bg-slate-800 p-1.5 text-slate-200 hover:bg-red-500 hover:text-white"
            >
              <X className="h-4 w-4" />
            </button>
            <img
              src={lightboxFrame.url}
              alt="Frame ampliado"
              className="max-h-[80vh] w-auto rounded-xl border border-white/10 object-contain"
            />
            {lightboxFrame.caption && (
              <p className="mt-2 max-w-5xl text-center text-xs text-slate-300">{lightboxFrame.caption}</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function InsightChipList({ title, items }: { title: string; items: string[] }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-4">
      <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">{title}</h3>
      {items.length === 0 ? (
        <p className="text-xs text-slate-500">—</p>
      ) : (
        <div className="flex flex-wrap gap-1.5">
          {items.map((item) => (
            <span key={item} className="rounded-full bg-white/5 px-2.5 py-1 text-xs text-slate-300">
              {item}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

function formatTimestamp(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}
