import { apiPost } from '@/features/capabilities/api/httpClient';

export interface TecContigoChatHistoryEntry {
  role: 'Student' | 'Agent';
  text: string;
}

interface TecContigoChatResponse {
  message: string;
}

/** One turn of "tecBot" — the Tec Contigo student companion agent (see
 * backend TecContigoAgent/PostTecContigoChatFunction). Grounded server-side
 * in the student's own Interests, profile, enrolled courses/grades and the
 * real course catalog; the caller only ever passes the running
 * conversation history. */
export async function postTecContigoChat(
  personId: string | undefined,
  message: string,
  history: TecContigoChatHistoryEntry[],
): Promise<string> {
  const response = await apiPost<TecContigoChatResponse>('/tec-contigo/chat', {
    personId,
    message,
    history: history.map((h) => ({ role: h.role, text: h.text })),
  });
  return response.message;
}
