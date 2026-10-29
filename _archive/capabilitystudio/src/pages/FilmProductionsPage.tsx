import { useEffect, useRef, useState } from 'react';
import {
  Clapperboard,
  Film,
  Wand2,
  UploadCloud,
  X,
  Loader2,
  CheckCircle2,
  XCircle,
  ArrowRight,
  Camera,
  Users,
  MessageSquare,
  MapPin,
  Trash2,
  DollarSign,
  Video,
  PlayCircle,
} from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { readFileAsBase64 } from '../lib/api/pdfPipelineApi';
import {
  createFilmProduction,
  createFilmScene,
  createFilmScenesFromScript,
  deleteFilmScene,
  generateFilmProductionVideos,
  generateFilmSceneVideo,
  getFilmProductionCost,
  getFilmProductionVideoUrl,
  getFilmSceneImageUrl,
  getFilmSceneVideoUrl,
  listFilmProductions,
  listFilmScenes,
  mergeFilmProductionVideos,
  retryFilmScene,
  type FilmProduction,
  type FilmProductionCost,
  type FilmScene,
  type FilmSceneAnchorImageMode,
} from '../lib/api/filmProductionApi';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

const ANCHOR_MODES: { value: FilmSceneAnchorImageMode; label: string; hint: string }[] = [
  { value: 'Uploaded', label: 'Upload my photo', hint: "We'll place your real photo into the environment you describe." },
  { value: 'GeneratedFromScenePrompt', label: 'Generate from scene prompt', hint: 'No upload — the whole frame is generated from the polished scene prompt.' },
  { value: 'GeneratedFromCustomPrompt', label: 'Generate from a custom prompt', hint: 'No upload — write your own separate prompt just for the image.' },
];

