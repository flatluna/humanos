import type { LocalizedText } from '@/localization/types';

export type { LocalizedText };

export type EvolutionLayerId =
  | 'foundation'
  | 'exploration'
  | 'mastery'
  | 'professional'
  | 'frontier'
  | 'creator';

/** Canonical order of the six Human OS evolution layers. */
export const EVOLUTION_LAYER_ORDER: EvolutionLayerId[] = [
  'foundation',
  'exploration',
  'mastery',
  'professional',
  'frontier',
  'creator',
];

export interface CapabilityProgress {
  id: string;
  name: LocalizedText;
  /** 0-100 */
  progress: number;
  /** 1-5 */
  level: number;
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

export type TodayActionType = 'recall' | 'practice' | 'project' | 'evidence';

export interface TodayAction {
  id: string;
  type: TodayActionType;
  title: LocalizedText;
  why: LocalizedText;
}

export interface HumanStateMetric {
  key: 'focus' | 'energy' | 'purpose' | 'confidence';
  /** 0-100 */
  value: number;
}

export interface TodaySnapshot {
  userName: string;
  currentLayer: EvolutionLayerId;
  /** 0-100, progress toward the next layer */
  progressTowardNextLayer: number;
  futureSelf: LocalizedText;
  motivations: LocalizedText[];
  personalGoal: PersonalGoal;
  /** `null` when the person does not belong to an organization. */
  organizationInitiative: OrganizationInitiative | null;
  sharedCapabilities: LocalizedText[];
  capabilities: CapabilityProgress[];
  actions: TodayAction[];
  humanState: HumanStateMetric[];
}
