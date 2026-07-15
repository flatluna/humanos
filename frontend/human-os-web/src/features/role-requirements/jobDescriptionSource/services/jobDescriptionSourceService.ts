import { uploadRoleDocument } from '@/api/humanOsApi';
import { DEMO_PERSON_ID } from '@/features/enterprise-context/constants';
import type { JobDescription } from '../../types';

/** TODO: Replace with a real Azure Function once an official job
 *  description / organizational role-standards endpoint exists (e.g.
 *  GET /api/roles/{roleId}/job-description). No such backend concept
 *  exists today, so this always returns `null` — the employee can
 *  still provide their own working description.
 */
export async function getOfficialJobDescription(): Promise<JobDescription | null> {
  return null;
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();

    reader.onload = () => {
      const result = reader.result;
      if (typeof result !== 'string') {
        reject(new Error('Could not read the selected file.'));
        return;
      }
      resolve(result.split(',')[1] ?? '');
    };

    reader.onerror = () => reject(reader.error ?? new Error('Could not read the selected file.'));
    reader.readAsDataURL(file);
  });
}

/** Uploads a job description file via the real Data Lake upload Azure
 *  Function (`UploadRoleDocumentFunction`, `documentType: "job-description"`
 *  — the same real endpoint the résumé upload uses, just a different
 *  document type it already accepts). Will fail with a 503 until the
 *  backend's `DataLakeStorage` application setting is configured with
 *  real credentials.
 *
 *  IMPORTANT: this only stores the raw file. There is no backend
 *  extraction pipeline yet — the file's *content* is never parsed or
 *  presented as analyzed. Until a real ingestion function exists, the
 *  uploaded file is recorded as employee-provided context (file name +
 *  storage path only), never as an official, structured Job
 *  Description.
 *
 *  TODO: build a dedicated, secure Job Description ingestion Azure
 *  Function (agent-based extraction) that reads the file back out of
 *  Data Lake and proposes structured `JobDescription` fields for
 *  organization/employee review — analogous to the résumé extraction
 *  TODO in `professionalProfileService.ts`, but for this document type.
 */
export async function uploadJobDescriptionFile(file: File): Promise<{ fileName: string; storagePath: string }> {
  const contentBase64 = await fileToBase64(file);

  const response = await uploadRoleDocument(DEMO_PERSON_ID, {
    documentType: 'job-description',
    fileName: file.name,
    contentType: file.type,
    contentBase64,
  });

  return { fileName: file.name, storagePath: response.storagePath };
}
