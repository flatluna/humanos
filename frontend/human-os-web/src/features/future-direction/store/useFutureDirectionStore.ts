import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { growthPlanApi } from '../../../api/growthPlanApi';

/** Universal goals a person might have — deliberately NOT corporate
 *  vocabulary ("promotion", "leadership"). Applies to a student, a
 *  homemaker, someone changing careers, etc. */
export type FutureGoalId =
  | 'helpFamily'
  | 'extraIncome'
  | 'careerChange'
  | 'dailyIndependence'
  | 'loveOfLearning'
  | 'personalChallenge'
  | 'wellbeing'
  | 'ownProject'
  | 'reclaimFocus';

export const FUTURE_GOAL_IDS: FutureGoalId[] = [
  'helpFamily',
  'extraIncome',
  'careerChange',
  'dailyIndependence',
  'loveOfLearning',
  'personalChallenge',
  'wellbeing',
  'ownProject',
  'reclaimFocus',
];

export type MotivationId =
  | 'growth'
  | 'helpingOthers'
  | 'independence'
  | 'curiosity'
  | 'security'
  | 'creativity'
  | 'pride';

export const MOTIVATION_IDS: MotivationId[] = [
  'growth',
  'helpingOthers',
  'independence',
  'curiosity',
  'security',
  'creativity',
  'pride',
];

interface FutureDirectionState {
  /** What the person would like to achieve. At least one required to
   *  continue (see FutureDirectionPage.canContinue). */
  selectedGoalIds: FutureGoalId[];
  /** What motivates them — optional, secondary signal. */
  selectedMotivationIds: MotivationId[];
  /** Whether this Growth Plan step has been completed at least once. */
  completed: boolean;
  toggleGoal: (id: FutureGoalId) => void;
  toggleMotivation: (id: MotivationId) => void;
  markCompleted: () => void;
  reopen: () => void;
  /** Load state from backend by personId. Falls back to localStorage if fetch fails. */
  loadFromBackend: (personId: string) => Promise<void>;
  /** Persist current state to backend by personId. */
  saveToBackend: (personId: string) => Promise<void>;
}

/** Growth Plan — Step 2 ("Where You Want to Go"): a universal survey of
 *  goals/motivations, replacing the employee-specific "Work and
 *  Experience" (job description/résumé) step for the individuals use
 *  case. Now syncs with backend database instead of localStorage-only.
 *  Later feeds the agent-proposed plan step alongside the Step 1 self-assessment.
 */
export const useFutureDirectionStore = create<FutureDirectionState>()(
  persist(
    (set, get) => ({
      selectedGoalIds: [],
      selectedMotivationIds: [],
      completed: false,
      toggleGoal: (id) =>
        set((state) => ({
          selectedGoalIds: state.selectedGoalIds.includes(id)
            ? state.selectedGoalIds.filter((existing) => existing !== id)
            : [...state.selectedGoalIds, id],
        })),
      toggleMotivation: (id) =>
        set((state) => ({
          selectedMotivationIds: state.selectedMotivationIds.includes(id)
            ? state.selectedMotivationIds.filter((existing) => existing !== id)
            : [...state.selectedMotivationIds, id],
        })),
      markCompleted: () => set({ completed: true }),
      reopen: () => set({ completed: false }),
      loadFromBackend: async (personId: string) => {
        try {
          console.log('📥 GET /people/:personId/growth-plan/future-direction con personId:', personId);
          const data = await growthPlanApi.getFutureDirection(personId);
          if (data) {
            console.log('✅ Datos cargados desde SQL:', data);
            set({
              selectedGoalIds: (data.selectedGoalIds as FutureGoalId[]) || [],
              selectedMotivationIds: (data.selectedMotivationCodes as MotivationId[]) || [],
              completed: data.completed || false,
            });
          } else {
            console.log('ℹ️ No hay datos guardados aún en SQL');
          }
        } catch (error) {
          console.warn('⚠️ Error al cargar, usando localStorage:', error);
          // Fallback to localStorage data (already loaded by persist middleware)
        }
      },
      saveToBackend: async (personId: string) => {
        try {
          const state = get();
          console.log('📤 POST /people/:personId/growth-plan/future-direction con datos:', {
            personId,
            selectedGoalIds: state.selectedGoalIds,
            selectedMotivationCodes: state.selectedMotivationIds,
            completed: state.completed,
          });
          await growthPlanApi.upsertFutureDirection(personId, {
            selectedGoalIds: state.selectedGoalIds,
            selectedMotivationCodes: state.selectedMotivationIds,
            completed: state.completed,
          });
          console.log('✅ Respuesta exitosa del servidor');
        } catch (error) {
          console.error('❌ Error en POST:', error);
          throw error;
        }
      },
    }),
    { name: 'human-os-future-direction' },
  ),
);
