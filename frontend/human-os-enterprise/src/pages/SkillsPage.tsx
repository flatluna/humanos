import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useLocation, useNavigate } from 'react-router';
import {
  GraduationCap,
  Heart,
  Building2,
  ShieldCheck,
  Plus,
  Pencil,
  Trash2,
  RefreshCw,
  BookOpen,
  PlayCircle,
  ExternalLink,
  Search,
  X,
  Filter,
  Sparkles,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  deleteSkill,
  getSkills,
  syncSkillsFromJobRoles,
  type SkillDto,
} from '@/api/skillsApi';

const CATEGORY_TABS = [
  {
    id: 'job-skills',
    labelKey: 'skills.categoryTabs.job-skills.label',
    descKey: 'skills.categoryTabs.job-skills.description',
    icon: GraduationCap,
    color: 'amber',
  },
  {
    id: 'soft-skills',
    labelKey: 'skills.categoryTabs.soft-skills.label',
    descKey: 'skills.categoryTabs.soft-skills.description',
    icon: Heart,
    color: 'violet',
  },
  {
    id: 'department-processes',
    labelKey: 'skills.categoryTabs.department-processes.label',
    descKey: 'skills.categoryTabs.department-processes.description',
    icon: Building2,
    color: 'sky',
  },
  {
    id: 'hr-processes',
    labelKey: 'skills.categoryTabs.hr-processes.label',
    descKey: 'skills.categoryTabs.hr-processes.description',
    icon: ShieldCheck,
    color: 'rose',
  },
] as const;

type CategoryTabId = (typeof CATEGORY_TABS)[number]['id'];
type CourseFilter = 'all' | 'with-course' | 'no-course' | 'linked' | 'unlinked';

const CAPABILITY_STUDIO_URL = 'http://localhost:3010';

function buildGenerateCourseUrl(skill: SkillDto, user: { oid: string; tid: string; email: string } | null): string {
  const params = new URLSearchParams();
  params.set('skillId', skill.skillId);
  params.set('name', skill.name);
  if (skill.description) params.set('description', skill.description);
  params.set('category', skill.category);
  params.set('targetSkill', skill.name);
  if (skill.documentStoragePath) {
    params.set('hasDoc', 'true');
    params.set('docFileName', skill.documentFileName || 'document.pdf');
  }
  const studioPath = `/new-structured-learning?${params.toString()}`;
  if (user) {
    return `http://localhost:7071/api/humanos/studio-launch?oid=${encodeURIComponent(user.oid)}&tid=${encodeURIComponent(user.tid)}&email=${encodeURIComponent(user.email)}&returnTo=${encodeURIComponent(studioPath)}`;
  }
  return `${CAPABILITY_STUDIO_URL}${studioPath}`;
}

function buildCapabilityStudioDetailUrl(
  capabilityId: string,
  courseType?: string | null,
  user?: { oid: string; tid: string; email: string } | null,
): string {
  const studioPath =
    courseType === 'graph'
      ? `/capabilities/${capabilityId}`
      : `/structured-learning/${capabilityId}`;

  if (user) {
    return `http://localhost:7071/api/humanos/studio-launch?oid=${encodeURIComponent(user.oid)}&tid=${encodeURIComponent(user.tid)}&email=${encodeURIComponent(user.email)}&returnTo=${encodeURIComponent(studioPath)}`;
  }
  return `${CAPABILITY_STUDIO_URL}${studioPath}`;
}

function isCategoryTabId(value: string | undefined): value is CategoryTabId {
  return CATEGORY_TABS.some((tab) => tab.id === value);
}

