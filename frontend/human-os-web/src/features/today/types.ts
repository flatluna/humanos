import type { LocalizedText } from '@/localization/types';

export type { LocalizedText };

export type CapabilityLevel = 'none' | 'basico' | 'intermedio' | 'avanzado';

export interface CapabilityProgress {
  id: string;
  name: LocalizedText;
  /** 0-100 */
  progress: number;
  /** Derived from the average score of nodes mastered so far in this capability. */
  level: CapabilityLevel;
  /** What this capability's growth supports (future self, a goal, an org initiative). */
  supports: LocalizedText[];
}

export interface PersonalGoal {
  id: string;
  title: LocalizedText;
}

export interface OrganizationInitiative {
  id: string;
  title: LocalizedText;
}

export interface HumanStateMetric {
  key: 'focus' | 'energy' | 'purpose' | 'confidence';
  /** 0-100 */
  value: number;
}

