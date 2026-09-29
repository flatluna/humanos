import { useEffect, useState, type ChangeEvent } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { ArrowLeft, Check, Clapperboard, ImagePlus, Loader2, Play, Sparkles, Trash2, Upload, WandSparkles } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { readFileAsBase64 } from '../lib/api/pdfPipelineApi';
import { generateStructuredLearningVideoPrompt } from '../lib/api/structuredLearningApi';
import {
  createVideoProduction,
  deleteVideoProductionClip,
  generateVideoProductionClip,
  getVideoProductionClipAnchorImage,
  getVideoProductionClipLastFrame,
  getVideoProductionClipVideoUrl,
  getVideoProductionStartingImage,
  getVideoProductionVideoUrl,
  listVideoProductionClips,
  listVideoProductions,
  mergeVideoProduction,
  renameVideoProduction,
  saveVideoProductionDraft,
  deleteVideoProduction,
  type VideoProduction,
  type VideoProductionClip,
} from '../lib/api/videoProductionApi';

type Step = 'objective' | 'instructions' | 'environment' | 'image';

export default function VideoProductionsPage() {
  const { t } = useTranslation();
  const steps: { id: Step; label: string }[] = [
    { id: 'objective', label: t('videoProductions.steps.objective') },
    { id: 'instructions', label: t('videoProductions.steps.instructions') },
    { id: 'environment', label: t('videoProductions.steps.environment') },
    { id: 'image', label: t('videoProductions.steps.image') },
  ];
  const { user } = useAuth();
  const navigate = useNavigate();
  const { videoProductionId: routeVideoProductionId } = useParams<{ videoProductionId: string }>();
  const [activeStep, setActiveStep] = useState<Step>('objective');
  // Which video generation backend to bill clips to (2026-09-24) — Kling
  // is a much cheaper third-party alternative to Google Veo added after
  // Veo's per-second billing ran $3-5/clip even on the "-fast" tier.
  const [videoModel, setVideoModel] = useState<'veo' | 'veo-lite' | 'omni' | 'kling'>('veo');
  const [objective, setObjective] = useState('');
  const [instructions, setInstructions] = useState('');
  const [environment, setEnvironment] = useState('');
  const [maxClips, setMaxClips] = useState(10);
  const [imageMode, setImageMode] = useState<'upload' | 'ai' | 'edit' | 'none'>('none');
  const [imagePrompt, setImagePrompt] = useState('');
  const [imageEditPrompt, setImageEditPrompt] = useState('');
  const [imageName, setImageName] = useState('');
  const [imageFile, setImageFile] = useState<File | null>(null);
  const [imagePreviewUrl, setImagePreviewUrl] = useState<string | null>(null);
  // The saved step-4 starting image, fetched back on reopen (2026-09-24) —
  // used to regenerate clip 0 with the SAME image even when there's no
  // real File object anymore (the browser can't restore that across a
  // page reload; only these raw bytes survive).
  const [restoredStartingImage, setRestoredStartingImage] = useState<{ base64: string; contentType: string } | null>(null);
  const [production, setProduction] = useState<VideoProduction | null>(null);
  const [sequenceGenerated, setSequenceGenerated] = useState(false);
  const [generatingSequence, setGeneratingSequence] = useState(false);
  const [generationError, setGenerationError] = useState<string | null>(null);
  const [videoError, setVideoError] = useState<string | null>(null);
  const [generatingClipIndex, setGeneratingClipIndex] = useState<number | null>(null);
  const [merging, setMerging] = useState(false);
  const [prompts, setPrompts] = useState<string[]>([]);
  const [clips, setClips] = useState<VideoProductionClip[]>([]);
  // The starting frame actually used to generate each clip (2026-09-23) —
  // clip 0 uses the wizard's own starting image (if any); every clip after
  // that uses the PREVIOUS clip's own last frame, so video 2 starts
  // exactly where video 1 finished. Keyed by clip index, shown as a
  // thumbnail under that clip so the reviewer can verify continuity.
  const [clipAnchors, setClipAnchors] = useState<Record<number, { base64: string; contentType: string } | null>>({});
  const [chainImage, setChainImage] = useState<{ base64: string; contentType: string } | null>(null);
  // Merge picker (2026-09-23, mirrors Structured Learning's video merge UX)
  // — lets the reviewer pick WHICH completed clips to merge and in what
  // order, instead of always concatenating every completed clip by SortOrder.
  const [mergeMode, setMergeMode] = useState(false);
  const [mergeSelection, setMergeSelection] = useState<string[]>([]);
  const [deletingClipId, setDeletingClipId] = useState<string | null>(null);
  const [renamingTitle, setRenamingTitle] = useState(false);
  // Delete-project confirmation (2026-09-24) — irreversible (removes the
  // row, every clip, and every blob), so it's gated behind an explicit
  // confirm dialog rather than a single click.
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deletingProduction, setDeletingProduction] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [loadingExisting, setLoadingExisting] = useState(Boolean(routeVideoProductionId));
  const currentIndex = steps.findIndex((step) => step.id === activeStep);

  // Reopens the project named in the URL (2026-09-24) — this page is now
  // reached either at "/video-productions/new" (blank draft) or
  // "/video-productions/:videoProductionId" (from a card on the gallery
  // page), so the id in the route — not any in-page list — drives which
  // project loads.
  useEffect(() => {
    if (!routeVideoProductionId) return;
    let cancelled = false;
    setLoadingExisting(true);
    Promise.all([listVideoProductions(), listVideoProductionClips(routeVideoProductionId)])
      .then(([allProductions, loadedClips]) => {
        if (cancelled) return;
        const target = allProductions.find((p) => p.VideoProductionId === routeVideoProductionId) ?? null;
        if (!target) {
          setVideoError(t('videoProductions.projectNotFound'));
          return;
        }
        setClips(loadedClips);
        setClipAnchors({});
        setChainImage(null);
        setRestoredStartingImage(null);
        // A clip row only exists once its "Create video" button was
        // clicked (2026-09-24) — so when there are none yet, the planned
        // prompt sequence and wizard inputs come from the production's own
        // saved draft fields instead, or the project would look empty.
        if (loadedClips.length > 0) {
          setPrompts(loadedClips.map((clip) => clip.Prompt));
        } else if (target.PromptsJson) {
          try {
            setPrompts(JSON.parse(target.PromptsJson) as string[]);
          } catch {
            setPrompts([]);
          }
        }
        setObjective(target.Objective ?? '');
        setInstructions(target.Instructions ?? '');
        setEnvironment(target.Environment ?? '');
        setMaxClips((current) => target.MaxClips ?? Math.max(current, loadedClips.length));
        setImageMode((target.ImageMode as typeof imageMode) ?? 'none');
        setImagePrompt(target.ImagePrompt ?? '');
        setImageEditPrompt(target.ImageEditPrompt ?? '');
        setSequenceGenerated(loadedClips.length > 0 || Boolean(target.PromptsJson));
        setProduction(target);

        // Restores step 4's own saved starting image, if any (best-effort
        // — the preview just stays empty if this fails) (2026-09-24).
        if (target.StartingImageStoragePath) {
          getVideoProductionStartingImage(target.VideoProductionId)
            .then((image) => {
              if (cancelled) return;
              setImagePreviewUrl(`data:${image.ContentType};base64,${image.Base64}`);
              setRestoredStartingImage({ base64: image.Base64, contentType: image.ContentType });
            })
            .catch(() => {
              // Best-effort — see comment above.
            });
        }

        // Restores each clip's own starting-frame preview, and the
        // continuity anchor for creating the NEXT new clip (2026-09-24) —
        // both best-effort, since neither blocks the reviewer from seeing
        // or continuing the rest of the project if one image is missing.
        loadedClips.forEach((clip, index) => {
          if (!clip.AnchorImageStoragePath) return;
          getVideoProductionClipAnchorImage(clip.VideoProductionClipId)
            .then((image) => {
              if (cancelled) return;
              setClipAnchors((current) => ({ ...current, [index]: { base64: image.Base64, contentType: image.ContentType } }));
            })
            .catch(() => {
              // Best-effort — see comment above.
            });
        });
        const lastCompletedClip = [...loadedClips].reverse().find((clip) => clip.Status === 2);
        if (lastCompletedClip) {
          getVideoProductionClipLastFrame(lastCompletedClip.VideoProductionClipId)
            .then((lastFrame) => {
              if (!cancelled) setChainImage({ base64: lastFrame.Base64, contentType: lastFrame.ContentType });
            })
            .catch(() => {
              // Best-effort — continuing to a new clip just won't have an
              // auto-carried starting frame if this fails.
            });
        }
      })
      .catch((error) => {
        if (!cancelled) setVideoError(error instanceof Error ? error.message : t('videoProductions.projectNotFound'));
      })
      .finally(() => {
        if (!cancelled) setLoadingExisting(false);
      });
    return () => {
      cancelled = true;
    };
  }, [routeVideoProductionId]);
  const handleImage = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    setImageFile(file);
    setImageName(file?.name ?? '');
    setRestoredStartingImage(null);
    setImagePreviewUrl((prev) => {
      if (prev) URL.revokeObjectURL(prev);
      return file ? URL.createObjectURL(file) : null;
    });
  };
  // Requires an ACTUAL image (a freshly chosen File, OR one already saved
  // and restored on reopen) — "ai" mode has no real image-generation step
  // on this page, so a description alone would leave clip 0 with no real
  // starting frame despite looking "ready".
  // Computed via a plain function (not an inline comparison chain) so
  // TypeScript's aliased-condition narrowing doesn't leak into
  // handleGeneratePromptSequence's own `imageMode === 'ai'` check below.
  function isImageDataReady(mode: typeof imageMode, file: File | null, editPrompt: string, hasRestoredImage: boolean): boolean {
    if (mode === 'none') return true;
    if (mode === 'upload') return file !== null || hasRestoredImage;
    if (mode === 'edit') return (file !== null || hasRestoredImage) && editPrompt.trim().length > 0;
    return false;
  }
  const imageDataReady = isImageDataReady(imageMode, imageFile, imageEditPrompt, restoredStartingImage !== null);
  const formReady = objective.trim().length > 0 && instructions.trim().length > 0 && environment.trim().length > 0 && maxClips > 0 && imageDataReady;

  // "Generate prompt sequence" calls the SAME fast, text-only agent used by
  // the Structured Learning wizard (StructuredLearningVideoPromptAgent) —
  // deliberately NOT the Film Production pipeline (which runs a slow
  // per-scene image-generation agent for every clip before returning).
  async function handleGeneratePromptSequence() {
    if (!formReady || generatingSequence) return;
    setGeneratingSequence(true);
    setGenerationError(null);
    try {
      // Clean up the PREVIOUS production's clips/videos (2026-09-23) —
      // regenerating a sequence must not leave the old clips' rows/blobs
      // orphaned in the DB/Data Lake. Best-effort: one already-deleted or
      // failed clip must never block clearing the rest / starting fresh.
      await Promise.all(clips.map((clip) => deleteVideoProductionClip(clip.VideoProductionClipId).catch(() => {})));

      const imageDescription = imageMode === 'ai'
        ? imagePrompt.trim()
        : imageMode === 'edit'
          ? imageEditPrompt.trim()
          : imageMode === 'none'
            ? 'No specific starting image provided.'
            : (imageName || 'Uploaded starting image.');

      const [result, newProduction] = await Promise.all([
        generateStructuredLearningVideoPrompt(objective.trim(), instructions.trim(), environment.trim(), imageDescription, undefined, maxClips),
        createVideoProduction({ tenantId: user?.tenantId ?? '00000000-0000-0000-0000-000000000000', title: objective.trim().slice(0, 80) }),
      ]);

      setProduction(newProduction);
      setPrompts(result.RecommendedPrompts);
      setClips([]);
      setClipAnchors({});
      setChainImage(null);
      setSequenceGenerated(true);

      // Persists the draft immediately (2026-09-24) — a clip row only gets
      // created once the user clicks "Create video" on it, so without this
      // the plan/prompts would vanish on reopen until at least one clip
      // was actually generated. Also carries over step 4's own starting
      // image (a fresh upload, or one already restored from a previous
      // save) — this always creates a NEW VideoProduction row, so without
      // resending the image bytes here they'd be lost even though the
      // reviewer never touched step 4 again. Best-effort: a save failure
      // here shouldn't block the reviewer from continuing to work with
      // the in-memory plan.
      const startingImage = (imageMode === 'upload' || imageMode === 'edit')
        ? (imageFile
          ? { base64: await readFileAsBase64(imageFile), contentType: imageFile.type || 'image/png' }
          : restoredStartingImage)
        : null;
      saveVideoProductionDraft(newProduction.VideoProductionId, {
        objective: objective.trim(),
        instructions: instructions.trim(),
        environment: environment.trim(),
        maxClips,
        prompts: result.RecommendedPrompts,
        imageMode,
        imagePrompt,
        imageEditPrompt,
        startingImageBase64: startingImage?.base64 ?? null,
        startingImageContentType: startingImage?.contentType ?? null,
      }).catch(() => {});
    } catch (error) {
      setGenerationError(error instanceof Error ? error.message : t('videoProductions.errorGenerateSequence'));
    } finally {
      setGeneratingSequence(false);
    }
  }

  // "Clear draft" also cleans up any already-generated clips/videos
  // (2026-09-23), same as regenerating a sequence — never leaves orphaned
  // rows/blobs behind just because the reviewer wanted a blank slate.
  async function handleClearDraft() {
    await Promise.all(clips.map((clip) => deleteVideoProductionClip(clip.VideoProductionClipId).catch(() => {})));
    setSequenceGenerated(false);
    setPrompts([]);
    setClips([]);
    setClipAnchors({});
    setChainImage(null);
    setProduction(null);
    setGenerationError(null);
    setVideoError(null);
  }

  // Clip 0 uses the wizard's own starting image (if any); every clip after
  // that uses the PREVIOUS clip's own last frame (2026-09-23), so video 2
  // starts exactly where video 1 finished — same continuity approach as
  // the Structured Learning wizard, kept fully independent from Film
  // Production. Allows re-creating a clip even after it already succeeded
  // (2026-09-23) — deletes the old row/blob first so no orphaned duplicate
  // is left behind at the same SortOrder.
  async function handleGenerateClip(index: number) {
    if (!production || generatingClipIndex !== null) return;
    setGeneratingClipIndex(index);
    setVideoError(null);
    try {
      const existing = clipForIndex(index);
      if (existing) {
        await deleteVideoProductionClip(existing.VideoProductionClipId);
        setClips((current) => current.filter((c) => c.VideoProductionClipId !== existing.VideoProductionClipId));
      }

      let anchor: { base64: string; contentType: string } | null = null;
      if (index === 0) {
        if ((imageMode === 'upload' || imageMode === 'edit') && imageFile) {
          anchor = { base64: await readFileAsBase64(imageFile), contentType: imageFile.type || 'image/png' };
        } else if ((imageMode === 'upload' || imageMode === 'edit') && restoredStartingImage) {
          // Reopened project, no new file chosen (2026-09-24) — reuse the
          // saved starting image bytes so "continue from where I left
          // off" doesn't force a re-upload.
          anchor = restoredStartingImage;
        }
      } else {
        anchor = chainImage;
      }
      setClipAnchors((current) => ({ ...current, [index]: anchor }));

      const clip = await generateVideoProductionClip(
        production.VideoProductionId, index, prompts[index], videoModel, anchor?.base64 ?? null, anchor?.contentType ?? null
      );
      setClips((current) => {
        const next = current.filter((c) => c.SortOrder !== index);
        next.push(clip);
        return next.sort((a, b) => a.SortOrder - b.SortOrder);
      });

      if (clip.Status === 2) {
        try {
          const lastFrame = await getVideoProductionClipLastFrame(clip.VideoProductionClipId);
          setChainImage({ base64: lastFrame.Base64, contentType: lastFrame.ContentType });
        } catch (frameError) {
          setVideoError(
            t('videoProductions.errorContinuityFailed', { error: frameError instanceof Error ? frameError.message : String(frameError) })
          );
        }
      }
    } catch (error) {
      setVideoError(error instanceof Error ? error.message : t('videoProductions.errorGenerateClip'));
    } finally {
      setGeneratingClipIndex(null);
    }
  }

  function toggleMergeSelection(clipId: string) {
    setMergeSelection((prev) => (prev.includes(clipId) ? prev.filter((id) => id !== clipId) : [...prev, clipId]));
  }

  function moveMergeSelection(index: number, delta: 1 | -1) {
    setMergeSelection((prev) => {
      const next = [...prev];
      const target = index + delta;
      if (target < 0 || target >= next.length) return prev;
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  async function handleMergeVideos() {
    if (!production || merging || mergeSelection.length < 2) return;
    setMerging(true);
    setVideoError(null);
    try {
      setProduction(await mergeVideoProduction(production.VideoProductionId, mergeSelection));
      setMergeSelection([]);
      setMergeMode(false);
    }
    catch (error) { setVideoError(error instanceof Error ? error.message : t('videoProductions.errorMergeVideos')); }
    finally { setMerging(false); }
  }

  async function handleDeleteClip(clipId: string) {
    if (deletingClipId) return;
    setDeletingClipId(clipId);
    setVideoError(null);
    try {
      await deleteVideoProductionClip(clipId);
      setClips((current) => current.filter((c) => c.VideoProductionClipId !== clipId));
      setMergeSelection((current) => current.filter((id) => id !== clipId));
    } catch (error) {
      setVideoError(error instanceof Error ? error.message : t('videoProductions.errorDeleteClip'));
    } finally {
      setDeletingClipId(null);
    }
  }

  // Names the saved production so it can be found later (2026-09-23) —
  // doesn't move/rename anything already saved in the Data Lake (blobs are
  // keyed by the production's Guid, not its title), just labels the row.
  async function handleRenameProduction(newTitle: string) {
    if (!production || !newTitle.trim() || newTitle.trim() === production.Title) return;
    setRenamingTitle(true);
    setVideoError(null);
    try {
      setProduction(await renameVideoProduction(production.VideoProductionId, newTitle.trim()));
    } catch (error) {
      setVideoError(error instanceof Error ? error.message : t('videoProductions.errorRenameProduction'));
    } finally {
      setRenamingTitle(false);
    }
  }

  function clipForIndex(index: number) {
    return clips.find((c) => c.SortOrder === index) ?? null;
  }

  async function handleDeleteProduction() {
    if (!production || deletingProduction) return;
    setDeletingProduction(true);
    setDeleteError(null);
    try {
      await deleteVideoProduction(production.VideoProductionId);
      navigate('/video-productions');
    } catch (error) {
      setDeleteError(error instanceof Error ? error.message : t('videoProductions.errorDeleteProject'));
      setDeletingProduction(false);
    }
  }

  // Lets the reviewer tweak a clip's prompt before (re)generating it —
  // editing an already-generated clip's text does NOT retroactively change
  // its existing video; regenerate afterward to apply the new wording.
  function updatePrompt(index: number, value: string) {
    setPrompts((current) => current.map((p, i) => (i === index ? value : p)));
  }

  // Downloads via a fetched blob (2026-09-23) — the video URL is on a
  // different origin (backend :7071 vs frontend :3010), so a plain
  // `<a download>` gets ignored by the browser; fetching first and using
  // an object URL forces a real save-to-disk regardless of origin.
  const [downloadingUrl, setDownloadingUrl] = useState<string | null>(null);
  async function handleDownloadVideo(url: string, filename: string) {
    setDownloadingUrl(url);
    try {
      const response = await fetch(url);
      const blob = await response.blob();
      const objectUrl = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = objectUrl;
      link.download = filename;
      link.click();
      URL.revokeObjectURL(objectUrl);
    } catch (error) {
      setVideoError(error instanceof Error ? error.message : t('videoProductions.errorDownloadVideo'));
    } finally {
      setDownloadingUrl(null);
    }
  }

  // What starting frame will/did anchor this clip — shown BEFORE the clip
  // is generated (so the reviewer can verify it first) and unchanged after
  // (the exact one actually used, from clipAnchors). Clip 0 previews the
  // wizard's own uploaded image; later clips preview the last completed
  // clip's last frame, since it isn't known until that earlier clip finishes.
  function plannedAnchorForIndex(index: number): { previewUrl: string; label: string } | null {
    if (index in clipAnchors) {
      const anchor = clipAnchors[index];
      if (!anchor) return null;
      return {
        previewUrl: `data:${anchor.contentType};base64,${anchor.base64}`,
        label: index === 0 ? t('videoProductions.yourUploadedImage') : t('videoProductions.previousClipLastFrame'),
      };
    }
    if (index === 0) {
      if ((imageMode === 'upload' || imageMode === 'edit') && imagePreviewUrl) {
        return { previewUrl: imagePreviewUrl, label: t('videoProductions.yourUploadedImage') };
      }
      return null;
    }
    if (chainImage) {
      return { previewUrl: `data:${chainImage.contentType};base64,${chainImage.base64}`, label: t('videoProductions.previousClipLastFrame') };
    }
    return null;
  }

  return (
    <div className="mx-auto max-w-7xl px-6 py-10 lg:px-10">
      <div className="mb-6 flex items-center justify-between">
        <button
          type="button"
          onClick={() => navigate('/video-productions')}
          className="flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-cyan-300"
        >
          <ArrowLeft className="h-3.5 w-3.5" /> {t('videoProductions.backToMyVideos')}
        </button>
        {production && (
          <button
            type="button"
            onClick={() => setConfirmingDelete(true)}
            className="flex items-center gap-1.5 text-xs font-medium text-rose-300/80 hover:text-rose-300"
          >
            <Trash2 className="h-3.5 w-3.5" /> {t('videoProductions.deleteProject')}
          </button>
        )}
      </div>
      <header className="mb-8 flex flex-wrap items-start justify-between gap-4">
        <div><div className="mb-3 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-cyan-400"><Clapperboard className="h-4 w-4" /> {t('videoProductions.header.eyebrow')}</div><h1 className="text-3xl font-bold tracking-tight text-white">{t('videoProductions.header.title')}</h1><p className="mt-2 max-w-2xl text-sm text-slate-400">{t('videoProductions.header.subtitle')}</p></div>
        <span className="rounded-full border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-medium text-slate-400">{t('videoProductions.header.draftBadge')}</span>
      </header>
      {loadingExisting ? (
        <div className="flex items-center gap-2 text-sm text-slate-400">
          <Loader2 className="h-4 w-4 animate-spin" /> {t('videoProductions.loadingProject')}
        </div>
      ) : (
      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
        <section className="rounded-2xl border border-white/10 bg-white/[0.03] p-6 shadow-xl lg:p-8">
          <div className="mb-7 flex items-start gap-3"><div className="rounded-xl bg-cyan-500/10 p-2.5 text-cyan-300"><WandSparkles className="h-5 w-5" /></div><div><h2 className="text-lg font-semibold text-white">{t('videoProductions.wizard.title')}</h2><p className="mt-1 text-xs text-slate-500">{t('videoProductions.wizard.subtitle')}</p></div></div>
          <div className="mb-8 grid grid-cols-2 gap-2 border-b border-white/10 pb-4 sm:grid-cols-4">{steps.map((step, index) => <button key={step.id} type="button" onClick={() => setActiveStep(step.id)} className={`flex items-center gap-2 rounded-lg px-2 py-2 text-left text-xs transition-colors ${activeStep === step.id ? 'bg-cyan-500/10 text-cyan-300' : 'text-slate-500 hover:bg-white/5 hover:text-slate-300'}`}><span className="grid h-6 w-6 shrink-0 place-items-center rounded-full border border-current text-[10px]">{index < currentIndex ? <Check className="h-3 w-3" /> : `0${index + 1}`}</span><span>{step.label}</span></button>)}</div>
          <div className="space-y-5">
            {activeStep === 'objective' && <label className="block"><span className="mb-2 block text-sm font-medium text-slate-200">{t('videoProductions.objectiveLabel')}</span><textarea value={objective} onChange={(event) => setObjective(event.target.value)} rows={5} placeholder={t('videoProductions.objectivePlaceholder')} className="w-full rounded-xl border border-white/10 bg-black/20 px-4 py-3 text-sm text-white outline-none placeholder:text-slate-600 focus:border-cyan-400/50 focus:ring-2 focus:ring-cyan-400/10" /></label>}
            {activeStep === 'instructions' && <label className="block"><span className="mb-2 block text-sm font-medium text-slate-200">{t('videoProductions.instructionsLabel')} <em className="font-normal text-slate-500">{t('videoProductions.instructionsNote')}</em></span><textarea value={instructions} onChange={(event) => setInstructions(event.target.value)} rows={8} placeholder={t('videoProductions.instructionsPlaceholder')} className="w-full rounded-xl border border-white/10 bg-black/20 px-4 py-3 text-sm text-white outline-none placeholder:text-slate-600 focus:border-cyan-400/50 focus:ring-2 focus:ring-cyan-400/10" /></label>}
            {activeStep === 'environment' && <div className="space-y-5"><label className="block"><span className="mb-2 block text-sm font-medium text-slate-200">{t('videoProductions.environmentLabel')}</span><input value={environment} onChange={(event) => setEnvironment(event.target.value)} placeholder={t('videoProductions.environmentPlaceholder')} className="w-full rounded-xl border border-white/10 bg-black/20 px-4 py-3 text-sm text-white outline-none placeholder:text-slate-600 focus:border-cyan-400/50 focus:ring-2 focus:ring-cyan-400/10" /></label><label className="block"><span className="mb-2 block text-sm font-medium text-slate-200">{t('videoProductions.maxClipsLabel')}</span><input type="number" min={1} max={10} value={maxClips} onChange={(event) => setMaxClips(Number(event.target.value))} className="w-32 rounded-xl border border-white/10 bg-black/20 px-4 py-3 text-sm text-white outline-none focus:border-cyan-400/50" /></label></div>}
            {activeStep === 'image' && <div className="space-y-4"><p className="text-sm font-medium text-slate-200">{t('videoProductions.startingImageLabel')}</p><div className="grid gap-2 sm:grid-cols-2"><button type="button" onClick={() => setImageMode('upload')} className={`rounded-xl border p-4 text-left text-sm ${imageMode === 'upload' ? 'border-cyan-400/60 bg-cyan-500/10 text-cyan-300' : 'border-white/10 text-slate-400 hover:bg-white/5'}`}><Upload className="mb-3 h-5 w-5" />{t('videoProductions.uploadImageTitle')}<span className="mt-1 block text-xs text-slate-500">{t('videoProductions.uploadImageHint')}</span></button><button type="button" onClick={() => setImageMode('ai')} className={`rounded-xl border p-4 text-left text-sm ${imageMode === 'ai' ? 'border-cyan-400/60 bg-cyan-500/10 text-cyan-300' : 'border-white/10 text-slate-400 hover:bg-white/5'}`}><Sparkles className="mb-3 h-5 w-5" />{t('videoProductions.aiImageTitle')}<span className="mt-1 block text-xs text-slate-500">{t('videoProductions.aiImageHint')}</span></button><button type="button" onClick={() => setImageMode('edit')} className={`rounded-xl border p-4 text-left text-sm ${imageMode === 'edit' ? 'border-cyan-400/60 bg-cyan-500/10 text-cyan-300' : 'border-white/10 text-slate-400 hover:bg-white/5'}`}><WandSparkles className="mb-3 h-5 w-5" />{t('videoProductions.editImageTitle')}<span className="mt-1 block text-xs text-slate-500">{t('videoProductions.editImageHint')}</span></button><button type="button" onClick={() => { setImageMode('none'); setImageName(''); }} className={`rounded-xl border p-4 text-left text-sm ${imageMode === 'none' ? 'border-cyan-400/60 bg-cyan-500/10 text-cyan-300' : 'border-white/10 text-slate-400 hover:bg-white/5'}`}><Clapperboard className="mb-3 h-5 w-5" />{t('videoProductions.noImageTitle')}<span className="mt-1 block text-xs text-slate-500">{t('videoProductions.noImageHint')}</span></button></div>{imageMode === 'ai' && <label className="block"><span className="mb-2 block text-sm font-medium text-slate-200">{t('videoProductions.describeImageToGenerateLabel')}</span><textarea value={imagePrompt} onChange={(event) => setImagePrompt(event.target.value)} rows={4} placeholder={t('videoProductions.describeImageToGeneratePlaceholder')} className="w-full rounded-xl border border-white/10 bg-black/20 px-4 py-3 text-sm text-white outline-none placeholder:text-slate-600 focus:border-cyan-400/50" /><span className="mt-2 block text-xs text-amber-400">{t('videoProductions.aiNotWiredNote')}</span></label>}{imageMode === 'edit' && <label className="block"><span className="mb-2 block text-sm font-medium text-slate-200">{t('videoProductions.describeEditLabel')}</span><textarea value={imageEditPrompt} onChange={(event) => setImageEditPrompt(event.target.value)} rows={4} placeholder={t('videoProductions.describeEditPlaceholder')} className="w-full rounded-xl border border-white/10 bg-black/20 px-4 py-3 text-sm text-white outline-none placeholder:text-slate-600 focus:border-cyan-400/50" /></label>}{(imageMode === 'upload' || imageMode === 'edit') && <div className="flex flex-wrap items-center gap-3 rounded-xl border border-dashed border-white/15 bg-black/10 p-4"><ImagePlus className="h-5 w-5 text-cyan-300" /><div className="min-w-0 flex-1"><p className="truncate text-sm text-slate-200">{imageName || t('videoProductions.noImageSelected')}</p><p className="text-xs text-slate-500">{imageName ? t('videoProductions.imageSelectedHint') : t('videoProductions.selectImageHint')}</p></div><label className="flex cursor-pointer items-center gap-2 rounded-lg border border-white/10 px-3 py-2 text-xs font-medium text-slate-300 hover:bg-white/5"><Upload className="h-3.5 w-3.5" /> {imageName ? t('videoProductions.replaceImage') : t('videoProductions.uploadImageButton')}<input type="file" accept="image/*" onChange={handleImage} className="hidden" /></label></div>}{imageMode === 'none' && <p className="rounded-xl border border-white/10 bg-black/10 p-4 text-xs text-slate-400">{t('videoProductions.noImageNote')}</p>}</div>}
            {generationError && <p className="rounded-lg border border-rose-400/20 bg-rose-500/10 p-3 text-xs text-rose-300">{generationError}</p>}
            {videoError && <p className="rounded-lg border border-rose-400/20 bg-rose-500/10 p-3 text-xs text-rose-300">{videoError}</p>}
            <div className="flex flex-wrap justify-end gap-3 border-t border-white/10 pt-5"><button type="button" onClick={() => void handleClearDraft()} className="rounded-lg border border-white/10 px-4 py-2.5 text-xs font-medium text-slate-400 hover:bg-white/5">{t('videoProductions.clearDraft')}</button><button type="button" disabled={!formReady || generatingSequence} onClick={() => void handleGeneratePromptSequence()} className="flex items-center gap-2 rounded-lg bg-cyan-500/10 px-4 py-2.5 text-xs font-semibold text-cyan-300 hover:bg-cyan-500/20 disabled:cursor-not-allowed disabled:opacity-40"><Sparkles className={`h-4 w-4 ${generatingSequence ? 'animate-spin' : ''}`} /> {generatingSequence ? t('videoProductions.generatingSequence') : t('videoProductions.generatePromptSequence')}</button></div>
            {sequenceGenerated && <p className="text-right text-xs text-slate-500">{t('videoProductions.sequenceReadyHint')}</p>}
          </div>
        </section>
        <aside className="h-fit rounded-2xl border border-white/10 bg-white/[0.03] p-5">
          <div className="mb-4 flex items-center justify-between">
            <span className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">{t('videoProductions.previewLabel')}</span>
            <div className="flex items-center gap-2">
              <span className="text-[10px] text-slate-600">{maxClips} {t('videoProductions.clipsMax')}</span>
              {clips.filter((c) => c.Status === 2).length > 1 && (
                <button
                  type="button"
                  onClick={() => { setMergeMode((v) => !v); setMergeSelection([]); }}
                  className="text-[11px] font-medium text-emerald-300 underline hover:text-emerald-200"
                >
                  {mergeMode ? t('videoProductions.cancel') : t('videoProductions.selectClipsToMerge')}
                </button>
              )}
            </div>
          </div>
          <label className="mb-4 block">
            <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wider text-slate-500">Video engine</span>
            <select
              value={videoModel}
              onChange={(event) => setVideoModel(event.target.value as 'veo' | 'veo-lite' | 'omni' | 'kling')}
              className="w-full rounded-lg border border-white/10 bg-black/20 px-3 py-2 text-sm text-white outline-none focus:border-cyan-400/50"
            >
              <option value="veo">Google Veo (fast, 720p)</option>
              <option value="veo-lite">Google Veo Lite (cheapest Veo tier)</option>
              <option value="omni">Gemini Omni Flash</option>
              <option value="kling">Kling AI (cheaper alternative)</option>
            </select>
          </label>
          {production && (
            <label className="mb-4 block">
              <span className="mb-1 block text-[10px] font-semibold uppercase tracking-wider text-slate-500">{t('videoProductions.productionNameLabel')}</span>
              <input
                defaultValue={production.Title}
                key={production.VideoProductionId}
                onBlur={(event) => void handleRenameProduction(event.target.value)}
                disabled={renamingTitle}
                className="w-full rounded-lg border border-white/10 bg-black/20 px-3 py-2 text-sm text-white outline-none focus:border-cyan-400/50 disabled:opacity-50"
              />
            </label>
          )}
          {production?.FinalVideoStoragePath && (
            <div className="mb-4">
              <div className="mb-1.5 flex items-center justify-between">
                <p className="flex items-center gap-1.5 text-[10px] font-semibold uppercase tracking-wider text-emerald-400"><Sparkles className="h-3 w-3" /> {t('videoProductions.mergedFinalVideo')}</p>
                <button
                  type="button"
                  onClick={() => void handleDownloadVideo(getVideoProductionVideoUrl(production.VideoProductionId), `${production.Title || 'video-production'}.mp4`)}
                  disabled={downloadingUrl !== null}
                  className="text-[11px] font-medium text-emerald-300 underline hover:text-emerald-200 disabled:opacity-50"
                >
                  {downloadingUrl === getVideoProductionVideoUrl(production.VideoProductionId) ? t('videoProductions.downloading') : t('videoProductions.download')}
                </button>
              </div>
              <video src={getVideoProductionVideoUrl(production.VideoProductionId)} controls className="w-full rounded-xl bg-black" />
            </div>
          )}
          {mergeMode && (
            <div className="mb-3 space-y-2 rounded-lg border border-emerald-400/30 bg-emerald-500/[0.04] p-3">
              <p className="text-[11px] text-slate-400">{t('videoProductions.mergeHint', { count: mergeSelection.length })}</p>
              {mergeSelection.length > 0 && (
                <ol className="space-y-1">
                  {mergeSelection.map((id, index) => {
                    const clip = clips.find((c) => c.VideoProductionClipId === id);
                    return (
                      <li key={id} className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/[0.03] px-2 py-1 text-xs text-slate-300">
                        <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-emerald-500/30 text-[10px] font-bold text-emerald-200">{index + 1}</span>
                        <span className="flex-1 truncate">{t('videoProductions.clipLabel', { number: (clip?.SortOrder ?? 0) + 1 })}</span>
                        <button type="button" onClick={() => moveMergeSelection(index, -1)} disabled={index === 0} className="rounded px-1 text-slate-400 hover:bg-white/10 disabled:opacity-30">↑</button>
                        <button type="button" onClick={() => moveMergeSelection(index, 1)} disabled={index === mergeSelection.length - 1} className="rounded px-1 text-slate-400 hover:bg-white/10 disabled:opacity-30">↓</button>
                        <button type="button" onClick={() => toggleMergeSelection(id)} className="rounded px-1 text-rose-300 hover:bg-rose-500/10">✕</button>
                      </li>
                    );
                  })}
                </ol>
              )}
              <button
                type="button"
                onClick={() => void handleMergeVideos()}
                disabled={mergeSelection.length < 2 || merging}
                className="w-full rounded-lg bg-emerald-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
              >
                {merging ? t('videoProductions.merging') : t('videoProductions.mergeSelected', { count: mergeSelection.length })}
              </button>
            </div>
          )}
          {prompts.length > 0 ? (
            <div className="max-h-[32rem] space-y-3 overflow-auto">
              {prompts.map((clipPrompt, index) => {
                const clip = clipForIndex(index);
                const status = clip?.Status ?? 0;
                const isSelected = clip ? mergeSelection.includes(clip.VideoProductionClipId) : false;
                return (
                  <div
                    key={index}
                    onClick={() => { if (mergeMode && status === 2 && clip) toggleMergeSelection(clip.VideoProductionClipId); }}
                    className={`rounded-xl border p-3 ${mergeMode && status === 2 ? 'cursor-pointer' : ''} ${isSelected ? 'border-emerald-400/60 bg-emerald-500/10' : 'border-white/10 bg-black/20'}`}
                  >
                    <div className="mb-1 flex items-center justify-between text-[10px] font-semibold uppercase tracking-wider text-cyan-400">
                      <span>{t('videoProductions.clipLabel', { number: index + 1 })}</span>
                      <span className={status === 2 ? 'text-emerald-400' : status === 3 ? 'text-rose-400' : 'text-slate-500'}>{status === 2 ? t('videoProductions.statusReady') : status === 3 ? t('videoProductions.statusFailed') : t('videoProductions.statusNotGenerated')}</span>
                    </div>
                    <textarea
                      value={clipPrompt}
                      onChange={(event) => updatePrompt(index, event.target.value)}
                      onClick={(event) => event.stopPropagation()}
                      disabled={mergeMode}
                      rows={4}
                      className="w-full resize-y rounded-lg border border-white/10 bg-black/20 px-2 py-1.5 text-xs leading-5 text-slate-300 outline-none focus:border-cyan-400/50 disabled:opacity-70"
                    />
                    {(() => {
                      const anchor = plannedAnchorForIndex(index);
                      return (
                        <p className="mt-2 flex items-center gap-1.5 text-[10px] text-slate-400">
                          {t('videoProductions.startingFrameLabel')}{' '}
                          {anchor ? (
                            <>
                              <img src={anchor.previewUrl} alt="Starting frame" className="h-10 w-10 rounded object-cover" />
                              {anchor.label}
                            </>
                          ) : (
                            <span className="text-amber-400">
                              {index > 0 && !(index in clipAnchors)
                                ? t('videoProductions.waitingForClip', { number: index })
                                : t('videoProductions.noImageAutoNote')}
                            </span>
                          )}
                        </p>
                      );
                    })()}
                    {clip?.Status === 2 && clip.StoragePath && (
                      <div>
                        <video src={getVideoProductionClipVideoUrl(clip.VideoProductionClipId)} controls className="mt-3 w-full rounded-lg bg-black" />
                        <button
                          type="button"
                          onClick={() => void handleDownloadVideo(getVideoProductionClipVideoUrl(clip.VideoProductionClipId), `clip-${index + 1}.mp4`)}
                          disabled={downloadingUrl !== null}
                          className="mt-1.5 text-[11px] font-medium text-cyan-300 underline hover:text-cyan-200 disabled:opacity-50"
                        >
                          {downloadingUrl === getVideoProductionClipVideoUrl(clip.VideoProductionClipId) ? t('videoProductions.downloading') : t('videoProductions.downloadThisClip')}
                        </button>
                      </div>
                    )}
                    {!mergeMode && (
                      <button
                        type="button"
                        onClick={() => void handleGenerateClip(index)}
                        disabled={generatingClipIndex !== null}
                        className={`mt-3 flex items-center gap-2 rounded-lg border px-3 py-1.5 text-xs disabled:opacity-50 ${status === 3 ? 'border-rose-400/30 text-rose-300' : status === 2 ? 'border-white/15 text-slate-300' : 'border-cyan-400/30 text-cyan-300'}`}
                      >
                        {generatingClipIndex === index ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Play className="h-3.5 w-3.5" />}
                        {generatingClipIndex === index ? t('videoProductions.generatingClip') : status === 3 ? t('videoProductions.retryVideo') : status === 2 ? t('videoProductions.regenerate') : t('videoProductions.createVideoButton')}
                      </button>
                    )}
                    {!mergeMode && clip && (
                      <button
                        type="button"
                        onClick={() => void handleDeleteClip(clip.VideoProductionClipId)}
                        disabled={deletingClipId !== null}
                        className="mt-2 flex items-center gap-1.5 text-[11px] text-rose-300/80 hover:text-rose-300 disabled:opacity-50"
                      >
                        {deletingClipId === clip.VideoProductionClipId ? t('videoProductions.deletingClip') : t('videoProductions.deleteClip')}
                      </button>
                    )}
                  </div>
                );
              })}
            </div>
          ) : (
            <div className="flex min-h-72 flex-col items-center justify-center gap-3 rounded-xl border border-white/5 bg-black/20 text-center text-slate-500">
              {sequenceGenerated ? <Sparkles className="h-8 w-8 text-cyan-300" /> : <Clapperboard className="h-8 w-8" />}
              <strong className="text-sm text-slate-300">{sequenceGenerated ? t('videoProductions.sequenceReadyTitle') : t('videoProductions.videoWillAppearHere')}</strong>
              <span className="max-w-xs text-xs leading-5">{sequenceGenerated ? t('videoProductions.sequenceReadyBody') : t('videoProductions.generateToPreviewBody')}</span>
            </div>
          )}
          <p className="mt-4 text-xs leading-5 text-slate-500">{t('videoProductions.footerNote')}</p>
        </aside>
      </div>
      )}
      {confirmingDelete && production && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-6"
          onClick={() => (deletingProduction ? null : setConfirmingDelete(false))}
        >
          <div className="w-full max-w-md rounded-2xl border border-white/10 bg-[#12141c] p-6 shadow-2xl" onClick={(event) => event.stopPropagation()}>
            <div className="mb-3 flex items-center gap-2 text-rose-300">
              <Trash2 className="h-5 w-5" />
              <h2 className="text-lg font-semibold text-white">{t('videoProductions.deleteConfirmTitle')}</h2>
            </div>
            <p className="text-sm text-slate-400">
              {t('videoProductions.deleteConfirmBody', { title: production.Title || t('videoProductions.untitledProject') })}
            </p>
            {deleteError && <p className="mt-3 rounded-lg border border-rose-400/20 bg-rose-500/10 p-2 text-xs text-rose-300">{deleteError}</p>}
            <div className="mt-5 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setConfirmingDelete(false)}
                disabled={deletingProduction}
                className="rounded-lg border border-white/10 px-4 py-2 text-xs font-medium text-slate-300 hover:bg-white/5 disabled:opacity-50"
              >
                {t('videoProductions.cancel')}
              </button>
              <button
                type="button"
                onClick={() => void handleDeleteProduction()}
                disabled={deletingProduction}
                className="flex items-center gap-2 rounded-lg bg-rose-500 px-4 py-2 text-xs font-semibold text-white hover:bg-rose-400 disabled:opacity-50"
              >
                {deletingProduction ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
                {deletingProduction ? t('videoProductions.deletingProject') : t('videoProductions.deletePermanently')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
