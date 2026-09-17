import { apiClient } from '@/api/index';

// ── Types (mirrors backend Contracts/OrgStructure + Contracts/People) ─────

export interface DepartmentDto {
  departmentId: string;
  tenantId: string;
  parentDepartmentId: string | null;
  name: string;
  code: string | null;
  description: string | null;
  category: string | null;
  contactEmail: string | null;
  location: string | null;
  costCenter: string | null;
  missionStatement: string | null;
  leaderPersonId: string | null;
  leaderDisplayName: string | null;
  isActive: boolean;
  jobRoleCount: number;
  personCount: number;
  createdDate: string;
  updatedDate: string;
}

export interface SaveDepartmentRequest {
  name: string;
  code?: string | null;
  description?: string | null;
  category?: string | null;
  contactEmail?: string | null;
  location?: string | null;
  costCenter?: string | null;
  missionStatement?: string | null;
  leaderPersonId?: string | null;
  parentDepartmentId?: string | null;
}

export interface JobRoleDto {
  jobRoleId: string;
  tenantId: string;
  departmentId: string | null;
  departmentName: string | null;
  title: string;
  levelLabel: string | null;
  purpose: string | null;
  summary: string | null;
  responsibilities: string[];
  requiredExperience: string | null;
  requiredEducation: string | null;
  requiredCertifications: string[];
  requiredLanguages: string[];
  technicalSkills: string[];
  softSkills: string[];
  departmentProcesses: string[];
  hrProcesses: string[];
  tools: string[];
  dailyActivities: string[];
  performanceIndicators: string[];
  expectedOutcomes: string[];
  reportsToJobRoleId: string | null;
  reportsToJobRoleTitle: string | null;
  hasDirectReports: boolean;
  workSchedule: string | null;
  workModality: string | null;
  requiresTravel: boolean;
  workLocation: string | null;
  availabilityNotes: string | null;
  employmentType: string | null;
  salaryRangeMin: number | null;
  salaryRangeMax: number | null;
  salaryCurrency: string | null;
  benefits: string[];
  isActive: boolean;
  personCount: number;
  createdDate: string;
  updatedDate: string;
}

export interface SaveJobRoleRequest {
  departmentId?: string | null;
  title: string;
  levelLabel?: string | null;
  purpose?: string | null;
  summary?: string | null;
  responsibilities?: string[];
  requiredExperience?: string | null;
  requiredEducation?: string | null;
  requiredCertifications?: string[];
  requiredLanguages?: string[];
  technicalSkills?: string[];
  softSkills?: string[];
  departmentProcesses?: string[];
  hrProcesses?: string[];
  tools?: string[];
  dailyActivities?: string[];
  performanceIndicators?: string[];
  expectedOutcomes?: string[];
  reportsToJobRoleId?: string | null;
  workSchedule?: string | null;
  workModality?: string | null;
  requiresTravel?: boolean;
  workLocation?: string | null;
  availabilityNotes?: string | null;
  employmentType?: string | null;
  salaryRangeMin?: number | null;
  salaryRangeMax?: number | null;
  salaryCurrency?: string | null;
  benefits?: string[];
}

export interface GenerateJobRoleDescriptionRequest {
  jobRoleId?: string | null;
  departmentId?: string | null;
  title: string;
  levelLabel?: string | null;
  purpose?: string | null;
  summary?: string | null;
  responsibilities?: string[];
  requiredExperience?: string | null;
  requiredEducation?: string | null;
  requiredCertifications?: string[];
  requiredLanguages?: string[];
  technicalSkills?: string[];
  softSkills?: string[];
  tools?: string[];
  dailyActivities?: string[];
  performanceIndicators?: string[];
  expectedOutcomes?: string[];
  workSchedule?: string | null;
  workModality?: string | null;
  requiresTravel?: boolean;
  workLocation?: string | null;
  availabilityNotes?: string | null;
  employmentType?: string | null;
  salaryRangeMin?: number | null;
  salaryRangeMax?: number | null;
  salaryCurrency?: string | null;
  benefits?: string[];
}

export interface GenerateJobRoleDescriptionResponse {
  levelLabel: string | null;
  purpose: string | null;
  summary: string | null;
  responsibilities: string[];
  requiredExperience: string | null;
  requiredEducation: string | null;
  requiredCertifications: string[];
  requiredLanguages: string[];
  technicalSkills: string[];
  softSkills: string[];
  tools: string[];
  dailyActivities: string[];
  performanceIndicators: string[];
  expectedOutcomes: string[];
  workSchedule: string | null;
  workModality: string | null;
  requiresTravel: boolean;
  workLocation: string | null;
  availabilityNotes: string | null;
  employmentType: string | null;
  salaryRangeMin: number | null;
  salaryRangeMax: number | null;
  salaryCurrency: string | null;
  benefits: string[];
  modelName: string;
  inputTokens: number;
  outputTokens: number;
}

