import type { TenantResponse, PersonProfileResponse } from '@/api/types';
import type { WorkContext, WorkContextFieldProvenance } from '../types';

const REAL: WorkContextFieldProvenance = { source: 'organization' };
const REAL_PROFILE: WorkContextFieldProvenance = { source: 'employeeProfile' };

function mock(todo: string): WorkContextFieldProvenance {
  return { source: 'temporaryPlaceholder', todo };
}

/** Combines the real Tenant + Person Profile API responses into the
 *  WorkContext page model, filling in enterprise HR fields the backend
 *  does not yet model with clearly-flagged temporary placeholders.
 *
 *  NOTE: GetHumanProfile was inspected per the implementation brief but
 *  is intentionally NOT called here — its fields (mission statement,
 *  learning style, motivation/confidence scores) are personal-growth
 *  concepts, not organizational Work Context fields, so calling it would
 *  add network cost with no benefit to this screen.
 */
export function adaptToWorkContext(tenant: TenantResponse, personProfile: PersonProfileResponse): WorkContext {
  return {
    personId: personProfile.personId,
    tenantId: tenant.tenantId,

    organization: tenant.name,
    businessUnit: 'Product and Innovation',
    department: 'Digital Transformation',
    team: 'Product Innovation',
    currentRole: personProfile.occupation,
    roleLevel: 'Senior',
    jobFamily: 'Product Management',
    workLocation: 'Remote — United States',
    employmentType: 'fullTime',
    preferredLanguage: personProfile.preferredLanguage,
    manager: 'Alex Rivera',

    fieldProvenance: {
      organization: REAL,
      businessUnit: mock(
        'Business Unit does not exist in the backend yet. Add a BusinessUnit field or a dedicated OrgUnit concept alongside Tenant.',
      ),
      department: mock(
        'Department assignment does not exist in the backend yet. Add a PersonRoleAssignment/Department endpoint.',
      ),
      team: mock('Team assignment does not exist in the backend yet. Add a Team endpoint alongside Department.'),
      currentRole: {
        ...REAL_PROFILE,
        todo:
          'Backend has no dedicated Role/JobTitle field. Using PersonProfile.Occupation as a proxy until a JobRole assignment endpoint exists.',
      },
      roleLevel: mock('Role Level does not exist in the backend yet. Add it to a future JobRole assignment endpoint.'),
      jobFamily: mock('Job Family does not exist in the backend yet. Add it to a future JobRole catalog.'),
      workLocation: mock(
        'Work Location does not exist in the backend yet. PersonProfile.CountryCode is too coarse a substitute; add a dedicated WorkLocation field.',
      ),
      employmentType: mock('Employment Type does not exist in the backend yet. Add it to a future Person/Role assignment endpoint.'),
      preferredLanguage: REAL_PROFILE,
      manager: mock('Manager assignment does not exist in the backend yet. Add a ManagerPersonId field to a future Role assignment endpoint.'),
    },

    dataSource: 'employeeProfile',
    lastSynchronizedDate: personProfile.updatedDate,
    verificationStatus: 'verified',
    confirmationStatus: 'unconfirmed',
    pendingCorrectionStatus: 'none',
  };
}
