import { useMemo, useState, type FormEvent } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import {
  Activity,
  Bot,
  Briefcase,
  Building2,
  Calculator,
  CheckCircle2,
  Circle,
  Clock,
  Compass,
  Cpu,
  ExternalLink,
  FileText,
  GraduationCap,
  Lightbulb,
  MapPin,
  MessageSquareQuote,
  Pencil,
  PlayCircle,
  Plus,
  ShieldCheck,
  Sparkles,
  Target,
  Trash2,
  TrendingUp,
  UserCheck,
  Wrench,
  X,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getJobRole } from '@/features/profile/api/jobRoleApi';
import { getMyCareerPlan } from '@/features/career-plan/api/careerPlanApi';
import { getSkills } from '@/features/capabilities/api/skillsApi';
import {
  getAvailableDepartmentObjectives,
  getEmployeeObjectives,
  saveEmployeeObjectives,
  getEmployeeGrowthRecommendations,
  saveEmployeeGrowthRecommendations,
  recommendSmartObjective,
  type EmployeeObjective,
  type EmployeeGrowthRecommendation,
  type GrowthActionItem,
} from './api/employeeObjectivesApi';

type TabType = 'objectives' | 'growth' | 'role';

const ENGRAM_APP_URL = 'http://localhost:5178';

function buildEngramProgramUrl(programId: string): string {
  return `${ENGRAM_APP_URL}/programs/${programId}`;
}

const STATUS_META: Record<
  string,
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
  Pending: {
    labelEs: 'Pendiente',
    labelEn: 'Pending',
    className: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/50',
  },
  Available: {
    labelEs: 'Disponible',
    labelEn: 'Available',
    className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  },
};

const GROWTH_STATUS_META: Record<
  string,
  { labelEs: string; labelEn: string; className: string }
> = {
  NotStarted: {
    labelEs: 'Por Iniciar',
    labelEn: 'Not Started',
    className: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/60',
  },
  InProgress: {
    labelEs: 'En Desarrollo',
    labelEn: 'In Progress',
    className: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  },
  Applied: {
    labelEs: 'Aplicado en el Puesto',
    labelEn: 'Applied in Role',
    className: 'bg-indigo-100 text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300',
  },
  Mastered: {
    labelEs: 'Dominado / Superado',
    labelEn: 'Mastered',
    className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  },
};

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
}

