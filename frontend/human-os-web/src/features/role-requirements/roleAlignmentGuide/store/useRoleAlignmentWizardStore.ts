import { useMemo } from 'react';
import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { HumanReviewDecision, RoleAlignmentFinding } from '../../types';
import {
  buildReviewSummary,
  buildWizardProgress,
  buildWizardSteps,
  getActiveWizardSteps,
  getNextStepId,
  getPreviousStepId,
  getStepFindings,
  projectReviewedRoleOperatingModel,
} from '../wizardSteps';
import type {
  CorrectionPayload,
  ReviewedRoleOperatingModel,
  RoleAlignmentFindingDecision,
  RoleAlignmentReviewSummary,
  RoleAlignmentWizardProgress,
  RoleAlignmentWizardStep,
  RoleAlignmentWizardStepId,
  RoleOperatingModelDraft,
} from '../types';

interface RoleAlignmentWizardState {
  draft: RoleOperatingModelDraft | null;
  findings: RoleAlignmentFinding[];
  /** Keyed by `RoleAlignmentFinding.id`. */
  decisions: Record<string, RoleAlignmentFindingDecision>;
  currentStepId: RoleAlignmentWizardStepId;
  isSummaryAcknowledged: boolean;
  isFinalReviewConfirmed: boolean;
  /** ISO date-time set once, when `confirmFinalReview()` succeeds. Not
   *  recomputed from the wall clock on every read. */
  reviewedDate: string | null;

  /** Loads a new agent draft + its findings and resets all review
   *  progress. Call this once when a fresh Role Alignment Guide pass
   *  is generated for the employee. */
  initializeReview: (draft: RoleOperatingModelDraft, findings: RoleAlignmentFinding[]) => void;
  /** Returns navigation to the Summary step without discarding any
   *  recorded decisions (unlike `resetReview`). */
  startReview: () => void;
  goToStep: (stepId: RoleAlignmentWizardStepId) => void;
  goToNextStep: () => void;
  goToPreviousStep: () => void;
  setFindingDecision: (
    findingId: string,
    decision: HumanReviewDecision,
    employeeNote?: string | null,
    correction?: CorrectionPayload | null,
  ) => void;
  clearFindingDecision: (findingId: string) => void;
  /** The employee has read the Summary and chosen to begin the
   *  dimension-by-dimension review. */
  markSummaryComplete: () => void;
  /** The employee has confirmed the Final Review. Only takes effect
   *  when every active dimension step is already complete. */
  confirmFinalReview: () => void;
  resetReview: () => void;
}

const INITIAL_REVIEW_STATE: Pick<
  RoleAlignmentWizardState,
  | 'draft'
  | 'findings'
  | 'decisions'
  | 'currentStepId'
  | 'isSummaryAcknowledged'
  | 'isFinalReviewConfirmed'
  | 'reviewedDate'
> = {
  draft: null,
  findings: [],
  decisions: {},
  currentStepId: 'summary',
  isSummaryAcknowledged: false,
  isFinalReviewConfirmed: false,
  reviewedDate: null,
};

/** State for the Role Alignment Guide's guided review wizard (Step 2C).
 *  Deliberately holds only the raw inputs (draft, findings, decisions,
 *  current position) — everything derivable (active steps, progress,
 *  reviewed/pending counts, follow-ups) is computed on demand by the
 *  selector hooks below via the pure functions in `wizardSteps.ts`,
 *  rather than duplicated into persisted state.
 */
export const useRoleAlignmentWizardStore = create<RoleAlignmentWizardState>()(
  persist(
    (set, get) => ({
      ...INITIAL_REVIEW_STATE,

      initializeReview: (draft, findings) =>
        set({
          draft,
          findings,
          decisions: {},
          currentStepId: 'summary',
          isSummaryAcknowledged: false,
          isFinalReviewConfirmed: false,
          reviewedDate: null,
        }),

      startReview: () => set({ currentStepId: 'summary' }),

      goToStep: (stepId) => set({ currentStepId: stepId }),

      goToNextStep: () => {
        const { draft, findings, decisions, currentStepId, isSummaryAcknowledged, isFinalReviewConfirmed } = get();
        if (!draft) return;
        const steps = buildWizardSteps(draft, findings, decisions, isSummaryAcknowledged, isFinalReviewConfirmed);
        const nextStepId = getNextStepId(getActiveWizardSteps(steps), currentStepId);
        if (nextStepId) set({ currentStepId: nextStepId });
      },

      goToPreviousStep: () => {
        const { draft, findings, decisions, currentStepId, isSummaryAcknowledged, isFinalReviewConfirmed } = get();
        if (!draft) return;
        const steps = buildWizardSteps(draft, findings, decisions, isSummaryAcknowledged, isFinalReviewConfirmed);
        const previousStepId = getPreviousStepId(getActiveWizardSteps(steps), currentStepId);
        if (previousStepId) set({ currentStepId: previousStepId });
      },

      setFindingDecision: (findingId, decision, employeeNote = null, correction = null) =>
        set((state) => {
          const existing = state.decisions[findingId];
          const now = new Date().toISOString();
          const record: RoleAlignmentFindingDecision = {
            findingId,
            decision,
            employeeNote: employeeNote ?? existing?.employeeNote ?? null,
            correction: correction ?? existing?.correction ?? null,
            decidedAt: existing?.decidedAt ?? now,
            lastUpdatedAt: now,
          };
          return { decisions: { ...state.decisions, [findingId]: record } };
        }),

      clearFindingDecision: (findingId) =>
        set((state) => {
          const { [findingId]: _removed, ...remaining } = state.decisions;
          return { decisions: remaining };
        }),

      markSummaryComplete: () => set({ isSummaryAcknowledged: true }),

      confirmFinalReview: () => {
        const { draft, findings, decisions, isSummaryAcknowledged } = get();
        if (!draft) return;
        const steps = buildWizardSteps(draft, findings, decisions, isSummaryAcknowledged, false);
        const finalReviewStep = steps.find((step) => step.id === 'finalReview');
        if (finalReviewStep?.isAvailable) {
          set({ isFinalReviewConfirmed: true, reviewedDate: new Date().toISOString() });
        }
      },

      resetReview: () => set({ ...INITIAL_REVIEW_STATE }),
    }),
    { name: 'human-os-role-alignment-wizard' },
  ),
);

