import { getTenant, getPersonProfile } from '@/api/humanOsApi';
import { adaptToWorkContext } from '../adapters/workContextAdapter';
import { DEMO_PERSON_ID, DEMO_TENANT_ID } from '../constants';
import type { CorrectionRequest, WorkContext } from '../types';

/** Fetches the real Tenant + Person Profile via the existing Azure
 *  Functions and adapts them into the Work Context page model.
 */
export async function getWorkContext(): Promise<WorkContext> {
  const [tenant, personProfile] = await Promise.all([
    getTenant(DEMO_TENANT_ID),
    getPersonProfile(DEMO_PERSON_ID),
  ]);

  return adaptToWorkContext(tenant, personProfile);
}

/** TODO: Replace with a real Azure Function once a work-context
 *  confirmation endpoint exists, e.g.
 *  `POST /api/people/{personId}/work-context/confirm`.
 *  For now this resolves immediately; confirmed/unconfirmed state is
 *  tracked client-side in useWorkContextStore.
 */
export async function confirmWorkContext(): Promise<void> {
  return Promise.resolve();
}

/** TODO: Replace with a real Azure Function once a correction-request
 *  endpoint exists, e.g.
 *  `POST /api/people/{personId}/work-context/corrections`.
 */
export async function submitCorrectionRequest(_request: CorrectionRequest): Promise<void> {
  return Promise.resolve();
}
