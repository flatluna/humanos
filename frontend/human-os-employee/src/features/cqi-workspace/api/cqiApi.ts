import { API_BASE_URL } from '@/features/capabilities/api/httpClient';
import { apiGet } from '@/features/capabilities/api/httpClient';
import { getIdentityHeaders } from '@/auth';

/** CQI Workspace (HermanScience) — client for the employee's own CQI
 * assessment profile, extracted from an uploaded PDF report. Mirrors
 * resumeApi.ts's camelCase convention (goes through FunctionResponseFactory). */
export interface CqiProfileData {
  leadingPatternType: string;
  leadingPatternName: string;
  profileSnapshot: string;
  howTheyTendToWork: string;
  thinkingAndDecisions: string;
  communicationAndCollaboration: string;
  learningAndWorkingPreferences: string;
  naturalStrengths: string[];
  patternsWorthNoticing: string[];
  workingEffectivelyWithAi: string;
  whatHelpsThemThrive: string;
  whatStrainCanLookLike: string;
  growthDirection: string;
  wingInfluence: string;
  anchorTypeName: string;
  anchorDescription: string;
  whatMayDrawAttention: string;
  howChoicesMayTakeShape: string;
  workAndCommunication: string;
  whatMaySupportBalance: string;
  potentialStrengths: string[];
  patternsToWatchUnderPressure: string[];
}

export interface CqiProfileResponse {
  personId: string;
  profile: CqiProfileData | null;
  sourceFileName: string | null;
  extractionModel: string | null;
  extractedDate: string | null;
  updatedDate: string | null;
}

export function getMyCqiProfile(personId: string): Promise<CqiProfileResponse> {
  return apiGet<CqiProfileResponse>(`/people/${personId}/cqi-profile`);
}

/** Uploads a CQI report PDF for extraction — raw bytes as the request
 * body, same convention as uploadProfilePhoto. */
export async function uploadCqiReport(personId: string, file: File): Promise<CqiProfileResponse> {
  const response = await fetch(`${API_BASE_URL}/people/${personId}/cqi-profile`, {
    method: 'POST',
    headers: {
      'Content-Type': file.type || 'application/pdf',
      'X-File-Name': encodeURIComponent(file.name),
      ...getIdentityHeaders(),
    },
    body: file,
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.message ?? body?.error ?? `Request failed with status ${response.status}`);
  }

  return (await response.json()) as CqiProfileResponse;
}