export default function FilmProductionsPage() {
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;

  const [production, setProduction] = useState<FilmProduction | null>(null);
  const [title, setTitle] = useState('');
  const [creatingProduction, setCreatingProduction] = useState(false);
  const [productionError, setProductionError] = useState<string | null>(null);

  const [scenes, setScenes] = useState<FilmScene[]>([]);
  const [selectedScene, setSelectedScene] = useState<FilmScene | null>(null);
  const [wizardMode, setWizardMode] = useState<'manual' | 'script'>('manual');
  const [cost, setCost] = useState<FilmProductionCost | null>(null);
  const [generatingAllVideos, setGeneratingAllVideos] = useState(false);
  const [videosError, setVideosError] = useState<string | null>(null);
  const [merging, setMerging] = useState(false);
  const [mergeError, setMergeError] = useState<string | null>(null);

  const [existingProductions, setExistingProductions] = useState<FilmProduction[]>([]);
  const [loadingExisting, setLoadingExisting] = useState(true);
  const [reopeningId, setReopeningId] = useState<string | null>(null);

  useEffect(() => {
    if (production) return;
    listFilmProductions()
      .then(setExistingProductions)
      .catch(() => {
        // Best-effort — the "reopen" list just stays empty if this fails.
      })
      .finally(() => setLoadingExisting(false));
  }, [production]);

  useEffect(() => {
    if (!production) return;
    getFilmProductionCost(production.FilmProductionId)
      .then(setCost)
      .catch(() => {
        // Best-effort — cost panel just stays hidden if this fails.
      });
  }, [production, scenes]);

  async function handleReopenProduction(target: FilmProduction) {
    setReopeningId(target.FilmProductionId);
    setProductionError(null);
    try {
      const loadedScenes = await listFilmScenes(target.FilmProductionId);
      setScenes(loadedScenes);
      setProduction(target);
    } catch (err) {
      setProductionError(err instanceof Error ? err.message : 'Failed to reopen this production.');
    } finally {
      setReopeningId(null);
    }
  }

  async function handleDeleteScene(sceneId: string) {
    await deleteFilmScene(sceneId);
    setScenes((prev) => prev.filter((s) => s.FilmSceneId !== sceneId));
    setSelectedScene((prev) => (prev?.FilmSceneId === sceneId ? null : prev));
  }

  function applyUpdatedScene(updated: FilmScene) {
    setScenes((prev) => prev.map((s) => (s.FilmSceneId === updated.FilmSceneId ? updated : s)));
    setSelectedScene((prev) => (prev?.FilmSceneId === updated.FilmSceneId ? updated : prev));
  }

  async function handleGenerateSceneVideo(sceneId: string) {
    const updated = await generateFilmSceneVideo(sceneId);
    applyUpdatedScene(updated);
  }

  async function handleRetryScene(sceneId: string) {
    const updated = await retryFilmScene(sceneId);
    applyUpdatedScene(updated);
  }

  async function handleMergeAllScenes() {
    if (!production || merging) return;
    setMerging(true);
    setMergeError(null);
    try {
      const updated = await mergeFilmProductionVideos(production.FilmProductionId);
      setProduction(updated);
    } catch (err) {
      setMergeError(err instanceof Error ? err.message : 'Failed to merge scenes.');
    } finally {
      setMerging(false);
    }
  }

  async function handleGenerateAllVideos() {
    if (!production || generatingAllVideos) return;
    setGeneratingAllVideos(true);
    setVideosError(null);
    try {
      const updated = await generateFilmProductionVideos(production.FilmProductionId);
      setScenes((prev) => {
        const byId = new Map(updated.map((s) => [s.FilmSceneId, s]));
        return prev.map((s) => byId.get(s.FilmSceneId) ?? s);
      });
    } catch (err) {
      setVideosError(err instanceof Error ? err.message : 'Failed to generate videos.');
    } finally {
      setGeneratingAllVideos(false);
    }
  }

  async function handleStartProduction() {
    if (!title.trim()) return;
    setCreatingProduction(true);
    setProductionError(null);
    try {
      const created = await createFilmProduction({ tenantId, title: title.trim() });
      setProduction(created);
    } catch (err) {
      setProductionError(err instanceof Error ? err.message : 'Failed to start the production.');
    } finally {
      setCreatingProduction(false);
    }
  }

  if (!production) {
    return (
      <div className="relative mx-auto flex min-h-[80vh] max-w-3xl flex-col items-center justify-center px-6 py-16 text-center">
        <div className="pointer-events-none absolute inset-0 -z-10 overflow-hidden">
          <div className="absolute left-1/2 top-0 h-96 w-96 -translate-x-1/2 rounded-full bg-amber-500/10 blur-3xl" />
          <div className="absolute bottom-0 right-1/4 h-72 w-72 rounded-full bg-rose-500/10 blur-3xl" />
        </div>

        <div className="mb-6 flex h-20 w-20 items-center justify-center rounded-3xl bg-gradient-to-br from-amber-500 to-rose-600 shadow-2xl shadow-amber-500/30">
          <Clapperboard className="h-10 w-10 text-white" />
        </div>
        <h1 className="text-3xl font-bold tracking-tight text-white">Film Productions</h1>
        <p className="mt-2 max-w-md text-sm text-slate-400">
          Direct a short film, scene by scene. Two AI agents turn your notes into a polished cinematic
          prompt and a final composited image — automatically.
        </p>

        <div className="mt-10 w-full rounded-2xl border border-white/10 bg-white/[0.02] p-6 shadow-xl">
          <label className="mb-2 block text-left text-xs font-semibold uppercase tracking-wider text-slate-500">
            Production title
          </label>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleStartProduction()}
            placeholder="e.g. The Last Signal"
            className="w-full rounded-xl border border-white/10 bg-black/30 px-4 py-3 text-white placeholder:text-slate-600 focus:border-amber-400/50 focus:outline-none focus:ring-2 focus:ring-amber-400/20"
            autoFocus
          />
          {productionError && <p className="mt-2 text-left text-sm text-rose-400">{productionError}</p>}
          <button
            type="button"
            onClick={handleStartProduction}
            disabled={!title.trim() || creatingProduction}
            className="mt-4 flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-500 to-rose-600 py-3 text-sm font-semibold text-white shadow-lg shadow-amber-500/25 transition-transform hover:scale-[1.01] active:scale-[0.99] disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:scale-100"
          >
            {creatingProduction ? <Loader2 className="h-4 w-4 animate-spin" /> : <Film className="h-4 w-4" />}
            Start production
          </button>
        </div>

        {!loadingExisting && existingProductions.length > 0 && (
          <div className="mt-8 w-full rounded-2xl border border-white/10 bg-white/[0.02] p-6 text-left shadow-xl">
            <h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-slate-500">
              Or reopen a production
            </h2>
            <div className="space-y-2">
              {existingProductions.map((p) => (
                <button
                  key={p.FilmProductionId}
                  type="button"
                  onClick={() => handleReopenProduction(p)}
                  disabled={reopeningId !== null}
                  className="flex w-full items-center justify-between rounded-xl border border-white/10 bg-white/[0.02] px-4 py-3 text-left transition-colors hover:border-amber-400/40 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <span className="truncate text-sm text-white">{p.Title}</span>
                  {reopeningId === p.FilmProductionId ? (
                    <Loader2 className="h-4 w-4 shrink-0 animate-spin text-amber-400" />
                  ) : (
                    <ArrowRight className="h-4 w-4 shrink-0 text-slate-500" />
                  )}
                </button>
              ))}
            </div>
          </div>
        )}
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl px-6 py-10">
      <header className="mb-8 flex items-center gap-4">
        <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-500 to-rose-600 shadow-lg shadow-amber-500/20">
          <Clapperboard className="h-7 w-7 text-white" />
        </div>
        <div>
          <div className="text-[11px] font-semibold uppercase tracking-[0.18em] text-amber-400/80">Film Production</div>
          <h1 className="text-2xl font-bold tracking-tight text-white">{production.Title}</h1>
        </div>
        <div className="ml-auto rounded-full border border-white/10 bg-white/[0.04] px-4 py-1.5 text-xs font-medium text-slate-300">
          {scenes.length} {scenes.length === 1 ? 'scene' : 'scenes'}
        </div>
      </header>

      {cost && cost.SceneCount > 0 && (
        <div className="mb-6 flex flex-wrap items-center gap-x-6 gap-y-2 rounded-2xl border border-white/10 bg-white/[0.02] px-5 py-3 text-xs text-slate-400">
          <div className="flex items-center gap-1.5 font-semibold text-emerald-400">
            <DollarSign className="h-3.5 w-3.5" />
            {'$' + cost.EstimatedCostUsd.toFixed(4)} estimated
          </div>
          <div>{cost.CompletedImageCount} images</div>
          <div>{cost.CompletedVideoCount} videos</div>
          <div>{(cost.TotalInputTokens + cost.TotalOutputTokens).toLocaleString()} tokens (in+out)</div>
        </div>
      )}

      <div className="mb-6 inline-flex rounded-xl border border-white/10 bg-white/[0.02] p-1">
        <button
          type="button"
          onClick={() => setWizardMode('manual')}
          className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${
            wizardMode === 'manual' ? 'bg-amber-500/15 text-amber-300' : 'text-slate-400 hover:text-white'
          }`}
        >
          Scene by scene
        </button>
        <button
          type="button"
          onClick={() => setWizardMode('script')}
          className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${
            wizardMode === 'script' ? 'bg-amber-500/15 text-amber-300' : 'text-slate-400 hover:text-white'
          }`}
        >
          Full script (auto-storyboard)
        </button>
      </div>

      {wizardMode === 'manual' ? (
        <SceneForm
          tenantId={tenantId}
          production={production}
          nextSortOrder={scenes.length + 1}
          onSceneCreated={(scene) => setScenes((prev) => [...prev, scene])}
        />
      ) : (
        <ScriptForm
          tenantId={tenantId}
          production={production}
          onScenesCreated={(newScenes) => setScenes((prev) => [...prev, ...newScenes])}
        />
      )}

      {scenes.length > 0 && (
        <div className="mt-12">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wider text-slate-500">
              <Film className="h-4 w-4" /> Storyboard
            </h2>
            <button
              type="button"
              onClick={handleGenerateAllVideos}
              disabled={generatingAllVideos || !scenes.some((s) => s.Status === 2 && !s.FinalVideoStoragePath)}
              className="flex items-center gap-2 rounded-xl border border-amber-400/30 bg-amber-500/10 px-4 py-2 text-xs font-semibold text-amber-300 transition-colors hover:bg-amber-500/20 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {generatingAllVideos ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Video className="h-3.5 w-3.5" />}
              Generate all videos
            </button>
            <button
              type="button"
              onClick={handleMergeAllScenes}
              disabled={
                merging ||
                scenes.filter((s) => s.Status === 2).length < 2 ||
                scenes.some((s) => s.Status === 2 && !s.FinalVideoStoragePath)
              }
              className="flex items-center gap-2 rounded-xl border border-emerald-400/30 bg-emerald-500/10 px-4 py-2 text-xs font-semibold text-emerald-300 transition-colors hover:bg-emerald-500/20 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {merging ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Film className="h-3.5 w-3.5" />}
              Merge all scenes
            </button>
          </div>
          {videosError && <p className="mb-4 text-xs text-rose-400">{videosError}</p>}
          {mergeError && <p className="mb-4 text-xs text-rose-400">{mergeError}</p>}
          {production.FinalFilmStoragePath && (
            <div className="mb-6 rounded-2xl border border-emerald-400/20 bg-emerald-500/5 p-4">
              <div className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-emerald-400">
                <Film className="h-4 w-4" /> Final film
              </div>
              <video
                key={production.FinalFilmStoragePath}
                src={getFilmProductionVideoUrl(production.FilmProductionId)}
                controls
                className="w-full rounded-xl bg-black"
              />
            </div>
          )}
          <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
            {scenes.map((scene) => (
              <SceneCard
                key={scene.FilmSceneId}
                scene={scene}
                onOpen={() => setSelectedScene(scene)}
                onDelete={() => handleDeleteScene(scene.FilmSceneId)}
                onRetryVideo={() => handleGenerateSceneVideo(scene.FilmSceneId)}
              />
            ))}
          </div>
        </div>
      )}

      {selectedScene && (
        <SceneDetailModal
          scene={selectedScene}
          onClose={() => setSelectedScene(null)}
          onDelete={() => handleDeleteScene(selectedScene.FilmSceneId)}
          onGenerateVideo={() => handleGenerateSceneVideo(selectedScene.FilmSceneId)}
          onRetry={() => handleRetryScene(selectedScene.FilmSceneId)}
        />
      )}
    </div>
  );
}

