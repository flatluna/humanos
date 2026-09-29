import { create } from 'zustand';
import type { GrowWithUsPlan } from '@/features/advance-your-knowledge/api/growWithUsPlanApi';
import {
  getGrowWithUsSavedSearch,
  listGrowWithUsSavedSearches,
  searchGrowWithUsSavedSearches,
  type GrowWithUsSavedSearchSummary,
} from '@/features/advance-your-knowledge/api/growWithUsSavedSearchApi';

/**
 * "Grow With Us" prompt modal state (2026-09-27) — opened from the main
 * side menu's "Grow With Us" item (EmployeeSidebar) instead of navigating
 * straight to "/", so the learner is asked for a free-text prompt first.
 * The generated plan is kept here (in-memory only, not persisted) so the
 * Advance Your Knowledge page (/advance-your-knowledge) can render it
 * after the modal closes and navigates there.
 *
 * Extended 2026-09-28 with the saved-searches history panel state: the
 * list is fetched once per personId and re-fetched after every new plan
 * generation (auto-saved server-side by GenerateGrowWithUsPlanFunction),
 * plus loading a past search back into `plan` and semantic-searching the
 * history by free text.
 */
interface GrowWithUsPlanState {
  isModalOpen: boolean;
  openModal: () => void;
  closeModal: () => void;
  plan: GrowWithUsPlan | null;
  isGenerating: boolean;
  lastError: string | null;
  setGenerating: (value: boolean) => void;
  setPlan: (plan: GrowWithUsPlan | null) => void;
  setError: (error: string | null) => void;

  savedSearches: GrowWithUsSavedSearchSummary[];
  activeSavedSearchId: string | null;
  isLoadingSavedSearches: boolean;
  isLoadingSavedSearch: boolean;
  loadSavedSearches: (personId: string) => Promise<void>;
  openSavedSearch: (personId: string, savedSearchId: string) => Promise<void>;
  searchSavedSearches: (personId: string, query: string) => Promise<void>;
}

export const useGrowWithUsPlanStore = create<GrowWithUsPlanState>()((set, get) => ({
  isModalOpen: false,
  openModal: () => set({ isModalOpen: true }),
  closeModal: () => set({ isModalOpen: false }),
  plan: null,
  isGenerating: false,
  lastError: null,
  setGenerating: (value) => set({ isGenerating: value }),
  setPlan: (plan) => set({ plan }),
  setError: (error) => set({ lastError: error }),

  savedSearches: [],
  activeSavedSearchId: null,
  isLoadingSavedSearches: false,
  isLoadingSavedSearch: false,

  loadSavedSearches: async (personId) => {
    set({ isLoadingSavedSearches: true });
    try {
      const savedSearches = await listGrowWithUsSavedSearches(personId);
      set({ savedSearches });
    } catch {
      // Best-effort — the panel just stays empty if this fails.
    } finally {
      set({ isLoadingSavedSearches: false });
    }
  },

  openSavedSearch: async (personId, savedSearchId) => {
    set({ isLoadingSavedSearch: true });
    try {
      const detail = await getGrowWithUsSavedSearch(personId, savedSearchId);
      set({ plan: detail.plan, activeSavedSearchId: savedSearchId });
    } catch (error) {
      set({ lastError: error instanceof Error ? error.message : String(error) });
    } finally {
      set({ isLoadingSavedSearch: false });
    }
  },

  searchSavedSearches: async (personId, query) => {
    set({ isLoadingSavedSearches: true });
    try {
      const savedSearches = query.trim()
        ? await searchGrowWithUsSavedSearches(personId, query.trim())
        : await listGrowWithUsSavedSearches(personId);
      set({ savedSearches });
    } catch {
      // Best-effort — keep whatever list was already shown.
    } finally {
      set({ isLoadingSavedSearches: false });
    }
  },
}));