export interface TenantPersonDto {
  personId: string;
  email: string | null;
  displayName: string | null;
  phoneNumber?: string | null;
  address?: string | null;
  linkedInUrl?: string | null;
  departmentId: string | null;
  departmentName: string | null;
  jobRoleId: string | null;
  jobRoleTitle: string | null;
  managerId: string | null;
  managerDisplayName: string | null;
  orgRole: string;
  isActive: boolean;
}

export interface PersonOrgAssignmentRequest {
  departmentId?: string | null;
  jobRoleId?: string | null;
  managerId?: string | null;
  orgRole?: string | null;
  email?: string | null;
  displayName?: string | null;
  phoneNumber?: string | null;
  address?: string | null;
  linkedInUrl?: string | null;
}

export interface AddressSuggestion {
  address: string;
  streetName?: string | null;
  streetNumber?: string | null;
  city?: string | null;
  region?: string | null;
  countryCode?: string | null;
  postalCode?: string | null;
  entityId?: string | null;
  latitude?: number | null;
  longitude?: number | null;
}

export async function searchAddresses(query: string): Promise<AddressSuggestion[]> {
  if (!query || query.trim().length < 3) return [];
  const { data } = await apiClient.get<AddressSuggestion[] | { value: AddressSuggestion[] }>(`/addresses/search`, {
    params: { query: query.trim() },
  });
  if (Array.isArray(data)) return data;
  if (data && Array.isArray((data as { value: AddressSuggestion[] }).value)) {
    return (data as { value: AddressSuggestion[] }).value;
  }
  return [];
}

export interface TenantInvitationDto {
  tenantInvitationId: string;
  tenantId: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  departmentId: string | null;
  jobRoleId: string | null;
  managerId: string | null;
  orgRole: string;
  status: string;
  createdDate: string;
  updatedDate: string;
  acceptedDate: string | null;
  revokedDate: string | null;
  /** One-time temporary password for the newly created local account, only present in the response
   * of the create call that created it — never returned again afterward (see GetTenantInvitations). */
  temporaryPassword?: string | null;
}

export interface CreateTenantInvitationRequest {
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  departmentId?: string | null;
  jobRoleId?: string | null;
  managerId?: string | null;
  orgRole?: string | null;
}

export interface PersonResumeDto {
  personId: string;
  address: string | null;
  linkedInUrl: string | null;
  professionalObjective: string | null;
  professionalSummary: string | null;
  skills: string[];
  workExperience: string[];
  education: string[];
  certifications: string[];
  languages: string[];
  rawResumeText: string | null;
  studentAcademicProfile: StudentAcademicProfile | null;
  updatedDate: string | null;
}

export interface AcademicSubject { name: string | null; grade: number | null; }
export interface AcademicSemester { name: string | null; average: number | null; subjects: AcademicSubject[]; }
export interface StudentAcademicProfile {
  institution: string | null;
  degree: string | null;
  graduationYear: number | null;
  academicStage: string | null;
  cumulativeAverage: number | null;
  averageScale: string | null;
  distinctions: string[];
  semesters: AcademicSemester[];
}

export interface SavePersonResumeRequest {
  address?: string | null;
  linkedInUrl?: string | null;
  professionalObjective?: string | null;
  professionalSummary?: string | null;
  skills?: string[];
  workExperience?: string[];
  education?: string[];
  certifications?: string[];
  languages?: string[];
  rawResumeText?: string | null;
  studentAcademicProfile?: StudentAcademicProfile | null;
}

export interface ExtractPersonResumeResponse {
  address: string | null;
  linkedInUrl: string | null;
  professionalObjective: string | null;
  professionalSummary: string | null;
  skills: string[];
  workExperience: string[];
  education: string[];
  certifications: string[];
  languages: string[];
  studentAcademicProfile: StudentAcademicProfile | null;
  modelName: string;
  inputTokens: number;
  outputTokens: number;
}

// ── Tenant ──────────────────────────────────────────────────────────────

export interface TenantDto {
  tenantId: string;
  name: string;
  slug: string;
  domain: string | null;
  description: string | null;
  cultureCode: string;
  timeZone: string;
  isActive: boolean;
}

export async function getTenant(tenantId: string): Promise<TenantDto> {
  const { data } = await apiClient.get<TenantDto>(`/tenants/${tenantId}`);
  return data;
}

export async function updateTenant(tenantId: string, body: Partial<TenantDto>): Promise<TenantDto> {
  const { data } = await apiClient.put<TenantDto>(`/tenants/${tenantId}`, body);
  return data;
}

