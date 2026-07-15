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
