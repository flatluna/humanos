import { create } from 'zustand';

interface HumanOsAgentPanelState {
  isOpen: boolean;
  open: () => void;
  close: () => void;
}

export const useHumanOsAgentPanelStore = create<HumanOsAgentPanelState>()((set) => ({
  isOpen: false,
  open: () => set({ isOpen: true }),
  close: () => set({ isOpen: false }),
}));
