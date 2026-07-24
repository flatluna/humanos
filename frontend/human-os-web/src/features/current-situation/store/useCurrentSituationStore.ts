import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { growthPlanApi } from '../../../api/growthPlanApi';

/** Matches the backend's StartCapabilityDevelopmentRequest.SelfAssessedLevel
 *  (see backend/HumanOS/Contracts/Capabilities/StartCapabilityDevelopmentRequest.cs). */
export type SelfAssessedLevel = 'Beginner' | 'Intermediate' | 'Advanced';

interface CurrentSituationState {
  /** Subject codes the person picked as "areas that interest me right now". */
  selectedSubjectCodes: string[];
  /** One self-assessed level per selected subject code. */
  selfAssessedLevelBySubject: Record<string, SelfAssessedLevel>;
  /** Whether this Growth Plan step has been completed at least once. */
  completed: boolean;
  toggleSubject: (code: string) => void;
  setLevel: (code: string, level: SelfAssessedLevel) => void;
  markCompleted: () => void;
  reopen: () => void;
  /** Load state from backend by personId. Falls back to localStorage if fetch fails. */
  loadFromBackend: (personId: string) => Promise<void>;
  /** Persist current state to backend by personId. */
  saveToBackend: (personId: string) => Promise<void>;
}

/** Growth Plan — Step 1 ("Your Current Situation"): a universal onboarding
 *  survey (applies to anyone — student, homemaker, career changer — not
 *  just employees). Now syncs with backend database instead of localStorage-only.
 *  When the person later picks specific Capabilities within these areas, this
 *  self-assessment feeds StartCapabilityDevelopmentRequest.SelfAssessedLevel so
 *  the real PersonCapability record seeds ConfidenceScore/CurrentLevel/KnowledgeScore
 *  instead of starting from zero/unknown.
 */
export const useCurrentSituationStore = create<CurrentSituationState>()(
  persist(
    (set, get) => ({
      selectedSubjectCodes: [],
      selfAssessedLevelBySubject: {},
      completed: false,
      toggleSubject: (code) =>
        set((state) => {
          const isSelected = state.selectedSubjectCodes.includes(code);
          const selectedSubjectCodes = isSelected
            ? state.selectedSubjectCodes.filter((existing) => existing !== code)
            : [...state.selectedSubjectCodes, code];

          const selfAssessedLevelBySubject = { ...state.selfAssessedLevelBySubject };
          if (isSelected) {
            delete selfAssessedLevelBySubject[code];
          }

          return { selectedSubjectCodes, selfAssessedLevelBySubject };
        }),
      setLevel: (code, level) =>
        set((state) => ({
          selfAssessedLevelBySubject: { ...state.selfAssessedLevelBySubject, [code]: level },
        })),
      markCompleted: () => set({ completed: true }),
      reopen: () => set({ completed: false }),
      loadFromBackend: async (personId: string) => {
        try {
          console.log('📥 GET /people/:personId/growth-plan/current-situation con personId:', personId);
          const data = await growthPlanApi.getCurrentSituation(personId);
          if (data) {
            console.log('✅ Datos cargados desde SQL:', data);
            set({
              selectedSubjectCodes: data.selectedSubjectCodes || [],
              selfAssessedLevelBySubject: (data.selfAssessedLevelBySubject || {}) as Record<string, SelfAssessedLevel>,
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
          console.log('📤 POST /people/:personId/growth-plan/current-situation con datos:', {
            personId,
            selectedSubjectCodes: state.selectedSubjectCodes,
            selfAssessedLevelBySubject: state.selfAssessedLevelBySubject,
            completed: state.completed,
          });
          await growthPlanApi.upsertCurrentSituation(personId, {
            selectedSubjectCodes: state.selectedSubjectCodes,
            selfAssessedLevelBySubject: state.selfAssessedLevelBySubject,
            completed: state.completed,
          });
          console.log('✅ Respuesta exitosa del servidor');
        } catch (error) {
          console.error('❌ Error en POST:', error);
          throw error;
        }
      },
    }),
    { name: 'human-os-current-situation' },
  ),
);
