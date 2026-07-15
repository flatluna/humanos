import { useQuery } from '@tanstack/react-query';
import { getEnterpriseContext } from '../data/mockEnterpriseContext';

export function useEnterpriseContext() {
  return useQuery({
    queryKey: ['enterprise-context'],
    queryFn: getEnterpriseContext,
    staleTime: 1000 * 60 * 10,
  });
}
