import type { LocalizedText } from '@/localization/types';

export type RequirementCategory = 'core' | 'policy' | 'futureReady';

/** @deprecated Flat placeholder from an early pass, before the Role
 *  Operating Model existed. It collapses knowledge, procedures, and
 *  policies into "capability requirements with a level", which is the
 *  exact flattening the Role Operating Model (below) exists to correct.
 *  Kept only because `data/mockRoleRequirements.ts` still returns it;
 *  nothing else in the app consumes it. Do not build new UI on top of
 *  this — use `RoleOperatingModel` instead.
 */
export interface RoleRequirement {
  id: string;
  name: LocalizedText;
  category: RequirementCategory;
  /** 1-5 */
  requiredLevel: number;
  /** 0-5 */
  currentLevel: number;
  hasEvidence: boolean;
}

// ---------------------------------------------------------------------
// Step 2 — Your Role and Experience
// ---------------------------------------------------------------------

/** Where a piece of information originated, so the UI can always show
 *  its provenance rather than presenting everything as equally certain.
 */
export type AnalysisSource = 'organization' | 'jobDescription' | 'resume' | 'employeeDeclared' | 'agentInferred';

/** How validated a piece of information is — and *only* that. This is
 *  deliberately narrow: it does not say who validated it (see
 *  `ValidationAuthority`) and it does not say where it came from (see
 *  `AnalysisSource`, above). Earlier revisions conflated all three into
 *  one enum (`declared`/`inferred`/`confirmedByEmployee`/etc.), which
 *  made it impossible to say e.g. "inferred from résumé, validated by
 *  organization" without inventing a new combined value for every
 *  source × authority pairing. Splitting them keeps each question
 *  answerable independently:
 *    - Status: unvalidated | needsValidation | partiallyValidated | validated
 *    - Authority: who performed the validation (`ValidationAuthority`)
 *    - Source: where the underlying claim originated (`AnalysisSource`)
 */
export type ValidationStatus = 'unvalidated' | 'needsValidation' | 'partiallyValidated' | 'validated';

/** Who performed the validation referenced by a `ValidationStatus` of
 *  `validated`/`partiallyValidated`. Kept as its own enum so adding a
 *  new authority (e.g. a future external auditor) never forces
 *  `ValidationStatus` to grow a new combined value. `null` wherever
 *  nothing has been validated yet. */
export type ValidationAuthority = 'employee' | 'organization' | 'authorizedReviewer' | 'systemEvidence' | 'assessment';

/** Stages of the agentic Role Alignment Guide workflow. The guide must
 *  never treat its own output as final — `humanReview` is always the
 *  last stage, never `decide`, because the agent proposes and a human
 *  (employee or, for organization-owned facts, an authorized reviewer)
 *  decides. */
export type AgentWorkflowStage = 'understand' | 'ask' | 'analyze' | 'explain' | 'recommend' | 'humanReview';

/** What the human decided about an agent-proposed finding. Distinct
 *  from `ValidationStatus`:
 *    - `ValidationStatus` answers "how validated is this finding?"
 *    - `HumanReviewDecision` answers "what did the employee/reviewer
 *      decide to do about the agent's proposal?"
 *  A finding can be `needsValidation` while its review decision is
 *  still `pending`, or `corrected` (the human edited it) while it is
 *  now `partiallyValidated` pending organization confirmation. */
export type HumanReviewDecision =
  | 'pending'
  | 'accepted'
  | 'corrected'
  | 'rejected'
  | 'needsEvidence'
  | 'requestOrganizationReview';

/** A validity window for organization-defined facts that can go stale —
 *  policies, procedures, regulations, controls, tax requirements,
 *  responsible-AI-use rules, etc. Kept as a reusable struct rather than
 *  duplicating `effectiveFrom`/`effectiveTo`/`reviewDueDate` on every
 *  dimension that needs it. */
export interface EffectivePeriod {
  /** ISO date. `null` if already in effect with no recorded start. */
  effectiveFrom: string | null;
  /** ISO date. `null` if there is no known expiration. */
  effectiveTo: string | null;
  /** ISO date. When this should next be reviewed for continued
   *  validity — independent of `effectiveTo`, since some facts must be
   *  periodically re-confirmed even without a hard expiration. */
  reviewDueDate: string | null;
}

