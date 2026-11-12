import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router';
import { ArrowLeft, Heart, ListChecks, Plus, Pencil, Trash2 } from 'lucide-react';
import { clsx } from 'clsx';
import { useAuth } from '@/auth/AuthContext';
import {
  createCulturePrinciple,
  createCulturePrincipleBehavior,
  deleteCulturePrincipleBehavior,
  getCulturePrincipleBehaviors,
  getCulturePrincipleById,
  updateCulturePrinciple,
  updateCulturePrincipleBehavior,
  type CulturePrincipleBehaviorDto,
  type CulturePrincipleBehaviorType,
  type CulturePrincipleStatus,
  type SaveCulturePrincipleBehaviorRequest,
  type SaveCulturePrincipleRequest,
} from '@/api/culturePrinciplesApi';

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

const STATUS_OPTIONS: CulturePrincipleStatus[] = ['Draft', 'Approved', 'Superseded', 'Retired'];
const BEHAVIOR_TYPE_OPTIONS: CulturePrincipleBehaviorType[] = [
  'ExpectedBehavior',
  'BehaviorToAvoid',
  'LeadershipExpectation',
  'EmployeeExpectation',
  'AITransformationImplication',
];

const emptyBehaviorForm = {
  behaviorId: null as string | null,
  behaviorCode: '',
  behaviorType: 'ExpectedBehavior' as CulturePrincipleBehaviorType,
  descriptionEn: '',
  descriptionEs: '',
  sortOrder: 0,
};

