import { apiPost, apiPostFile, apiPut, API_BASE_URL } from '@/features/capabilities/api/httpClient';

/**
 * "Mi Perfil" — client for the employee's own self-service profile
 * (GET/POST/PUT /people/{personId}/profile — PersonProfile, separate
 * from the HR-managed org assignment fields like Department/JobRole/
 * OrgRole, which stay HRAdmin-only in human-os-enterprise).
 *
 * Casing gotcha: unlike this app's other API clients (capabilitiesApi,
 * careerPlanApi, etc.), which go through `FunctionResponseFactory` and
 * come back camelCase, the 3 PersonProfile Azure Functions
 * (Get/Create/UpdatePersonProfileFunction) write their responses with a
 * bare `response.WriteAsJsonAsync(new { ... })` — no camelCase naming
 * policy applied — so response bodies here are PascalCase. The request
 * body IS deserialized with `PropertyNameCaseInsensitive = true` on the
 * backend, so outgoing request fields can stay camelCase.
 */
export interface PersonProfileDto {
  PersonProfileId: string;
  PersonId: string;
  FirstName: string | null;
  LastName: string | null;
  DisplayName: string | null;
  PhoneNumber: string | null;
  PreferredLanguage: string;
  CountryCode: string | null;
  TimeZone: string | null;
  ProfilePhotoUrl: string | null;
  /** DateOnly serialized as "yyyy-MM-dd". */
  DateOfBirth: string | null;
  Occupation: string | null;
  Company: string | null;
  Biography: string | null;
  CreatedDate: string;
  UpdatedDate: string;
}

export interface UpsertPersonProfileRequest {
  firstName?: string | null;
  lastName?: string | null;
  displayName?: string | null;
  phoneNumber?: string | null;
  preferredLanguage: string;
  countryCode?: string | null;
  timeZone?: string | null;
  profilePhotoUrl?: string | null;
  dateOfBirth?: string | null;
  occupation?: string | null;
  company?: string | null;
  biography?: string | null;
}

export interface PersonDto {
  personId: string;
  tenantId: string;
  email: string;
  isActive: boolean;
  lastLoginDate: string | null;
  createdDate: string;
  updatedDate: string;
  orgRole: string;
  departmentId: string | null;
  jobRoleId: string | null;
  managerId: string | null;
}

export async function getPerson(personId: string): Promise<PersonDto | null> {
  const response = await fetch(`${API_BASE_URL}/people/${personId}`, {
    headers: { Accept: 'application/json' },
  });

  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Request failed with status ${response.status}`);

  return (await response.json()) as PersonDto;
}

/** Returns `null` (not a thrown error) when the employee has no profile
 *  row yet — every account created before this feature, or that simply
 *  never filled it in, hits this on first visit to "Mi Perfil". */
export async function getMyProfile(personId: string): Promise<PersonProfileDto | null> {
  const response = await fetch(`${API_BASE_URL}/people/${personId}/profile`, {
    headers: { Accept: 'application/json' },
  });

  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`Request failed with status ${response.status}`);

  return (await response.json()) as PersonProfileDto;
}

export function createMyProfile(personId: string, body: UpsertPersonProfileRequest): Promise<PersonProfileDto> {
  return apiPost<PersonProfileDto>(`/people/${personId}/profile`, body);
}

export function updateMyProfile(personId: string, body: UpsertPersonProfileRequest): Promise<PersonProfileDto> {
  return apiPut<PersonProfileDto>(`/people/${personId}/profile`, body);
}

/** Uploads a new profile photo (overwrites any existing one — the backend
 *  always writes it to a single `{personId}/photo.{ext}` blob path). */
export function uploadProfilePhoto(personId: string, file: File): Promise<{ personId: string; photoUrl: string }> {
  return apiPostFile(`/people/${personId}/profile/photo`, file);
}

/** Direct <img src> URL for the employee's own profile photo. `version` is
 *  an arbitrary cache-busting value (e.g. `UpdatedDate`) — the endpoint is
 *  served with `Cache-Control: no-cache`, but browsers still dedupe
 *  identical URLs within a single page session, so a fresh upload needs a
 *  changed query string to actually show up immediately. */
export function getProfilePhotoUrl(personId: string, version?: string | number): string {
  const query = version !== undefined ? `?v=${encodeURIComponent(String(version))}` : '';
  return `${API_BASE_URL}/people/${personId}/profile/photo${query}`;
}
