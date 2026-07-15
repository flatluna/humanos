import { useQuery } from '@tanstack/react-query';
import { getRoleOperatingModelDraft } from '../data/mockRoleOperatingModelDraft';

/** Loads the Role Alignment Guide's draft `RoleOperatingModel` +
 *  findings. Currently backed by a canned mock (see
 *  `data/mockRoleOperatingModelDraft.ts`) until a real agent pipeline
 *  exists — wrapped in `useQuery` so the loading/error states already
 *  match the rest of the app's data-fetching conventions.
 */
export function useRoleOperatingModelDraftQuery() {
  return useQuery({
    queryKey: ['role-operating-model-draft'],
    queryFn: getRoleOperatingModelDraft,
  });
}
