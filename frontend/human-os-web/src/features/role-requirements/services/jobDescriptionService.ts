import type { JobDescription } from '../types';

/** TODO: Replace with a real Azure Function once an official job
 *  description / organizational role-standards endpoint exists (e.g.
 *  GET /api/roles/{roleId}/job-description). No such backend concept
 *  exists today, so this always returns null — the employee can still
 *  create a working draft via MissingJobDescription.
 */
export async function getJobDescription(): Promise<JobDescription | null> {
  return null;
}

/** TODO: Replace with a real Azure Function once a job-description
 *  feedback endpoint exists, e.g.
 *  `POST /api/people/{personId}/role-experience/job-description/feedback`.
 */
export async function submitJobDescriptionFeedback(
  _feedback: { type: 'reflects' | 'different'; note?: string },
): Promise<void> {
  return Promise.resolve();
}
