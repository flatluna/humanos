import { apiDelete, apiGet, apiPost } from './httpClient';

export type FilmSceneAnchorImageMode = 'Uploaded' | 'GeneratedFromScenePrompt' | 'GeneratedFromCustomPrompt';

export interface FilmProduction {
  FilmProductionId: string;
  TenantId: string;
  Title: string;
  FinalFilmStoragePath: string | null;
  CreatedDate: string;
}

export interface FilmScene {
  FilmSceneId: string;
  FilmProductionId: string;
  SortOrder: number;
  Description: string;
  DialogueText: string | null;
  Characters: string | null;
  CameraMovements: string | null;
  Environment: string;
  AnchorImageMode: number;
  CustomImagePrompt: string | null;
  AnchorImageStoragePath: string | null;
  SuggestedSceneName: string | null;
  GeneratedPrompt: string | null;
  FinalImageStoragePath: string | null;
  ImageModel: string | null;
  Status: number;
  ErrorMessage: string | null;
  FinalVideoStoragePath: string | null;
  VideoModel: string | null;
  VideoStatus: number;
  VideoErrorMessage: string | null;
  CreatedDate: string;
}

export interface CreateFilmProductionRequest {
  tenantId: string;
  title: string;
}

export interface CreateFilmSceneRequest {
  tenantId: string;
  sortOrder: number;
  description: string;
  dialogueText?: string | null;
  characters?: string | null;
  cameraMovements?: string | null;
  environment: string;
  anchorImageMode: FilmSceneAnchorImageMode;
  customImagePrompt?: string | null;
  anchorImageBase64?: string | null;
}

export function createFilmProduction(request: CreateFilmProductionRequest): Promise<FilmProduction> {
  return apiPost('/studio/film-productions', request);
}

export function listFilmProductions(): Promise<FilmProduction[]> {
  return apiGet('/studio/film-productions');
}

export function createFilmScene(filmProductionId: string, request: CreateFilmSceneRequest): Promise<FilmScene> {
  return apiPost(`/studio/film-productions/${filmProductionId}/scenes`, request);
}

/** Auto-storyboard mode: breaks a full script/story into 10-second scenes
 * and generates all of them — can take minutes for a long script. */
export function createFilmScenesFromScript(
  filmProductionId: string,
  request: {
    tenantId: string;
    fullScriptText: string;
    environmentsDescription?: string | null;
    maxScenes?: number | null;
    initialAnchorImageBase64?: string | null;
  }
): Promise<FilmScene[]> {
  return apiPost(`/studio/film-productions/${filmProductionId}/scenes/from-script`, request);
}

export function listFilmScenes(filmProductionId: string): Promise<FilmScene[]> {
  return apiGet(`/studio/film-productions/${filmProductionId}/scenes`);
}

export function deleteFilmScene(filmSceneId: string): Promise<void> {
  return apiDelete(`/studio/film-productions/scenes/${filmSceneId}`);
}

/** Re-runs both agents for an existing (typically Failed) scene, reusing its
 * already-stored raw fields \u2014 e.g. after an agent-instruction fix. */
export function retryFilmScene(filmSceneId: string): Promise<FilmScene> {
  return apiPost(`/studio/film-productions/scenes/${filmSceneId}/retry`, {});
}

export interface FilmProductionCost {
  SceneCount: number;
  CompletedImageCount: number;
  CompletedVideoCount: number;
  TotalInputTokens: number;
  TotalOutputTokens: number;
  TotalCachedInputTokens: number;
  EstimatedCostUsd: number;
  ImageCostUsd: number;
  InputCostUsd: number;
  OutputCostUsd: number;
}

export function getFilmProductionCost(filmProductionId: string): Promise<FilmProductionCost> {
  return apiGet(`/studio/film-productions/${filmProductionId}/cost`);
}

/** Backend serves the raw PNG bytes directly — build the <img src> URL, no JSON wrapper. */
export function getFilmSceneImageUrl(filmSceneId: string): string {
  const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:7071/api';
  return `${base}/studio/film-productions/scenes/${filmSceneId}/image`;
}

/** Generates (or regenerates) the review/edit video for one scene, image-to-video
 * from its final still + GeneratedPrompt (or an optional prompt override — e.g.
 * to retry with different wording after a content-policy rejection). Synchronous,
 * typically well under a minute. */
export function generateFilmSceneVideo(filmSceneId: string, prompt?: string): Promise<FilmScene> {
  return apiPost(`/studio/film-productions/scenes/${filmSceneId}/video`, prompt ? { prompt } : {});
}

/** Generates videos for every completed scene in the production that doesn't
 * already have one, sequentially — can take minutes for many scenes. */
export function generateFilmProductionVideos(filmProductionId: string): Promise<FilmScene[]> {
  return apiPost(`/studio/film-productions/${filmProductionId}/videos`, {});
}

/** Backend serves the raw MP4 bytes directly — build the <video src> URL, no JSON wrapper. */
export function getFilmSceneVideoUrl(filmSceneId: string): string {
  const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:7071/api';
  return `${base}/studio/film-productions/scenes/${filmSceneId}/video`;
}

/** Concatenates every completed scene's video (in SortOrder) into one final
 * film — local ffmpeg merge, no billed API call. Requires every completed
 * scene to already have a video. */
export function mergeFilmProductionVideos(filmProductionId: string): Promise<FilmProduction> {
  return apiPost(`/studio/film-productions/${filmProductionId}/merge`, {});
}

/** Backend serves the raw MP4 bytes directly — build the <video src> URL, no JSON wrapper. */
export function getFilmProductionVideoUrl(filmProductionId: string): string {
  const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:7071/api';
  return `${base}/studio/film-productions/${filmProductionId}/video`;
}
