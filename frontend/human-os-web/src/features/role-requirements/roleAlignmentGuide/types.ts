import type { LocalizedText } from '@/localization/types';
import type {
  BusinessValueReference,
  EvidenceExpectation,
  GovernanceRequirement,
  HumanReviewDecision,
  OrganizationalProcedure,
  PerformanceCriteria,
  ProfessionalMethod,
  RequiredKnowledgeItem,
  RoleAlignmentFinding,
  RoleAlignmentFindingCategory,
  RoleCapabilityRequirement,
  RoleExpectedOutcome,
  RoleOperatingModel,
  RolePurpose,
  ToolRequirement,
} from '../types';

// ---------------------------------------------------------------------
// Role Alignment Guide — guided review wizard (Step 2C)
// ---------------------------------------------------------------------
//
// RoleOperatingModelDraft (agent proposal, nothing confirmed)
//         ↓ guided dimension review, one wizard step per dimension
// RoleAlignmentFindingDecision per finding (HumanReviewDecision)
//         ↓
// ReviewedRoleOperatingModel (deterministic projection)
//
// This file only defines the typed state foundation. No UI, no agent
// calls, no Growth Paths, no Organizational Priorities.

/** The agent's not-yet-reviewed proposal for a role's operating model.
 *  Identical shape to `RoleOperatingModel` — aliased so it's always
 *  clear at a type level whether code is holding an unreviewed agent
 *  draft or something an employee has already reviewed. */
export type RoleOperatingModelDraft = RoleOperatingModel;

// --- Wizard step identifiers ---------------------------------------------
// Stable string identifiers, not display labels. Display labels come from
// typed localization dictionaries at UI time (not part of this task).

export type RoleAlignmentWizardStepId =
  | 'summary'
  | 'expectedOutcomes'
  | 'capabilities'
  | 'requiredKnowledge'
  | 'professionalMethods'
  | 'organizationalProcedures'
  | 'governance'
  | 'tools'
  | 'evidenceExpectations'
  | 'performanceCriteria'
  | 'businessValue'
  | 'finalReview';

/** Fixed, deterministic ordering for every wizard step. `summary` and
 *  `finalReview` are always present; every step in between is a
 *  dimension review step that may or may not be active for a given
 *  draft (see `buildWizardSteps` in `wizardSteps.ts`). */
export const WIZARD_STEP_ORDER: readonly RoleAlignmentWizardStepId[] = [
  'summary',
  'expectedOutcomes',
  'capabilities',
  'requiredKnowledge',
  'professionalMethods',
  'organizationalProcedures',
  'governance',
  'tools',
  'evidenceExpectations',
  'performanceCriteria',
  'businessValue',
  'finalReview',
];

/** Maps each dimension review step to the `RoleAlignmentFinding`
 *  category it reviews. `summary` and `finalReview` are not tied to a
 *  single category (Role Purpose findings surface within `summary`
 *  instead of a dedicated step), so they're intentionally absent here. */
export const WIZARD_STEP_FINDING_CATEGORY: Partial<Record<RoleAlignmentWizardStepId, RoleAlignmentFindingCategory>> =
  {
    expectedOutcomes: 'expectedOutcome',
    capabilities: 'capability',
    requiredKnowledge: 'knowledge',
    professionalMethods: 'method',
    organizationalProcedures: 'procedure',
    governance: 'governance',
    tools: 'tool',
    evidenceExpectations: 'evidenceExpectation',
    performanceCriteria: 'performanceCriteria',
    businessValue: 'businessValue',
  };

export type RoleAlignmentWizardStepStatus = 'notStarted' | 'inProgress' | 'complete';

export interface RoleAlignmentWizardStep {
  id: RoleAlignmentWizardStepId;
  /** `null` for `summary`/`finalReview`, which aren't tied to one
   *  finding category. */
  category: RoleAlignmentFindingCategory | null;
  order: number;
  /** Semantic importance, not a completion requirement. `summary`,
   *  `capabilities`, and `finalReview` are marked required; every other
   *  dimension is optional because a role may legitimately have none of
   *  it (e.g. no documented organizational procedures yet). */
  isRequired: boolean;
  status: RoleAlignmentWizardStepStatus;
  findingCount: number;
  reviewedCount: number;
  pendingCount: number;
  /** Whether this step is part of the active sequence for the current
   *  draft. Steps that are not available are omitted from navigation
   *  entirely — never rendered as an empty screen. */
  isAvailable: boolean;
  /** Set only when `isAvailable` is `false`, explaining why (e.g. "No
   *  organizational procedures identified for this role"). Not a
   *  user-facing string yet — a stable reason code for the future
   *  localized message to key off of. */
  omittedReason: string | null;
}

export interface RoleAlignmentWizardProgress {
  steps: RoleAlignmentWizardStep[];
  currentStepId: RoleAlignmentWizardStepId;
  currentStepIndex: number;
  totalSteps: number;
  completedSteps: number;
  isFinalReviewAvailable: boolean;
  isComplete: boolean;
}

// --- Finding decisions ----------------------------------------------------

