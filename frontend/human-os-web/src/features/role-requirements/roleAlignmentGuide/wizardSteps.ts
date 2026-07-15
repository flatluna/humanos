import type {
  PerformanceCriteria,
  RoleAlignmentFinding,
  RoleAlignmentFindingCategory,
} from '../types';
import {
  WIZARD_STEP_FINDING_CATEGORY,
  WIZARD_STEP_ORDER,
} from './types';
import type {
  ReviewedItem,
  ReviewedRoleOperatingModel,
  RoleAlignmentFindingDecision,
  RoleAlignmentFindingFollowUp,
  RoleAlignmentReviewSummary,
  RoleAlignmentWizardProgress,
  RoleAlignmentWizardStep,
  RoleAlignmentWizardStepId,
  RoleAlignmentWizardStepStatus,
  RoleOperatingModelDraft,
} from './types';

// ---------------------------------------------------------------------
// Pure functions for the Role Alignment Guide wizard. No React, no
// store access — everything here is a deterministic function of its
// arguments so it can be unit tested and reused from the store.
// ---------------------------------------------------------------------

/** Findings belonging to a given dimension step. `summary`/`finalReview`
 *  aren't tied to one category, so they always return an empty array
 *  here — the store computes their `findingCount` from the full
 *  collection instead (see `buildWizardSteps`). */
export function getStepFindings(
  stepId: RoleAlignmentWizardStepId,
  findings: RoleAlignmentFinding[],
): RoleAlignmentFinding[] {
  const category = WIZARD_STEP_FINDING_CATEGORY[stepId];
  if (!category) return [];
  return findings.filter((finding) => finding.category === category);
}

/** A finding counts as reviewed once it has a decision and that
 *  decision is not `pending`. An undefined decision (never touched) is
 *  always unresolved. */
export function isFindingResolved(decision: RoleAlignmentFindingDecision | undefined): boolean {
  return decision !== undefined && decision.decision !== 'pending';
}

function hasPerformanceCriteriaData(criteria: PerformanceCriteria): boolean {
  return Object.values(criteria).some((value) => value !== null);
}

/** How many draft entities exist for a given dimension step, regardless
 *  of whether any of them have an agent finding attached. Used to
 *  decide whether an optional step should stay in the active sequence
 *  even when it has zero *findings* (e.g. already-established,
 *  previously validated procedures with nothing left to review). */
function getDimensionItemCount(stepId: RoleAlignmentWizardStepId, draft: RoleOperatingModelDraft): number {
  switch (stepId) {
    case 'expectedOutcomes':
      return draft.expectedOutcomes.length;
    case 'capabilities':
      return draft.capabilityRequirements.length;
    case 'requiredKnowledge':
      return draft.requiredKnowledge.length;
    case 'professionalMethods':
      return draft.professionalMethods.length;
    case 'organizationalProcedures':
      return draft.organizationalProcedures.length;
    case 'governance':
      return draft.governanceRequirements.length;
    case 'tools':
      return draft.toolRequirements.length;
    case 'evidenceExpectations':
      return draft.evidenceExpectations.length;
    case 'performanceCriteria':
      return hasPerformanceCriteriaData(draft.performanceCriteria) ? 1 : 0;
    case 'businessValue':
      return draft.businessValue.length;
    default:
      return 0;
  }
}

function computeDimensionStepStatus(findingCount: number, reviewedCount: number): RoleAlignmentWizardStepStatus {
  if (findingCount === 0) return 'complete';
  if (reviewedCount === 0) return 'notStarted';
  if (reviewedCount < findingCount) return 'inProgress';
  return 'complete';
}

/** Builds the full, deterministic 12-step map for a draft — including
 *  steps that are not currently available (`isAvailable: false`, with
 *  an `omittedReason`). Use `getActiveWizardSteps` to get the navigable
 *  sequence. Returns `[]` when there is no draft to review yet. */
