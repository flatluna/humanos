/**
 * Minimal fetch wrapper for the real HumanOS backend (Azure Functions,
 * `func start` default route prefix `/api`). Response error contract is
 * `{ error, message }` (see backend/HumanOS/AzureFunctions/Api/
 * FunctionResponseFactory.cs) — thrown as a plain Error(message), matching
 * what the mock APIs already threw so existing UI error handling
 * (`err instanceof Error ? err.message : ...`) works unchanged.
 *
 * NOTE on casing: request bodies can be lowerCamelCase (backend
 * deserializes with PropertyNameCaseInsensitive = true), but RESPONSE
 * bodies come back PascalCase (no camelCase policy configured on the
 * backend's JSON serializer) — see the PascalCase fields in
 * src/lib/api/studioApi.ts's response types.
 */

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:7071/api';

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

export async function apiDelete<T = void>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json' },
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

  // 204/empty responses (none expected today, but safe to guard).
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}
