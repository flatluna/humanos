import { BackendCapabilityBlueprint, BackendCapabilityPackage } from './api/studioApi';

/**
 * Singleton in-memory store for the CURRENT real Studio capability-creation
 * run (module-level, same "shared mutable singleton" pattern already used
 * by the mock*Api.ts files elsewhere in this app). Bridges data between
 * pages without prop-drilling through the router:
 *   ObjectiveStep (writes runId/domainId)
 *   -> BlueprintStep (writes/reads blueprint + gate1SubjectId)
 *   -> StudioGenerationPage (reads blueprint, writes capabilityPackage + gate2SubjectId)
 *   -> StudioFinalReviewPage (reads/writes capabilityPackage)
 *   -> StudioPublicationPage (writes publishedResult)
 *
 * KNOWN LIMITATION (matches the backend's own prototype scope): this is
 * pure client-side memory, lost on a hard page refresh — same limitation
 * already accepted for the mock singletons in Pasos 9-11.
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

let state: StudioRunState = {
  runId: null,
  capabilityDomainId: null,
  blueprint: null,
  gate1SubjectId: null,
  capabilityPackage: null,
  gate2SubjectId: null,
  publishedResult: null,
};

export function getStudioRun(): StudioRunState {
  return state;
}

export function updateStudioRun(patch: Partial<StudioRunState>): void {
  state = { ...state, ...patch };
}

/** Used by "Crear otra capability" to reset all temporary flow state. */
export function clearStudioRun(): void {
  state = {
    runId: null,
    capabilityDomainId: null,
    blueprint: null,
    gate1SubjectId: null,
    capabilityPackage: null,
    gate2SubjectId: null,
    publishedResult: null,
  };
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
