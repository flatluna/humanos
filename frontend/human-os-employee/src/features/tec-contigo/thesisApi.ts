import { apiClient } from '@/api';

export interface ThesisUploadResponse {
  personId: string;
  documentType: 'thesis';
  storagePath: string;
  uploadedDate: string;
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result !== 'string') {
        reject(new Error('No se pudo leer el archivo.'));
        return;
      }
      resolve(reader.result.split(',')[1] ?? '');
    };
    reader.onerror = () => reject(reader.error ?? new Error('No se pudo leer el archivo.'));
    reader.readAsDataURL(file);
  });
}

export async function uploadThesis(personId: string, file: File): Promise<ThesisUploadResponse> {
  const { data } = await apiClient.post<ThesisUploadResponse>(`/people/${personId}/role-documents`, {
    documentType: 'thesis',
    fileName: file.name,
    contentType: 'application/pdf',
    contentBase64: await fileToBase64(file),
  });
  return data;
}

/** Checks whether a thesis PDF was already uploaded for this person, so the
 *  UI can show it instead of an empty upload form after a page reload. */
export async function getThesisStatus(personId: string): Promise<{ fileName: string } | null> {
  try {
    const { data } = await apiClient.get<{ fileName: string }>(`/people/${personId}/thesis`);
    return data;
  } catch (error) {
    const status = (error as { response?: { status?: number } }).response?.status;
    if (status === 404) return null;
    throw error;
  }
}

export async function downloadThesis(personId: string, fileName: string): Promise<void> {
  const response = await apiClient.get(`/people/${personId}/thesis/${encodeURIComponent(fileName)}`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

export async function extractThesis(personId: string, fileName: string): Promise<{ storagePath: string; pageCount: number }> {
  const { data } = await apiClient.post<{ storagePath: string; pageCount: number }>(`/people/${personId}/thesis/${encodeURIComponent(fileName)}/extract`);
  return data;
}

export async function deleteThesis(personId: string, fileName: string): Promise<void> {
  await apiClient.delete(`/people/${personId}/thesis/${encodeURIComponent(fileName)}`);
}

export async function downloadThesisMarkdown(personId: string, fileName: string): Promise<void> {
  const response = await apiClient.get(`/people/${personId}/thesis/${encodeURIComponent(fileName)}/markdown`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${fileName.replace(/\.pdf$/i, '')}.md`;
  link.click();
  URL.revokeObjectURL(url);
}
