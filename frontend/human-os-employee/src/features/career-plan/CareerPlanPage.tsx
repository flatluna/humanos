import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link, useLocation } from 'react-router';
import {
  Activity,
  Bot,
  Briefcase,
  Building2,
  Calculator,
  CheckCircle2,
  Clock,
  Clock3,
  Compass,
  FileText,
  GraduationCap,
  Hammer,
  Hourglass,
  LayoutDashboard,
  MapPin,
  Pencil,
  PlayCircle,
  Plus,
  ShieldCheck,
  Sparkles,
  Target,
  Trash2,
  TrendingUp,
  Wrench,
  X,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { getMyCareerPlan, type CareerPlanRoadmapStepDto } from './api/careerPlanApi';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getJobRole } from '@/features/profile/api/jobRoleApi';
import { getSkills } from '@/features/capabilities/api/skillsApi';
import {
  getAvailableDepartmentObjectives,
  getAllCorporateDepartmentObjectives,
  getEmployeeObjectives,
  saveEmployeeObjectives,
  recommendSmartObjective,
  type EmployeeObjective,
} from '@/features/objectives/api/employeeObjectivesApi';

// Program matches point directly to /programs/:programId within this employee portal.

type Track = 'technical' | 'soft' | 'department' | 'hr';
type RoleSubTab = 'role' | 'objectives';

interface RoadmapCourse extends CareerPlanRoadmapStepDto {
  track: Track;
}

const CATEGORY_TABS = [
  { id: 'job-skills', label: 'Mi Puesto', icon: Briefcase, accent: 'sky' as const },
  { id: 'department-processes', label: 'Procesos de Depto.', icon: Building2, accent: 'slate' as const },
  { id: 'hr-processes', label: 'Procesos de RRHH', icon: ShieldCheck, accent: 'slate' as const },
] as const;

type CategoryTabId = (typeof CATEGORY_TABS)[number]['id'];

function isCategoryTabId(value: string | undefined): value is CategoryTabId {
  return CATEGORY_TABS.some((tab) => tab.id === value);
}

const STATUS_FILTERS = [
  { id: 'all', label: 'Todos' },
  { id: 'Pending', label: 'Pendiente' },
  { id: 'InProgress', label: 'En progreso' },
  { id: 'Completed', label: 'Completado' },
] as const;

type StatusFilterId = (typeof STATUS_FILTERS)[number]['id'];

const OBJECTIVE_STATUS_META: Record<
  EmployeeObjective['status'],
  { labelEs: string; labelEn: string; className: string }
> = {
  Active: {
    labelEs: 'Activo',
    labelEn: 'Active',
    className: 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300',
  },
  InProgress: {
    labelEs: 'En Progreso',
    labelEn: 'In Progress',
    className: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  },
  Completed: {
    labelEs: 'Completado',
    labelEn: 'Completed',
    className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  },
  Paused: {
    labelEs: 'Pausado',
    labelEn: 'Paused',
    className: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/50',
  },
};

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
}

/** "Mi Plan de Capacitación" — the employee's own AI-generated career
 * development roadmap (technical + soft-skill gaps for their job role),
 * with real progress against each step. Ported from human-os-enterprise's
 * `EmployeeCareerPlanPage.tsx` (the HR-facing view of any employee's plan)
 * but scoped to ONLY the signed-in employee's own plan — no personId
 * route param, no "generate a course" action (that's an HR/Capability
 * Studio action, out of scope here), no navigation to other employees'
 * data (see /memories/repo for the privacy rule this mirrors on the HR
 * side: HR can only open this detail view for the demo account). */
