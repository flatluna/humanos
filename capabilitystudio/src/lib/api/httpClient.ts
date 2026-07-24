/**
 * Minimal fetch wrapper for the real HumanOS backend (Azure Functions,
 * `func start` default route prefix `/api`). Request bodies can be
 * lowerCamelCase (backend deserializes case-insensitively). Response
 * bodies come back **camelCase** from the backend's `FunctionResponseFactory`
 * (see its own doc comment) — but every DTO in this app's api/ files is
 * typed PascalCase. Rather than rewrite every consumer, `handleResponse`
 * recursively converts incoming camelCase keys to PascalCase before
 * returning, so existing PascalCase-typed code keeps working unchanged.
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

export async function apiPut<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'PUT',
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

/** Uploads a raw file as the request body (no multipart), matching the
 * backend's "read Content-Type header + raw body stream" upload endpoints
 * (e.g. Program logo). */
export async function apiPostFile<T>(path: string, file: File): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': file.type || 'application/octet-stream' },
    body: file,
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
  return (text ? toPascalCaseDeep(JSON.parse(text)) : undefined) as T;
}

/** Recursively converts every camelCase object key to PascalCase (first
 * letter uppercased, rest untouched) so the backend's camelCase JSON
 * matches this app's PascalCase DTO types. Arrays/nested objects are
 * walked; primitives (including date strings) pass through untouched. */
function toPascalCaseDeep(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map(toPascalCaseDeep);
  }
  if (value !== null && typeof value === 'object') {
    const result: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(value as Record<string, unknown>)) {
      const pascalKey = key.length > 0 ? key[0].toUpperCase() + key.slice(1) : key;
      result[pascalKey] = toPascalCaseDeep(val);
    }
    return result;
  }
  return value;
}

export function apiImageUrl(path: string): string {
  return `${API_BASE_URL}${path}`;
}