export function buildWizardSteps(
  draft: RoleOperatingModelDraft | null,
  findings: RoleAlignmentFinding[],
  decisions: Record<string, RoleAlignmentFindingDecision>,
  isSummaryAcknowledged: boolean,
  isFinalReviewConfirmed: boolean,
): RoleAlignmentWizardStep[] {
  if (!draft) return [];

  const steps: RoleAlignmentWizardStep[] = [];
  let order = 1;

  const totalFindings = findings.length;
  const totalReviewed = findings.filter((finding) => isFindingResolved(decisions[finding.id])).length;

  // "summary" is always first and always available — its completion is
  // driven by the employee choosing to begin the review, not by
  // per-finding decisions.
  steps.push({
    id: 'summary',
    category: null,
    order: order++,
    isRequired: true,
    status: isSummaryAcknowledged ? 'complete' : 'notStarted',
    findingCount: totalFindings,
    reviewedCount: totalReviewed,
    pendingCount: totalFindings - totalReviewed,
    isAvailable: true,
    omittedReason: null,
  });

  const dimensionStepIds = WIZARD_STEP_ORDER.filter((id) => id !== 'summary' && id !== 'finalReview');

  for (const stepId of dimensionStepIds) {
    const stepFindings = getStepFindings(stepId, findings);
    const itemCount = getDimensionItemCount(stepId, draft);
    const isAvailable = stepFindings.length > 0 || itemCount > 0;
    const category: RoleAlignmentFindingCategory | null = WIZARD_STEP_FINDING_CATEGORY[stepId] ?? null;
    // "capabilities" is the one dimension explicitly called out as
    // always-included whenever the role has capability requirements;
    // every other dimension is genuinely optional.
    const isRequired = stepId === 'capabilities';

    if (!isAvailable) {
      steps.push({
        id: stepId,
        category,
        order: order++,
        isRequired,
        status: 'complete',
        findingCount: 0,
        reviewedCount: 0,
        pendingCount: 0,
        isAvailable: false,
        omittedReason: `No ${stepId} identified for this role.`,
      });
      continue;
    }

    const reviewedCount = stepFindings.filter((finding) => isFindingResolved(decisions[finding.id])).length;
    steps.push({
      id: stepId,
      category,
      order: order++,
      isRequired,
      status: computeDimensionStepStatus(stepFindings.length, reviewedCount),
      findingCount: stepFindings.length,
      reviewedCount,
      pendingCount: stepFindings.length - reviewedCount,
      isAvailable: true,
      omittedReason: null,
    });
  }

  // "finalReview" only becomes available once every *active* dimension
  // step (summary excluded) is complete.
  const activeDimensionSteps = steps.filter((step) => step.id !== 'summary' && step.isAvailable);
  const finalReviewAvailable = activeDimensionSteps.every((step) => step.status === 'complete');

  steps.push({
    id: 'finalReview',
    category: null,
    order: order++,
    isRequired: true,
    status: isFinalReviewConfirmed ? 'complete' : 'notStarted',
    findingCount: totalFindings,
    reviewedCount: totalReviewed,
    pendingCount: totalFindings - totalReviewed,
    isAvailable: finalReviewAvailable,
    omittedReason: finalReviewAvailable ? null : 'Complete all dimension reviews first.',
  });

  return steps;
}

/** The navigable sequence: every step from `buildWizardSteps` that is
 *  actually available, in order. This is "the active wizard-step
 *  sequence" — never render an unavailable step as an empty screen. */
export function getActiveWizardSteps(steps: RoleAlignmentWizardStep[]): RoleAlignmentWizardStep[] {
  return steps.filter((step) => step.isAvailable);
}

export function getStepIndex(activeSteps: RoleAlignmentWizardStep[], stepId: RoleAlignmentWizardStepId): number {
  return activeSteps.findIndex((step) => step.id === stepId);
}

export function getNextStepId(
  activeSteps: RoleAlignmentWizardStep[],
  currentStepId: RoleAlignmentWizardStepId,
): RoleAlignmentWizardStepId | null {
  const index = getStepIndex(activeSteps, currentStepId);
  if (index === -1 || index === activeSteps.length - 1) return null;
  return activeSteps[index + 1].id;
}

export function getPreviousStepId(
  activeSteps: RoleAlignmentWizardStep[],
  currentStepId: RoleAlignmentWizardStepId,
): RoleAlignmentWizardStepId | null {
  const index = getStepIndex(activeSteps, currentStepId);
  if (index <= 0) return null;
  return activeSteps[index - 1].id;
}

/** Whether the current step's review can advance to the next one —
 *  i.e. it has no reviewable finding still left `pending`. `summary`
 *  and `finalReview` use their own `status`, which already reflects
 *  their special-cased completion rules. */
export function canContinueFromStep(step: RoleAlignmentWizardStep | undefined): boolean {
  if (!step) return false;
  return step.status === 'complete';
}

export function buildWizardProgress(
  steps: RoleAlignmentWizardStep[],
  currentStepId: RoleAlignmentWizardStepId,
  isFinalReviewConfirmed: boolean,
): RoleAlignmentWizardProgress {
  const activeSteps = getActiveWizardSteps(steps);
  const finalReviewStep = steps.find((step) => step.id === 'finalReview');
  const isFinalReviewAvailable = finalReviewStep?.isAvailable ?? false;

  return {
    steps: activeSteps,
    currentStepId,
    currentStepIndex: getStepIndex(activeSteps, currentStepId),
    totalSteps: activeSteps.length,
    completedSteps: activeSteps.filter((step) => step.status === 'complete').length,
    isFinalReviewAvailable,
    isComplete: isFinalReviewAvailable && isFinalReviewConfirmed,
  };
}

