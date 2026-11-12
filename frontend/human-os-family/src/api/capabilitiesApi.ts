export interface HumanDevelopmentStage {
  stageId: string;
  stageKey: string;
  stageNameEs: string;
  stageNameEn: string;
  ageRange: string;
  categoryGroup: string;
  displayOrder: number;
  descriptionEs: string | null;
  descriptionEn: string | null;
}

export interface HumanCapability {
  capabilityId: string;
  code: string;
  stageKey: string;
  nameEs: string;
  nameEn: string;
  domain: string;
  descriptionEs: string;
  descriptionEn: string;
  whyItMattersEs: string | null;
  whyItMattersEn: string | null;
  targetMasteryLevel: string;
  recommendedHoursToMaster: number;
  skillsCovered: string[];
  isGlobal: boolean;
  familyId: string | null;
}

export interface Country {
  countryId: string;
  code: string;
  nameEs: string;
  nameEn: string;
  displayOrder: number;
}

export interface EducationSystemType {
  educationSystemTypeId: string;
  code: string;
  nameEs: string;
  nameEn: string;
  displayOrder: number;
}

export interface Subject {
  subjectId: string;
  code: string;
  nameEs: string;
  nameEn: string;
  subjectCategory: 'Academic' | 'Character';
  displayOrder: number;
}

export interface CapabilityRecommendation {
  recommendationId: string;
  code: string;
  countryCode: string;
  educationSystemTypeCode: string;
  stageKey: string;
  subjectCode: string;
  /** Human-readable age range for the grade/stage, e.g. '6 - 7 años'. From the stages catalog, not AI-generated. */
  ageRange: string | null;
  /** 'es' | 'en' — single language this recommendation's real content was generated in (Es/En
   * fields hold the same duplicated text). Null for legacy bilingual rows. */
  language: 'es' | 'en' | null;
  nameEs: string;
  nameEn: string;
  descriptionEs: string;
  descriptionEn: string;
  whyItMattersEs: string | null;
  whyItMattersEn: string | null;
  microSkillsCoveredEs: string[];
  microSkillsCoveredEn: string[];
  /** Prerequisites/requirements a student should already have before starting this course. */
  courseRequirementsEs: string | null;
  courseRequirementsEn: string | null;
  /** Short 4-8 item chapter/topic-title preview, shown before sending to Studio. */
  courseOutlineEs: string[];
  courseOutlineEn: string[];
  /** Sources/references the AI agent actually used (advanced teaching methods from countries
   * like Japan/Finland/Singapore, and/or real URLs found via live web search). Empty if none. */
  sources: string[];
  generationStatus: 'PendingEngramGeneration' | 'SentToStudio' | 'Generated';
  studioIdeaDraftEs: string | null;
  studioIdeaDraftEn: string | null;
  studioRunId: string | null;
  /** The Studio-side StructuredLearningDocumentId once the course/Engrams exist. Null until then. */
  studioDocumentId: string | null;
  createdDate: string;
}

export interface GenerateRecommendationsRequest {
  countryCode: string;
  educationSystemTypeCode: string;
  stageKey: string;
  subjectCode: string;
  /** 'es' | 'en' — when set, the course is generated in that single language only (no
   * redundant translation effort); when omitted, legacy full bilingual generation runs. */
  language?: 'es' | 'en';
}

export interface UpdateRecommendationRequest {
  nameEs: string;
  nameEn: string;
  descriptionEs: string;
  descriptionEn: string;
  whyItMattersEs?: string | null;
  whyItMattersEn?: string | null;
  microSkillsCoveredEs?: string[];
  microSkillsCoveredEn?: string[];
  courseRequirementsEs?: string | null;
  courseRequirementsEn?: string | null;
  courseOutlineEs?: string[];
  courseOutlineEn?: string[];
}