// ---------------------------------------------------------------------
// Derived selector hooks. Nothing here is persisted state — each hook
// recomputes its result from the store's raw fields via the pure
// functions in `wizardSteps.ts`.
//
// IMPORTANT: each derived value is wrapped in `useMemo`, not returned
// directly from a zustand selector. `buildWizardSteps`/etc. always
// build a brand-new array/object, and a zustand selector that returns
// a new reference on every call makes `useSyncExternalStore` think the
// snapshot changed on every render — an infinite render loop ("Maximum
// update depth exceeded"). Selecting the raw fields (stable unless
// actually mutated) and memoizing the derived computation on top of
// them avoids that entirely.
// ---------------------------------------------------------------------

/** The full 12-step map (including unavailable steps + their omission
 *  reason). Use `useActiveWizardSteps` for the navigable sequence. */
export function useRoleAlignmentWizardSteps(): RoleAlignmentWizardStep[] {
  const draft = useRoleAlignmentWizardStore((state) => state.draft);
  const findings = useRoleAlignmentWizardStore((state) => state.findings);
  const decisions = useRoleAlignmentWizardStore((state) => state.decisions);
  const isSummaryAcknowledged = useRoleAlignmentWizardStore((state) => state.isSummaryAcknowledged);
  const isFinalReviewConfirmed = useRoleAlignmentWizardStore((state) => state.isFinalReviewConfirmed);

  return useMemo(
    () => buildWizardSteps(draft, findings, decisions, isSummaryAcknowledged, isFinalReviewConfirmed),
    [draft, findings, decisions, isSummaryAcknowledged, isFinalReviewConfirmed],
  );
}

/** Only the steps that are part of the active navigable sequence. */
export function useActiveWizardSteps(): RoleAlignmentWizardStep[] {
  const steps = useRoleAlignmentWizardSteps();
  return useMemo(() => getActiveWizardSteps(steps), [steps]);
}

export function useCurrentWizardStep(): RoleAlignmentWizardStep | undefined {
  const currentStepId = useRoleAlignmentWizardStore((state) => state.currentStepId);
  const steps = useRoleAlignmentWizardSteps();
  return useMemo(() => steps.find((step) => step.id === currentStepId), [steps, currentStepId]);
}

export function useRoleAlignmentWizardProgress(): RoleAlignmentWizardProgress {
  const currentStepId = useRoleAlignmentWizardStore((state) => state.currentStepId);
  const isFinalReviewConfirmed = useRoleAlignmentWizardStore((state) => state.isFinalReviewConfirmed);
  const steps = useRoleAlignmentWizardSteps();
  return useMemo(
    () => buildWizardProgress(steps, currentStepId, isFinalReviewConfirmed),
    [steps, currentStepId, isFinalReviewConfirmed],
  );
}

/** Findings belonging to the current step (`[]` for `summary`/`finalReview`,
 *  which aren't tied to a single finding category). */
export function useCurrentStepFindings(): RoleAlignmentFinding[] {
  const currentStepId = useRoleAlignmentWizardStore((state) => state.currentStepId);
  const findings = useRoleAlignmentWizardStore((state) => state.findings);
  return useMemo(() => getStepFindings(currentStepId, findings), [currentStepId, findings]);
}

export function useRoleAlignmentReviewSummary(): RoleAlignmentReviewSummary {
  const findings = useRoleAlignmentWizardStore((state) => state.findings);
  const decisions = useRoleAlignmentWizardStore((state) => state.decisions);
  return useMemo(() => buildReviewSummary(findings, decisions), [findings, decisions]);
}

/** `null` until there is a draft loaded. */
export function useReviewedRoleOperatingModel(): ReviewedRoleOperatingModel | null {
  const draft = useRoleAlignmentWizardStore((state) => state.draft);
  const findings = useRoleAlignmentWizardStore((state) => state.findings);
  const decisions = useRoleAlignmentWizardStore((state) => state.decisions);
  const reviewedDate = useRoleAlignmentWizardStore((state) => state.reviewedDate);

  return useMemo(() => {
    if (!draft) return null;
    return projectReviewedRoleOperatingModel(draft, findings, decisions, reviewedDate);
  }, [draft, findings, decisions, reviewedDate]);
}

