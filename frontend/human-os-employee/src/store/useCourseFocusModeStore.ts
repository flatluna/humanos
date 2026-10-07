import { create } from 'zustand';

interface CourseFocusModeState {
  /** True while the student is inside a Structured Learning course/engram
   * session (2026-10-02) — the layout hides the sidebar and blocks all
   * navigation (including browser back) while this is true, only ever
   * cleared via the explicit "Exit course" action. */
  active: boolean;
  enter: () => void;
  exit: () => void;
}

export const useCourseFocusModeStore = create<CourseFocusModeState>()((set) => ({
  active: false,
  enter: () => set({ active: true }),
  exit: () => set({ active: false }),
}));
