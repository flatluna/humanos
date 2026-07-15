import type { Capability, TodayAction } from "@/types";

/**
 * Placeholder data for local development only — will be replaced by real
 * API data once the backend integration is wired up.
 */
export const mockCapabilities: Capability[] = [
  { id: "cap-1", domain: "mind", name: "Deep Focus", level: 62 },
  { id: "cap-2", domain: "build", name: "Shipping Discipline", level: 45 },
  { id: "cap-3", domain: "home", name: "Presence", level: 70 },
  { id: "cap-4", domain: "life", name: "Energy Management", level: 38 },
  { id: "cap-5", domain: "value", name: "Financial Clarity", level: 55 },
  { id: "cap-6", domain: "future", name: "Vision Setting", level: 30 },
];

export const mockTodayActions: TodayAction[] = [
  { id: "action-1", domain: "mind", title: "10 min focused reading", isComplete: false },
  { id: "action-2", domain: "build", title: "Ship one small improvement", isComplete: false },
  { id: "action-3", domain: "life", title: "Evening wind-down routine", isComplete: true },
];