const COLOR_STYLES: Record<(typeof CATEGORY_TABS)[number]['color'], { chip: string; button: string; border: string }> = {
  amber: {
    chip: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
    button: 'bg-gradient-to-r from-amber-500 to-amber-400 shadow-amber-500/30',
    border: 'border-amber-200 dark:border-amber-500/30',
  },
  violet: {
    chip: 'bg-violet-100 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300',
    button: 'bg-gradient-to-r from-violet-600 to-violet-500 shadow-violet-500/30',
    border: 'border-violet-200 dark:border-violet-500/30',
  },
  sky: {
    chip: 'bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300',
    button: 'bg-gradient-to-r from-sky-600 to-sky-500 shadow-sky-500/30',
    border: 'border-sky-200 dark:border-sky-500/30',
  },
  rose: {
    chip: 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',
    button: 'bg-gradient-to-r from-rose-600 to-rose-500 shadow-rose-500/30',
    border: 'border-rose-200 dark:border-rose-500/30',
  },
};

export function SkillsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const location = useLocation();
  const navigate = useNavigate();

  const [searchTerm, setSearchTerm] = useState('');
  const [searchAllCategories, setSearchAllCategories] = useState(false);
  const [courseFilter, setCourseFilter] = useState<CourseFilter>('all');

  const pathSegment = location.pathname.split('/').filter(Boolean).pop();
  const activeCategory: CategoryTabId = isCategoryTabId(pathSegment) ? pathSegment : 'job-skills';
  const activeTab = CATEGORY_TABS.find((tab) => tab.id === activeCategory)!;
  const activeStyles = COLOR_STYLES[activeTab.color];

  // Fetch all skills for the tenant to enable instant cross-category search and accurate tab counts
  const allSkillsQuery = useQuery({
    queryKey: ['skills', tenantId],
    queryFn: () => getSkills(tenantId),
  });

  const deleteMutation = useMutation({
    mutationFn: (skillId: string) => deleteSkill(skillId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
    },
  });

  const syncMutation = useMutation({
    mutationFn: () => syncSkillsFromJobRoles(tenantId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
    },
  });

  const allSkills = allSkillsQuery.data ?? [];

  // Filter skills based on search term, category scope, and course/link status
  const filteredSkills = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();

    return allSkills.filter((skill) => {
      // Category scope filter
      if (!searchAllCategories && skill.category !== activeCategory) {
        return false;
      }

      // Course / Role links filter
      const roles = skill.jobRoles ?? [];
      if (courseFilter === 'with-course' && !skill.matchedCapabilityId) return false;
      if (courseFilter === 'no-course' && skill.matchedCapabilityId) return false;
      if (courseFilter === 'linked' && roles.length === 0) return false;
      if (courseFilter === 'unlinked' && roles.length > 0) return false;

      // Text search matching
      if (!term) return true;

      const nameMatch = skill.name?.toLowerCase().includes(term) ?? false;
      const descMatch = skill.description?.toLowerCase().includes(term) ?? false;
      const roleMatch = roles.some(
        (r) =>
          (r.jobRoleTitle && r.jobRoleTitle.toLowerCase().includes(term)) ||
          (r.departmentName && r.departmentName.toLowerCase().includes(term)),
      );

      return nameMatch || descMatch || roleMatch;
    });
  }, [allSkills, searchTerm, searchAllCategories, activeCategory, courseFilter]);

  // Compute counts per category tab to display helpful badges
  const categoryCounts = useMemo(() => {
    const term = searchTerm.trim().toLowerCase();
    const counts: Record<CategoryTabId, number> = {
      'job-skills': 0,
      'soft-skills': 0,
      'department-processes': 0,
      'hr-processes': 0,
    };

    for (const skill of allSkills) {
      if (!counts.hasOwnProperty(skill.category)) continue;

      if (term) {
        const nameMatch = skill.name?.toLowerCase().includes(term) ?? false;
        const descMatch = skill.description?.toLowerCase().includes(term) ?? false;
        const roleMatch = (skill.jobRoles ?? []).some(
          (r) =>
            (r.jobRoleTitle && r.jobRoleTitle.toLowerCase().includes(term)) ||
            (r.departmentName && r.departmentName.toLowerCase().includes(term)),
        );
        if (nameMatch || descMatch || roleMatch) {
          counts[skill.category]++;
        }
      } else {
        counts[skill.category]++;
      }
    }

    return counts;
  }, [allSkills, searchTerm]);

  const ActiveIcon = activeTab.icon;

  const handleDelete = (skill: SkillDto) => {
    if (window.confirm(t('common.confirmDelete', { name: skill.name }) || `Delete "${skill.name}"?`)) {
      deleteMutation.mutate(skill.skillId);
    }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">{t('skills.title')}</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {t('skills.subtitle')}
          </p>
        </div>
        <button
          type="button"
          onClick={() => navigate(`/skills/nuevo?category=${activeCategory}`)}
          className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-4 py-2 text-xs font-semibold text-white shadow-md transition hover:brightness-105 ${activeStyles.button}`}
        >
          <Plus className="h-4 w-4" />
          {t('skills.newSkill')}
        </button>
      </div>

      {/* Sync Banner */}
      <div className="flex items-center justify-between gap-3 rounded-2xl border border-dashed border-slate-200 bg-slate-50/60 px-4 py-3 dark:border-white/10 dark:bg-white/5">
        <p className="text-xs text-slate-500 dark:text-white/50">
          {t('skills.syncBannerText')}
        </p>
        <button
          type="button"
          onClick={() => syncMutation.mutate()}
          disabled={syncMutation.isPending}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 shadow-sm transition hover:bg-slate-50 disabled:opacity-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${syncMutation.isPending ? 'animate-spin' : ''}`} />
          {syncMutation.isPending ? t('skills.syncing') : t('skills.syncButton')}
        </button>
      </div>

      {syncMutation.isSuccess && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-2.5 text-xs text-emerald-700 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-300">
          {t('skills.syncedSuccess', { count: syncMutation.data.syncedJobRoleCount })}
        </div>
      )}

      {/* Smart Search Bar & Filter Options */}
      <div className="space-y-3 rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/5">
        <div className="relative">
          <Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder={t('skills.searchPlaceholder')}
            className="w-full rounded-xl border border-slate-200 bg-slate-50/50 py-2.5 pl-10 pr-24 text-sm text-slate-900 outline-none transition focus:border-brand-500 focus:bg-white dark:border-white/10 dark:bg-white/5 dark:text-white dark:focus:border-brand-400 dark:focus:bg-white/10"
          />
          {searchTerm && (
            <button
              type="button"
              onClick={() => setSearchTerm('')}
              className="absolute right-3 top-1/2 -translate-y-1/2 rounded-full p-1 text-slate-400 hover:bg-slate-200 dark:hover:bg-white/10"
              title={t('skills.clearSearch')}
            >
              <X className="h-4 w-4" />
            </button>
          )}
        </div>

        {/* Quick Filter Pills */}
        <div className="flex flex-wrap items-center justify-between gap-2 pt-1 text-xs">
          <div className="flex flex-wrap items-center gap-1.5">
            <span className="flex items-center gap-1 font-medium text-slate-400 dark:text-white/40">
              <Filter className="h-3 w-3" />
            </span>
            {(
              [
                { id: 'all', label: t('skills.allFilter') },
                { id: 'with-course', label: t('skills.withCourseFilter') },
                { id: 'no-course', label: t('skills.noCourseFilter') },
                { id: 'linked', label: t('skills.linkedFilter') },
                { id: 'unlinked', label: t('skills.unlinkedFilter') },
              ] as const
            ).map((filter) => (
              <button
                key={filter.id}
                type="button"
                onClick={() => setCourseFilter(filter.id)}
                className={`rounded-lg px-2.5 py-1 font-medium transition ${
                  courseFilter === filter.id
                    ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-white/10 dark:text-white/70 dark:hover:bg-white/15'
                }`}
              >
                {filter.label}
              </button>
            ))}
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setSearchAllCategories((prev) => !prev)}
              className={`inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs font-medium transition ${
                searchAllCategories
                  ? 'border-indigo-500 bg-indigo-50 text-indigo-700 dark:border-indigo-400/30 dark:bg-indigo-500/15 dark:text-indigo-300'
                  : 'border-slate-200 text-slate-600 hover:bg-slate-100 dark:border-white/10 dark:text-white/70 dark:hover:bg-white/10'
              }`}
            >
              <Sparkles className="h-3 w-3" />
              {searchAllCategories ? t('skills.searchAllCategories') : t('skills.searchInCurrent')}
            </button>
          </div>
        </div>
      </div>

      {/* Category Tabs */}
      {!searchAllCategories && (
        <div className="flex flex-wrap gap-2 border-b border-slate-200 pb-4 dark:border-white/10">
          {CATEGORY_TABS.map((tab) => {
            const Icon = tab.icon;
            const isActive = tab.id === activeCategory;
            const tabStyles = COLOR_STYLES[tab.color];
            const count = categoryCounts[tab.id];

            return (
              <Link
                key={tab.id}
                to={`/skills/${tab.id}`}
                className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-medium transition ${
                  isActive ? `text-white shadow-md ${tabStyles.button}` : `${tabStyles.chip} hover:brightness-95`
                }`}
              >
                <Icon className="h-3.5 w-3.5" />
                <span>{t(tab.labelKey as any)}</span>
                <span
                  className={`rounded-full px-1.5 py-0.2 text-[10px] font-bold ${
                    isActive ? 'bg-white/25 text-white' : 'bg-black/5 dark:bg-white/10'
                  }`}
                >
                  {count}
                </span>
              </Link>
            );
          })}
        </div>
      )}

      {/* Results Section */}
      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
        <div className="flex items-start justify-between gap-3 border-b border-slate-100 pb-3 dark:border-white/5">
          <div className="flex items-start gap-3">
            <span className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${activeStyles.chip}`}>
              <ActiveIcon className="h-4 w-4" />
            </span>
            <div>
              <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
                {searchAllCategories ? t('skills.searchAllCategories') : t(activeTab.labelKey as any)}
              </h2>
              <p className="text-xs text-slate-400 dark:text-white/40">
                {t('skills.searchResultsCount', { count: filteredSkills.length })}
              </p>
            </div>
          </div>
        </div>

        {allSkillsQuery.isLoading && (
          <p className="p-4 text-xs text-slate-400 dark:text-white/40">{t('common.loading')}</p>
        )}

        {allSkillsQuery.isSuccess && filteredSkills.length === 0 && (
          <div className="py-8 text-center">
            <p className="text-sm text-slate-500 dark:text-white/50">
              {searchTerm
                ? t('skills.noSearchResults', { query: searchTerm })
                : t('skills.emptyCategory')}
            </p>
            {searchTerm && (
              <button
                type="button"
                onClick={() => {
                  setSearchTerm('');
                  setCourseFilter('all');
                }}
                className="mt-2 text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
              >
                {t('skills.clearSearch')}
              </button>
            )}
          </div>
        )}

        {filteredSkills.length > 0 && (
          <div className="space-y-2">
            {filteredSkills.map((skill) => {
              const tabDef = CATEGORY_TABS.find((ct) => ct.id === skill.category) ?? activeTab;
              const styles = COLOR_STYLES[tabDef.color];

              return (
                <div
                  key={skill.skillId}
                  className="group flex items-start justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50/50 px-4 py-3 transition hover:border-slate-300 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:hover:border-white/20"
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-sm font-medium text-slate-900 dark:text-white">{skill.name}</p>
                      {searchAllCategories && (
                        <span className={`rounded-md px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider ${styles.chip}`}>
                          {t(tabDef.labelKey as any)}
                        </span>
                      )}
                    </div>

                    {skill.description && (
                      <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">{skill.description}</p>
                    )}

                    <div className="mt-1.5 flex flex-wrap gap-1">
                      {skill.jobRoles.length === 0 ? (
                        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] text-slate-400 dark:bg-white/10 dark:text-white/30">
                          {t('skills.noLinkedRoles')}
                        </span>
                      ) : (
                        skill.jobRoles.map((role) => (
                          <span
                            key={role.jobRoleId}
                            className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-white/10 dark:text-white/60"
                          >
                            <span>{role.jobRoleTitle}</span>
                            {role.departmentName && (
                              <span className="text-[10px] text-slate-400 dark:text-white/40">
                                ({role.departmentName})
                              </span>
                            )}
                          </span>
                        ))
                      )}
                    </div>

                    <div className="mt-2">
                      {skill.matchedCourses && skill.matchedCourses.length > 0 ? (
                        <div className="flex flex-wrap gap-2">
                          {skill.matchedCourses.map((course) => (
                            <a
                              key={`${course.courseId}-${course.languageCode}`}
                              href={buildCapabilityStudioDetailUrl(course.courseId, course.courseType, user)}
                              target="_blank"
                              rel="noreferrer"
                              className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold text-white shadow-sm transition ${course.languageCode === 'en' ? 'bg-amber-600 hover:bg-amber-500' : 'bg-emerald-600 hover:bg-emerald-500'}`}
                            >
                              <PlayCircle className="h-3.5 w-3.5" />
                              {course.languageCode === 'en' ? 'English' : 'Español'}
                              <ExternalLink className="h-3 w-3" />
                            </a>
                          ))}
                          <a
                            href={buildGenerateCourseUrl(skill, user)}
                            target="_blank"
                            rel="noreferrer"
                            className="inline-flex items-center gap-1.5 rounded-full border border-indigo-200 px-2.5 py-1 text-xs font-medium text-indigo-700 transition hover:bg-indigo-50 dark:border-indigo-500/30 dark:text-indigo-300 dark:hover:bg-indigo-500/10"
                          >
                            <RefreshCw className="h-3 w-3" />
                            {t('skills.recreateCourse')}
                          </a>
                        </div>
                      ) : skill.matchedCapabilityId ? (
                        <>
                          <a
                            href={buildCapabilityStudioDetailUrl(skill.matchedCapabilityId, skill.matchedCourseType, user)}
                            target="_blank"
                            rel="noreferrer"
                            className="inline-flex items-center gap-1.5 rounded-full bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white shadow-sm transition hover:bg-emerald-500"
                          >
                            <PlayCircle className="h-3.5 w-3.5" />
                            {t('skills.courseCreated')}
                            <ExternalLink className="h-3 w-3" />
                          </a>
                          <a
                            href={buildGenerateCourseUrl(skill, user)}
                            target="_blank"
                            rel="noreferrer"
                            className="ml-2 inline-flex items-center gap-1.5 rounded-full border border-indigo-200 px-2.5 py-1 text-xs font-medium text-indigo-700 transition hover:bg-indigo-50 dark:border-indigo-500/30 dark:text-indigo-300 dark:hover:bg-indigo-500/10"
                          >
                            <RefreshCw className="h-3 w-3" />
                            {t('skills.recreateCourse')}
                          </a>
                        </>
                      ) : (
                        <a
                          href={buildGenerateCourseUrl(skill, user)}
                          target="_blank"
                          rel="noreferrer"
                          className="inline-flex items-center gap-1.5 rounded-full bg-indigo-600 px-2.5 py-1 text-xs font-medium text-white shadow-sm transition hover:bg-indigo-500"
                        >
                          <BookOpen className="h-3 w-3" />
                          {t('skills.generateCourse')}
                          <ExternalLink className="h-3 w-3" />
                        </a>
                      )}
                    </div>
                  </div>

                  <div className="flex shrink-0 items-center gap-1 opacity-0 transition group-hover:opacity-100">
                    <button
                      type="button"
                      title={t('common.edit')}
                      onClick={() => navigate(`/skills/${skill.skillId}/edit`)}
                      className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-slate-200 dark:hover:bg-white/10"
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      title={t('common.delete')}
                      onClick={() => handleDelete(skill)}
                      className="flex h-7 w-7 items-center justify-center rounded-full text-slate-400 hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-500/10"
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}