// ── Departments ─────────────────────────────────────────────────────────

export async function getDepartments(tenantId: string): Promise<DepartmentDto[]> {
  const { data } = await apiClient.get<DepartmentDto[]>(`/tenants/${tenantId}/departments`);
  return data;
}

export async function getDepartment(departmentId: string): Promise<DepartmentDto> {
  const { data } = await apiClient.get<DepartmentDto>(`/departments/${departmentId}`);
  return data;
}

export async function createDepartment(tenantId: string, body: SaveDepartmentRequest): Promise<DepartmentDto> {
  const { data } = await apiClient.post<DepartmentDto>(`/tenants/${tenantId}/departments`, body);
  return data;
}

export async function updateDepartment(departmentId: string, body: SaveDepartmentRequest): Promise<DepartmentDto> {
  const { data } = await apiClient.put<DepartmentDto>(`/departments/${departmentId}`, body);
  return data;
}

export async function deleteDepartment(departmentId: string): Promise<void> {
  await apiClient.delete(`/departments/${departmentId}`);
}

// ── Job Roles ───────────────────────────────────────────────────────────

export async function getJobRoles(tenantId: string, departmentId?: string): Promise<JobRoleDto[]> {
  const { data } = await apiClient.get<JobRoleDto[]>(`/tenants/${tenantId}/job-roles`, {
    params: departmentId ? { departmentId } : undefined,
  });
  return data;
}

export async function getJobRole(jobRoleId: string): Promise<JobRoleDto> {
  const { data } = await apiClient.get<JobRoleDto>(`/job-roles/${jobRoleId}`);
  return data;
}

export async function createJobRole(tenantId: string, body: SaveJobRoleRequest): Promise<JobRoleDto> {
  const { data } = await apiClient.post<JobRoleDto>(`/tenants/${tenantId}/job-roles`, body);
  return data;
}

export async function updateJobRole(jobRoleId: string, body: SaveJobRoleRequest): Promise<JobRoleDto> {
  const { data } = await apiClient.put<JobRoleDto>(`/job-roles/${jobRoleId}`, body);
  return data;
}

export async function generateJobRoleDescription(
  tenantId: string,
  body: GenerateJobRoleDescriptionRequest,
): Promise<GenerateJobRoleDescriptionResponse> {
  const { data } = await apiClient.post<GenerateJobRoleDescriptionResponse>(
    `/tenants/${tenantId}/job-roles/generate-description`,
    body,
  );
  return data;
}

export async function deleteJobRole(jobRoleId: string): Promise<void> {
  await apiClient.delete(`/job-roles/${jobRoleId}`);
}

export async function attachDepartmentJobRole(departmentId: string, jobRoleId: string): Promise<void> {
  await apiClient.post(`/departments/${departmentId}/job-roles/${jobRoleId}`);
}

export async function detachDepartmentJobRole(departmentId: string, jobRoleId: string): Promise<void> {
  await apiClient.delete(`/departments/${departmentId}/job-roles/${jobRoleId}`);
}

// ── People directory ───────────────────────────────────────────────────

export async function getTenantPeople(tenantId: string): Promise<TenantPersonDto[]> {
  const { data } = await apiClient.get<TenantPersonDto[]>(`/tenants/${tenantId}/people`);
  return data;
}

export async function updatePersonOrgAssignment(
  personId: string,
  body: PersonOrgAssignmentRequest,
): Promise<TenantPersonDto> {
  const { data } = await apiClient.put<TenantPersonDto>(`/people/${personId}/org-assignment`, body);
  return data;
}

export async function deactivatePerson(personId: string): Promise<void> {
  await apiClient.delete(`/people/${personId}`);
}

export async function getTenantInvitations(tenantId: string): Promise<TenantInvitationDto[]> {
  const { data } = await apiClient.get<TenantInvitationDto[]>(`/tenants/${tenantId}/invitations`);
  return data;
}

export async function createTenantInvitation(
  tenantId: string,
  body: CreateTenantInvitationRequest,
): Promise<TenantInvitationDto> {
  const { data } = await apiClient.post<TenantInvitationDto>(`/tenants/${tenantId}/invitations`, body);
  return data;
}

export async function deleteTenantInvitation(tenantId: string, tenantInvitationId: string): Promise<void> {
  await apiClient.delete(`/tenants/${tenantId}/invitations/${tenantInvitationId}`);
}

// ── Employee résumé/CV dashboard (Personal tab detail page) ────────────

export async function getPersonResume(personId: string): Promise<PersonResumeDto> {
  const { data } = await apiClient.get<PersonResumeDto>(`/people/${personId}/resume`);
  return data;
}

