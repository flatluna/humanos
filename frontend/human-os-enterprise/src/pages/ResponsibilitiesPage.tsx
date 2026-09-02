import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { ListChecks, Plus, Pencil, Trash2, Search, Briefcase } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  deleteResponsibility,
  getResponsibilities,
  type ResponsibilityDto,
} from '@/api/responsibilitiesApi';

export function ResponsibilitiesPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [searchTerm, setSearchTerm] = useState('');

  const responsibilitiesQuery = useQuery({
    queryKey: ['responsibilities', tenantId],
    queryFn: () => getResponsibilities(tenantId),
  });

  const deleteMutation = useMutation({
    mutationFn: (respId: string) => deleteResponsibility(respId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['responsibilities', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
    },
  });

  const allResponsibilities = responsibilitiesQuery.data ?? [];
  const filtered = allResponsibilities.filter(
    (r) =>
      r.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (r.description && r.description.toLowerCase().includes(searchTerm.toLowerCase())),
  );

  const handleDelete = (resp: ResponsibilityDto) => {
    if (window.confirm(t('responsibilities.confirmDelete', { name: resp.name }))) {
      deleteMutation.mutate(resp.responsibilityId);
    }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
            {t('responsibilities.title')}
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {t('responsibilities.subtitle')}
          </p>
        </div>
        <button
          type="button"
          onClick={() => navigate('/responsibilities/nuevo')}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-full bg-gradient-to-r from-blue-600 to-indigo-600 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-blue-500/20 transition hover:brightness-105"
        >
          <Plus className="h-4 w-4" />
          {t('responsibilities.newResponsibility')}
        </button>
      </div>

      <div className="relative">
        <Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <input
          type="text"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          placeholder={t('responsibilities.searchPlaceholder')}
          className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-10 pr-4 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
        />
      </div>

      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
        <div className="flex items-center gap-3 border-b border-slate-100 pb-4 dark:border-white/5">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-blue-100 text-blue-600 dark:bg-blue-500/15 dark:text-blue-300">
            <ListChecks className="h-5 w-5" />
          </span>
          <div>
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
              {t('responsibilities.catalogTitle')}
            </h2>
            <p className="text-xs text-slate-400 dark:text-white/40">
              {t('responsibilities.catalogCount', { count: filtered.length })}
            </p>
          </div>
        </div>

        {responsibilitiesQuery.isLoading && (
          <p className="py-6 text-center text-xs text-slate-400 dark:text-white/40">
            {t('common.loading')}
          </p>
        )}

        {responsibilitiesQuery.isSuccess && filtered.length === 0 && (
          <p className="py-6 text-center text-xs text-slate-400 dark:text-white/40">
            {searchTerm ? t('responsibilities.noResults') : t('responsibilities.emptyCatalog')}
          </p>
        )}

        {filtered.length > 0 && (
          <div className="divide-y divide-slate-100 dark:divide-white/5">
            {filtered.map((resp) => (
              <div
                key={resp.responsibilityId}
                className="group flex items-start justify-between gap-3 py-3.5 transition hover:bg-slate-50/50 -mx-2 px-2 rounded-lg dark:hover:bg-white/[0.02]"
              >
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium text-slate-900 dark:text-white">{resp.name}</p>
                  {resp.description && (
                    <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">{resp.description}</p>
                  )}
                  <div className="mt-2 flex flex-wrap gap-1">
                    {resp.jobRoles.length === 0 ? (
                      <span className="text-[11px] text-slate-400 dark:text-white/30">
                        {t('responsibilities.noLinkedRoles')}
                      </span>
                    ) : (
                      resp.jobRoles.map((role) => (
                        <span
                          key={role.jobRoleId}
                          className="inline-flex items-center gap-1 rounded-md bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-white/10 dark:text-slate-300"
                        >
                          <Briefcase className="h-3 w-3 text-slate-400" />
                          <span>{role.jobRoleTitle}</span>
                        </span>
                      ))
                    )}
                  </div>
                </div>

                <div className="flex shrink-0 items-center gap-1">
                  <button
                    type="button"
                    onClick={() => navigate(`/responsibilities/${resp.responsibilityId}/edit`)}
                    className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                    title={t('common.edit')}
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    onClick={() => handleDelete(resp)}
                    disabled={deleteMutation.isPending}
                    className="rounded-lg p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-500/10 dark:hover:text-rose-400"
                    title={t('common.delete')}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
