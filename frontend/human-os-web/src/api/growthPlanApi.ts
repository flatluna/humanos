import { apiClient } from ".";

export interface CurrentSituation {
  selectedSubjectCodes: string[];
  selfAssessedLevelBySubject: Record<string, string>;
  completed: boolean;
}

export interface FutureDirection {
  selectedGoalIds: string[];
  selectedMotivationCodes: string[];
  completed: boolean;
}

export interface RecommendedProgramStep {
  name: string;
  level: string;
}

/** One agent-recommended program/capabilities snapshot the person accepted
 *  for a subject in Step 3 — a frozen copy, not a live link (matches
 *  backend's Contracts/GrowthPlan/GrowthPlanContracts.cs AcceptedRecommendation). */
export interface AcceptedRecommendation {
  subjectCode: string;
  recommendationType: string;
  programName: string;
  programDescription: string;
  steps: RecommendedProgramStep[];
  rationale: string;
  programId: string | null;
}

export interface StartingPoint {
  selectedCapabilityIds: string[];
  gapCapabilitiesBySubject: Record<string, string[]>;
  acceptedRecommendations: AcceptedRecommendation[];
  completed: boolean;
}

/**
 * Growth Plan API client – persists 3-step wizard state to backend
 */
export const growthPlanApi = {
  // ── Current Situation (Step 1) ───────────────────

  async getCurrentSituation(personId: string): Promise<CurrentSituation | null> {
    try {
      const response = await apiClient.get<CurrentSituation>(
        `/people/${personId}/growth-plan/current-situation`
      );
      return response.data || null;
    } catch (error: any) {
      if (error.response?.status === 404) return null;
      throw error;
    }
  },

  async upsertCurrentSituation(
    personId: string,
    data: CurrentSituation
  ): Promise<CurrentSituation> {
    const response = await apiClient.post<CurrentSituation>(
      `/people/${personId}/growth-plan/current-situation`,
      data
    );
    return response.data;
  },

  // ── Future Direction (Step 2) ────────────────────

  async getFutureDirection(personId: string): Promise<FutureDirection | null> {
    try {
      const response = await apiClient.get<FutureDirection>(
        `/people/${personId}/growth-plan/future-direction`
      );
      return response.data || null;
    } catch (error: any) {
      if (error.response?.status === 404) return null;
      throw error;
    }
  },

  async upsertFutureDirection(
    personId: string,
    data: FutureDirection
  ): Promise<FutureDirection> {
    const response = await apiClient.post<FutureDirection>(
      `/people/${personId}/growth-plan/future-direction`,
      data
    );
    return response.data;
  },

  // ── Starting Point (Step 3) ──────────────────────

  async getStartingPoint(personId: string): Promise<StartingPoint | null> {
    try {
      const response = await apiClient.get<StartingPoint>(
        `/people/${personId}/growth-plan/starting-point`
      );
      return response.data || null;
    } catch (error: any) {
      if (error.response?.status === 404) return null;
      throw error;
    }
  },

  async upsertStartingPoint(
    personId: string,
    data: StartingPoint
  ): Promise<StartingPoint> {
    const response = await apiClient.post<StartingPoint>(
      `/people/${personId}/growth-plan/starting-point`,
      data
    );
    return response.data;
  }
};
