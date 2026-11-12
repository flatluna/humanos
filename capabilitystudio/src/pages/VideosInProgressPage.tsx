import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ArrowLeft, FileVideo, Loader2, AlertTriangle, RefreshCw, Trash2, PlayCircle, CheckCircle2 } from 'lucide-react';
import { apiGet } from '../lib/api/httpClient';

/**
 * "Clases en Proceso" (2026-11-07) — lists every video already indexed in the
 * Azure AI Video Indexer account (via the LearnWitVideos backend's GET /videos),
 * so a user can resume an in-progress or finished video upload without
 * re-uploading the MP4 or needing to remember/know its Video Indexer id.
 * Cross-references HumanOS's GetVideoSourcedDocuments endpoint so "Listo"
 * actually means "the course/Engrams were created", not just "Azure AI Video
 * Indexer finished indexing" (those are two very different things — a video
 * can be indexed for hours without anyone ever generating its course).
 * Companion to NewLearningFromVideoPage, which this page deep-links into via
 * `?resume={videoId}`.
 */
const VIDEO_API_BASE = 'http://localhost:7072/api';

// Mirrors LearnWitVideos.Contracts.VideoSummary — plain PascalCase JSON (no camelCase policy).
interface VideoSummary {
  VideoId: string;
  Name: string;
  State: string;
  CreatedUtc: string;
  DurationInSeconds: number;
}

interface VideoSourcedDocumentSummary {
  SourceVideoId: string;
  StructuredLearningDocumentId: string;
  DocumentName: string;
}

