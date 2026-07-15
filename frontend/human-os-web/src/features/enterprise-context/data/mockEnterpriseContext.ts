import type { EnterpriseContext } from '../types';

/** TODO: Replace with a real Azure Function once an organization/role
 *  assignment endpoint exists (e.g. GET /api/people/{personId}/enterprise-context).
 *  Person/Tenant already exist in the backend; Department/Team/Role
 *  assignment do not yet have dedicated endpoints.
 */
export async function getEnterpriseContext(): Promise<EnterpriseContext> {
  return {
    organizationName: 'Contoso',
    department: 'Digital Transformation',
    team: 'Product Innovation',
    roleName: 'Product Owner',
    managerName: 'Alex Rivera',
  };
}