export function CareerPlanPage() {
  const { user } = useAuth();
  const { t, language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';
  const tenantId = user?.tenantId ?? '';
  const departmentId = user?.departmentId ?? '';
  const location = useLocation();
  const [statusFilter, setStatusFilter] = useState<StatusFilterId>('all');
  const [roleSubTab, setRoleSubTab] = useState<RoleSubTab>('role');

  const categoryTabs = useMemo(() => [
    { id: 'job-skills' as const, label: language === 'en' ? 'My Role' : 'Mi Puesto', icon: Briefcase, accent: 'sky' as const },
    { id: 'department-processes' as const, label: language === 'en' ? 'Department Processes' : 'Procesos de Depto.', icon: Building2, accent: 'slate' as const },
    { id: 'hr-processes' as const, label: language === 'en' ? 'HR Processes' : 'Procesos de RRHH', icon: ShieldCheck, accent: 'slate' as const },
  ], [language]);

  const statusFilters = useMemo(() => [
    { id: 'all' as const, label: t.careerFilterAll },
    { id: 'Pending' as const, label: t.careerFilterPending },
    { id: 'InProgress' as const, label: t.careerFilterInProgress },
    { id: 'Completed' as const, label: t.careerFilterCompleted },
  ], [t]);

  const statusMeta: Record<string, { label: string; className: string }> = useMemo(() => ({
    Completed: {
      label: t.careerFilterCompleted,
      className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
    },
    InProgress: {
      label: t.careerFilterInProgress,
      className: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
    },
    Pending: {
      label: t.careerFilterPending,
      className: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/50',
    },
  }), [t]);

  const pathSegment = location.pathname.split('/').filter(Boolean).pop();
  const activeTab: CategoryTabId = isCategoryTabId(pathSegment) ? pathSegment : 'job-skills';

  const planQuery = useQuery({
    queryKey: ['my-career-plan', personId],
    queryFn: () => getMyCareerPlan(personId),
    enabled: !!personId,
  });

  const plan = planQuery.data;

  // Job Role Query to fetch full database role specifications
  const effectiveJobRoleId = user?.jobRoleId || plan?.jobRoleId || '';
  const jobRoleQuery = useQuery({
    queryKey: ['my-job-role-full', effectiveJobRoleId],
    queryFn: () => getJobRole(effectiveJobRoleId),
    enabled: Boolean(effectiveJobRoleId),
  });
  const jobRole = jobRoleQuery.data;

  // Skills Query to check all catalog courses (Structured Learning and Graph)
  const skillsQuery = useQuery({
    queryKey: ['skills', tenantId],
    queryFn: () => getSkills(tenantId),
    enabled: Boolean(tenantId),
  });

  const departmentName = user?.departmentName ?? '';
  const jobRoleTitle = user?.jobRoleTitle ?? jobRole?.title ?? '';

  // Available Department Objectives for alignment
  const departmentObjectives = useMemo(() => {
    return getAvailableDepartmentObjectives(tenantId, departmentId, isEn, departmentName, jobRoleTitle);
  }, [tenantId, departmentId, isEn, departmentName, jobRoleTitle]);

  const groupedDeptObjectives = useMemo(() => {
    return getAllCorporateDepartmentObjectives(tenantId, isEn, departmentId, departmentName, jobRoleTitle);
  }, [tenantId, isEn, departmentId, departmentName, jobRoleTitle]);

  const allCorporateObjectives = useMemo(() => {
    return groupedDeptObjectives.flatMap((g) => g.objectives);
  }, [groupedDeptObjectives]);

  // Personal SMART Objectives
  const [objectives, setObjectives] = useState<EmployeeObjective[]>(() => {
    return getEmployeeObjectives(tenantId, personId, isEn, departmentName, jobRoleTitle);
  });

  // Re-sync default objectives once user profile details load
  useEffect(() => {
    if (personId) {
      setObjectives(getEmployeeObjectives(tenantId, personId, isEn, departmentName, jobRoleTitle));
    }
  }, [tenantId, personId, isEn, departmentName, jobRoleTitle]);

  function updateObjectives(next: EmployeeObjective[]) {
    setObjectives(next);
    saveEmployeeObjectives(tenantId, personId, next);
  }

  // Objective Modal State
  const [modalOpen, setModalOpen] = useState(false);
  const [editingObjective, setEditingObjective] = useState<EmployeeObjective | null>(null);

  const [formTitle, setFormTitle] = useState('');
  const [formDesc, setFormDesc] = useState('');
  const [formQuarter, setFormQuarter] = useState('Q4 2026');
  const [formDate, setFormDate] = useState('2026-12-31');
  const [formProgress, setFormProgress] = useState(0);
  const [formStatus, setFormStatus] = useState<EmployeeObjective['status']>('Active');
  const [formDeptObjId, setFormDeptObjId] = useState('');
  const [formMetricName, setFormMetricName] = useState('');
  const [formCurrentVal, setFormCurrentVal] = useState('');
  const [formTargetVal, setFormTargetVal] = useState('');
  const [formUnit, setFormUnit] = useState('%');
  const [formMeasurementMethod, setFormMeasurementMethod] = useState('');
  const [formActionPlan, setFormActionPlan] = useState('');
  const [formBusinessImpact, setFormBusinessImpact] = useState('');
  const [formRecommendedTechSkills, setFormRecommendedTechSkills] = useState<string[]>([]);
  const [formRecommendedSoftSkills, setFormRecommendedSoftSkills] = useState<string[]>([]);
  const [formReasoning, setFormReasoning] = useState<string>('');
  const [isAiRecommending, setIsAiRecommending] = useState(false);

  function openCreateModal() {
    setEditingObjective(null);
    setFormTitle('');
    setFormDesc('');
    setFormQuarter('Q4 2026');
    setFormDate('2026-12-31');
    setFormProgress(0);
    setFormStatus('Active');
    setFormDeptObjId(departmentObjectives[0]?.id ?? '');
    setFormMetricName('');
    setFormCurrentVal('');
    setFormTargetVal('');
    setFormUnit('%');
    setFormMeasurementMethod('');
    setFormActionPlan('');
    setFormBusinessImpact('');
    setFormRecommendedTechSkills([]);
    setFormRecommendedSoftSkills([]);
    setFormReasoning('');
    setModalOpen(true);
  }

  function openEditModal(obj: EmployeeObjective) {
    setEditingObjective(obj);
    setFormTitle(obj.title);
    setFormDesc(obj.description);
    setFormQuarter(obj.targetQuarter);
    setFormDate(obj.targetDate);
    setFormProgress(obj.progressPercentage);
    setFormStatus(obj.status);
    setFormDeptObjId(obj.departmentObjectiveId ?? '');
    setFormMetricName(obj.metricName ?? '');
    setFormCurrentVal(obj.currentValue != null ? String(obj.currentValue) : '');
    setFormTargetVal(obj.targetValue != null ? String(obj.targetValue) : '');
    setFormUnit(obj.unit ?? '%');
    setFormMeasurementMethod(obj.measurementMethod ?? '');
    setFormActionPlan(obj.actionPlan ?? '');
    setFormBusinessImpact(obj.businessImpact ?? '');
    setFormRecommendedTechSkills(obj.recommendedTechnicalSkills ?? []);
    setFormRecommendedSoftSkills(obj.recommendedSoftSkills ?? []);
    setFormReasoning(obj.recommendationReasoning ?? '');
    setModalOpen(true);
  }

  function autoCalculateProgress() {
    const cur = parseFloat(formCurrentVal);
    const tgt = parseFloat(formTargetVal);
    if (!isNaN(cur) && !isNaN(tgt) && tgt !== 0) {
      const pct = Math.min(100, Math.max(0, Math.round((cur / tgt) * 100)));
      setFormProgress(pct);
    }
  }

  async function handleAiRecommend() {
    const selectedDeptObj =
      allCorporateObjectives.find((d) => d.id === formDeptObjId) ||
      departmentObjectives.find((d) => d.id === formDeptObjId) ||
      departmentObjectives[0];
    setIsAiRecommending(true);
    try {
      const rec = await recommendSmartObjective({
        departmentObjectiveTitle: selectedDeptObj?.title,
        departmentObjectiveDescription: selectedDeptObj?.description,
        departmentObjectiveTargetQuarter: selectedDeptObj?.targetQuarter,
        departmentObjectiveTargetDate: selectedDeptObj?.targetDate,
        strategicGoalTitle: selectedDeptObj?.strategicGoalTitle ?? undefined,
        pillarTitle: selectedDeptObj?.pillarTitle ?? undefined,
        departmentMetricName: selectedDeptObj?.metricName ?? undefined,
        departmentTargetValue: selectedDeptObj?.targetValue ?? undefined,
        departmentUnit: selectedDeptObj?.unit ?? undefined,
        employeeName: user?.name,
        jobRoleTitle: jobRole?.title || user?.jobRoleTitle,
        jobRoleLevel: jobRole?.levelLabel ?? undefined,
        jobRolePurpose: jobRole?.purpose ?? undefined,
        jobRoleSummary: jobRole?.summary ?? undefined,
        jobRoleTechnicalSkills: jobRole?.technicalSkills || [],
        jobRoleSoftSkills: jobRole?.softSkills || [],
        language,
      });

      setFormTitle(rec.title);
      setFormDesc(rec.description);
      setFormMetricName(rec.metricName);
      setFormCurrentVal(String(rec.currentValue ?? 0));
      setFormTargetVal(String(rec.targetValue ?? 100));
      setFormUnit(rec.unit || '%');
      setFormMeasurementMethod(rec.measurementMethod);
      setFormActionPlan(rec.actionPlan);
      setFormBusinessImpact(rec.businessImpact);
      if (rec.targetQuarter) setFormQuarter(rec.targetQuarter);
      if (rec.targetDate) setFormDate(rec.targetDate);
      setFormRecommendedTechSkills(rec.recommendedTechnicalSkills || []);
      setFormRecommendedSoftSkills(rec.recommendedSoftSkills || []);
      setFormReasoning(rec.recommendationReasoning || '');
      if (rec.currentValue != null && rec.targetValue != null && rec.targetValue > 0) {
        setFormProgress(Math.min(100, Math.max(0, Math.round((rec.currentValue / rec.targetValue) * 100))));
      }
    } catch (err) {
      console.error('Error generating AI recommendation:', err);
      alert(isEn ? 'Could not generate AI suggestion. Please check backend connection.' : 'No se pudo generar la sugerencia con IA. Verifica la conexión con el backend.');
    } finally {
      setIsAiRecommending(false);
    }
  }

  function handleObjectiveSubmit(e: FormEvent) {
    e.preventDefault();
    if (!formTitle.trim()) return;

    const selectedDeptObj =
      allCorporateObjectives.find((d) => d.id === formDeptObjId) ||
      departmentObjectives.find((d) => d.id === formDeptObjId);
    const curValNum = formCurrentVal.trim() !== '' && !isNaN(Number(formCurrentVal)) ? Number(formCurrentVal) : null;
    const tgtValNum = formTargetVal.trim() !== '' && !isNaN(Number(formTargetVal)) ? Number(formTargetVal) : null;

    if (editingObjective) {
      const next = objectives.map((o) =>
        o.id === editingObjective.id
          ? {
              ...o,
              title: formTitle.trim(),
              description: formDesc.trim(),
              targetQuarter: formQuarter,
              targetDate: formDate,
              progressPercentage: Number(formProgress),
              status: formStatus,
              departmentObjectiveId: selectedDeptObj ? selectedDeptObj.id : null,
              departmentObjectiveTitle: selectedDeptObj ? selectedDeptObj.title : null,
              departmentName: selectedDeptObj?.departmentName || user?.departmentName || 'Legal',
              strategicGoalTitle: selectedDeptObj ? selectedDeptObj.strategicGoalTitle : null,
              metricName: formMetricName.trim() || null,
              currentValue: curValNum,
              targetValue: tgtValNum,
              unit: formUnit.trim() || null,
              measurementMethod: formMeasurementMethod.trim() || null,
              actionPlan: formActionPlan.trim() || null,
              businessImpact: formBusinessImpact.trim() || null,
              recommendedTechnicalSkills: formRecommendedTechSkills.length > 0 ? formRecommendedTechSkills : null,
              recommendedSoftSkills: formRecommendedSoftSkills.length > 0 ? formRecommendedSoftSkills : null,
              recommendationReasoning: formReasoning || null,
            }
          : o,
      );
      updateObjectives(next);
    } else {
      const newObj: EmployeeObjective = {
        id: `emp-obj-${Date.now()}`,
        personId,
        tenantId,
        title: formTitle.trim(),
        description: formDesc.trim(),
        targetQuarter: formQuarter,
        targetDate: formDate,
        progressPercentage: Number(formProgress),
        status: formStatus,
        departmentObjectiveId: selectedDeptObj ? selectedDeptObj.id : null,
        departmentObjectiveTitle: selectedDeptObj ? selectedDeptObj.title : null,
        departmentName: selectedDeptObj?.departmentName || user?.departmentName || 'Legal',
        strategicGoalTitle: selectedDeptObj ? selectedDeptObj.strategicGoalTitle : null,
        metricName: formMetricName.trim() || null,
        currentValue: curValNum,
        targetValue: tgtValNum,
        unit: formUnit.trim() || null,
        measurementMethod: formMeasurementMethod.trim() || null,
        actionPlan: formActionPlan.trim() || null,
        businessImpact: formBusinessImpact.trim() || null,
        recommendedTechnicalSkills: formRecommendedTechSkills.length > 0 ? formRecommendedTechSkills : null,
        recommendedSoftSkills: formRecommendedSoftSkills.length > 0 ? formRecommendedSoftSkills : null,
        recommendationReasoning: formReasoning || null,
      };
      updateObjectives([newObj, ...objectives]);
    }
    setModalOpen(false);
  }

  function handleDeleteObjective(id: string, title: string) {
    const confirmMsg = isEn ? `Delete objective "${title}"?` : `¿Eliminar el objetivo "${title}"?`;
    if (window.confirm(confirmMsg)) {
      updateObjectives(objectives.filter((o) => o.id !== id));
    }
  }

  const allCourses: RoadmapCourse[] = useMemo(() => {
    if (!plan?.hasPlan) return [];
    return [
      ...plan.technicalRoadmap.map((step) => ({ ...step, track: 'technical' as const })),
      ...plan.softSkillsRoadmap.map((step) => ({ ...step, track: 'soft' as const })),
      ...plan.departmentProcessesRoadmap.map((step) => ({ ...step, track: 'department' as const })),
      ...plan.hrProcessesRoadmap.map((step) => ({ ...step, track: 'hr' as const })),
    ];
  }, [plan]);

  function resolveCourseForSkill(skillName: string) {
    const clean = (s: string) =>
      s
        .toLowerCase()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .replace(/emgineering/g, 'engineering')
        .replace(/[^a-z0-9]/g, ' ')
        .replace(/\s+/g, ' ')
        .trim();

    const norm = clean(skillName);

    // 1. Check allCourses from Career Plan
    const planMatch = allCourses.find((c) => {
      const cSkillNorm = clean(c.skill);
      const cTitleNorm = clean(c.title);
      return (
        cSkillNorm === norm ||
        cTitleNorm === norm ||
        (norm.length > 3 && (cSkillNorm.includes(norm) || norm.includes(cSkillNorm))) ||
        (norm.length > 3 && (cTitleNorm.includes(norm) || norm.includes(cTitleNorm)))
      );
    });

    if (planMatch && (planMatch.matchedCapabilityId || planMatch.matchedProgramId)) {
      return {
        skill: planMatch.skill,
        title: planMatch.title,
        description: planMatch.description,
        matchedProgramId: planMatch.matchedProgramId,
        matchedProgramName: planMatch.matchedProgramName,
        matchedCapabilityId: planMatch.matchedCapabilityId,
        matchedCapabilityName: planMatch.matchedCapabilityName,
        matchedCourseType: 'structured-learning' as const,
        trainingStatus: planMatch.trainingStatus || 'Pending',
        hasCourse: true,
      };
    }

    // 2. Check catalog skills
    const catalogSkill = (skillsQuery.data ?? []).find((s) => {
      const sNorm = clean(s.name);
      return (
        sNorm === norm ||
        (norm.length > 3 && (sNorm.includes(norm) || norm.includes(sNorm)))
      );
    });

    if (catalogSkill?.matchedCapabilityId) {
      return {
        skill: catalogSkill.name,
        title: catalogSkill.matchedCapabilityName || catalogSkill.name,
        description: catalogSkill.description || planMatch?.description || '',
        matchedProgramId: null,
        matchedProgramName: null,
        matchedCapabilityId: catalogSkill.matchedCapabilityId,
        matchedCapabilityName: catalogSkill.matchedCapabilityName,
        matchedCourseType: catalogSkill.matchedCourseType || 'structured-learning',
        trainingStatus: planMatch?.trainingStatus || 'Available',
        hasCourse: true,
      };
    }

    return {
      skill: skillName,
      title: planMatch?.title || skillName,
      description: planMatch?.description || '',
      matchedProgramId: null,
      matchedProgramName: null,
      matchedCapabilityId: null,
      matchedCapabilityName: null,
      matchedCourseType: null,
      trainingStatus: planMatch?.trainingStatus || 'Pending',
      hasCourse: false,
    };
  }

  const completed = allCourses.filter((c) => c.trainingStatus === 'Completed');
  const inProgress = allCourses.filter((c) => c.trainingStatus === 'InProgress');
  const pending = allCourses.filter((c) => c.trainingStatus === 'Pending');

  const jobDescriptionCourses = useMemo(
    () => allCourses.filter((c) => c.track === 'technical' || c.track === 'soft'),
    [allCourses],
  );
  const departmentProcessCourses = useMemo(
    () => allCourses.filter((c) => c.track === 'department'),
    [allCourses],
  );
  const hrProcessCourses = useMemo(
    () => allCourses.filter((c) => c.track === 'hr'),
    [allCourses],
  );

  const total = allCourses.length;
  const completionRate = total === 0 ? 0 : Math.round((completed.length / total) * 100);

  const coursesByTab: Record<CategoryTabId, RoadmapCourse[]> = {
    'job-skills': jobDescriptionCourses,
    'department-processes': departmentProcessCourses,
    'hr-processes': hrProcessCourses,
  };

  const emptyMessageByTab: Record<CategoryTabId, string> = {
    'job-skills': t.careerNoCoursesJob,
    'department-processes': t.careerNoCoursesDept,
    'hr-processes': t.careerNoCoursesHr,
  };

  const activeCourses = coursesByTab[activeTab];
  const filteredCourses =
    statusFilter === 'all' ? activeCourses : activeCourses.filter((c) => c.trainingStatus === statusFilter);

  const inputClass =
    'mt-1 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
  const labelClass = 'block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-white/50';

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-4 pb-16 sm:p-8">
      {/* Gradient header banner — mirrors the HR-facing dashboard at
       * EmployeeCareerPlanPage.tsx (human-os-enterprise) 1:1, per explicit
       * user request ("ese banner no lo veo aquí"). */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-indigo-600 via-indigo-500 to-violet-600 p-6 text-white shadow-lg shadow-indigo-500/20 dark:border-white/10 sm:p-8">
        <div className="flex flex-col gap-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 text-xl font-bold ring-1 ring-white/25 backdrop-blur">
              {initialsOf(user?.name ?? 'Empleado')}
            </span>
            <div>
              <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/70">
                <LayoutDashboard className="h-3.5 w-3.5" />
                {t.careerDashboardTitle}
              </p>
              <h1 className="mt-1 text-2xl font-semibold">{user?.name ?? 'Empleado'}</h1>
              <div className="mt-1 flex flex-wrap items-center gap-3">
                <p className="text-sm text-white/80">
                  {plan?.jobRoleTitle ?? t.profileUnassignedJob}
                </p>
                <Link
                  to="/objectives"
                  className="inline-flex items-center gap-1.5 rounded-full bg-white/15 px-3 py-1 text-xs font-semibold text-white ring-1 ring-white/30 transition hover:bg-white/25"
                >
                  <Target className="h-3.5 w-3.5 text-indigo-200" />
                  {language === 'en' ? 'Role & SMART Objectives →' : 'Mi Puesto & Objetivos SMART →'}
                </Link>
              </div>
            </div>
          </div>

          {total > 0 && (
            <div className="flex items-center gap-4 rounded-2xl bg-white/10 px-5 py-4 ring-1 ring-white/20 backdrop-blur">
              <div className="relative flex h-16 w-16 shrink-0 items-center justify-center">
                <svg viewBox="0 0 36 36" className="h-16 w-16 -rotate-90">
                  <circle cx="18" cy="18" r="15.5" fill="none" stroke="rgba(255,255,255,0.2)" strokeWidth="3" />
                  <circle
                    cx="18"
                    cy="18"
                    r="15.5"
                    fill="none"
                    stroke="white"
                    strokeWidth="3"
                    strokeDasharray={`${completionRate * 0.974} 1000`}
                    strokeLinecap="round"
                  />
                </svg>
                <span className="absolute text-sm font-bold">{completionRate}%</span>
              </div>
              <div className="text-sm">
                <p className="font-semibold">{t.careerOverallProgress}</p>
                <p className="text-white/70">
                  {completed.length} {language === 'en' ? 'of' : 'de'} {total} {t.careerTrainingsCompleted}
                </p>
              </div>
            </div>
          )}
        </div>
      </div>

      {planQuery.isLoading && (
        <LoadingBackdrop title={t.careerLoadingPlan} description={t.careerLoadingPlanDesc} />
      )}

      {planQuery.isSuccess && !plan?.hasPlan && (
        <Card className="flex flex-col items-center gap-3 border-dashed p-10 text-center">
          <span className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-100 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
            <GraduationCap className="h-6 w-6" />
          </span>
          <p className="text-sm text-slate-500 dark:text-white/40">
            {t.careerNoPlan}
          </p>
        </Card>
      )}

      {plan?.hasPlan && (
        <>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <StatCard label={t.careerStatTotal} value={total} icon={LayoutDashboard} accent="slate" />
            <StatCard label={t.careerStatTaken} value={completed.length} icon={CheckCircle2} accent="brand" />
            <StatCard label={t.careerStatInProgress} value={inProgress.length} icon={Clock3} accent="amber" />
            <StatCard label={t.careerStatPending} value={pending.length} icon={Hourglass} accent="rose" />
          </div>

          {plan.overallRationale && (
            <p className="rounded-2xl border border-brand-200 bg-brand-50/60 p-4 text-sm text-brand-900 dark:border-brand-500/20 dark:bg-brand-500/10 dark:text-brand-200">
              {plan.overallRationale}
            </p>
          )}

          {/* Category Tabs */}
          <div className="flex flex-wrap items-center gap-1.5 rounded-2xl border border-slate-200 bg-white p-1.5 dark:border-white/10 dark:bg-white/5">
            {categoryTabs.map((tab) => {
              const TabIcon = tab.icon;
              const isActive = tab.id === activeTab;
              return (
                <Link
                  key={tab.id}
                  to={`/capabilities/${tab.id}`}
                  className={`flex items-center gap-1.5 rounded-xl px-3 py-1.5 text-xs font-semibold transition ${
                    isActive
                      ? ACCENT_STYLES[tab.accent].badge
                      : 'text-slate-500 hover:bg-slate-100 dark:text-white/50 dark:hover:bg-white/5'
                  }`}
                >
                  <TabIcon className="h-3.5 w-3.5" />
                  {tab.label}
                  {coursesByTab[tab.id].length > 0 && (
                    <span className="rounded-full bg-black/10 px-1.5 py-0.5 text-[10px] dark:bg-white/10">
                      {coursesByTab[tab.id].length}
                    </span>
                  )}
                </Link>
              );
            })}
          </div>

          {/* Sub-tabs when on 'job-skills' (Mi Puesto) */}
          {activeTab === 'job-skills' && (
            <div className="flex flex-wrap items-center gap-2 border-b border-slate-200 pb-3 dark:border-white/10">
              <button
                type="button"
                onClick={() => setRoleSubTab('role')}
                className={`flex items-center gap-2 rounded-xl px-4 py-2.5 text-xs font-semibold transition ${
                  roleSubTab === 'role'
                    ? 'bg-indigo-600 text-white shadow-sm'
                    : 'bg-white text-slate-600 hover:bg-slate-100 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10'
                }`}
              >
                <Briefcase className="h-4 w-4" />
                {isEn ? 'My Role & Required Classes' : 'Mi Puesto & Clases Requeridas'}
              </button>

              <button
                type="button"
                onClick={() => setRoleSubTab('objectives')}
                className={`flex items-center gap-2 rounded-xl px-4 py-2.5 text-xs font-semibold transition ${
                  roleSubTab === 'objectives'
                    ? 'bg-indigo-600 text-white shadow-sm'
                    : 'bg-white text-slate-600 hover:bg-slate-100 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10'
                }`}
              >
                <Target className="h-4 w-4" />
                {isEn ? `SMART Objectives (${objectives.length})` : `Objetivos SMART (${objectives.length})`}
              </button>
            </div>
          )}

          {/* SUB-VIEW 1: FULL ROLE DESCRIPTION & REQUIRED CLASSES (when activeTab === 'job-skills' && roleSubTab === 'role') */}
          {activeTab === 'job-skills' && roleSubTab === 'role' && (
            <div className="space-y-6">
              {jobRoleQuery.isLoading && (
                <LoadingBackdrop
                  title={isEn ? 'Loading Job Role...' : 'Cargando descripción de puesto...'}
                  description={isEn ? 'Retrieving position requirements and competencies.' : 'Obteniendo requisitos y competencias del puesto.'}
                />
              )}

              {jobRole && (
                <div className="space-y-6">
                  {/* Header Card */}
                  <Card className="p-6 sm:p-8">
                    <div className="flex flex-col sm:flex-row sm:items-start sm:justify-between gap-4">
                      <div className="space-y-2">
                        <div className="flex items-center gap-2">
                          <span className="rounded-full bg-indigo-50 px-3 py-1 text-xs font-bold text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300">
                            {jobRole.departmentName}
                          </span>
                          {jobRole.levelLabel && (
                            <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-700 dark:bg-white/10 dark:text-white/70">
                              {jobRole.levelLabel}
                            </span>
                          )}
                        </div>
                        <h2 className="text-2xl font-bold text-slate-900 dark:text-white">
                          {jobRole.title}
                        </h2>
                      </div>

                      <div className="flex flex-wrap gap-2 text-xs text-slate-500 dark:text-white/60">
                        {jobRole.workModality && (
                          <span className="flex items-center gap-1 rounded-lg border border-slate-200 px-2.5 py-1 dark:border-white/10">
                            <MapPin className="h-3.5 w-3.5" />
                            {jobRole.workModality}
                          </span>
                        )}
                        {jobRole.employmentType && (
                          <span className="flex items-center gap-1 rounded-lg border border-slate-200 px-2.5 py-1 dark:border-white/10">
                            <Briefcase className="h-3.5 w-3.5" />
                            {jobRole.employmentType}
                          </span>
                        )}
                        {jobRole.workSchedule && (
                          <span className="flex items-center gap-1 rounded-lg border border-slate-200 px-2.5 py-1 dark:border-white/10">
                            <Clock className="h-3.5 w-3.5" />
                            {jobRole.workSchedule}
                          </span>
                        )}
                      </div>
                    </div>

                    {jobRole.purpose && (
                      <div className="mt-6 rounded-2xl bg-slate-50 p-4 dark:bg-white/5">
                        <h4 className="text-xs font-bold uppercase tracking-wider text-slate-400">
                          {isEn ? 'Role Purpose' : 'Propósito Fundamental del Puesto'}
                        </h4>
                        <p className="mt-1 text-sm text-slate-800 dark:text-white/90 leading-relaxed font-medium">
                          {jobRole.purpose}
                        </p>
                      </div>
                    )}

                    {jobRole.summary && (
                      <div className="mt-4">
                        <h4 className="text-xs font-bold uppercase tracking-wider text-slate-400">
                          {isEn ? 'Summary' : 'Resumen General'}
                        </h4>
                        <p className="mt-1 text-xs text-slate-600 dark:text-white/70 leading-relaxed">
                          {jobRole.summary}
                        </p>
                      </div>
                    )}
                  </Card>

                  {/* Technical Skills & Linked Classes */}
                  <Card className="p-6 sm:p-8">
                    <div className="flex items-center justify-between mb-5">
                      <div className="flex items-center gap-2.5 text-sky-600 dark:text-sky-400">
                        <GraduationCap className="h-5 w-5" />
                        <div>
                          <h3 className="text-base font-bold text-slate-900 dark:text-white">
                            {isEn ? 'Technical Skills & Assigned Classes' : 'Habilidades Técnicas & Clases Asignadas'}
                          </h3>
                          <p className="text-xs text-slate-500 dark:text-white/50">
                            {isEn ? 'Core technical competencies for your role and their learning roadmaps.' : 'Competencias técnicas del puesto y sus cursos asignados.'}
                          </p>
                        </div>
                      </div>
                      <span className="rounded-full bg-sky-50 px-2.5 py-0.5 text-xs font-bold text-sky-700 dark:bg-sky-500/15 dark:text-sky-300">
                        {jobRole.technicalSkills.length} {isEn ? 'Skills' : 'Habilidades'}
                      </span>
                    </div>

                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      {jobRole.technicalSkills.map((sk, i) => {
                        const course = resolveCourseForSkill(sk);
                        return (
                          <div
                            key={i}
                            className="flex flex-col justify-between rounded-2xl border border-sky-100 bg-sky-50/20 p-4 transition hover:border-sky-300 dark:border-sky-500/20 dark:bg-sky-500/5"
                          >
                            <div>
                              <div className="flex items-start justify-between gap-2">
                                <span className="text-[10px] font-bold uppercase tracking-wider text-sky-700 dark:text-sky-300">
                                  #{i + 1} {isEn ? 'Technical Skill' : 'Habilidad Técnica'}
                                </span>
                                {course.hasCourse && (
                                  <span
                                    className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                                      statusMeta[course.trainingStatus]?.className ?? statusMeta.Pending.className
                                    }`}
                                  >
                                    {statusMeta[course.trainingStatus]?.label ?? course.trainingStatus}
                                  </span>
                                )}
                              </div>
                              <h4 className="mt-1 text-sm font-semibold text-slate-900 dark:text-white">
                                {sk}
                              </h4>

                              {course.hasCourse && (
                                <div className="mt-2.5 space-y-1 rounded-xl bg-white/80 p-2.5 text-xs dark:bg-slate-900/60">
                                  <p className="font-semibold text-indigo-600 dark:text-indigo-300">
                                    {isEn ? 'Class:' : 'Curso:'} {course.title}
                                  </p>
                                  {course.description && (
                                    <p className="text-[11px] text-slate-500 dark:text-white/60 line-clamp-2">
                                      {course.description}
                                    </p>
                                  )}
                                </div>
                              )}
                            </div>

                            <div className="mt-3 pt-2 border-t border-sky-100/80 dark:border-white/5 flex items-center justify-between">
                              <span className="text-[11px] text-slate-400">
                                {course.hasCourse ? '✓ En Catálogo' : 'Ruta recomendada'}
                              </span>
                              {course.hasCourse && course.matchedCapabilityId ? (
                                <Link
                                  to={
                                    course.matchedCourseType === 'graph'
                                      ? `/capabilities/${course.matchedCapabilityId}`
                                      : `/structured-learning/${course.matchedCapabilityId}`
                                  }
                                  className="inline-flex items-center gap-1.5 rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110"
                                >
                                  <PlayCircle className="h-3.5 w-3.5" />
                                  {isEn ? 'Start Class' : 'Iniciar Clase'}
                                </Link>
                              ) : course.matchedProgramId ? (
                                <Link
                                  to={`/programs/${course.matchedProgramId}`}
                                  className="inline-flex items-center gap-1.5 rounded-full bg-gradient-to-r from-purple-600 to-indigo-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-md shadow-purple-500/20 transition hover:brightness-110"
                                >
                                  <GraduationCap className="h-3.5 w-3.5" />
                                  {isEn ? 'Open Program' : 'Abrir Programa'}
                                </Link>
                              ) : (
                                <span className="text-[11px] text-amber-600 dark:text-amber-400 font-medium">
                                  {isEn ? 'In Development' : 'En desarrollo'}
                                </span>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </Card>

                  {/* Soft Skills & Linked Classes */}
                  <Card className="p-6 sm:p-8">
                    <div className="flex items-center justify-between mb-5">
                      <div className="flex items-center gap-2.5 text-violet-600 dark:text-violet-400">
                        <Sparkles className="h-5 w-5" />
                        <div>
                          <h3 className="text-base font-bold text-slate-900 dark:text-white">
                            {isEn ? 'Soft Skills & Behavioral Classes' : 'Competencias Blandas (Soft Skills) & Clases'}
                          </h3>
                          <p className="text-xs text-slate-500 dark:text-white/50">
                            {isEn ? 'Leadership, collaboration, and mindset development courses.' : 'Desarrollo de liderazgo, comunicación y trabajo en equipo.'}
                          </p>
                        </div>
                      </div>
                      <span className="rounded-full bg-violet-50 px-2.5 py-0.5 text-xs font-bold text-violet-700 dark:bg-violet-500/15 dark:text-violet-300">
                        {jobRole.softSkills.length} {isEn ? 'Skills' : 'Competencias'}
                      </span>
                    </div>

                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      {jobRole.softSkills.map((sk, i) => {
                        const course = resolveCourseForSkill(sk);
                        return (
                          <div
                            key={i}
                            className="flex flex-col justify-between rounded-2xl border border-violet-100 bg-violet-50/20 p-4 transition hover:border-violet-300 dark:border-violet-500/20 dark:bg-violet-500/5"
                          >
                            <div>
                              <div className="flex items-start justify-between gap-2">
                                <span className="text-[10px] font-bold uppercase tracking-wider text-violet-700 dark:text-violet-300">
                                  #{i + 1} Soft Skill
                                </span>
                                {course.hasCourse && (
                                  <span
                                    className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                                      statusMeta[course.trainingStatus]?.className ?? statusMeta.Pending.className
                                    }`}
                                  >
                                    {statusMeta[course.trainingStatus]?.label ?? course.trainingStatus}
                                  </span>
                                )}
                              </div>
                              <h4 className="mt-1 text-sm font-semibold text-slate-900 dark:text-white">
                                {sk}
                              </h4>

                              {course.hasCourse && (
                                <div className="mt-2.5 space-y-1 rounded-xl bg-white/80 p-2.5 text-xs dark:bg-slate-900/60">
                                  <p className="font-semibold text-violet-700 dark:text-violet-300">
                                    {isEn ? 'Class:' : 'Curso:'} {course.title}
                                  </p>
                                  {course.description && (
                                    <p className="text-[11px] text-slate-500 dark:text-white/60 line-clamp-2">
                                      {course.description}
                                    </p>
                                  )}
                                </div>
                              )}
                            </div>

                            <div className="mt-3 pt-2 border-t border-violet-100/80 dark:border-white/5 flex items-center justify-between">
                              <span className="text-[11px] text-slate-400">
                                {course.hasCourse ? '✓ En Catálogo' : 'Ruta recomendada'}
                              </span>
                              {course.hasCourse && course.matchedCapabilityId ? (
                                <Link
                                  to={
                                    course.matchedCourseType === 'graph'
                                      ? `/capabilities/${course.matchedCapabilityId}`
                                      : `/structured-learning/${course.matchedCapabilityId}`
                                  }
                                  className="inline-flex items-center gap-1.5 rounded-full bg-gradient-to-r from-violet-600 to-indigo-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-md shadow-violet-500/20 transition hover:brightness-110"
                                >
                                  <PlayCircle className="h-3.5 w-3.5" />
                                  {isEn ? 'Start Class' : 'Iniciar Clase'}
                                </Link>
                              ) : (
                                <span className="text-[11px] text-amber-600 dark:text-amber-400 font-medium">
                                  {isEn ? 'In Development' : 'En desarrollo'}
                                </span>
                              )}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </Card>

                  {/* Responsibilities & Daily Activities */}
                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                    <Card className="p-6">
                      <div className="flex items-center gap-2 text-indigo-600 dark:text-indigo-400 mb-4">
                        <FileText className="h-5 w-5" />
                        <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                          {isEn ? 'Core Responsibilities' : 'Responsabilidades Principales'}
                        </h3>
                      </div>
                      {jobRole.responsibilities.length === 0 ? (
                        <p className="text-xs text-slate-400 italic">{isEn ? 'None registered' : 'Sin registrar'}</p>
                      ) : (
                        <ul className="space-y-2.5">
                          {jobRole.responsibilities.map((resp, i) => (
                            <li key={i} className="flex items-start gap-2.5 text-xs text-slate-700 dark:text-white/80">
                              <CheckCircle2 className="h-4 w-4 text-indigo-500 shrink-0 mt-0.5" />
                              <span>{resp}</span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </Card>

                    <Card className="p-6">
                      <div className="flex items-center gap-2 text-emerald-600 dark:text-emerald-400 mb-4">
                        <Activity className="h-5 w-5" />
                        <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                          {isEn ? 'Daily Activities' : 'Actividades Diarias y Periódicas'}
                        </h3>
                      </div>
                      {jobRole.dailyActivities.length === 0 ? (
                        <p className="text-xs text-slate-400 italic">{isEn ? 'None registered' : 'Sin registrar'}</p>
                      ) : (
                        <ul className="space-y-2.5">
                          {jobRole.dailyActivities.map((act, i) => (
                            <li key={i} className="flex items-start gap-2.5 text-xs text-slate-700 dark:text-white/80">
                              <Clock className="h-4 w-4 text-emerald-500 shrink-0 mt-0.5" />
                              <span>{act}</span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </Card>
                  </div>

                  {/* KPIs & Expected Outcomes */}
                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                    <Card className="p-6">
                      <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400 mb-4">
                        <TrendingUp className="h-5 w-5" />
                        <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                          {isEn ? 'Performance Indicators (KPIs)' : 'Indicadores de Desempeño (KPIs)'}
                        </h3>
                      </div>
                      {jobRole.performanceIndicators.length === 0 ? (
                        <p className="text-xs text-slate-400 italic">{isEn ? 'None registered' : 'Sin registrar'}</p>
                      ) : (
                        <ul className="space-y-2">
                          {jobRole.performanceIndicators.map((kpi, i) => (
                            <li key={i} className="flex items-start gap-2 text-xs text-slate-700 dark:text-white/80">
                              <span className="h-1.5 w-1.5 rounded-full bg-amber-500 mt-1.5 shrink-0" />
                              <span>{kpi}</span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </Card>

                    <Card className="p-6">
                      <div className="flex items-center gap-2 text-purple-600 dark:text-purple-400 mb-4">
                        <Target className="h-5 w-5" />
                        <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                          {isEn ? 'Expected Success Outcomes' : 'Resultados Esperados del Puesto'}
                        </h3>
                      </div>
                      {jobRole.expectedOutcomes.length === 0 ? (
                        <p className="text-xs text-slate-400 italic">{isEn ? 'None registered' : 'Sin registrar'}</p>
                      ) : (
                        <ul className="space-y-2">
                          {jobRole.expectedOutcomes.map((out, i) => (
                            <li key={i} className="flex items-start gap-2 text-xs text-slate-700 dark:text-white/80">
                              <span className="h-1.5 w-1.5 rounded-full bg-purple-500 mt-1.5 shrink-0" />
                              <span>{out}</span>
                            </li>
                          ))}
                        </ul>
                      )}
                    </Card>
                  </div>

                  {/* Tools, Software & Processes */}
                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                    <Card className="p-6">
                      <div className="flex items-center gap-2 text-rose-600 dark:text-rose-400 mb-4">
                        <Wrench className="h-5 w-5" />
                        <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                          {isEn ? 'Tools & Software' : 'Herramientas y Software'}
                        </h3>
                      </div>
                      <div className="flex flex-wrap gap-2">
                        {jobRole.tools.map((tl, i) => (
                          <span
                            key={i}
                            className="rounded-lg border border-slate-200 bg-slate-50 px-2.5 py-1 text-xs font-medium text-slate-700 dark:border-white/10 dark:bg-white/5 dark:text-white/80"
                          >
                            {tl}
                          </span>
                        ))}
                      </div>
                    </Card>

                    <Card className="p-6">
                      <div className="flex items-center gap-2 text-teal-600 dark:text-teal-400 mb-4">
                        <ShieldCheck className="h-5 w-5" />
                        <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                          {isEn ? 'Required Processes & Compliance' : 'Procesos Obligatorios y Cumplimiento'}
                        </h3>
                      </div>
                      <div className="space-y-3">
                        {jobRole.departmentProcesses.length > 0 && (
                          <div>
                            <span className="text-[11px] font-bold uppercase text-slate-400">{isEn ? 'Department Processes' : 'Procesos de Departamento'}</span>
                            <div className="flex flex-wrap gap-1.5 mt-1">
                              {jobRole.departmentProcesses.map((p, i) => (
                                <span key={i} className="rounded-md bg-teal-50 px-2 py-0.5 text-xs text-teal-700 dark:bg-teal-500/15 dark:text-teal-300">
                                  {p}
                                </span>
                              ))}
                            </div>
                          </div>
                        )}
                        {jobRole.hrProcesses.length > 0 && (
                          <div>
                            <span className="text-[11px] font-bold uppercase text-slate-400">{isEn ? 'HR / Compliance' : 'RRHH y Cumplimiento'}</span>
                            <div className="flex flex-wrap gap-1.5 mt-1">
                              {jobRole.hrProcesses.map((p, i) => (
                                <span key={i} className="rounded-md bg-slate-100 px-2 py-0.5 text-xs text-slate-700 dark:bg-white/10 dark:text-white/70">
                                  {p}
                                </span>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    </Card>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* SUB-VIEW 2: OBJECTIVES (when activeTab === 'job-skills' && roleSubTab === 'objectives') */}
          {activeTab === 'job-skills' && roleSubTab === 'objectives' && (
            <div className="space-y-6">
              <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 rounded-3xl border border-indigo-100 bg-gradient-to-r from-indigo-50/90 via-purple-50/50 to-violet-50/90 p-5 shadow-sm dark:border-indigo-500/20 dark:bg-indigo-500/10">
                <div className="flex items-start gap-3.5">
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-indigo-600 text-white shadow-md shadow-indigo-500/20">
                    <Compass className="h-5 w-5" />
                  </span>
                  <div>
                    <h3 className="text-sm font-bold text-indigo-950 dark:text-indigo-200">
                      {isEn ? 'SMART Alignment with Department Goals' : 'Alineación SMART con los Objetivos del Departamento'}
                    </h3>
                    <p className="mt-0.5 text-xs text-indigo-800/80 dark:text-indigo-300/80">
                      {isEn
                        ? `Individual SMART goals that roll up into ${user?.departmentName || 'your department'}'s tactical plan.`
                        : `Metas SMART individuales que se conectan con el plan táctico de ${user?.departmentName || 'tu departamento'}.`}
                    </p>
                  </div>
                </div>

                <button
                  type="button"
                  onClick={openCreateModal}
                  className="inline-flex items-center gap-2 self-start sm:self-auto rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-5 py-2.5 text-xs font-semibold text-white shadow-md shadow-indigo-500/25 transition hover:brightness-110"
                >
                  <Plus className="h-4 w-4" />
                  {isEn ? 'New SMART Objective' : '+ Nuevo Objetivo SMART'}
                </button>
              </div>

              {objectives.length === 0 ? (
                <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
                  <Target className="h-10 w-10 text-slate-300 dark:text-white/20" />
                  <h3 className="mt-3 text-sm font-semibold text-slate-900 dark:text-white">
                    {isEn ? 'No objectives created yet' : 'Aún no hay objetivos registrados'}
                  </h3>
                  <button
                    type="button"
                    onClick={openCreateModal}
                    className="mt-4 inline-flex items-center gap-2 rounded-full bg-indigo-600 px-4 py-2 text-xs font-semibold text-white hover:bg-indigo-700"
                  >
                    <Plus className="h-4 w-4" />
                    {isEn ? 'Create Objective' : 'Crear Objetivo'}
                  </button>
                </Card>
              ) : (
                <div className="space-y-4">
                  {objectives.map((obj) => {
                    const meta = OBJECTIVE_STATUS_META[obj.status];
                    return (
                      <Card
                        key={obj.id}
                        className="group relative p-6 transition hover:border-indigo-200 hover:shadow-md dark:hover:border-indigo-500/30"
                      >
                        <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                          <div className="space-y-3 flex-1">
                            <div className="flex flex-wrap items-center gap-2">
                              <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${meta.className}`}>
                                {isEn ? meta.labelEn : meta.labelEs}
                              </span>

                              {(obj.metricName || obj.measurementMethod) && (
                                <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">
                                  <Sparkles className="h-3 w-3" />
                                  SMART
                                </span>
                              )}

                              <span className="flex items-center gap-1 text-xs text-slate-400">
                                <Clock className="h-3.5 w-3.5" />
                                {obj.targetQuarter} ({obj.targetDate})
                              </span>
                            </div>

                            <div>
                              <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                                {obj.title}
                              </h3>
                              {obj.description && (
                                <p className="mt-1 text-xs text-slate-600 dark:text-white/70 leading-relaxed">
                                  {obj.description}
                                </p>
                              )}
                            </div>

                            {(obj.metricName || obj.measurementMethod || (obj.targetValue != null && obj.currentValue != null)) && (
                              <div className="rounded-2xl border border-emerald-100 bg-emerald-50/40 p-3.5 text-xs space-y-1.5 dark:border-emerald-500/20 dark:bg-emerald-500/5">
                                <div className="flex flex-wrap items-center justify-between gap-2">
                                  <div className="flex items-center gap-1.5 font-medium text-emerald-950 dark:text-emerald-200">
                                    <Activity className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                                    <span className="font-semibold">{isEn ? 'Metric:' : 'Métrica:'}</span>
                                    <span>{obj.metricName || (isEn ? 'Key Indicator' : 'Indicador Clave')}</span>
                                  </div>
                                  {obj.targetValue != null && (
                                    <div className="flex items-baseline gap-1 font-semibold text-slate-900 dark:text-white">
                                      <span className="text-emerald-700 dark:text-emerald-300">
                                        {obj.currentValue != null ? obj.currentValue : '0'}
                                      </span>
                                      <span className="text-slate-400">
                                        / {obj.targetValue} {obj.unit || ''}
                                      </span>
                                    </div>
                                  )}
                                </div>

                                {obj.measurementMethod && (
                                  <p className="text-[11px] text-slate-600 dark:text-white/70">
                                    <span className="font-semibold text-slate-700 dark:text-white/90">
                                      {isEn ? 'Measurement Method:' : 'Método de Medición:'}{' '}
                                    </span>
                                    {obj.measurementMethod}
                                  </p>
                                )}
                              </div>
                            )}

                            {obj.actionPlan && (
                              <div className="text-xs text-slate-600 dark:text-white/70">
                                <span className="font-semibold text-slate-700 dark:text-white/90">
                                  {isEn ? 'Action Plan & Strategy:' : 'Plan de Acción y Recursos:'}{' '}
                                </span>
                                {obj.actionPlan}
                              </div>
                            )}

                            {/* Recommended Skills */}
                            {((obj.recommendedTechnicalSkills && obj.recommendedTechnicalSkills.length > 0) ||
                              (obj.recommendedSoftSkills && obj.recommendedSoftSkills.length > 0)) && (
                              <div className="space-y-1.5 rounded-2xl border border-blue-100 bg-blue-50/40 p-3.5 text-xs dark:border-blue-500/20 dark:bg-blue-500/5">
                                <div className="flex items-center gap-1.5 font-semibold text-blue-900 dark:text-blue-200">
                                  <GraduationCap className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                                  <span>{isEn ? 'Recommended Skills to Complete Objective:' : 'Skills Recomendados para este Objetivo:'}</span>
                                </div>
                                <div className="flex flex-wrap items-center gap-1.5 pt-0.5">
                                  {obj.recommendedTechnicalSkills?.map((sk, i) => (
                                    <span
                                      key={i}
                                      className="rounded-md bg-sky-100 px-2 py-0.5 text-[11px] font-medium text-sky-800 border border-sky-200/60 dark:bg-sky-500/20 dark:border-sky-500/30 dark:text-sky-300"
                                    >
                                      🛠️ {sk}
                                    </span>
                                  ))}
                                  {obj.recommendedSoftSkills?.map((sk, i) => (
                                    <span
                                      key={i}
                                      className="rounded-md bg-purple-100 px-2 py-0.5 text-[11px] font-medium text-purple-800 border border-purple-200/60 dark:bg-purple-500/20 dark:border-purple-500/30 dark:text-purple-300"
                                    >
                                      🤝 {sk}
                                    </span>
                                  ))}
                                </div>
                              </div>
                            )}

                            {obj.departmentObjectiveTitle && (
                              <div className="space-y-1 pt-1">
                                <span className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-50 px-3 py-1 text-xs font-semibold text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300">
                                  <Building2 className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                                  {isEn ? 'Aligned with Dept Objective:' : 'Alineado al Objetivo del Depto:'}{' '}
                                  <span className="font-bold">{obj.departmentObjectiveTitle}</span>
                                </span>
                              </div>
                            )}
                          </div>

                          <div className="flex sm:flex-col items-end justify-between sm:justify-start gap-4 min-w-[170px]">
                            <div className="flex items-center gap-1.5 opacity-0 transition group-hover:opacity-100">
                              <button
                                type="button"
                                onClick={() => openEditModal(obj)}
                                className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                              >
                                <Pencil className="h-4 w-4" />
                              </button>
                              <button
                                type="button"
                                onClick={() => handleDeleteObjective(obj.id, obj.title)}
                                className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400"
                              >
                                <Trash2 className="h-4 w-4" />
                              </button>
                            </div>

                            <div className="w-full text-right">
                              <span className="text-xs font-bold text-slate-900 dark:text-white">
                                {obj.progressPercentage}%
                              </span>
                              <div className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                                <div
                                  className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-violet-500 transition-all duration-300"
                                  style={{ width: `${obj.progressPercentage}%` }}
                                />
                              </div>
                            </div>
                          </div>
                        </div>
                      </Card>
                    );
                  })}
                </div>
              )}
            </div>
          )}

          {/* OTHER CATEGORY TABS: COURSES (when activeTab !== 'job-skills') */}
          {activeTab !== 'job-skills' && (
            <>
              <div className="flex flex-wrap items-center gap-1.5">
                {statusFilters.map((filter) => (
                  <button
                    key={filter.id}
                    type="button"
                    onClick={() => setStatusFilter(filter.id)}
                    className={`rounded-full border px-2.5 py-1 text-[11px] font-medium transition ${
                      statusFilter === filter.id
                        ? 'border-brand-500 bg-brand-500 text-white'
                        : 'border-slate-200 text-slate-500 hover:bg-slate-100 dark:border-white/10 dark:text-white/50 dark:hover:bg-white/5'
                    }`}
                  >
                    {filter.label}
                  </button>
                ))}
              </div>

              <CourseSection
                title={categoryTabs.find((t) => t.id === activeTab)!.label}
                icon={categoryTabs.find((t) => t.id === activeTab)!.icon}
                accent={categoryTabs.find((t) => t.id === activeTab)!.accent}
                courses={filteredCourses}
                statusMeta={statusMeta}
                emptyMessage={
                  activeCourses.length === 0
                    ? emptyMessageByTab[activeTab]
                    : (language === 'en' ? 'No course matches this filter.' : 'Ningún curso coincide con este filtro.')
                }
              />
            </>
          )}

          {/* SMART Objective Modal */}
          {modalOpen && (
            <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm overflow-y-auto">
              <div className="my-8 w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-3xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900 sm:p-8">
                <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-white/10">
                  <div className="flex items-center gap-3">
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-indigo-500 to-violet-600 text-white shadow-md shadow-indigo-500/20">
                      <Target className="h-5 w-5" />
                    </span>
                    <div>
                      <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                        {editingObjective
                          ? isEn ? 'Edit SMART Objective' : 'Editar Objetivo SMART'
                          : isEn ? 'New SMART Objective' : 'Nuevo Objetivo SMART'}
                      </h2>
                      <p className="text-xs text-slate-500 dark:text-white/50">
                        {isEn
                          ? 'Structured using the SMART goal framework and aligned to your department.'
                          : 'Estructurado con metodología SMART y alineado a tu departamento.'}
                      </p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => setModalOpen(false)}
                    className="rounded-full p-1.5 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
                  >
                    <X className="h-5 w-5" />
                  </button>
                </div>

                <form onSubmit={handleObjectiveSubmit} className="mt-5 space-y-6">
                  {/* AI SMART & Skills Recommendation Banner */}
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 rounded-2xl bg-gradient-to-r from-indigo-500/10 via-purple-500/10 to-pink-500/10 border border-indigo-200/60 p-4 dark:border-indigo-500/20 dark:bg-white/[0.03]">
                    <div className="flex items-start gap-3">
                      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-600 to-violet-600 text-white shadow-sm">
                        <Bot className="h-5 w-5" />
                      </span>
                      <div>
                        <p className="text-xs font-bold text-indigo-950 dark:text-indigo-200">
                          {isEn ? 'AI SMART & Skills Recommendation' : 'Agente IA: Recomendar SMART y Skills del Puesto'}
                        </p>
                        <p className="text-[11px] text-slate-600 dark:text-white/60">
                          {isEn
                            ? 'Analyzes the selected department goal and your job role skills to formulate an individual SMART objective with technical & soft skill recommendations.'
                            : 'Analiza el objetivo del departamento seleccionado y los skills de tu puesto para sugerir un objetivo SMART completo y los skills clave.'}
                        </p>
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={handleAiRecommend}
                      disabled={isAiRecommending}
                      className="inline-flex items-center justify-center gap-1.5 self-start sm:self-auto rounded-xl bg-gradient-to-r from-indigo-600 to-violet-600 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110 disabled:opacity-50 shrink-0"
                    >
                      <Sparkles className={`h-4 w-4 ${isAiRecommending ? 'animate-spin' : ''}`} />
                      {isAiRecommending
                        ? (isEn ? 'Generating suggestion...' : 'Generando sugerencia...')
                        : (isEn ? '✨ Suggest with AI' : '✨ Sugerir con IA')}
                    </button>
                  </div>

                  {/* SMART Header Pills */}
                  <div className="grid grid-cols-5 gap-1 rounded-xl bg-slate-100 p-1 text-center text-[10px] font-bold uppercase tracking-wider text-slate-600 dark:bg-white/5 dark:text-white/70">
                    <span className="rounded-lg bg-white py-1 text-indigo-600 shadow-sm dark:bg-white/10 dark:text-indigo-300">
                      S - {isEn ? 'Specific' : 'Específico'}
                    </span>
                    <span className="rounded-lg bg-white py-1 text-emerald-600 shadow-sm dark:bg-white/10 dark:text-emerald-300">
                      M - {isEn ? 'Measurable' : 'Medible'}
                    </span>
                    <span className="rounded-lg bg-white py-1 text-amber-600 shadow-sm dark:bg-white/10 dark:text-amber-300">
                      A - {isEn ? 'Achievable' : 'Alcanzable'}
                    </span>
                    <span className="rounded-lg bg-white py-1 text-purple-600 shadow-sm dark:bg-white/10 dark:text-purple-300">
                      R - {isEn ? 'Relevant' : 'Relevante'}
                    </span>
                    <span className="rounded-lg bg-white py-1 text-sky-600 shadow-sm dark:bg-white/10 dark:text-sky-300">
                      T - {isEn ? 'Time-Bound' : 'Temporal'}
                    </span>
                  </div>

                  {/* 1. S - Specific */}
                  <div className="space-y-4 rounded-2xl border border-indigo-100 bg-indigo-50/20 p-4 dark:border-indigo-500/20 dark:bg-indigo-500/5">
                    <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-indigo-700 dark:text-indigo-300">
                      <Target className="h-4 w-4" />
                      <span>S - {isEn ? 'Specific' : 'Específico'}</span>
                    </div>

                    <div>
                      <label className={labelClass}>
                        {isEn ? 'Objective Title *' : 'Título del Objetivo *'}
                      </label>
                      <input
                        required
                        value={formTitle}
                        onChange={(e) => setFormTitle(e.target.value)}
                        placeholder={isEn ? 'e.g. Build automated CI/CD code review subagent' : 'p. ej. Desarrollar subagente de verificación automática de código'}
                        className={inputClass}
                      />
                    </div>

                    <div>
                      <label className={labelClass}>
                        {isEn ? 'Description & Scope' : 'Descripción y Alcance Detallado'}
                      </label>
                      <textarea
                        rows={2}
                        value={formDesc}
                        onChange={(e) => setFormDesc(e.target.value)}
                        placeholder={isEn ? 'Describe specific tasks and deliverables...' : 'Describe los entregables y alcance específico...'}
                        className={inputClass}
                      />
                    </div>
                  </div>

                  {/* 2. M - Measurable */}
                  <div className="space-y-4 rounded-2xl border border-emerald-100 bg-emerald-50/20 p-4 dark:border-emerald-500/20 dark:bg-emerald-500/5">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-emerald-700 dark:text-emerald-300">
                        <Activity className="h-4 w-4" />
                        <span>M - {isEn ? 'Measurable (How to measure)' : 'Medible (¿Cómo se medirá?)'}</span>
                      </div>
                      {formCurrentVal && formTargetVal && (
                        <button
                          type="button"
                          onClick={autoCalculateProgress}
                          className="inline-flex items-center gap-1 rounded-md bg-emerald-100 px-2.5 py-1 text-[11px] font-semibold text-emerald-800 transition hover:bg-emerald-200 dark:bg-emerald-500/20 dark:text-emerald-300"
                        >
                          <Calculator className="h-3 w-3" />
                          {isEn ? 'Sync Progress %' : 'Sincronizar %'}
                        </button>
                      )}
                    </div>

                    <div>
                      <label className={labelClass}>
                        {isEn ? 'Metric Name' : 'Nombre del Indicador / Métrica'}
                      </label>
                      <input
                        value={formMetricName}
                        onChange={(e) => setFormMetricName(e.target.value)}
                        placeholder={isEn ? 'e.g. Percentage of Unit Tests Autogenerated' : 'p. ej. Porcentaje de Tests Unitarios Autogenerados con Éxito'}
                        className={inputClass}
                      />
                    </div>

                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                      <div>
                        <label className={labelClass}>{isEn ? 'Baseline / Current' : 'Valor Actual'}</label>
                        <input
                          type="number"
                          step="any"
                          value={formCurrentVal}
                          onChange={(e) => setFormCurrentVal(e.target.value)}
                          placeholder="0"
                          className={inputClass}
                        />
                      </div>
                      <div>
                        <label className={labelClass}>{isEn ? 'Target Value' : 'Valor Meta'}</label>
                        <input
                          type="number"
                          step="any"
                          value={formTargetVal}
                          onChange={(e) => setFormTargetVal(e.target.value)}
                          placeholder="100"
                          className={inputClass}
                        />
                      </div>
                      <div>
                        <label className={labelClass}>{isEn ? 'Unit' : 'Unidad'}</label>
                        <input
                          value={formUnit}
                          onChange={(e) => setFormUnit(e.target.value)}
                          placeholder="%, hrs, pts..."
                          className={inputClass}
                        />
                      </div>
                    </div>

                    <div>
                      <label className={labelClass}>
                        {isEn ? 'Measurement Method & Source' : 'Método de Medición y Fuente de Datos'}
                      </label>
                      <textarea
                        rows={2}
                        value={formMeasurementMethod}
                        onChange={(e) => setFormMeasurementMethod(e.target.value)}
                        placeholder={isEn ? 'e.g. GitHub Actions test reports...' : 'p. ej. Reportes de GitHub Actions y logs de telemetría...'}
                        className={inputClass}
                      />
                    </div>
                  </div>

                  {/* 3. A - Achievable */}
                  <div className="space-y-4 rounded-2xl border border-amber-100 bg-amber-50/20 p-4 dark:border-amber-500/20 dark:bg-amber-500/5">
                    <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-amber-700 dark:text-amber-300">
                      <Sparkles className="h-4 w-4" />
                      <span>A - {isEn ? 'Achievable (Action Plan)' : 'Alcanzable (Plan de Acción)'}</span>
                    </div>
                    <div>
                      <label className={labelClass}>
                        {isEn ? 'Action Steps & Feasibility Strategy' : 'Plan de Acción y Recursos Necesarios'}
                      </label>
                      <textarea
                        rows={2}
                        value={formActionPlan}
                        onChange={(e) => setFormActionPlan(e.target.value)}
                        placeholder={isEn ? 'Steps you will take to achieve this goal...' : 'Describe los pasos y recursos que garantizan la viabilidad...'}
                        className={inputClass}
                      />
                    </div>
                  </div>

                  {/* 4. R - Relevant */}
                  <div className="space-y-4 rounded-2xl border border-purple-100 bg-purple-50/20 p-4 dark:border-purple-500/20 dark:bg-purple-500/5">
                    <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-purple-700 dark:text-purple-300">
                      <Compass className="h-4 w-4" />
                      <span>R - {isEn ? 'Relevant (Department Alignment)' : 'Relevante (Alineación con el Departamento)'}</span>
                    </div>

                    <div>
                      <label className={labelClass}>
                        {isEn ? 'Aligned Department Objective' : 'Objetivo Táctico del Departamento'}
                      </label>
                      <select
                        value={formDeptObjId}
                        onChange={(e) => setFormDeptObjId(e.target.value)}
                        className="mt-2 w-full rounded-xl border border-purple-200 bg-white px-3.5 py-2.5 text-xs font-medium text-slate-900 outline-none transition focus:border-purple-500 focus:ring-1 focus:ring-purple-500 dark:border-purple-500/30 dark:bg-slate-900 dark:text-white"
                      >
                        <option value="">{isEn ? '— Independent Goal —' : '— Objetivo individual independiente —'}</option>
                        {groupedDeptObjectives.map((group) => (
                          <optgroup key={group.departmentName} label={group.departmentName}>
                            {group.objectives.map((d) => (
                              <option key={d.id} value={d.id}>
                                {d.title} ({d.targetQuarter})
                              </option>
                            ))}
                          </optgroup>
                        ))}
                      </select>

                      {/* Selected Dept Objective Preview */}
                      {(() => {
                        const preview =
                          allCorporateObjectives.find((d) => d.id === formDeptObjId) ||
                          departmentObjectives.find((d) => d.id === formDeptObjId);
                        if (!preview) return null;
                        return (
                          <div className="mt-3 rounded-xl border border-purple-200/80 bg-white p-3.5 text-xs shadow-sm dark:border-purple-500/30 dark:bg-slate-900/90">
                            <div className="flex items-center justify-between">
                              <span className="text-[10px] font-bold uppercase text-purple-600 dark:text-purple-400">
                                {preview.departmentName ? `${preview.departmentName} · ` : ''}{isEn ? 'Department Objective Target' : 'Meta del Departamento'}
                              </span>
                              <span className="rounded-full bg-purple-100 px-2 py-0.5 text-[10px] font-semibold text-purple-800 dark:bg-purple-500/20 dark:text-purple-300">
                                {preview.targetQuarter}
                              </span>
                            </div>
                            <h4 className="mt-1 text-xs font-semibold text-slate-900 dark:text-white">
                              {preview.title}
                            </h4>
                            {preview.description && (
                              <p className="mt-1 text-[11px] text-slate-600 dark:text-white/70">
                                {preview.description}
                              </p>
                            )}
                            {preview.metricName && (
                              <div className="mt-2 flex flex-wrap gap-2 text-[10px] text-slate-500 dark:text-white/50">
                                <span>Métrica: <strong>{preview.metricName}</strong></span>
                                {preview.targetValue != null && (
                                  <span>Meta: <strong>{preview.targetValue} {preview.unit ?? ''}</strong></span>
                                )}
                              </div>
                            )}
                            {preview.strategicGoalTitle && (
                              <div className="mt-2 border-t border-slate-100 pt-1.5 text-[10px] text-purple-700 dark:border-white/5 dark:text-purple-300">
                                <strong>{isEn ? 'Corporate Goal:' : 'Meta Corporativa:'}</strong> {preview.strategicGoalTitle}
                              </div>
                            )}
                          </div>
                        );
                      })()}
                    </div>

                    <div>
                      <label className={labelClass}>
                        {isEn ? 'Contribution & Business Impact' : 'Impacto Esperado en el Departamento'}
                      </label>
                      <textarea
                        rows={2}
                        value={formBusinessImpact}
                        onChange={(e) => setFormBusinessImpact(e.target.value)}
                        placeholder={isEn ? 'Why is this objective important...' : '¿Por qué es importante este objetivo?...'}
                        className={inputClass}
                      />
                    </div>
                  </div>

                  {/* 5. Recommended Skills for this Objective (Job Role Competencies) */}
                  <div className="space-y-4 rounded-2xl border border-blue-100 bg-blue-50/20 p-4 dark:border-blue-500/20 dark:bg-blue-500/5">
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-1">
                      <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-blue-700 dark:text-blue-300">
                        <GraduationCap className="h-4 w-4" />
                        <span>{isEn ? 'Recommended Skills for this Objective' : 'Skills Recomendados para Completar este Objetivo'}</span>
                      </div>
                      {jobRole?.title && (
                        <span className="text-[11px] font-medium text-slate-500 dark:text-white/50">
                          {isEn ? 'From Job Role:' : 'Del Puesto:'} <strong className="text-blue-600 dark:text-blue-400">{jobRole.title}</strong>
                        </span>
                      )}
                    </div>

                    {formReasoning && (
                      <div className="rounded-xl bg-white/80 p-3 text-xs text-slate-700 border border-blue-100/80 shadow-xs dark:bg-slate-900/60 dark:border-white/10 dark:text-white/80">
                        <p className="font-semibold text-blue-900 dark:text-blue-200 mb-0.5">
                          💡 {isEn ? 'AI Agent Recommendation Rationale:' : 'Justificación del Agente de IA:'}
                        </p>
                        <p className="italic text-slate-600 dark:text-white/70">{formReasoning}</p>
                      </div>
                    )}

                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                      {/* Technical Skills */}
                      <div className="rounded-xl bg-white p-3.5 border border-slate-200/80 shadow-xs dark:border-white/10 dark:bg-slate-900/50">
                        <div className="flex items-center justify-between mb-2">
                          <span className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                            🛠️ {isEn ? 'Technical Skills' : 'Skills Técnicos'}
                          </span>
                          <span className="text-[10px] rounded-full bg-sky-100 px-2 py-0.5 font-semibold text-sky-800 dark:bg-sky-500/20 dark:text-sky-300">
                            {formRecommendedTechSkills.length}
                          </span>
                        </div>

                        {formRecommendedTechSkills.length === 0 ? (
                          <p className="text-xs text-slate-400 italic py-1">
                            {isEn ? 'No technical skills assigned yet. Click "Suggest with AI" above.' : 'Aún no se han asignado skills. Haz clic en "Sugerir con IA" arriba.'}
                          </p>
                        ) : (
                          <div className="flex flex-wrap gap-1.5">
                            {formRecommendedTechSkills.map((sk, idx) => (
                              <span
                                key={idx}
                                className="inline-flex items-center gap-1.5 rounded-lg bg-sky-50 px-2.5 py-1 text-xs font-medium text-sky-800 border border-sky-200 dark:bg-sky-500/15 dark:border-sky-500/30 dark:text-sky-300"
                              >
                                <span>{sk}</span>
                                <button
                                  type="button"
                                  onClick={() => setFormRecommendedTechSkills(formRecommendedTechSkills.filter((_, i) => i !== idx))}
                                  className="text-sky-400 hover:text-sky-700 font-bold dark:hover:text-sky-200"
                                  title={isEn ? 'Remove skill' : 'Quitar skill'}
                                >
                                  ×
                                </button>
                              </span>
                            ))}
                          </div>
                        )}
                      </div>

                      {/* Soft Skills */}
                      <div className="rounded-xl bg-white p-3.5 border border-slate-200/80 shadow-xs dark:border-white/10 dark:bg-slate-900/50">
                        <div className="flex items-center justify-between mb-2">
                          <span className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-slate-300">
                            🤝 {isEn ? 'Soft Skills / Competencies' : 'Skills Blandos y Competencias'}
                          </span>
                          <span className="text-[10px] rounded-full bg-purple-100 px-2 py-0.5 font-semibold text-purple-800 dark:bg-purple-500/20 dark:text-purple-300">
                            {formRecommendedSoftSkills.length}
                          </span>
                        </div>

                        {formRecommendedSoftSkills.length === 0 ? (
                          <p className="text-xs text-slate-400 italic py-1">
                            {isEn ? 'No soft skills assigned yet. Click "Suggest with AI" above.' : 'Aún no se han asignado skills. Haz clic en "Sugerir con IA" arriba.'}
                          </p>
                        ) : (
                          <div className="flex flex-wrap gap-1.5">
                            {formRecommendedSoftSkills.map((sk, idx) => (
                              <span
                                key={idx}
                                className="inline-flex items-center gap-1.5 rounded-lg bg-purple-50 px-2.5 py-1 text-xs font-medium text-purple-800 border border-purple-200 dark:bg-purple-500/15 dark:border-purple-500/30 dark:text-purple-300"
                              >
                                <span>{sk}</span>
                                <button
                                  type="button"
                                  onClick={() => setFormRecommendedSoftSkills(formRecommendedSoftSkills.filter((_, i) => i !== idx))}
                                  className="text-purple-400 hover:text-purple-700 font-bold dark:hover:text-purple-200"
                                  title={isEn ? 'Remove skill' : 'Quitar skill'}
                                >
                                  ×
                                </button>
                              </span>
                            ))}
                          </div>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* 6. T - Time-Bound */}
                  <div className="space-y-4 rounded-2xl border border-sky-100 bg-sky-50/20 p-4 dark:border-sky-500/20 dark:bg-sky-500/5">
                    <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-sky-700 dark:text-sky-300">
                      <Clock className="h-4 w-4" />
                      <span>T - {isEn ? 'Time-Bound & Progress' : 'Temporal y Progreso'}</span>
                    </div>

                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                      <div>
                        <label className={labelClass}>{isEn ? 'Target Quarter' : 'Trimestre Meta'}</label>
                        <input
                          value={formQuarter}
                          onChange={(e) => setFormQuarter(e.target.value)}
                          placeholder="Q4 2026"
                          className={inputClass}
                        />
                      </div>
                      <div>
                        <label className={labelClass}>{isEn ? 'Target Date' : 'Fecha Límite'}</label>
                        <input
                          type="date"
                          value={formDate}
                          onChange={(e) => setFormDate(e.target.value)}
                          className={inputClass}
                        />
                      </div>
                    </div>

                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                      <div>
                        <label className={labelClass}>{isEn ? 'Status' : 'Estado'}</label>
                        <select
                          value={formStatus}
                          onChange={(e) => setFormStatus(e.target.value as EmployeeObjective['status'])}
                          className={inputClass}
                        >
                          <option value="Active">{isEn ? 'Active' : 'Activo'}</option>
                          <option value="InProgress">{isEn ? 'In Progress' : 'En Progreso'}</option>
                          <option value="Completed">{isEn ? 'Completed' : 'Completado'}</option>
                          <option value="Paused">{isEn ? 'Paused' : 'Pausado'}</option>
                        </select>
                      </div>

                      <div>
                        <label className={labelClass}>
                          {isEn ? `Progress (${formProgress}%)` : `Progreso (${formProgress}%)`}
                        </label>
                        <input
                          type="range"
                          min="0"
                          max="100"
                          value={formProgress}
                          onChange={(e) => setFormProgress(Number(e.target.value))}
                          className="mt-3 w-full accent-indigo-600"
                        />
                      </div>
                    </div>
                  </div>

                  <div className="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4 dark:border-white/10">
                    <button
                      type="button"
                      onClick={() => setModalOpen(false)}
                      className="rounded-full px-5 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
                    >
                      {isEn ? 'Cancel' : 'Cancelar'}
                    </button>
                    <button
                      type="submit"
                      disabled={!formTitle.trim()}
                      className="rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-6 py-2.5 text-sm font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110 disabled:opacity-50"
                    >
                      {editingObjective ? (isEn ? 'Save Changes' : 'Guardar Cambios') : (isEn ? 'Create Objective' : 'Crear Objetivo')}
                    </button>
                  </div>
                </form>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

const ACCENT_STYLES = {
  slate: {
    icon: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70',
    heading: 'text-slate-600 dark:text-white/70',
    badge: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/60',
  },
  brand: {
    icon: 'bg-brand-100 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300',
    heading: 'text-brand-600 dark:text-brand-300',
    badge: 'bg-brand-100 text-brand-700 dark:bg-brand-500/15 dark:text-brand-300',
  },
  amber: {
    icon: 'bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300',
    heading: 'text-amber-600 dark:text-amber-300',
    badge: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  },
  rose: {
    icon: 'bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300',
    heading: 'text-rose-500 dark:text-rose-300',
    badge: 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',
  },
  sky: {
    icon: 'bg-sky-100 text-sky-600 dark:bg-sky-500/15 dark:text-sky-300',
    heading: 'text-sky-600 dark:text-sky-300',
    badge: 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300',
  },
  violet: {
    icon: 'bg-violet-100 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300',
    heading: 'text-violet-600 dark:text-violet-300',
    badge: 'bg-violet-100 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300',
  },
} as const;

type Accent = keyof typeof ACCENT_STYLES;

interface StatCardProps {
  label: string;
  value: number;
  icon: typeof CheckCircle2;
  accent: Accent;
}

function StatCard({ label, value, icon: Icon, accent }: StatCardProps) {
  const styles = ACCENT_STYLES[accent];
  return (
    <Card className="p-4">
      <div className="flex items-center gap-3">
        <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${styles.icon}`}>
          <Icon className="h-4.5 w-4.5" />
        </span>
        <div>
          <p className="text-2xl font-semibold text-slate-900 dark:text-white">{value}</p>
          <p className="text-xs text-slate-400 dark:text-white/40">{label}</p>
        </div>
      </div>
    </Card>
  );
}

interface CourseSectionProps {
  title: string;
  icon: typeof CheckCircle2;
  accent: Accent;
  courses: RoadmapCourse[];
  statusMeta: Record<string, { label: string; className: string }>;
  emptyMessage: string;
}

function CourseSection({ title, icon: Icon, accent, courses, statusMeta, emptyMessage }: CourseSectionProps) {
  const styles = ACCENT_STYLES[accent];
  const { language } = useI18n();

  return (
    <div>
      <h2 className={`flex items-center gap-2 text-sm font-semibold ${styles.heading}`}>
        <Icon className="h-4 w-4" />
        {title}
        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-500 dark:bg-white/10 dark:text-white/50">
          {courses.length}
        </span>
      </h2>

      {courses.length === 0 ? (
        <p className="mt-2 rounded-xl border border-dashed border-slate-200 p-4 text-sm text-slate-400 dark:border-white/10 dark:text-white/40">
          {emptyMessage}
        </p>
      ) : (
        <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {courses.map((course) => (
            <div
              key={`${course.track}-${course.order}-${course.title}`}
              className="flex flex-col gap-2 rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5"
            >
              <div className="flex items-center justify-between gap-2">
                <span
                  className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${
                    statusMeta[course.trainingStatus]?.className ?? statusMeta.Pending!.className
                  }`}
                >
                  {statusMeta[course.trainingStatus]?.label ?? course.trainingStatus}
                </span>
                <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${styles.badge}`}>
                  #{course.order}
                </span>
              </div>

              <p className="text-sm font-semibold text-slate-900 dark:text-white">{course.title}</p>
              <p className="text-xs text-slate-400 dark:text-white/40">
                {language === 'en' ? 'Target skill:' : 'Cierra:'} {course.skill}
              </p>
              <p className="text-sm text-slate-600 dark:text-white/60">{course.description}</p>
              {(course.matchedProgramName || course.matchedCapabilityName) && (
                <p className="text-xs font-medium text-emerald-600 dark:text-emerald-400">
                  {language === 'en' ? 'Course in catalog:' : 'Curso en catálogo:'} {course.matchedProgramName ?? course.matchedCapabilityName}
                </p>
              )}

              {course.matchedProgramId ? (
                <Link
                  to={`/programs/${course.matchedProgramId}`}
                  className="mt-1 inline-flex w-fit items-center gap-1.5 rounded-full bg-purple-600 px-2.5 py-1 text-xs font-medium text-white transition hover:bg-purple-500"
                >
                  <GraduationCap className="h-3 w-3" />
                  {language === 'en' ? 'Open Program' : 'Abrir Programa'}
                </Link>
              ) : course.matchedCapabilityId ? (
                <Link
                  to={`/capabilities/${course.matchedCapabilityId}`}
                  className="mt-1 inline-flex w-fit items-center gap-1.5 rounded-full bg-emerald-600 px-2.5 py-1 text-xs font-medium text-white transition hover:bg-emerald-500"
                >
                  <PlayCircle className="h-3 w-3" />
                  {language === 'en' ? 'Take course' : 'Tomar el curso'}
                </Link>
              ) : (
                <span className="mt-1 inline-flex w-fit items-center gap-1.5 rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-500 dark:bg-white/10 dark:text-white/50">
                  <Hammer className="h-3 w-3" />
                  {language === 'en' ? 'In development' : 'En proceso / Desarrollo'}
                </span>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
