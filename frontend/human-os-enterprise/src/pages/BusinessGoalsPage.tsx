import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import {
  Target,
  Compass,
  GitBranch,
  Plus,
  Pencil,
  Trash2,
  Clock,
  Building2,
  Briefcase,
  GraduationCap,
  TrendingUp,
  Award,
  Layers,
  Search,
  Check,
  X,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  getDepartments,
  getJobRoles,
} from '@/api/orgStructureApi';
import {
  getSkills,
} from '@/api/skillsApi';
import {
  getStrategicGoals,
  saveStrategicGoals,
  getCompanyDirection,
  saveCompanyDirection,
  getContributingObjectivesForStrategicGoal,
  type StrategicGoal,
  type StrategicPillar,
  type CompanyDirection,
} from '@/api/strategicGoalsApi';

export type { StrategicGoal, StrategicPillar, CompanyDirection };

type TabType = 'company-direction' | 'strategic-goals' | 'capability-alignment';

export function BusinessGoalsPage() {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const isEn = i18n.language?.startsWith('en');
  const navigate = useNavigate();

  const statusMeta: Record<StrategicGoal['status'], { label: string; className: string }> = {
    Active: {
      label: t('businessGoals.statusActive'),
      className: 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300',
    },
    InProgress: {
      label: t('businessGoals.statusInProgress'),
      className: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
    },
    Completed: {
      label: t('businessGoals.statusCompleted'),
      className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
    },
    Paused: {
      label: t('businessGoals.statusPaused'),
      className: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/50',
    },
  };

  const [activeTab, setActiveTab] = useState<TabType>('company-direction');

  // Company Direction State
  const [direction, setCompanyDirection] = useState<CompanyDirection>(() => {
    return getCompanyDirection(tenantId, isEn);
  });

  // Strategic Goals State
  const [goals, setGoals] = useState<StrategicGoal[]>(() => {
    return getStrategicGoals(tenantId, isEn);
  });

  // Reload when language changes if nothing was customized or if key changes
  useEffect(() => {
    setCompanyDirection(getCompanyDirection(tenantId, isEn));
    setGoals(getStrategicGoals(tenantId, isEn));
  }, [tenantId, isEn]);

  const [savedBadge, setSavedBadge] = useState(false);

  // Sync to localStorage
  function saveDirectionState(next: CompanyDirection) {
    setCompanyDirection(next);
    saveCompanyDirection(tenantId, isEn, next);
    showSavedBadge();
  }

  function saveGoalsState(next: StrategicGoal[]) {
    setGoals(next);
    saveStrategicGoals(tenantId, isEn, next);
    showSavedBadge();
  }

  function showSavedBadge() {
    setSavedBadge(true);
    setTimeout(() => setSavedBadge(false), 3000);
  }

  // Load departments, roles, skills for selection and alignment
  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });

  const jobRolesQuery = useQuery({
    queryKey: ['job-roles', tenantId],
    queryFn: () => getJobRoles(tenantId),
  });

  const skillsQuery = useQuery({
    queryKey: ['skills', tenantId],
    queryFn: () => getSkills(tenantId),
  });

  const departments = departmentsQuery.data ?? [];
  const jobRoles = jobRolesQuery.data ?? [];
  const skills = skillsQuery.data ?? [];

  // Goal Form Modal State
  const [goalModalOpen, setGoalModalOpen] = useState(false);
  const [editingGoal, setEditingGoal] = useState<StrategicGoal | null>(null);
  const [goalTitle, setGoalTitle] = useState('');
  const [goalDescription, setGoalDescription] = useState('');
  const [goalPillarId, setGoalPillarId] = useState(direction.pillars[0]?.id ?? 'p-1');
  const [goalDepartmentId, setGoalDepartmentId] = useState<string>('');
  const [goalQuarter, setGoalQuarter] = useState('Q4 2026');
  const [goalDate, setGoalDate] = useState('2026-12-31');
  const [goalProgress, setGoalProgress] = useState(0);
  const [goalStatus, setGoalStatus] = useState<StrategicGoal['status']>('Active');
  const [goalLinkedSkills, setGoalLinkedSkills] = useState<string[]>([]);
  const [skillInput, setSkillInput] = useState('');

  // Value Edit State for Direction
  const [newValueInput, setNewValueInput] = useState('');

  function openCreateGoalModal() {
    setEditingGoal(null);
    setGoalTitle('');
    setGoalDescription('');
    setGoalPillarId(direction.pillars[0]?.id ?? 'p-1');
    setGoalDepartmentId('');
    setGoalQuarter('Q4 2026');
    setGoalDate('2026-12-31');
    setGoalProgress(0);
    setGoalStatus('Active');
    setGoalLinkedSkills([]);
    setGoalModalOpen(true);
  }

  function openEditGoalModal(goal: StrategicGoal) {
    setEditingGoal(goal);
    setGoalTitle(goal.title);
    setGoalDescription(goal.description);
    setGoalPillarId(goal.pillarId);
    setGoalDepartmentId(goal.departmentId ?? '');
    setGoalQuarter(goal.targetQuarter);
    setGoalDate(goal.targetDate);
    setGoalProgress(goal.progressPercentage);
    setGoalStatus(goal.status);
    setGoalLinkedSkills([...goal.linkedSkillNames]);
    setGoalModalOpen(true);
  }

  function handleGoalSubmit(e: FormEvent) {
    e.preventDefault();
    if (!goalTitle.trim()) return;

    if (editingGoal) {
      const updated = goals.map((g) =>
        g.id === editingGoal.id
          ? {
              ...g,
              title: goalTitle.trim(),
              description: goalDescription.trim(),
              pillarId: goalPillarId,
              departmentId: goalDepartmentId || null,
              targetQuarter: goalQuarter,
              targetDate: goalDate,
              progressPercentage: Number(goalProgress),
              status: goalStatus,
              linkedSkillNames: goalLinkedSkills,
            }
          : g,
      );
      saveGoalsState(updated);
    } else {
      const newGoal: StrategicGoal = {
        id: `g-${Date.now()}`,
        title: goalTitle.trim(),
        description: goalDescription.trim(),
        pillarId: goalPillarId,
        departmentId: goalDepartmentId || null,
        targetQuarter: goalQuarter,
        targetDate: goalDate,
        progressPercentage: Number(goalProgress),
        status: goalStatus,
        linkedSkillNames: goalLinkedSkills,
      };
      saveGoalsState([newGoal, ...goals]);
    }

    setGoalModalOpen(false);
  }

  function handleDeleteGoal(goalId: string, title: string) {
    if (window.confirm(t('businessGoals.deleteGoalConfirm', { title }))) {
      saveGoalsState(goals.filter((g) => g.id !== goalId));
    }
  }

  function addCoreValue() {
    if (!newValueInput.trim()) return;
    const val = newValueInput.trim();
    if (!direction.coreValues.includes(val)) {
      saveDirectionState({
        ...direction,
        coreValues: [...direction.coreValues, val],
      });
    }
    setNewValueInput('');
  }

  function removeCoreValue(val: string) {
    saveDirectionState({
      ...direction,
      coreValues: direction.coreValues.filter((v) => v !== val),
    });
  }

  function addSkillTag() {
    if (!skillInput.trim()) return;
    const val = skillInput.trim();
    if (!goalLinkedSkills.includes(val)) {
      setGoalLinkedSkills([...goalLinkedSkills, val]);
    }
    setSkillInput('');
  }

  function removeSkillTag(val: string) {
    setGoalLinkedSkills(goalLinkedSkills.filter((s) => s !== val));
  }

  // Quick stats
  const completedCount = goals.filter((g) => g.status === 'Completed').length;
  const inProgressCount = goals.filter((g) => g.status === 'InProgress' || g.status === 'Active').length;
  const overallProgress =
    goals.length === 0
      ? 0
      : Math.round(
          goals.reduce((acc, g) => acc + g.progressPercentage, 0) / goals.length,
        );

  // Search/Filters for Goals
  const [goalSearch, setGoalSearch] = useState('');
  const [goalStatusFilter, setGoalStatusFilter] = useState<string>('all');

  const filteredGoals = useMemo(() => {
    return goals.filter((g) => {
      const matchesSearch =
        !goalSearch.trim() ||
        g.title.toLowerCase().includes(goalSearch.toLowerCase()) ||
        g.description.toLowerCase().includes(goalSearch.toLowerCase()) ||
        g.linkedSkillNames.some((s) => s.toLowerCase().includes(goalSearch.toLowerCase()));

      const matchesStatus = goalStatusFilter === 'all' || g.status === goalStatusFilter;

      return matchesSearch && matchesStatus;
    });
  }, [goals, goalSearch, goalStatusFilter]);

  const inputClass =
    'mt-1 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 outline-none transition focus:border-brand-500 focus:ring-1 focus:ring-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
  const labelClass = 'block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-white/50';

  return (
    <div className="mx-auto max-w-6xl space-y-6 pb-16">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-3">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-rose-500 to-pink-600 text-white shadow-md shadow-rose-500/20">
            <Target className="h-6 w-6" />
          </span>
          <div>
            <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
              {t('businessGoals.title')}
            </h1>
            <p className="text-sm text-slate-500 dark:text-white/50">
              {t('businessGoals.subtitle', { tenant: user?.tenantName ?? '' })}
            </p>
          </div>
        </div>

        {savedBadge && (
          <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">
            <Check className="h-4 w-4" />
            {t('businessGoals.savedBadge')}
          </span>
        )}
      </div>

      {/* Summary Stat Cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-500 dark:text-white/50">
              {t('businessGoals.overallProgress')}
            </span>
            <TrendingUp className="h-4 w-4 text-emerald-500" />
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900 dark:text-white">
            {overallProgress}%
          </p>
          <div className="mt-2.5 h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
            <div
              className="h-full rounded-full bg-gradient-to-r from-brand-600 to-indigo-500 transition-all duration-500"
              style={{ width: `${overallProgress}%` }}
            />
          </div>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-500 dark:text-white/50">
              {t('businessGoals.activeGoals')}
            </span>
            <Target className="h-4 w-4 text-sky-500" />
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900 dark:text-white">
            {inProgressCount} <span className="text-sm font-normal text-slate-400">{t('businessGoals.totalRatio', { total: goals.length })}</span>
          </p>
          <p className="mt-2 text-xs text-slate-400">
            {t('businessGoals.completedGoalsRatio', { count: completedCount })}
          </p>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-500 dark:text-white/50">
              {t('businessGoals.strategicPillars')}
            </span>
            <Compass className="h-4 w-4 text-violet-500" />
          </div>
          <p className="mt-2 text-2xl font-bold text-slate-900 dark:text-white">
            {direction.pillars.length}
          </p>
          <p className="mt-2 text-xs text-slate-400">
            {t('businessGoals.pillarsDescription')}
          </p>
        </div>
      </div>

      {/* Navigation Tabs */}
      <div className="border-b border-slate-200 dark:border-white/10">
        <nav className="-mb-px flex space-x-6">
          <button
            type="button"
            onClick={() => setActiveTab('company-direction')}
            className={`flex items-center gap-2 border-b-2 py-3 text-sm font-medium transition ${
              activeTab === 'company-direction'
                ? 'border-brand-600 text-brand-600 dark:border-brand-400 dark:text-brand-400'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
            }`}
          >
            <Compass className="h-4 w-4" />
            {t('businessGoals.tabCompanyDirection')}
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('strategic-goals')}
            className={`flex items-center gap-2 border-b-2 py-3 text-sm font-medium transition ${
              activeTab === 'strategic-goals'
                ? 'border-brand-600 text-brand-600 dark:border-brand-400 dark:text-brand-400'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
            }`}
          >
            <Target className="h-4 w-4" />
            {t('businessGoals.tabStrategicGoals', { count: goals.length })}
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('capability-alignment')}
            className={`flex items-center gap-2 border-b-2 py-3 text-sm font-medium transition ${
              activeTab === 'capability-alignment'
                ? 'border-brand-600 text-brand-600 dark:border-brand-400 dark:text-brand-400'
                : 'border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-700 dark:text-white/50 dark:hover:text-white'
            }`}
          >
            <GitBranch className="h-4 w-4" />
            {t('businessGoals.tabCapabilityAlignment')}
          </button>
        </nav>
      </div>

      {/* TAB 1: Company Direction */}
      {activeTab === 'company-direction' && (
        <div className="space-y-6">
          {/* Vision & Mission */}
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <div className="flex items-center gap-2 text-brand-600 dark:text-brand-400">
              <Compass className="h-5 w-5" />
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                {t('businessGoals.visionMissionTitle')}
              </h2>
            </div>
            <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
              {t('businessGoals.visionMissionSubtitle', { tenant: user?.tenantName ?? '' })}
            </p>

            <div className="mt-6 space-y-5">
              <div>
                <label className={labelClass}>{t('businessGoals.visionLabel')}</label>
                <textarea
                  rows={3}
                  value={direction.vision}
                  onChange={(e) =>
                    saveDirectionState({ ...direction, vision: e.target.value })
                  }
                  placeholder={t('businessGoals.visionPlaceholder')}
                  className={inputClass}
                />
              </div>

              <div>
                <label className={labelClass}>{t('businessGoals.missionLabel')}</label>
                <textarea
                  rows={3}
                  value={direction.mission}
                  onChange={(e) =>
                    saveDirectionState({ ...direction, mission: e.target.value })
                  }
                  placeholder={t('businessGoals.missionPlaceholder')}
                  className={inputClass}
                />
              </div>
            </div>
          </section>

          {/* Core Values */}
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <div className="flex items-center gap-2 text-violet-600 dark:text-violet-400">
              <Award className="h-5 w-5" />
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                {t('businessGoals.coreValuesTitle')}
              </h2>
            </div>
            <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
              {t('businessGoals.coreValuesSubtitle')}
            </p>

            <div className="mt-4 flex flex-wrap gap-2">
              {direction.coreValues.map((val) => (
                <span
                  key={val}
                  className="inline-flex items-center gap-1.5 rounded-full bg-violet-50 px-3.5 py-1.5 text-xs font-semibold text-violet-700 dark:bg-violet-500/15 dark:text-violet-300"
                >
                  {val}
                  <button
                    type="button"
                    onClick={() => removeCoreValue(val)}
                    className="rounded-full p-0.5 hover:bg-violet-200 dark:hover:bg-violet-500/30"
                  >
                    <X className="h-3 w-3" />
                  </button>
                </span>
              ))}
            </div>

            <div className="mt-4 flex max-w-md gap-2">
              <input
                type="text"
                value={newValueInput}
                onChange={(e) => setNewValueInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    addCoreValue();
                  }
                }}
                placeholder={t('businessGoals.addValuePlaceholder')}
                className="w-full rounded-full border border-slate-200 bg-white px-4 py-2 text-xs text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
              />
              <button
                type="button"
                onClick={addCoreValue}
                disabled={!newValueInput.trim()}
                className="rounded-full bg-violet-600 px-4 py-2 text-xs font-semibold text-white hover:bg-violet-700 disabled:opacity-50"
              >
                {t('businessGoals.addValueButton')}
              </button>
            </div>
          </section>

          {/* Strategic Pillars */}
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <div className="flex items-center gap-2 text-indigo-600 dark:text-indigo-400">
              <Layers className="h-5 w-5" />
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                {t('businessGoals.pillarsTitle')}
              </h2>
            </div>
            <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
              {t('businessGoals.pillarsSubtitle')}
            </p>

            <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
              {direction.pillars.map((pillar, idx) => (
                <div
                  key={pillar.id}
                  className="relative overflow-hidden rounded-2xl border border-slate-200 bg-slate-50/50 p-5 dark:border-white/10 dark:bg-white/[0.02]"
                >
                  <span
                    className={`absolute left-0 top-0 bottom-0 w-1.5 bg-gradient-to-b ${pillar.color}`}
                  />
                  <div className="pl-2">
                    <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400">
                      {t('businessGoals.pillarLabel', { index: idx + 1 })}
                    </span>
                    <h3 className="mt-1 text-sm font-semibold text-slate-900 dark:text-white">
                      {pillar.title}
                    </h3>
                    <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
                      {pillar.description}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </section>
        </div>
      )}

      {/* TAB 2: Strategic Goals */}
      {activeTab === 'strategic-goals' && (
        <div className="space-y-6">
          {/* Action & Filter Bar */}
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-1 items-center gap-3">
              <div className="relative flex-1 max-w-md">
                <Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                <input
                  type="text"
                  value={goalSearch}
                  onChange={(e) => setGoalSearch(e.target.value)}
                  placeholder={t('businessGoals.searchPlaceholder')}
                  className="w-full rounded-full border border-slate-200 bg-white pl-10 pr-4 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </div>

              <select
                value={goalStatusFilter}
                onChange={(e) => setGoalStatusFilter(e.target.value)}
                className="rounded-full border border-slate-200 bg-white px-4 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
              >
                <option value="all">{t('businessGoals.allStatuses')}</option>
                <option value="Active">{t('businessGoals.statusActive')}</option>
                <option value="InProgress">{t('businessGoals.statusInProgress')}</option>
                <option value="Completed">{t('businessGoals.statusCompleted')}</option>
                <option value="Paused">{t('businessGoals.statusPaused')}</option>
              </select>
            </div>

            <button
              type="button"
              onClick={openCreateGoalModal}
              className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-rose-600 to-pink-600 px-5 py-2.5 text-sm font-semibold text-white shadow-md shadow-rose-500/20 transition hover:brightness-110"
            >
              <Plus className="h-4 w-4" />
              {t('businessGoals.newGoalButton')}
            </button>
          </div>

          {/* Goals List */}
          {filteredGoals.length === 0 ? (
            <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 p-12 text-center dark:border-white/10 dark:bg-white/5">
              <Target className="h-10 w-10 text-slate-300 dark:text-white/20" />
              <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">
                {t('businessGoals.noGoalsTitle')}
              </h3>
              <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
                {t('businessGoals.noGoalsSubtitle')}
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {filteredGoals.map((goal) => {
                const pillar = direction.pillars.find((p) => p.id === goal.pillarId);
                const dept = departments.find((d) => d.departmentId === goal.departmentId);

                return (
                  <div
                    key={goal.id}
                    className="group relative rounded-2xl border border-slate-200 bg-white p-6 shadow-sm transition hover:border-slate-300 dark:border-white/10 dark:bg-white/5 dark:hover:border-white/20"
                  >
                    <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                      <div className="space-y-2 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span
                            className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                              statusMeta[goal.status]?.className ?? statusMeta.Active.className
                            }`}
                          >
                            {statusMeta[goal.status]?.label ?? goal.status}
                          </span>

                          {pillar && (
                            <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-600 dark:bg-white/10 dark:text-white/70">
                              {pillar.title}
                            </span>
                          )}

                          <span className="flex items-center gap-1 text-xs text-slate-400">
                            <Clock className="h-3.5 w-3.5" />
                            {goal.targetQuarter} ({goal.targetDate})
                          </span>
                        </div>

                        <h3 className="text-base font-semibold text-slate-900 dark:text-white">
                          {goal.title}
                        </h3>

                        <p className="text-xs text-slate-600 dark:text-white/70">
                          {goal.description}
                        </p>

                        {dept && (
                          <div className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-white/50">
                            <Building2 className="h-3.5 w-3.5 text-slate-400" />
                            <span>{t('businessGoals.deptResponsible', { dept: dept.name })}</span>
                          </div>
                        )}

                        {goal.linkedSkillNames.length > 0 && (
                          <div className="flex flex-wrap items-center gap-1.5 pt-1">
                            <span className="text-[11px] font-medium text-slate-400">
                              {t('businessGoals.linkedSkills')}
                            </span>
                            {goal.linkedSkillNames.map((s) => (
                              <span
                                key={s}
                                className="rounded-md bg-indigo-50 px-2 py-0.5 text-[11px] font-medium text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300"
                              >
                                {s}
                              </span>
                            ))}
                          </div>
                        )}

                        {/* Contributing Department Objectives */}
                        {(() => {
                          const contributing = getContributingObjectivesForStrategicGoal(
                            tenantId,
                            goal.id,
                            departments,
                          );
                          if (contributing.length === 0) return null;
                          return (
                            <div className="flex flex-wrap items-center gap-1.5 pt-1">
                              <span className="text-[11px] font-medium text-slate-400">
                                {t('businessGoals.contributingDeptObjectives', { count: contributing.length })}:
                              </span>
                              {contributing.map((c) => (
                                <button
                                  key={c.objective.id}
                                  type="button"
                                  onClick={() => navigate(`/departamentos/${c.departmentId}`)}
                                  className="inline-flex items-center gap-1 rounded-md bg-purple-50 px-2 py-0.5 text-[11px] font-medium text-purple-700 transition hover:bg-purple-100 dark:bg-purple-500/15 dark:text-purple-300"
                                >
                                  <Building2 className="h-3 w-3" />
                                  <span className="font-semibold">{c.departmentName}:</span> {c.objective.title}
                                </button>
                              ))}
                            </div>
                          );
                        })()}
                      </div>

                      {/* Right side: progress + actions */}
                      <div className="flex sm:flex-col items-end justify-between sm:justify-start gap-4 min-w-[180px]">
                        <div className="flex items-center gap-2 opacity-0 transition group-hover:opacity-100">
                          <button
                            type="button"
                            title="Editar objetivo"
                            onClick={() => openEditGoalModal(goal)}
                            className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                          >
                            <Pencil className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            title="Eliminar"
                            onClick={() => handleDeleteGoal(goal.id, goal.title)}
                            className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400"
                          >
                            <Trash2 className="h-4 w-4" />
                          </button>
                        </div>

                        <div className="w-full text-right">
                          <span className="text-xs font-semibold text-slate-700 dark:text-white/80">
                            {t('businessGoals.progressLabel', { percentage: goal.progressPercentage })}
                          </span>
                          <div className="mt-1 h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                            <div
                              className="h-full rounded-full bg-gradient-to-r from-rose-500 to-pink-500 transition-all duration-300"
                              style={{ width: `${goal.progressPercentage}%` }}
                            />
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}

      {/* TAB 3: Capability Alignment */}
      {activeTab === 'capability-alignment' && (
        <div className="space-y-6">
          <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/5 sm:p-8">
            <div className="flex items-center gap-2 text-indigo-600 dark:text-indigo-400">
              <GitBranch className="h-5 w-5" />
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                {t('businessGoals.alignmentTitle')}
              </h2>
            </div>
            <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
              {t('businessGoals.alignmentSubtitle')}
            </p>

            <div className="mt-6 space-y-6">
              {goals.map((goal) => {
                // Find matching roles or skills in the system
                const matchingSkills = skills.filter((s) =>
                  goal.linkedSkillNames.some(
                    (ls) =>
                      s.name.toLowerCase().includes(ls.toLowerCase()) ||
                      ls.toLowerCase().includes(s.name.toLowerCase()),
                  ),
                );

                const matchingRoles = jobRoles.filter((r) =>
                  r.technicalSkills.some((ts) =>
                    goal.linkedSkillNames.some((ls) =>
                      ts.toLowerCase().includes(ls.toLowerCase()),
                    ),
                  ) ||
                  r.softSkills.some((ss) =>
                    goal.linkedSkillNames.some((ls) =>
                      ss.toLowerCase().includes(ls.toLowerCase()),
                    ),
                  ),
                );

                return (
                  <div
                    key={goal.id}
                    className="rounded-2xl border border-slate-200 bg-slate-50/50 p-5 dark:border-white/10 dark:bg-white/[0.02]"
                  >
                    <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 border-b border-slate-200 pb-3 dark:border-white/10">
                      <div>
                        <span className="text-[11px] font-bold uppercase tracking-wider text-rose-600 dark:text-rose-400">
                          {t('businessGoals.strategicGoalHeader')}
                        </span>
                        <h3 className="text-sm font-semibold text-slate-900 dark:text-white">
                          {goal.title}
                        </h3>
                      </div>
                      <span
                        className={`inline-block w-fit rounded-full px-2.5 py-0.5 text-xs font-semibold ${
                          statusMeta[goal.status]?.className
                        }`}
                      >
                        {statusMeta[goal.status]?.label}
                      </span>
                    </div>

                    <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
                      {/* Skills in Catalog */}
                      <div className="space-y-2">
                        <p className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 dark:text-white/80">
                          <GraduationCap className="h-4 w-4 text-teal-600 dark:text-teal-400" />
                          {t('businessGoals.linkedSkillsCatalog', { count: goal.linkedSkillNames.length })}
                        </p>
                        <div className="flex flex-wrap gap-1.5">
                          {goal.linkedSkillNames.map((skName) => {
                            const found = matchingSkills.find((s) => s.name.toLowerCase() === skName.toLowerCase());
                            return (
                              <span
                                key={skName}
                                className="rounded-lg bg-teal-50 px-2.5 py-1 text-xs font-medium text-teal-700 dark:bg-teal-500/15 dark:text-teal-300"
                              >
                                {skName} {found?.matchedCapabilityName ? `✓ (${found.matchedCapabilityName})` : ''}
                              </span>
                            );
                          })}
                        </div>
                      </div>

                      {/* Targeted Job Roles */}
                      <div className="space-y-2">
                        <p className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 dark:text-white/80">
                          <Briefcase className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                          {t('businessGoals.impactedRoles', { count: matchingRoles.length })}
                        </p>
                        {matchingRoles.length === 0 ? (
                          <p className="text-xs text-slate-400 italic">
                            {t('businessGoals.noImpactedRoles')}
                          </p>
                        ) : (
                          <div className="flex flex-wrap gap-1.5">
                            {matchingRoles.map((role) => (
                              <span
                                key={role.jobRoleId}
                                className="rounded-lg bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-700 dark:bg-amber-500/15 dark:text-amber-300"
                              >
                                {role.title}
                              </span>
                            ))}
                          </div>
                        )}
                      </div>

                      {/* Contributing Department Objectives */}
                      {(() => {
                        const contributing = getContributingObjectivesForStrategicGoal(
                          tenantId,
                          goal.id,
                          departments,
                        );
                        return (
                          <div className="space-y-2 sm:col-span-2 border-t border-slate-200/60 pt-3 dark:border-white/5">
                            <p className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 dark:text-white/80">
                              <Building2 className="h-4 w-4 text-purple-600 dark:text-purple-400" />
                              {t('businessGoals.contributingDeptObjectives', { count: contributing.length })}
                            </p>
                            {contributing.length === 0 ? (
                              <p className="text-xs text-slate-400 italic">
                                {t('businessGoals.noContributingDeptObjectives')}
                              </p>
                            ) : (
                              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                                {contributing.map((c) => (
                                  <div
                                    key={c.objective.id}
                                    className="flex items-center justify-between rounded-xl border border-purple-100 bg-white p-3 text-xs dark:border-purple-500/20 dark:bg-slate-900"
                                  >
                                    <div className="min-w-0 flex-1 pr-2">
                                      <span className="inline-block rounded bg-purple-100 px-1.5 py-0.5 text-[10px] font-semibold text-purple-800 dark:bg-purple-500/20 dark:text-purple-300">
                                        {c.departmentName}
                                      </span>
                                      <p className="mt-1 truncate font-medium text-slate-900 dark:text-white">
                                        {c.objective.title}
                                      </p>
                                      <span className="text-[10px] text-slate-400">
                                        {c.objective.targetQuarter} · {t('businessGoals.progressLabel', { percentage: c.objective.progressPercentage })}
                                      </span>
                                    </div>
                                    <button
                                      type="button"
                                      onClick={() => navigate(`/departamentos/${c.departmentId}`)}
                                      className="shrink-0 text-xs font-medium text-indigo-600 hover:underline dark:text-indigo-400"
                                    >
                                      {t('businessGoals.viewDepartment')}
                                    </button>
                                  </div>
                                ))}
                              </div>
                            )}
                          </div>
                        );
                      })()}
                    </div>
                  </div>
                );
              })}
            </div>
          </section>
        </div>
      )}

      {/* Goal Create/Edit Modal */}
      {goalModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm">
          <div className="w-full max-w-xl rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900">
            <div className="flex items-center justify-between border-b border-slate-100 pb-4 dark:border-white/10">
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                {editingGoal ? t('businessGoals.modalTitleEdit') : t('businessGoals.modalTitleNew')}
              </h2>
              <button
                type="button"
                onClick={() => setGoalModalOpen(false)}
                className="rounded-full p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <form onSubmit={handleGoalSubmit} className="mt-4 space-y-4">
              <div>
                <label className={labelClass}>{t('businessGoals.modalGoalTitleLabel')}</label>
                <input
                  required
                  value={goalTitle}
                  onChange={(e) => setGoalTitle(e.target.value)}
                  placeholder={t('businessGoals.modalGoalTitlePlaceholder')}
                  className={inputClass}
                />
              </div>

              <div>
                <label className={labelClass}>{t('businessGoals.modalGoalDescLabel')}</label>
                <textarea
                  rows={2}
                  value={goalDescription}
                  onChange={(e) => setGoalDescription(e.target.value)}
                  placeholder={t('businessGoals.modalGoalDescPlaceholder')}
                  className={inputClass}
                />
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label className={labelClass}>{t('businessGoals.modalPillarLabel')}</label>
                  <select
                    value={goalPillarId}
                    onChange={(e) => setGoalPillarId(e.target.value)}
                    className={inputClass}
                  >
                    {direction.pillars.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.title}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className={labelClass}>{t('businessGoals.modalDeptLabel')}</label>
                  <select
                    value={goalDepartmentId}
                    onChange={(e) => setGoalDepartmentId(e.target.value)}
                    className={inputClass}
                  >
                    <option value="">{t('businessGoals.modalEntireCompany')}</option>
                    {departments.map((d) => (
                      <option key={d.departmentId} value={d.departmentId}>
                        {d.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className={labelClass}>{t('businessGoals.modalQuarterLabel')}</label>
                  <input
                    value={goalQuarter}
                    onChange={(e) => setGoalQuarter(e.target.value)}
                    placeholder="Q4 2026"
                    className={inputClass}
                  />
                </div>

                <div>
                  <label className={labelClass}>{t('businessGoals.modalDateLabel')}</label>
                  <input
                    type="date"
                    value={goalDate}
                    onChange={(e) => setGoalDate(e.target.value)}
                    className={inputClass}
                  />
                </div>

                <div>
                  <label className={labelClass}>{t('businessGoals.modalStatusLabel')}</label>
                  <select
                    value={goalStatus}
                    onChange={(e) =>
                      setGoalStatus(e.target.value as StrategicGoal['status'])
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
                  <label className={labelClass}>{t('businessGoals.modalProgressLabel', { percentage: goalProgress })}</label>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    value={goalProgress}
                    onChange={(e) => setGoalProgress(Number(e.target.value))}
                    className="mt-3 w-full accent-rose-600"
                  />
                </div>
              </div>

              <div>
                <label className={labelClass}>{t('businessGoals.modalLinkedSkillsLabel')}</label>
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {goalLinkedSkills.map((s) => (
                    <span
                      key={s}
                      className="inline-flex items-center gap-1 rounded-md bg-indigo-50 px-2 py-1 text-xs font-medium text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300"
                    >
                      {s}
                      <button
                        type="button"
                        onClick={() => removeSkillTag(s)}
                        className="rounded hover:bg-indigo-200 dark:hover:bg-indigo-500/30"
                      >
                        <X className="h-3 w-3" />
                      </button>
                    </span>
                  ))}
                </div>

                <div className="mt-2 flex gap-2">
                  <input
                    type="text"
                    value={skillInput}
                    onChange={(e) => setSkillInput(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter') {
                        e.preventDefault();
                        addSkillTag();
                      }
                    }}
                    placeholder={t('businessGoals.modalSkillInputPlaceholder')}
                    className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                  />
                  <button
                    type="button"
                    onClick={addSkillTag}
                    disabled={!skillInput.trim()}
                    className="rounded-xl bg-slate-800 px-3 py-2 text-xs font-medium text-white hover:bg-slate-700 disabled:opacity-50"
                  >
                    {t('businessGoals.modalLinkButton')}
                  </button>
                </div>
              </div>

              <div className="mt-6 flex justify-end gap-3 border-t border-slate-100 pt-4 dark:border-white/10">
                <button
                  type="button"
                  onClick={() => setGoalModalOpen(false)}
                  className="rounded-full px-4 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
                >
                  {t('businessGoals.modalCancelButton')}
                </button>
                <button
                  type="submit"
                  disabled={!goalTitle.trim()}
                  className="rounded-full bg-rose-600 px-5 py-2 text-sm font-semibold text-white transition hover:bg-rose-700 disabled:opacity-50"
                >
                  {editingGoal ? t('businessGoals.modalSaveEditButton') : t('businessGoals.modalSaveCreateButton')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
