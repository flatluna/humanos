import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import { ArrowLeft, ListChecks } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  createResponsibility,
  getResponsibilities,
  updateResponsibility,
  type SaveResponsibilityRequest,
} from '@/api/responsibilitiesApi';

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

export function ResponsibilityDetailPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const params = useParams<{ responsibilityId?: string }>();
  const queryClient = useQueryClient();

  const responsibilityId = params.responsibilityId;
  const isEditing = Boolean(responsibilityId);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const responsibilitiesQuery = useQuery({
    queryKey: ['responsibilities', tenantId],
    queryFn: () => getResponsibilities(tenantId),
  });

  useEffect(() => {
    if (responsibilityId && responsibilitiesQuery.data) {
      const resp = responsibilitiesQuery.data.find((r) => r.responsibilityId === responsibilityId);
      if (resp) {
        setName(resp.name);
        setDescription(resp.description ?? '');
      }
    }
  }, [responsibilityId, responsibilitiesQuery.data]);

  const saveMutation = useMutation({
    mutationFn: (body: SaveResponsibilityRequest) =>
      responsibilityId ? updateResponsibility(responsibilityId, body) : createResponsibility(tenantId, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['responsibilities', tenantId] });
      void queryClient.invalidateQueries({ queryKey: ['job-roles', tenantId] });
      navigate('/responsibilities');
    },
    onError: (error: unknown) => {
      setErrorMessage(error instanceof Error ? error.message : t('common.unexpectedError'));
    },
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    if (!name.trim()) {
      setErrorMessage(t('responsibilities.nameRequired'));
      return;
    }

    saveMutation.mutate({
      name: name.trim(),
      description: description.trim() || null,
    });
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => navigate('/responsibilities')}
          className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
            {isEditing ? t('responsibilities.editTitle') : t('responsibilities.newTitle')}
          </h1>
          <p className="mt-0.5 text-xs text-slate-400 dark:text-white/40">
            {t('responsibilities.formSubtitle')}
          </p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-center gap-2 border-b border-slate-100 pb-3 dark:border-white/5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-blue-100 text-blue-600 dark:bg-blue-500/15 dark:text-blue-300">
              <ListChecks className="h-4 w-4" />
            </span>
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
              {t('responsibilities.infoTitle')}
            </h2>
          </div>

          <div>
            <label className={labelClass}>{t('responsibilities.nameLabel')} *</label>
            <input
              type="text"
              required
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('responsibilities.namePlaceholder')}
              className={inputClass}
            />
          </div>

          <div>
            <label className={labelClass}>{t('responsibilities.descLabel')}</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={4}
              placeholder={t('responsibilities.descPlaceholder')}
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
            onClick={() => navigate('/responsibilities')}
            className="rounded-full px-4 py-2 text-xs font-medium text-slate-500 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
          >
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            disabled={saveMutation.isPending}
            className="rounded-full bg-blue-600 px-5 py-2 text-xs font-semibold text-white shadow-md shadow-blue-500/20 hover:bg-blue-700 disabled:opacity-50"
          >
            {saveMutation.isPending ? t('common.saving') : t('common.save')}
          </button>
        </div>
      </form>
    </div>
  );
}
