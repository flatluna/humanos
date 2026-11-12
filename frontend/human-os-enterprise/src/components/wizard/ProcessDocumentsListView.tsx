import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  ListTree,
  Plus,
  Search,
  BookOpen,
  BarChart3,
  Trash2,
  Calendar,
  Layers,
  GraduationCap,
  ArrowRight,
  CheckCircle2,
  Clock,
  AlertTriangle,
  X,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  getProcessDocuments,
  deleteProcessDocument,
  type ProcessDocumentListItemDto,
} from '@/api/processDocumentsApi';

export interface ProcessDocumentsListViewProps {
  tenantId?: string;
  onCreateNew: () => void;
  onEditProcess: (documentId: string) => void;
  onViewSummary: (documentId: string) => void;
}

export function ProcessDocumentsListView({
  tenantId,
  onCreateNew,
  onEditProcess,
  onViewSummary,
}: ProcessDocumentsListViewProps) {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [searchTerm, setSearchTerm] = useState('');
  const [deleteTarget, setDeleteTarget] = useState<ProcessDocumentListItemDto | null>(null);
  const [confirmText, setConfirmText] = useState('');

  const { data: processes = [], isLoading, isError, refetch } = useQuery({
    queryKey: ['process-documents', tenantId],
    queryFn: () => getProcessDocuments(tenantId),
    enabled: !!tenantId,
  });

  const deleteMutation = useMutation({
    mutationFn: (docId: string) => deleteProcessDocument(docId, tenantId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['process-documents', tenantId] });
      setDeleteTarget(null);
      setConfirmText('');
    },
    onError: (err: any) => {
      alert(t('processLearning.list.deleteError', { message: err?.response?.data?.message || err?.message }));
    },
  });

  const handleDelete = (doc: ProcessDocumentListItemDto, e: React.MouseEvent) => {
    e.stopPropagation();
    setConfirmText('');
    setDeleteTarget(doc);
  };

  const closeDeleteModal = () => {
    if (deleteMutation.isPending) return;
    setDeleteTarget(null);
    setConfirmText('');
  };

  const confirmDelete = () => {
    if (!deleteTarget) return;
    deleteMutation.mutate(deleteTarget.processDocumentId);
  };

  const isConfirmTextMatching =
    !!deleteTarget && confirmText.trim().toLowerCase() === deleteTarget.title.trim().toLowerCase();

  const filteredProcesses = processes.filter((p) => {
    if (!searchTerm.trim()) return true;
    const q = searchTerm.toLowerCase();
    return (
      p.title.toLowerCase().includes(q) ||
      (p.code && p.code.toLowerCase().includes(q)) ||
      (p.status && p.status.toLowerCase().includes(q))
    );
  });

  return (
    <div className="space-y-6 animate-in fade-in duration-200">
      {/* Top Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-500/10 text-brand-600 dark:bg-brand-500/20 dark:text-brand-400">
              <ListTree className="h-5 w-5" />
            </span>
            <div>
              <h1 className="text-xl font-bold text-slate-900 dark:text-white">
                {t('processLearning.list.title')}
              </h1>
              <p className="text-xs text-slate-500 dark:text-white/60">
                {t('processLearning.list.subtitle')}
              </p>
            </div>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={onCreateNew}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-xs font-bold text-white shadow-md shadow-brand-500/20 transition-all hover:bg-brand-500 hover:shadow-brand-500/30 active:scale-95"
          >
            <Plus className="h-4 w-4" />
            <span>{t('processLearning.list.newProcess')}</span>
          </button>
        </div>
      </div>

      {/* Filter / Search Bar */}
      <div className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-white/5 sm:flex-row sm:items-center sm:justify-between">
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder={t('processLearning.list.searchPlaceholder')}
            className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2 pl-9 pr-4 text-xs text-slate-800 placeholder-slate-400 focus:border-brand-500 focus:bg-white focus:outline-none dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
        </div>

        <div className="flex items-center gap-2 text-xs font-medium text-slate-500 dark:text-white/60">
          <span>{t('processLearning.list.total')}</span>
          <span className="rounded-md bg-slate-100 px-2 py-0.5 font-bold text-slate-800 dark:bg-white/10 dark:text-white">
            {filteredProcesses.length}
          </span>
          <span>{filteredProcesses.length === 1 ? t('processLearning.list.processSingular') : t('processLearning.list.processPlural')}</span>
        </div>
      </div>

      {/* Loading State */}
      {isLoading && (
        <div className="flex min-h-[300px] items-center justify-center rounded-2xl border border-slate-200 bg-white p-8 dark:border-white/10 dark:bg-white/5">
          <div className="flex flex-col items-center gap-3">
            <div className="h-8 w-8 animate-spin rounded-full border-2 border-brand-500 border-t-transparent" />
            <p className="text-xs text-slate-500 dark:text-white/60">{t('processLearning.list.loading')}</p>
          </div>
        </div>
      )}

      {/* Error State */}
      {!isLoading && isError && (
        <div className="rounded-2xl border border-rose-200 bg-rose-50/60 p-6 text-center dark:border-rose-500/20 dark:bg-rose-500/5">
          <p className="text-xs font-semibold text-rose-800 dark:text-rose-300">
            {t('processLearning.list.loadError')}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-3 rounded-lg bg-rose-600 px-3 py-1.5 text-xs font-bold text-white hover:bg-rose-500"
          >
            {t('processLearning.list.retry')}
          </button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !isError && processes.length === 0 && (
        <div className="flex min-h-[360px] flex-col items-center justify-center rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center dark:border-white/15 dark:bg-white/[0.02]">
          <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-brand-50 text-brand-600 dark:bg-brand-500/10 dark:text-brand-400">
            <BookOpen className="h-8 w-8" />
          </div>
          <h2 className="mt-4 text-base font-bold text-slate-900 dark:text-white">
            {t('processLearning.list.emptyTitle')}
          </h2>
          <p className="mt-1 max-w-md text-xs text-slate-500 dark:text-white/60">
            {t('processLearning.list.emptyDescription')}
          </p>
          <button
            type="button"
            onClick={onCreateNew}
            className="mt-6 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-5 py-2.5 text-xs font-bold text-white shadow-md shadow-brand-500/20 hover:bg-brand-500"
          >
            <Plus className="h-4 w-4" />
            <span>{t('processLearning.list.emptyCreateFirst')}</span>
          </button>
        </div>
      )}

      {/* Search No Matches State */}
      {!isLoading && !isError && processes.length > 0 && filteredProcesses.length === 0 && (
        <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center dark:border-white/10 dark:bg-white/5">
          <p className="text-xs text-slate-500 dark:text-white/60">
            {t('processLearning.list.noMatches', { searchTerm })}
          </p>
        </div>
      )}

      {/* Cards Grid */}
      {!isLoading && !isError && filteredProcesses.length > 0 && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filteredProcesses.map((proc) => {
            const isAnalyzed = proc.status === 'Analyzed' || proc.status === 'Approved' || proc.status === 'Published';
            const formattedDate = proc.createdDate
              ? new Date(proc.createdDate).toLocaleDateString(i18n.language.startsWith('es') ? 'es-ES' : 'en-US', {
                  day: 'numeric',
                  month: 'short',
                  year: 'numeric',
                })
              : '—';

            return (
              <div
                key={proc.processDocumentId}
                onClick={() => onEditProcess(proc.processDocumentId)}
                className="group relative flex flex-col justify-between rounded-2xl border border-slate-200 bg-white p-5 shadow-sm transition-all hover:border-brand-500 hover:shadow-md hover:shadow-brand-500/10 dark:border-white/10 dark:bg-white/5 dark:hover:border-brand-400 cursor-pointer"
              >
                <div>
                  {/* Top card metadata */}
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-center gap-1.5 flex-wrap">
                      <span
                        className={`inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-[10px] font-bold ${
                          isAnalyzed
                            ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300'
                            : 'bg-amber-50 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300'
                        }`}
                      >
                        {isAnalyzed ? <CheckCircle2 className="h-3 w-3" /> : <Clock className="h-3 w-3" />}
                        {proc.status || t('processLearning.list.draftStatus')}
                      </span>
                      {proc.version && (
                        <span className="rounded-md bg-slate-100 px-1.5 py-0.5 text-[10px] font-semibold text-slate-600 dark:bg-white/10 dark:text-white/60">
                          v{proc.version}
                        </span>
                      )}
                      {proc.code && (
                        <span className="rounded-md bg-violet-50 px-1.5 py-0.5 text-[10px] font-semibold text-violet-700 dark:bg-violet-500/20 dark:text-violet-300">
                          {proc.code}
                        </span>
                      )}
                    </div>

                    <button
                      type="button"
                      title={t('processLearning.list.deleteProcess')}
                      onClick={(e) => handleDelete(proc, e)}
                      className="rounded-lg p-1.5 text-slate-400 transition hover:bg-rose-50 hover:text-rose-600 dark:hover:bg-rose-500/20 dark:hover:text-rose-300"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>

                  {/* Title & Description */}
                  <div className="mt-3">
                    <h2 className="text-sm font-bold text-slate-900 group-hover:text-brand-600 dark:text-white dark:group-hover:text-brand-400 transition-colors line-clamp-2">
                      {proc.title}
                    </h2>
                    <p className="mt-1 text-xs text-slate-500 dark:text-white/60 line-clamp-2">
                      {t('processLearning.list.cardDescription')}
                    </p>
                  </div>

                  {/* Metrics Pills */}
                  <div className="mt-4 grid grid-cols-2 gap-2 border-t border-slate-100 pt-3 dark:border-white/5">
                    <div className="flex items-center gap-2 rounded-xl bg-slate-50 p-2 dark:bg-white/[0.03]">
                      <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-brand-100 text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
                        <Layers className="h-3.5 w-3.5" />
                      </span>
                      <div>
                        <p className="text-xs font-bold text-slate-900 dark:text-white">
                          {proc.totalStepsCount || 0}
                        </p>
                        <p className="text-[10px] text-slate-400">{t('processLearning.list.steps')}</p>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 rounded-xl bg-slate-50 p-2 dark:bg-white/[0.03]">
                      <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-teal-100 text-teal-700 dark:bg-teal-500/20 dark:text-teal-300">
                        <GraduationCap className="h-3.5 w-3.5" />
                      </span>
                      <div>
                        <p className="text-xs font-bold text-slate-900 dark:text-white">
                          {proc.uniqueSkillsCount || 0}
                        </p>
                        <p className="text-[10px] text-slate-400">{t('processLearning.list.skillsMatrix')}</p>
                      </div>
                    </div>
                  </div>
                </div>

                {/* Bottom Actions */}
                <div className="mt-5 flex items-center justify-between border-t border-slate-100 pt-3 dark:border-white/5">
                  <div className="flex items-center gap-1.5 text-[10px] text-slate-400">
                    <Calendar className="h-3 w-3" />
                    <span>{formattedDate}</span>
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        onViewSummary(proc.processDocumentId);
                      }}
                      className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white dark:hover:bg-white/10"
                    >
                      <BarChart3 className="h-3.5 w-3.5 text-teal-600 dark:text-teal-400" />
                      <span>{t('processLearning.list.matrixButton')}</span>
                    </button>

                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        onEditProcess(proc.processDocumentId);
                      }}
                      className="inline-flex items-center gap-1 rounded-lg bg-brand-600 px-3 py-1 text-xs font-bold text-white shadow-sm hover:bg-brand-500 transition-colors"
                    >
                      <span>{t('processLearning.list.openAndEdit')}</span>
                      <ArrowRight className="h-3 w-3" />
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {deleteTarget && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-sm p-4"
          onClick={closeDeleteModal}
        >
          <div
            className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-3">
              <div className="flex items-center gap-3">
                <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-rose-50 text-rose-600 dark:bg-rose-500/20 dark:text-rose-300">
                  <AlertTriangle className="h-5 w-5" />
                </span>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {t('processLearning.list.deleteModalTitle')}
                </h3>
              </div>
              <button
                type="button"
                onClick={closeDeleteModal}
                className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <p className="mt-4 text-sm text-slate-600 dark:text-white/70">
              {t('processLearning.list.deleteModalIntro')}
            </p>

            <ul className="mt-3 space-y-2 rounded-xl bg-slate-50 p-3 text-xs text-slate-700 dark:bg-white/5 dark:text-white/80">
              <li className="flex items-start gap-2">
                <span className="mt-0.5 h-1.5 w-1.5 shrink-0 rounded-full bg-rose-500" />
                <span>
                  {t('processLearning.list.deleteModalItemProcess', {
                    title: deleteTarget.title,
                    version: deleteTarget.version || '1.0',
                  })}
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-0.5 h-1.5 w-1.5 shrink-0 rounded-full bg-rose-500" />
                <span>
                  {t('processLearning.list.deleteModalItemSteps', { count: deleteTarget.totalStepsCount || 0 })}
                </span>
              </li>
              <li className="flex items-start gap-2">
                <span className="mt-0.5 h-1.5 w-1.5 shrink-0 rounded-full bg-rose-500" />
                <span>{t('processLearning.list.deleteModalItemCapability')}</span>
              </li>
            </ul>

            <p className="mt-3 text-xs font-bold text-rose-600 dark:text-rose-400">
              {t('processLearning.list.deleteModalWarning')}
            </p>

            <div className="mt-4">
              <label className="text-xs font-semibold text-slate-700 dark:text-white/70">
                {t('processLearning.list.deleteModalConfirmLabel')}
              </label>
              <input
                type="text"
                value={confirmText}
                onChange={(e) => setConfirmText(e.target.value)}
                placeholder={t('processLearning.list.deleteModalConfirmPlaceholder', { title: deleteTarget.title })}
                autoFocus
                className="mt-1.5 w-full rounded-lg border border-slate-300 px-3 py-2 text-sm text-slate-900 focus:border-rose-500 focus:outline-none focus:ring-1 focus:ring-rose-500 dark:border-white/20 dark:bg-slate-800 dark:text-white"
              />
              {user?.name && (
                <p className="mt-1.5 text-[11px] text-slate-400 dark:text-white/40">
                  {t('processLearning.list.deleteModalConfirmingAs', { name: user.name })}
                </p>
              )}
            </div>

            <div className="mt-5 flex items-center justify-end gap-2">
              <button
                type="button"
                onClick={closeDeleteModal}
                disabled={deleteMutation.isPending}
                className="rounded-lg border border-slate-200 bg-white px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50 dark:border-white/10 dark:bg-white/5 dark:text-white dark:hover:bg-white/10"
              >
                {t('processLearning.list.deleteModalCancel')}
              </button>
              <button
                type="button"
                onClick={confirmDelete}
                disabled={!isConfirmTextMatching || deleteMutation.isPending}
                className="inline-flex items-center gap-1.5 rounded-lg bg-rose-600 px-4 py-2 text-sm font-bold text-white shadow-sm hover:bg-rose-500 disabled:cursor-not-allowed disabled:opacity-40"
              >
                <Trash2 className="h-4 w-4" />
                {deleteMutation.isPending
                  ? t('processLearning.list.deleteModalDeleting')
                  : t('processLearning.list.deleteModalConfirmButton')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