export const capabilitiesApi = {
  /**
   * Global catalog of human development stages / grade levels, shared across all tenants.
   */
  async getStages(): Promise<HumanDevelopmentStage[]> {
    const response = await fetch('/api/family/stages', {
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) {
      throw new Error(`Failed to load human development stages (${response.status})`);
    }
    return response.json();
  },

  /**
   * Capabilities for a given stage: global reference capabilities plus any family-custom ones.
   */
  async getStageCapabilities(stageKey: string, familyId?: string | null): Promise<HumanCapability[]> {
    const query = familyId ? `?familyId=${encodeURIComponent(familyId)}` : '';
    const response = await fetch(`/api/family/stages/${encodeURIComponent(stageKey)}/capabilities${query}`, {
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) {
      throw new Error(`Failed to load capabilities for stage ${stageKey} (${response.status})`);
    }
    return response.json();
  },

  /** Global catalog of supported countries. */
  async getCountries(): Promise<Country[]> {
    const response = await fetch('/api/family/lookups/countries', { headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error(`Failed to load countries (${response.status})`);
    return response.json();
  },

  /** Global catalog of education system types (Public, Montessori, IB, etc.). */
  async getEducationSystemTypes(): Promise<EducationSystemType[]> {
    const response = await fetch('/api/family/lookups/education-system-types', { headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error(`Failed to load education system types (${response.status})`);
    return response.json();
  },

  /** Global catalog of subjects (Academic + Character/Life Skills). */
  async getSubjects(): Promise<Subject[]> {
    const response = await fetch('/api/family/lookups/subjects', { headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error(`Failed to load subjects (${response.status})`);
    return response.json();
  },

  /** Runs the AI Subject Capability Recommender Agent and saves results as pending recommendations. */
  async generateRecommendations(request: GenerateRecommendationsRequest): Promise<CapabilityRecommendation[]> {
    const response = await fetch('/api/family/capability-recommendations/generate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(request),
    });
    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(`Failed to generate recommendations (${response.status}): ${text}`);
    }
    return response.json();
  },

  /** Lists saved AI capability recommendations, optionally filtered. */
  async getRecommendations(filters?: Partial<GenerateRecommendationsRequest>): Promise<CapabilityRecommendation[]> {
    const params = new URLSearchParams();
    if (filters?.countryCode) params.set('countryCode', filters.countryCode);
    if (filters?.educationSystemTypeCode) params.set('educationSystemTypeCode', filters.educationSystemTypeCode);
    if (filters?.stageKey) params.set('stageKey', filters.stageKey);
    if (filters?.subjectCode) params.set('subjectCode', filters.subjectCode);
    const query = params.toString() ? `?${params.toString()}` : '';
    const response = await fetch(`/api/family/capability-recommendations${query}`, { headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error(`Failed to load recommendations (${response.status})`);
    return response.json();
  },

  /** Edits the text fields of an existing, still-pending recommendation. */
  async updateRecommendation(recommendationId: string, request: UpdateRecommendationRequest): Promise<CapabilityRecommendation> {
    const response = await fetch(`/api/family/capability-recommendations/${encodeURIComponent(recommendationId)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify(request),
    });
    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(`Failed to update recommendation (${response.status}): ${text}`);
    }
    return response.json();
  },

  /** Permanently deletes a pending AI capability recommendation. */
  async deleteRecommendation(recommendationId: string): Promise<void> {
    const response = await fetch(`/api/family/capability-recommendations/${encodeURIComponent(recommendationId)}`, {
      method: 'DELETE',
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) {
      throw new Error(`Failed to delete recommendation (${response.status})`);
    }
  },

  /** Marks a recommendation as sent to HumanOS Studio (after the admin opens NewCapabilityPage for it). */
  async markRecommendationSentToStudio(recommendationId: string, studioRunId?: string | null): Promise<void> {
    const response = await fetch(`/api/family/capability-recommendations/${encodeURIComponent(recommendationId)}/mark-sent-to-studio`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ studioRunId: studioRunId ?? null }),
    });
    if (!response.ok) {
      throw new Error(`Failed to mark recommendation as sent to Studio (${response.status})`);
    }
  },

  /**
   * Checks whether Studio has already created the course/Engrams for this recommendation
   * (matched by Code) and, if so, flips it to 'Generated' with its studioDocumentId filled in.
   * Safe to call repeatedly — a no-op once already Generated.
   */
  async syncRecommendationStudioStatus(recommendationId: string): Promise<CapabilityRecommendation> {
    const response = await fetch(`/api/family/capability-recommendations/${encodeURIComponent(recommendationId)}/sync-studio-status`, {
      method: 'POST',
      headers: { Accept: 'application/json' },
    });
    if (!response.ok) {
      throw new Error(`Failed to sync Studio status (${response.status})`);
    }
    return response.json();
  },
};
