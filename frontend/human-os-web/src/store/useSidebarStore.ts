import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface SidebarState {
  /** Manually collapsed to icon-only via the header toggle button. */
  collapsed: boolean;
  toggle: () => void;
}

export const useSidebarStore = create<SidebarState>()(
  persist(
    (set, get) => ({
      collapsed: false,
      toggle: () => set({ collapsed: !get().collapsed }),
    }),
    { name: 'human-os-sidebar' },
  ),
);
