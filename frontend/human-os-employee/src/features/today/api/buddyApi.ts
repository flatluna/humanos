import { apiPost } from '@/features/capabilities/api/httpClient';

/** BuddyVoiceSessionFunction actually serializes camelCase (every response goes
 * through FunctionResponseFactory's JsonSerializerDefaults.Web policy — confirmed
 * via curl: `{"clientSecret":...}`, not `{"ClientSecret":...}`) despite this DTO
 * mirroring the backend's PascalCase C# property names. Without converting, a raw
 * `apiPost` leaves `session.RealtimeCallsUrl` undefined, which silently POSTs the
 * SDP offer to `http://localhost:5180/undefined` and fails with a 404 — surfaced
 * to the user as the generic "Could not connect to Buddy" error. Same bug class/fix
 * as runtimeSessionApi.ts's rGet/rPost `toPascalCaseDeep`. */
export interface BuddyVoiceSessionResponse {
  ClientSecret: string;
  RealtimeCallsUrl: string;
  Model: string;
  Voice: string;
  ExpiresAtUnixSeconds?: number | null;
}

function toPascalCaseDeep(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(toPascalCaseDeep);
  }
  if (value !== null && typeof value === 'object') {
    const result: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(value as Record<string, unknown>)) {
      const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
      result[pascalKey] = toPascalCaseDeep(val);
    }
    return result;
  }
  return value;
}

/** Mints a Buddy Realtime voice session — grounded server-side in the
 * caller's own profile/skills/CQI/objectives/achievements (see
 * BuddyContextBuilder.cs). The identity headers (X-Azure-OID/TID) already
 * sent by apiPost let the backend resolve the employee without needing
 * personId here. */
export async function requestBuddyVoiceSession(personName?: string, languageCode?: string): Promise<BuddyVoiceSessionResponse> {
  const data = await apiPost<unknown>('/humanos/buddy/voice-session', {
    personName,
    languageCode,
  });
  return toPascalCaseDeep(data) as BuddyVoiceSessionResponse;
}

/** Backs Buddy's Realtime "search_the_web" tool call (see BuddyVoiceModal's data-channel
 * handler) — the browser can't reach Bing/Azure OpenAI credentials directly, so the actual
 * search runs server-side via BuddyWebSearchFunction/WebGroundingService. */
export async function searchBuddyWeb(query: string): Promise<string> {
  const data = await apiPost<{ text?: string }>('/humanos/buddy/web-search', { query });
  return data.text ?? 'No relevant web results were found.';
}
