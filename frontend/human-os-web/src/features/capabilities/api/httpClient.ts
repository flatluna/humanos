/**
 * Minimal fetch wrapper for the real HumanOS backend (Azure Functions,
 * `func start` default route prefix `/api`). Mirrors humanstudio's
 * httpClient.ts pattern: request bodies can be lowerCamelCase, response
 * bodies come back PascalCase (no camelCase policy on the backend's JSON
 * serializer) — see PascalCase response types across this app's api/ files.
 */

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export { API_BASE_URL };

interface ApiErrorBody {
  error?: string;
  message?: string;
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'GET',
    headers: { Accept: 'application/json' },
  });
  return handleResponse<T>(response);
}

export async function apiPost<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return handleResponse<T>(response);
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const body = (await response.json()) as ApiErrorBody;
      message = body.message ?? message;
    } catch {
      // Response body wasn't JSON; keep the generic status message.
    }
    throw new Error(message);
  }

  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
