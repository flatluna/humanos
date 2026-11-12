import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface SidebarState {
  isOpen: boolean;
  toggleSidebar: () => void;
  setSidebarOpen: (isOpen: boolean) => void;
}

export const useSidebarStore = create<SidebarState>()(
  persist(
    (set, get) => ({
      isOpen: true,
      toggleSidebar: () => set({ isOpen: !get().isOpen }),
      setSidebarOpen: (isOpen) => set({ isOpen }),
    }),
    { name: 'human-os-enterprise-sidebar' },
  ),
);
