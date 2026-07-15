import { uploadRoleDocument } from '@/api/humanOsApi';
import { DEMO_PERSON_ID } from '@/features/enterprise-context/constants';
import { adaptToResumeDocument } from '../adapters/professionalProfileAdapter';
import type { DeclaredExperienceItem, ResumeDocument } from '../types';

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

/** Uploads a résumé via the real Data Lake upload Azure Function
 *  (UploadRoleDocumentFunction). Will fail with a 503 until the
 *  backend's "DataLakeStorage" application setting is configured with
 *  real credentials — see backend/HumanOS/Storage/RoleDocumentStorageService.cs.
 */
export async function uploadResume(file: File): Promise<ResumeDocument> {
  const contentBase64 = await fileToBase64(file);

  const response = await uploadRoleDocument(DEMO_PERSON_ID, {
    documentType: 'resume',
    fileName: file.name,
    contentType: file.type,
    contentBase64,
  });

  return adaptToResumeDocument(file.name, response);
}

/** TODO: Replace with a real Azure Function once agent-based résumé
 *  extraction exists. Simulates a short "processing" delay and returns a
 *  small set of canned declared-experience items so the review UX can be
 *  built and previewed before the real extraction pipeline exists.
 *
 *  Every item is intentionally left at `unvalidated` / `needsValidation`
 *  with `validationAuthority: null` — never `validated` — per the résumé
 *  interpretation rule: a résumé is a declaration of experience, not
 *  proof of mastery. `source` (not `validationStatus`) is what records
 *  that these came from the résumé/agent inference.
 */
export async function simulateResumeExtraction(): Promise<DeclaredExperienceItem[]> {
  await new Promise((resolve) => setTimeout(resolve, 1400));

  return [
    {
      id: 'exp-cross-functional-leadership',
      text: {
        en: 'Experience leading cross-functional product teams',
        es: 'Experiencia liderando equipos de producto multifuncionales',
      },
      source: 'resume',
      validationStatus: 'unvalidated',
      validationAuthority: null,
    },
    {
      id: 'exp-ai-product',
      text: {
        en: 'Possible AI product experience identified',
        es: 'Posible experiencia en productos de IA identificada',
      },
      source: 'resume',
      validationStatus: 'needsValidation',
      validationAuthority: null,
    },
    {
      id: 'exp-stakeholder-comms',
      text: {
        en: 'Stakeholder communication across multiple projects',
        es: 'Comunicación con interesados en múltiples proyectos',
      },
      source: 'agentInferred',
      validationStatus: 'unvalidated',
      validationAuthority: null,
    },
  ];
}
