/** Response shapes mirroring the real backend contracts exactly
 *  (camelCase, per the Azure Functions Worker's default JSON
 *  serialization). Keep these in sync with:
 *  - backend/HumanOS/Contracts/People/PersonResponse.cs
 *  - backend/HumanOS/AzureFunctions/Api/GetTenantFunction.cs (anonymous response shape)
 *  - backend/HumanOS/AzureFunctions/Api/GetPersonProfileFunction.cs (anonymous response shape)
 *  - backend/HumanOS/AzureFunctions/Api/GetHumanProfileFunction.cs (anonymous response shape)
 */

export interface TenantResponse {
  tenantId: string;
  name: string;
  slug: string;
  domain: string | null;
  description: string | null;
  cultureCode: string;
  timeZone: string;
  isActive: boolean;
  createdDate: string;
  updatedDate: string;
}

export interface PersonResponse {
  personId: string;
  tenantId: string;
  email: string | null;
  isActive: boolean;
  lastLoginDate: string | null;
  createdDate: string;
  updatedDate: string;
}

export interface PersonProfileResponse {
  personProfileId: string;
  personId: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string | null;
  phoneNumber: string | null;
  preferredLanguage: string;
  countryCode: string | null;
  timeZone: string | null;
  profilePhotoUrl: string | null;
  dateOfBirth: string | null;
  occupation: string | null;
  company: string | null;
  biography: string | null;
  createdDate: string;
  updatedDate: string;
}

export interface HumanProfileResponse {
  humanProfileId: string;
  personId: string;
  missionStatement: string | null;
  primaryGoal: string | null;
  learningStyle: string | null;
  currentLifeStage: string | null;
  weeklyAvailabilityHours: number | null;
  motivationScore: number | null;
  confidenceScore: number | null;
  createdDate: string;
  updatedDate: string;
}

/** Mirrors backend/HumanOS/Contracts/RoleExperience/UploadRoleDocumentRequest.cs */
export interface UploadRoleDocumentRequest {
  documentType: 'job-description' | 'resume';
  fileName: string;
  contentType: string;
  contentBase64: string;
}

/** Mirrors backend/HumanOS/Contracts/RoleExperience/UploadRoleDocumentResponse.cs */
export interface UploadRoleDocumentResponse {
  personId: string;
  documentType: 'job-description' | 'resume';
  storagePath: string;
  uploadedDate: string;
}

/** Mirrors backend/HumanOS/Contracts/Goals/GoalResponse.cs */
export interface GoalResponse {
  goalId: string;
  code: string;
  name: string;
  description: string | null;
  category: string | null;
  isActive: boolean;
}

/** Mirrors backend/HumanOS/Contracts/Goals/PersonGoalResponse.cs */
export interface PersonGoalResponse {
  personGoalId: string;
  personId: string;
  goalId: string;
  goalCode: string;
  goalName: string;
  category: string | null;
  status: 'Active' | 'Completed' | 'Abandoned' | 'Paused' | string;
  progressPercentage: number;
  targetDate: string | null;
  startedDate: string;
  completedDate: string | null;
  createdDate: string;
  updatedDate: string;
}

/** Mirrors backend/HumanOS/Contracts/Motivations/MotivationResponse.cs */
export interface MotivationResponse {
  motivationId: string;
  code: string;
  name: string;
  isActive: boolean;
}

/** Mirrors backend/HumanOS/Contracts/Motivations/PersonMotivationResponse.cs */
export interface PersonMotivationResponse {
  personMotivationId: string;
  personId: string;
  motivationId: string;
  motivationCode: string;
  motivationName: string;
  createdDate: string;
}

/** Mirrors backend/HumanOS/Contracts/Capabilities/PersonCapabilityResponse.cs */
export interface PersonCapabilityResponse {
  personCapabilityId: string;
  personId: string;
  capabilityId: string;
  capabilityCode: string;
  capabilityName: string;
  currentLevel: number;
  targetLevel: number;
  progressPercentage: number;
  masteryScore: number;
  independenceLevel: number;
  retentionScore: number | null;
  confidenceScore: number | null;
  knowledgeScore: number;
  recallScore: number;
  applicationScore: number;
  status: 'NotStarted' | 'Active' | 'Paused' | 'Completed' | 'Abandoned' | string;
  startedDate: string | null;
  lastActivityDate: string | null;
  createdDate: string;
  updatedDate: string;
}

// ── Growth Plan — Step 3 goal recommendation (real agent) ─────────────────

export interface RecommendGrowthPathSubjectOption {
  code: string;
  name: string;
}

/** Mirrors backend/HumanOS/Contracts/GrowthPlan/RecommendGrowthPathContracts.cs's RecommendGrowthPathRequest. */
export interface RecommendGrowthPathRequest {
  goalPrompt: string;
  personName: string;
  allowedSubjects: RecommendGrowthPathSubjectOption[];
  statedGoals: string[];
  catalogContext?: string;
}

export interface RecommendGrowthPathStepResponse {
  name: string;
  level: string;
}

/** Mirrors backend/HumanOS/Contracts/GrowthPlan/RecommendGrowthPathContracts.cs's RecommendGrowthPathResponse. */
export interface RecommendGrowthPathResponse {
  hasRecommendation: boolean;
  recommendationType: string;
  programName: string | null;
  programDescription: string | null;
  subjectCode: string | null;
  steps: RecommendGrowthPathStepResponse[];
  rationale: string | null;
  /** Set when the agent matched a real, existing Program — pass this
   * through as the accepted recommendation's programId when saving. */
  matchedProgramId: string | null;
}

