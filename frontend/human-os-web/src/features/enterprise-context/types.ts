export interface EnterpriseContext {
  organizationName: string;
  department: string;
  team: string;
  roleName: string;
  managerName?: string;
}

export type EmploymentType = 'fullTime' | 'partTime' | 'contractor' | 'intern';

export type WorkContextDataSource = 'organization' | 'employeeProfile' | 'temporaryPlaceholder';

export type VerificationStatus = 'verified' | 'unverified';

export type WorkContextConfirmationStatus = 'unconfirmed' | 'confirmed';

export type PendingCorrectionStatus = 'none' | 'submitted';

/** Marks whether a given field's value came from a real backend response
 *  or is a temporary placeholder awaiting a future Azure Function/field.
 *  Never mix these silently — every field the UI renders must know its
 *  own provenance.
 */
export interface WorkContextFieldProvenance {
  source: WorkContextDataSource;
  /** Present only when `source` is `'temporaryPlaceholder'`. */
  todo?: string;
}

/** The page model consumed by the Work Context screen. Combines real
 *  data (Tenant, Person, PersonProfile) with clearly-marked temporary
 *  placeholders for enterprise HR concepts the backend does not yet
 *  model (business unit, department, team, role, manager, etc.).
 */
export interface WorkContext {
  personId: string;
  tenantId: string;

  organization: string | null;
  businessUnit: string | null;
  department: string | null;
  team: string | null;
  currentRole: string | null;
  roleLevel: string | null;
  jobFamily: string | null;
  workLocation: string | null;
  employmentType: EmploymentType | null;
  preferredLanguage: string | null;
  manager: string | null;

  /** Per-field provenance, keyed by the WorkContext property name. */
  fieldProvenance: Partial<Record<keyof WorkContext, WorkContextFieldProvenance>>;

  dataSource: WorkContextDataSource;
  lastSynchronizedDate: string | null;
  verificationStatus: VerificationStatus;
  confirmationStatus: WorkContextConfirmationStatus;
  pendingCorrectionStatus: PendingCorrectionStatus;
}

export type CorrectionReason =
  | 'organization'
  | 'department'
  | 'team'
  | 'role'
  | 'roleLevel'
  | 'manager'
  | 'workLocation'
  | 'other';

export interface CorrectionRequest {
  reason: CorrectionReason;
  details: string;
}

