import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Clapperboard, Film, Loader2, Plus, Trash2 } from 'lucide-react';
import { deleteVideoProduction, getVideoProductionVideoUrl, listVideoProductions, type VideoProduction } from '../lib/api/videoProductionApi';

/** Card grid of every saved Video Production (2026-09-24) — the previous
 * design only offered "Create video" with a buried reopen list, so a
 * produced video effectively "disappeared" once you left the wizard.
 * This is now the "/video-productions" landing page; the wizard itself
 * lives at "/video-productions/new" (create) and
 * "/video-productions/:videoProductionId" (reopen/edit one project). */
export default function VideoProjectsGalleryPage() {
  const navigate = useNavigate();
  const [productions, setProductions] = useState<VideoProduction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // Delete confirmation (2026-09-24) — deleting removes the DB rows AND
  // every blob (all clips + the merged final video), so it's irreversible;
  // a plain click can never trigger it, only an explicit confirm.
  const [pendingDelete, setPendingDelete] = useState<VideoProduction | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  useEffect(() => {
    listVideoProductions()
      .then(setProductions)
      .catch((err) => setError(err instanceof Error ? err.message : 'Could not load your video projects.'))
      .finally(() => setLoading(false));
  }, []);

  async function handleConfirmDelete() {
    if (!pendingDelete) return;
    setDeletingId(pendingDelete.VideoProductionId);
    setDeleteError(null);
    try {
      await deleteVideoProduction(pendingDelete.VideoProductionId);
      setProductions((current) => current.filter((p) => p.VideoProductionId !== pendingDelete.VideoProductionId));
      setPendingDelete(null);
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : 'Could not delete this project.');
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <div className="mx-auto max-w-7xl px-6 py-10 lg:px-10">
      <header className="mb-8 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="mb-3 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-cyan-400">
            <Clapperboard className="h-4 w-4" /> Video Productions
          </div>
          <h1 className="text-3xl font-bold tracking-tight text-white">My video projects</h1>
          <p className="mt-2 max-w-2xl text-sm text-slate-400">Every video project you've saved, most recent first.</p>
        </div>
        <button
          type="button"
          onClick={() => navigate('/video-productions/new')}
          className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-cyan-500 to-blue-600 px-4 py-2.5 text-sm font-semibold text-white shadow-lg shadow-cyan-500/25 transition-transform hover:scale-[1.02] active:scale-[0.99]"
        >
          <Plus className="h-4 w-4" /> Create video
        </button>
      </header>

      {loading && (
        <div className="flex items-center gap-2 text-sm text-slate-400">
          <Loader2 className="h-4 w-4 animate-spin" /> Loading your projects...
        </div>
      )}

      {error && <p className="rounded-lg border border-rose-400/20 bg-rose-500/10 p-3 text-sm text-rose-300">{error}</p>}

      {!loading && !error && productions.length === 0 && (
        <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-10 text-center">
          <Film className="mx-auto mb-3 h-8 w-8 text-slate-600" />
          <p className="text-sm text-slate-400">You haven't created any video projects yet.</p>
          <button
            type="button"
            onClick={() => navigate('/video-productions/new')}
            className="mt-4 inline-flex items-center gap-2 rounded-xl bg-cyan-500/10 px-4 py-2.5 text-xs font-semibold text-cyan-300 hover:bg-cyan-500/20"
          >
            <Plus className="h-4 w-4" /> Create your first video
          </button>
        </div>
      )}

      {!loading && productions.length > 0 && (
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {productions.map((p) => (
            <div
              key={p.VideoProductionId}
              className="group relative flex flex-col overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03] text-left shadow-xl transition-colors hover:border-cyan-400/40"
            >
              <button
                type="button"
                onClick={() => setPendingDelete(p)}
                title="Delete project"
                className="absolute right-2 top-2 z-10 rounded-lg border border-white/10 bg-black/60 p-1.5 text-slate-400 opacity-0 transition-opacity hover:border-rose-400/50 hover:text-rose-300 group-hover:opacity-100"
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
              <button type="button" onClick={() => navigate(`/video-productions/${p.VideoProductionId}`)} className="flex flex-1 flex-col text-left">
                <div className="aspect-video w-full bg-black/40">
                  {p.FinalVideoStoragePath ? (
                    <video src={getVideoProductionVideoUrl(p.VideoProductionId)} className="h-full w-full object-cover" muted />
                  ) : (
                    <div className="flex h-full w-full items-center justify-center">
                      <Film className="h-8 w-8 text-slate-700" />
                    </div>
                  )}
                </div>
                <div className="flex flex-1 flex-col gap-1 p-4">
                  <span className="truncate text-sm font-semibold text-white group-hover:text-cyan-300">{p.Title || 'Untitled project'}</span>
                  <span className="text-[11px] text-slate-500">{new Date(p.CreatedDate).toLocaleDateString()}</span>
                  <span className="mt-1 text-[11px] font-medium uppercase tracking-wider text-slate-600">
                    {p.FinalVideoStoragePath ? 'Merged video ready' : 'Draft — not merged yet'}
                  </span>
                </div>
              </button>
            </div>
          ))}
        </div>
      )}

      {pendingDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-6" onClick={() => (deletingId ? null : setPendingDelete(null))}>
          <div
            className="w-full max-w-md rounded-2xl border border-white/10 bg-slate-900 p-6 shadow-2xl"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="mb-3 flex items-center gap-2 text-rose-300">
              <Trash2 className="h-5 w-5" />
              <h2 className="text-lg font-semibold text-white">Delete this project?</h2>
            </div>
            <p className="text-sm text-slate-400">
              This permanently deletes <strong className="text-slate-200">{pendingDelete.Title || 'Untitled project'}</strong> — every generated clip,
              its prompts, and the merged final video, if any. This cannot be undone.
            </p>
            {deleteError && <p className="mt-3 rounded-lg border border-rose-400/20 bg-rose-500/10 p-2 text-xs text-rose-300">{deleteError}</p>}
            <div className="mt-5 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setPendingDelete(null)}
                disabled={deletingId !== null}
                className="rounded-lg border border-white/10 px-4 py-2 text-xs font-medium text-slate-300 hover:bg-white/5 disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={() => void handleConfirmDelete()}
                disabled={deletingId !== null}
                className="flex items-center gap-2 rounded-lg bg-rose-500 px-4 py-2 text-xs font-semibold text-white hover:bg-rose-400 disabled:opacity-50"
              >
                {deletingId ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
                {deletingId ? 'Deleting...' : 'Delete permanently'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
