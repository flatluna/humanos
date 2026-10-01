import { apiGet } from '@/features/capabilities/api/httpClient';

export interface CrossCourseEntityOccurrence {
  courseId: string;
  courseName: string;
  engramId: string;
  engramTitle: string;
  chapterTitle?: string | null;
  sortOrder: number;
  sourceText: string;
  startPosition?: number | null;
  endPosition?: number | null;
}

export interface CrossCourseEntityItem {
  entityId: string;
  canonicalName: string;
  entityType: string;
  description?: string | null;
  ontologicalClass?: string | null;
  aliases: string[];
  totalOccurrences: number;
  distinctCoursesCount: number;
  occurrences: CrossCourseEntityOccurrence[];
}

export interface CrossCourseCourseSummary {
  courseId: string;
  courseName: string;
  engramCount: number;
  entityCount: number;
}

export interface CrossCourseEntitiesResponse {
  totalCourses: number;
  totalEngrams: number;
  totalEntities: number;
  totalOccurrences: number;
  courses: CrossCourseCourseSummary[];
  entities: CrossCourseEntityItem[];
}

/** GET /people/{personId}/cross-course-entities?courseIds=id1,id2,... —
 * unlike getProgramEntities, the course set is chosen freely by the user
 * from their own enrolled courses, not tied to a curated Program. */
export async function getCrossCourseEntities(
  personId: string,
  courseIds: string[],
): Promise<CrossCourseEntitiesResponse> {
  const query = courseIds.length > 0 ? `?courseIds=${courseIds.map(encodeURIComponent).join(',')}` : '';
  return apiGet<CrossCourseEntitiesResponse>(`/people/${personId}/cross-course-entities${query}`);
}