export function ObjectivesPage() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';

  const tenantId = user?.tenantId ?? '';
  const personId = user?.personId ?? '';
  const departmentId = user?.departmentId ?? '';

  const [activeTab, setActiveTab] = useState<TabType>('objectives');
  const [statusFilter, setStatusFilter] = useState<string>('all');

  // Fetch full Job Role details
  const effectiveJobRoleId = user?.jobRoleId || '';
  const jobRoleQuery = useQuery({
    queryKey: ['my-job-role-full', effectiveJobRoleId],
    queryFn: () => getJobRole(effectiveJobRoleId),
    enabled: Boolean(effectiveJobRoleId),
  });

  const jobRole = jobRoleQuery.data;

  // Fetch Career Plan to link classes to skills
  const planQuery = useQuery({
    queryKey: ['my-career-plan-obj', personId],
    queryFn: () => getMyCareerPlan(personId),
    enabled: Boolean(personId),
  });
  const plan = planQuery.data;

  // Skills Query to check all catalog courses (Structured Learning and Graph)
  const skillsQuery = useQuery({
    queryKey: ['skills-obj', tenantId],
    queryFn: () => getSkills(tenantId),
    enabled: Boolean(tenantId),
  });

  const allCourses = useMemo(() => {
    if (!plan?.hasPlan) return [];
    return [
      ...plan.technicalRoadmap.map((step) => ({ ...step, track: 'technical' as const })),
      ...plan.softSkillsRoadmap.map((step) => ({ ...step, track: 'soft' as const })),
      ...plan.departmentProcessesRoadmap.map((step) => ({ ...step, track: 'department' as const })),
      ...plan.hrProcessesRoadmap.map((step) => ({ ...step, track: 'hr' as const })),
    ];
  }, [plan]);

  function resolveCourseForSkill(skillName: string) {
    const norm = skillName.toLowerCase().replace(/\s+/g, ' ').trim();

    // 1. Check allCourses from Career Plan
    const planMatch = allCourses.find((c) => {
      const cSkillNorm = c.skill.toLowerCase().replace(/\s+/g, ' ').trim();
      const cTitleNorm = c.title.toLowerCase().replace(/\s+/g, ' ').trim();
      return cSkillNorm === norm || cTitleNorm === norm || norm.includes(cSkillNorm) || cSkillNorm.includes(norm);
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
      const sNorm = s.name.toLowerCase().replace(/\s+/g, ' ').trim();
      return sNorm === norm || norm.includes(sNorm) || sNorm.includes(norm);
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

  // Available Department Objectives for alignment
  const departmentObjectives = useMemo(() => {
    return getAvailableDepartmentObjectives(tenantId, departmentId, isEn);
  }, [tenantId, departmentId, isEn]);

  // Personal SMART Objectives
  const [objectives, setObjectives] = useState<EmployeeObjective[]>(() => {
    return getEmployeeObjectives(tenantId, personId, isEn);
  });

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
    const selectedDeptObj = departmentObjectives.find((d) => d.id === formDeptObjId) || departmentObjectives[0];
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
        jobRoleTechnicalSkills: jobRole?.technicalSkills || roleTechnicalSkills || [],
        jobRoleSoftSkills: jobRole?.softSkills || roleSoftSkills || [],
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

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!formTitle.trim()) return;

    const selectedDeptObj = departmentObjectives.find((d) => d.id === formDeptObjId);
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
              departmentName: user?.departmentName ?? 'Software Engineering',
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
        departmentName: user?.departmentName ?? 'Software Engineering',
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

  function handleDelete(id: string, title: string) {
    const confirmMsg = isEn ? `Delete objective "${title}"?` : `¿Eliminar el objetivo "${title}"?`;
    if (window.confirm(confirmMsg)) {
      updateObjectives(objectives.filter((o) => o.id !== id));
    }
  }

  // ── Areas for Improvement / Growth Recommendations ─────────────────────────
  const [growthRecommendations, setGrowthRecommendations] = useState<EmployeeGrowthRecommendation[]>(() => {
    return getEmployeeGrowthRecommendations(tenantId, personId, isEn);
  });
  const [selectedQuarter, setSelectedQuarter] = useState<string>('Q1 2026');
  const [growthSkillFilter, setGrowthSkillFilter] = useState<'all' | 'technical' | 'soft'>('all');

  const roleTechnicalSkills = useMemo(() => {
    return jobRole?.technicalSkills && jobRole.technicalSkills.length > 0
      ? jobRole.technicalSkills
      : ['API + Microservicios', 'C# & .NET 10', 'Cloud Architecture & Microservices', 'CI/CD & DevOps'];
  }, [jobRole]);

  const roleSoftSkills = useMemo(() => {
    return jobRole?.softSkills && jobRole.softSkills.length > 0
      ? jobRole.softSkills
      : ['Comunicación Asertiva y Liderazgo Técnico', 'Resolución de Problemas Complejos', 'Gestión de Prioridades'];
  }, [jobRole]);

  function updateGrowthRecommendations(next: EmployeeGrowthRecommendation[]) {
    setGrowthRecommendations(next);
    saveEmployeeGrowthRecommendations(tenantId, personId, next);
  }

  function toggleActionItem(recId: string, actId: string) {
    const next = growthRecommendations.map((r) => {
      if (r.id !== recId) return r;
      const updatedActs = r.actionItems.map((a) => (a.id === actId ? { ...a, completed: !a.completed } : a));
      const completedCount = updatedActs.filter((a) => a.completed).length;
      const progress = updatedActs.length > 0 ? Math.round((completedCount / updatedActs.length) * 100) : r.progressPercentage;
      return {
        ...r,
        actionItems: updatedActs,
        progressPercentage: progress,
        status:
          progress === 100 ? ('Mastered' as const) : progress > 0 ? ('InProgress' as const) : ('NotStarted' as const),
      };
    });
    updateGrowthRecommendations(next);
  }

  function updateRecStatus(recId: string, status: EmployeeGrowthRecommendation['status']) {
    const next = growthRecommendations.map((r) => {
      if (r.id !== recId) return r;
      return {
        ...r,
        status,
        progressPercentage:
          status === 'Mastered'
            ? 100
            : status === 'Applied'
              ? Math.max(r.progressPercentage, 80)
              : status === 'InProgress'
                ? Math.max(r.progressPercentage, 40)
                : 0,
      };
    });
    updateGrowthRecommendations(next);
  }

  // Growth Modal State
  const [growthModalOpen, setGrowthModalOpen] = useState(false);
  const [editingGrowthRec, setEditingGrowthRec] = useState<EmployeeGrowthRecommendation | null>(null);

  const [gTitle, setGTitle] = useState('');
  const [gQuarter, setGQuarter] = useState('Q1 2026');
  const [gSkillType, setGSkillType] = useState<'technical' | 'soft'>('technical');
  const [gSkillName, setGSkillName] = useState('');
  const [gRecommendationText, setGRecommendationText] = useState('');
  const [gManagerName, setGManagerName] = useState('Carlos Morales');
  const [gManagerTitle, setGManagerTitle] = useState('Director de Ingeniería');
  const [gLinkedObjId, setGLinkedObjId] = useState('');
  const [gLinkedCourseId, setGLinkedCourseId] = useState('75a4d98f-0dcb-499e-a5d4-09faba01a885');
  const [gLinkedCourseTitle, setGLinkedCourseTitle] = useState('API +MICROSERVICES');
  const [gMetricName, setGMetricName] = useState('');
  const [gCurrentVal, setGCurrentVal] = useState('');
  const [gTargetVal, setGTargetVal] = useState('');
  const [gActionItemsText, setGActionItemsText] = useState('');
  const [gNotes, setGNotes] = useState('');

  const skillsForSelectedType = gSkillType === 'technical' ? roleTechnicalSkills : roleSoftSkills;

  function handleSkillChange(newSkill: string) {
    setGSkillName(newSkill);
    const resolved = resolveCourseForSkill(newSkill);
    setGLinkedCourseId(resolved.matchedCapabilityId || resolved.matchedProgramId || '');
    setGLinkedCourseTitle(resolved.hasCourse ? resolved.title : newSkill);
  }

  function handleSkillTypeChange(type: 'technical' | 'soft') {
    setGSkillType(type);
    const list = type === 'technical' ? roleTechnicalSkills : roleSoftSkills;
    const defaultSk = list[0] || '';
    if (defaultSk) {
      setGSkillName(defaultSk);
      const resolved = resolveCourseForSkill(defaultSk);
      setGLinkedCourseId(resolved.matchedCapabilityId || resolved.matchedProgramId || '');
      setGLinkedCourseTitle(resolved.hasCourse ? resolved.title : defaultSk);
    }
  }

  function openCreateGrowthModal() {
    setEditingGrowthRec(null);
    setGTitle('');
    setGQuarter(selectedQuarter === 'all' ? 'Q1 2026' : selectedQuarter);
    setGSkillType('technical');
    const defaultSk = roleTechnicalSkills[0] ?? 'API + Microservicios';
    setGSkillName(defaultSk);
    const resolved = resolveCourseForSkill(defaultSk);
    setGLinkedCourseId(resolved.matchedCapabilityId || resolved.matchedProgramId || '');
    setGLinkedCourseTitle(resolved.hasCourse ? resolved.title : defaultSk);
    setGRecommendationText('');
    setGManagerName('Carlos Morales');
    setGManagerTitle('Director de Ingeniería');
    setGLinkedObjId(objectives[0]?.id ?? '');
    setGMetricName('');
    setGCurrentVal('');
    setGTargetVal('');
    setGActionItemsText('');
    setGNotes('');
    setGrowthModalOpen(true);
  }

  function openEditGrowthModal(rec: EmployeeGrowthRecommendation) {
    setEditingGrowthRec(rec);
    setGTitle(rec.title);
    setGQuarter(rec.quarter);
    setGSkillType(rec.skillType);
    setGSkillName(rec.skillName);
    setGRecommendationText(rec.recommendationText);
    setGManagerName(rec.managerName);
    setGManagerTitle(rec.managerTitle);
    setGLinkedObjId(rec.linkedObjectiveId ?? '');
    setGLinkedCourseId(rec.linkedCourseId ?? '');
    setGLinkedCourseTitle(rec.linkedCourseTitle ?? '');
    setGMetricName(rec.metricName ?? '');
    setGCurrentVal(rec.currentValue ?? '');
    setGTargetVal(rec.targetValue ?? '');
    setGActionItemsText(rec.actionItems.map((a) => a.text).join('\n'));
    setGNotes(rec.quarterlyReflectionNotes ?? '');
    setGrowthModalOpen(true);
  }

  function handleGrowthSubmit(e: FormEvent) {
    e.preventDefault();
    if (!gTitle.trim() || !gSkillName.trim()) return;

    const linkedObj = objectives.find((o) => o.id === gLinkedObjId);
    const parsedActionItems: GrowthActionItem[] = gActionItemsText
      .split('\n')
      .map((t) => t.trim())
      .filter((t) => t.length > 0)
      .map((text, idx) => ({ id: `act-${Date.now()}-${idx}`, text, completed: false }));

    if (editingGrowthRec) {
      const next = growthRecommendations.map((r) =>
        r.id === editingGrowthRec.id
          ? {
              ...r,
              title: gTitle.trim(),
              quarter: gQuarter,
              skillType: gSkillType,
              skillName: gSkillName.trim(),
              recommendationText: gRecommendationText.trim(),
              managerName: gManagerName.trim(),
              managerTitle: gManagerTitle.trim(),
              linkedObjectiveId: linkedObj ? linkedObj.id : null,
              linkedObjectiveTitle: linkedObj ? linkedObj.title : null,
              linkedCourseId: gLinkedCourseId.trim() || null,
              linkedCourseTitle: gLinkedCourseTitle.trim() || null,
              metricName: gMetricName.trim() || null,
              currentValue: gCurrentVal.trim() || null,
              targetValue: gTargetVal.trim() || null,
              actionItems: parsedActionItems.length > 0 ? parsedActionItems : r.actionItems,
              quarterlyReflectionNotes: gNotes.trim() || null,
            }
          : r,
      );
      updateGrowthRecommendations(next);
    } else {
      const newRec: EmployeeGrowthRecommendation = {
        id: `rec-${Date.now()}`,
        personId,
        tenantId,
        quarter: gQuarter,
        year: 2026,
        title: gTitle.trim(),
        skillType: gSkillType,
        skillName: gSkillName.trim(),
        recommendationText: gRecommendationText.trim(),
        managerName: gManagerName.trim() || 'Management Team',
        managerTitle: gManagerTitle.trim() || 'Leadership',
        assignedDate: '2026-01-15',
        status: 'InProgress',
        progressPercentage: 0,
        linkedObjectiveId: linkedObj ? linkedObj.id : null,
        linkedObjectiveTitle: linkedObj ? linkedObj.title : null,
        linkedCourseId: gLinkedCourseId.trim() || null,
        linkedCourseTitle: gLinkedCourseTitle.trim() || null,
        linkedCourseType: 'structured-learning',
        metricName: gMetricName.trim() || null,
        currentValue: gCurrentVal.trim() || null,
        targetValue: gTargetVal.trim() || null,
        actionItems: parsedActionItems,
        quarterlyReflectionNotes: gNotes.trim() || null,
      };
      updateGrowthRecommendations([newRec, ...growthRecommendations]);
    }
    setGrowthModalOpen(false);
  }

  function handleDeleteGrowth(id: string, title: string) {
    const confirmMsg = isEn ? `Delete recommendation "${title}"?` : `¿Eliminar recomendación "${title}"?`;
    if (window.confirm(confirmMsg)) {
      updateGrowthRecommendations(growthRecommendations.filter((r) => r.id !== id));
    }
  }

  // Filtered Growth Recommendations
  const filteredGrowth = useMemo(() => {
    return growthRecommendations.filter((r) => {
      const matchQuarter = selectedQuarter === 'all' || r.quarter === selectedQuarter;
      const matchSkill = growthSkillFilter === 'all' || r.skillType === growthSkillFilter;
      return matchQuarter && matchSkill;
    });
  }, [growthRecommendations, selectedQuarter, growthSkillFilter]);

  const technicalCount = growthRecommendations.filter((r) => r.skillType === 'technical').length;
  const softCount = growthRecommendations.filter((r) => r.skillType === 'soft').length;
  const allActionItems = growthRecommendations.flatMap((r) => r.actionItems);
  const completedActionsCount = allActionItems.filter((a) => a.completed).length;

  // Filtered Objectives
  const filteredObjectives = useMemo(() => {
    if (statusFilter === 'all') return objectives;
    return objectives.filter((o) => o.status === statusFilter);
  }, [objectives, statusFilter]);

  const completedCount = objectives.filter((o) => o.status === 'Completed').length;
  const inProgressCount = objectives.filter((o) => o.status === 'InProgress' || o.status === 'Active').length;
  const overallProgress =
    objectives.length === 0
      ? 0
      : Math.round(
          objectives.reduce((acc, o) => acc + o.progressPercentage, 0) / objectives.length,
        );

  const inputClass =
    'mt-1 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
  const labelClass = 'block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-white/50';

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-4 pb-16 sm:p-8">
      {/* Top Header Banner */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-indigo-700 via-indigo-600 to-violet-700 p-6 text-white shadow-xl shadow-indigo-500/20 dark:border-white/10 sm:p-8">
        <div className="flex flex-col gap-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 text-xl font-bold ring-1 ring-white/25 backdrop-blur">
              {initialsOf(user?.name ?? 'Empleado')}
            </span>
            <div>
              <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/75">
                <Target className="h-3.5 w-3.5" />
                {isEn ? 'Performance & Growth' : 'Desempeño y Crecimiento'}
              </p>
              <h1 className="mt-1 text-2xl font-bold">{user?.name ?? 'Empleado'}</h1>
              <p className="mt-0.5 text-sm text-white/85">
                {user?.jobRoleTitle ?? (isEn ? 'Unassigned Role' : 'Sin puesto asignado')}
                {user?.departmentName ? ` · ${user.departmentName}` : ''}
              </p>
            </div>
          </div>

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
                  strokeDasharray={`${overallProgress * 0.974} 1000`}
                  strokeLinecap="round"
                />
              </svg>
              <span className="absolute text-sm font-bold">{overallProgress}%</span>
            </div>
            <div className="text-sm">
              <p className="font-semibold">{isEn ? 'SMART Objectives Progress' : 'Cumplimiento de Objetivos'}</p>
              <p className="text-white/75">
                {completedCount} {isEn ? 'of' : 'de'} {objectives.length} {isEn ? 'completed' : 'completados'}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Main Tab Navigation */}
      <div className="border-b border-slate-200 dark:border-white/10">
        <nav className="-mb-px flex space-x-6 sm:space-x-8">
          <button
            type="button"
            onClick={() => setActiveTab('objectives')}
            className={`flex items-center gap-2.5 border-b-2 py-3.5 text-sm font-semibold transition ${
              activeTab === 'objectives'
                ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-400'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
            }`}
          >
            <Target className="h-4 w-4" />
            {isEn ? `My Objectives (${objectives.length})` : `Mis Objetivos (${objectives.length})`}
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('growth')}
            className={`flex items-center gap-2.5 border-b-2 py-3.5 text-sm font-semibold transition ${
              activeTab === 'growth'
                ? 'border-amber-600 text-amber-600 dark:border-amber-400 dark:text-amber-400'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
            }`}
          >
            <TrendingUp className="h-4 w-4" />
            {isEn
              ? `Areas to Improve & Growth (${growthRecommendations.length})`
              : `Áreas de Mejora & Crecimiento (${growthRecommendations.length})`}
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('role')}
            className={`flex items-center gap-2.5 border-b-2 py-3.5 text-sm font-semibold transition ${
              activeTab === 'role'
                ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-400'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
            }`}
          >
            <Briefcase className="h-4 w-4" />
            {isEn ? 'My Role & Job Description' : 'Descripción de Mi Puesto'}
          </button>
        </nav>
      </div>

      {/* ─────────────────────────────────────────────────────────────────── */}
      {/* TAB 1: MIS OBJETIVOS (SMART OBJECTIVES)                           */}
      {/* ─────────────────────────────────────────────────────────────────── */}
      {activeTab === 'objectives' && (
        <div className="space-y-6">
          {/* Alignment Info Banner */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 rounded-3xl border border-indigo-100 bg-gradient-to-r from-indigo-50/90 via-purple-50/50 to-violet-50/90 p-5 shadow-sm dark:border-indigo-500/20 dark:bg-indigo-500/10">
            <div className="flex items-start gap-3.5">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-indigo-600 text-white shadow-md shadow-indigo-500/20">
                <Compass className="h-5 w-5" />
              </span>
              <div>
                <h3 className="text-sm font-bold text-indigo-950 dark:text-indigo-200">
                  {isEn
                    ? 'SMART Alignment with Department & Company Goals'
                    : 'Alineación SMART con los Objetivos del Departamento'}
                </h3>
                <p className="mt-0.5 text-xs text-indigo-800/80 dark:text-indigo-300/80">
                  {isEn
                    ? `Your personal objectives are structured with SMART criteria and directly contribute to ${user?.departmentName || 'your department'}'s tactical goals.`
                    : `Tus objetivos individuales están estructurados con metodología SMART y contribuyen directamente a las metas tácticas de ${user?.departmentName || 'tu departamento'}.`}
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

          {/* Quick Metrics Bar */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Card className="p-4">
              <span className="text-xs text-slate-500 dark:text-white/50">{isEn ? 'Total Goals' : 'Total Metas'}</span>
              <p className="mt-1 text-xl font-bold text-slate-900 dark:text-white">{objectives.length}</p>
            </Card>
            <Card className="p-4">
              <span className="text-xs text-slate-500 dark:text-white/50">{isEn ? 'In Progress' : 'En Progreso'}</span>
              <p className="mt-1 text-xl font-bold text-amber-600 dark:text-amber-400">{inProgressCount}</p>
            </Card>
            <Card className="p-4">
              <span className="text-xs text-slate-500 dark:text-white/50">{isEn ? 'Completed' : 'Completados'}</span>
              <p className="mt-1 text-xl font-bold text-emerald-600 dark:text-emerald-400">{completedCount}</p>
            </Card>
            <Card className="p-4">
              <span className="text-xs text-slate-500 dark:text-white/50">{isEn ? 'Overall Progress' : 'Cumplimiento Global'}</span>
              <p className="mt-1 text-xl font-bold text-indigo-600 dark:text-indigo-400">{overallProgress}%</p>
            </Card>
          </div>

          {/* Filter Bar */}
          <div className="flex items-center justify-between">
            <div className="flex flex-wrap gap-2">
              {['all', 'Active', 'InProgress', 'Completed', 'Paused'].map((st) => (
                <button
                  key={st}
                  type="button"
                  onClick={() => setStatusFilter(st)}
                  className={`rounded-full px-3.5 py-1.5 text-xs font-medium transition ${
                    statusFilter === st
                      ? 'bg-indigo-600 text-white shadow-sm'
                      : 'bg-white text-slate-600 hover:bg-slate-100 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10'
                  }`}
                >
                  {st === 'all'
                    ? isEn ? 'All' : 'Todos'
                    : isEn ? STATUS_META[st as EmployeeObjective['status']]?.labelEn : STATUS_META[st as EmployeeObjective['status']]?.labelEs}
                </button>
              ))}
            </div>
          </div>

          {/* Objectives List */}
          {filteredObjectives.length === 0 ? (
            <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
              <Target className="h-10 w-10 text-slate-300 dark:text-white/20" />
              <h3 className="mt-3 text-sm font-semibold text-slate-900 dark:text-white">
                {isEn ? 'No objectives found' : 'No hay objetivos registrados'}
              </h3>
              <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
                {isEn
                  ? 'Define your first SMART objective aligned with your department goals.'
                  : 'Registra tu primer objetivo SMART alineado a las metas del departamento.'}
              </p>
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
              {filteredObjectives.map((obj) => {
                const meta = STATUS_META[obj.status] ?? STATUS_META.Active;
                return (
                  <Card
                    key={obj.id}
                    className="group relative p-6 transition hover:border-indigo-200 hover:shadow-md dark:hover:border-indigo-500/30"
                  >
                    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                      <div className="space-y-3 flex-1">
                        {/* Status + SMART + Quarter Badges */}
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

                        {/* Specific (Title & Description) */}
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

                        {/* Measurable (Metric & Method) */}
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

                        {/* Recommended Skills (Technical & Soft) */}
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

                        {/* Achievable (Action Plan) */}
                        {obj.actionPlan && (
                          <div className="text-xs text-slate-600 dark:text-white/70">
                            <span className="font-semibold text-slate-700 dark:text-white/90">
                              {isEn ? 'Action Plan & Strategy:' : 'Plan de Acción y Recursos:'}{' '}
                            </span>
                            {obj.actionPlan}
                          </div>
                        )}

                        {/* Relevant (Department Objective Alignment) */}
                        {obj.departmentObjectiveTitle ? (
                          <div className="space-y-1 pt-1">
                            <div className="flex flex-wrap items-center gap-1.5">
                              <span className="inline-flex items-center gap-1.5 rounded-xl bg-indigo-50 px-3 py-1 text-xs font-semibold text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300">
                                <Building2 className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                                {isEn ? 'Aligned with Dept Objective:' : 'Alineado al Objetivo del Depto:'}{' '}
                                <span className="font-bold">{obj.departmentObjectiveTitle}</span>
                              </span>
                              {obj.strategicGoalTitle && (
                                <span className="rounded-xl bg-purple-50 px-2.5 py-1 text-[11px] font-medium text-purple-700 dark:bg-purple-500/15 dark:text-purple-300">
                                  {isEn ? 'Corporate Goal:' : 'Meta Corporativa:'} {obj.strategicGoalTitle}
                                </span>
                              )}
                            </div>
                            {obj.businessImpact && (
                              <p className="text-[11px] text-slate-500 dark:text-white/50 pl-1">
                                <span className="font-semibold text-slate-600 dark:text-white/70">
                                  {isEn ? 'Value Impact:' : 'Impacto Esperado:'}
                                </span>{' '}
                                {obj.businessImpact}
                              </p>
                            )}
                          </div>
                        ) : (
                          <div className="pt-1">
                            <span className="text-[11px] text-amber-600 dark:text-amber-400 italic">
                              {isEn ? 'Independent Objective (No department link)' : 'Objetivo individual independiente'}
                            </span>
                          </div>
                        )}
                      </div>

                      {/* Right side: Progress Bar + Actions */}
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
                            onClick={() => handleDelete(obj.id, obj.title)}
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

      {/* ─────────────────────────────────────────────────────────────────── */}
      {/* TAB 2: AREAS TO IMPROVE & GROWTH RECOMMENDATIONS (QUARTERLY)       */}
      {/* ─────────────────────────────────────────────────────────────────── */}
      {activeTab === 'growth' && (
        <div className="space-y-6">
          {/* Growth Coaching Philosophy Banner */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 rounded-3xl border border-amber-200 bg-gradient-to-r from-amber-50/90 via-orange-50/50 to-amber-50/90 p-5 shadow-sm dark:border-amber-500/20 dark:bg-amber-500/10">
            <div className="flex items-start gap-3.5">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-500 to-orange-600 text-white shadow-md shadow-amber-500/20">
                <Lightbulb className="h-5 w-5" />
              </span>
              <div>
                <h3 className="text-sm font-bold text-amber-950 dark:text-amber-200">
                  {isEn
                    ? 'Quarterly Coaching & Areas for Growth (Management Recommendations)'
                    : 'Áreas de Mejora y Recomendaciones de Liderazgo (Seguimiento Trimestral)'}
                </h3>
                <p className="mt-0.5 text-xs text-amber-900/80 dark:text-amber-300/80 leading-relaxed">
                  {isEn
                    ? 'Continuous, actionable coaching from your managers to sharpen technical and soft competencies, directly connected to your quarterly objectives and Engram learning tracks.'
                    : 'Recomendaciones estratégicas de tu liderazgo para acelerar tu desarrollo en competencias técnicas y blandas, vinculadas a tus metas del trimestre y cursos interactivos de Engram.'}
                </p>
              </div>
            </div>

            <button
              type="button"
              onClick={openCreateGrowthModal}
              className="inline-flex items-center gap-2 self-start sm:self-auto rounded-full bg-gradient-to-r from-amber-600 to-orange-600 px-5 py-2.5 text-xs font-semibold text-white shadow-md shadow-amber-500/25 transition hover:brightness-110"
            >
              <Plus className="h-4 w-4" />
              {isEn ? 'Add Growth Area' : '+ Nueva Recomendación'}
            </button>
          </div>

          {/* Quick Metrics Bar for Growth */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Card className="p-4">
              <span className="text-xs text-slate-500 dark:text-white/50">{isEn ? 'Total Focus Areas' : 'Áreas de Enfoque'}</span>
              <p className="mt-1 text-xl font-bold text-slate-900 dark:text-white">{growthRecommendations.length}</p>
            </Card>
            <Card className="p-4">
              <span className="text-xs text-slate-500 dark:text-white/50">{isEn ? 'Technical Skills' : 'Habilidades Técnicas'}</span>
              <p className="mt-1 text-xl font-bold text-sky-600 dark:text-sky-400">{technicalCount}</p>
            </Card>
            <Card className="p-4">
              <span className="text-xs text-slate-500 dark:text-white/50">{isEn ? 'Soft Skills & Leadership' : 'Habilidades Blandas'}</span>
              <p className="mt-1 text-xl font-bold text-violet-600 dark:text-violet-400">{softCount}</p>
            </Card>
            <Card className="p-4">
              <span className="text-xs text-slate-500 dark:text-white/50">{isEn ? 'Milestones Mastered' : 'Hitos Completados'}</span>
              <p className="mt-1 text-xl font-bold text-emerald-600 dark:text-emerald-400">
                {completedActionsCount}/{allActionItems.length}
              </p>
            </Card>
          </div>

          {/* Quarter & Skill Filters */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
            {/* Quarter Selector */}
            <div className="flex flex-wrap items-center gap-1.5">
              <span className="text-xs font-semibold text-slate-500 dark:text-white/50 mr-1">
                {isEn ? 'Quarter:' : 'Trimestre:'}
              </span>
              {['all', 'Q1 2026', 'Q2 2026', 'Q3 2026', 'Q4 2026'].map((q) => (
                <button
                  key={q}
                  type="button"
                  onClick={() => setSelectedQuarter(q)}
                  className={`rounded-full px-3 py-1 text-xs font-semibold transition ${
                    selectedQuarter === q
                      ? 'bg-amber-500 text-white shadow-sm'
                      : 'bg-white text-slate-600 hover:bg-slate-100 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10'
                  }`}
                >
                  {q === 'all' ? (isEn ? 'All Quarters' : 'Todos') : q}
                </button>
              ))}
            </div>

            {/* Skill Category Filter */}
            <div className="flex items-center gap-1.5">
              <button
                type="button"
                onClick={() => setGrowthSkillFilter('all')}
                className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                  growthSkillFilter === 'all'
                    ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                    : 'bg-white text-slate-600 hover:bg-slate-100 dark:bg-white/5 dark:text-white/70'
                }`}
              >
                {isEn ? 'All Competencies' : 'Todas'}
              </button>
              <button
                type="button"
                onClick={() => setGrowthSkillFilter('technical')}
                className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                  growthSkillFilter === 'technical'
                    ? 'bg-sky-600 text-white'
                    : 'bg-white text-slate-600 hover:bg-slate-100 dark:bg-white/5 dark:text-white/70'
                }`}
              >
                {isEn ? 'Technical' : 'Técnicas'}
              </button>
              <button
                type="button"
                onClick={() => setGrowthSkillFilter('soft')}
                className={`rounded-full px-3 py-1 text-xs font-medium transition ${
                  growthSkillFilter === 'soft'
                    ? 'bg-violet-600 text-white'
                    : 'bg-white text-slate-600 hover:bg-slate-100 dark:bg-white/5 dark:text-white/70'
                }`}
              >
                {isEn ? 'Soft Skills' : 'Blandas'}
              </button>
            </div>
          </div>

          {/* Growth Cards List */}
          {filteredGrowth.length === 0 ? (
            <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
              <Lightbulb className="h-10 w-10 text-slate-300 dark:text-white/20" />
              <h3 className="mt-3 text-sm font-semibold text-slate-900 dark:text-white">
                {isEn ? 'No growth recommendations for this filter' : 'No hay recomendaciones de mejora para este filtro'}
              </h3>
              <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
                {isEn
                  ? 'Leadership recommendations for continuous quarterly improvement will appear here.'
                  : 'Las recomendaciones de tu liderazgo para mejora continua aparecerán aquí.'}
              </p>
              <button
                type="button"
                onClick={openCreateGrowthModal}
                className="mt-4 inline-flex items-center gap-2 rounded-full bg-amber-600 px-4 py-2 text-xs font-semibold text-white hover:bg-amber-700"
              >
                <Plus className="h-4 w-4" />
                {isEn ? 'Add Recommendation' : 'Agregar Recomendación'}
              </button>
            </Card>
          ) : (
            <div className="space-y-6">
              {filteredGrowth.map((rec) => {
                const isTech = rec.skillType === 'technical';
                const statusMeta = GROWTH_STATUS_META[rec.status] ?? GROWTH_STATUS_META.InProgress;
                return (
                  <Card
                    key={rec.id}
                    className="group relative p-6 transition hover:shadow-lg dark:hover:border-white/20"
                  >
                    <div className="space-y-5">
                      {/* Top Header Line: Skill Type + Target Skill + Quarter + Status */}
                      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-slate-100 pb-4 dark:border-white/5">
                        <div className="flex flex-wrap items-center gap-2">
                          <span
                            className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-bold ${
                              isTech
                                ? 'bg-sky-50 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300'
                                : 'bg-violet-50 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300'
                            }`}
                          >
                            {isTech ? <Cpu className="h-3.5 w-3.5" /> : <Sparkles className="h-3.5 w-3.5" />}
                            {isTech ? (isEn ? 'Technical Competency' : 'Competencia Técnica') : (isEn ? 'Soft Skill & Leadership' : 'Competencia Blanda')}
                          </span>

                          <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-700 dark:bg-white/10 dark:text-white/80">
                            {rec.skillName}
                          </span>

                          <span className="rounded-full border border-amber-300/60 bg-amber-50 px-2.5 py-1 text-xs font-bold text-amber-800 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-300">
                            {rec.quarter}
                          </span>
                        </div>

                        {/* Interactive Status Selector */}
                        <div className="flex items-center gap-2">
                          <span className="text-[11px] font-semibold text-slate-400">
                            {isEn ? 'Status:' : 'Estado:'}
                          </span>
                          <select
                            value={rec.status}
                            onChange={(e) => updateRecStatus(rec.id, e.target.value as EmployeeGrowthRecommendation['status'])}
                            className={`rounded-full px-3 py-1 text-xs font-bold outline-none cursor-pointer border border-transparent shadow-sm ${statusMeta.className}`}
                          >
                            <option value="NotStarted">{isEn ? 'Not Started' : 'Por Iniciar'}</option>
                            <option value="InProgress">{isEn ? 'In Progress' : 'En Desarrollo'}</option>
                            <option value="Applied">{isEn ? 'Applied in Role' : 'Aplicado en el Puesto'}</option>
                            <option value="Mastered">{isEn ? 'Mastered' : 'Dominado / Superado'}</option>
                          </select>

                          <div className="flex items-center gap-1 opacity-0 transition group-hover:opacity-100 ml-2">
                            <button
                              type="button"
                              onClick={() => openEditGrowthModal(rec)}
                              className="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                            >
                              <Pencil className="h-3.5 w-3.5" />
                            </button>
                            <button
                              type="button"
                              onClick={() => handleDeleteGrowth(rec.id, rec.title)}
                              className="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400"
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </button>
                          </div>
                        </div>
                      </div>

                      {/* Title */}
                      <h3 className="text-lg font-bold text-slate-900 dark:text-white">
                        {rec.title}
                      </h3>

                      {/* Manager Coaching Quote Box */}
                      <div className="relative rounded-2xl border border-slate-200 bg-slate-50/70 p-4 pl-5 dark:border-white/10 dark:bg-white/[0.03]">
                        <MessageSquareQuote className="absolute top-3 right-4 h-6 w-6 text-slate-300 dark:text-white/10" />
                        <div className="flex items-center gap-2 mb-2">
                          <UserCheck className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
                          <span className="text-xs font-bold text-slate-900 dark:text-white">
                            {rec.managerName}
                          </span>
                          <span className="text-xs text-slate-500 dark:text-white/50">
                            · {rec.managerTitle} ({rec.assignedDate})
                          </span>
                        </div>
                        <p className="text-sm text-slate-700 dark:text-white/80 leading-relaxed italic">
                          "{rec.recommendationText}"
                        </p>
                      </div>

                      {/* Alignment with Objective & Course */}
                      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                        {/* Linked Objective */}
                        <div className="rounded-2xl border border-indigo-100 bg-indigo-50/40 p-3.5 text-xs dark:border-indigo-500/20 dark:bg-indigo-500/5">
                          <div className="flex items-center gap-2 text-indigo-700 dark:text-indigo-300 font-bold uppercase tracking-wider text-[10px]">
                            <Target className="h-3.5 w-3.5" />
                            <span>{isEn ? 'Impacts SMART Objective:' : 'Impulsa el Objetivo SMART:'}</span>
                          </div>
                          <p className="mt-1 font-semibold text-slate-900 dark:text-white text-xs">
                            {rec.linkedObjectiveTitle || (isEn ? 'General Role Excellence' : 'Excelencia General en el Puesto')}
                          </p>
                        </div>

                        {/* Linked Course / Learning Track */}
                        <div className="rounded-2xl border border-violet-100 bg-violet-50/40 p-3.5 text-xs dark:border-violet-500/20 dark:bg-violet-500/5 flex items-center justify-between gap-3">
                          <div>
                            <div className="flex items-center gap-2 text-violet-700 dark:text-violet-300 font-bold uppercase tracking-wider text-[10px]">
                              <GraduationCap className="h-3.5 w-3.5" />
                              <span>{isEn ? 'Recommended Learning Track:' : 'Ruta de Aprendizaje Recomendada:'}</span>
                            </div>
                            <p className="mt-1 font-semibold text-slate-900 dark:text-white text-xs truncate max-w-[200px]">
                              {rec.linkedCourseTitle || rec.skillName}
                            </p>
                          </div>
                          {rec.linkedCourseId ? (
                            <Link
                              to={`/structured-learning/${rec.linkedCourseId}`}
                              className="inline-flex shrink-0 items-center gap-1.5 rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-3 py-1.5 text-xs font-semibold text-white shadow-sm hover:brightness-110"
                            >
                              <PlayCircle className="h-3.5 w-3.5" />
                              {isEn ? 'Launch Course' : 'Iniciar Curso'}
                            </Link>
                          ) : (
                            <span className="text-[11px] text-violet-600 dark:text-violet-300 font-semibold shrink-0">
                              ✓ En Catálogo
                            </span>
                          )}
                        </div>
                      </div>

                      {/* Measurable Action Checklist */}
                      {rec.actionItems.length > 0 && (
                        <div className="rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/[0.02] space-y-3">
                          <div className="flex items-center justify-between">
                            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700 dark:text-white/80">
                              {isEn ? 'Measurable Action Milestones & Deliverables' : 'Hitos de Acción y Entregables Medibles'}
                            </h4>
                            <span className="text-xs font-semibold text-slate-500">
                              {rec.actionItems.filter((a) => a.completed).length} / {rec.actionItems.length} {isEn ? 'Done' : 'Listos'}
                            </span>
                          </div>

                          <div className="space-y-2">
                            {rec.actionItems.map((item) => (
                              <button
                                key={item.id}
                                type="button"
                                onClick={() => toggleActionItem(rec.id, item.id)}
                                className={`flex w-full items-start gap-3 rounded-xl p-2.5 text-left text-xs transition ${
                                  item.completed
                                    ? 'bg-emerald-50/90 text-emerald-950 dark:bg-emerald-500/15 dark:text-emerald-200 font-medium'
                                    : 'bg-slate-50 text-slate-800 hover:bg-slate-100 dark:bg-white/5 dark:text-white/80 dark:hover:bg-white/10'
                                }`}
                              >
                                {item.completed ? (
                                  <CheckCircle2 className="h-4 w-4 text-emerald-600 shrink-0 mt-0.5" />
                                ) : (
                                  <Circle className="h-4 w-4 text-slate-400 shrink-0 mt-0.5" />
                                )}
                                <span className={item.completed ? 'text-emerald-950 dark:text-emerald-200' : 'text-slate-800 dark:text-white/90'}>
                                  {item.text}
                                </span>
                              </button>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Target KPI / Metric & Quarterly Reflection Notes */}
                      {(rec.metricName || rec.quarterlyReflectionNotes) && (
                        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 text-xs">
                          {rec.metricName && (
                            <div className="rounded-xl bg-slate-50 p-3 dark:bg-white/5 space-y-1">
                              <span className="font-bold uppercase tracking-wider text-[10px] text-slate-400">
                                {isEn ? 'Expected Metric Target' : 'Métrica de Medición'}
                              </span>
                              <p className="font-semibold text-slate-800 dark:text-white/90">{rec.metricName}</p>
                              {rec.currentValue && (
                                <p className="text-[11px] text-slate-500">
                                  <strong>{isEn ? 'Current:' : 'Actual:'}</strong> {rec.currentValue}
                                  {rec.targetValue && ` · Target: ${rec.targetValue}`}
                                </p>
                              )}
                            </div>
                          )}

                          {rec.quarterlyReflectionNotes && (
                            <div className="rounded-xl bg-slate-50 p-3 dark:bg-white/5 space-y-1">
                              <span className="font-bold uppercase tracking-wider text-[10px] text-slate-400">
                                {isEn ? 'Quarterly Evidence & Progress Note' : 'Evidencia y Progreso del Trimestre'}
                              </span>
                              <p className="text-[11px] text-slate-700 dark:text-white/80 italic leading-relaxed">
                                {rec.quarterlyReflectionNotes}
                              </p>
                            </div>
                          )}
                        </div>
                      )}

                      {/* Progress Bar */}
                      <div className="space-y-1.5 pt-2">
                        <div className="flex items-center justify-between text-xs font-bold">
                          <span className="text-slate-600 dark:text-white/70">
                            {isEn ? 'Growth Mastery Progress' : 'Nivel de Dominio Alcanzado'}
                          </span>
                          <span className="text-amber-600 dark:text-amber-400">
                            {rec.progressPercentage}%
                          </span>
                        </div>
                        <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                          <div
                            className={`h-full rounded-full transition-all duration-300 ${
                              isTech
                                ? 'bg-gradient-to-r from-sky-500 to-indigo-600'
                                : 'bg-gradient-to-r from-amber-500 to-violet-600'
                            }`}
                            style={{ width: `${rec.progressPercentage}%` }}
                          />
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

      {/* ─────────────────────────────────────────────────────────────────── */}
      {/* TAB 3: MI PUESTO / JOB ROLE FULL SPECIFICATION                    */}
      {/* ─────────────────────────────────────────────────────────────────── */}
      {activeTab === 'role' && (
        <div className="space-y-6">
          {jobRoleQuery.isLoading && (
            <LoadingBackdrop
              title={isEn ? 'Loading Job Role...' : 'Cargando descripción de puesto...'}
              description={isEn ? 'Retrieving position requirements and competencies.' : 'Obteniendo requisitos y competencias del puesto.'}
            />
          )}

          {!jobRoleQuery.isLoading && !jobRole && (
            <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
              <Briefcase className="h-10 w-10 text-slate-300 dark:text-white/20" />
              <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">
                {isEn ? 'No Job Role Assigned' : 'Sin puesto asignado'}
              </h3>
              <p className="mt-1 max-w-md text-xs text-slate-500 dark:text-white/50">
                {isEn
                  ? 'Your HR Administrator has not linked a formal job description to your profile yet.'
                  : 'Tu Administrador de RRHH aún no ha vinculado una descripción de puesto a tu perfil.'}
              </p>
            </Card>
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

              {/* Responsibilities & Activities */}
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
                                  STATUS_META[course.trainingStatus as EmployeeObjective['status']]?.className ?? STATUS_META.Active.className
                                }`}
                              >
                                {isEn ? STATUS_META[course.trainingStatus as EmployeeObjective['status']]?.labelEn ?? course.trainingStatus : STATUS_META[course.trainingStatus as EmployeeObjective['status']]?.labelEs ?? course.trainingStatus}
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
                            <a
                              href={buildEngramProgramUrl(course.matchedProgramId)}
                              target="_blank"
                              rel="noreferrer"
                              className="inline-flex items-center gap-1 rounded-full bg-indigo-600 px-3 py-1 text-xs font-semibold text-white transition hover:bg-indigo-700"
                            >
                              <ExternalLink className="h-3.5 w-3.5" />
                              {isEn ? 'Open Program' : 'Abrir Programa'}
                            </a>
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
                                  STATUS_META[course.trainingStatus as EmployeeObjective['status']]?.className ?? STATUS_META.Active.className
                                }`}
                              >
                                {isEn ? STATUS_META[course.trainingStatus as EmployeeObjective['status']]?.labelEn ?? course.trainingStatus : STATUS_META[course.trainingStatus as EmployeeObjective['status']]?.labelEs ?? course.trainingStatus}
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

              {/* Tools & Processes */}
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
                      {isEn ? 'Required Processes' : 'Procesos Obligatorios'}
                    </h3>
                  </div>
                  <div className="space-y-3">
                    {jobRole.departmentProcesses.length > 0 && (
                      <div>
                        <span className="text-[11px] font-bold uppercase text-slate-400">{isEn ? 'Department' : 'Departamento'}</span>
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

      {/* ─────────────────────────────────────────────────────────────────── */}
      {/* OBJECTIVE CREATE / EDIT MODAL (SMART FRAMEWORK)                    */}
      {/* ─────────────────────────────────────────────────────────────────── */}
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

            <form onSubmit={handleSubmit} className="mt-5 space-y-6">
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
                    {departmentObjectives.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.title} ({d.targetQuarter})
                      </option>
                    ))}
                  </select>

                  {/* Selected Dept Objective Preview */}
                  {(() => {
                    const preview = departmentObjectives.find((d) => d.id === formDeptObjId);
                    if (!preview) return null;
                    return (
                      <div className="mt-3 rounded-xl border border-purple-200/80 bg-white p-3.5 text-xs shadow-sm dark:border-purple-500/30 dark:bg-slate-900/90">
                        <span className="text-[10px] font-bold uppercase text-purple-600 dark:text-purple-400">
                          {isEn ? 'Department Objective Target' : 'Meta del Departamento'}
                        </span>
                        <h4 className="mt-0.5 text-xs font-semibold text-slate-900 dark:text-white">
                          {preview.title}
                        </h4>
                        {preview.description && (
                          <p className="mt-1 text-[11px] text-slate-600 dark:text-white/70">
                            {preview.description}
                          </p>
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

      {/* ─────────────────────────────────────────────────────────────────── */}
      {/* GROWTH RECOMMENDATION CREATE / EDIT MODAL                           */}
      {/* ─────────────────────────────────────────────────────────────────── */}
      {growthModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm overflow-y-auto">
          <div className="my-8 w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-3xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900 sm:p-8">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-white/10">
              <div className="flex items-center gap-3">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-500 to-orange-600 text-white shadow-md shadow-amber-500/20">
                  <TrendingUp className="h-5 w-5" />
                </span>
                <div>
                  <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                    {editingGrowthRec
                      ? isEn ? 'Edit Growth Focus Recommendation' : 'Editar Recomendación de Crecimiento'
                      : isEn ? 'Add Growth Focus Recommendation' : 'Agregar Área de Mejora y Crecimiento'}
                  </h2>
                  <p className="text-xs text-slate-500 dark:text-white/50">
                    {isEn
                      ? 'Forward-looking guidance from leadership to sharpen competencies and impact objectives.'
                      : 'Recomendaciones constructivas de liderazgo para acelerar tu desarrollo profesional.'}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setGrowthModalOpen(false)}
                className="rounded-full p-1.5 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <form onSubmit={handleGrowthSubmit} className="mt-5 space-y-6">
              {/* 1. Skill Type & Target Skill from Job Description */}
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label className={labelClass}>{isEn ? 'Competency Type *' : 'Tipo de Competencia *'}</label>
                  <select
                    value={gSkillType}
                    onChange={(e) => handleSkillTypeChange(e.target.value as 'technical' | 'soft')}
                    className={inputClass}
                  >
                    <option value="technical">{isEn ? 'Technical Competency (Job Role)' : 'Competencia Técnica (Del Puesto)'}</option>
                    <option value="soft">{isEn ? 'Soft Skill / Leadership (Job Role)' : 'Competencia Blanda / Liderazgo (Del Puesto)'}</option>
                  </select>
                </div>

                <div>
                  <label className={labelClass}>
                    {isEn ? 'Required Job Role Skill *' : 'Habilidad Requerida del Puesto *'}
                  </label>
                  <select
                    required
                    value={gSkillName}
                    onChange={(e) => handleSkillChange(e.target.value)}
                    className={inputClass}
                  >
                    {skillsForSelectedType.length === 0 ? (
                      <option value={gSkillName}>{gSkillName || (isEn ? 'Select a skill' : 'Seleccionar habilidad')}</option>
                    ) : (
                      skillsForSelectedType.map((sk) => (
                        <option key={sk} value={sk}>
                          {sk}
                        </option>
                      ))
                    )}
                  </select>
                </div>
              </div>

              {/* 2. Recommendation Title & Quarter */}
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                <div className="sm:col-span-2">
                  <label className={labelClass}>{isEn ? 'Recommendation Focus Title *' : 'Título del Enfoque *'}</label>
                  <input
                    required
                    value={gTitle}
                    onChange={(e) => setGTitle(e.target.value)}
                    placeholder={isEn ? 'e.g. Elevate Microservices Observability Practices' : 'p. ej. Elevar Prácticas de Observabilidad en Microservicios'}
                    className={inputClass}
                  />
                </div>
                <div>
                  <label className={labelClass}>{isEn ? 'Target Quarter' : 'Trimestre Meta'}</label>
                  <input
                    value={gQuarter}
                    onChange={(e) => setGQuarter(e.target.value)}
                    placeholder="Q1 2026"
                    className={inputClass}
                  />
                </div>
              </div>

              {/* 3. Management Coaching Narrative */}
              <div>
                <label className={labelClass}>
                  {isEn ? 'Management Coaching Guidance & Advice *' : 'Recomendación y Guía del Líder / Manager *'}
                </label>
                <textarea
                  required
                  rows={3}
                  value={gRecommendationText}
                  onChange={(e) => setGRecommendationText(e.target.value)}
                  placeholder={isEn ? 'Constructive coaching explaining why this area is critical and how to level up...' : 'Explica la razón estratégica y cómo el colaborador puede superar este nivel...'}
                  className={inputClass}
                />
              </div>

              {/* 4. Manager Credentials */}
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label className={labelClass}>{isEn ? 'Manager / Mentor Name' : 'Nombre del Evaluador / Manager'}</label>
                  <input
                    value={gManagerName}
                    onChange={(e) => setGManagerName(e.target.value)}
                    placeholder="Carlos Morales"
                    className={inputClass}
                  />
                </div>
                <div>
                  <label className={labelClass}>{isEn ? 'Manager Title' : 'Cargo del Manager'}</label>
                  <input
                    value={gManagerTitle}
                    onChange={(e) => setGManagerTitle(e.target.value)}
                    placeholder="Director de Ingeniería"
                    className={inputClass}
                  />
                </div>
              </div>

              {/* 5. Connected Objective & Assigned Engram Course (Auto-Linked) */}
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label className={labelClass}>{isEn ? 'Empowers SMART Objective' : 'Impulsa el Objetivo SMART'}</label>
                  <select
                    value={gLinkedObjId}
                    onChange={(e) => setGLinkedObjId(e.target.value)}
                    className={inputClass}
                  >
                    <option value="">{isEn ? '— General Role Mastery —' : '— Excelencia General del Puesto —'}</option>
                    {objectives.map((o) => (
                      <option key={o.id} value={o.id}>
                        {o.title}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className={labelClass}>{isEn ? 'Assigned Engram Class / Course' : 'Clase / Curso Engram Asignado'}</label>
                  {(() => {
                    const course = resolveCourseForSkill(gSkillName);
                    return (
                      <div className="mt-1 flex items-center justify-between gap-2 rounded-xl border border-violet-200/90 bg-violet-50/70 p-2.5 text-xs shadow-sm dark:border-violet-500/20 dark:bg-violet-500/10">
                        <div className="flex items-center gap-2 min-w-0">
                          <GraduationCap className="h-4 w-4 shrink-0 text-violet-600 dark:text-violet-400" />
                          <div className="min-w-0">
                            <p className="font-semibold text-slate-900 dark:text-white truncate">
                              {course.hasCourse ? course.title : gSkillName}
                            </p>
                            <p className="text-[10px] text-slate-500 dark:text-white/60">
                              {course.hasCourse
                                ? (isEn ? '✓ Auto-linked to Job Skill Engrams' : '✓ Vinculado a Engramas del Puesto')
                                : (isEn ? 'Role-specific competency roadmap' : 'Ruta formativa del puesto')}
                            </p>
                          </div>
                        </div>
                        {course.hasCourse && course.matchedCapabilityId && (
                          <span className="rounded-full bg-violet-200/60 px-2 py-0.5 text-[10px] font-bold text-violet-800 dark:bg-violet-500/25 dark:text-violet-200 shrink-0">
                            {course.matchedCourseType === 'structured-learning' ? 'Structured Learning' : 'Engram Graph'}
                          </span>
                        )}
                      </div>
                    );
                  })()}
                </div>
              </div>

              {/* 6. Action Items (one per line) */}
              <div>
                <label className={labelClass}>
                  {isEn ? 'Action Milestones & Deliverables (one per line)' : 'Hitos de Acción y Entregables (uno por línea)'}
                </label>
                <textarea
                  rows={3}
                  value={gActionItemsText}
                  onChange={(e) => setGActionItemsText(e.target.value)}
                  placeholder={isEn ? "1. Complete course modules\n2. Implement tracing PR\n3. Demo to team" : "1. Completar módulos del curso\n2. Implementar tracing en PR\n3. Demostración técnica al equipo"}
                  className={inputClass}
                />
              </div>

              {/* 7. Expected Metric Target */}
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <div>
                  <label className={labelClass}>{isEn ? 'Key Metric Name' : 'Nombre de Métrica'}</label>
                  <input
                    value={gMetricName}
                    onChange={(e) => setGMetricName(e.target.value)}
                    placeholder={isEn ? 'e.g. Tracing Adoption Rate' : 'p. ej. Adopción de Trazas'}
                    className={inputClass}
                  />
                </div>
                <div>
                  <label className={labelClass}>{isEn ? 'Current Level' : 'Nivel Actual'}</label>
                  <input
                    value={gCurrentVal}
                    onChange={(e) => setGCurrentVal(e.target.value)}
                    placeholder="2 of 4 services"
                    className={inputClass}
                  />
                </div>
                <div>
                  <label className={labelClass}>{isEn ? 'Target Level' : 'Nivel Meta'}</label>
                  <input
                    value={gTargetVal}
                    onChange={(e) => setGTargetVal(e.target.value)}
                    placeholder="100% services"
                    className={inputClass}
                  />
                </div>
              </div>

              <div className="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4 dark:border-white/10">
                <button
                  type="button"
                  onClick={() => setGrowthModalOpen(false)}
                  className="rounded-full px-5 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
                >
                  {isEn ? 'Cancel' : 'Cancelar'}
                </button>
                <button
                  type="submit"
                  disabled={!gTitle.trim() || !gSkillName.trim()}
                  className="rounded-full bg-gradient-to-r from-amber-600 to-orange-600 px-6 py-2.5 text-sm font-semibold text-white shadow-md shadow-amber-500/20 transition hover:brightness-110 disabled:opacity-50"
                >
                  {editingGrowthRec ? (isEn ? 'Save Changes' : 'Guardar Cambios') : (isEn ? 'Create Recommendation' : 'Crear Recomendación')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