function formatDuration(seconds: number): string {
  if (!seconds || seconds <= 0) return '—';
  const mins = Math.floor(seconds / 60);
  const secs = Math.round(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, '0')}`;
}

function formatDate(iso: string): string {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime()) || date.getFullYear() <= 1) return '—';
  return date.toLocaleString();
}

/** Indexing state (Azure VI) is NOT the same as "course created" — see
 * module doc comment. This only reflects whether Azure finished indexing. */
function stateBadge(state: string): { label: string; className: string } {
  const normalized = state?.toLowerCase() ?? '';
  if (normalized === 'processed') return { label: 'Indexado', className: 'bg-sky-500/15 text-sky-300' };
  if (normalized === 'failed') return { label: 'Falló', className: 'bg-red-500/15 text-red-300' };
  if (normalized === 'processing' || normalized === 'uploaded') return { label: 'Procesando', className: 'bg-amber-500/15 text-amber-300' };
  return { label: state || 'Desconocido', className: 'bg-slate-500/15 text-slate-300' };
}

export default function VideosInProgressPage() {
  const navigate = useNavigate();
  const [videos, setVideos] = useState<VideoSummary[] | null>(null);
  const [documentsByVideoId, setDocumentsByVideoId] = useState<Map<string, VideoSourcedDocumentSummary>>(new Map());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  async function loadVideos() {
    setLoading(true);
    setError(null);
    try {
      const [videosRes, documents] = await Promise.all([
        fetch(`${VIDEO_API_BASE}/videos`),
        apiGet<VideoSourcedDocumentSummary[]>('/studio/structured-learning/video/by-source-video').catch(() => []),
      ]);
      if (!videosRes.ok) throw new Error(`HTTP ${videosRes.status}`);
      const data = (await videosRes.json()) as VideoSummary[];
      setVideos(data);
      setDocumentsByVideoId(new Map(documents.map((d) => [d.SourceVideoId, d])));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo cargar la lista de videos.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadVideos();
  }, []);

  async function handleDelete(videoId: string) {
    if (!window.confirm('¿Eliminar esta clase de Azure AI Video Indexer? Esta acción no se puede deshacer.')) return;
    setDeletingId(videoId);
    try {
      const res = await fetch(`${VIDEO_API_BASE}/videos/${videoId}`, { method: 'DELETE' });
      if (!res.ok && res.status !== 204) throw new Error(`HTTP ${res.status}`);
      setVideos((prev) => (prev ? prev.filter((v) => v.VideoId !== videoId) : prev));
    } catch (err) {
      alert(err instanceof Error ? err.message : 'No se pudo eliminar el video.');
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-8">
      <button
        type="button"
        onClick={() => navigate('/')}
        className="mb-6 inline-flex items-center gap-2 text-sm font-medium text-slate-400 hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" />
        Volver
      </button>

      <div className="mb-6 flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <FileVideo className="h-7 w-7 text-rose-400" />
          <div>
            <h1 className="text-xl font-bold text-white">Clases en Proceso</h1>
            <p className="text-sm text-slate-400">
              Clases (videos con Engrams) ya indexadas con Azure AI Video Indexer. Reanuda cualquiera sin volver a subir el archivo.
            </p>
          </div>
        </div>
        <button
          type="button"
          onClick={() => void loadVideos()}
          disabled={loading}
          className="inline-flex items-center gap-2 rounded-lg border border-white/10 bg-slate-900/60 px-3 py-2 text-sm font-medium text-slate-300 hover:bg-slate-800 disabled:opacity-50"
        >
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          Actualizar
        </button>
      </div>

      {loading && !videos && (
        <div className="flex items-center justify-center gap-2 rounded-xl border border-white/10 bg-slate-900/40 py-12 text-slate-400">
          <Loader2 className="h-5 w-5 animate-spin" />
          Cargando videos...
        </div>
      )}

      {error && (
        <div className="mb-4 flex items-center gap-2 rounded-xl border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-300">
          <AlertTriangle className="h-5 w-5 shrink-0" />
          {error}
        </div>
      )}

      {videos && videos.length === 0 && !loading && (
        <div className="rounded-xl border border-white/10 bg-slate-900/40 py-12 text-center text-slate-400">
          Aún no hay clases creadas. Ve a "New Learning From Video" para subir la primera.
        </div>
      )}

      {videos && videos.length > 0 && (
        <div className="overflow-hidden rounded-xl border border-white/10">
          <table className="w-full text-left text-sm">
            <thead className="bg-slate-900/70 text-xs uppercase tracking-wide text-slate-400">
              <tr>
                <th className="px-4 py-3">Clase</th>
                <th className="px-4 py-3">Video ID</th>
                <th className="px-4 py-3">Estado</th>
                <th className="px-4 py-3">Duración</th>
                <th className="px-4 py-3">Subido</th>
                <th className="px-4 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5">
              {videos.map((video) => {
                const badge = stateBadge(video.State);
                const isProcessed = video.State?.toLowerCase() === 'processed';
                const document = documentsByVideoId.get(video.VideoId);
                return (
                  <tr key={video.VideoId} className="bg-slate-900/30 hover:bg-slate-900/50">
                    <td className="px-4 py-3 font-medium text-white">{video.Name || '(sin nombre)'}</td>
                    <td className="px-4 py-3 font-mono text-xs text-slate-400">{video.VideoId}</td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-1.5">
                        <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${badge.className}`}>{badge.label}</span>
                        {document && (
                          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/15 px-2.5 py-1 text-xs font-semibold text-emerald-300">
                            <CheckCircle2 className="h-3.5 w-3.5" />
                            Curso creado
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-4 py-3 text-slate-300">{formatDuration(video.DurationInSeconds)}</td>
                    <td className="px-4 py-3 text-slate-400">{formatDate(video.CreatedUtc)}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-2">
                        {document ? (
                          <button
                            type="button"
                            onClick={() => navigate(`/structured-learning/${document.StructuredLearningDocumentId}`)}
                            className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-500/90 px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-emerald-500"
                          >
                            <CheckCircle2 className="h-3.5 w-3.5" />
                            Ver curso
                          </button>
                        ) : (
                          <button
                            type="button"
                            onClick={() => navigate(`/new-learning-from-video?resume=${encodeURIComponent(video.VideoId)}`)}
                            disabled={!isProcessed && video.State?.toLowerCase() !== 'processing' && video.State?.toLowerCase() !== 'uploaded'}
                            className="inline-flex items-center gap-1.5 rounded-lg bg-amber-500/90 px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-amber-500 disabled:cursor-not-allowed disabled:opacity-40"
                          >
                            <PlayCircle className="h-3.5 w-3.5" />
                            Reanudar
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => void handleDelete(video.VideoId)}
                          disabled={deletingId === video.VideoId}
                          className="inline-flex items-center gap-1.5 rounded-lg border border-red-500/30 bg-red-500/10 px-2.5 py-1.5 text-xs font-semibold text-red-300 hover:bg-red-500/20 disabled:opacity-50"
                        >
                          {deletingId === video.VideoId ? (
                            <Loader2 className="h-3.5 w-3.5 animate-spin" />
                          ) : (
                            <Trash2 className="h-3.5 w-3.5" />
                          )}
                          Eliminar
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
