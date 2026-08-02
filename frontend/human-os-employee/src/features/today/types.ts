export type CapabilityLevel = 'none' | 'basico' | 'intermedio' | 'avanzado';

export interface CapabilityProgress {
  id: string;
  name: string;
  /** 0-100 */
  progress: number;
  /** Derived from the average score of nodes mastered so far in this capability. */
  level: CapabilityLevel;
  /** What this capability belongs to (its subject, or "Independiente" when uncategorized). */
  supports: string[];
}
