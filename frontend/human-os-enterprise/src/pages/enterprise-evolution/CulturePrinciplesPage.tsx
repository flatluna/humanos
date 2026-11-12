import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { ArrowLeft, Heart, Plus, Pencil, Trash2, Search, Globe2, Building2 } from 'lucide-react';
import { clsx } from 'clsx';
import { useAuth } from '@/auth/AuthContext';
import {
  deleteCulturePrinciple,
  getCulturePrinciples,
  type CulturePrincipleDto,
} from '@/api/culturePrinciplesApi';

const STATUS_STYLES: Record<string, string> = {
  Draft: 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/60',
  Approved: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300',
  Superseded: 'bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300',
  Retired: 'bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300',
};

type ScopeFilter = 'all' | 'global' | 'tenant';

export function CulturePrinciplesPage() {
  const { t, i18n } = useTranslation();
  const isSpanish = i18n.language.startsWith('es');
  const localizedName = (p: CulturePrincipleDto) => (isSpanish ? p.nameEs || p.nameEn : p.nameEn);
  const localizedDescription = (p: CulturePrincipleDto) =>
    isSpanish ? p.descriptionEs || p.descriptionEn : p.descriptionEn;
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [searchTerm, setSearchTerm] = useState('');
  const [scope, setScope] = useState<ScopeFilter>('all');

  const principlesQuery = useQuery({
    queryKey: ['culture-principles', tenantId],
    queryFn: () => getCulturePrinciples(tenantId),
  });

  const deleteMutation = useMutation({
    mutationFn: (principleId: string) => deleteCulturePrinciple(principleId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['culture-principles', tenantId] });
    },
  });

  const all = principlesQuery.data ?? [];

  const filtered = useMemo(() => {
    const term = searchTerm.toLowerCase();
    return all.filter((p) => {
      const matchesScope =
        scope === 'all' || (scope === 'global' && p.tenantId === null) || (scope === 'tenant' && p.tenantId !== null);
      const matchesTerm =
        !term ||
        p.principleCode.toLowerCase().includes(term) ||
        p.nameEn.toLowerCase().includes(term) ||
        (p.nameEs && p.nameEs.toLowerCase().includes(term)) ||
        p.descriptionEn.toLowerCase().includes(term);
      return matchesScope && matchesTerm;
    });
  }, [all, scope, searchTerm]);

  const handleDelete = (principle: CulturePrincipleDto) => {
    const name = localizedName(principle);
    if (!window.confirm(t('enterpriseEvolution.culturePrinciples.confirmDelete', { name }))) return;
    deleteMutation.mutate(principle.culturePrincipleId, {
      onError: () => {
        window.alert(t('enterpriseEvolution.culturePrinciples.deleteBlocked'));
      },
    });
  };

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <a
          href="/enterprise-evolution/foundations"
          className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-slate-600 dark:text-white/40 dark:hover:text-white/70"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          {t('enterpriseEvolution.culturePrinciples.backToFoundations')}
        </a>
      </div>

      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
            {t('enterpriseEvolution.culturePrinciples.title')}
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {t('enterpriseEvolution.culturePrinciples.subtitle')}
          </p>
        </div>
        <button
          type="button"
          onClick={() => navigate('/enterprise-evolution/foundations/culture-principles/nuevo')}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-full bg-gradient-to-r from-rose-500 to-pink-500 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-rose-500/20 transition hover:brightness-105"
        >
          <Plus className="h-4 w-4" />
          {t('enterpriseEvolution.culturePrinciples.newPrinciple')}
        </button>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder={t('enterpriseEvolution.culturePrinciples.searchPlaceholder')}
            className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-10 pr-4 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
        </div>
        <div className="inline-flex shrink-0 rounded-xl border border-slate-200 bg-white p-1 text-xs dark:border-white/10 dark:bg-white/5">
          {(['all', 'global', 'tenant'] as ScopeFilter[]).map((key) => (
            <button
              key={key}
              type="button"
              onClick={() => setScope(key)}
              className={clsx(
                'rounded-lg px-3 py-1.5 font-medium transition',
                scope === key
                  ? 'bg-rose-500 text-white shadow-sm'
                  : 'text-slate-500 hover:bg-slate-100 dark:text-white/50 dark:hover:bg-white/10',
              )}
            >
              {t(`enterpriseEvolution.culturePrinciples.filter${key.charAt(0).toUpperCase()}${key.slice(1)}` as const)}
            </button>
          ))}
        </div>
      </div>

      {principlesQuery.isLoading && (
        <p className="py-10 text-center text-xs text-slate-400 dark:text-white/40">{t('common.loading')}</p>
      )}

      {principlesQuery.isSuccess && filtered.length === 0 && (
        <div className="rounded-3xl border border-dashed border-slate-200 bg-slate-50/60 py-12 text-center dark:border-white/10 dark:bg-white/[0.02]">
          <span className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300">
            <Heart className="h-6 w-6" />
          </span>
          <p className="mt-4 text-sm text-slate-500 dark:text-white/50">
            {searchTerm ? t('enterpriseEvolution.culturePrinciples.noResults') : t('enterpriseEvolution.culturePrinciples.emptyCatalog')}
          </p>
        </div>
      )}

      {filtered.length > 0 && (
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {filtered.map((p) => (
            <div
              key={p.culturePrincipleId}
              onClick={() => navigate(`/enterprise-evolution/foundations/culture-principles/${p.culturePrincipleId}/edit`)}
              className="group relative cursor-pointer overflow-hidden rounded-3xl border border-slate-200 bg-white p-6 transition hover:-translate-y-1 hover:shadow-xl hover:shadow-slate-200/60 dark:border-white/10 dark:bg-white/5 dark:hover:shadow-none"
            >
              <span className="absolute inset-x-0 top-0 h-1.5 bg-gradient-to-r from-rose-500 to-pink-400" />

              <div className="flex items-start justify-between">
                <span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300">
                  <Heart className="h-5 w-5" />
                </span>
                <div className="flex shrink-0 items-center gap-1 opacity-0 transition group-hover:opacity-100">
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      navigate(`/enterprise-evolution/foundations/culture-principles/${p.culturePrincipleId}/edit`);
                    }}
                    className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                    title={t('common.edit')}
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      handleDelete(p);
                    }}
                    disabled={deleteMutation.isPending}
                    className="rounded-lg p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-500/10 dark:hover:text-rose-400"
                    title={t('common.delete')}
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>

              <h3 className="mt-4 text-lg font-semibold text-slate-900 dark:text-white">{localizedName(p)}</h3>
              <p className="mt-2 line-clamp-3 text-sm leading-relaxed text-slate-500 dark:text-white/50">
                {localizedDescription(p)}
              </p>

              <div className="mt-4 flex flex-wrap items-center gap-2">
                <span className="rounded-md bg-slate-100 px-1.5 py-0.5 font-mono text-[10px] text-slate-500 dark:bg-white/10 dark:text-white/40">
                  {p.principleCode}
                </span>
                <span className={clsx('rounded-md px-2 py-0.5 text-[11px] font-medium', STATUS_STYLES[p.status])}>
                  {t(`enterpriseEvolution.culturePrinciples.status.${p.status}` as const)}
                </span>
                <span className="inline-flex items-center gap-1 rounded-md bg-slate-50 px-2 py-0.5 text-[11px] text-slate-400 dark:bg-white/5 dark:text-white/30">
                  {p.tenantId === null ? <Globe2 className="h-3 w-3" /> : <Building2 className="h-3 w-3" />}
                  {p.tenantId === null
                    ? t('enterpriseEvolution.culturePrinciples.globalBadge')
                    : t('enterpriseEvolution.culturePrinciples.tenantBadge')}
                </span>
                <span className="text-[11px] text-slate-400 dark:text-white/30">
                  {t('enterpriseEvolution.culturePrinciples.versionLabel', { version: p.versionNumber })}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
