import { apiDelete, apiGet, apiPost, apiPut } from './httpClient';

export interface VideoProduction {
  VideoProductionId: string;
  TenantId: string;
  Title: string;
  FinalVideoStoragePath: string | null;
  Objective: string | null;
  Instructions: string | null;
  Environment: string | null;
  MaxClips: number | null;
  PromptsJson: string | null;
  ImageMode: string | null;
  ImagePrompt: string | null;
  ImageEditPrompt: string | null;
  StartingImageStoragePath: string | null;
  StartingImageContentType: string | null;
  CreatedDate: string;
}

export interface VideoProductionClip {
  VideoProductionClipId: string;
  VideoProductionId: string;
  SortOrder: number;
  Prompt: string;
  StoragePath: string | null;
  AnchorImageStoragePath: string | null;
  VideoModel: string | null;
  /** 0=Pending, 1=Processing, 2=Completed, 3=Failed. */
  Status: number;
  ErrorMessage: string | null;
  CreatedDate: string;
}

/** Standalone "Video Productions" module (2026-09-23) — deliberately does
 * NOT use the Film Production entities/agents (that pipeline runs a slow
 * per-scene image-generation agent). Clips go straight from a text prompt
 * (already produced by the fast, text-only prompt-sequence agent — see
 * generateStructuredLearningVideoPrompt) to a Veo/Omni Flash video. */
export function createVideoProduction(request: { tenantId: string; title: string }): Promise<VideoProduction> {
  return apiPost('/studio/video-productions', request);
}

/** Lists all saved productions, most recent first (2026-09-24) — powers the
 * "reopen a project" list on the wizard's hero screen. */
export function listVideoProductions(): Promise<VideoProduction[]> {
  return apiGet('/studio/video-productions');
}

/** Lists a production's own clips, ordered by SortOrder (2026-09-24) — used
 * to restore the wizard's clip grid when reopening a saved project. */
export function listVideoProductionClips(videoProductionId: string): Promise<VideoProductionClip[]> {
  return apiGet(`/studio/video-productions/${videoProductionId}/clips`);
}

/** Deletes an entire project — its row, every clip, and every blob
 * (per-clip .mp4s plus the merged final video) (2026-09-24). Irreversible —
 * the caller must confirm with the reviewer first. */
export function deleteVideoProduction(videoProductionId: string): Promise<{ deleted: boolean }> {
  return apiDelete(`/studio/video-productions/${videoProductionId}`);
}

/** Renames a production — the Title identifies the saved production/merged
 * video for later reference; it does not affect Data Lake blob paths
 * (those are keyed by the production's own Guid). */
export function renameVideoProduction(videoProductionId: string, title: string): Promise<VideoProduction> {
  return apiPut(`/studio/video-productions/${videoProductionId}/title`, { title });
}

/** Saves the wizard's own inputs + planned prompt sequence (2026-09-24) —
 * called right after prompt-sequence generation succeeds, since a clip row
 * only exists once the user actually clicks "Create video" on it; without
 * this, a generated-but-not-yet-clicked sequence had nowhere to be saved
 * and reopening the project later showed a blank wizard. Also saves step
 * 4's own starting image (mode + prompt/edit text + the uploaded image
 * itself, if any) so reopening restores it too. */
export function saveVideoProductionDraft(
  videoProductionId: string,
  draft: {
    objective: string;
    instructions: string;
    environment: string;
    maxClips: number;
    prompts: string[];
    imageMode: string;
    imagePrompt: string;
    imageEditPrompt: string;
    startingImageBase64?: string | null;
    startingImageContentType?: string | null;
  }
): Promise<VideoProduction> {
  return apiPut(`/studio/video-productions/${videoProductionId}/draft`, draft);
}

/** The wizard's own saved step-4 starting image (2026-09-24). */
export function getVideoProductionStartingImage(videoProductionId: string): Promise<{ Base64: string; ContentType: string }> {
  return apiGet(`/studio/video-productions/${videoProductionId}/starting-image`);
}

/** The exact starting frame a clip was generated from (2026-09-24) — clip
 * 0's own uploaded/edited image, or the previous clip's last frame carried
 * over for continuity. */
export function getVideoProductionClipAnchorImage(clipId: string): Promise<{ Base64: string; ContentType: string }> {
  return apiGet(`/studio/video-productions/clips/${clipId}/anchor-image`);
}

export function generateVideoProductionClip(
  videoProductionId: string,
  sortOrder: number,
  prompt: string,
  videoModel: 'veo' | 'veo-lite' | 'omni' | 'kling' = 'veo',
  referenceImageBase64?: string | null,
  referenceImageContentType?: string | null
): Promise<VideoProductionClip> {
  return apiPost(`/studio/video-productions/${videoProductionId}/clips`, {
    sortOrder,
    prompt,
    videoModel,
    referenceImageBase64: referenceImageBase64 ?? null,
    referenceImageContentType: referenceImageContentType ?? null,
  });
}

/** Merges 2+ already-generated clips, in the caller's chosen order, into
 * one new final video (ffmpeg concat, local/unbilled) — mirrors
 * mergeStructuredLearningVideos. Source clips are left untouched. */
export function mergeVideoProduction(videoProductionId: string, orderedClipIds: string[]): Promise<VideoProduction> {
  return apiPost(`/studio/video-productions/${videoProductionId}/merge`, { clipIds: orderedClipIds });
}

/** Deletes one clip's row and its .mp4 blob — mirrors deleteStructuredLearningVideo. */
export function deleteVideoProductionClip(clipId: string): Promise<{ deleted: boolean }> {
  return apiDelete(`/studio/video-productions/clips/${clipId}`);
}

/** Last frame of an already-generated clip (JPEG base64) — used to carry
 * visual continuity into the next clip's starting image automatically,
 * and to show the reviewer exactly where the previous clip left off. */
export function getVideoProductionClipLastFrame(clipId: string): Promise<{ Base64: string; ContentType: string }> {
  return apiGet(`/studio/video-productions/clips/${clipId}/last-frame`);
}

/** Backend serves the raw MP4 bytes directly — build the <video src> URL, no JSON wrapper. */
export function getVideoProductionClipVideoUrl(clipId: string): string {
  const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:7071/api';
  return `${base}/studio/video-productions/clips/${clipId}/video`;
}

/** Backend serves the raw MP4 bytes directly — build the <video src> URL, no JSON wrapper. */
export function getVideoProductionVideoUrl(videoProductionId: string): string {
  const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:7071/api';
  return `${base}/studio/video-productions/${videoProductionId}/video`;
}