function SceneForm({
  tenantId,
  production,
  nextSortOrder,
  onSceneCreated,
}: {
  tenantId: string;
  production: FilmProduction;
  nextSortOrder: number;
  onSceneCreated: (scene: FilmScene) => void;
}) {
  const [description, setDescription] = useState('');
  const [dialogueText, setDialogueText] = useState('');
  const [characters, setCharacters] = useState('');
  const [cameraMovements, setCameraMovements] = useState('');
  const [environment, setEnvironment] = useState('');
  const [anchorMode, setAnchorMode] = useState<FilmSceneAnchorImageMode>('GeneratedFromScenePrompt');
  const [customImagePrompt, setCustomImagePrompt] = useState('');
  const [anchorFile, setAnchorFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

  function handleFileSelected(file: File | null) {
    setAnchorFile(file);
    setPreviewUrl((prev) => {
      if (prev) URL.revokeObjectURL(prev);
      return file ? URL.createObjectURL(file) : null;
    });
  }

  const canSubmit =
    description.trim().length > 0 &&
    environment.trim().length > 0 &&
    (anchorMode !== 'Uploaded' || !!anchorFile) &&
    (anchorMode !== 'GeneratedFromCustomPrompt' || customImagePrompt.trim().length > 0);

  async function handleSubmit() {
    if (!canSubmit || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      const anchorImageBase64 = anchorFile ? await readFileAsBase64(anchorFile) : null;
      const scene = await createFilmScene(production.FilmProductionId, {
        tenantId,
        sortOrder: nextSortOrder,
        description: description.trim(),
        dialogueText: dialogueText.trim() || null,
        characters: characters.trim() || null,
        cameraMovements: cameraMovements.trim() || null,
        environment: environment.trim(),
        anchorImageMode: anchorMode,
        customImagePrompt: anchorMode === 'GeneratedFromCustomPrompt' ? customImagePrompt.trim() : null,
        anchorImageBase64,
      });
      onSceneCreated(scene);
      // Reset for the next loop iteration.
      setDescription('');
      setDialogueText('');
      setCharacters('');
      setCameraMovements('');
      setEnvironment('');
      setCustomImagePrompt('');
      handleFileSelected(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to generate the scene.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="rounded-3xl border border-white/10 bg-gradient-to-b from-white/[0.03] to-transparent p-6 shadow-2xl sm:p-8">
      <div className="mb-6 flex items-center gap-2">
        <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-amber-500/15">
          <Camera className="h-4.5 w-4.5 text-amber-400" />
        </div>
        <h2 className="text-lg font-semibold text-white">Scene {nextSortOrder}</h2>
      </div>

      <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
        <Field icon={<Film className="h-4 w-4" />} label="Description" required span2>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="What happens in this scene..."
            rows={3}
            className={inputClass}
          />
        </Field>

        <Field icon={<MessageSquare className="h-4 w-4" />} label="Dialogue / narration">
          <textarea
            value={dialogueText}
            onChange={(e) => setDialogueText(e.target.value)}
            placeholder="What is said..."
            rows={2}
            className={inputClass}
          />
        </Field>

        <Field icon={<Users className="h-4 w-4" />} label="Characters">
          <input
            value={characters}
            onChange={(e) => setCharacters(e.target.value)}
            placeholder="Who's in the scene..."
            className={inputClass}
          />
        </Field>

        <Field icon={<Camera className="h-4 w-4" />} label="Camera movements">
          <input
            value={cameraMovements}
            onChange={(e) => setCameraMovements(e.target.value)}
            placeholder="slow zoom in, pan left..."
            className={inputClass}
          />
        </Field>

        <Field icon={<MapPin className="h-4 w-4" />} label="Environment" required>
          <input
            value={environment}
            onChange={(e) => setEnvironment(e.target.value)}
            placeholder="Where the anchor image will be placed..."
            className={inputClass}
          />
        </Field>
      </div>

      <div className="mt-6">
        <label className="mb-2 block text-xs font-semibold uppercase tracking-wider text-slate-500">
          Anchor image
        </label>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
          {ANCHOR_MODES.map((mode) => (
            <button
              key={mode.value}
              type="button"
              onClick={() => setAnchorMode(mode.value)}
              className={`rounded-xl border p-3 text-left transition-colors ${
                anchorMode === mode.value
                  ? 'border-amber-400/50 bg-amber-500/10'
                  : 'border-white/10 bg-white/[0.02] hover:border-white/20'
              }`}
            >
              <div className="text-sm font-medium text-white">{mode.label}</div>
              <div className="mt-1 text-xs text-slate-400">{mode.hint}</div>
            </button>
          ))}
        </div>

        {anchorMode === 'Uploaded' && (
          <div className="mt-4">
            {previewUrl ? (
              <div className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.04] p-4">
                <img src={previewUrl} alt="Anchor preview" className="h-16 w-16 rounded-xl object-cover" />
                <div className="min-w-0 flex-1 truncate text-sm text-slate-300">{anchorFile?.name}</div>
                <button
                  type="button"
                  onClick={() => handleFileSelected(null)}
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-white/10 hover:text-white"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
            ) : (
              <label className="flex cursor-pointer flex-col items-center gap-2 rounded-2xl border-2 border-dashed border-white/15 bg-white/[0.02] p-8 text-center hover:border-amber-400/40">
                <UploadCloud className="h-6 w-6 text-slate-400" />
                <span className="text-sm text-slate-400">Click to upload a photo</span>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  className="hidden"
                  onChange={(e) => handleFileSelected(e.target.files?.[0] ?? null)}
                />
              </label>
            )}
          </div>
        )}

        {anchorMode === 'GeneratedFromCustomPrompt' && (
          <textarea
            value={customImagePrompt}
            onChange={(e) => setCustomImagePrompt(e.target.value)}
            placeholder="Write the exact image prompt to use instead of the scene's narrative prompt..."
            rows={3}
            className={`${inputClass} mt-4`}
          />
        )}
      </div>

      {error && <p className="mt-4 text-sm text-rose-400">{error}</p>}

      <button
        type="button"
        onClick={handleSubmit}
        disabled={!canSubmit || submitting}
        className="mt-6 flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-500 to-rose-600 py-3 text-sm font-semibold text-white shadow-lg shadow-amber-500/25 transition-transform hover:scale-[1.005] active:scale-[0.995] disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:scale-100 sm:w-auto sm:px-8"
      >
        {submitting ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" /> Directing scene...
          </>
        ) : (
          <>
            <Wand2 className="h-4 w-4" /> Generate scene <ArrowRight className="h-4 w-4" />
          </>
        )}
      </button>
    </div>
  );
}

const inputClass =
  'w-full rounded-xl border border-white/10 bg-black/30 px-4 py-2.5 text-sm text-white placeholder:text-slate-600 focus:border-amber-400/50 focus:outline-none focus:ring-2 focus:ring-amber-400/20';

function ScriptForm({
  tenantId,
  production,
  onScenesCreated,
}: {
  tenantId: string;
  production: FilmProduction;
  onScenesCreated: (scenes: FilmScene[]) => void;
}) {
  const scriptDraftKey = `film-production-script-draft:${production.FilmProductionId}`;

  const [scriptText, setScriptText] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem(scriptDraftKey) ?? '{}').scriptText ?? '';
    } catch {
      return '';
    }
  });
  const [environmentsDescription, setEnvironmentsDescription] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem(scriptDraftKey) ?? '{}').environmentsDescription ?? '';
    } catch {
      return '';
    }
  });
  const [maxScenes, setMaxScenes] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem(scriptDraftKey) ?? '{}').maxScenes ?? 8;
    } catch {
      return 8;
    }
  });
  const [anchorFile, setAnchorFile] = useState<File | null>(null);
  const [anchorPreviewUrl, setAnchorPreviewUrl] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Save the draft on every change — the ORIGINAL script text must never be
  // silently lost, even if generation fails partway through or the user
  // navigates away/reloads before finishing.
  useEffect(() => {
    localStorage.setItem(scriptDraftKey, JSON.stringify({ scriptText, environmentsDescription, maxScenes }));
  }, [scriptDraftKey, scriptText, environmentsDescription, maxScenes]);

  async function handleAnchorFileSelected(file: File | null) {
    setAnchorFile(file);
    setAnchorPreviewUrl(file ? URL.createObjectURL(file) : null);
  }

  async function handleSubmit() {
    if (!scriptText.trim() || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      const initialAnchorImageBase64 = anchorFile ? await readFileAsBase64(anchorFile) : null;
      const scenes = await createFilmScenesFromScript(production.FilmProductionId, {
        tenantId,
        fullScriptText: scriptText.trim(),
        environmentsDescription: environmentsDescription.trim() || null,
        maxScenes,
        initialAnchorImageBase64,
      });
      onScenesCreated(scenes);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to break down the script.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="rounded-3xl border border-white/10 bg-gradient-to-b from-white/[0.03] to-transparent p-6 shadow-2xl sm:p-8">
      <div className="mb-6 flex items-center gap-2">
        <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-amber-500/15">
          <Wand2 className="h-4.5 w-4.5 text-amber-400" />
        </div>
        <div>
          <h2 className="text-lg font-semibold text-white">Full script</h2>
          <p className="text-xs text-slate-400">
            Paste your whole script or story — an AI agent breaks it into 10-second scenes with narrative
            continuity, then generates every prompt and image automatically.
          </p>
        </div>
      </div>

      <textarea
        value={scriptText}
        onChange={(e) => setScriptText(e.target.value)}
        placeholder="Paste your full script or story here..."
        rows={10}
        className={inputClass}
        disabled={submitting}
      />

      <div className="mt-5">
        <label className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-500">
          <MapPin className="h-3.5 w-3.5 text-slate-600" />
          Environments (optional)
        </label>
        <p className="mb-2 text-xs text-slate-500">
          Describe the film's recurring locations so every scene that returns to one stays visually
          consistent — e.g. "Classroom: bright modern classroom, rows of desks, whiteboard up front.
          Hallway: long school hallway, lockers on both sides, fluorescent lights."
        </p>
        <textarea
          value={environmentsDescription}
          onChange={(e) => setEnvironmentsDescription(e.target.value)}
          placeholder="e.g. Classroom: ... / Hallway: ... / Office: ..."
          rows={4}
          className={inputClass}
          disabled={submitting}
        />
      </div>

      <div className="mt-5">
        <label className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-500">
          <UploadCloud className="h-3.5 w-3.5 text-slate-600" />
          Anchor photo (optional)
        </label>
        <p className="mb-2 text-xs text-slate-500">
          Upload a real photo (e.g. the teacher/host) to start from — every scene after the first then
          chains from the PREVIOUS scene's own final image, so the same person and look carry through
          the whole film instead of each scene looking unrelated.
        </p>
        {anchorPreviewUrl ? (
          <div className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.04] p-4">
            <img src={anchorPreviewUrl} alt="Anchor preview" className="h-16 w-16 rounded-xl object-cover" />
            <div className="min-w-0 flex-1 truncate text-sm text-slate-300">{anchorFile?.name}</div>
            <button
              type="button"
              onClick={() => handleAnchorFileSelected(null)}
              disabled={submitting}
              className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-slate-400 hover:bg-white/10 hover:text-white"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        ) : (
          <label className="flex cursor-pointer flex-col items-center gap-2 rounded-2xl border-2 border-dashed border-white/15 bg-white/[0.02] p-6 text-center hover:border-amber-400/40">
            <UploadCloud className="h-5 w-5 text-slate-400" />
            <span className="text-xs text-slate-400">Click to upload a photo</span>
            <input
              type="file"
              accept="image/*"
              className="hidden"
              disabled={submitting}
              onChange={(e) => handleAnchorFileSelected(e.target.files?.[0] ?? null)}
            />
          </label>
        )}
      </div>

      <div className="mt-5 max-w-xs">
        <label className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-500">
          <Film className="h-3.5 w-3.5 text-slate-600" />
          Max scenes
        </label>
        <p className="mb-2 text-xs text-slate-500">
          Caps how many scenes the agent can break the script into — a dense script combines beats
          into fewer scenes instead of exceeding this.
        </p>
        <input
          type="number"
          min={1}
          max={20}
          value={maxScenes}
          onChange={(e) => setMaxScenes(Math.min(20, Math.max(1, Number(e.target.value) || 1)))}
          className={inputClass}
          disabled={submitting}
        />
      </div>

      {error && <p className="mt-4 text-sm text-rose-400">{error}</p>}

      <button
        type="button"
        onClick={handleSubmit}
        disabled={!scriptText.trim() || submitting}
        className="mt-6 flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-amber-500 to-rose-600 py-3 text-sm font-semibold text-white shadow-lg shadow-amber-500/25 transition-transform hover:scale-[1.005] active:scale-[0.995] disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:scale-100 sm:w-auto sm:px-8"
      >
        {submitting ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" /> Storyboarding the whole film...
          </>
        ) : (
          <>
            <Wand2 className="h-4 w-4" /> Generate all scenes <ArrowRight className="h-4 w-4" />
          </>
        )}
      </button>
      {submitting && (
        <p className="mt-2 text-xs text-slate-500">
          This can take a few minutes for a long script — each scene's image is generated one at a time.
        </p>
      )}
    </div>
  );
}

