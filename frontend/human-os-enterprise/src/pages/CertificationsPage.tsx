import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { Award, Plus, Pencil, Trash2, Search, Building2 } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  deleteCertification,
  getCertifications,
  type CertificationDto,
} from '@/api/certificationsApi';

export function CertificationsPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [searchTerm, setSearchTerm] = useState('');

  const certsQuery = useQuery({
    queryKey: ['certifications', tenantId],
    queryFn: () => getCertifications(tenantId),
  });

  const deleteMutation = useMutation({
    mutationFn: (certId: string) => deleteCertification(certId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['certifications', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
    },
  });

  const allCerts = certsQuery.data ?? [];
  const filtered = allCerts.filter(
    (c) =>
      c.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (c.issuer && c.issuer.toLowerCase().includes(searchTerm.toLowerCase())) ||
      (c.description && c.description.toLowerCase().includes(searchTerm.toLowerCase())),
  );

  const handleDelete = (cert: CertificationDto) => {
    if (window.confirm(t('certifications.confirmDelete', { name: cert.name }))) {
      deleteMutation.mutate(cert.certificationId);
    }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
            {t('certifications.title')}
          </h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
            {t('certifications.subtitle')}
          </p>
        </div>
        <button
          type="button"
          onClick={() => navigate('/certifications/nuevo')}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-full bg-gradient-to-r from-amber-500 to-orange-500 px-4 py-2 text-xs font-semibold text-white shadow-md shadow-amber-500/20 transition hover:brightness-105"
        >
          <Plus className="h-4 w-4" />
          {t('certifications.newCertification')}
        </button>
      </div>

      <div className="relative">
        <Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <input
          type="text"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          placeholder={t('certifications.searchPlaceholder')}
          className="w-full rounded-xl border border-slate-200 bg-white py-2 pl-10 pr-4 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
        />
      </div>

      <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 dark:border-white/10 dark:bg-white/5">
        <div className="flex items-center gap-3 border-b border-slate-100 pb-4 dark:border-white/5">
          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300">
            <Award className="h-5 w-5" />
          </span>
          <div>
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
              {t('certifications.catalogTitle')}
            </h2>
            <p className="text-xs text-slate-400 dark:text-white/40">
              {t('certifications.catalogCount', { count: filtered.length })}
            </p>
          </div>
        </div>

        {certsQuery.isLoading && (
          <p className="py-6 text-center text-xs text-slate-400 dark:text-white/40">
            {t('common.loading')}
          </p>
        )}

        {certsQuery.isSuccess && filtered.length === 0 && (
          <p className="py-6 text-center text-xs text-slate-400 dark:text-white/40">
            {searchTerm ? t('certifications.noResults') : t('certifications.emptyCatalog')}
          </p>
        )}

        {filtered.length > 0 && (
          <div className="divide-y divide-slate-100 dark:divide-white/5">
            {filtered.map((cert) => (
              <div
                key={cert.certificationId}
                className="group flex items-start justify-between gap-3 py-3.5 transition hover:bg-slate-50/50 -mx-2 px-2 rounded-lg dark:hover:bg-white/[0.02]"
              >
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <p className="text-sm font-medium text-slate-900 dark:text-white">{cert.name}</p>
                    {cert.issuer && (
                      <span className="inline-flex items-center gap-1 rounded-md bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 dark:bg-amber-500/10 dark:text-amber-300">
                        <Building2 className="h-3 w-3" />
                        <span>{cert.issuer}</span>
                      </span>
                    )}
                  </div>
                  {cert.description && (
                    <p className="mt-1 text-xs text-slate-500 dark:text-white/50">{cert.description}</p>
                  )}
                </div>

                <div className="flex shrink-0 items-center gap-1">
                  <button
                    type="button"
                    onClick={() => navigate(`/certifications/${cert.certificationId}/edit`)}
                    className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                    title={t('common.edit')}
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button
                    type="button"
                    onClick={() => handleDelete(cert)}
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
