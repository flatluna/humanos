import { apiGet, apiPost, apiPut, apiDelete, apiPostFile } from './httpClient';

/** Real backend response shape (PascalCase, converted by httpClient) — see
 * ProgramResponse.cs. */
export interface BackendProgram {
  ProgramId: string;
  TenantId?: string | null;
  Code: string;
  Name: string;
  Description: string | null;
  Objectives: string | null;
  Requirements: string | null;
  HasLogo: boolean;
  IsActive: boolean;
  CapabilityCount: number;
  CreatedDate: string;
  UpdatedDate: string;
}

/** See ProgramCapabilityResponse.cs. */
export interface BackendProgramCapability {
  ProgramCapabilityId: string;
  CapabilityId: string;
  CapabilityCode: string;
  CapabilityName: string;
  SubjectCode: string | null;
  SortOrder: number;
  IsRequired: boolean;
  PhaseLabel: string | null;
  Objectives: string | null;
  Requirements: string | null;
  LevelCount: number;
  NodeCount: number;
  HasCoverImage: boolean;
  CourseType?: string | null;
}

/** See ProgramDetailResponse.cs. */
export interface BackendProgramDetail extends BackendProgram {
  Capabilities: BackendProgramCapability[];
}

export interface SaveProgramInput {
  name: string;
  tenantId?: string | null;
  description?: string | null;
  objectives?: string | null;
  requirements?: string | null;
}

export interface ProgramCapabilityEntry {
  capabilityId: string;
  sortOrder: number;
  isRequired: boolean;
  phaseLabel?: string | null;
}

/** Real "Program catalog" list, 100% DB-backed (GET /programs). */
export async function getPrograms(): Promise<BackendProgram[]> {
  const programs = await apiGet<BackendProgram[]>('/programs');
  return programs.sort((a, b) => new Date(b.UpdatedDate).getTime() - new Date(a.UpdatedDate).getTime());
}

export async function getProgramById(programId: string): Promise<BackendProgramDetail> {
  return apiGet<BackendProgramDetail>(`/programs/${programId}`);
}

export async function createProgram(input: SaveProgramInput): Promise<BackendProgram> {
  return apiPost<BackendProgram>('/programs', input);
}

export async function updateProgram(programId: string, input: SaveProgramInput): Promise<BackendProgram> {
  return apiPut<BackendProgram>(`/programs/${programId}`, input);
}

export async function updateProgramCapabilities(
  programId: string,
  capabilities: ProgramCapabilityEntry[],
): Promise<BackendProgramDetail> {
  return apiPut<BackendProgramDetail>(`/programs/${programId}/capabilities`, { capabilities });
}

export async function uploadProgramLogo(programId: string, file: File): Promise<void> {
  await apiPostFile(`/programs/${programId}/logo`, file);
}

/** Generates a candidate logo image (gpt-image-1.5) from a Program's name
 * + description — pure preview, does NOT touch the database or Data Lake.
 * The caller converts the returned base64 into a File and uploads it via
 * `uploadProgramLogo` once the user accepts it (typically right after
 * `createProgram`). */
export async function generateProgramLogoPreview(
  name: string,
  description?: string | null,
): Promise<{ ImageBase64: string; ContentType: string }> {
  return apiPost<{ ImageBase64: string; ContentType: string }>('/programs/logo/generate', {
    name,
    description: description || null,
  });
}


/**
 * Permanently deletes a program (its ProgramCapability rows cascade-delete
 * with it; the underlying Capabilities themselves are untouched).
 */
export async function deleteProgram(programId: string): Promise<void> {
  await apiDelete(`/programs/${programId}`);
}

/** See CapabilityProgramMembershipResponse.cs. Powers the "Programas"
 * section on a Capability's own detail page — a Capability can belong to
 * zero, one, or several Programs. */
export interface CapabilityProgramMembership {
  ProgramId: string;
  ProgramCode: string;
  ProgramName: string;
  SortOrder: number;
  IsRequired: boolean;
  PhaseLabel: string | null;
  Objectives: string | null;
  Requirements: string | null;
}

export interface ProgramEntityOccurrence {
  CourseId: string;
  CourseName: string;
  EngramId: string;
  EngramTitle: string;
  ChapterTitle?: string | null;
  SortOrder: number;
  SourceText: string;
  StartPosition?: number | null;
  EndPosition?: number | null;
}

export interface ProgramEntityItem {
  EntityId: string;
  CanonicalName: string;
  EntityType: string;
  Description?: string | null;
  OntologicalClass?: string | null;
  Aliases: string[];
  TotalOccurrences: number;
  DistinctCoursesCount: number;
  Occurrences: ProgramEntityOccurrence[];
}

export interface ProgramCourseSummary {
  CourseId: string;
  CourseName: string;
  EngramCount: number;
  EntityCount: number;
}

export interface ProgramEntitiesResponse {
  ProgramId: string;
  ProgramName: string;
  ProgramCode?: string | null;
  Description?: string | null;
  TotalCourses: number;
  TotalEngrams: number;
  TotalEntities: number;
  TotalOccurrences: number;
  Courses: ProgramCourseSummary[];
  Entities: ProgramEntityItem[];
}

export async function getProgramEntities(programId: string): Promise<ProgramEntitiesResponse> {
  return apiGet<ProgramEntitiesResponse>(`/programs/${programId}/entities`);
}

/** Every Program a given Capability currently belongs to. */
export async function getProgramsForCapability(capabilityId: string): Promise<CapabilityProgramMembership[]> {
  return apiGet<CapabilityProgramMembership[]>(`/capabilities/${capabilityId}/programs`);
}

/** Attaches an existing Capability to the END of a Program's sequence
 * (auto-assigned SortOrder) — the "connect a capability to a program"
 * direction of the flow: Programs are created top-down first (empty),
 * Capabilities are attached to them afterward. */
export async function addCapabilityToProgram(capabilityId: string, programId: string): Promise<void> {
  await apiPost(`/capabilities/${capabilityId}/programs/${programId}`);
}

/** Unlinks a Capability from a Program (the Capability itself is untouched). */
export async function removeCapabilityFromProgram(capabilityId: string, programId: string): Promise<void> {
  await apiDelete(`/capabilities/${capabilityId}/programs/${programId}`);
}
