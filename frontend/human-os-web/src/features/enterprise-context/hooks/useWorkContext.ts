import { useMutation, useQuery } from '@tanstack/react-query';
import { getWorkContext, confirmWorkContext, submitCorrectionRequest } from '../services/workContextService';
import { useWorkContextStore } from '../store/useWorkContextStore';
import type { CorrectionRequest, WorkContext } from '../types';

export function useWorkContext() {
  const confirmationStatus = useWorkContextStore((state) => state.confirmationStatus);
  const pendingCorrectionStatus = useWorkContextStore((state) => state.pendingCorrectionStatus);

  const query = useQuery({
    queryKey: ['work-context'],
    queryFn: getWorkContext,
  });

  const data: WorkContext | undefined = query.data
    ? { ...query.data, confirmationStatus, pendingCorrectionStatus }
    : undefined;

  return { ...query, data };
}

export function useConfirmWorkContext() {
  const confirm = useWorkContextStore((state) => state.confirm);

  return useMutation({
    mutationFn: confirmWorkContext,
    onSuccess: () => confirm(),
  });
}

export function useSubmitCorrectionRequest() {
  const submitCorrection = useWorkContextStore((state) => state.submitCorrection);

  return useMutation({
    mutationFn: (request: CorrectionRequest) => submitCorrectionRequest(request),
    onSuccess: () => submitCorrection(),
  });
}
