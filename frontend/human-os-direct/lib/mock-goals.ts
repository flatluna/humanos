/**
 * Mock goal data for local development. Once backend integration is wired,
 * replace with real API calls. Uses i18n keys instead of hardcoded text.
 */
export const mockGoals: Goal[] = [
  {
    id: "goal-1",
    titleKey: "goals.mock.invest", // "Invertir bien mi dinero" / "Invest my money wisely"
    descriptionKey: "goals.mock.invest_desc",
    targetDate: "2025-12-31",
    isAchieved: false,
  },
  {
    id: "goal-2",
    titleKey: "goals.mock.firstJob",
    descriptionKey: "goals.mock.firstJob_desc",
    targetDate: "2025-06-30",
    isAchieved: false,
  },
  {
    id: "goal-3",
    titleKey: "goals.mock.timeManagement",
    descriptionKey: "goals.mock.timeManagement_desc",
    targetDate: "2024-12-31",
    isAchieved: true,
  },
];

/**
 * Mock connections between goals and capabilities.
 * In reality, this comes from the GoalCapability table.
 */
export const mockGoalCapabilities: Record<string, string[]> = {
  "goal-1": ["cap-5", "cap-6"], // Financial Clarity + Vision Setting
  "goal-2": ["cap-1", "cap-4"], // Deep Focus + Energy Management
  "goal-3": ["cap-4"],           // Energy Management
};

/**
 * Mock person capability data (the real progress source).
 * Each goal's overall progress = average of its connected capabilities.
 * Uses i18n keys for capability names.
 */
export const mockPersonCapabilitiesForGoals: PersonCapability[] = [
  {
    id: "pc-5",
    capabilityId: "cap-5",
    capabilityNameKey: "capabilities.mock.financial",
    level: 7,
    progressPercentage: 70,
    masteryScore: 65,
  },
  {
    id: "pc-6",
    capabilityId: "cap-6",
    capabilityNameKey: "capabilities.mock.vision",
    level: 5,
    progressPercentage: 54,
    masteryScore: 48,
  },
  {
    id: "pc-1",
    capabilityId: "cap-1",
    capabilityNameKey: "capabilities.mock.deepFocus",
    level: 6,
    progressPercentage: 60,
    masteryScore: 55,
  },
  {
    id: "pc-4",
    capabilityId: "cap-4",
    capabilityNameKey: "capabilities.mock.energy",
    level: 8,
    progressPercentage: 100,
    masteryScore: 95,
  },
];

export type Goal = {
  id: string;
  titleKey: string;
  descriptionKey?: string;
  targetDate?: string;
  isAchieved: boolean;
};

export type GoalCapability = {
  id: string;
  goalId: string;
  capabilityId: string;
};

export type PersonCapability = {
  id: string;
  capabilityId: string;
  capabilityNameKey: string;
  level: number;
  progressPercentage: number;
  masteryScore: number;
};
