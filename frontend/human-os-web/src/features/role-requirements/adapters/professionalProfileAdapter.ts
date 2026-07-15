import type { UploadRoleDocumentResponse } from '@/api/types';
import type { ResumeDocument } from '../types';

export function adaptToResumeDocument(fileName: string, response: UploadRoleDocumentResponse): ResumeDocument {
  return {
    id: response.storagePath,
    fileName,
    uploadedDate: response.uploadedDate,
    storagePath: response.storagePath,
  };
}
