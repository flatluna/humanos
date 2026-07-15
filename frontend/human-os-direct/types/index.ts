/** Growth dimensions ("domains") of Human OS, each with its own accent color. */
export type Domain = "mind" | "build" | "home" | "life" | "value" | "future";

export interface Capability {
  id: string;
  domain: Domain;
  name: string;
  level: number; // 0-100
  description?: string;
}

/** A single layer of the user's personal operating system (e.g. a life area). */
export interface Layer {
  id: string;
  domain: Domain;
  title: string;
  summary: string;
}

export interface TodayAction {
  id: string;
  domain: Domain;
  title: string;
  isComplete: boolean;
}
