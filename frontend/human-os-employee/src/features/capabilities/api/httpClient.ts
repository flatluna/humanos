/**
 * Minimal fetch wrapper for the real HumanOS backend (Azure Functions,
 * `func start` default route prefix `/api`). Mirrors humanstudio's
 * httpClient.ts pattern: request bodies can be lowerCamelCase, response
 * bodies come back PascalCase (no camelCase policy on the backend's JSON
 * serializer) — see PascalCase response types across this app's api/ files.
 */

import { getIdentityHeaders } from '@/auth';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

export { API_BASE_URL };

export function apiImageUrl(path: string): string {
  return `${API_BASE_URL}${path}`;
}

interface ApiErrorBody {
  error?: string;
  message?: string;
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'GET',
    headers: { Accept: 'application/json', ...getIdentityHeaders() },
  });
  return handleResponse<T>(response);
}

export async function apiPost<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...getIdentityHeaders() },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return handleResponse<T>(response);
}

export async function apiPut<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...getIdentityHeaders() },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return handleResponse<T>(response);
}

export async function apiDelete<T>(path: string): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json', ...getIdentityHeaders() },
  });
  return handleResponse<T>(response);
}

/** Uploads a raw file as the request body (no multipart), matching the
 * backend's "read Content-Type header + raw body stream" upload endpoints
 * (e.g. profile photo). */
export async function apiPostFile<T>(path: string, file: File): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': file.type || 'application/octet-stream', ...getIdentityHeaders() },
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

  if (response.status === 204) {
    return undefined as unknown as T;
  }

  const text = await response.text();
  if (!text || text.trim() === '') {
    return undefined as unknown as T;
  }
  return JSON.parse(text) as T;
}
