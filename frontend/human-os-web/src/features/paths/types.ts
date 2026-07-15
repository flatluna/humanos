import type { LocalizedText } from '@/localization/types';

export type GrowthPathOrigin = 'required' | 'recommended' | 'personal';
export type GrowthPathStatus = 'notStarted' | 'active' | 'completed';

export interface GrowthPathOutcomes {
  capabilityCount: number;
  practicalProjectCount: number;
  recallCycleCount: number;
  evidenceRequirementCount: number;
  /** 1-5 */
  targetIndependenceLevel: number;
}

export interface GrowthPath {
  id: string;
  name: LocalizedText;
  origin: GrowthPathOrigin;
  reason: LocalizedText;
  capabilitiesDeveloped: LocalizedText[];
  demonstration: LocalizedText[];
  outcomes: GrowthPathOutcomes;
  weeklyActionsEstimate: number;
  supportsFutureSelf: boolean;
  supportsRoleRequirement: boolean;
  supportsOrganizationalInitiative: boolean;
  availableLanguages: Array<'en' | 'es'>;
  status: GrowthPathStatus;
  /** 0-100 */
  readinessPercentage: number;
  /** ISO date string; only set for required/regulatory paths with a deadline. */
  dueDate?: string;
}