function Field({
  icon,
  label,
  required,
  span2,
  children,
}: {
  icon: React.ReactNode;
  label: string;
  required?: boolean;
  span2?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className={span2 ? 'md:col-span-2' : undefined}>
      <label className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-500">
        <span className="text-slate-600">{icon}</span>
        {label}
        {required && <span className="text-amber-400">*</span>}
      </label>
      {children}
    </div>
  );
}

function SceneCard({
  scene,
  onOpen,
  onDelete,
  onRetryVideo,
}: {
  scene: FilmScene;
  onOpen: () => void;
  onDelete: () => Promise<void>;
  onRetryVideo: () => Promise<void>;
}) {
  const isCompleted = scene.Status === 2;
  const isFailed = scene.Status === 3;
  const [deleting, setDeleting] = useState(false);
  const [retryingVideo, setRetryingVideo] = useState(false);

  async function handleDeleteClick(e: React.MouseEvent) {
    e.stopPropagation();
    if (deleting || !window.confirm('Delete this scene? This cannot be undone.')) return;
    setDeleting(true);
    try {
      await onDelete();
    } catch {
      setDeleting(false);
    }
  }

  async function handleRetryVideoClick(e: React.MouseEvent) {
    e.stopPropagation();
    if (retryingVideo) return;
    setRetryingVideo(true);
    try {
      await onRetryVideo();
    } finally {
      setRetryingVideo(false);
    }
  }

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={onOpen}
      onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && onOpen()}
      className="group cursor-pointer overflow-hidden rounded-2xl border border-white/10 bg-white/[0.02] text-left shadow-lg transition-transform hover:-translate-y-0.5 hover:shadow-xl"
    >
      <div className="relative aspect-video w-full overflow-hidden bg-black/40">
        {isCompleted && scene.FinalImageStoragePath ? (
          <img
            src={getFilmSceneImageUrl(scene.FilmSceneId)}
            alt={scene.SuggestedSceneName ?? 'Scene'}
            className="h-full w-full object-cover transition-transform duration-500 group-hover:scale-105"
          />
        ) : isFailed ? (
          <div className="flex h-full w-full flex-col items-center justify-center gap-2 text-rose-400">
            <XCircle className="h-8 w-8" />
            <span className="text-xs">Generation failed</span>
          </div>
        ) : (
          <div className="flex h-full w-full flex-col items-center justify-center gap-2 text-slate-500">
            <Loader2 className="h-8 w-8 animate-spin" />
            <span className="text-xs">Directing...</span>
          </div>
        )}
        <div className="absolute left-2 top-2 rounded-full bg-black/60 px-2.5 py-1 text-[10px] font-semibold uppercase tracking-wider text-amber-300 backdrop-blur-sm">
          Scene {scene.SortOrder}
        </div>
        <div className="absolute right-2 top-2 flex items-center gap-1.5">
          {isCompleted && scene.FinalVideoStoragePath && (
            <div className="flex h-6 w-6 items-center justify-center rounded-full bg-sky-500/90" title="Video ready">
              <Video className="h-3.5 w-3.5 text-white" />
            </div>
          )}
          {isCompleted && scene.VideoStatus === 3 && !scene.FinalVideoStoragePath && (
            <button
              type="button"
              onClick={handleRetryVideoClick}
              disabled={retryingVideo}
              title={scene.VideoErrorMessage ? `Retry video: ${scene.VideoErrorMessage}` : 'Retry video'}
              className="flex h-6 w-6 items-center justify-center rounded-full bg-rose-500/90 text-white backdrop-blur-sm transition-colors hover:bg-rose-500 disabled:opacity-50"
            >
              {retryingVideo ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Video className="h-3.5 w-3.5" />}
            </button>
          )}
          {isCompleted && (
            <div className="flex h-6 w-6 items-center justify-center rounded-full bg-emerald-500/90">
              <CheckCircle2 className="h-3.5 w-3.5 text-white" />
            </div>
          )}
          <button
            type="button"
            onClick={handleDeleteClick}
            disabled={deleting}
            title="Delete scene"
            className="flex h-6 w-6 items-center justify-center rounded-full bg-black/60 text-slate-300 backdrop-blur-sm transition-colors hover:bg-rose-500/90 hover:text-white disabled:opacity-50"
          >
            {deleting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
          </button>
        </div>
      </div>
      <div className="p-4">
        <h3 className="truncate text-sm font-semibold text-white">
          {scene.SuggestedSceneName ?? scene.Description.slice(0, 40)}
        </h3>
        {scene.GeneratedPrompt && (
          <p className="mt-1.5 line-clamp-3 text-xs text-slate-400">{scene.GeneratedPrompt}</p>
        )}
        {isFailed && scene.ErrorMessage && (
          <p className="mt-1.5 text-xs text-rose-400">{scene.ErrorMessage}</p>
        )}
      </div>
    </div>
  );
}

