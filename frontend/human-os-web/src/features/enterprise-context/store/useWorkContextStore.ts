import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { PendingCorrectionStatus, WorkContextConfirmationStatus } from '../types';

interface WorkContextState {
  confirmationStatus: WorkContextConfirmationStatus;
  pendingCorrectionStatus: PendingCorrectionStatus;
  confirm: () => void;
  submitCorrection: () => void;
}

/** Local, client-side state for the two Work Context actions that do not
 *  yet have a backend endpoint (confirming the summary and requesting a
 *  correction). See workContextService.ts for the matching TODOs.
 */
export const useWorkContextStore = create<WorkContextState>()(
  persist(
    (set) => ({
      confirmationStatus: 'unconfirmed',
      pendingCorrectionStatus: 'none',
      confirm: () => set({ confirmationStatus: 'confirmed' }),
      submitCorrection: () => set({ pendingCorrectionStatus: 'submitted' }),
    }),
    { name: 'human-os-work-context' },
  ),
);
