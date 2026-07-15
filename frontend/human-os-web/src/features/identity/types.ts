import type { LocalizedText } from '@/localization/types';

export interface FutureSelfOption {
  id: string;
  title: LocalizedText;
}

export interface GrowthGoalOption {
  id: string;
  title: LocalizedText;
}

export interface MotivationOption {
  id: string;
  title: LocalizedText;
}

/** The employee's selected personal direction. `null` fields mean the
 *  corresponding step has not been completed yet.
 */
export interface PersonalDirection {
  futureSelfId: string | null;
  goalIds: string[];
  motivationIds: string[];
}
