import type { JobDescription } from '../types';

// ---------------------------------------------------------------------
// Job Description Source (Step 2 — Your Role and Experience)
// ---------------------------------------------------------------------
//
// The Job Description is an INPUT, not an agent-generated conclusion.
// This step only collects and confirms that input — it must never
// generate capabilities, knowledge, methods, procedures, policies, or a
// Development Plan. Official content, employee-provided context, and
// employee comments are always kept as separate fields so employee
// input can never be mistaken for (or overwrite) organizational fact.

/** The three-state trust badge shown on this screen. Distinct from
 *  `JobDescription.source` (`JobDescriptionOrigin`), which only
 *  distinguishes who authored the underlying *content* — this also
 *  covers "a review was requested but hasn't happened yet". */
export type JobDescriptionSourceType = 'organizationProvided' | 'employeeProvided' | 'pendingOrganizationReview';

/** The employee's disposition toward the description once they've
 *  actually looked at it. Distinct from `JobDescriptionSourceType`
 *  (who authored the content) and from confirmation (whether the
 *  source may be used in a future agent analysis). */
export type JobDescriptionReviewStatus = 'notReviewed' | 'reflectsRole' | 'workIsDifferent';

/** Free-text role information the employee supplies when there is no
 *  official description (or to supplement one). Plain `string`s, not
 *  `LocalizedText` — this is whatever the employee typed in whichever
 *  language they used, not bilingual authored content. */
export interface EmployeeRoleContext {
  rolePurpose: string;
  mainResponsibilities: string[];
  expectedResults: string[];
  missingContext: string | null;
  /** Set when the employee's context came from an uploaded file rather
   *  than typed text. The file's *content* is not parsed/extracted yet
   *  (see `services/jobDescriptionSourceService.ts`) — only its name
   *  and storage path are recorded. */
  uploadedFileName: string | null;
  uploadedFileStoragePath: string | null;
}

export type JobDescriptionEmployeeCommentType = 'missingContext' | 'organizationReviewRequest' | 'generalComment';

/** An employee note stored separately from the official Job
 *  Description — it never modifies or overwrites official content. */
export interface JobDescriptionEmployeeComment {
  id: string;
  type: JobDescriptionEmployeeCommentType;
  text: string;
  createdDate: string;
}

/** The aggregate this screen reads/writes. Keeps official content,
 *  employee-provided context, employee comments, review status, and
 *  confirmation state all separate from one another. */
export interface JobDescriptionSource {
  /** `null` when no official job description exists yet. */
  officialDescription: JobDescription | null;
  /** `null` until the employee provides their own role context. */
  employeeContext: EmployeeRoleContext | null;
  comments: JobDescriptionEmployeeComment[];
  sourceType: JobDescriptionSourceType;
  reviewStatus: JobDescriptionReviewStatus;
  /** Whether the employee has confirmed this source may be used later
   *  in agent analysis. Confirmation is NOT validation of correctness,
   *  NOT conversion into role requirements, and NOT capability
   *  validation. */
  isConfirmed: boolean;
  confirmedDate: string | null;
}