export async function savePersonResume(personId: string, body: SavePersonResumeRequest): Promise<PersonResumeDto> {
  const { data } = await apiClient.put<PersonResumeDto>(`/people/${personId}/resume`, body);
  return data;
}

export async function extractPersonResume(personId: string, resumeText: string): Promise<ExtractPersonResumeResponse> {
  const { data } = await apiClient.post<ExtractPersonResumeResponse>(`/people/${personId}/resume/extract`, {
    resumeText,
  });
  return data;
}

// ── AI career development plan ("Rutas de Carrera" tab) ────────────────

export interface CareerPlanRoadmapStepDto {
  order: number;
  skill: string;
  title: string;
  description: string;
  matchedProgramId: string | null;
  matchedProgramName: string | null;
  /** Set automatically by the backend when a standalone Capability (no
   * wrapping Program) whose Name matches this step's Title already
   * exists in the catalog — no manual linking required, see
   * PersonCareerPlanService.AutoMatchCapabilitiesAsync. */
  matchedCapabilityId: string | null;
  matchedCapabilityName: string | null;
  /** "Completed" | "InProgress" | "Pending" — this employee's real progress. */
  trainingStatus: 'Completed' | 'InProgress' | 'Pending';
}

export interface PersonCareerPlanDto {
  personId: string;
  hasPlan: boolean;
  jobRoleId: string | null;
  jobRoleTitle: string | null;
  gapSkills: string[];
  technicalRoadmap: CareerPlanRoadmapStepDto[];
  softSkillsRoadmap: CareerPlanRoadmapStepDto[];
  departmentProcessesRoadmap: CareerPlanRoadmapStepDto[];
  hrProcessesRoadmap: CareerPlanRoadmapStepDto[];
  overallRationale: string | null;
  generatedDate: string | null;
  isAccepted: boolean;
  acceptedDate: string | null;
}

export async function getPersonCareerPlan(personId: string): Promise<PersonCareerPlanDto> {
  const { data } = await apiClient.get<PersonCareerPlanDto>(`/people/${personId}/career-plan`);
  return data;
}

export async function generatePersonCareerPlan(personId: string): Promise<PersonCareerPlanDto> {
  const { data } = await apiClient.post<PersonCareerPlanDto>(`/people/${personId}/career-plan/generate`);
  return data;
}

export async function acceptPersonCareerPlan(personId: string): Promise<PersonCareerPlanDto> {
  const { data } = await apiClient.post<PersonCareerPlanDto>(`/people/${personId}/career-plan/accept`);
  return data;
}

// ── Org chart import (AI extraction) ───────────────────────────────────

export interface OrgChartDepartmentNode {
  name: string;
  parentDepartmentName: string | null;
}

export interface OrgChartPersonNode {
  name: string;
  position: string;
  departmentName: string | null;
  reportsTo: string | null;
  level: number;
}

export interface OrgChartExtractionResult {
  departments: OrgChartDepartmentNode[];
  people: OrgChartPersonNode[];
}

export interface ApplyOrgChartJobRole {
  title: string;
  levelLabel?: string | null;
  detectedPersonName?: string | null;
}

export interface ApplyOrgChartDepartment {
  name: string;
  parentName?: string | null;
  jobRoles: ApplyOrgChartJobRole[];
}

export interface ApplyOrgChartRequest {
  departments: ApplyOrgChartDepartment[];
  /** When true, also creates a placeholder Person (test/demo account,
   *  cannot really sign in — see backend ApplyOrgChartRequest docs) for
   *  every job role with a detected person name. */
  createTestPersonRecords?: boolean;
}

export interface ApplyOrgChartResponse {
  departmentsCreated: number;
  departmentsReused: number;
  jobRolesCreated: number;
  peopleCreated: number;
}

/** Uploads an org chart image (raw file bytes, Content-Type set from the
 *  file) and returns the AI-proposed department/people hierarchy for the
 *  HRAdmin to review before creating anything for real. */
export async function extractOrgChart(tenantId: string, file: File): Promise<OrgChartExtractionResult> {
  const { data } = await apiClient.post<OrgChartExtractionResult>(
    `/tenants/${tenantId}/org-chart/extract`,
    file,
    { headers: { 'Content-Type': file.type || 'application/octet-stream' } },
  );
  return data;
}

/** Creates the confirmed departments/job-roles from a reviewed org chart
 *  import proposal. */
export async function applyOrgChart(tenantId: string, body: ApplyOrgChartRequest): Promise<ApplyOrgChartResponse> {
  const { data } = await apiClient.post<ApplyOrgChartResponse>(`/tenants/${tenantId}/org-chart/apply`, body);
  return data;
}
