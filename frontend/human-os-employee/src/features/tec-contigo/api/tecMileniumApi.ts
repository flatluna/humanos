import { apiPost } from "@/features/capabilities/api/httpClient";

export interface TecMileniumCatalogCourseDto {
  catalogItemId: string;
  catalogType: string;
  name: string;
  reason: string;
}

export interface TecMileniumExternalCourseDto {
  name: string;
  provider: string;
  reason: string;
  url?: string | null;
}

export interface TecMileniumPlanDto {
  title: string;
  overallSummary: string;
  catalogCourses: TecMileniumCatalogCourseDto[];
  externalCourses: TecMileniumExternalCourseDto[];
}

export function generateTecMileniumPlan(
  personId: string,
  prompt?: string,
): Promise<TecMileniumPlanDto> {
  return apiPost<TecMileniumPlanDto>(`/people/${personId}/tec-milenium/plan`, {
    prompt,
    languageCode: "es",
  });
}

export function acceptTecMileniumCatalogCourse(
  personId: string,
  catalogItemId: string,
  catalogType: string,
): Promise<unknown> {
  return apiPost(`/people/${personId}/tec-milenium/plan/accept-catalog-course`, {
    catalogItemId,
    catalogType,
  });
}

export function requestTecMileniumExternalCourse(
  personId: string,
  courses: TecMileniumExternalCourseDto[],
): Promise<unknown> {
  return apiPost(`/people/${personId}/tec-milenium/plan/request-external-course`, {
    courses: courses.map((course) => ({
      courseName: course.name,
      provider: course.provider,
      reason: course.reason,
      url: course.url,
    })),
  });
}
