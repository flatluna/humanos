import { apiGet, apiDelete, apiPatch } from './httpClient';

/** Real backend response shape (PascalCase) — see CapabilityResponse.cs. */
export interface BackendCapability {
  CapabilityId: string;
  CapabilityDomainId: string;
  DomainCode: string;
  SubjectId: string | null;
  SubjectCode: string | null;
  Code: string;
  Name: string;
  Description: string | null;
  IsActive: boolean;
  /** "GraphMemoryParadox" | "TraditionalChapters" — which course-creation
   * method produced this capability. See Capability.LearningMode. */
  LearningMode: 'GraphMemoryParadox' | 'TraditionalChapters';
  /** "es" or "en" — the language this capability's entire generated
   * content was authored in. See Capability.ContentLanguageCode. */
  ContentLanguageCode: string;
  /** Optional tone/persona directive for ALL generated learner-facing
   * content on this capability. Null means "Standard". The only other
   * supported value today is "YoungLearner" (K-12 early-childhood
   * courses). See Capability.PedagogicalAudience. */
  PedagogicalAudience: string | null;
  /** True when this capability's blueprints include the optional 6th
   * "Examples" step (formal/school-track courses). See
   * Capability.IncludeExamplesStep. Set only at creation time — no update
   * endpoint exists for it today. */
  IncludeExamplesStep: boolean;
  LevelCount: number;
  ModuleCount: number;
  NodeCount: number;
  HasCoverImage: boolean;
  LearningSummary: string | null;
  Levels: string[];
  CreatedDate: string;
  UpdatedDate: string;
  /** See Capability.OwnerPersonId. Null for the public/official catalog. */
  OwnerPersonId: string | null;
  /** See Capability.OwnerTenantId. Null for a public capability or a Consumer's personal one. */
  OwnerTenantId: string | null;
}

export interface GetCapabilitiesFilters {
  language?: string;
  domain?: string;
  subject?: string;
  /** Filters by the language the capability's CONTENT was authored in
   * ("es"/"en") — distinct from `language` above, which only picks a
   * Translations row for Name/Description labels. */
  contentLanguage?: string;
}

/** Real "Capability catalog" list, 100% DB-backed (GET /capabilities). */
export async function getCapabilities(filters: GetCapabilitiesFilters = {}): Promise<BackendCapability[]> {
  const params = new URLSearchParams({ language: filters.language ?? 'es' });
  if (filters.domain) params.set('domain', filters.domain);
  if (filters.subject) params.set('subject', filters.subject);
  if (filters.contentLanguage) params.set('contentLanguage', filters.contentLanguage);

  const capabilities = await apiGet<BackendCapability[]>(`/capabilities?${params.toString()}`);
  return capabilities.sort((a, b) => new Date(b.UpdatedDate).getTime() - new Date(a.UpdatedDate).getTime());
}

/**
 * Permanently deletes a capability and ALL its content (ACID transaction
 * on the backend). Irreversible — the caller must confirm with the user
 * explicitly first (see DeleteCapabilityModal).
 */
export async function deleteCapability(capabilityId: string): Promise<void> {
  await apiDelete(`/capabilities/${capabilityId}`);
}

/**
 * Changes an EXISTING capability's tone/persona directive. Only affects
 * FUTURE generated content (new nodes added via the "add node" tool) —
 * never rewrites already-persisted blueprint step text for existing
 * nodes. Pass null to reset to "Standard".
 */
export async function updateCapabilityPedagogicalAudience(
  capabilityId: string,
  pedagogicalAudience: 'YoungLearner' | null,
): Promise<void> {
  await apiPatch(`/capabilities/${capabilityId}/pedagogical-audience`, { pedagogicalAudience });
}

/**
 * Flips an EXISTING capability between the public/official catalog
 * (Global) and scoped-to-one-person/tenant (Personal). Marking Personal
 * makes the current caller its new owner (requires a resolvable identity
 * — see getIdentityHeaders); only the current owner (or nobody, for an
 * already-public capability) may change it.
 */
export async function updateCapabilityVisibility(capabilityId: string, isGlobal: boolean): Promise<void> {
  await apiPatch(`/capabilities/${capabilityId}/visibility`, { isGlobal });
}