export interface JobResponsibility {
  id: string;
  text: LocalizedText;
}

export interface ExpectedOutcome {
  id: string;
  text: LocalizedText;
}

export type JobDescriptionOrigin = 'organization' | 'employeeProvided';

export interface JobDescription {
  jobTitle: LocalizedText;
  rolePurpose: LocalizedText;
  roleSummary: LocalizedText;
  primaryResponsibilities: JobResponsibility[];
  expectedOutcomes: ExpectedOutcome[];
  coreCapabilities: LocalizedText[];
  requiredKnowledge: LocalizedText[];
  toolsAndTechnologies: LocalizedText[];
  applicablePolicies: LocalizedText[];
  regulatoryRequirements: LocalizedText[];
  expectedExperience: LocalizedText | null;
  source: JobDescriptionOrigin;
  /** The organizational owner of this description (e.g. "HR",
   *  "Engineering Program Management") — `null` for employee-provided
   *  working descriptions, which have no organizational owner. */
  organizationOwner: LocalizedText | null;
  /** `null` when not tracked (e.g. an employee-provided draft). */
  version: string | null;
  lastUpdatedDate: string | null;
  verificationStatus: 'verified' | 'unverified';
}

export type ResumeUploadStatus = 'idle' | 'uploading' | 'processing' | 'extracted' | 'error';

export interface ResumeDocument {
  id: string;
  fileName: string;
  uploadedDate: string;
  /** Populated once the backend Data Lake upload succeeds. */
  storagePath: string | null;
}

/** A single preliminary item surfaced after a (currently simulated)
 *  agent extraction pass over an uploaded résumé. Always starts at
 *  `unvalidated` or `needsValidation` with `validationAuthority: null`
 *  — never `validated` — until the employee or future evidence
 *  confirms it. `source` stays `resume`/`agentInferred` throughout;
 *  it describes where the claim came from, not how validated it is.
 */
export interface DeclaredExperienceItem {
  id: string;
  text: LocalizedText;
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}


export interface JobRole {
  id: string;
  name: LocalizedText;
  requirements: RoleRequirement[];
}

// ---------------------------------------------------------------------
// Role Operating Model
// Spanish: Modelo Operativo del Rol
// ---------------------------------------------------------------------
//
// The Role Operating Model represents how a role is performed
// successfully *inside a particular organization*. It is broader than a
// Job Description (a document) and broader than a list of Capabilities
// (a catalog entry with a level). It is the future foundation for
// Capability Baseline, capability-gap analysis, Growth Path
// recommendations, Practice, Recall, Projects, Evidence, Assessments,
// role readiness, mastery, and value creation — none of which are
// implemented by this task. This task only establishes the shape.
//
// ROLE → EXPECTED OUTCOMES → CAPABILITIES → REQUIRED KNOWLEDGE →
// PROFESSIONAL METHODS → ORGANIZATIONAL PROCEDURES → POLICIES AND
// CONTROLS → TOOLS AND SYSTEMS → EVIDENCE → QUALITY AND INDEPENDENCE →
// BUSINESS VALUE
//
// CRITICAL: these dimensions are deliberately kept as separate
// interfaces. Do not collapse them into Capability. Running example
// (Financial Analyst) used throughout:
//   Capability: Financial Analysis
//   Knowledge:  Accounting principles and internal financial rules
//   Method:     Variance analysis
//   Procedure:  Monthly close procedure
//   Policy:     Expense approval policy
//   Control:    Segregation of duties
//   Tool:       ERP and approved financial template
//   Evidence:   Validated reconciliation and final financial report
//   Outcome:    Accurate and timely financial reporting
//   Quality:    No material errors
//   Independence: Completed without direct supervisor intervention

/** How important a requirement/outcome is to the role. Deliberately
 *  separate from `ValidationStatus` (certainty) and from any single
 *  numeric score — importance and confidence are different questions.
 */
export type ImportanceLevel = 'critical' | 'high' | 'medium' | 'low';

/** How often an expected outcome or governance item recurs. Used both
 *  by `RoleExpectedOutcome.frequency` and as a revalidation cadence for
 *  `GovernanceRequirement`. */
