import { apiDelete, apiGet, apiPost } from '@/features/capabilities/api/httpClient';

/**
 * "Mis Programas" — client for GET /people/{personId}/enrolled-programs.
 * Mirrors GrowthPlanService.GetEnrolledProgramsAsync's DTOs
 * (Contracts/GrowthPlan/EnrolledProgramContracts.cs), serialized camelCase
 * by FunctionResponseFactory (same convention as capabilityGraphApi.ts).
 */

export type EnrolledCapabilityStatus = 'NotStarted' | 'InProgress' | 'Completed';

export interface EnrolledProgramCapability {
  capabilityId: string;
  name: string;
  sortOrder: number;
  isRequired: boolean;
  status: EnrolledCapabilityStatus;
  totalNodes: number;
  masteredNodes: number;
  /** Average FinalScore (0-100) across every node mastered so far in this
   * capability. Null (not omitted) while none are mastered yet — the
   * backend serializes a nullable int, so always check for `null` here,
   * not just `undefined`. */
  averageScore?: number | null;
}

export interface EnrolledProgram {
  programId: string;
  code: string;
  name: string;
  description?: string | null;
  acceptedDate: string;
  /** Average FinalScore (0-100) across every node mastered so far, across
   * ALL of this program's capabilities — "how am I doing up to today".
   * Null (not omitted) while nothing has been mastered yet. */
  averageScore?: number | null;
  capabilities: EnrolledProgramCapability[];
}

/**
 * Client-side cache for GET /people/{personId}/enrolled-programs
 * (2026-07-26). This endpoint is genuinely expensive on the backend
 * (GrowthPlanService.GetEnrolledProgramsAsync does an N+1: one full
 * GraphProgressionEngine.GetFullGraphAsync — itself 5 DB round-trips —
 * PER capability in EVERY enrolled program) and gets called from FIVE
 * different pages/hooks (MyProgramsPage, ProgramDetailPage,
 * ProgramCatalogPage, TodayPage, useBrainGraph), so without a cache the
 * same slow query re-runs every time the user navigates between them.
 * Cached in-memory (survives route changes) AND in sessionStorage
 * (survives a page refresh, cleared when the tab closes) keyed by
 * personId, with a short TTL — progress data does change as the student
 * completes lessons, so this is deliberately short-lived and always
 * revalidated in the background by callers (see MyProgramsPage).
 */
const ENROLLED_PROGRAMS_CACHE_TTL_MS = 3 * 60 * 1000; // 3 minutes

interface EnrolledProgramsCacheEntry {
  data: EnrolledProgram[];
  timestamp: number;
}

const memoryCache = new Map<string, EnrolledProgramsCacheEntry>();

function sessionCacheKey(personId: string): string {
  return `humanos.enrolledPrograms.v1.${personId}`;
}

function readSessionCache(personId: string): EnrolledProgramsCacheEntry | null {
  try {
    const raw = sessionStorage.getItem(sessionCacheKey(personId));
    return raw ? (JSON.parse(raw) as EnrolledProgramsCacheEntry) : null;
  } catch {
    return null; // Storage disabled/unavailable (e.g. private browsing) — memory cache still works.
  }
}

function writeCache(personId: string, data: EnrolledProgram[]): void {
  const entry: EnrolledProgramsCacheEntry = { data, timestamp: Date.now() };
  memoryCache.set(personId, entry);
  try {
    sessionStorage.setItem(sessionCacheKey(personId), JSON.stringify(entry));
  } catch {
    // Storage full/unavailable — memory cache for this tab session still works.
  }
}

function isFresh(entry: EnrolledProgramsCacheEntry | null | undefined): entry is EnrolledProgramsCacheEntry {
  return !!entry && Date.now() - entry.timestamp < ENROLLED_PROGRAMS_CACHE_TTL_MS;
}

/** Synchronous read of whatever data is cached for this person, REGARDLESS
 * of age — lets a page render instantly with last-known data (stale-while-
 * revalidate) instead of showing a blank loading state on every visit.
 * Returns null only when nothing has ever been cached for this person in
 * this tab session. */
export function peekEnrolledProgramsCache(personId: string): EnrolledProgram[] | null {
  const cached = memoryCache.get(personId) ?? readSessionCache(personId) ?? null;
  if (cached && !memoryCache.has(personId)) {
    memoryCache.set(personId, cached); // Promote sessionStorage hit into the faster in-memory cache.
  }
  return cached?.data ?? null;
}

export function getEnrolledPrograms(
  personId: string,
  options: { forceRefresh?: boolean } = {},
): Promise<EnrolledProgram[]> {
  const cached = memoryCache.get(personId) ?? readSessionCache(personId);
  if (!options.forceRefresh && isFresh(cached)) {
    return Promise.resolve(cached.data);
  }

  return apiGet<EnrolledProgram[]>(`/people/${personId}/enrolled-programs`).then((data) => {
    writeCache(personId, data);
    return data;
  });
}

/** Call after any mutation that changes enrollment (enroll/unenroll) so the
 * next read fetches fresh data instead of serving a stale cache. */
export function invalidateEnrolledProgramsCache(personId?: string): void {
  if (personId) {
    memoryCache.delete(personId);
    try {
      sessionStorage.removeItem(sessionCacheKey(personId));
    } catch {
      // ignore
    }
  } else {
    memoryCache.clear();
  }
}

/**
 * "Inscribirme" (2026-07-25) — client for the Programs catalog's direct
 * enrollment endpoints. Mirrors enrolledCapabilitiesApi.ts's
 * enrollCapability/unenrollCapability. Deliberately separate from the
 * pre-existing Growth Plan Starting Point recommendation-acceptance path
 * — this is the easier, direct "browse and enroll" flow (see
 * ProgramEnrollmentService.EnrollAsync on the backend). Both paths are
 * merged server-side so `getEnrolledPrograms` above always reflects
 * either kind of enrollment.
 */
export interface ProgramEnrollmentAction {
  programId: string;
  enrolled: boolean;
  enrolledDate?: string | null;
}

export async function enrollProgram(personId: string, programId: string): Promise<ProgramEnrollmentAction> {
  const result = await apiPost<ProgramEnrollmentAction>(`/people/${personId}/programs/${programId}/enroll`);
  invalidateEnrolledProgramsCache(personId);
  return result;
}

export async function unenrollProgram(personId: string, programId: string): Promise<ProgramEnrollmentAction> {
  const result = await apiDelete<ProgramEnrollmentAction>(`/people/${personId}/programs/${programId}/enroll`);
  invalidateEnrolledProgramsCache(personId);
  return result;
}

