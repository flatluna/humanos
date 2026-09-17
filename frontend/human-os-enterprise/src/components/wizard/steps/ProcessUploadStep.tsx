import { useState, useEffect, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import {
  UploadCloud,
  FileText,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  Layers,
  ListTree,
  Table,
  Sparkles,
  ArrowRight,
  Eye,
} from 'lucide-react';
import {
  uploadProcessDocument,
  getProcessDocumentStructure,
  getProcessDocuments,
  type ProcessDocumentUploadResponse,
  type ProcessDocumentStructureDto,
} from '@/api/processDocumentsApi';

export interface ProcessUploadStepProps {
  tenantId?: string;
  onStructureLoaded: (result: ProcessDocumentUploadResponse, structure: ProcessDocumentStructureDto) => void;
  onNext: () => void;
  initialUploadResult?: ProcessDocumentUploadResponse | null;
  initialStructure?: ProcessDocumentStructureDto | null;
}

export function ProcessUploadStep({
  tenantId,
  onStructureLoaded,
  onNext,
  initialUploadResult,
  initialStructure,
}: ProcessUploadStepProps) {
  const { t } = useTranslation();

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [processName, setProcessName] = useState('');
  const [processDescription, setProcessDescription] = useState('');
  const [processLanguage, setProcessLanguage] = useState('es');
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploadResult, setUploadResult] = useState<ProcessDocumentUploadResponse | null>(initialUploadResult || null);
  const [structure, setStructure] = useState<ProcessDocumentStructureDto | null>(initialStructure || null);
  const [showFullStructureModal, setShowFullStructureModal] = useState(false);

  // Load existing processes in the organization to validate unique process name
  const { data: existingProcesses = [] } = useQuery({
    queryKey: ['processDocuments', tenantId],
    queryFn: () => getProcessDocuments(tenantId),
    enabled: !!tenantId,
  });

  const trimmedName = processName.trim();
  const isDuplicateName = useMemo(() => {
    if (!trimmedName) return false;
    const lower = trimmedName.toLowerCase();
    return existingProcesses.some((p) => p.title.trim().toLowerCase() === lower);
  }, [trimmedName, existingProcesses]);

  useEffect(() => {
    if (initialUploadResult) setUploadResult(initialUploadResult);
  }, [initialUploadResult]);

  useEffect(() => {
    if (initialStructure) setStructure(initialStructure);
  }, [initialStructure]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.name.toLowerCase().endsWith('.pdf') && file.type !== 'application/pdf') {
        setUploadError(t('processLearning.uploadStep.fileTypesNotice'));
        setSelectedFile(null);
        return;
      }
      setSelectedFile(file);
      setUploadError(null);
      if (!processName) {
        setProcessName(file.name.replace(/\.pdf$/i, ''));
      }
    }
  };

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFile) {
      setUploadError(t('processLearning.uploadStep.dropPrompt'));
      return;
    }

    if (!trimmedName) {
      setUploadError(t('processLearning.uploadStep.processNameRequiredError', 'El nombre del proceso es obligatorio y debe ser único.'));
      return;
    }

    if (isDuplicateName) {
      setUploadError(t('processLearning.uploadStep.duplicateNameError', 'Ya existe un proceso registrado con este nombre en tu organización. Elige un nombre único.'));
      return;
    }

    setIsUploading(true);
    setUploadError(null);

    try {
      const result = await uploadProcessDocument(selectedFile, {
        processName: trimmedName,
        processDescription: processDescription.trim() || undefined,
      });

      setUploadResult(result);

      // Fetch the full structured extraction
      const fullStructure = await getProcessDocumentStructure(result.document_id, tenantId);
      setStructure(fullStructure);
      onStructureLoaded(result, fullStructure);
    } catch (err: any) {
      console.error('Error uploading process document:', err);
      const msg = err.response?.data?.message || err.message || 'Error al procesar el documento PDF.';
      setUploadError(msg);
    } finally {
      setIsUploading(false);
    }
  };

  const handleReset = () => {
    setSelectedFile(null);
    setUploadResult(null);
    setStructure(null);
    setUploadError(null);
  };

  return (
    <div className="space-y-6">
      {/* Upload card if not yet uploaded */}
      {!uploadResult ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
          <div className="mb-4">
            <h3 className="text-base font-bold text-slate-900 dark:text-white">
              {t('processLearning.uploadStep.title')}
            </h3>
            <p className="text-xs text-slate-500 dark:text-white/60">
              {t('processLearning.uploadStep.subtitle')}
            </p>
          </div>

          <form onSubmit={handleUpload} className="space-y-5">
            {/* File dropzone */}
            <div className="relative flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-slate-300 p-8 text-center transition hover:border-brand-500 dark:border-white/20 dark:hover:border-brand-400">
              <input
                type="file"
                accept=".pdf,application/pdf"
                onChange={handleFileChange}
                disabled={isUploading}
                aria-label="Seleccionar archivo PDF"
                className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
              />
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600 dark:bg-brand-500/10 dark:text-brand-400">
                <UploadCloud className="h-6 w-6" />
              </div>
              <p className="mt-3 text-sm font-medium text-slate-900 dark:text-white">
                {selectedFile ? (
                  <span className="flex items-center gap-2 text-brand-600 dark:text-brand-400 font-semibold">
                    <FileText className="h-4 w-4" />
                    {selectedFile.name} ({(selectedFile.size / 1024 / 1024).toFixed(2)} MB)
                  </span>
                ) : (
                  t('processLearning.uploadStep.dropPrompt')
                )}
              </p>
              <p className="mt-1 text-xs text-slate-400 dark:text-white/40">
                {t('processLearning.uploadStep.fileTypesNotice')}
              </p>
            </div>

            {/* Form Fields */}
            <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
              <div className="md:col-span-2">
                <div className="flex items-center justify-between">
                  <label className="block text-xs font-semibold uppercase tracking-wider text-slate-700 dark:text-white/70">
                    {t('processLearning.uploadStep.processNameLabel')} <span className="text-rose-500 font-bold">*</span>
                  </label>
                  <span className="text-[10px] font-bold text-rose-500 uppercase tracking-wider">
                    Obligatorio & Único
                  </span>
                </div>
                <input
                  type="text"
                  required
                  value={processName}
                  onChange={(e) => {
                    setProcessName(e.target.value);
                    if (uploadError) setUploadError(null);
                  }}
                  placeholder={t('processLearning.uploadStep.processNamePlaceholder')}
                  disabled={isUploading}
                  className={`mt-1.5 w-full rounded-xl border px-3.5 py-2.5 text-sm shadow-sm focus:outline-none focus:ring-2 transition-colors ${
                    isDuplicateName
                      ? 'border-rose-400 bg-rose-50/20 text-rose-900 focus:border-rose-500 focus:ring-rose-500/20 dark:border-rose-500 dark:bg-rose-950/20 dark:text-white'
                      : 'border-slate-200 bg-white text-slate-900 focus:border-brand-500 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/5 dark:text-white'
                  }`}
                />
                {isDuplicateName ? (
                  <p className="mt-1 flex items-center gap-1 text-[11px] font-semibold text-rose-600 dark:text-rose-400">
                    <AlertTriangle className="h-3.5 w-3.5 shrink-0" />
                    <span>{t('processLearning.uploadStep.duplicateNameError', 'Ya existe un proceso registrado con este nombre en tu organización. Elige un nombre único.')}</span>
                  </p>
                ) : (
                  <p className="mt-1 text-[11px] text-slate-400 dark:text-white/40">
                    {t('processLearning.uploadStep.processNameHelp')}
                  </p>
                )}
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-white/50">
                  {t('processLearning.uploadStep.languageLabel')}
                </label>
                <select
                  value={processLanguage}
                  onChange={(e) => setProcessLanguage(e.target.value)}
                  disabled={isUploading}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/5 dark:text-white"
                >
                  <option value="es">Español (es)</option>
                  <option value="en">English (en)</option>
                </select>
              </div>

              <div className="md:col-span-3">
                <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-white/50">
                  {t('processLearning.uploadStep.processDescLabel')}
                </label>
                <input
                  type="text"
                  value={processDescription}
                  onChange={(e) => setProcessDescription(e.target.value)}
                  placeholder={t('processLearning.uploadStep.processDescPlaceholder')}
                  disabled={isUploading}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </div>
            </div>

            {/* Error banner */}
            {uploadError && (
              <div className="flex items-center gap-2 rounded-xl bg-rose-50 p-3.5 text-sm text-rose-700 dark:bg-rose-500/10 dark:text-rose-400">
                <AlertTriangle className="h-4 w-4 shrink-0" />
                <span>{uploadError}</span>
              </div>
            )}

            {/* Submit Action */}
            <div className="flex justify-end">
              <button
                type="submit"
                disabled={!selectedFile || isUploading || !trimmedName || isDuplicateName}
                className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-6 py-2.5 text-sm font-semibold text-white shadow-md shadow-brand-500/25 transition hover:from-brand-500 hover:to-indigo-500 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {isUploading ? (
                  <>
                    <RefreshCw className="h-4 w-4 animate-spin" />
                    <span>{t('processLearning.uploadStep.uploadingText')}</span>
                  </>
                ) : (
                  <>
                    <Sparkles className="h-4 w-4" />
                    <span>{t('processLearning.uploadStep.uploadBtn')}</span>
                  </>
                )}
              </button>
            </div>
          </form>
        </div>
      ) : (
        /* Completion Summary Card */
        <div className="space-y-6">
          <div className="rounded-2xl border border-emerald-200 bg-emerald-50/50 p-6 dark:border-emerald-500/20 dark:bg-emerald-500/5">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
              <div className="flex items-start gap-3">
                <CheckCircle2 className="mt-0.5 h-6 w-6 text-emerald-600 dark:text-emerald-400" />
                <div>
                  <h3 className="text-lg font-bold text-slate-900 dark:text-white">
                    {uploadResult.process_name || uploadResult.file_name}
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-white/60">
                    {t('processLearning.uploadStep.documentId')}{' '}
                    <code className="rounded bg-slate-200/60 px-1.5 py-0.5 font-mono text-slate-800 dark:bg-white/10 dark:text-white">
                      {uploadResult.document_id}
                    </code>
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={handleReset}
                  className="rounded-xl border border-slate-300 bg-white px-4 py-2 text-xs font-semibold text-slate-700 transition hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white"
                >
                  {t('processLearning.uploadStep.replaceFileBtn')}
                </button>
                <button
                  type="button"
                  onClick={onNext}
                  className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-5 py-2 text-xs font-semibold text-white shadow-sm hover:from-brand-500 hover:to-indigo-500"
                >
                  <span>{t('processLearning.uploadStep.reviewProcessBtn')}</span>
                  <ArrowRight className="h-4 w-4" />
                </button>
              </div>
            </div>

            {/* Metrics Grid */}
            <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-6">
              <div className="rounded-xl bg-white p-3.5 shadow-sm dark:bg-white/5 border border-slate-200/60 dark:border-white/5 text-center">
                <div className="flex items-center justify-center gap-1.5 text-brand-600 dark:text-brand-400">
                  <Layers className="h-4 w-4" />
                  <span className="text-[10px] font-bold uppercase tracking-wider">{t('processLearning.uploadStep.chapters')}</span>
                </div>
                <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">
                  {uploadResult.chapters_identified}
                </p>
              </div>

              <div className="rounded-xl bg-white p-3.5 shadow-sm dark:bg-white/5 border border-slate-200/60 dark:border-white/5 text-center">
                <div className="flex items-center justify-center gap-1.5 text-indigo-600 dark:text-indigo-400">
                  <ListTree className="h-4 w-4" />
                  <span className="text-[10px] font-bold uppercase tracking-wider">{t('processLearning.uploadStep.steps')}</span>
                </div>
                <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">
                  {uploadResult.steps_identified}
                </p>
              </div>

              <div className="rounded-xl bg-white p-3.5 shadow-sm dark:bg-white/5 border border-slate-200/60 dark:border-white/5 text-center">
                <div className="flex items-center justify-center gap-1.5 text-teal-600 dark:text-teal-400">
                  <Table className="h-4 w-4" />
                  <span className="text-[10px] font-bold uppercase tracking-wider">{t('processLearning.uploadStep.tables')}</span>
                </div>
                <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">
                  {uploadResult.tables_extracted}
                </p>
              </div>

              <div className="rounded-xl bg-white p-3.5 shadow-sm dark:bg-white/5 border border-slate-200/60 dark:border-white/5 text-center">
                <div className="flex items-center justify-center gap-1.5 text-violet-600 dark:text-violet-400">
                  <FileText className="h-4 w-4" />
                  <span className="text-[10px] font-bold uppercase tracking-wider">{t('processLearning.uploadStep.appendices')}</span>
                </div>
                <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">
                  {structure?.appendices.length ?? 0}
                </p>
              </div>

              <div className="rounded-xl bg-white p-3.5 shadow-sm dark:bg-white/5 border border-slate-200/60 dark:border-white/5 text-center">
                <div className="flex items-center justify-center gap-1.5 text-amber-600 dark:text-amber-400">
                  <AlertTriangle className="h-4 w-4" />
                  <span className="text-[10px] font-bold uppercase tracking-wider">{t('processLearning.uploadStep.warnings')}</span>
                </div>
                <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">
                  {uploadResult.warnings?.length ?? 0}
                </p>
              </div>

              <div className="rounded-xl bg-white p-3.5 shadow-sm dark:bg-white/5 border border-slate-200/60 dark:border-white/5 flex flex-col justify-center items-center">
                <button
                  type="button"
                  onClick={() => setShowFullStructureModal(true)}
                  className="inline-flex items-center gap-1 text-xs font-semibold text-brand-600 hover:text-brand-700 dark:text-brand-400"
                >
                  <Eye className="h-3.5 w-3.5" />
                  <span>{t('processLearning.common.viewFullStructure')}</span>
                </button>
              </div>
            </div>
          </div>

          {/* Warnings notice if any */}
          {Boolean(uploadResult.warnings?.length) && (
            <div className="rounded-xl border border-amber-200 bg-amber-50/50 p-4 text-xs text-amber-800 dark:border-amber-500/20 dark:bg-amber-500/5 dark:text-amber-300">
              <p className="font-semibold">{t('processLearning.uploadStep.warnings')}:</p>
              <ul className="mt-1 list-inside list-disc space-y-1">
                {uploadResult.warnings!.map((w, idx) => (
                  <li key={idx}>{w}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Full Structure Modal */}
      {showFullStructureModal && structure && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
          <div className="flex max-h-[85vh] w-full max-w-4xl flex-col rounded-2xl border border-slate-200 bg-white shadow-2xl dark:border-white/10 dark:bg-slate-900">
            <div className="flex items-center justify-between border-b border-slate-200 p-4 dark:border-white/10">
              <h3 className="text-sm font-bold text-slate-900 dark:text-white">
                {structure.document_title}
              </h3>
              <button
                type="button"
                onClick={() => setShowFullStructureModal(false)}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                ✕
              </button>
            </div>
            <div className="flex-1 overflow-auto p-4">
              <pre className="rounded-xl bg-slate-900 p-4 font-mono text-xs text-slate-100 dark:bg-black/80 max-h-96">
                {JSON.stringify(structure, null, 2)}
              </pre>
            </div>
            <div className="flex justify-end border-t border-slate-200 p-3 dark:border-white/10">
              <button
                type="button"
                onClick={() => setShowFullStructureModal(false)}
                className="rounded-xl bg-slate-900 px-4 py-2 text-xs font-semibold text-white dark:bg-white dark:text-slate-900"
              >
                {t('processLearning.common.close')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