export function CulturePrincipleDetailPage() {
  const { t, i18n } = useTranslation();
  const isSpanish = i18n.language.startsWith('es');
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const navigate = useNavigate();
  const params = useParams<{ principleId?: string }>();
  const queryClient = useQueryClient();

  const principleId = params.principleId;
  const isEditing = Boolean(principleId);

  const [principleCode, setPrincipleCode] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [nameEs, setNameEs] = useState('');
  const [descriptionEn, setDescriptionEn] = useState('');
  const [descriptionEs, setDescriptionEs] = useState('');
  const [status, setStatus] = useState<CulturePrincipleStatus>('Draft');
  const [versionNumber, setVersionNumber] = useState(1);
  const [effectiveFrom, setEffectiveFrom] = useState('');
  const [effectiveTo, setEffectiveTo] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [behaviorForm, setBehaviorForm] = useState(emptyBehaviorForm);
  const [showBehaviorForm, setShowBehaviorForm] = useState(false);
  const [behaviorError, setBehaviorError] = useState<string | null>(null);

  const principleQuery = useQuery({
    queryKey: ['culture-principle', principleId],
    queryFn: () => getCulturePrincipleById(principleId!),
    enabled: isEditing,
  });

  const behaviorsQuery = useQuery({
    queryKey: ['culture-principle-behaviors', principleId],
    queryFn: () => getCulturePrincipleBehaviors(principleId!),
    enabled: isEditing,
  });

  useEffect(() => {
    const p = principleQuery.data;
    if (p) {
      setPrincipleCode(p.principleCode);
      setNameEn(p.nameEn);
      setNameEs(p.nameEs ?? '');
      setDescriptionEn(p.descriptionEn);
      setDescriptionEs(p.descriptionEs ?? '');
      setStatus(p.status);
      setVersionNumber(p.versionNumber);
      setEffectiveFrom(p.effectiveFrom ?? '');
      setEffectiveTo(p.effectiveTo ?? '');
    }
  }, [principleQuery.data]);

  const saveMutation = useMutation({
    mutationFn: (body: SaveCulturePrincipleRequest) =>
      principleId ? updateCulturePrinciple(principleId, body) : createCulturePrinciple(body),
    onSuccess: (saved) => {
      void queryClient.invalidateQueries({ queryKey: ['culture-principles', tenantId] });
      if (!principleId) {
        // After the first save, move into edit mode on the new record so behaviors can be added.
        navigate(`/enterprise-evolution/foundations/culture-principles/${saved.culturePrincipleId}/edit`, {
          replace: true,
        });
      } else {
        void queryClient.invalidateQueries({ queryKey: ['culture-principle', principleId] });
      }
    },
    onError: (error: unknown) => {
      setErrorMessage(error instanceof Error ? error.message : t('common.unexpectedError'));
    },
  });

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!principleCode.trim()) {
      setErrorMessage(t('enterpriseEvolution.culturePrinciples.codeRequired'));
      return;
    }
    if (!nameEn.trim()) {
      setErrorMessage(t('enterpriseEvolution.culturePrinciples.nameEnRequired'));
      return;
    }
    if (!descriptionEn.trim()) {
      setErrorMessage(t('enterpriseEvolution.culturePrinciples.descEnRequired'));
      return;
    }
    if (status === 'Approved' && !effectiveFrom) {
      setErrorMessage(t('enterpriseEvolution.culturePrinciples.approvedRequiresEffectiveFrom'));
      return;
    }

    const nowIso = new Date().toISOString();
    saveMutation.mutate({
      tenantId,
      principleCode: principleCode.trim(),
      nameEn: nameEn.trim(),
      nameEs: nameEs.trim() || null,
      descriptionEn: descriptionEn.trim(),
      descriptionEs: descriptionEs.trim() || null,
      versionNumber,
      status,
      effectiveFrom: effectiveFrom || null,
      effectiveTo: effectiveTo || null,
      ownerPersonId: status === 'Approved' ? (user?.personId ?? null) : null,
      approvedByPersonId: status === 'Approved' ? (user?.personId ?? null) : null,
      approvedAt: status === 'Approved' ? nowIso : null,
      createdByPersonId: user?.personId ?? null,
    });
  };

  // --- Behaviors (dbo.CulturePrincipleBehavior) ---

  const behaviorMutation = useMutation({
    mutationFn: (body: SaveCulturePrincipleBehaviorRequest) =>
      behaviorForm.behaviorId
        ? updateCulturePrincipleBehavior(principleId!, behaviorForm.behaviorId, body)
        : createCulturePrincipleBehavior(principleId!, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['culture-principle-behaviors', principleId] });
      setShowBehaviorForm(false);
      setBehaviorForm(emptyBehaviorForm);
      setBehaviorError(null);
    },
    onError: (error: unknown) => {
      setBehaviorError(error instanceof Error ? error.message : t('common.unexpectedError'));
    },
  });

  const deleteBehaviorMutation = useMutation({
    mutationFn: (behaviorId: string) => deleteCulturePrincipleBehavior(principleId!, behaviorId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['culture-principle-behaviors', principleId] });
    },
  });

  const openNewBehaviorForm = () => {
    setBehaviorForm({ ...emptyBehaviorForm, sortOrder: (behaviorsQuery.data?.length ?? 0) + 1 });
    setBehaviorError(null);
    setShowBehaviorForm(true);
  };

  const openEditBehaviorForm = (b: CulturePrincipleBehaviorDto) => {
    setBehaviorForm({
      behaviorId: b.culturePrincipleBehaviorId,
      behaviorCode: b.behaviorCode ?? '',
      behaviorType: b.behaviorType,
      descriptionEn: b.descriptionEn,
      descriptionEs: b.descriptionEs ?? '',
      sortOrder: b.sortOrder,
    });
    setBehaviorError(null);
    setShowBehaviorForm(true);
  };

  const handleBehaviorSubmit = (e: FormEvent) => {
    e.preventDefault();
    setBehaviorError(null);
    if (!behaviorForm.descriptionEn.trim()) {
      setBehaviorError(t('enterpriseEvolution.culturePrinciples.behaviorDescRequired'));
      return;
    }
    if (!behaviorForm.behaviorCode.trim()) {
      setBehaviorError(t('enterpriseEvolution.culturePrinciples.behaviorCodeRequired'));
      return;
    }
    behaviorMutation.mutate({
      behaviorCode: behaviorForm.behaviorCode.trim(),
      behaviorType: behaviorForm.behaviorType,
      descriptionEn: behaviorForm.descriptionEn.trim(),
      descriptionEs: behaviorForm.descriptionEs.trim() || null,
      sortOrder: behaviorForm.sortOrder,
      isActive: true,
      createdByPersonId: user?.personId ?? null,
    });
  };

  const handleDeleteBehavior = (behaviorId: string) => {
    if (window.confirm(t('enterpriseEvolution.culturePrinciples.confirmDeleteBehavior'))) {
      deleteBehaviorMutation.mutate(behaviorId);
    }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={() => navigate('/enterprise-evolution/foundations/culture-principles')}
          className="rounded-full p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">
            {isEditing ? t('enterpriseEvolution.culturePrinciples.formTitleEdit') : t('enterpriseEvolution.culturePrinciples.formTitleNew')}
          </h1>
          <p className="mt-0.5 text-xs text-slate-400 dark:text-white/40">
            {t('enterpriseEvolution.culturePrinciples.formSubtitle')}
          </p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 dark:border-white/10 dark:bg-white/5">
          <div className="flex items-center gap-2 border-b border-slate-100 pb-3 dark:border-white/5">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300">
              <Heart className="h-4 w-4" />
            </span>
            <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
              {t('enterpriseEvolution.culturePrinciples.infoTitle')}
            </h2>
          </div>

          <div>
            <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.codeLabel')} *</label>
            <input
              type="text"
              required
              value={principleCode}
              onChange={(e) => setPrincipleCode(e.target.value)}
              placeholder={t('enterpriseEvolution.culturePrinciples.codePlaceholder')}
              className={inputClass}
            />
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.nameEnLabel')} *</label>
              <input
                type="text"
                required
                value={nameEn}
                onChange={(e) => setNameEn(e.target.value)}
                placeholder={t('enterpriseEvolution.culturePrinciples.nameEnPlaceholder')}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.nameEsLabel')}</label>
              <input
                type="text"
                value={nameEs}
                onChange={(e) => setNameEs(e.target.value)}
                placeholder={t('enterpriseEvolution.culturePrinciples.nameEsPlaceholder')}
                className={inputClass}
              />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.descEnLabel')} *</label>
              <textarea
                required
                rows={3}
                value={descriptionEn}
                onChange={(e) => setDescriptionEn(e.target.value)}
                placeholder={t('enterpriseEvolution.culturePrinciples.descEnPlaceholder')}
                className={inputClass}
              />
            </div>
            <div>
              <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.descEsLabel')}</label>
              <textarea
                rows={3}
                value={descriptionEs}
                onChange={(e) => setDescriptionEs(e.target.value)}
                placeholder={t('enterpriseEvolution.culturePrinciples.descEsPlaceholder')}
                className={inputClass}
              />
            </div>
          </div>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.statusLabel')}</label>
              <select value={status} onChange={(e) => setStatus(e.target.value as CulturePrincipleStatus)} className={inputClass}>
                {STATUS_OPTIONS.map((s) => (
                  <option key={s} value={s}>
                    {t(`enterpriseEvolution.culturePrinciples.status.${s}` as const)}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.versionLabelForm')}</label>
              <input
                type="number"
                min={1}
                value={versionNumber}
                onChange={(e) => setVersionNumber(Math.max(1, Number(e.target.value) || 1))}
                className={inputClass}
              />
            </div>
          </div>

          {status === 'Approved' && (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.effectiveFromLabel')} *</label>
                <input type="date" value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} className={inputClass} />
              </div>
              <div>
                <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.effectiveToLabel')}</label>
                <input type="date" value={effectiveTo} onChange={(e) => setEffectiveTo(e.target.value)} className={inputClass} />
              </div>
            </div>
          )}
        </section>

        {errorMessage && (
          <div className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700 dark:border-rose-500/20 dark:bg-rose-500/10 dark:text-rose-300">
            {errorMessage}
          </div>
        )}

        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={() => navigate('/enterprise-evolution/foundations/culture-principles')}
            className="rounded-full px-4 py-2 text-xs font-medium text-slate-500 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
          >
            {t('common.cancel')}
          </button>
          <button
            type="submit"
            disabled={saveMutation.isPending}
            className="rounded-full bg-gradient-to-r from-rose-500 to-pink-500 px-5 py-2 text-xs font-semibold text-white shadow-md shadow-rose-500/20 hover:brightness-105 disabled:opacity-50"
          >
            {saveMutation.isPending ? t('common.saving') : isEditing ? t('common.save') : t('enterpriseEvolution.culturePrinciples.saveAndContinue')}
          </button>
        </div>
      </form>

      {/* Behaviors: dbo.CulturePrincipleBehavior, linked to this CulturePrinciple */}
      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 dark:border-white/10 dark:bg-white/5">
        <div className="flex items-center justify-between gap-2 border-b border-slate-100 pb-3 dark:border-white/5">
          <div className="flex items-center gap-2">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-indigo-100 text-indigo-600 dark:bg-indigo-500/15 dark:text-indigo-300">
              <ListChecks className="h-4 w-4" />
            </span>
            <div>
              <h2 className="text-sm font-semibold text-slate-900 dark:text-white">
                {t('enterpriseEvolution.culturePrinciples.behaviorsTitle')}
              </h2>
              <p className="text-xs text-slate-400 dark:text-white/40">
                {t('enterpriseEvolution.culturePrinciples.behaviorsSubtitle')}
              </p>
            </div>
          </div>
          {isEditing && (
            <button
              type="button"
              onClick={openNewBehaviorForm}
              className="inline-flex shrink-0 items-center gap-1.5 rounded-full bg-indigo-100 px-3 py-1.5 text-xs font-semibold text-indigo-700 hover:bg-indigo-200 dark:bg-indigo-500/15 dark:text-indigo-300 dark:hover:bg-indigo-500/25"
            >
              <Plus className="h-3.5 w-3.5" />
              {t('enterpriseEvolution.culturePrinciples.addBehavior')}
            </button>
          )}
        </div>

        {!isEditing && (
          <p className="text-xs text-slate-400 dark:text-white/40">
            {t('enterpriseEvolution.culturePrinciples.behaviorsSaveFirstNotice')}
          </p>
        )}

        {isEditing && (
          <>
            {behaviorsQuery.isSuccess && (behaviorsQuery.data?.length ?? 0) === 0 && !showBehaviorForm && (
              <p className="text-xs text-slate-400 dark:text-white/40">
                {t('enterpriseEvolution.culturePrinciples.behaviorsEmpty')}
              </p>
            )}

            {(behaviorsQuery.data?.length ?? 0) > 0 && (
              <div className="divide-y divide-slate-100 dark:divide-white/5">
                {behaviorsQuery.data!.map((b) => (
                  <div key={b.culturePrincipleBehaviorId} className="flex items-start justify-between gap-3 py-3">
                    <div className="min-w-0 flex-1">
                      <span
                        className={clsx(
                          'inline-block rounded-md px-2 py-0.5 text-[11px] font-medium',
                          'bg-indigo-50 text-indigo-700 dark:bg-indigo-500/10 dark:text-indigo-300',
                        )}
                      >
                        {t(`enterpriseEvolution.culturePrinciples.behaviorType.${b.behaviorType}` as const)}
                      </span>
                      <p className="mt-1 text-sm text-slate-700 dark:text-white/70">
                        {isSpanish ? b.descriptionEs || b.descriptionEn : b.descriptionEn}
                      </p>
                    </div>
                    <div className="flex shrink-0 items-center gap-1">
                      <button
                        type="button"
                        onClick={() => openEditBehaviorForm(b)}
                        className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
                        title={t('common.edit')}
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDeleteBehavior(b.culturePrincipleBehaviorId)}
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

            {showBehaviorForm && (
              <form
                onSubmit={handleBehaviorSubmit}
                className="space-y-3 rounded-xl border border-indigo-100 bg-indigo-50/40 p-4 dark:border-indigo-500/20 dark:bg-indigo-500/5"
              >
                <p className="text-xs font-semibold text-slate-700 dark:text-white/70">
                  {behaviorForm.behaviorId
                    ? t('enterpriseEvolution.culturePrinciples.editBehavior')
                    : t('enterpriseEvolution.culturePrinciples.addBehavior')}
                </p>
                <div>
                  <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.behaviorCodeLabel')} *</label>
                  <input
                    type="text"
                    value={behaviorForm.behaviorCode}
                    onChange={(e) => setBehaviorForm((f) => ({ ...f, behaviorCode: e.target.value }))}
                    placeholder={t('enterpriseEvolution.culturePrinciples.behaviorCodePlaceholder')}
                    className={inputClass}
                  />
                </div>
                <div>
                  <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.behaviorTypeLabel')}</label>
                  <select
                    value={behaviorForm.behaviorType}
                    onChange={(e) =>
                      setBehaviorForm((f) => ({ ...f, behaviorType: e.target.value as CulturePrincipleBehaviorType }))
                    }
                    className={inputClass}
                  >
                    {BEHAVIOR_TYPE_OPTIONS.map((bt) => (
                      <option key={bt} value={bt}>
                        {t(`enterpriseEvolution.culturePrinciples.behaviorType.${bt}` as const)}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <div>
                    <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.behaviorDescEnLabel')} *</label>
                    <textarea
                      rows={2}
                      value={behaviorForm.descriptionEn}
                      onChange={(e) => setBehaviorForm((f) => ({ ...f, descriptionEn: e.target.value }))}
                      className={inputClass}
                    />
                  </div>
                  <div>
                    <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.behaviorDescEsLabel')}</label>
                    <textarea
                      rows={2}
                      value={behaviorForm.descriptionEs}
                      onChange={(e) => setBehaviorForm((f) => ({ ...f, descriptionEs: e.target.value }))}
                      className={inputClass}
                    />
                  </div>
                </div>
                <div>
                  <label className={labelClass}>{t('enterpriseEvolution.culturePrinciples.behaviorSortOrderLabel')}</label>
                  <input
                    type="number"
                    min={0}
                    value={behaviorForm.sortOrder}
                    onChange={(e) => setBehaviorForm((f) => ({ ...f, sortOrder: Number(e.target.value) || 0 }))}
                    className={`${inputClass} max-w-[120px]`}
                  />
                </div>

                {behaviorError && <p className="text-xs text-rose-600 dark:text-rose-400">{behaviorError}</p>}

                <div className="flex justify-end gap-2 pt-1">
                  <button
                    type="button"
                    onClick={() => {
                      setShowBehaviorForm(false);
                      setBehaviorForm(emptyBehaviorForm);
                    }}
                    className="rounded-full px-3 py-1.5 text-xs font-medium text-slate-500 hover:bg-slate-100 dark:text-white/60 dark:hover:bg-white/10"
                  >
                    {t('common.cancel')}
                  </button>
                  <button
                    type="submit"
                    disabled={behaviorMutation.isPending}
                    className="rounded-full bg-indigo-600 px-4 py-1.5 text-xs font-semibold text-white hover:brightness-105 disabled:opacity-50"
                  >
                    {behaviorMutation.isPending ? t('common.saving') : t('common.save')}
                  </button>
                </div>
              </form>
            )}
          </>
        )}
      </section>
    </div>
  );
}