export function buildReviewSummary(
  findings: RoleAlignmentFinding[],
  decisions: Record<string, RoleAlignmentFindingDecision>,
): RoleAlignmentReviewSummary {
  const countsByCategory: Partial<Record<RoleAlignmentFindingCategory, number>> = {};
  const needsEvidence: RoleAlignmentFindingFollowUp[] = [];
  const pendingOrganizationReview: RoleAlignmentFindingFollowUp[] = [];
  let reviewedFindings = 0;

  for (const finding of findings) {
    countsByCategory[finding.category] = (countsByCategory[finding.category] ?? 0) + 1;

    const decision = decisions[finding.id];
    if (isFindingResolved(decision)) {
      reviewedFindings += 1;
    }
    if (decision?.decision === 'needsEvidence') {
      needsEvidence.push({ findingId: finding.id, category: finding.category, summary: finding.summary });
    }
    if (decision?.decision === 'requestOrganizationReview') {
      pendingOrganizationReview.push({ findingId: finding.id, category: finding.category, summary: finding.summary });
    }
  }

  return {
    totalFindings: findings.length,
    reviewedFindings,
    pendingFindings: findings.length - reviewedFindings,
    countsByCategory,
    followUps: { needsEvidence, pendingOrganizationReview },
  };
}

/** Projects a `value` array + its `category`'s findings + decisions
 *  into a `ReviewedItem<T>[]`:
 *    - no matching finding  → passes through untouched (never agent-proposed)
 *    - pending / no decision → excluded (never shown as confirmed)
 *    - rejected              → excluded (stays in `findings` for audit)
 *    - accepted/corrected/needsEvidence/requestOrganizationReview → included,
 *      tagged with the decision (and correction, if any)
 */
function projectDimension<T extends { id: string }>(
  items: T[],
  category: RoleAlignmentFindingCategory,
  findings: RoleAlignmentFinding[],
  decisions: Record<string, RoleAlignmentFindingDecision>,
): ReviewedItem<T>[] {
  const findingByEntityId = new Map<string, RoleAlignmentFinding>();
  for (const finding of findings) {
    if (finding.category === category && finding.proposedEntityId) {
      findingByEntityId.set(finding.proposedEntityId, finding);
    }
  }

  const result: ReviewedItem<T>[] = [];
  for (const item of items) {
    const finding = findingByEntityId.get(item.id);
    if (!finding) {
      result.push({ value: item, findingId: null, humanReviewDecision: null, correction: null });
      continue;
    }

    const decision = decisions[finding.id];
    if (!decision || decision.decision === 'pending' || decision.decision === 'rejected') {
      continue;
    }

    result.push({
      value: item,
      findingId: finding.id,
      humanReviewDecision: decision.decision,
      correction: decision.correction,
    });
  }
  return result;
}

/** Deterministically derives the `ReviewedRoleOperatingModel` from the
 *  original draft, its findings, and the recorded human decisions.
 *  Never mutates `draft`. Never populates `validationAuthority` —
 *  employee acceptance is a review decision, not a validation event. */
export function projectReviewedRoleOperatingModel(
  draft: RoleOperatingModelDraft,
  findings: RoleAlignmentFinding[],
  decisions: Record<string, RoleAlignmentFindingDecision>,
  reviewedDate: string | null = null,
): ReviewedRoleOperatingModel {
  return {
    id: draft.id,
    jobRoleId: draft.jobRoleId,
    purpose: draft.purpose,
    expectedOutcomes: projectDimension(draft.expectedOutcomes, 'expectedOutcome', findings, decisions),
    capabilityRequirements: projectDimension(draft.capabilityRequirements, 'capability', findings, decisions),
    requiredKnowledge: projectDimension(draft.requiredKnowledge, 'knowledge', findings, decisions),
    professionalMethods: projectDimension(draft.professionalMethods, 'method', findings, decisions),
    organizationalProcedures: projectDimension(draft.organizationalProcedures, 'procedure', findings, decisions),
    governanceRequirements: projectDimension(draft.governanceRequirements, 'governance', findings, decisions),
    toolRequirements: projectDimension(draft.toolRequirements, 'tool', findings, decisions),
    evidenceExpectations: projectDimension(draft.evidenceExpectations, 'evidenceExpectation', findings, decisions),
    performanceCriteria: draft.performanceCriteria,
    businessValue: projectDimension(draft.businessValue, 'businessValue', findings, decisions),
    followUps: buildReviewSummary(findings, decisions).followUps,
    reviewedDate,
  };
}
