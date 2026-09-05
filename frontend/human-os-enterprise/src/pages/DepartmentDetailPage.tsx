import { useEffect, useState, useMemo, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import {
  ArrowLeft,
  Building2,
  Check,
  Briefcase,
  Users,
  Target,
  BarChart3,
  Plus,
  Pencil,
  Trash2,
  Clock,
  X,
  Unlink,
  Link2,
  Compass,
  ExternalLink,
  Sparkles,
  Activity,
  Calculator,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  attachDepartmentJobRole,
  createDepartment,
  detachDepartmentJobRole,
  getDepartment,
  getDepartments,
  getJobRoles,
  getTenantPeople,
  updateDepartment,
  type SaveDepartmentRequest,
} from '@/api/orgStructureApi';
import {
  getStrategicGoals,
  getCompanyDirection,
  getDepartmentObjectives,
  saveDepartmentObjectives,
  getDepartmentKpis,
  saveDepartmentKpis,
  type DepartmentObjective,
  type DepartmentKPI,
} from '@/api/strategicGoalsApi';

export type { DepartmentObjective, DepartmentKPI };

const CATEGORY_OPTIONS_ES = [
  'Recursos Humanos',
  'Finanzas & Contabilidad',
  'Ingeniería & Tecnología',
  'Ventas & Comercial',
  'Marketing & Comunicación',
  'Operaciones & Logística',
  'Dirección Ejecutiva',
  'Legal & Cumplimiento',
  'Atención a Clientes',
  'Producto & Diseño',
  'Otro',
];

const CATEGORY_OPTIONS_EN = [
  'Human Resources',
  'Finance & Accounting',
  'Engineering & Technology',
  'Sales & Commercial',
  'Marketing & Communication',
  'Operations & Logistics',
  'Executive Management',
  'Legal & Compliance',
  'Customer Support',
  'Product & Design',
  'Other',
];

const emptyForm = {
  name: '',
  code: '',
  description: '',
  category: '',
  location: '',
  costCenter: '',
  missionStatement: '',
  leaderPersonId: '',
  parentDepartmentId: '',
};

type DepartmentFormState = typeof emptyForm;
type TabType = 'profile' | 'objectives' | 'team' | 'roles' | 'kpis';

const STATUS_META: Record<DepartmentObjective['status'], { labelKey: string; className: string }> = {
  Active: {
    labelKey: 'businessGoals.statusActive',
    className: 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300',
  },
  InProgress: {
    labelKey: 'businessGoals.statusInProgress',
    className: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  },
  Completed: {
    labelKey: 'businessGoals.statusCompleted',
    className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  },
  Paused: {
    labelKey: 'businessGoals.statusPaused',
    className: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/50',
  },
};

export function DepartmentDetailPage() {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const { departmentId } = useParams();
  const isNew = !departmentId || departmentId === 'nuevo';
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const isEn = i18n.language?.startsWith('en');
  const categoryOptions = isEn ? CATEGORY_OPTIONS_EN : CATEGORY_OPTIONS_ES;

  const [activeTab, setActiveTab] = useState<TabType>('profile');
  const [form, setForm] = useState<DepartmentFormState>(emptyForm);
  const [savedSuccess, setSavedSuccess] = useState(false);

  // Load Company Strategic Goals & Direction from corporate level
  const companyGoals = useMemo(() => getStrategicGoals(tenantId, isEn), [tenantId, isEn]);
  const companyDirection = useMemo(() => getCompanyDirection(tenantId, isEn), [tenantId, isEn]);

  // Department Objectives state
  const [objectives, setObjectives] = useState<DepartmentObjective[]>(() => {
    if (isNew || !departmentId) return [];
    return getDepartmentObjectives(tenantId, departmentId, isEn);
  });

  // Department KPIs state
  const [kpis, setKpis] = useState<DepartmentKPI[]>(() => {
    if (isNew || !departmentId) return [];
    return getDepartmentKpis(tenantId, departmentId, isEn);
  });

  useEffect(() => {
    if (!isNew && departmentId) {
      setObjectives(getDepartmentObjectives(tenantId, departmentId, isEn));
      setKpis(getDepartmentKpis(tenantId, departmentId, isEn));
    }
  }, [tenantId, departmentId, isEn, isNew]);

  function saveObjectivesState(next: DepartmentObjective[]) {
    setObjectives(next);
    if (!isNew && departmentId) {
      saveDepartmentObjectives(tenantId, departmentId, next);
    }
  }

  function saveKpisState(next: DepartmentKPI[]) {
    setKpis(next);
    if (!isNew && departmentId) {
      saveDepartmentKpis(tenantId, departmentId, next);
    }
  }

  // Load department details if editing
  const departmentQuery = useQuery({
    queryKey: ['department', departmentId],
    queryFn: () => getDepartment(departmentId!),
    enabled: !isNew && !!departmentId,
  });

  // Load parent candidates
  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });

  // Load people in tenant
  const peopleQuery = useQuery({
    queryKey: ['tenant-people', tenantId],
    queryFn: () => getTenantPeople(tenantId),
  });

  // Filter team members belonging to this department
  const teamMembers = useMemo(() => {
    if (isNew || !departmentId) return [];
    return (peopleQuery.data ?? []).filter((p) => p.departmentId === departmentId);
  }, [peopleQuery.data, departmentId, isNew]);

  // Load job roles belonging to this department
  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId, departmentId],
    queryFn: () => getJobRoles(tenantId, departmentId),
    enabled: !isNew && !!departmentId,
  });

  // Load all tenant job roles to allow attaching existing roles
  const allJobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
    enabled: !isNew && !!departmentId,
  });

  const jobRoles = jobRolesQuery.data ?? [];

  const attachedRoleIds = useMemo(
    () => new Set(jobRoles.map((r) => r.jobRoleId)),
    [jobRoles],
  );

  const availableRolesToAttach = useMemo(
    () => (allJobRolesQuery.data ?? []).filter((r) => !attachedRoleIds.has(r.jobRoleId)),
    [allJobRolesQuery.data, attachedRoleIds],
  );

  // Attach / Detach modal and mutations
  const [attachModalOpen, setAttachModalOpen] = useState(false);
  const [selectedRoleIdToAttach, setSelectedRoleIdToAttach] = useState('');

  const detachMutation = useMutation({
    mutationFn: (roleId: string) => detachDepartmentJobRole(departmentId!, roleId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId, departmentId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['departments', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['department', departmentId] });
    },
  });

  const attachMutation = useMutation({
    mutationFn: (roleId: string) => attachDepartmentJobRole(departmentId!, roleId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId, departmentId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['departments', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['department', departmentId] });
      setAttachModalOpen(false);
      setSelectedRoleIdToAttach('');
    },
  });

  function handleDetachRole(roleId: string, title: string) {
    const deptName = form.name || t('departmentDetail.newTitle');
    if (window.confirm(t('departmentDetail.detachRoleConfirm', { title, department: deptName }))) {
      detachMutation.mutate(roleId);
    }
  }

  function handleAttachSubmit(e: FormEvent) {
    e.preventDefault();
    if (!selectedRoleIdToAttach) return;
    attachMutation.mutate(selectedRoleIdToAttach);
  }

  useEffect(() => {
    if (departmentQuery.data) {
      const d = departmentQuery.data;
      setForm({
        name: d.name ?? '',
        code: d.code ?? '',
        description: d.description ?? '',
        category: d.category ?? '',
        location: d.location ?? '',
        costCenter: d.costCenter ?? '',
        missionStatement: d.missionStatement ?? '',
        leaderPersonId: d.leaderPersonId ?? '',
        parentDepartmentId: d.parentDepartmentId ?? '',
      });
    }
  }, [departmentQuery.data]);

  function invalidate() {
    void queryClient.invalidateQueries({ queryKey: ['departments', tenantId] });
    if (!isNew && departmentId) {
      void queryClient.invalidateQueries({ queryKey: ['department', departmentId] });
    }
  }

  const saveMutation = useMutation({
    mutationFn: (body: SaveDepartmentRequest) =>
      isNew ? createDepartment(tenantId, body) : updateDepartment(departmentId!, body),
    onSuccess: (saved) => {
      invalidate();
      setSavedSuccess(true);
      setTimeout(() => setSavedSuccess(false), 3000);
      if (isNew) {
        navigate(`/departamentos/${saved.departmentId}`, { replace: true });
      }
    },
  });

  function handleChange(key: keyof DepartmentFormState, value: string) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const body: SaveDepartmentRequest = {
      name: form.name,
      code: form.code || null,
      description: form.description || null,
      category: form.category || null,
      location: form.location || null,
      costCenter: form.costCenter || null,
      missionStatement: form.missionStatement || null,
      leaderPersonId: form.leaderPersonId || null,
      parentDepartmentId: form.parentDepartmentId || null,
    };
    saveMutation.mutate(body);
  }

  // Modals for Objectives and KPIs
  const [objModalOpen, setObjModalOpen] = useState(false);
  const [editingObj, setEditingObj] = useState<DepartmentObjective | null>(null);
  const [objTitle, setObjTitle] = useState('');
  const [objDesc, setObjDesc] = useState('');
  const [objQuarter, setObjQuarter] = useState('Q4 2026');
  const [objDate, setObjDate] = useState('2026-12-31');
  const [objProgress, setObjProgress] = useState(0);
  const [objStatus, setObjStatus] = useState<DepartmentObjective['status']>('Active');
  const [objStrategicGoalId, setObjStrategicGoalId] = useState<string>('');
  const [objMetricName, setObjMetricName] = useState('');
  const [objCurrentValue, setObjCurrentValue] = useState<string>('');
  const [objTargetValue, setObjTargetValue] = useState<string>('');
  const [objUnit, setObjUnit] = useState('%');
  const [objMeasurementMethod, setObjMeasurementMethod] = useState('');
  const [objActionPlan, setObjActionPlan] = useState('');
  const [objBusinessImpact, setObjBusinessImpact] = useState('');
  const [showAdvancedDetails, setShowAdvancedDetails] = useState(false);

  function openCreateObjModal() {
    setEditingObj(null);
    setObjTitle('');
    setObjDesc('');
    setObjQuarter('Q4 2026');
    setObjDate('2026-12-31');
    setObjProgress(0);
    setObjStatus('Active');
    setObjStrategicGoalId('');
    setObjMetricName('');
    setObjCurrentValue('');
    setObjTargetValue('');
    setObjUnit('%');
    setObjMeasurementMethod('');
    setObjActionPlan('');
    setObjBusinessImpact('');
    setShowAdvancedDetails(false);
    setObjModalOpen(true);
  }

  function openEditObjModal(o: DepartmentObjective) {
    setEditingObj(o);
    setObjTitle(o.title);
    setObjDesc(o.description);
    setObjQuarter(o.targetQuarter);
    setObjDate(o.targetDate);
    setObjProgress(o.progressPercentage);
    setObjStatus(o.status);
    setObjStrategicGoalId(o.strategicGoalId ?? '');
    setObjMetricName(o.metricName ?? '');
    setObjCurrentValue(o.currentValue != null ? String(o.currentValue) : '');
    setObjTargetValue(o.targetValue != null ? String(o.targetValue) : '');
    setObjUnit(o.unit ?? '%');
    setObjMeasurementMethod(o.measurementMethod ?? '');
    setObjActionPlan(o.actionPlan ?? '');
    setObjBusinessImpact(o.businessImpact ?? '');
    setShowAdvancedDetails(Boolean(o.actionPlan || o.businessImpact || o.measurementMethod));
    setObjModalOpen(true);
  }

  function autoCalculateProgress() {
    const cur = parseFloat(objCurrentValue);
    const tgt = parseFloat(objTargetValue);
    if (!isNaN(cur) && !isNaN(tgt) && tgt !== 0) {
      const calculated = Math.min(100, Math.max(0, Math.round((cur / tgt) * 100)));
      setObjProgress(calculated);
    }
  }

  function handleObjSubmit(e: FormEvent) {
    e.preventDefault();
    if (!objTitle.trim()) return;

    const selectedGoal = companyGoals.find((g) => g.id === objStrategicGoalId);
    const pillar = selectedGoal ? companyDirection.pillars.find((p) => p.id === selectedGoal.pillarId) : null;

    const curValNum = objCurrentValue.trim() !== '' && !isNaN(Number(objCurrentValue)) ? Number(objCurrentValue) : null;
    const tgtValNum = objTargetValue.trim() !== '' && !isNaN(Number(objTargetValue)) ? Number(objTargetValue) : null;

    if (editingObj) {
      const next = objectives.map((o) =>
        o.id === editingObj.id
          ? {
              ...o,
              title: objTitle.trim(),
              description: objDesc.trim(),
              targetQuarter: objQuarter,
              targetDate: objDate,
              progressPercentage: Number(objProgress),
              status: objStatus,
              strategicGoalId: selectedGoal ? selectedGoal.id : null,
              strategicGoalTitle: selectedGoal ? selectedGoal.title : null,
              pillarTitle: pillar ? pillar.title : null,
              metricName: objMetricName.trim() || null,
              currentValue: curValNum,
              targetValue: tgtValNum,
              unit: objUnit.trim() || null,
              measurementMethod: objMeasurementMethod.trim() || null,
              actionPlan: objActionPlan.trim() || null,
              businessImpact: objBusinessImpact.trim() || null,
            }
          : o,
      );
      saveObjectivesState(next);
    } else {
      const newObj: DepartmentObjective = {
        id: `d-obj-${Date.now()}`,
        title: objTitle.trim(),
        description: objDesc.trim(),
        targetQuarter: objQuarter,
        targetDate: objDate,
        progressPercentage: Number(objProgress),
        status: objStatus,
        strategicGoalId: selectedGoal ? selectedGoal.id : null,
        strategicGoalTitle: selectedGoal ? selectedGoal.title : null,
        pillarTitle: pillar ? pillar.title : null,
        metricName: objMetricName.trim() || null,
        currentValue: curValNum,
        targetValue: tgtValNum,
        unit: objUnit.trim() || null,
        measurementMethod: objMeasurementMethod.trim() || null,
        actionPlan: objActionPlan.trim() || null,
        businessImpact: objBusinessImpact.trim() || null,
      };
      saveObjectivesState([newObj, ...objectives]);
    }
    setObjModalOpen(false);
  }

  function handleDeleteObj(id: string, title: string) {
    if (window.confirm(t('departmentDetail.deleteObjConfirm', { title }))) {
      saveObjectivesState(objectives.filter((o) => o.id !== id));
    }
  }

  // KPI Modal State
  const [kpiModalOpen, setKpiModalOpen] = useState(false);
  const [editingKpi, setEditingKpi] = useState<DepartmentKPI | null>(null);
  const [kpiName, setKpiName] = useState('');
  const [kpiTarget, setKpiTarget] = useState(100);
  const [kpiCurrent, setKpiCurrent] = useState(0);
  const [kpiUnit, setKpiUnit] = useState('%');
  const [kpiCategory, setKpiCategory] = useState(i18n.language?.startsWith('en') ? 'Performance' : 'Desempeño');

  function openCreateKpiModal() {
    setEditingKpi(null);
    setKpiName('');
    setKpiTarget(100);
    setKpiCurrent(0);
    setKpiUnit('%');
    setKpiCategory(i18n.language?.startsWith('en') ? 'Performance' : 'Desempeño');
    setKpiModalOpen(true);
  }

  function openEditKpiModal(k: DepartmentKPI) {
    setEditingKpi(k);
    setKpiName(k.name);
    setKpiTarget(k.targetValue);
    setKpiCurrent(k.currentValue);
    setKpiUnit(k.unit);
    setKpiCategory(k.category);
    setKpiModalOpen(true);
  }

  function handleKpiSubmit(e: FormEvent) {
    e.preventDefault();
    if (!kpiName.trim()) return;

    if (editingKpi) {
      const next = kpis.map((k) =>
        k.id === editingKpi.id
          ? {
              ...k,
              name: kpiName.trim(),
              targetValue: Number(kpiTarget),
              currentValue: Number(kpiCurrent),
              unit: kpiUnit.trim(),
              category: kpiCategory.trim(),
            }
          : k,
      );
      saveKpisState(next);
    } else {
      const newKpi: DepartmentKPI = {
        id: `kpi-${Date.now()}`,
        name: kpiName.trim(),
        targetValue: Number(kpiTarget),
        currentValue: Number(kpiCurrent),
        unit: kpiUnit.trim(),
        category: kpiCategory.trim(),
      };
      saveKpisState([...kpis, newKpi]);
    }
    setKpiModalOpen(false);
  }

  function handleDeleteKpi(id: string, name: string) {
    if (window.confirm(t('departmentDetail.deleteKpiConfirm', { name }))) {
      saveKpisState(kpis.filter((k) => k.id !== id));
    }
  }

  function initialsOf(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) return '?';
    if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
    return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
  }

  const inputClass =
    'mt-1 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
  const labelClass = 'block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-white/50';

  return (
    <div className="mx-auto max-w-5xl space-y-6 pb-16">
      {/* Back Button */}
      <button
        type="button"
        onClick={() => navigate('/departamentos')}
        className="inline-flex items-center gap-1.5 text-sm font-medium text-slate-500 hover:text-slate-700 dark:text-white/50 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" />
        {t('departmentDetail.backToDepartments')}
      </button>

      {/* Page Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-indigo-500 to-violet-600 text-white shadow-md shadow-indigo-500/20">
            <Building2 className="h-6 w-6" />
          </span>
          <div>
            <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
              {isNew ? t('departmentDetail.newTitle') : form.name || t('departmentDetail.newTitle')}
            </h1>
            <p className="text-sm text-slate-500 dark:text-white/50">
              {isNew ? t('departmentDetail.newSubtitle') : t('departmentDetail.editSubtitle')}
            </p>
          </div>
        </div>

        {savedSuccess && (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">
            <Check className="h-4 w-4" />
            {t('departmentDetail.savedSuccess')}
          </span>
        )}
      </div>

      {saveMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {saveMutation.error instanceof Error ? saveMutation.error.message : t('departmentDetail.saveError')}
        </div>
      )}

      {/* Navigation Tabs (if editing existing) */}
      {!isNew && (
        <div className="border-b border-slate-200 dark:border-white/10">
          <nav className="-mb-px flex flex-wrap gap-6">
            <button
              type="button"
              onClick={() => setActiveTab('profile')}
              className={`flex items-center gap-2 border-b-2 py-3 text-sm font-medium transition ${
                activeTab === 'profile'
                  ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-400'
                  : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
              }`}
            >
              <Building2 className="h-4 w-4" />
              {t('departmentDetail.tabs.profile')}
            </button>

            <button
              type="button"
              onClick={() => setActiveTab('objectives')}
              className={`flex items-center gap-2 border-b-2 py-3 text-sm font-medium transition ${
                activeTab === 'objectives'
                  ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-400'
                  : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
              }`}
            >
              <Target className="h-4 w-4" />
              {t('departmentDetail.tabs.objectives')} ({objectives.length})
            </button>

            <button
              type="button"
              onClick={() => setActiveTab('team')}
              className={`flex items-center gap-2 border-b-2 py-3 text-sm font-medium transition ${
                activeTab === 'team'
                  ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-400'
                  : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
              }`}
            >
              <Users className="h-4 w-4" />
              {t('departmentDetail.tabs.team')} ({teamMembers.length})
            </button>

            <button
              type="button"
              onClick={() => setActiveTab('roles')}
              className={`flex items-center gap-2 border-b-2 py-3 text-sm font-medium transition ${
                activeTab === 'roles'
                  ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-400'
                  : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
              }`}
            >
              <Briefcase className="h-4 w-4" />
              {t('departmentDetail.tabs.roles')} ({jobRoles.length})
            </button>

            <button
              type="button"
              onClick={() => setActiveTab('kpis')}
              className={`flex items-center gap-2 border-b-2 py-3 text-sm font-medium transition ${
                activeTab === 'kpis'
                  ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-400'
                  : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
              }`}
            >
              <BarChart3 className="h-4 w-4" />
              {t('departmentDetail.tabs.kpis')} ({kpis.length})
            </button>
          </nav>
        </div>
      )}

      {/* TAB 1: Profile & General Info */}
      {(isNew || activeTab === 'profile') && (
        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Section 1: General Info */}
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <h2 className="text-base font-semibold text-slate-900 dark:text-white">
              {t('departmentDetail.section1Title')}
            </h2>
            <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
              {t('departmentDetail.section1Subtitle')}
            </p>

            <div className="mt-6 grid grid-cols-1 gap-5 sm:grid-cols-2">
              <div>
                <label className={labelClass}>{t('departmentDetail.nameLabel')}</label>
                <input
                  required
                  value={form.name}
                  onChange={(e) => handleChange('name', e.target.value)}
                  placeholder={t('departmentDetail.namePlaceholder')}
                  className={inputClass}
                />
              </div>

              <div>
                <label className={labelClass}>{t('departmentDetail.codeLabel')}</label>
                <input
                  value={form.code}
                  onChange={(e) => handleChange('code', e.target.value)}
                  placeholder={t('departmentDetail.codePlaceholder')}
                  className={inputClass}
                />
              </div>

              <div>
                <label className={labelClass}>{t('departmentDetail.categoryLabel')}</label>
                <select
                  value={form.category}
                  onChange={(e) => handleChange('category', e.target.value)}
                  className={inputClass}
                >
                  <option value="">{t('departmentDetail.selectCategoryPlaceholder')}</option>
                  {categoryOptions.map((c) => (
                    <option key={c} value={c}>
                      {c}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className={labelClass}>{t('departmentDetail.parentLabel')}</label>
                <select
                  value={form.parentDepartmentId}
                  onChange={(e) => handleChange('parentDepartmentId', e.target.value)}
                  className={inputClass}
                >
                  <option value="">{t('departmentDetail.topLevelOption')}</option>
                  {departmentsQuery.data
                    ?.filter((d) => d.departmentId !== departmentId)
                    .map((d) => (
                      <option key={d.departmentId} value={d.departmentId}>
                        {d.name} {d.code ? `(${d.code})` : ''}
                      </option>
                    ))}
                </select>
              </div>
            </div>
          </section>

          {/* Section 2: Leadership & Location */}
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <h2 className="text-base font-semibold text-slate-900 dark:text-white">
              {t('departmentDetail.section2Title')}
            </h2>
            <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
              {t('departmentDetail.section2Subtitle')}
            </p>

            <div className="mt-6 grid grid-cols-1 gap-5 sm:grid-cols-2">
              <div>
                <label className={labelClass}>{t('departmentDetail.leaderLabel')}</label>
                <select
                  value={form.leaderPersonId}
                  onChange={(e) => handleChange('leaderPersonId', e.target.value)}
                  className={inputClass}
                >
                  <option value="">{t('departmentDetail.noLeaderOption')}</option>
                  {peopleQuery.data?.map((p) => (
                    <option key={p.personId} value={p.personId}>
                      {p.displayName || p.email} ({p.jobRoleTitle ?? 'Sin puesto'})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className={labelClass}>{t('departmentDetail.locationLabel')}</label>
                <input
                  value={form.location}
                  onChange={(e) => handleChange('location', e.target.value)}
                  placeholder={t('departmentDetail.locationPlaceholder')}
                  className={inputClass}
                />
              </div>

              <div>
                <label className={labelClass}>{t('departmentDetail.costCenterLabel')}</label>
                <input
                  value={form.costCenter}
                  onChange={(e) => handleChange('costCenter', e.target.value)}
                  placeholder={t('departmentDetail.costCenterPlaceholder')}
                  className={inputClass}
                />
              </div>
            </div>
          </section>

          {/* Section 3: Mission & Description */}
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <h2 className="text-base font-semibold text-slate-900 dark:text-white">
              {t('departmentDetail.section3Title')}
            </h2>
            <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
              {t('departmentDetail.section3Subtitle')}
            </p>

            <div className="mt-6 space-y-5">
              <div>
                <label className={labelClass}>{t('departmentDetail.missionLabel')}</label>
                <textarea
                  rows={3}
                  value={form.missionStatement}
                  onChange={(e) => handleChange('missionStatement', e.target.value)}
                  placeholder={t('departmentDetail.missionPlaceholder')}
                  className={inputClass}
                />
              </div>

              <div>
                <label className={labelClass}>{t('departmentDetail.descriptionLabel')}</label>
                <textarea
                  rows={3}
                  value={form.description}
                  onChange={(e) => handleChange('description', e.target.value)}
                  placeholder={t('departmentDetail.descriptionPlaceholder')}
                  className={inputClass}
                />
              </div>
            </div>
          </section>

          {/* Save Bar */}
          <div className="flex justify-end gap-3">
            <button
              type="button"
              onClick={() => navigate('/departamentos')}
              className="rounded-full px-5 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
            >
              {t('departmentDetail.cancelButton')}
            </button>
            <button
              type="submit"
              disabled={saveMutation.isPending || !form.name.trim()}
              className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-6 py-2.5 text-sm font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110 disabled:opacity-50"
            >
              {saveMutation.isPending ? t('departmentDetail.savingButton') : t('departmentDetail.saveButton')}
            </button>
          </div>
        </form>
      )}

      {/* TAB 2: Department Objectives */}
      {!isNew && activeTab === 'objectives' && (
        <div className="space-y-6">
          {/* Corporate Strategic Alignment Banner */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 rounded-2xl border border-indigo-100 bg-gradient-to-r from-indigo-50/80 via-purple-50/40 to-violet-50/80 p-4 dark:border-indigo-500/20 dark:bg-indigo-500/10">
            <div className="flex items-start gap-3">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-indigo-600 text-white shadow-sm">
                <Compass className="h-5 w-5" />
              </span>
              <div>
                <h3 className="text-xs font-semibold text-indigo-950 dark:text-indigo-200">
                  {t('departmentDetail.corporateAlignmentBannerTitle')}
                </h3>
                <p className="mt-0.5 text-xs text-indigo-700/80 dark:text-indigo-300/70">
                  {t('departmentDetail.corporateAlignmentBannerDesc')}
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={() => navigate('/objetivos')}
              className="inline-flex items-center gap-1.5 self-start sm:self-auto rounded-full bg-white px-3.5 py-1.5 text-xs font-semibold text-indigo-700 shadow-sm transition hover:bg-indigo-50 dark:bg-white/10 dark:text-white dark:hover:bg-white/20"
            >
              <ExternalLink className="h-3.5 w-3.5" />
              {t('departmentDetail.viewCorporateGoals')}
            </button>
          </div>

          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                {t('departmentDetail.objectivesTitle', { count: objectives.length })}
              </h2>
              <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
                {t('departmentDetail.objectivesSubtitle')}
              </p>
            </div>
            <button
              type="button"
              onClick={openCreateObjModal}
              className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110"
            >
              <Plus className="h-4 w-4" />
              {t('departmentDetail.newObjectiveButton')}
            </button>
          </div>

          {objectives.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 p-10 text-center dark:border-white/10 dark:bg-white/5">
              <Target className="h-8 w-8 text-slate-300 dark:text-white/20" />
              <p className="mt-2 text-sm text-slate-500 dark:text-white/50">
                {t('departmentDetail.emptyObjectives')}
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {objectives.map((obj) => (
                <div
                  key={obj.id}
                  className="group relative rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-slate-300 dark:border-white/10 dark:bg-white/5"
                >
                  <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                    <div className="space-y-2.5 flex-1">
                      {/* Status & Time-bound Badges */}
                      <div className="flex flex-wrap items-center gap-2">
                        <span
                          className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                            STATUS_META[obj.status]?.className
                          }`}
                        >
                          {t(STATUS_META[obj.status]?.labelKey as any) || obj.status}
                        </span>

                        {(obj.metricName || obj.measurementMethod) && (
                          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2.5 py-0.5 text-[11px] font-semibold text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">
                            <Sparkles className="h-3 w-3" />
                            {t('departmentDetail.smartBadge')}
                          </span>
                        )}

                        <span className="flex items-center gap-1 text-xs text-slate-400">
                          <Clock className="h-3.5 w-3.5" />
                          {obj.targetQuarter} ({obj.targetDate})
                        </span>
                      </div>

                      {/* S - Specific Title & Description */}
                      <div>
                        <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                          {obj.title}
                        </h3>
                        {obj.description && (
                          <p className="mt-0.5 text-xs text-slate-600 dark:text-white/70">
                            {obj.description}
                          </p>
                        )}
                      </div>

                      {/* M - Measurable (How to measure) */}
                      {(obj.metricName || obj.measurementMethod || (obj.targetValue != null && obj.currentValue != null)) && (
                        <div className="rounded-xl border border-emerald-100 bg-emerald-50/40 p-3 text-xs space-y-1.5 dark:border-emerald-500/20 dark:bg-emerald-500/5">
                          <div className="flex flex-wrap items-center justify-between gap-2">
                            <div className="flex items-center gap-1.5 font-medium text-emerald-950 dark:text-emerald-200">
                              <Activity className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                              <span className="font-semibold">{t('departmentDetail.metricBadge')}</span>
                              <span>{obj.metricName || t('departmentDetail.kpisTitle')}</span>
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
                              <span className="font-medium text-slate-700 dark:text-white/90">
                                {t('departmentDetail.howToMeasureBadge')}{' '}
                              </span>
                              {obj.measurementMethod}
                            </p>
                          )}
                        </div>
                      )}

                      {/* A - Achievable (Action Plan) */}
                      {obj.actionPlan && (
                        <div className="text-xs text-slate-600 dark:text-white/70">
                          <span className="font-medium text-slate-700 dark:text-white/90">
                            {t('departmentDetail.actionPlanBadge')}{' '}
                          </span>
                          {obj.actionPlan}
                        </div>
                      )}

                      {/* R - Relevant (Corporate Strategic Alignment & Business Impact) */}
                      {obj.strategicGoalTitle ? (
                        <div className="flex flex-wrap items-center gap-1.5 pt-0.5">
                          <span className="inline-flex items-center gap-1 rounded-md bg-indigo-50 px-2.5 py-1 text-[11px] font-semibold text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300">
                            <Compass className="h-3 w-3 text-indigo-500" />
                            {t('departmentDetail.alignedWithStrategicGoal', { goal: obj.strategicGoalTitle })}
                          </span>
                          {obj.pillarTitle && (
                            <span className="rounded-md bg-slate-100 px-2 py-1 text-[10px] font-medium text-slate-600 dark:bg-white/10 dark:text-white/60">
                              {obj.pillarTitle}
                            </span>
                          )}
                          {obj.businessImpact && (
                            <span className="w-full text-[11px] text-slate-500 dark:text-white/50 pt-0.5">
                              <span className="font-medium">{t('departmentDetail.businessImpactBadge')}</span> {obj.businessImpact}
                            </span>
                          )}
                        </div>
                      ) : (
                        <div className="pt-0.5">
                          <button
                            type="button"
                            onClick={() => openEditObjModal(obj)}
                            className="inline-flex items-center gap-1 text-[11px] text-amber-600 hover:underline dark:text-amber-400"
                          >
                            <Compass className="h-3 w-3" />
                            {t('departmentDetail.unalignedStrategicGoal')} — <span className="font-medium underline">{t('departmentDetail.alignWithCorporateGoalPrompt')}</span>
                          </button>
                        </div>
                      )}
                    </div>

                    <div className="flex sm:flex-col items-end justify-between sm:justify-start gap-3 min-w-[160px]">
                      <div className="flex items-center gap-1.5 opacity-0 transition group-hover:opacity-100">
                        <button
                          type="button"
                          onClick={() => openEditObjModal(obj)}
                          className="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                        >
                          <Pencil className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={() => handleDeleteObj(obj.id, obj.title)}
                          className="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400"
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>

                      <div className="w-full text-right">
                        <span className="text-xs font-semibold text-slate-700 dark:text-white/80">
                          {obj.progressPercentage}%
                        </span>
                        <div className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                          <div
                            className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-violet-500 transition-all duration-300"
                            style={{ width: `${obj.progressPercentage}%` }}
                          />
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* TAB 3: Team Members */}
      {!isNew && activeTab === 'team' && (
        <div className="space-y-6">
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                  {t('departmentDetail.teamTitle', { count: teamMembers.length })}
                </h2>
                <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
                  {t('departmentDetail.teamSubtitle')}
                </p>
              </div>
              <button
                type="button"
                onClick={() => navigate('/personal')}
                className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-50 px-3.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-100 dark:border-white/10 dark:bg-white/5 dark:text-white"
              >
                {t('departmentDetail.assignMemberButton')}
              </button>
            </div>

            <div className="mt-6">
              {teamMembers.length === 0 ? (
                <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 p-10 text-center dark:border-white/10 dark:bg-white/5">
                  <Users className="h-8 w-8 text-slate-300 dark:text-white/20" />
                  <p className="mt-2 text-sm text-slate-500 dark:text-white/50">
                    {t('departmentDetail.emptyTeam')}
                  </p>
                </div>
              ) : (
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {teamMembers.map((person) => {
                    const displayName = person.displayName || person.email || t('people.noName');
                    return (
                      <div
                        key={person.personId}
                        className="group flex flex-col justify-between rounded-xl border border-slate-200 bg-white p-4 transition hover:border-slate-300 hover:shadow-sm dark:border-white/10 dark:bg-white/5 dark:hover:border-white/20"
                      >
                        <div className="flex items-start gap-3">
                          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-indigo-500 to-violet-600 text-xs font-bold text-white">
                            {initialsOf(displayName)}
                          </span>
                          <div className="min-w-0 flex-1">
                            <h3 className="truncate text-sm font-semibold text-slate-900 dark:text-white">
                              {displayName}
                            </h3>
                            <p className="truncate text-xs text-slate-500 dark:text-white/50">
                              {person.jobRoleTitle ?? t('departmentDetail.unassignedJobRole')}
                            </p>
                            {person.managerDisplayName && (
                              <p className="mt-1 truncate text-[11px] text-slate-400">
                                {t('departmentDetail.reportsToLabel', { manager: person.managerDisplayName })}
                              </p>
                            )}
                          </div>
                        </div>

                        <div className="mt-4 flex items-center justify-between border-t border-slate-100 pt-2.5 dark:border-white/5">
                          <span className="text-[11px] text-slate-400">
                            {person.email}
                          </span>
                          <button
                            type="button"
                            onClick={() => navigate(`/personal/${person.personId}`)}
                            className="text-xs font-semibold text-indigo-600 hover:underline dark:text-indigo-400"
                          >
                            {t('departmentDetail.viewProfileButton')}
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </section>
        </div>
      )}

      {/* TAB 4: Job Roles */}
      {!isNew && activeTab === 'roles' && (
        <div className="space-y-6">
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                  {t('departmentDetail.rolesTitle', { count: jobRoles.length })}
                </h2>
                <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
                  {t('departmentDetail.rolesSubtitle')}
                </p>
                <p className="mt-1 text-xs text-slate-400 dark:text-white/40 italic">
                  {t('departmentDetail.detachNotice')}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => {
                    setSelectedRoleIdToAttach(availableRolesToAttach[0]?.jobRoleId ?? '');
                    setAttachModalOpen(true);
                  }}
                  className="inline-flex items-center gap-1.5 rounded-full border border-indigo-200 bg-indigo-50 px-3.5 py-1.5 text-xs font-medium text-indigo-700 hover:bg-indigo-100 dark:border-indigo-500/30 dark:bg-indigo-500/10 dark:text-indigo-300"
                >
                  <Link2 className="h-3.5 w-3.5" />
                  {t('departmentDetail.attachRoleButton')}
                </button>
                <button
                  type="button"
                  onClick={() =>
                    navigate('/roles/nuevo', {
                      state: { returnUrl: `/departamentos/${departmentId}`, departmentId },
                    })
                  }
                  className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-50 px-3.5 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-100 dark:border-white/10 dark:bg-white/5 dark:text-white"
                >
                  {t('departmentDetail.createRoleButton')}
                </button>
              </div>
            </div>

            <div className="mt-6">
              {jobRolesQuery.isLoading && (
                <p className="text-xs text-slate-400">{t('departmentDetail.loadingRoles')}</p>
              )}

              {!jobRolesQuery.isLoading && jobRoles.length === 0 && (
                <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 p-10 text-center dark:border-white/10 dark:bg-white/5">
                  <Briefcase className="h-8 w-8 text-slate-300 dark:text-white/20" />
                  <p className="mt-2 text-sm text-slate-500 dark:text-white/50">
                    {t('departmentDetail.emptyRoles')}
                  </p>
                </div>
              )}

              {jobRoles.length > 0 && (
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
                  {jobRoles.map((role) => (
                    <div
                      key={role.jobRoleId}
                      onClick={() =>
                        navigate(`/roles/${role.jobRoleId}`, {
                          state: { returnUrl: `/departamentos/${departmentId}`, departmentId },
                        })
                      }
                      className="group flex cursor-pointer flex-col justify-between rounded-xl border border-slate-200 bg-white p-4 transition hover:border-indigo-300 hover:shadow-sm dark:border-white/10 dark:bg-white/5 dark:hover:border-white/20"
                    >
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex items-center gap-2.5">
                          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-indigo-50 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-300">
                            <Briefcase className="h-4 w-4" />
                          </span>
                          <div>
                            <p className="text-sm font-semibold text-slate-900 dark:text-white">
                              {role.title}
                            </p>
                            {role.levelLabel && (
                              <p className="text-xs text-slate-400">{role.levelLabel}</p>
                            )}
                          </div>
                        </div>

                        <button
                          type="button"
                          title={t('departmentDetail.detachRoleTooltip')}
                          onClick={(e) => {
                            e.stopPropagation();
                            handleDetachRole(role.jobRoleId, role.title);
                          }}
                          className="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400"
                        >
                          <Unlink className="h-4 w-4" />
                        </button>
                      </div>

                      <div className="mt-4 flex items-center justify-between border-t border-slate-100 pt-2.5 text-xs text-slate-400 dark:border-white/5">
                        <span>{t('departmentDetail.personCountRatio', { count: role.personCount })}</span>
                        <span className="font-semibold text-indigo-600 dark:text-indigo-400">
                          {t('departmentDetail.viewDetail')}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </section>
        </div>
      )}

      {/* TAB 5: Department KPIs */}
      {!isNew && activeTab === 'kpis' && (
        <div className="space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                {t('departmentDetail.kpisTitle')}
              </h2>
              <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
                {t('departmentDetail.kpisSubtitle')}
              </p>
            </div>
            <button
              type="button"
              onClick={openCreateKpiModal}
              className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110"
            >
              <Plus className="h-4 w-4" />
              {t('departmentDetail.newKpiButton')}
            </button>
          </div>

          {kpis.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 p-10 text-center dark:border-white/10 dark:bg-white/5">
              <BarChart3 className="h-8 w-8 text-slate-300 dark:text-white/20" />
              <p className="mt-2 text-sm text-slate-500 dark:text-white/50">
                {t('departmentDetail.emptyKpis')}
              </p>
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {kpis.map((kpi) => {
                const pct =
                  kpi.targetValue === 0
                    ? 0
                    : Math.min(100, Math.round((kpi.currentValue / kpi.targetValue) * 100));

                return (
                  <div
                    key={kpi.id}
                    className="group relative flex flex-col justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition hover:border-slate-300 dark:border-white/10 dark:bg-white/5"
                  >
                    <div>
                      <div className="flex items-start justify-between gap-2">
                        <span className="rounded-full bg-indigo-50 px-2.5 py-0.5 text-[11px] font-semibold text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300">
                          {kpi.category}
                        </span>

                        <div className="flex items-center gap-1 opacity-0 transition group-hover:opacity-100">
                          <button
                            type="button"
                            onClick={() => openEditKpiModal(kpi)}
                            className="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                          >
                            <Pencil className="h-3.5 w-3.5" />
                          </button>
                          <button
                            type="button"
                            onClick={() => handleDeleteKpi(kpi.id, kpi.name)}
                            className="flex h-7 w-7 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </div>

                      <h3 className="mt-2.5 text-sm font-semibold text-slate-900 dark:text-white">
                        {kpi.name}
                      </h3>

                      <div className="mt-3 flex items-baseline gap-1">
                        <span className="text-2xl font-bold text-slate-900 dark:text-white">
                          {kpi.currentValue}
                        </span>
                        <span className="text-xs text-slate-400">
                          / {kpi.targetValue} {kpi.unit}
                        </span>
                      </div>
                    </div>

                    <div className="mt-4">
                      <div className="flex justify-between text-xs text-slate-400">
                        <span>{t('departmentDetail.complianceLabel')}</span>
                        <span className="font-semibold text-slate-700 dark:text-white/80">
                          {pct}%
                        </span>
                      </div>
                      <div className="mt-1 h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                        <div
                          className="h-full rounded-full bg-gradient-to-r from-indigo-500 to-violet-500 transition-all duration-300"
                          style={{ width: `${pct}%` }}
                        />
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* Objective Modal (Streamlined for Departments) */}
      {objModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm overflow-y-auto">
          <div className="my-8 w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-white/10">
              <div className="flex items-center gap-2.5">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-violet-600 text-white shadow-sm">
                  <Target className="h-4 w-4" />
                </span>
                <div>
                  <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                    {editingObj
                      ? t('departmentDetail.modalEditObjTitle')
                      : t('departmentDetail.modalNewObjTitle')}
                  </h2>
                  <p className="text-xs text-slate-500 dark:text-white/50">
                    {t('departmentDetail.smartSectionDesc')}
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setObjModalOpen(false)}
                className="rounded-full p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <form onSubmit={handleObjSubmit} className="mt-5 space-y-5">
              {/* 1. Core Objective & Description */}
              <div className="space-y-3.5 rounded-xl border border-indigo-100 bg-indigo-50/20 p-4 dark:border-indigo-500/20 dark:bg-indigo-500/5">
                <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-indigo-700 dark:text-indigo-300">
                  <Target className="h-4 w-4" />
                  <span>{t('departmentDetail.generalInfoSectionTitle')}</span>
                </div>

                <div>
                  <label className={labelClass}>
                    {t('departmentDetail.objTitleLabel')}
                  </label>
                  <input
                    required
                    value={objTitle}
                    onChange={(e) => setObjTitle(e.target.value)}
                    placeholder={t('departmentDetail.objTitlePlaceholder')}
                    className={inputClass}
                  />
                </div>

                <div>
                  <label className={labelClass}>
                    {t('departmentDetail.objDescLabel')}
                  </label>
                  <textarea
                    rows={2}
                    value={objDesc}
                    onChange={(e) => setObjDesc(e.target.value)}
                    placeholder={t('departmentDetail.objDescPlaceholder')}
                    className={inputClass}
                  />
                </div>
              </div>

              {/* 2. Timeline, Status & Progress */}
              <div className="space-y-3.5 rounded-xl border border-sky-100 bg-sky-50/20 p-4 dark:border-sky-500/20 dark:bg-sky-500/5">
                <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-sky-700 dark:text-sky-300">
                  <Clock className="h-4 w-4" />
                  <span>{t('departmentDetail.timelineSectionTitle')}</span>
                </div>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <div>
                    <label className={labelClass}>
                      {t('departmentDetail.objQuarterLabel')}
                    </label>
                    <input
                      value={objQuarter}
                      onChange={(e) => setObjQuarter(e.target.value)}
                      placeholder="Q4 2026"
                      className={inputClass}
                    />
                  </div>

                  <div>
                    <label className={labelClass}>
                      {t('departmentDetail.objDateLabel')}
                    </label>
                    <input
                      type="date"
                      value={objDate}
                      onChange={(e) => setObjDate(e.target.value)}
                      className={inputClass}
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <div>
                    <label className={labelClass}>{t('departmentDetail.statusLabel')}</label>
                    <select
                      value={objStatus}
                      onChange={(e) =>
                        setObjStatus(e.target.value as DepartmentObjective['status'])
                      }
                      className={inputClass}
                    >
                      <option value="Active">{t('businessGoals.statusActive')}</option>
                      <option value="InProgress">{t('businessGoals.statusInProgress')}</option>
                      <option value="Completed">{t('businessGoals.statusCompleted')}</option>
                      <option value="Paused">{t('businessGoals.statusPaused')}</option>
                    </select>
                  </div>

                  <div>
                    <label className={labelClass}>
                      {t('departmentDetail.objProgressLabel', {
                        percentage: objProgress,
                      })}
                    </label>
                    <input
                      type="range"
                      min="0"
                      max="100"
                      value={objProgress}
                      onChange={(e) => setObjProgress(Number(e.target.value))}
                      className="mt-3 w-full accent-indigo-600"
                    />
                  </div>
                </div>
              </div>

              {/* 3. Corporate Strategic Alignment (Optional) */}
              <div className="space-y-3.5 rounded-xl border border-purple-100 bg-purple-50/20 p-4 dark:border-purple-500/20 dark:bg-purple-500/5">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-purple-700 dark:text-purple-300">
                    <Compass className="h-4 w-4" />
                    <span>{t('departmentDetail.strategicGoalSelectLabel')}</span>
                  </div>
                  <button
                    type="button"
                    onClick={() => navigate('/objetivos')}
                    className="inline-flex items-center gap-1 text-[11px] font-medium text-purple-600 hover:underline dark:text-purple-400"
                  >
                    <ExternalLink className="h-3 w-3" />
                    {t('departmentDetail.viewCorporateGoals')}
                  </button>
                </div>

                <div>
                  <select
                    value={objStrategicGoalId}
                    onChange={(e) => setObjStrategicGoalId(e.target.value)}
                    className="w-full rounded-xl border border-purple-200 bg-white px-3.5 py-2.5 text-xs font-medium text-slate-900 outline-none transition focus:border-purple-500 focus:ring-1 focus:ring-purple-500 dark:border-purple-500/30 dark:bg-slate-900 dark:text-white"
                  >
                    <option value="">{t('departmentDetail.strategicGoalNoneOption')}</option>
                    {companyGoals.map((cg) => {
                      const p = companyDirection.pillars.find((pillar) => pillar.id === cg.pillarId);
                      return (
                        <option key={cg.id} value={cg.id}>
                          {p ? `[${p.title}] ` : ''}{cg.title} ({cg.targetQuarter})
                        </option>
                      );
                    })}
                  </select>

                  {/* Selected Goal Preview */}
                  {(() => {
                    const previewGoal = companyGoals.find((g) => g.id === objStrategicGoalId);
                    if (!previewGoal) return null;
                    const previewPillar = companyDirection.pillars.find((p) => p.id === previewGoal.pillarId);
                    return (
                      <div className="mt-3 rounded-xl border border-purple-200/80 bg-white p-3.5 text-xs shadow-sm dark:border-purple-500/30 dark:bg-slate-900/90">
                        <div className="flex items-start justify-between gap-2">
                          <div>
                            <span className="text-[10px] font-bold uppercase tracking-wider text-purple-600 dark:text-purple-400">
                              {previewPillar ? previewPillar.title : 'Objetivo Corporativo'}
                            </span>
                            <h4 className="mt-0.5 text-xs font-semibold text-slate-900 dark:text-white">
                              {previewGoal.title}
                            </h4>
                          </div>
                          <span className="shrink-0 rounded-full bg-purple-50 px-2 py-0.5 text-[10px] font-semibold text-purple-700 dark:bg-purple-500/20 dark:text-purple-300">
                            {previewGoal.targetQuarter}
                          </span>
                        </div>
                        {previewGoal.description && (
                          <p className="mt-1.5 text-[11px] text-slate-600 dark:text-white/70">
                            {previewGoal.description}
                          </p>
                        )}
                        <div className="mt-2.5 flex items-center justify-between border-t border-slate-100 pt-2 text-[10px] text-slate-400 dark:border-white/5">
                          <span>Meta corporativa: {previewGoal.targetDate}</span>
                          <span className="font-semibold text-emerald-600 dark:text-emerald-400">
                            Progreso Empresa: {previewGoal.progressPercentage}%
                          </span>
                        </div>
                      </div>
                    );
                  })()}
                </div>
              </div>

              {/* 4. Quantifiable Metric / Target (Optional) */}
              <div className="space-y-3.5 rounded-xl border border-emerald-100 bg-emerald-50/20 p-4 dark:border-emerald-500/20 dark:bg-emerald-500/5">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider text-emerald-700 dark:text-emerald-300">
                    <Activity className="h-4 w-4" />
                    <span>{t('departmentDetail.metricsSectionTitle')}</span>
                  </div>
                  {objCurrentValue && objTargetValue && (
                    <button
                      type="button"
                      onClick={autoCalculateProgress}
                      className="inline-flex items-center gap-1 rounded-md bg-emerald-100 px-2.5 py-1 text-[11px] font-semibold text-emerald-800 transition hover:bg-emerald-200 dark:bg-emerald-500/20 dark:text-emerald-300"
                    >
                      <Calculator className="h-3 w-3" />
                      Sincronizar Progreso %
                    </button>
                  )}
                </div>

                <div>
                  <label className={labelClass}>
                    {t('departmentDetail.metricNameLabel')}
                  </label>
                  <input
                    value={objMetricName}
                    onChange={(e) => setObjMetricName(e.target.value)}
                    placeholder={t('departmentDetail.metricNamePlaceholder')}
                    className={inputClass}
                  />
                </div>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                  <div>
                    <label className={labelClass}>
                      {t('departmentDetail.metricCurrentLabel')}
                    </label>
                    <input
                      type="number"
                      step="any"
                      value={objCurrentValue}
                      onChange={(e) => setObjCurrentValue(e.target.value)}
                      placeholder="0"
                      className={inputClass}
                    />
                  </div>

                  <div>
                    <label className={labelClass}>
                      {t('departmentDetail.metricTargetLabel')}
                    </label>
                    <input
                      type="number"
                      step="any"
                      value={objTargetValue}
                      onChange={(e) => setObjTargetValue(e.target.value)}
                      placeholder="3"
                      className={inputClass}
                    />
                  </div>

                  <div>
                    <label className={labelClass}>
                      {t('departmentDetail.metricUnitLabel')}
                    </label>
                    <input
                      value={objUnit}
                      onChange={(e) => setObjUnit(e.target.value)}
                      placeholder={t('departmentDetail.metricUnitPlaceholder')}
                      className={inputClass}
                    />
                  </div>
                </div>
              </div>

              {/* 5. Additional Details (Collapsible) */}
              <div className="rounded-xl border border-slate-200 bg-slate-50/50 dark:border-white/10 dark:bg-white/[0.02]">
                <button
                  type="button"
                  onClick={() => setShowAdvancedDetails(!showAdvancedDetails)}
                  className="flex w-full items-center justify-between px-4 py-3 text-xs font-semibold text-slate-700 hover:bg-slate-100/60 dark:text-slate-300 dark:hover:bg-white/5 rounded-xl transition"
                >
                  <span className="flex items-center gap-1.5">
                    <Sparkles className="h-3.5 w-3.5 text-indigo-500" />
                    {t('departmentDetail.advancedDetailsToggle')}
                  </span>
                  {showAdvancedDetails ? (
                    <ChevronUp className="h-4 w-4 text-slate-400" />
                  ) : (
                    <ChevronDown className="h-4 w-4 text-slate-400" />
                  )}
                </button>

                {showAdvancedDetails && (
                  <div className="space-y-3.5 border-t border-slate-200 p-4 dark:border-white/10">
                    <div>
                      <label className={labelClass}>
                        {t('departmentDetail.measurementMethodLabel')}
                      </label>
                      <textarea
                        rows={2}
                        value={objMeasurementMethod}
                        onChange={(e) => setObjMeasurementMethod(e.target.value)}
                        placeholder={t('departmentDetail.measurementMethodPlaceholder')}
                        className={inputClass}
                      />
                    </div>

                    <div>
                      <label className={labelClass}>
                        {t('departmentDetail.actionPlanLabel')}
                      </label>
                      <textarea
                        rows={2}
                        value={objActionPlan}
                        onChange={(e) => setObjActionPlan(e.target.value)}
                        placeholder={t('departmentDetail.actionPlanPlaceholder')}
                        className={inputClass}
                      />
                    </div>

                    <div>
                      <label className={labelClass}>
                        {t('departmentDetail.businessImpactLabel')}
                      </label>
                      <textarea
                        rows={2}
                        value={objBusinessImpact}
                        onChange={(e) => setObjBusinessImpact(e.target.value)}
                        placeholder={t('departmentDetail.businessImpactPlaceholder')}
                        className={inputClass}
                      />
                    </div>
                  </div>
                )}
              </div>

              <div className="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4 dark:border-white/10">
                <button
                  type="button"
                  onClick={() => setObjModalOpen(false)}
                  className="rounded-full px-5 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
                >
                  {t('departmentDetail.cancelButton')}
                </button>
                <button
                  type="submit"
                  disabled={!objTitle.trim()}
                  className="rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-6 py-2.5 text-sm font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110 disabled:opacity-50"
                >
                  {editingObj ? t('common.save') : t('businessGoals.modalSaveCreateButton')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* KPI Modal */}
      {kpiModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm">
          <div className="w-full max-w-lg rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-white/10">
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                {editingKpi
                  ? t('departmentDetail.modalEditKpiTitle')
                  : t('departmentDetail.modalNewKpiTitle')}
              </h2>
              <button
                type="button"
                onClick={() => setKpiModalOpen(false)}
                className="rounded-full p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <form onSubmit={handleKpiSubmit} className="mt-4 space-y-4">
              <div>
                <label className={labelClass}>
                  {t('departmentDetail.kpiNameLabel')}
                </label>
                <input
                  required
                  value={kpiName}
                  onChange={(e) => setKpiName(e.target.value)}
                  placeholder={t('departmentDetail.kpiNamePlaceholder')}
                  className={inputClass}
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className={labelClass}>
                    {t('departmentDetail.kpiCurrentLabel')}
                  </label>
                  <input
                    type="number"
                    value={kpiCurrent}
                    onChange={(e) => setKpiCurrent(Number(e.target.value))}
                    className={inputClass}
                  />
                </div>

                <div>
                  <label className={labelClass}>
                    {t('departmentDetail.kpiTargetLabel')}
                  </label>
                  <input
                    type="number"
                    value={kpiTarget}
                    onChange={(e) => setKpiTarget(Number(e.target.value))}
                    className={inputClass}
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className={labelClass}>
                    {t('departmentDetail.kpiUnitLabel')}
                  </label>
                  <input
                    value={kpiUnit}
                    onChange={(e) => setKpiUnit(e.target.value)}
                    placeholder={t('departmentDetail.kpiUnitPlaceholder')}
                    className={inputClass}
                  />
                </div>

                <div>
                  <label className={labelClass}>
                    {t('departmentDetail.kpiCategoryLabel')}
                  </label>
                  <input
                    value={kpiCategory}
                    onChange={(e) => setKpiCategory(e.target.value)}
                    placeholder="Desempeño, Operaciones..."
                    className={inputClass}
                  />
                </div>
              </div>

              <div className="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4 dark:border-white/10">
                <button
                  type="button"
                  onClick={() => setKpiModalOpen(false)}
                  className="rounded-full px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
                >
                  {t('departmentDetail.cancelButton')}
                </button>
                <button
                  type="submit"
                  disabled={!kpiName.trim()}
                  className="rounded-full bg-indigo-600 px-5 py-2 text-sm font-semibold text-white transition hover:bg-indigo-700 disabled:opacity-50"
                >
                  {editingKpi ? t('common.save') : t('businessGoals.modalSaveCreateButton')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Attach Existing Job Role Modal */}
      {attachModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm">
          <div className="w-full max-w-lg rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-white/10">
              <div>
                <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                  {t('departmentDetail.modalAttachRoleTitle')}
                </h2>
                <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
                  {t('departmentDetail.modalAttachRoleDesc', {
                    department: form.name || t('departmentDetail.newTitle'),
                  })}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setAttachModalOpen(false)}
                className="rounded-full p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <form onSubmit={handleAttachSubmit} className="mt-4 space-y-4">
              {availableRolesToAttach.length === 0 ? (
                <div className="rounded-xl border border-dashed border-slate-200 bg-slate-50 p-6 text-center text-xs text-slate-500 dark:border-white/10 dark:bg-white/5 dark:text-white/50">
                  {t('departmentDetail.noAvailableRolesToAttach')}
                </div>
              ) : (
                <div>
                  <label className={labelClass}>
                    {t('departmentDetail.rolesTitle', { count: availableRolesToAttach.length })}
                  </label>
                  <select
                    required
                    value={selectedRoleIdToAttach}
                    onChange={(e) => setSelectedRoleIdToAttach(e.target.value)}
                    className={inputClass}
                  >
                    <option value="" disabled>
                      {t('departmentDetail.selectRolePlaceholder')}
                    </option>
                    {availableRolesToAttach.map((role) => (
                      <option key={role.jobRoleId} value={role.jobRoleId}>
                        {role.title} {role.levelLabel ? `(${role.levelLabel})` : ''}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              <div className="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4 dark:border-white/10">
                <button
                  type="button"
                  onClick={() => setAttachModalOpen(false)}
                  className="rounded-full px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
                >
                  {t('departmentDetail.cancelButton')}
                </button>
                <button
                  type="submit"
                  disabled={!selectedRoleIdToAttach || attachMutation.isPending}
                  className="rounded-full bg-indigo-600 px-5 py-2 text-sm font-semibold text-white transition hover:bg-indigo-700 disabled:opacity-50"
                >
                  {attachMutation.isPending
                    ? t('departmentDetail.attachingButton')
                    : t('departmentDetail.attachButton')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
