import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import {
  Building2,
  Plus,
  Search,
  Pencil,
  Trash2,
  Users,
  Briefcase,
  User,
  MapPin,
  DollarSign,
  Tag,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  deleteDepartment,
  getDepartments,
  type DepartmentDto,
} from '@/api/orgStructureApi';

export function DepartmentsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [search, setSearch] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('all');

  const departmentsQuery = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId),
  });

  const departments = departmentsQuery.data ?? [];

  const categories = useMemo(() => {
    const set = new Set<string>();
    for (const d of departments) {
      if (d.category?.trim()) {
        set.add(d.category.trim());
      }
    }
    return Array.from(set).sort();
  }, [departments]);

  const filtered = useMemo(() => {
    return departments.filter((d) => {
      const matchesSearch =
        !search.trim() ||
        d.name.toLowerCase().includes(search.toLowerCase()) ||
        d.code?.toLowerCase().includes(search.toLowerCase()) ||
        d.description?.toLowerCase().includes(search.toLowerCase()) ||
        d.leaderDisplayName?.toLowerCase().includes(search.toLowerCase());

      const matchesCategory =
        selectedCategory === 'all' || d.category === selectedCategory;

      return matchesSearch && matchesCategory;
    });
  }, [departments, search, selectedCategory]);

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteDepartment(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['departments', tenantId] });
    },
  });

  function handleDelete(d: DepartmentDto) {
    if (window.confirm(t('departmentsPage.deleteConfirm', { name: d.name }))) {
      deleteMutation.mutate(d.departmentId);
    }
  }

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
            {t('departmentsPage.title')}
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {t('departmentsPage.subtitle', { tenant: user?.tenantName ?? '' })}
          </p>
        </div>
        <button
          type="button"
          onClick={() => navigate('/departamentos/nuevo')}
          className="inline-flex items-center gap-2 rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-5 py-2.5 text-sm font-medium text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110"
        >
          <Plus className="h-4 w-4" />
          {t('departmentsPage.newDepartment')}
        </button>
      </div>

      {deleteMutation.error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {deleteMutation.error instanceof Error ? deleteMutation.error.message : t('common.error')}
        </div>
      )}

      {/* Search & Filter Bar */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t('departmentsPage.searchPlaceholder')}
            className="w-full rounded-full border border-slate-200 bg-white pl-10 pr-4 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
        </div>

        {categories.length > 0 && (
          <select
            value={selectedCategory}
            onChange={(e) => setSelectedCategory(e.target.value)}
            className="rounded-full border border-slate-200 bg-white px-4 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          >
            <option value="all">{t('departmentsPage.allCategories')}</option>
            {categories.map((c) => (
              <option key={c} value={c}>
                {c}
              </option>
            ))}
          </select>
        )}
      </div>

      {/* Loading state */}
      {departmentsQuery.isLoading && (
        <p className="text-sm text-slate-400">{t('departmentsPage.loading')}</p>
      )}

      {/* Empty state */}
      {!departmentsQuery.isLoading && filtered.length === 0 && (
        <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-slate-50/50 p-12 text-center dark:border-white/10 dark:bg-white/5">
          <Building2 className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">
            {search || selectedCategory !== 'all' ? t('departmentsPage.emptySearchTitle') : t('departmentsPage.emptyTitle')}
          </h3>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {search || selectedCategory !== 'all'
              ? t('departmentsPage.emptySearchDesc')
              : t('departmentsPage.emptyDesc')}
          </p>
          {!search && selectedCategory === 'all' && (
            <button
              type="button"
              onClick={() => navigate('/departamentos/nuevo')}
              className="mt-4 inline-flex items-center gap-2 rounded-full bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
            >
              <Plus className="h-4 w-4" />
              {t('departmentsPage.createDepartment')}
            </button>
          )}
        </div>
      )}

      {/* Departments Grid */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {filtered.map((dept) => (
          <div
            key={dept.departmentId}
            className="group relative flex flex-col justify-between rounded-2xl border border-slate-200 bg-white p-5 transition hover:border-slate-300 hover:shadow-md dark:border-white/10 dark:bg-white/5 dark:hover:border-white/20"
          >
            <div>
              {/* Card Top Row */}
              <div className="flex items-start justify-between gap-2">
                <div className="flex items-center gap-2">
                  <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-indigo-50 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-300">
                    <Building2 className="h-5 w-5" />
                  </span>
                  <div>
                    <h3 className="font-semibold text-slate-900 dark:text-white">
                      {dept.name}
                    </h3>
                    {dept.code && (
                      <span className="inline-block rounded-md bg-slate-100 px-1.5 py-0.5 text-[11px] font-mono font-medium text-slate-600 dark:bg-white/10 dark:text-white/60">
                        {dept.code}
                      </span>
                    )}
                  </div>
                </div>

                {/* Actions */}
                <div className="flex items-center gap-1 opacity-0 transition group-hover:opacity-100">
                  <button
                    type="button"
                    title={t('departmentsPage.editProfileTitle')}
                    onClick={() => navigate(`/departamentos/${dept.departmentId}`)}
                    className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    title={t('departmentsPage.deleteTitle')}
                    onClick={() => handleDelete(dept)}
                    className="flex h-8 w-8 items-center justify-center rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10 dark:hover:text-red-400"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>

              {/* Badges / Category & Member Count */}
              <div className="mt-3 flex flex-wrap items-center gap-2 text-xs">
                {dept.category && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-indigo-50 px-2.5 py-0.5 font-medium text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300">
                    <Tag className="h-3 w-3" />
                    {dept.category}
                  </span>
                )}
                <span className="inline-flex items-center gap-1 rounded-full bg-sky-50 px-2.5 py-0.5 font-semibold text-sky-700 dark:bg-sky-500/15 dark:text-sky-300">
                  <Users className="h-3 w-3" />
                  {t('departmentsPage.membersCount', { count: dept.personCount })}
                </span>
              </div>

              {/* Description or Mission */}
              {(dept.description || dept.missionStatement) && (
                <p className="mt-3 line-clamp-2 text-xs text-slate-500 dark:text-white/60">
                  {dept.missionStatement || dept.description}
                </p>
              )}

              {/* Details List */}
              <div className="mt-4 space-y-1.5 text-xs text-slate-600 dark:text-white/70">
                {dept.leaderDisplayName && (
                  <div className="flex items-center gap-1.5">
                    <User className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                    <span className="font-medium">{t('departmentsPage.leaderLabel', { leader: dept.leaderDisplayName })}</span>
                  </div>
                )}
                {dept.location && (
                  <div className="flex items-center gap-1.5">
                    <MapPin className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                    <span>{dept.location}</span>
                  </div>
                )}
                {dept.costCenter && (
                  <div className="flex items-center gap-1.5">
                    <DollarSign className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                    <span>{t('departmentsPage.costCenterLabel', { costCenter: dept.costCenter })}</span>
                  </div>
                )}
              </div>
            </div>

            {/* Bottom stats row */}
            <div className="mt-5 flex items-center justify-between border-t border-slate-100 pt-3 text-xs text-slate-400 dark:border-white/5 dark:text-white/40">
              <span className="flex items-center gap-1">
                <Briefcase className="h-3.5 w-3.5" />
                {t('departmentsPage.rolesCount', { count: dept.jobRoleCount })}
              </span>
              <span className="flex items-center gap-1">
                <Users className="h-3.5 w-3.5" />
                {t('departmentsPage.collaboratorsCount', { count: dept.personCount })}
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
