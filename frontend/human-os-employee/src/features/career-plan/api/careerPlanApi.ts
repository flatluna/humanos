import { apiGet } from '@/features/capabilities/api/httpClient';

/**
 * "Mi Plan de Capacitación" — employee-facing client for the AI career
 * development plan endpoint, ported from human-os-enterprise's
 * orgStructureApi.ts (its HR-facing "Rutas de Carrera" tab uses the same
 * endpoint, scoped by an arbitrary personId). Here it's always called
 * with the SIGNED-IN employee's own personId (see CareerPlanPage.tsx) —
 * there is no HR-style "view anyone's plan" capability in this app.
 *
 * BUG FIX (2026-08-01): this was originally written assuming PascalCase
 * response fields, based on an outdated doc comment in httpClient.ts. In
 * reality `FunctionResponseFactory.cs` (backend) serializes ALL responses
 * with `JsonSerializerDefaults.Web` (camelCase) — confirmed via a live
 * curl against GET /people/{id}/career-plan returning `{"hasPlan":...,
 * "jobRoleTitle":...}`, matching this app's OWN capabilitiesApi.ts /
 * enrolledCapabilitiesApi.ts, which are already camelCase and work fine.
 * Every field below was renamed to camelCase to match reality.
 */
export interface CareerPlanRoadmapStepDto {
  order: number;
  skill: string;
  title: string;
  description: string;
  matchedProgramId: string | null;
  matchedProgramName: string | null;
  matchedCapabilityId: string | null;
  matchedCapabilityName: string | null;
  /** "es"/"en" — see PersonCareerPlanService.ApplyTrainingStatusAsync. Null when no course is matched yet. */
  language?: string | null;
  /** "Completed" | "InProgress" | "Pending" */
  trainingStatus: string;
}

export interface PersonCareerPlanDto {
  personId: string;
  hasPlan: boolean;
  jobRoleId: string | null;
  jobRoleTitle: string | null;
  gapSkills: string[];
  technicalRoadmap: CareerPlanRoadmapStepDto[];
  softSkillsRoadmap: CareerPlanRoadmapStepDto[];
  departmentProcessesRoadmap: CareerPlanRoadmapStepDto[];
  hrProcessesRoadmap: CareerPlanRoadmapStepDto[];
  overallRationale: string | null;
  generatedDate: string | null;
  isAccepted: boolean;
  acceptedDate: string | null;
}

export async function getMyCareerPlan(personId: string): Promise<PersonCareerPlanDto> {
  return apiGet<PersonCareerPlanDto>(`/people/${personId}/career-plan`);
}