export type OutcomeFrequency = 'daily' | 'weekly' | 'monthly' | 'quarterly' | 'annual' | 'ongoing' | 'onDemand';

// --- Dimension 1 — Role Purpose -----------------------------------------
// "Why the role exists" — not the same as a list of responsibilities.

export interface RolePurpose {
  /** e.g. "The Financial Analyst supports accurate financial
   *  decision-making by producing reliable analysis, forecasts,
   *  reconciliations, and reports." */
  statement: LocalizedText;
  problemsSolved: LocalizedText[];
  /** People or business areas this role serves. */
  servedAudiences: LocalizedText[];
  organizationalValue: LocalizedText;
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 2 — Expected Outcomes ------------------------------------
// What the employee is expected to produce or achieve. Outcomes
// represent valuable *results*, never "tasks completed".

export interface RoleExpectedOutcome {
  id: string;
  name: LocalizedText;
  description: LocalizedText;
  importance: ImportanceLevel;
  frequency: OutcomeFrequency;
  stakeholders: LocalizedText[];
  qualityExpectation: LocalizedText | null;
  timelinessExpectation: LocalizedText | null;
  /** References into `evidenceExpectations` on the owning
   *  `RoleOperatingModel` — never a duplicate of the Evidence entity. */
  evidenceExpectationIds: string[];
  /** References into `businessValue` on the owning `RoleOperatingModel`. */
  businessValueIds: string[];
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 3 — Capabilities ------------------------------------------
// What the employee must be *capable of doing*. This is a role-specific
// requirement, not a duplicate capability catalog — it always points at
// an existing Capability (backend `Capability.CapabilityId`) and layers
// role context (required level, independence target, why it matters).

export type CapabilityRequirementType = 'coreRole' | 'futureReady' | 'organizationSpecific' | 'regulatory';

export interface RoleCapabilityRequirement {
  id: string;
  /** Backend `Capability.CapabilityId`. The catalog remains the single
   *  source of truth for the capability's name/domain/description. */
  capabilityId: string;
  capabilityName: LocalizedText;
  /** 1-5, same scale as `PersonCapability.CurrentLevel`/`TargetLevel`. */
  requiredLevel: number;
  /** Same scale as `PersonCapability.IndependenceLevel`. */
  targetIndependenceLevel: number;
  importance: ImportanceLevel;
  requirementType: CapabilityRequirementType;
  relatedOutcomeIds: string[];
  evidenceExpectationIds: string[];
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 4 — Required Knowledge ------------------------------------
// What the employee must know, understand, and retain. Not every
// knowledge requirement becomes a Capability — e.g. "the expense-approval
// policy" is knowledge to recall and apply, not a skill to rate 1-5.

export type KnowledgeCategory =
  | 'foundational'
  | 'roleSpecific'
  | 'organizationSpecific'
  | 'policyRelated'
  | 'regulatory'
  | 'toolRelated';

export interface RequiredKnowledgeItem {
  id: string;
  name: LocalizedText;
  description: LocalizedText;
  category: KnowledgeCategory;
  /** Whether this feeds Recall/retention activities (most knowledge
   *  does; some is purely reference material). */
  supportsRecall: boolean;
  sourceReference: LocalizedText | null;
  /** ISO date. When to review/renew this knowledge — relevant for
   *  regulatory or policy-related items that can go stale. */
  reviewDate: string | null;
  relatedCapabilityIds: string[];
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 5 — Professional Methods ----------------------------------
// A reusable, adaptable professional approach for analyzing/solving a
// class of problem (e.g. "variance analysis"). Not a fixed organizational
// sequence — that is a Procedure (Dimension 6).

export interface ProfessionalMethod {
  id: string;
  name: LocalizedText;
  description: LocalizedText;
  purpose: LocalizedText;
  applicableScenarios: LocalizedText[];
  relatedCapabilityIds: string[];
  relatedKnowledgeIds: string[];
  expectedReasoningSteps: LocalizedText[];
  qualityCriteria: LocalizedText[];
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 6 — Organizational Procedures ------------------------------
// The specific, organization-defined steps required (e.g. "monthly close
// procedure"). An employee may already have the general capability/method
// and still need to learn this organization's specific procedure — do
// not model a procedure as a capability.

export interface ProcedureStep {
  id: string;
  order: number;
  description: LocalizedText;
  isDecisionPoint: boolean;
  requiredApprovalRole: LocalizedText | null;
}

export interface OrganizationalProcedure {
  id: string;
  name: LocalizedText;
  description: LocalizedText;
  steps: ProcedureStep[];
  responsibleRoles: LocalizedText[];
  inputs: LocalizedText[];
  outputs: LocalizedText[];
  requiredToolIds: string[];
  requiredEvidenceExpectationIds: string[];
  escalationConditions: LocalizedText[];
  version: string | null;
  owner: LocalizedText | null;
  sourceDocument: LocalizedText | null;
  /** How long this procedure is/was in effect and when it's next due
   *  for review — organizations change their procedures over time. */
  effectivePeriod: EffectivePeriod;
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 7 — Policies, Controls, and Regulations --------------------
// Kept as one entity shape with a `kind` discriminant (rather than three
// near-identical interfaces) because they share the same readiness
// mechanics, but `kind` keeps them individually distinguishable and
// filterable — Human OS must not become a training portal, so readiness
// is demonstrated (recall, scenario application, evidence), never just
// "marked complete".

export type GovernanceRequirementKind = 'policy' | 'control' | 'regulation';

export type ReadinessDemonstrationMethod =
  | 'recall'
  | 'scenarioApplication'
  | 'decisionQuality'
  | 'proceduralBehavior'
  | 'evidence'
  | 'periodicRevalidation';

export interface GovernanceRequirement {
  id: string;
  kind: GovernanceRequirementKind;
  name: LocalizedText;
  description: LocalizedText;
  relatedProcedureIds: string[];
  relatedCapabilityIds: string[];
  readinessDemonstratedVia: ReadinessDemonstrationMethod[];
  revalidationFrequency: OutcomeFrequency | null;
  /** Policies, controls, and regulations change — this tracks whether
   *  this requirement is currently in force and when it's due for
   *  review (e.g. a tax rule or a responsible-AI-use policy). */
  effectivePeriod: EffectivePeriod;
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 8 — Tools and Systems --------------------------------------
// A tool is not a capability, e.g. Capability "Financial Analysis" uses
// Tool "Excel" following Procedure "corporate variance-analysis template".

export type ToolProficiencyLevel = 'awareness' | 'basic' | 'proficient' | 'advanced' | 'expert';

export interface ToolRequirement {
  id: string;
  name: LocalizedText;
  requiredProficiency: ToolProficiencyLevel;
  approvedUseBoundaries: LocalizedText | null;
  relatedCapabilityIds: string[];
  relatedProcedureIds: string[];
  requiresAccessApproval: boolean;
  requiresSecurityClearance: boolean;
  /** Reference into `evidenceExpectations`, not a duplicate entity. */
  evidenceOfUseExpectationId: string | null;
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 9 — Evidence Expectations ----------------------------------
// How the employee *can* demonstrate capability/readiness. This is an
// expectation/requirement only — actual submitted proof continues to use
// the existing Evidence domain (backend `Evidence.cs` / `CapabilityEvidence`).
// Never duplicate the Evidence entity here.

export type EvidenceExpectationType =
  | 'validatedReport'
  | 'completedProcess'
  | 'approvedDeliverable'
  | 'presentation'
  | 'scenarioSimulation'
  | 'supervisorObservation'
  | 'portfolioArtifact'
  | 'assessmentResult'
  | 'practicalDemonstration';

export interface EvidenceExpectation {
  id: string;
  type: EvidenceExpectationType;
  description: LocalizedText;
  relatedCapabilityIds: string[];
  relatedOutcomeIds: string[];
  relatedProcedureIds: string[];
  qualityCriteria: LocalizedText[];
  requiredReviewer: LocalizedText | null;
  visibility: 'private' | 'manager' | 'organization';
  expirationOrRenewalDate: string | null;
  /** Backend `Evidence.EvidenceId` values that have fulfilled this
   *  expectation. Empty until real evidence exists — this dimension
   *  never stores the evidence content itself. */
  fulfillingEvidenceIds: string[];
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Dimension 10 — Quality, Assistance, and Independence -----------------
// What successful, increasingly independent performance means.
// Deliberately kept as separate criteria rather than one combined score —
// they may later feed Mastery and Role Readiness, but are not the same
// measurement.

export interface PerformanceCriteria {
  accuracy: LocalizedText | null;
  quality: LocalizedText | null;
  timeliness: LocalizedText | null;
  /** e.g. "No direct supervisor intervention". */
  assistanceLevel: LocalizedText | null;
  /** Same scale as `PersonCapability.IndependenceLevel`. */
  independenceLevel: number | null;
  /** e.g. "Can resolve an unfamiliar discrepancy". */
  transferToNewSituations: LocalizedText | null;
  retention: LocalizedText | null;
  repeatability: LocalizedText | null;
  evidenceValidation: LocalizedText | null;
}

// --- Dimension 11 — Business Value ----------------------------------------
// Why successful performance matters. Only establishes a reference shape
// for a future Organizational Priorities relationship — that feature is
// explicitly out of scope for this task.

export interface BusinessValueReference {
  id: string;
  statement: LocalizedText;
  /** Left `null` until Organizational Priorities exists. */
  relatedOrganizationalPriorityId: string | null;
  source: AnalysisSource;
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
}

// --- Aggregate root --------------------------------------------------------

export interface RoleOperatingModel {
  id: string;
  /** Ties this operating model back to the Step 2 `JobDescription`/role
   *  this employee is working through. */
  jobRoleId: string;
  purpose: RolePurpose;
  expectedOutcomes: RoleExpectedOutcome[];
  capabilityRequirements: RoleCapabilityRequirement[];
  requiredKnowledge: RequiredKnowledgeItem[];
  professionalMethods: ProfessionalMethod[];
  organizationalProcedures: OrganizationalProcedure[];
  governanceRequirements: GovernanceRequirement[];
  toolRequirements: ToolRequirement[];
  evidenceExpectations: EvidenceExpectation[];
  performanceCriteria: PerformanceCriteria;
  businessValue: BusinessValueReference[];
  lastUpdatedDate: string | null;
}

// ---------------------------------------------------------------------
// Role Alignment Guide findings
// Spanish: Guía de Alineación Profesional
// ---------------------------------------------------------------------
//
// Generalizes across all 11 dimensions rather than one finding type per
// dimension, since every dimension needs the exact same
// understand → ask → analyze workflow and the same provenance rules:
// the agent must distinguish organization-provided, employee-provided,
// agent-inferred, employee-confirmed, organization-validated,
// evidence-supported, and needs-validation — never invent organizational
// facts (procedures, policies, tools) that were not actually provided.

export type RoleAlignmentFindingCategory =
  | 'rolePurpose'
  | 'expectedOutcome'
  | 'capability'
  | 'knowledge'
  | 'method'
  | 'procedure'
  | 'governance'
  | 'tool'
  | 'evidenceExpectation'
  | 'performanceCriteria'
  | 'businessValue';

export interface RoleAlignmentFinding {
  id: string;
  category: RoleAlignmentFindingCategory;
  /** ID of the concrete dimension entity on `RoleOperatingModel` this
   *  finding proposes (e.g. a `RequiredKnowledgeItem.id`). `null` while
   *  still a draft proposal not yet attached to the model. */
  proposedEntityId: string | null;
  summary: LocalizedText;
  rationale: LocalizedText | null;
  source: AnalysisSource;
  /** How validated the underlying finding is. */
  validationStatus: ValidationStatus;
  validationAuthority: ValidationAuthority | null;
  /** Where the agent is in its own workflow for this finding. Always
   *  ends at `humanReview` — the agent explains and recommends, it
   *  never finalizes. */
  stage: AgentWorkflowStage;
  /** What the human decided about this finding lives in
   *  `RoleAlignmentFindingDecision` (see the Role Alignment Guide
   *  wizard's `roleAlignmentGuide/types.ts`), keyed separately by
   *  `findingId` — never as a mutable field here. This keeps the
   *  agent's original proposal auditable no matter how many times the
   *  employee revises their decision. */
}