const ANCHOR_MODE_LABELS: Record<number, string> = {
  0: 'Uploaded photo',
  1: 'Generated from scene prompt',
  2: 'Generated from custom prompt',
};

function SceneDetailModal({
  scene,
  onClose,
  onDelete,
  onGenerateVideo,
  onRetry,
}: {
  scene: FilmScene;
  onClose: () => void;
  onDelete: () => Promise<void>;
  onGenerateVideo: () => Promise<void>;
  onRetry: () => Promise<void>;
}) {
  const isCompleted = scene.Status === 2;
  const isFailed = scene.Status === 3;
  const [deleting, setDeleting] = useState(false);
  const [retrying, setRetrying] = useState(false);
  const [retryError, setRetryError] = useState<string | null>(null);
  const [generatingVideo, setGeneratingVideo] = useState(false);
  const [videoError, setVideoError] = useState<string | null>(null);

  async function handleRetryClick() {
    if (retrying) return;
    setRetrying(true);
    setRetryError(null);
    try {
      await onRetry();
    } catch (err) {
      setRetryError(err instanceof Error ? err.message : 'Failed to retry this scene.');
    } finally {
      setRetrying(false);
    }
  }

  async function handleGenerateVideoClick() {
    if (generatingVideo) return;
    setGeneratingVideo(true);
    setVideoError(null);
    try {
      await onGenerateVideo();
    } catch (err) {
      setVideoError(err instanceof Error ? err.message : 'Failed to generate video.');
    } finally {
      setGeneratingVideo(false);
    }
  }

  async function handleDeleteClick() {
    if (deleting || !window.confirm('Delete this scene? This cannot be undone.')) return;
    setDeleting(true);
    try {
      await onDelete();
    } catch {
      setDeleting(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 px-4 py-8 backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-3xl border border-white/10 bg-slate-950 shadow-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="relative aspect-video w-full overflow-hidden bg-black/40">
          {isCompleted && scene.FinalImageStoragePath && (
            <img
              src={getFilmSceneImageUrl(scene.FilmSceneId)}
              alt={scene.SuggestedSceneName ?? 'Scene'}
              className="h-full w-full object-cover"
            />
          )}
          <button
            type="button"
            onClick={onClose}
            className="absolute right-3 top-3 flex h-9 w-9 items-center justify-center rounded-full bg-black/60 text-white backdrop-blur-sm hover:bg-black/80"
          >
            <X className="h-4 w-4" />
          </button>
          <div className="absolute left-3 top-3 rounded-full bg-black/60 px-3 py-1 text-xs font-semibold uppercase tracking-wider text-amber-300 backdrop-blur-sm">
            Scene {scene.SortOrder}
          </div>
        </div>

        <div className="p-6 sm:p-8">
          <h2 className="text-xl font-bold text-white">
            {scene.SuggestedSceneName ?? `Scene ${scene.SortOrder}`}
          </h2>

          <div className="mt-6 space-y-5">
            <DetailField icon={<Film className="h-4 w-4" />} label="Description" value={scene.Description} />
            <DetailField icon={<MessageSquare className="h-4 w-4" />} label="Dialogue / narration" value={scene.DialogueText} />
            <DetailField icon={<Users className="h-4 w-4" />} label="Characters" value={scene.Characters} />
            <DetailField icon={<Camera className="h-4 w-4" />} label="Camera movements" value={scene.CameraMovements} />
            <DetailField icon={<MapPin className="h-4 w-4" />} label="Environment" value={scene.Environment} />
            <DetailField
              icon={<UploadCloud className="h-4 w-4" />}
              label="Anchor image mode"
              value={ANCHOR_MODE_LABELS[scene.AnchorImageMode] ?? String(scene.AnchorImageMode)}
            />
            {scene.CustomImagePrompt && (
              <DetailField icon={<Wand2 className="h-4 w-4" />} label="Custom image prompt" value={scene.CustomImagePrompt} />
            )}

            <div className="rounded-2xl border border-amber-400/20 bg-amber-500/5 p-4">
              <div className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-amber-400">
                <Wand2 className="h-4 w-4" /> Agent-generated prompt
              </div>
              <p className="text-sm leading-relaxed text-slate-300">{scene.GeneratedPrompt}</p>
            </div>

            {isCompleted && (
              <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4">
                <div className="mb-2 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-500">
                  <Video className="h-4 w-4" /> Review video
                </div>
                {scene.FinalVideoStoragePath ? (
                  <video
                    key={scene.FinalVideoStoragePath}
                    src={getFilmSceneVideoUrl(scene.FilmSceneId)}
                    controls
                    className="w-full rounded-xl bg-black"
                  />
                ) : (
                  <button
                    type="button"
                    onClick={handleGenerateVideoClick}
                    disabled={generatingVideo || scene.VideoStatus === 1}
                    className="flex items-center gap-2 rounded-xl border border-amber-400/30 bg-amber-500/10 px-4 py-2 text-sm font-medium text-amber-300 transition-colors hover:bg-amber-500/20 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {generatingVideo || scene.VideoStatus === 1 ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <PlayCircle className="h-4 w-4" />
                    )}
                    {scene.VideoStatus === 3 ? 'Retry video' : 'Generate video'}
                  </button>
                )}
                {(videoError || (scene.VideoStatus === 3 && scene.VideoErrorMessage)) && (
                  <p className="mt-2 text-xs text-rose-400">{videoError ?? scene.VideoErrorMessage}</p>
                )}
              </div>
            )}

            {scene.Status === 3 && scene.ErrorMessage && (
              <div className="rounded-2xl border border-rose-400/20 bg-rose-500/5 p-4 text-sm text-rose-300">
                {scene.ErrorMessage}
              </div>
            )}

            {isFailed && (
              <button
                type="button"
                onClick={handleRetryClick}
                disabled={retrying}
                className="flex items-center gap-2 rounded-xl border border-amber-400/30 bg-amber-500/10 px-4 py-2 text-sm font-medium text-amber-300 transition-colors hover:bg-amber-500/20 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {retrying ? <Loader2 className="h-4 w-4 animate-spin" /> : <Wand2 className="h-4 w-4" />}
                Retry scene
              </button>
            )}
            {retryError && <p className="text-xs text-rose-400">{retryError}</p>}
          </div>

          <button
            type="button"
            onClick={handleDeleteClick}
            disabled={deleting}
            className="mt-6 flex w-full items-center justify-center gap-2 rounded-xl border border-rose-500/20 bg-rose-500/10 py-2.5 text-sm font-medium text-rose-300 transition-colors hover:bg-rose-500/20 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto sm:px-6"
          >
            {deleting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
            Delete scene
          </button>
        </div>
      </div>
    </div>
  );
}

function DetailField({ icon, label, value }: { icon: React.ReactNode; label: string; value: string | null }) {
  if (!value) return null;
  return (
    <div>
      <div className="mb-1 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-slate-500">
        <span className="text-slate-600">{icon}</span>
        {label}
      </div>
      <p className="text-sm leading-relaxed text-slate-300">{value}</p>
    </div>
  );
}
