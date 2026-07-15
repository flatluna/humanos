import type { LocalizedText } from '@/localization/types';

export interface AlignmentSummary {
  futureSelf: LocalizedText;
  role: LocalizedText;
  organizationInitiative: LocalizedText | null;
  sharedCapabilities: LocalizedText[];
}

export interface GrowthPlanSelection {
  primaryPathId: string | null;
  requiredPathId: string | null;
  personalPathId: string | null;
}

export interface ActiveGrowthPathSummary {
  pathId: string;
  pathName: LocalizedText;
  nextAction: LocalizedText;
  /** 0-100. Omitted for required paths tracked by due date instead. */
  readinessPercentage?: number;
  /** ISO date string. Only present for required/regulatory paths. */
  dueDate?: string;
}

/** `null` means the employee has not yet built a Growth Plan (State A). */
export interface ActiveGrowthPlan {
  primary: ActiveGrowthPathSummary | null;
  required: ActiveGrowthPathSummary | null;
  personal: ActiveGrowthPathSummary | null;
}