/** An employee-proposed correction. Never an authoritative replacement
 *  of organization-owned source data — for organization-provided
 *  requirements this is a contextual note/proposal that still requires
 *  `requestOrganizationReview` to become authoritative.
 *
 *  Plain `string`, not `LocalizedText`: this is free text the employee
 *  types in whichever language they're using, not bilingual content
 *  authored for both locales.
 */
export interface CorrectionPayload {
  correctedValue: string | null;
  note: string | null;
  requiresOrganizationReview: boolean;
}

/** A human decision about one `RoleAlignmentFinding`, stored separately
 *  from the finding itself so the original agent proposal always stays
 *  auditable. Only the latest decision is kept (see TODO below) —
 *  revising a decision overwrites `decision`/`employeeNote`/`correction`
 *  and bumps `lastUpdatedAt`, but `decidedAt` is preserved from the
 *  first time this finding received a non-pending decision.
 *
 *  TODO: preserve full decision history (every prior decision +
 *  timestamp) once an audit-trail requirement exists. Skipped for now
 *  per the "don't over-engineer" guidance — today only the current
 *  decision needs to be reviewable/revisable.
 */
export interface RoleAlignmentFindingDecision {
  findingId: string;
  decision: HumanReviewDecision;
  employeeNote: string | null;
  correction: CorrectionPayload | null;
  decidedAt: string;
  lastUpdatedAt: string;
}

/** The wizard's own persisted state: which draft/findings it's
 *  reviewing, the decisions recorded so far, and where the employee
 *  currently is in the sequence. */
export interface RoleAlignmentReviewState {
  draft: RoleOperatingModelDraft | null;
  findings: RoleAlignmentFinding[];
  /** Keyed by `RoleAlignmentFinding.id`. */
  decisions: Record<string, RoleAlignmentFindingDecision>;
  currentStepId: RoleAlignmentWizardStepId;
  isSummaryAcknowledged: boolean;
  isFinalReviewConfirmed: boolean;
  /** ISO date-time set once, when `confirmFinalReview()` succeeds.
   *  Not recomputed from the wall clock on every read — it's a
   *  point-in-time fact, not a derived value. */
  reviewedDate: string | null;
}

// --- Final review projection ----------------------------------------------

/** Wraps a draft dimension entity together with its review outcome,
 *  rather than merging corrections into ten different entity shapes.
 *  `findingId`/`humanReviewDecision` are `null` when the entity was
 *  never agent-proposed in the first place (e.g. already-established,
 *  organization-validated data that never needed employee review). */
export interface ReviewedItem<T> {
  value: T;
  findingId: string | null;
  humanReviewDecision: HumanReviewDecision | null;
  correction: CorrectionPayload | null;
}

export interface RoleAlignmentFindingFollowUp {
  findingId: string;
  category: RoleAlignmentFindingCategory;
  summary: LocalizedText;
}

export interface RoleAlignmentReviewSummary {
  totalFindings: number;
  reviewedFindings: number;
  pendingFindings: number;
  countsByCategory: Partial<Record<RoleAlignmentFindingCategory, number>>;
  followUps: {
    needsEvidence: RoleAlignmentFindingFollowUp[];
    pendingOrganizationReview: RoleAlignmentFindingFollowUp[];
  };
}

/** The deterministic result of projecting a `RoleOperatingModelDraft` +
 *  its findings through the employee's `HumanReviewDecision`s:
 *    - `accepted`   → item included as-is
 *    - `corrected`  → item included, `correction` populated
 *    - `rejected`   → item excluded here (still present in `findings`
 *                     for audit)
 *    - `needsEvidence` / `requestOrganizationReview` → included as a
 *                     provisional follow-up item, never as validated
 *    - `pending` (or no decision at all for an agent-proposed item)
 *                   → excluded; never shown as confirmed
 *  Employee acceptance is a review decision, not proof of mastery or
 *  organizational validation — `validationStatus`/`validationAuthority`
 *  on the wrapped `value` are left untouched by this projection.
 */
export interface ReviewedRoleOperatingModel {
  id: string;
  jobRoleId: string;
  /** Passed through unchanged — Role Purpose is a single synthesized
   *  narrative acknowledged during the Summary step, not a list of
   *  individually decided findings. */
  purpose: RolePurpose;
  expectedOutcomes: ReviewedItem<RoleExpectedOutcome>[];
  capabilityRequirements: ReviewedItem<RoleCapabilityRequirement>[];
  requiredKnowledge: ReviewedItem<RequiredKnowledgeItem>[];
  professionalMethods: ReviewedItem<ProfessionalMethod>[];
  organizationalProcedures: ReviewedItem<OrganizationalProcedure>[];
  governanceRequirements: ReviewedItem<GovernanceRequirement>[];
  toolRequirements: ReviewedItem<ToolRequirement>[];
  evidenceExpectations: ReviewedItem<EvidenceExpectation>[];
  /** Passed through unchanged, same reasoning as `purpose` — it's a
   *  single aggregate object, not an addressable list of findings. */
  performanceCriteria: PerformanceCriteria;
  businessValue: ReviewedItem<BusinessValueReference>[];
  followUps: RoleAlignmentReviewSummary['followUps'];
  /** ISO date-time. `null` until `confirmFinalReview()` has been
   *  called. */
  reviewedDate: string | null;
}
