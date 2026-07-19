import { BackendCapabilityBlueprint, BackendCapabilityPackage } from './api/studioApi';

/**
 * Singleton store for the CURRENT real Studio capability-creation run.
 * Bridges data between pages without prop-drilling through the router:
 *   ObjectiveStep (writes runId/domainId)
 *   -> BlueprintStep (writes/reads blueprint + gate1SubjectId)
 *   -> StudioGenerationPage (reads blueprint, writes capabilityPackage + gate2SubjectId)
 *   -> StudioFinalReviewPage (reads/writes capabilityPackage)
 *   -> StudioPublicationPage (writes publishedResult)
 *
 * PERSISTED TO sessionStorage (fixed 2026-07-16 — a pure in-memory JS
 * module-level singleton was silently wiped by a Vite HMR reload of this
 * module (triggered by unrelated edits elsewhere in the app) mid-generation,
 * losing the user's only way to find their still-alive backend run —
 * StudioGenerationPage/StudioFinalReviewPage have NO fallback fetch by
 * runId, they only ever read this store). sessionStorage survives HMR and
 * hard refreshes within the same tab (still lost if the tab is closed —
 * matches the backend's own prototype-scoped in-memory-only run storage).
 */
export interface StudioRunState {
  runId: string | null;
  capabilityDomainId: string | null;
  blueprint: BackendCapabilityBlueprint | null;
  gate1SubjectId: string | null;
  capabilityPackage: BackendCapabilityPackage | null;
  gate2SubjectId: string | null;
  publishedResult: BackendCapabilityPackage | null;
}

const STORAGE_KEY = 'humanstudio.studioRun';

const emptyState: StudioRunState = {
  runId: null,
  capabilityDomainId: null,
  blueprint: null,
  gate1SubjectId: null,
  capabilityPackage: null,
  gate2SubjectId: null,
  publishedResult: null,
};

function loadInitialState(): StudioRunState {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return { ...emptyState };
    return { ...emptyState, ...(JSON.parse(raw) as Partial<StudioRunState>) };
  } catch {
    // Corrupt/inaccessible sessionStorage (e.g. private browsing edge
    // cases) — fall back to a fresh empty state rather than crashing.
    return { ...emptyState };
  }
}

function persist(next: StudioRunState): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next));
  } catch {
    // Storage full/unavailable — state still works in-memory for the
    // rest of this page's lifetime, it just won't survive a reload.
  }
}

let state: StudioRunState = loadInitialState();

export function getStudioRun(): StudioRunState {
  return state;
}

export function updateStudioRun(patch: Partial<StudioRunState>): void {
  state = { ...state, ...patch };
  persist(state);
}

/** Used by "Crear otra capability" to reset all temporary flow state. */
export function clearStudioRun(): void {
  state = { ...emptyState };
  persist(state);
}

/**
 * Flattens a blueprint's modules in the EXACT order the backend processes
 * them (Levels in declared order, then each level's Modules in declared
 * order — matches ModuleQueueInitializerExecutor's
 * `blueprint.Levels.SelectMany(level => level.Modules...)`). Used to
 * synthesize a per-module progress list from the backend's
 * counter+current-title-only progress payload.
 */
export function flattenBlueprintModules(blueprint: BackendCapabilityBlueprint) {
  return blueprint.Levels.flatMap((level) =>
    level.Modules.map((module) => ({
      title: module.Title,
      description: module.Description,
      type: module.Type,
      targetMetric: module.TargetMetric,
      layer: level.Layer,
    }))
  );
}
