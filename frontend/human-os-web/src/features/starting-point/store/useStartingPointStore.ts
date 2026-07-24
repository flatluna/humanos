import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { growthPlanApi, type AcceptedRecommendation } from '../../../api/growthPlanApi';

/** Matches the backend's StartCapabilityDevelopmentRequest.SelfAssessedLevel
 *  (see backend/HumanOS/Contracts/Capabilities/StartCapabilityDevelopmentRequest.cs). */
export type SelfAssessedLevel = 'Beginner' | 'Intermediate' | 'Advanced';

interface StartingPointState {
  /** Real Capability IDs the person picked as part of their starting point,
   *  across every Subject selected in Step 1. */
  selectedCapabilityIds: string[];
  /** One self-assessed level per selected Capability ID. */
  selfAssessedLevelByCapabilityId: Record<string, SelfAssessedLevel>;
  /** Capabilities the person wants but that don't exist in the catalog yet
   *  ("Áreas que requieren validación" in the overview copy) — free-text
   *  names, keyed by Subject code. Purely a wishlist for now: Step 4 (the
   *  agent-proposed plan) is what decides which of these actually get
   *  created via the CapabilityCreationOrchestrator pipeline. */
  gapCapabilityNamesBySubject: Record<string, string[]>;
  /** Agent-recommended programs/capabilities the person has accepted so
   *  far, one entry per subject code (adding a new one for the same
   *  subject code replaces the previous entry). */
  acceptedRecommendations: AcceptedRecommendation[];
  /** Whether this Growth Plan step has been completed at least once. */
  completed: boolean;
  toggleCapability: (capabilityId: string) => void;
  setLevel: (capabilityId: string, level: SelfAssessedLevel) => void;
  addGapCapability: (subjectCode: string, name: string) => void;
  removeGapCapability: (subjectCode: string, name: string) => void;
  upsertAcceptedRecommendation: (recommendation: AcceptedRecommendation) => void;
  removeAcceptedRecommendation: (subjectCode: string) => void;
  markCompleted: () => void;
  reopen: () => void;
  /** Load state from backend by personId. Falls back to localStorage if fetch fails. */
  loadFromBackend: (personId: string) => Promise<void>;
  /** Persist current state to backend by personId. */
  saveToBackend: (personId: string) => Promise<void>;
}

/** Growth Plan — Step 3 ("Your Starting Point"): for every Subject/area the
 *  person picked in Step 1, show the real Capabilities that already exist
 *  in that area and let them (a) pick which ones describe where they stand
 *  today, self-assessing a level per capability, and (b) name capabilities
 *  they want that don't exist yet (many Subjects have zero Capabilities
 *  today, e.g. "tecnologia" — that's expected during development, not an
 *  error state). Now syncs with backend database instead of localStorage-only.
 *  This step does NOT decide a plan or create capabilities; Step 4
 *  (agentProposedPlan) consumes this data.
 */
export const useStartingPointStore = create<StartingPointState>()(
  persist(
    (set, get) => ({
      selectedCapabilityIds: [],
      selfAssessedLevelByCapabilityId: {},
      gapCapabilityNamesBySubject: {},
      acceptedRecommendations: [],
      completed: false,
      toggleCapability: (capabilityId) =>
        set((state) => {
          const isSelected = state.selectedCapabilityIds.includes(capabilityId);
          const selectedCapabilityIds = isSelected
            ? state.selectedCapabilityIds.filter((existing) => existing !== capabilityId)
            : [...state.selectedCapabilityIds, capabilityId];

          const selfAssessedLevelByCapabilityId = { ...state.selfAssessedLevelByCapabilityId };
          if (isSelected) {
            delete selfAssessedLevelByCapabilityId[capabilityId];
          }

          return { selectedCapabilityIds, selfAssessedLevelByCapabilityId };
        }),
      setLevel: (capabilityId, level) =>
        set((state) => ({
          selfAssessedLevelByCapabilityId: { ...state.selfAssessedLevelByCapabilityId, [capabilityId]: level },
        })),
      addGapCapability: (subjectCode, name) =>
        set((state) => {
          const trimmed = name.trim();
          if (!trimmed) {
            return state;
          }
          const existing = state.gapCapabilityNamesBySubject[subjectCode] ?? [];
          if (existing.includes(trimmed)) {
            return state;
          }
          return {
            gapCapabilityNamesBySubject: {
              ...state.gapCapabilityNamesBySubject,
              [subjectCode]: [...existing, trimmed],
            },
          };
        }),
      removeGapCapability: (subjectCode, name) =>
        set((state) => ({
          gapCapabilityNamesBySubject: {
            ...state.gapCapabilityNamesBySubject,
            [subjectCode]: (state.gapCapabilityNamesBySubject[subjectCode] ?? []).filter(
              (existing) => existing !== name,
            ),
          },
        })),
      upsertAcceptedRecommendation: (recommendation) =>
        set((state) => ({
          acceptedRecommendations: [
            ...state.acceptedRecommendations.filter((r) => r.subjectCode !== recommendation.subjectCode),
            recommendation,
          ],
        })),
      removeAcceptedRecommendation: (subjectCode) =>
        set((state) => ({
          acceptedRecommendations: state.acceptedRecommendations.filter((r) => r.subjectCode !== subjectCode),
        })),
      markCompleted: () => set({ completed: true }),
      reopen: () => set({ completed: false }),
      loadFromBackend: async (personId: string) => {
        try {
          console.log('📥 GET /people/:personId/growth-plan/starting-point con personId:', personId);
          const data = await growthPlanApi.getStartingPoint(personId);
          if (data) {
            console.log('✅ Datos cargados desde SQL:', data);
            set({
              selectedCapabilityIds: data.selectedCapabilityIds || [],
              selfAssessedLevelByCapabilityId: {},
              gapCapabilityNamesBySubject: data.gapCapabilitiesBySubject || {},
              acceptedRecommendations: data.acceptedRecommendations || [],
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
          console.log('📤 POST /people/:personId/growth-plan/starting-point con datos:', {
            personId,
            selectedCapabilityIds: state.selectedCapabilityIds,
            gapCapabilitiesBySubject: state.gapCapabilityNamesBySubject,
            acceptedRecommendations: state.acceptedRecommendations,
            completed: state.completed,
          });
          await growthPlanApi.upsertStartingPoint(personId, {
            selectedCapabilityIds: state.selectedCapabilityIds,
            gapCapabilitiesBySubject: state.gapCapabilityNamesBySubject,
            acceptedRecommendations: state.acceptedRecommendations,
            completed: state.completed,
          });
          console.log('✅ Respuesta exitosa del servidor');
        } catch (error) {
          console.error('❌ Error en POST:', error);
          throw error;
        }
      },
    }),
    { name: 'human-os-starting-point' },
  ),
);
