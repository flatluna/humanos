import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import { ArrowLeft, Award } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  createCertification,
  getCertifications,
  updateCertification,
  type SaveCertificationRequest,
} from '@/api/certificationsApi';

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

export function CertificationDetailPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const params = useParams<{ certificationId?: string }>();
  const queryClient = useQueryClient();

  const certificationId = params.certificationId;
  const isEditing = Boolean(certificationId);

  const [name, setName] = useState('');
  const [issuer, setIssuer] = useState('');
  const [description, setDescription] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const certsQuery = useQuery({
    queryKey: ['certifications', tenantId],
    queryFn: () => getCertifications(tenantId),
  });

  useEffect(() => {
    if (certificationId && certsQuery.data) {
      const cert = certsQuery.data.find((c) => c.certificationId === certificationId);
      if (cert) {
        setName(cert.name);
        setIssuer(cert.issuer ?? '');
        setDescription(cert.description ?? '');
      }
    }
  }, [certificationId, certsQuery.data]);

  const saveMutation = useMutation({
    mutationFn: (body: SaveCertificationRequest) =>
      certificationId ? updateCertification(certificationId, body) : createCertification(tenantId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['certifications', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      navigate('/certifications');
    },
    onError: (error: unknown) => {
      setErrorMessage(error instanceof Error ? error.message : t('common.unexpectedError'));
    },
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    if (!name.trim()) {
      setErrorMessage(t('certifications.nameRequired'));
      return;
    }

    saveMutation.mutate({
      name: name.trim(),
      issuer: issuer.trim() || null,
      description: description.trim() || null,
    });
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => navigate('/certifications')}
          className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
            {isEditing ? t('certifications.editTitle') : t('certifications.newTitle')}
          </h1>
          <p className="mt-0.5 text-xs text-slate-400 dark:text-white/40">
            {t('certifications.formSubtitle')}
          </p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-center gap-2 border-b border-slate-100 pb-3 dark:border-white/5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-amber-100 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300">
              <Award className="h-4 w-4" />
            </span>
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
              {t('certifications.infoTitle')}
            </h2>
          </div>

          <div>
            <label className={labelClass}>{t('certifications.nameLabel')} *</label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('certifications.namePlaceholder')}
              className={inputClass}
            />
          </div>

          <div>
            <label className={labelClass}>{t('certifications.issuerLabel')}</label>
            <input
              type="text"
              value={issuer}
              onChange={(e) => setIssuer(e.target.value)}
              placeholder={t('certifications.issuerPlaceholder')}
              className={inputClass}
            />
          </div>

          <div>
            <label className={labelClass}>{t('certifications.descLabel')}</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={4}
              placeholder={t('certifications.descPlaceholder')}
              className={inputClass}
            />
          </div>
        </section>

        {errorMessage && (
          <div className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700 dark:border-rose-500/20 dark:bg-rose-500/10 dark:text-rose-300">
            {errorMessage}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={() => navigate('/certifications')}
            className="rounded-full px-4 py-2 text-xs font-medium text-slate-500 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
          >
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            disabled={saveMutation.isPending}
            className="rounded-full bg-gradient-to-r from-amber-500 to-orange-500 px-5 py-2 text-xs font-semibold text-white shadow-md shadow-amber-500/20 hover:brightness-105 disabled:opacity-50"
          >
            {saveMutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>
    </div>
  );
}
