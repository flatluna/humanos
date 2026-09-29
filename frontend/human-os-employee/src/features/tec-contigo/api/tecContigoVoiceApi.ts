import { apiPost } from '@/features/capabilities/api/httpClient';

/** Mirrors buddyApi.ts's PascalCase-normalization fix — the backend's
 * FunctionResponseFactory serializes camelCase, but this DTO mirrors the
 * C# PascalCase property names, so every response must be converted or
 * `session.RealtimeCallsUrl` silently ends up undefined. */
export interface TecContigoVoiceSessionResponse {
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

/** Mints tecBot's Realtime voice session — grounded server-side in the
 * EXACT same CONTEXT block as the text chat (see
 * TecContigoContextBuilder.cs/TecContigoVoiceSessionFunction.cs): the
 * student's own interests, official TEC grades, Human OS/Engrams
 * progress and assessment evidence. */
export async function requestTecContigoVoiceSession(
  personId: string | undefined,
  personName?: string,
  languageCode?: string,
): Promise<TecContigoVoiceSessionResponse> {
  const data = await apiPost<unknown>('/tec-contigo/voice-session', {
    personId,
    personName,
    languageCode,
  });
  return toPascalCaseDeep(data) as TecContigoVoiceSessionResponse;
}

/** Backs tecBot voice's Realtime "search_the_web" tool call — reuses the same
 * Perplexity-backed endpoint Buddy uses (the browser can't reach the API key directly). */
export async function searchTecContigoWeb(query: string): Promise<string> {
  const data = await apiPost<{ text?: string }>('/humanos/buddy/web-search', { query });
  return data.text ?? 'No relevant web results were found.';
}
