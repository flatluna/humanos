import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { PersonalDirection } from '@/features/identity/types';
import type { GrowthPath } from '@/features/paths/types';
import type { ActiveGrowthPlan, GrowthPlanSelection } from '../types';

interface GrowthPlanState {
  personalDirection: PersonalDirection;
  /** Whether the Future Self / goals / motivations wizard has been
   *  completed at least once. Reopened via `reopenDirection` (Edit). */
  personalDirectionCompleted: boolean;
  activeGrowthPlan: ActiveGrowthPlan | null;
  setPersonalDirection: (direction: PersonalDirection) => void;
  reopenDirection: () => void;
  /** Builds an ActiveGrowthPlan from a selection of up to three paths.
   *  TODO: Once a real Growth Path backend exists, this should call an
   *  Azure Function instead of computing the summary on the client.
   */
  buildPlan: (selection: GrowthPlanSelection, paths: GrowthPath[]) => void;
}

function toSummary(path: GrowthPath | undefined) {
  if (!path) {
    return null;
  }

  const nextAction =
    path.origin === 'required'
      ? { en: 'Review required organizational policy', es: 'Revisa la política organizacional requerida' }
      : { en: 'Continue this path', es: 'Continúa esta ruta' };

  return {
    pathId: path.id,
    pathName: path.name,
    nextAction,
    readinessPercentage: path.origin === 'required' ? undefined : path.readinessPercentage,
    dueDate: path.dueDate,
  };
}

export const useGrowthPlanStore = create<GrowthPlanState>()(
  persist(
    (set) => ({
      personalDirection: { futureSelfId: null, goalIds: [], motivationIds: [] },
      personalDirectionCompleted: false,
      activeGrowthPlan: null,
      setPersonalDirection: (direction) =>
        set({ personalDirection: direction, personalDirectionCompleted: true }),
      reopenDirection: () => set({ personalDirectionCompleted: false }),
      buildPlan: (selection, paths) =>
        set({
          activeGrowthPlan: {
            primary: toSummary(paths.find((p) => p.id === selection.primaryPathId)),
            required: toSummary(paths.find((p) => p.id === selection.requiredPathId)),
            personal: toSummary(paths.find((p) => p.id === selection.personalPathId)),
          },
        }),
    }),
    { name: 'human-os-growth-plan' },
  ),
);
