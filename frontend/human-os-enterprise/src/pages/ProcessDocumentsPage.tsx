import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@/auth/AuthContext';
import { getTenantPeople } from '@/api/orgStructureApi';
import {
  FileText,
  UploadCloud,
  CheckCircle2,
  AlertTriangle,
  Layers,
  ListTree,
  Table,
  CheckSquare,
  Sparkles,
  RefreshCw,
  ChevronDown,
  ChevronRight,
  Database,
  GraduationCap,
  BookOpen,
  Target,
  ShieldAlert,
  FileEdit,
  ClipboardList,
  UserCheck,
  User,
} from 'lucide-react';
import {
  uploadProcessDocument,
  getProcessDocumentStructure,
  analyzeWorkStepRequirements,
  searchCandidateClasses,
  selectRelevantEngrams,
  analyzeLearningContentCoverage,
  generateContentGapDraft,
  generateStepLearningAssignment,
  createEmployeeStepLearningAssignment,
  type ProcessDocumentUploadResponse,
  type ProcessDocumentStructureDto,
  type WorkStepRequirementsAnalysisResultDto,
  type CandidateClassSearchResponseDto,
  type RelevantEngramsResponseDto,
  type LearningContentCoverageResponseDto,
  type LearningContentGapDraftResponseDto,
  type StepLearningAssignmentResponseDto,
  type EmployeeStepLearningAssignmentResponseDto,
} from '@/api/processDocumentsApi';

export function ProcessDocumentsPage() {
  const { user } = useAuth();

  // Load tenant employees for Step 8 selection
  const { data: tenantPeople = [] } = useQuery({
    queryKey: ['tenantPeople', user?.tid],
    queryFn: () => getTenantPeople(user!.tid),
    enabled: !!user?.tid,
  });

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [processName, setProcessName] = useState('');
  const [processDescription, setProcessDescription] = useState('');
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [uploadResult, setUploadResult] = useState<ProcessDocumentUploadResponse | null>(null);
  const [structure, setStructure] = useState<ProcessDocumentStructureDto | null>(null);
  const [isLoadingStructure, setIsLoadingStructure] = useState(false);
  const [activeTab, setActiveTab] = useState<'steps' | 'tables' | 'json'>('steps');
  const [expandedChapters, setExpandedChapters] = useState<Record<string, boolean>>({});

  // Step 2 Analysis state
  const [analyzingStepId, setAnalyzingStepId] = useState<string | null>(null);
  const [stepAnalyses, setStepAnalyses] = useState<Record<string, WorkStepRequirementsAnalysisResultDto>>({});
  const [analysisErrors, setAnalysisErrors] = useState<Record<string, string>>({});

  // Step 3 Candidate Classes state
  const [searchingCandidatesStepId, setSearchingCandidatesStepId] = useState<string | null>(null);
  const [stepCandidateClasses, setStepCandidateClasses] = useState<Record<string, CandidateClassSearchResponseDto>>({});
  const [searchCandidateErrors, setSearchCandidateErrors] = useState<Record<string, string>>({});

  // Step 4 Relevant Engrams state
  const [selectingEngramsStepId, setSelectingEngramsStepId] = useState<string | null>(null);
  const [stepRelevantEngrams, setStepRelevantEngrams] = useState<Record<string, RelevantEngramsResponseDto>>({});
  const [selectEngramErrors, setSelectEngramErrors] = useState<Record<string, string>>({});

  // Step 5 Coverage & Content Gap state
  const [analyzingCoverageStepId, setAnalyzingCoverageStepId] = useState<string | null>(null);
  const [stepCoverages, setStepCoverages] = useState<Record<string, LearningContentCoverageResponseDto>>({});
  const [coverageErrors, setCoverageErrors] = useState<Record<string, string>>({});

  // Step 6 Content Gap Draft state
  const [generatingDraftStepId, setGeneratingDraftStepId] = useState<string | null>(null);
  const [stepGapDrafts, setStepGapDrafts] = useState<Record<string, LearningContentGapDraftResponseDto>>({});
  const [draftErrors, setDraftErrors] = useState<Record<string, string>>({});

  // Step 7 Learning Assignment state
  const [generatingAssignmentStepId, setGeneratingAssignmentStepId] = useState<string | null>(null);
  const [stepAssignments, setStepAssignments] = useState<Record<string, StepLearningAssignmentResponseDto>>({});
  const [assignmentErrors, setAssignmentErrors] = useState<Record<string, string>>({});

  // Step 8 Employee Personalization & Assignment state
  const [selectedEmployeeIdByStep, setSelectedEmployeeIdByStep] = useState<Record<string, string>>({});
  const [personalizingStepId, setPersonalizingStepId] = useState<string | null>(null);
  const [stepEmployeeAssignments, setStepEmployeeAssignments] = useState<Record<string, EmployeeStepLearningAssignmentResponseDto>>({});
  const [employeeAssignmentErrors, setEmployeeAssignmentErrors] = useState<Record<string, string>>({});

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      if (!file.name.toLowerCase().endsWith('.pdf') && file.type !== 'application/pdf') {
        setUploadError('Solo se permiten archivos en formato PDF.');
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
      setUploadError('Por favor selecciona un archivo PDF de proceso.');
      return;
    }

    setIsUploading(true);
    setUploadError(null);
    setUploadResult(null);
    setStructure(null);

    try {
      const result = await uploadProcessDocument(selectedFile, {
        processName: processName.trim() || undefined,
        processDescription: processDescription.trim() || undefined,
      });

      setUploadResult(result);

      // Fetch the full structured extraction
      setIsLoadingStructure(true);
      try {
        const fullStructure = await getProcessDocumentStructure(result.document_id, user?.tid);
        setStructure(fullStructure);
        // Expand first chapter by default
        if (fullStructure.chapters.length > 0) {
          setExpandedChapters({ [fullStructure.chapters[0].chapter_id]: true });
        }
      } catch (structErr) {
        console.warn('Could not fetch complete structure detail:', structErr);
      } finally {
        setIsLoadingStructure(false);
      }
    } catch (err: any) {
      console.error('Error uploading process document:', err);
      const msg = err.response?.data?.message || err.message || 'Error al procesar el documento PDF.';
      setUploadError(msg);
    } finally {
      setIsUploading(false);
    }
  };

  const toggleChapter = (chapterId: string) => {
    setExpandedChapters((prev) => ({
      ...prev,
      [chapterId]: !prev[chapterId],
    }));
  };

  const handleAnalyzeStep = async (stepId: string) => {
    if (!structure?.document_id) return;

    setAnalyzingStepId(stepId);
    setAnalysisErrors((prev) => ({ ...prev, [stepId]: '' }));

    try {
      const analysis = await analyzeWorkStepRequirements(structure.document_id, stepId, user?.tid);
      setStepAnalyses((prev) => ({ ...prev, [stepId]: analysis }));
    } catch (err: any) {
      console.error('Error analyzing step requirements:', err);
      const msg = err.response?.data?.message || err.message || 'Error al analizar requerimientos del paso.';
      setAnalysisErrors((prev) => ({ ...prev, [stepId]: msg }));
    } finally {
      setAnalyzingStepId(null);
    }
  };

  const handleSearchCandidates = async (stepId: string) => {
    if (!structure?.document_id) return;

    setSearchingCandidatesStepId(stepId);
    setSearchCandidateErrors((prev) => ({ ...prev, [stepId]: '' }));

    try {
      const result = await searchCandidateClasses(structure.document_id, stepId, user?.tid);
      setStepCandidateClasses((prev) => ({ ...prev, [stepId]: result }));
    } catch (err: any) {
      console.error('Error searching candidate classes:', err);
      const msg = err.response?.data?.message || err.message || 'Error al buscar clases candidatas en Engram Studio.';
      setSearchCandidateErrors((prev) => ({ ...prev, [stepId]: msg }));
    } finally {
      setSearchingCandidatesStepId(null);
    }
  };

  const handleSelectEngrams = async (stepId: string) => {
    if (!structure?.document_id) return;

    setSelectingEngramsStepId(stepId);
    setSelectEngramErrors((prev) => ({ ...prev, [stepId]: '' }));

    try {
      const result = await selectRelevantEngrams(structure.document_id, stepId, user?.tid);
      setStepRelevantEngrams((prev) => ({ ...prev, [stepId]: result }));
    } catch (err: any) {
      console.error('Error selecting relevant Engrams:', err);
      const msg = err.response?.data?.message || err.message || 'Error al seleccionar Engramas relevantes en Engram Studio.';
      setSelectEngramErrors((prev) => ({ ...prev, [stepId]: msg }));
    } finally {
      setSelectingEngramsStepId(null);
    }
  };

  const handleAnalyzeCoverage = async (stepId: string) => {
    if (!structure?.document_id) return;

    setAnalyzingCoverageStepId(stepId);
    setCoverageErrors((prev) => ({ ...prev, [stepId]: '' }));

    try {
      const result = await analyzeLearningContentCoverage(structure.document_id, stepId, user?.tid);
      setStepCoverages((prev) => ({ ...prev, [stepId]: result }));
    } catch (err: any) {
      console.error('Error analyzing content coverage:', err);
      const msg = err.response?.data?.message || err.message || 'Error al analizar cobertura de contenido.';
      setCoverageErrors((prev) => ({ ...prev, [stepId]: msg }));
    } finally {
      setAnalyzingCoverageStepId(null);
    }
  };

  const handleGenerateDraft = async (stepId: string) => {
    if (!structure?.document_id) return;

    setGeneratingDraftStepId(stepId);
    setDraftErrors((prev) => ({ ...prev, [stepId]: '' }));

    try {
      const result = await generateContentGapDraft(structure.document_id, stepId, user?.tid);
      setStepGapDrafts((prev) => ({ ...prev, [stepId]: result }));
    } catch (err: any) {
      console.error('Error generating content gap draft:', err);
      const msg = err.response?.data?.message || err.message || 'Error al generar borrador de brecha de contenido.';
      setDraftErrors((prev) => ({ ...prev, [stepId]: msg }));
    } finally {
      setGeneratingDraftStepId(null);
    }
  };

  const handleGenerateAssignment = async (stepId: string) => {
    if (!structure?.document_id) return;

    setGeneratingAssignmentStepId(stepId);
    setAssignmentErrors((prev) => ({ ...prev, [stepId]: '' }));

    try {
      const result = await generateStepLearningAssignment(structure.document_id, stepId, user?.tid);
      setStepAssignments((prev) => ({ ...prev, [stepId]: result }));
    } catch (err: any) {
      console.error('Error generating step learning assignment:', err);
      const msg = err.response?.data?.message || err.message || 'Error al generar la Asignación de Aprendizaje del Paso.';
      setAssignmentErrors((prev) => ({ ...prev, [stepId]: msg }));
    } finally {
      setGeneratingAssignmentStepId(null);
    }
  };

  const handlePersonalizeAndAssign = async (stepId: string) => {
    if (!structure?.document_id) return;
    const empId = selectedEmployeeIdByStep[stepId] || user?.personId;
    if (!empId) {
      setEmployeeAssignmentErrors((prev) => ({ ...prev, [stepId]: 'Selecciona un empleado para personalizar y asignar.' }));
      return;
    }

    setPersonalizingStepId(stepId);
    setEmployeeAssignmentErrors((prev) => ({ ...prev, [stepId]: '' }));

    try {
      const result = await createEmployeeStepLearningAssignment(
        structure.document_id,
        stepId,
        empId,
        user?.tid,
      );
      setStepEmployeeAssignments((prev) => ({ ...prev, [stepId]: result }));
    } catch (err: any) {
      console.error('Error personalizing employee assignment:', err);
      const msg = err.response?.data?.message || err.message || 'Error al personalizar la asignación al empleado.';
      setEmployeeAssignmentErrors((prev) => ({ ...prev, [stepId]: msg }));
    } finally {
      setPersonalizingStepId(null);
    }
  };

  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div>
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-500 to-brand-600 text-white shadow-md shadow-brand-500/20">
            <ListTree className="h-5 w-5" />
          </div>
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
              Documentos de Proceso
            </h1>
            <p className="text-sm text-slate-500 dark:text-white/60">
              Ingesta y extracción estructural de documentos operativos y manuales en Azure Data Lake Storage (Paso 1).
            </p>
          </div>
        </div>
      </div>

      {/* Upload Form Card */}
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
        <h2 className="mb-4 text-base font-semibold text-slate-900 dark:text-white">
          Subir Documento de Proceso (PDF)
        </h2>

        <form onSubmit={handleUpload} className="space-y-5">
          {/* File dropzone */}
          <div className="relative flex flex-col items-center justify-center rounded-xl border-2 border-dashed border-slate-300 p-8 text-center transition hover:border-brand-500 dark:border-white/20 dark:hover:border-brand-400">
            <input
              type="file"
              accept=".pdf,application/pdf"
              onChange={handleFileChange}
              disabled={isUploading}
              className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
            />
            <div className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-50 text-brand-600 dark:bg-brand-500/10 dark:text-brand-400">
              <UploadCloud className="h-6 w-6" />
            </div>
            <p className="mt-3 text-sm font-medium text-slate-900 dark:text-white">
              {selectedFile ? (
                <span className="flex items-center gap-2 text-brand-600 dark:text-brand-400">
                  <FileText className="h-4 w-4" />
                  {selectedFile.name} ({(selectedFile.size / 1024 / 1024).toFixed(2)} MB)
                </span>
              ) : (
                'Haz clic o arrastra un archivo PDF de proceso aquí'
              )}
            </p>
            <p className="mt-1 text-xs text-slate-400 dark:text-white/40">
              Formatos soportados: PDF hasta 25 MB
            </p>
          </div>

          {/* Optional fields */}
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-white/50">
                Nombre del Proceso (Opcional)
              </label>
              <input
                type="text"
                value={processName}
                onChange={(e) => setProcessName(e.target.value)}
                placeholder="Ej. Proceso de Compras y Requisiciones"
                disabled={isUploading}
                className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/5 dark:text-white"
              />
              <p className="mt-1 text-[11px] text-slate-400 dark:text-white/40">
                Si se omite, se extraerá automáticamente el título del documento.
              </p>
            </div>

            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-white/50">
                Descripción del Proceso (Opcional)
              </label>
              <input
                type="text"
                value={processDescription}
                onChange={(e) => setProcessDescription(e.target.value)}
                placeholder="Ej. Procedimiento operativo para compras de bienes y servicios"
                disabled={isUploading}
                className="mt-1.5 w-full rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-sm text-slate-900 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 dark:border-white/10 dark:bg-white/5 dark:text-white"
              />
            </div>
          </div>

          {/* Error notice */}
          {uploadError && (
            <div className="flex items-center gap-2 rounded-xl bg-rose-50 p-3.5 text-sm text-rose-700 dark:bg-rose-500/10 dark:text-rose-400">
              <AlertTriangle className="h-4 w-4 shrink-0" />
              <span>{uploadError}</span>
            </div>
          )}

          {/* Action button */}
          <div className="flex justify-end">
            <button
              type="submit"
              disabled={!selectedFile || isUploading}
              className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 px-6 py-2.5 text-sm font-semibold text-white shadow-md shadow-brand-500/25 transition hover:from-brand-500 hover:to-indigo-500 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {isUploading ? (
                <>
                  <RefreshCw className="h-4 w-4 animate-spin" />
                  <span>Procesando PDF en Data Lake...</span>
                </>
              ) : (
                <>
                  <Sparkles className="h-4 w-4" />
                  <span>Subir y Extraer Estructura</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>

      {/* Results View */}
      {uploadResult && (
        <div className="space-y-6">
          {/* Status Banner */}
          <div className="rounded-2xl border border-emerald-200 bg-emerald-50/50 p-6 dark:border-emerald-500/20 dark:bg-emerald-500/5">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
              <div className="flex items-start gap-3">
                <CheckCircle2 className="mt-0.5 h-6 w-6 text-emerald-600 dark:text-emerald-400" />
                <div>
                  <h3 className="text-lg font-bold text-slate-900 dark:text-white">
                    {uploadResult.process_name || 'Documento Procesado Exitosamente'}
                  </h3>
                  <p className="text-xs text-slate-500 dark:text-white/60">
                    ID Documento: <code className="rounded bg-slate-200/60 px-1.5 py-0.5 font-mono text-slate-800 dark:bg-white/10 dark:text-white">{uploadResult.document_id}</code>
                  </p>
                </div>
              </div>
              <span className="inline-flex items-center gap-1.5 self-start rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300">
                Estatus: {uploadResult.status}
              </span>
            </div>

            {/* Storage paths */}
            <div className="mt-4 grid grid-cols-1 gap-3 text-xs md:grid-cols-2">
              <div className="rounded-xl bg-white/80 p-3 dark:bg-white/5">
                <p className="font-semibold text-slate-600 dark:text-white/60">Almacenamiento Origen (Data Lake):</p>
                <p className="mt-1 font-mono text-[11px] text-slate-800 truncate dark:text-white/80" title={uploadResult.source_storage_path}>
                  {uploadResult.source_storage_path}
                </p>
              </div>
              <div className="rounded-xl bg-white/80 p-3 dark:bg-white/5">
                <p className="font-semibold text-slate-600 dark:text-white/60">Resultado Estructurado (JSON):</p>
                <p className="mt-1 font-mono text-[11px] text-slate-800 truncate dark:text-white/80" title={uploadResult.structured_result_path}>
                  {uploadResult.structured_result_path}
                </p>
              </div>
            </div>
          </div>

          {/* Metrics summary cards */}
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
              <div className="flex items-center gap-2 text-brand-600 dark:text-brand-400">
                <Layers className="h-4 w-4" />
                <span className="text-xs font-semibold uppercase tracking-wider">Capítulos</span>
              </div>
              <p className="mt-2 text-2xl font-bold text-slate-900 dark:text-white">
                {uploadResult.chapters_identified}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
              <div className="flex items-center gap-2 text-indigo-600 dark:text-indigo-400">
                <ListTree className="h-4 w-4" />
                <span className="text-xs font-semibold uppercase tracking-wider">Pasos</span>
              </div>
              <p className="mt-2 text-2xl font-bold text-slate-900 dark:text-white">
                {uploadResult.steps_identified}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
              <div className="flex items-center gap-2 text-teal-600 dark:text-teal-400">
                <Table className="h-4 w-4" />
                <span className="text-xs font-semibold uppercase tracking-wider">Tablas</span>
              </div>
              <p className="mt-2 text-2xl font-bold text-slate-900 dark:text-white">
                {uploadResult.tables_extracted}
              </p>
            </div>

            <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-slate-900/40">
              <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400">
                <AlertTriangle className="h-4 w-4" />
                <span className="text-xs font-semibold uppercase tracking-wider">Alertas</span>
              </div>
              <p className="mt-2 text-2xl font-bold text-slate-900 dark:text-white">
                {uploadResult.warnings.length}
              </p>
            </div>
          </div>

          {/* Warnings List */}
          {uploadResult.warnings.length > 0 && (
            <div className="rounded-xl border border-amber-200 bg-amber-50/50 p-4 text-xs text-amber-800 dark:border-amber-500/20 dark:bg-amber-500/5 dark:text-amber-300">
              <p className="font-semibold">Advertencias durante la extracción:</p>
              <ul className="mt-1 list-inside list-disc space-y-1">
                {uploadResult.warnings.map((w, idx) => (
                  <li key={idx}>{w}</li>
                ))}
              </ul>
            </div>
          )}

          {/* Detailed Content Tabs */}
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-slate-900/40">
            <div className="flex border-b border-slate-200 px-4 dark:border-white/10">
              <button
                type="button"
                onClick={() => setActiveTab('steps')}
                className={`flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-medium transition ${
                  activeTab === 'steps'
                    ? 'border-brand-600 text-brand-600 dark:border-brand-400 dark:text-brand-400'
                    : 'border-transparent text-slate-500 hover:text-slate-800 dark:text-white/60 dark:hover:text-white'
                }`}
              >
                <ListTree className="h-4 w-4" />
                <span>Pasos del Proceso</span>
              </button>

              <button
                type="button"
                onClick={() => setActiveTab('tables')}
                className={`flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-medium transition ${
                  activeTab === 'tables'
                    ? 'border-brand-600 text-brand-600 dark:border-brand-400 dark:text-brand-400'
                    : 'border-transparent text-slate-500 hover:text-slate-800 dark:text-white/60 dark:hover:text-white'
                }`}
              >
                <Table className="h-4 w-4" />
                <span>Tablas Extraídas ({structure?.tables.length ?? uploadResult.tables_extracted})</span>
              </button>

              <button
                type="button"
                onClick={() => setActiveTab('json')}
                className={`flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-medium transition ${
                  activeTab === 'json'
                    ? 'border-brand-600 text-brand-600 dark:border-brand-400 dark:text-brand-400'
                    : 'border-transparent text-slate-500 hover:text-slate-800 dark:text-white/60 dark:hover:text-white'
                }`}
              >
                <Database className="h-4 w-4" />
                <span>JSON Estructurado</span>
              </button>
            </div>

            <div className="p-6">
              {isLoadingStructure && (
                <div className="flex items-center justify-center py-8">
                  <RefreshCw className="h-6 w-6 animate-spin text-brand-500" />
                </div>
              )}

              {/* Tab: Steps */}
              {activeTab === 'steps' && structure && (
                <div className="space-y-4">
                  {structure.chapters.map((ch) => {
                    const isExpanded = !!expandedChapters[ch.chapter_id];
                    return (
                      <div
                        key={ch.chapter_id}
                        className="rounded-xl border border-slate-200 bg-slate-50/50 overflow-hidden dark:border-white/10 dark:bg-white/5"
                      >
                        <button
                          type="button"
                          onClick={() => toggleChapter(ch.chapter_id)}
                          className="flex w-full items-center justify-between p-4 text-left font-semibold text-slate-900 dark:text-white hover:bg-slate-100/50 dark:hover:bg-white/5 transition"
                        >
                          <div className="flex items-center gap-3">
                            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-brand-100 text-xs font-bold text-brand-700 dark:bg-brand-500/20 dark:text-brand-300">
                              {ch.chapter_number || 'C'}
                            </span>
                            <span>{ch.title}</span>
                            <span className="text-xs font-normal text-slate-400 dark:text-white/40">
                              ({ch.steps.length} pasos)
                            </span>
                          </div>
                          {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                        </button>

                        {isExpanded && (
                          <div className="border-t border-slate-200 p-4 space-y-4 dark:border-white/10 bg-white dark:bg-slate-950/40">
                            {ch.steps.length === 0 ? (
                              <p className="text-xs italic text-slate-400 dark:text-white/40">
                                No se identificaron pasos específicos bajo este capítulo.
                              </p>
                            ) : (
                              ch.steps.map((st) => (
                                <div
                                  key={st.step_id}
                                  className="rounded-xl border border-slate-200 p-4 space-y-3 dark:border-white/10 dark:bg-white/5"
                                >
                                  <div className="flex items-center justify-between">
                                    <div className="flex items-center gap-2">
                                      <span className="rounded-md bg-indigo-50 px-2 py-0.5 text-xs font-bold text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-300">
                                        Paso {st.step_number}
                                      </span>
                                      <h4 className="text-sm font-semibold text-slate-900 dark:text-white">
                                        {st.step_name}
                                      </h4>
                                    </div>
                                    {st.source_pages.length > 0 && (
                                      <span className="rounded bg-slate-100 px-2 py-0.5 text-[11px] font-mono text-slate-500 dark:bg-white/10 dark:text-white/50">
                                        Pág. {st.source_pages.join(', ')}
                                      </span>
                                    )}
                                  </div>

                                  {st.objective && (
                                    <div className="text-xs text-slate-600 dark:text-white/70">
                                      <span className="font-semibold text-slate-800 dark:text-white">Objetivo: </span>
                                      {st.objective}
                                    </div>
                                  )}

                                  {st.required_activities.length > 0 && (
                                    <div className="text-xs">
                                      <span className="font-semibold text-slate-800 dark:text-white">Actividades Requeridas:</span>
                                      <ul className="mt-1 list-inside list-disc text-slate-600 dark:text-white/70">
                                        {st.required_activities.map((act, i) => (
                                          <li key={i}>{act}</li>
                                        ))}
                                      </ul>
                                    </div>
                                  )}

                                  {st.required_skills.length > 0 && (
                                    <div className="flex flex-wrap items-center gap-1.5 text-xs">
                                      <span className="font-semibold text-slate-800 dark:text-white">Habilidades:</span>
                                      {st.required_skills.map((sk, i) => (
                                        <span
                                          key={i}
                                          className="rounded-full bg-teal-50 px-2.5 py-0.5 text-[11px] font-medium text-teal-700 dark:bg-teal-500/10 dark:text-teal-300"
                                        >
                                          {sk}
                                        </span>
                                      ))}
                                    </div>
                                  )}

                                  {st.deliverables.length > 0 && (
                                    <div className="text-xs">
                                      <span className="font-semibold text-slate-800 dark:text-white">Entregables: </span>
                                      <span className="text-slate-600 dark:text-white/70">
                                        {st.deliverables.join(' • ')}
                                      </span>
                                    </div>
                                  )}

                                  {st.completion_condition && (
                                    <div className="rounded-lg bg-emerald-50/50 p-2.5 text-xs text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-300">
                                      <span className="font-semibold">Condición de Finalización: </span>
                                      {st.completion_condition}
                                    </div>
                                  )}

                                  {st.checklists.length > 0 && (
                                    <div className="space-y-1 text-xs">
                                      <span className="font-semibold text-slate-800 dark:text-white">Checklists / Criterios:</span>
                                      {st.checklists.map((chk, i) => (
                                        <div key={i} className="flex items-center gap-2 text-slate-600 dark:text-white/70">
                                          <CheckSquare className="h-3.5 w-3.5 text-brand-600 dark:text-brand-400" />
                                          <span>{chk}</span>
                                        </div>
                                      ))}
                                    </div>
                                  )}

                                  {/* Step 2 Analyze Button & Action */}
                                  <div className="pt-2 border-t border-slate-100 dark:border-white/5 flex items-center justify-between">
                                    <button
                                      type="button"
                                      onClick={() => handleAnalyzeStep(st.step_id)}
                                      disabled={analyzingStepId === st.step_id}
                                      className="inline-flex items-center gap-2 rounded-lg bg-indigo-50 px-3 py-1.5 text-xs font-semibold text-indigo-700 hover:bg-indigo-100 dark:bg-indigo-500/20 dark:text-indigo-300 dark:hover:bg-indigo-500/30 transition disabled:opacity-50"
                                    >
                                      {analyzingStepId === st.step_id ? (
                                        <>
                                          <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                                          <span>Analizando con GPT-5 mini...</span>
                                        </>
                                      ) : (
                                        <>
                                          <Sparkles className="h-3.5 w-3.5" />
                                          <span>Analizar Requerimientos (Paso 2)</span>
                                        </>
                                      )}
                                    </button>

                                    {stepAnalyses[st.step_id] && (
                                      <span className="text-[11px] font-medium text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
                                        <CheckCircle2 className="h-3.5 w-3.5" />
                                        Requerimientos analizados
                                      </span>
                                    )}
                                  </div>

                                  {/* Error during step analysis */}
                                  {analysisErrors[st.step_id] && (
                                    <div className="rounded-lg bg-rose-50 p-2.5 text-xs text-rose-700 dark:bg-rose-500/10 dark:text-rose-400 flex items-center gap-2">
                                      <AlertTriangle className="h-4 w-4 shrink-0" />
                                      <span>{analysisErrors[st.step_id]}</span>
                                    </div>
                                  )}

                                  {/* Step 2 Analysis Result Card */}
                                  {stepAnalyses[st.step_id] && (
                                    <div className="mt-3 rounded-xl border border-indigo-200 bg-indigo-50/40 p-4 space-y-4 dark:border-indigo-500/20 dark:bg-indigo-950/20">
                                      <div className="flex items-center justify-between">
                                        <div className="flex items-center gap-2 text-indigo-900 dark:text-indigo-200 font-semibold text-xs uppercase tracking-wider">
                                          <Sparkles className="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
                                          <span>Análisis de Requerimientos del Empleado (Paso 2)</span>
                                        </div>
                                      </div>

                                      {/* 1. Employee Must Be Able To */}
                                      {stepAnalyses[st.step_id].employeeMustBeAbleTo.length > 0 && (
                                        <div className="space-y-1 text-xs">
                                          <p className="font-semibold text-slate-900 dark:text-white">
                                            El empleado debe ser capaz de:
                                          </p>
                                          <ul className="list-inside list-disc space-y-1 text-slate-700 dark:text-white/80">
                                            {stepAnalyses[st.step_id].employeeMustBeAbleTo.map((item, idx) => (
                                              <li key={idx}>{item}</li>
                                            ))}
                                          </ul>
                                        </div>
                                      )}

                                      {/* 2, 3, 4. Skill Areas */}
                                      <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
                                        {/* Soft Skills */}
                                        <div className="rounded-lg bg-white/80 p-3 dark:bg-white/5 space-y-1">
                                          <p className="font-semibold text-violet-700 dark:text-violet-300">Soft Skills:</p>
                                          <ul className="space-y-1 text-slate-600 dark:text-white/70">
                                            {stepAnalyses[st.step_id].requiredSkillAreas.softSkills.map((sk, idx) => (
                                              <li key={idx}>
                                                <span className="font-medium text-slate-800 dark:text-white">{sk.name}: </span>
                                                {sk.reason}
                                              </li>
                                            ))}
                                          </ul>
                                        </div>

                                        {/* Business Skills */}
                                        <div className="rounded-lg bg-white/80 p-3 dark:bg-white/5 space-y-1">
                                          <p className="font-semibold text-amber-700 dark:text-amber-300">Habilidades de Negocio:</p>
                                          <ul className="space-y-1 text-slate-600 dark:text-white/70">
                                            {stepAnalyses[st.step_id].requiredSkillAreas.businessSkills.map((sk, idx) => (
                                              <li key={idx}>
                                                <span className="font-medium text-slate-800 dark:text-white">{sk.name}: </span>
                                                {sk.reason}
                                              </li>
                                            ))}
                                          </ul>
                                        </div>

                                        {/* Technical Skills */}
                                        <div className="rounded-lg bg-white/80 p-3 dark:bg-white/5 space-y-1">
                                          <p className="font-semibold text-teal-700 dark:text-teal-300">Habilidades Técnicas:</p>
                                          <ul className="space-y-1 text-slate-600 dark:text-white/70">
                                            {stepAnalyses[st.step_id].requiredSkillAreas.technicalSkills.map((sk, idx) => (
                                              <li key={idx}>
                                                <span className="font-medium text-slate-800 dark:text-white">{sk.name}: </span>
                                                {sk.reason}
                                              </li>
                                            ))}
                                          </ul>
                                        </div>
                                      </div>

                                      {/* 5. Required Tools & Methods */}
                                      {stepAnalyses[st.step_id].requiredTools.length > 0 && (
                                        <div className="space-y-1 text-xs">
                                          <p className="font-semibold text-slate-900 dark:text-white">
                                            Herramientas y Métodos de Trabajo:
                                          </p>
                                          <div className="flex flex-wrap gap-2">
                                            {stepAnalyses[st.step_id].requiredTools.map((tool, idx) => (
                                              <div
                                                key={idx}
                                                className="rounded-lg bg-white/80 px-2.5 py-1 text-slate-700 dark:bg-white/5 dark:text-white/80 border border-slate-200/60 dark:border-white/10"
                                              >
                                                <span className="font-semibold text-slate-900 dark:text-white">{tool.name}</span>
                                                {tool.useInStep && <span className="text-[11px] text-slate-500 dark:text-white/60"> ({tool.useInStep})</span>}
                                              </div>
                                            ))}
                                          </div>
                                        </div>
                                      )}

                                      {/* 6. Completion Evidence */}
                                      {stepAnalyses[st.step_id].completionEvidence.length > 0 && (
                                        <div className="space-y-1 text-xs">
                                          <p className="font-semibold text-slate-900 dark:text-white">
                                            Evidencia de Finalización:
                                          </p>
                                          <div className="flex flex-wrap gap-1.5">
                                            {stepAnalyses[st.step_id].completionEvidence.map((ev, idx) => (
                                              <span
                                                key={idx}
                                                className="rounded-full bg-emerald-100/80 px-2.5 py-0.5 text-[11px] font-medium text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300"
                                              >
                                                {ev}
                                              </span>
                                            ))}
                                          </div>
                                        </div>
                                      )}

                                      {/* 7. Assumptions & Warnings */}
                                      {stepAnalyses[st.step_id].assumptions.length > 0 && (
                                        <div className="text-xs text-slate-600 dark:text-white/70">
                                          <span className="font-semibold text-slate-800 dark:text-white">Supuestos: </span>
                                          {stepAnalyses[st.step_id].assumptions.join(' • ')}
                                        </div>
                                      )}

                                      {stepAnalyses[st.step_id].warnings.length > 0 && (
                                        <div className="rounded-lg bg-amber-50 p-2 text-xs text-amber-800 dark:bg-amber-500/10 dark:text-amber-300">
                                          <span className="font-semibold">Advertencias: </span>
                                          {stepAnalyses[st.step_id].warnings.join(' • ')}
                                        </div>
                                      )}

                                      {/* Action to Search Candidate Classes in Engram Studio (Step 3) */}
                                      <div className="pt-3 border-t border-indigo-200/60 dark:border-white/10 flex items-center justify-between">
                                        <button
                                          type="button"
                                          onClick={() => handleSearchCandidates(st.step_id)}
                                          disabled={searchingCandidatesStepId === st.step_id}
                                          className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-violet-600 to-indigo-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:from-violet-500 hover:to-indigo-500 transition disabled:opacity-50"
                                        >
                                          {searchingCandidatesStepId === st.step_id ? (
                                            <>
                                              <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                                              <span>Buscando en Engram Studio...</span>
                                            </>
                                          ) : (
                                            <>
                                              <GraduationCap className="h-3.5 w-3.5" />
                                              <span>Buscar Clases Candidatas (Paso 3)</span>
                                            </>
                                          )}
                                        </button>

                                        {stepCandidateClasses[st.step_id] && (
                                          <span className="text-[11px] font-medium text-violet-700 dark:text-violet-300 flex items-center gap-1">
                                            <CheckCircle2 className="h-3.5 w-3.5" />
                                            {stepCandidateClasses[st.step_id].candidateClasses.length} clases candidatas identificadas
                                          </span>
                                        )}
                                      </div>

                                      {/* Search Candidate Classes Error */}
                                      {searchCandidateErrors[st.step_id] && (
                                        <div className="rounded-lg bg-rose-50 p-2.5 text-xs text-rose-700 dark:bg-rose-500/10 dark:text-rose-400 flex items-center gap-2">
                                          <AlertTriangle className="h-4 w-4 shrink-0" />
                                          <span>{searchCandidateErrors[st.step_id]}</span>
                                        </div>
                                      )}

                                      {/* Step 3 Candidate Classes Display */}
                                      {stepCandidateClasses[st.step_id] && (
                                        <div className="rounded-xl border border-violet-200 bg-white/90 p-4 space-y-4 dark:border-violet-500/20 dark:bg-slate-900/60">
                                          <div className="flex items-center justify-between">
                                            <div className="flex items-center gap-2 text-violet-900 dark:text-violet-200 font-semibold text-xs uppercase tracking-wider">
                                              <BookOpen className="h-4 w-4 text-violet-600 dark:text-violet-400" />
                                              <span>Clases Candidatas en Engram Studio (Paso 3)</span>
                                            </div>
                                            <span className="text-[11px] font-semibold text-slate-500 dark:text-white/60">
                                              Top {stepCandidateClasses[st.step_id].candidateClasses.length} Clases
                                            </span>
                                          </div>

                                          {stepCandidateClasses[st.step_id].candidateClasses.length === 0 ? (
                                            <p className="text-xs italic text-slate-500 dark:text-white/50">
                                              No se encontraron clases publicadas relevantes en el catálogo para estos requerimientos.
                                            </p>
                                          ) : (
                                            <div className="space-y-3">
                                              {stepCandidateClasses[st.step_id].candidateClasses.map((cls) => (
                                                <div
                                                  key={cls.classId}
                                                  className="rounded-lg border border-slate-200 p-3.5 space-y-2 dark:border-white/10 dark:bg-white/5"
                                                >
                                                  <div className="flex items-start justify-between gap-2">
                                                    <div>
                                                      <span className="inline-block rounded bg-violet-100 px-2 py-0.5 text-[10px] font-bold text-violet-800 dark:bg-violet-500/20 dark:text-violet-300">
                                                        Learning Path: {cls.learningPathName}
                                                      </span>
                                                      <h5 className="mt-1 text-sm font-bold text-slate-900 dark:text-white">
                                                        {cls.className}
                                                      </h5>
                                                    </div>
                                                    <span className="rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-bold text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300 shrink-0">
                                                      Score: {(cls.relevanceScore * 100).toFixed(0)}%
                                                    </span>
                                                  </div>

                                                  <p className="text-xs text-slate-600 dark:text-white/70">
                                                    {cls.classDescription}
                                                  </p>

                                                  {cls.matchedAbilities.length > 0 && (
                                                    <div className="text-xs">
                                                      <span className="font-semibold text-slate-800 dark:text-white">Habilidades que cubre: </span>
                                                      <span className="text-slate-600 dark:text-white/70">
                                                        {cls.matchedAbilities.join(' • ')}
                                                      </span>
                                                    </div>
                                                  )}

                                                  {cls.matchedSkillRequirements.length > 0 && (
                                                    <div className="flex flex-wrap items-center gap-1.5 text-xs">
                                                      <span className="font-semibold text-slate-800 dark:text-white">Requerimientos cubiertos:</span>
                                                      {cls.matchedSkillRequirements.map((reqItem, rIdx) => (
                                                        <span
                                                          key={rIdx}
                                                          className="rounded-md bg-slate-100 px-2 py-0.5 text-[10px] font-medium text-slate-700 dark:bg-white/10 dark:text-white/80"
                                                        >
                                                          [{reqItem.category}] {reqItem.name}
                                                        </span>
                                                      ))}
                                                    </div>
                                                  )}

                                                  <div className="rounded-md bg-slate-50 p-2 text-xs text-slate-700 dark:bg-white/5 dark:text-white/80">
                                                    <span className="font-semibold">Razón de relevancia: </span>
                                                    {cls.relevanceReason}
                                                  </div>

                                                  {cls.limitations.length > 0 && (
                                                    <div className="text-[11px] text-amber-700 dark:text-amber-300">
                                                      <span className="font-semibold">Limitaciones: </span>
                                                      {cls.limitations.join(' • ')}
                                                    </div>
                                                  )}
                                                </div>
                                              ))}
                                            </div>
                                          )}

                                          {/* Unmatched requirements */}
                                          {stepCandidateClasses[st.step_id].requirementsWithoutCandidateClass.length > 0 && (
                                            <div className="rounded-lg bg-amber-50/70 p-3 text-xs text-amber-900 dark:bg-amber-500/10 dark:text-amber-200 space-y-1">
                                              <p className="font-semibold">
                                                Requerimientos sin Clase Candidata en el Catálogo:
                                              </p>
                                              <ul className="list-inside list-disc text-amber-800 dark:text-amber-300/80">
                                                {stepCandidateClasses[st.step_id].requirementsWithoutCandidateClass.map((unmatched, uIdx) => (
                                                  <li key={uIdx}>{unmatched}</li>
                                                ))}
                                              </ul>
                                            </div>
                                          )}

                                          {/* Step 3 Warnings */}
                                          {stepCandidateClasses[st.step_id].warnings.length > 0 && (
                                            <div className="rounded-lg bg-slate-100 p-2.5 text-[11px] text-slate-600 dark:bg-white/5 dark:text-white/60">
                                              {stepCandidateClasses[st.step_id].warnings.join(' • ')}
                                            </div>
                                          )}

                                          {/* Step 4 Action: Select Relevant Engrams */}
                                          <div className="pt-3 border-t border-violet-200/60 dark:border-white/10 flex items-center justify-between">
                                            <button
                                              type="button"
                                              onClick={() => handleSelectEngrams(st.step_id)}
                                              disabled={selectingEngramsStepId === st.step_id}
                                              className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-teal-600 to-emerald-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:from-teal-500 hover:to-emerald-500 transition disabled:opacity-50"
                                            >
                                              {selectingEngramsStepId === st.step_id ? (
                                                <>
                                                  <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                                                  <span>Seleccionando Engramas (GPT-5 mini)...</span>
                                                </>
                                              ) : (
                                                <>
                                                  <Layers className="h-3.5 w-3.5" />
                                                  <span>Seleccionar Engramas Relevantes (Paso 4)</span>
                                                </>
                                              )}
                                            </button>

                                            {stepRelevantEngrams[st.step_id] && (
                                              <span className="text-[11px] font-medium text-teal-700 dark:text-teal-300 flex items-center gap-1">
                                                <CheckCircle2 className="h-3.5 w-3.5" />
                                                {stepRelevantEngrams[st.step_id].selectedClasses.reduce((acc, c) => acc + c.selectedEngrams.length, 0)} Engramas seleccionados
                                              </span>
                                            )}
                                          </div>

                                          {/* Step 4 Error */}
                                          {selectEngramErrors[st.step_id] && (
                                            <div className="rounded-lg bg-rose-50 p-2.5 text-xs text-rose-700 dark:bg-rose-500/10 dark:text-rose-400 flex items-center gap-2">
                                              <AlertTriangle className="h-4 w-4 shrink-0" />
                                              <span>{selectEngramErrors[st.step_id]}</span>
                                            </div>
                                          )}

                                          {/* Step 4 Display */}
                                          {stepRelevantEngrams[st.step_id] && (
                                            <div className="rounded-xl border border-teal-200 bg-teal-50/30 p-4 space-y-4 dark:border-teal-500/20 dark:bg-slate-900/60">
                                              <div className="flex items-center justify-between">
                                                <div className="flex items-center gap-2 text-teal-900 dark:text-teal-200 font-semibold text-xs uppercase tracking-wider">
                                                  <Layers className="h-4 w-4 text-teal-600 dark:text-teal-400" />
                                                  <span>Engramas Relevantes Seleccionados (Paso 4)</span>
                                                </div>
                                              </div>

                                              {/* Classes and their selected Engrams */}
                                              <div className="space-y-4">
                                                {stepRelevantEngrams[st.step_id].selectedClasses.map((sc) => (
                                                  <div
                                                    key={sc.classId}
                                                    className="rounded-lg border border-teal-200/80 bg-white p-3.5 space-y-2 dark:border-white/10 dark:bg-white/5"
                                                  >
                                                    <div className="flex items-baseline justify-between gap-2 border-b border-slate-100 pb-2 dark:border-white/5">
                                                      <div>
                                                        <span className="rounded bg-teal-100 px-2 py-0.5 text-[10px] font-bold text-teal-800 dark:bg-teal-500/20 dark:text-teal-300">
                                                          {sc.learningPathName}
                                                        </span>
                                                        <h6 className="mt-1 text-xs font-bold text-slate-900 dark:text-white">
                                                          Clase: {sc.className}
                                                        </h6>
                                                      </div>
                                                      <span className="text-[11px] font-semibold text-slate-500 dark:text-white/60">
                                                        {sc.selectedEngrams.length} Engramas
                                                      </span>
                                                    </div>

                                                    <div className="space-y-2 pt-1">
                                                      {sc.selectedEngrams.map((eng) => (
                                                        <div
                                                          key={eng.engramId}
                                                          className="rounded-md border border-slate-200/60 p-2.5 text-xs space-y-1.5 dark:border-white/5 dark:bg-slate-950/40"
                                                        >
                                                          <div className="flex items-center justify-between gap-2">
                                                            <div className="flex items-center gap-2">
                                                              <span className="font-semibold text-slate-900 dark:text-white">
                                                                {eng.engramName}
                                                              </span>
                                                              <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                                                                eng.criticality === 'critical'
                                                                  ? 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                                  : 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300'
                                                              }`}>
                                                                {eng.criticality === 'critical' ? 'Crítico' : 'De Apoyo'}
                                                              </span>
                                                            </div>
                                                            <span className="text-[11px] font-bold text-teal-700 dark:text-teal-300">
                                                              {(eng.relevanceScore * 100).toFixed(0)}%
                                                            </span>
                                                          </div>

                                                          {eng.engramDescription && (
                                                            <p className="text-[11px] text-slate-600 dark:text-white/70">
                                                              {eng.engramDescription}
                                                            </p>
                                                          )}

                                                          <div className="rounded bg-slate-50 p-1.5 text-[11px] text-slate-700 dark:bg-white/5 dark:text-white/80">
                                                            <span className="font-semibold">Relevancia: </span>
                                                            {eng.relevanceReason}
                                                          </div>

                                                          {eng.supportedRequirements.length > 0 && (
                                                            <div className="flex flex-wrap gap-1 text-[10px]">
                                                              <span className="font-semibold text-slate-700 dark:text-white/70">Cubre:</span>
                                                              {eng.supportedRequirements.map((r, ri) => (
                                                                <span key={ri} className="rounded bg-slate-100 px-1.5 py-0.2 text-slate-600 dark:bg-white/10 dark:text-white/70">
                                                                  {r}
                                                                </span>
                                                              ))}
                                                            </div>
                                                          )}
                                                        </div>
                                                      ))}
                                                    </div>
                                                  </div>
                                                ))}
                                              </div>

                                              {/* Requirement Coverage Summary */}
                                              {stepRelevantEngrams[st.step_id].requirementCoverage.length > 0 && (
                                                <div className="space-y-2 pt-2 border-t border-teal-200/60 dark:border-white/10 text-xs">
                                                  <p className="font-semibold text-slate-900 dark:text-white">
                                                    Análisis de Cobertura de Requerimientos (Provisional):
                                                  </p>
                                                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                                                    {stepRelevantEngrams[st.step_id].requirementCoverage.map((cov, ci) => (
                                                      <div
                                                        key={ci}
                                                        className="rounded-lg border border-slate-200/80 p-2.5 space-y-1 dark:border-white/5 bg-white/70 dark:bg-white/5"
                                                      >
                                                        <div className="flex items-center justify-between">
                                                          <span className="font-medium text-slate-800 dark:text-white">
                                                            {cov.requirement}
                                                          </span>
                                                          <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                                                            cov.coverageStatus === 'covered'
                                                              ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                                                              : cov.coverageStatus === 'partially_covered'
                                                              ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                                                              : 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                          }`}>
                                                            {cov.coverageStatus === 'covered' ? 'Cubierto' : cov.coverageStatus === 'partially_covered' ? 'Parcial' : 'No Cubierto'}
                                                          </span>
                                                        </div>
                                                        {cov.notes && (
                                                          <p className="text-[11px] text-slate-500 dark:text-white/60">
                                                            {cov.notes}
                                                          </p>
                                                        )}
                                                      </div>
                                                    ))}
                                                  </div>
                                                </div>
                                              )}

                                              {/* Uncovered Requirements */}
                                              {stepRelevantEngrams[st.step_id].uncoveredRequirements.length > 0 && (
                                                <div className="rounded-lg bg-rose-50/70 p-3 text-xs text-rose-900 dark:bg-rose-500/10 dark:text-rose-200 space-y-1">
                                                  <p className="font-semibold">
                                                    Requerimientos No Cubiertos (Candidatos a Brecha de Contenido):
                                                  </p>
                                                  <ul className="list-inside list-disc text-rose-800 dark:text-rose-300/80">
                                                    {stepRelevantEngrams[st.step_id].uncoveredRequirements.map((ur, uri) => (
                                                      <li key={uri}>{ur}</li>
                                                    ))}
                                                  </ul>
                                                </div>
                                              )}

                                              {/* Step 5 Action: Determine Learning Content Coverage and Gaps */}
                                              <div className="pt-3 border-t border-teal-200/60 dark:border-white/10 flex items-center justify-between">
                                                <button
                                                  type="button"
                                                  onClick={() => handleAnalyzeCoverage(st.step_id)}
                                                  disabled={analyzingCoverageStepId === st.step_id}
                                                  className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-amber-600 to-rose-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:from-amber-500 hover:to-rose-500 transition disabled:opacity-50"
                                                >
                                                  {analyzingCoverageStepId === st.step_id ? (
                                                    <>
                                                      <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                                                      <span>Determinando Cobertura y Brechas...</span>
                                                    </>
                                                  ) : (
                                                    <>
                                                      <Target className="h-3.5 w-3.5" />
                                                      <span>Evaluar Cobertura y Brechas (Paso 5)</span>
                                                    </>
                                                  )}
                                                </button>

                                                {stepCoverages[st.step_id] && (
                                                  <span className={`text-[11px] font-bold px-2.5 py-0.5 rounded-full flex items-center gap-1 ${
                                                    stepCoverages[st.step_id].overallCoverageStatus === 'covered'
                                                      ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                                                      : stepCoverages[st.step_id].overallCoverageStatus === 'partially_covered'
                                                      ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                                                      : 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                  }`}>
                                                    <CheckCircle2 className="h-3.5 w-3.5" />
                                                    {stepCoverages[st.step_id].overallCoverageStatus === 'covered'
                                                      ? 'Cubierto'
                                                      : stepCoverages[st.step_id].overallCoverageStatus === 'partially_covered'
                                                      ? 'Parcialmente Cubierto'
                                                      : 'Brecha de Contenido'}
                                                  </span>
                                                )}
                                              </div>

                                              {/* Step 5 Error */}
                                              {coverageErrors[st.step_id] && (
                                                <div className="rounded-lg bg-rose-50 p-2.5 text-xs text-rose-700 dark:bg-rose-500/10 dark:text-rose-400 flex items-center gap-2">
                                                  <AlertTriangle className="h-4 w-4 shrink-0" />
                                                  <span>{coverageErrors[st.step_id]}</span>
                                                </div>
                                              )}

                                              {/* Step 5 Display */}
                                              {stepCoverages[st.step_id] && (
                                                <div className="rounded-xl border border-amber-200 bg-amber-50/20 p-4 space-y-4 dark:border-amber-500/20 dark:bg-slate-900/60">
                                                  <div className="flex items-center justify-between border-b border-amber-100 pb-2 dark:border-white/5">
                                                    <div className="flex items-center gap-2 text-amber-900 dark:text-amber-200 font-semibold text-xs uppercase tracking-wider">
                                                      <Target className="h-4 w-4 text-amber-600 dark:text-amber-400" />
                                                      <span>Evaluación de Cobertura y Brechas de Aprendizaje (Paso 5)</span>
                                                    </div>
                                                    <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold uppercase tracking-wider ${
                                                      stepCoverages[st.step_id].overallCoverageStatus === 'covered'
                                                        ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                                                        : stepCoverages[st.step_id].overallCoverageStatus === 'partially_covered'
                                                        ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                                                        : 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                    }`}>
                                                      {stepCoverages[st.step_id].overallCoverageStatus}
                                                    </span>
                                                  </div>

                                                  {/* Summary text */}
                                                  <div className="text-xs text-slate-700 dark:text-white/80 bg-white/80 dark:bg-white/5 p-3 rounded-lg border border-slate-200/60 dark:border-white/5">
                                                    <span className="font-semibold text-slate-900 dark:text-white">Resumen de Cobertura: </span>
                                                    {stepCoverages[st.step_id].coverageSummary}
                                                  </div>

                                                  {/* Covered Requirements */}
                                                  {stepCoverages[st.step_id].coveredRequirements.length > 0 && (
                                                    <div className="space-y-1.5 text-xs">
                                                      <p className="font-semibold text-emerald-800 dark:text-emerald-300 flex items-center gap-1.5">
                                                        <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600 dark:text-emerald-400" />
                                                        Requerimientos Cubiertos ({stepCoverages[st.step_id].coveredRequirements.length}):
                                                      </p>
                                                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                                                        {stepCoverages[st.step_id].coveredRequirements.map((cov, cIdx) => (
                                                          <div
                                                            key={cIdx}
                                                            className="rounded-lg border border-emerald-200/80 bg-white/90 p-2.5 dark:border-white/5 dark:bg-white/5 space-y-1"
                                                          >
                                                            <div className="flex items-center justify-between">
                                                              <span className="font-medium text-slate-800 dark:text-white">
                                                                {cov.requirement}
                                                              </span>
                                                              <span className="rounded bg-emerald-50 px-1.5 py-0.5 text-[10px] font-bold text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300">
                                                                {cov.criticality === 'critical' ? 'Crítico' : 'De Apoyo'}
                                                              </span>
                                                            </div>
                                                            {cov.supportingEngramIds.length > 0 && (
                                                              <p className="text-[10px] text-slate-500 dark:text-white/50">
                                                                Engramas: {cov.supportingEngramIds.join(', ')}
                                                              </p>
                                                            )}
                                                          </div>
                                                        ))}
                                                      </div>
                                                    </div>
                                                  )}

                                                  {/* Content Gaps */}
                                                  {stepCoverages[st.step_id].contentGaps.length > 0 && (
                                                    <div className="space-y-2 text-xs pt-2 border-t border-amber-200/60 dark:border-white/10">
                                                      <p className="font-semibold text-rose-800 dark:text-rose-300 flex items-center gap-1.5">
                                                        <ShieldAlert className="h-3.5 w-3.5 text-rose-600 dark:text-rose-400" />
                                                        Brechas de Contenido Identificadas ({stepCoverages[st.step_id].contentGaps.length}):
                                                      </p>
                                                      <div className="space-y-2">
                                                        {stepCoverages[st.step_id].contentGaps.map((gap, gIdx) => (
                                                          <div
                                                            key={gIdx}
                                                            className="rounded-lg border border-rose-200 bg-white/90 p-3 dark:border-rose-500/20 dark:bg-white/5 space-y-1.5"
                                                          >
                                                            <div className="flex items-start justify-between gap-2">
                                                              <div>
                                                                <span className="font-bold text-slate-900 dark:text-white">
                                                                  {gap.requirement}
                                                                </span>
                                                                <span className="ml-2 text-[10px] uppercase font-semibold text-slate-400 dark:text-white/40">
                                                                  [{gap.requirementType}]
                                                                </span>
                                                              </div>
                                                              <div className="flex items-center gap-1.5 shrink-0">
                                                                <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold ${
                                                                  gap.criticality === 'critical'
                                                                    ? 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                                    : 'bg-slate-100 text-slate-700 dark:bg-white/10 dark:text-white/70'
                                                                }`}>
                                                                  {gap.criticality === 'critical' ? 'Crítico' : 'De Apoyo'}
                                                                </span>
                                                                <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-bold text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
                                                                  {gap.currentCoverage === 'partially_covered' ? 'Parcial' : 'No Cubierto'}
                                                                </span>
                                                              </div>
                                                            </div>

                                                            <p className="text-[11px] text-slate-600 dark:text-white/70">
                                                              <span className="font-semibold text-slate-700 dark:text-white/80">Motivo de la brecha: </span>
                                                              {gap.gapReason}
                                                            </p>

                                                            <div className="rounded bg-slate-50 p-2 text-[11px] space-y-1 dark:bg-slate-950/40 border border-slate-200/40 dark:border-white/5">
                                                              <div className="flex items-center gap-1.5">
                                                                <span className="font-semibold text-slate-800 dark:text-white">Recomendación: </span>
                                                                <span className="font-medium text-indigo-700 dark:text-indigo-300">
                                                                  {gap.recommendedGapType === 'new_engram_in_existing_class'
                                                                    ? 'Agregar nuevo Engrama en Clase existente'
                                                                    : gap.recommendedGapType === 'new_class_required'
                                                                    ? 'Requiere diseñar una Nueva Clase'
                                                                    : 'Ninguna'}
                                                                </span>
                                                              </div>
                                                              {gap.recommendedGapType === 'new_engram_in_existing_class' && gap.existingClassId && (
                                                                <p className="text-[10px] font-mono text-slate-500 dark:text-white/50">
                                                                  Clase Destino: {gap.existingClassId} (Learning Path: {gap.existingLearningPathId})
                                                                </p>
                                                              )}
                                                            </div>
                                                          </div>
                                                        ))}
                                                      </div>
                                                    </div>
                                                  )}

                                                  {/* Step 6 Action: Create Content Gap Drafts */}
                                                  <div className="pt-3 border-t border-amber-200/60 dark:border-white/10 flex items-center justify-between">
                                                    <button
                                                      type="button"
                                                      onClick={() => handleGenerateDraft(st.step_id)}
                                                      disabled={generatingDraftStepId === st.step_id}
                                                      className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-rose-600 to-pink-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:from-rose-500 hover:to-pink-500 transition disabled:opacity-50"
                                                    >
                                                      {generatingDraftStepId === st.step_id ? (
                                                        <>
                                                          <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                                                          <span>Generando Borrador de Brechas (GPT-5 mini)...</span>
                                                        </>
                                                      ) : (
                                                        <>
                                                          <FileEdit className="h-3.5 w-3.5" />
                                                          <span>Generar Propuesta de Brechas (Paso 6)</span>
                                                        </>
                                                      )}
                                                    </button>

                                                    {stepGapDrafts[st.step_id] && (
                                                      <span className="text-[11px] font-bold text-rose-700 dark:text-rose-300 flex items-center gap-1">
                                                        <CheckCircle2 className="h-3.5 w-3.5" />
                                                        {stepGapDrafts[st.step_id].draftCreated
                                                          ? `${stepGapDrafts[st.step_id].engramDrafts.length} Engramas y ${stepGapDrafts[st.step_id].classDrafts.length} Clases propuestas`
                                                          : 'Sin brechas (Completamente cubierto)'}
                                                      </span>
                                                    )}
                                                  </div>

                                                  {/* Step 6 Error */}
                                                  {draftErrors[st.step_id] && (
                                                    <div className="rounded-lg bg-rose-50 p-2.5 text-xs text-rose-700 dark:bg-rose-500/10 dark:text-rose-400 flex items-center gap-2">
                                                      <AlertTriangle className="h-4 w-4 shrink-0" />
                                                      <span>{draftErrors[st.step_id]}</span>
                                                    </div>
                                                  )}

                                                  {/* Step 6 Display */}
                                                  {stepGapDrafts[st.step_id] && (
                                                    <div className="rounded-xl border border-rose-200 bg-rose-50/20 p-4 space-y-4 dark:border-rose-500/20 dark:bg-slate-900/60">
                                                      <div className="flex items-center justify-between border-b border-rose-100 pb-2 dark:border-white/5">
                                                        <div className="flex items-center gap-2 text-rose-900 dark:text-rose-200 font-semibold text-xs uppercase tracking-wider">
                                                          <FileEdit className="h-4 w-4 text-rose-600 dark:text-rose-400" />
                                                          <span>Borrador de Brechas de Contenido (Paso 6)</span>
                                                        </div>
                                                        <span className="rounded-full bg-amber-100 px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-amber-800 dark:bg-amber-500/20 dark:text-amber-300">
                                                          Needs Expert Review
                                                        </span>
                                                      </div>

                                                      {/* Outcome 1: Covered */}
                                                      {!stepGapDrafts[st.step_id].draftCreated && (
                                                        <div className="rounded-lg bg-emerald-50 p-3 text-xs text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-300">
                                                          <p className="font-semibold">Cobertura Completa</p>
                                                          <p className="mt-0.5 text-[11px]">
                                                            El catálogo actual proporciona suficiente cobertura para los requerimientos de este paso. No se requiere generar borradores.
                                                          </p>
                                                        </div>
                                                      )}

                                                      {/* Outcome 2: Engram Drafts inside existing class */}
                                                      {stepGapDrafts[st.step_id].engramDrafts.length > 0 && (
                                                        <div className="space-y-2 text-xs">
                                                          <p className="font-semibold text-slate-900 dark:text-white">
                                                            Engramas Propuestos para Clases Existentes:
                                                          </p>
                                                          <div className="space-y-2">
                                                            {stepGapDrafts[st.step_id].engramDrafts.map((ed, edi) => (
                                                              <div
                                                                key={edi}
                                                                className="rounded-lg border border-slate-200 bg-white/90 p-3 dark:border-white/10 dark:bg-white/5 space-y-1.5"
                                                              >
                                                                <div className="flex items-start justify-between gap-2">
                                                                  <div>
                                                                    <span className="font-mono text-[10px] text-slate-400">
                                                                      Ref: {ed.draftReference}
                                                                    </span>
                                                                    <h6 className="text-xs font-bold text-slate-900 dark:text-white">
                                                                      {ed.proposedEngramTitle}
                                                                    </h6>
                                                                    <p className="text-[10px] text-slate-500 dark:text-white/50">
                                                                      En Clase: {ed.parentClassName} ({ed.parentLearningPathName})
                                                                    </p>
                                                                  </div>
                                                                  <span className="rounded bg-amber-50 px-2 py-0.5 text-[10px] font-bold text-amber-700 dark:bg-amber-500/20 dark:text-amber-300">
                                                                    {ed.status}
                                                                  </span>
                                                                </div>

                                                                <p className="text-[11px] text-slate-600 dark:text-white/70">
                                                                  {ed.proposedEngramDescription}
                                                                </p>

                                                                <div className="rounded bg-slate-50 p-2 text-[10px] space-y-0.5 dark:bg-slate-950/40">
                                                                  <p><span className="font-semibold">Capacidad: </span>{ed.capabilitySupported}</p>
                                                                  <p><span className="font-semibold">Requerimiento que cubre: </span>{ed.workStepRequirementSupported}</p>
                                                                  <p><span className="font-semibold">Razón necesaria: </span>{ed.reasonNeeded}</p>
                                                                </div>
                                                              </div>
                                                            ))}
                                                          </div>
                                                        </div>
                                                      )}

                                                      {/* Outcome 3: Class Drafts with Engram index */}
                                                      {stepGapDrafts[st.step_id].classDrafts.length > 0 && (
                                                        <div className="space-y-3 text-xs pt-2 border-t border-rose-100 dark:border-white/5">
                                                          <p className="font-semibold text-slate-900 dark:text-white">
                                                            Nuevas Clases Propuestas:
                                                          </p>
                                                          <div className="space-y-3">
                                                            {stepGapDrafts[st.step_id].classDrafts.map((cd, cdi) => (
                                                              <div
                                                                key={cdi}
                                                                className="rounded-xl border border-rose-200/80 bg-white/95 p-3.5 dark:border-white/10 dark:bg-white/5 space-y-2.5"
                                                              >
                                                                <div className="flex items-start justify-between gap-2">
                                                                  <div>
                                                                    <span className="font-mono text-[10px] text-slate-400">
                                                                      Ref: {cd.draftReference}
                                                                    </span>
                                                                    <h5 className="text-sm font-bold text-slate-900 dark:text-white">
                                                                      {cd.proposedClassTitle}
                                                                    </h5>
                                                                    <p className="text-[10px] text-indigo-700 dark:text-indigo-300">
                                                                      {cd.recommendedExistingLearningPathName
                                                                        ? `Ruta Recomendada: ${cd.recommendedExistingLearningPathName}`
                                                                        : `Nueva Ruta Propuesta: ${cd.proposedLearningPathName}`}
                                                                    </p>
                                                                  </div>
                                                                  <span className="rounded bg-amber-50 px-2 py-0.5 text-[10px] font-bold text-amber-700 dark:bg-amber-500/20 dark:text-amber-300">
                                                                    {cd.status}
                                                                  </span>
                                                                </div>

                                                                <p className="text-[11px] text-slate-600 dark:text-white/70">
                                                                  {cd.proposedClassDescription}
                                                                </p>

                                                                <div className="rounded bg-slate-50 p-2 text-[10px] space-y-0.5 dark:bg-slate-950/40">
                                                                  <p><span className="font-semibold">Capacidad a Desarrollar: </span>{cd.capabilityDeveloped}</p>
                                                                  <p><span className="font-semibold">Por qué se requiere nueva Clase: </span>{cd.reasonNewClassIsRequired}</p>
                                                                  <p><span className="font-semibold">Requerimientos cubiertos: </span>{cd.workStepRequirementsSupported.join(' • ')}</p>
                                                                </div>

                                                                {/* Proposed Engram Index */}
                                                                {cd.proposedEngramIndex.length > 0 && (
                                                                  <div className="space-y-1.5 pt-1">
                                                                    <p className="font-semibold text-slate-800 dark:text-white text-[11px]">
                                                                      Índice de Engramas Propuesto ({cd.proposedEngramIndex.length} temas):
                                                                    </p>
                                                                    <div className="space-y-1">
                                                                      {cd.proposedEngramIndex.map((pe, pei) => (
                                                                        <div
                                                                          key={pei}
                                                                          className="rounded border border-slate-200/60 p-2 text-[10px] dark:border-white/5 bg-slate-50/50 dark:bg-slate-950/20 flex items-start justify-between gap-2"
                                                                        >
                                                                          <div className="space-y-0.5">
                                                                            <span className="font-bold text-slate-900 dark:text-white">
                                                                              {pe.suggestedOrder}. {pe.proposedEngramTitle}
                                                                            </span>
                                                                            <p className="text-slate-600 dark:text-white/60">
                                                                              {pe.proposedEngramDescription}
                                                                            </p>
                                                                          </div>
                                                                          <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[9px] text-slate-600 dark:bg-white/10 dark:text-white/50 shrink-0">
                                                                            {pe.capabilitySupported}
                                                                          </span>
                                                                        </div>
                                                                      ))}
                                                                    </div>
                                                                  </div>
                                                                )}
                                                              </div>
                                                            ))}
                                                          </div>
                                                        </div>
                                                      )}

                                                      {/* Catalog Checked Items */}
                                                      {stepGapDrafts[st.step_id].catalogItemsChecked.length > 0 && (
                                                        <div className="text-[10px] text-slate-400 dark:text-white/40 pt-1 border-t border-rose-100 dark:border-white/5">
                                                          Elementos del catálogo verificados para prevenir duplicación: {stepGapDrafts[st.step_id].catalogItemsChecked.length}
                                                        </div>
                                                      )}
                                                    </div>
                                                  )}

                                                  {/* Step 7 Action: Generate Standard Step Learning Assignment */}
                                                  <div className="pt-3 border-t border-rose-200/60 dark:border-white/10 flex items-center justify-between">
                                                    <button
                                                      type="button"
                                                      onClick={() => handleGenerateAssignment(st.step_id)}
                                                      disabled={generatingAssignmentStepId === st.step_id}
                                                      className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-blue-600 to-indigo-700 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:from-blue-500 hover:to-indigo-600 transition disabled:opacity-50"
                                                    >
                                                      {generatingAssignmentStepId === st.step_id ? (
                                                        <>
                                                          <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                                                          <span>Generando Asignación de Aprendizaje...</span>
                                                        </>
                                                      ) : (
                                                        <>
                                                          <ClipboardList className="h-3.5 w-3.5" />
                                                          <span>Generar Asignación de Aprendizaje (Paso 7)</span>
                                                        </>
                                                      )}
                                                    </button>

                                                    {stepAssignments[st.step_id] && (
                                                      <span className={`text-[11px] font-bold px-2.5 py-0.5 rounded-full flex items-center gap-1 ${
                                                        stepAssignments[st.step_id].assignmentStatus === 'ready'
                                                          ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                                                          : stepAssignments[st.step_id].assignmentStatus === 'ready_with_content_gaps'
                                                          ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                                                          : 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                      }`}>
                                                        <CheckCircle2 className="h-3.5 w-3.5" />
                                                        Estatus: {stepAssignments[st.step_id].assignmentStatus === 'ready'
                                                          ? 'Listo'
                                                          : stepAssignments[st.step_id].assignmentStatus === 'ready_with_content_gaps'
                                                          ? 'Listo con brechas de soporte'
                                                          : 'Bloqueado por brecha crítica'}
                                                      </span>
                                                    )}
                                                  </div>

                                                  {/* Step 7 Error */}
                                                  {assignmentErrors[st.step_id] && (
                                                    <div className="rounded-lg bg-rose-50 p-2.5 text-xs text-rose-700 dark:bg-rose-500/10 dark:text-rose-400 flex items-center gap-2">
                                                      <AlertTriangle className="h-4 w-4 shrink-0" />
                                                      <span>{assignmentErrors[st.step_id]}</span>
                                                    </div>
                                                  )}

                                                  {/* Step 7 Display */}
                                                  {stepAssignments[st.step_id] && (
                                                    <div className="rounded-xl border border-blue-200 bg-blue-50/20 p-4 space-y-4 dark:border-blue-500/20 dark:bg-slate-900/60">
                                                      <div className="flex items-center justify-between border-b border-blue-100 pb-2 dark:border-white/5">
                                                        <div className="flex items-center gap-2 text-blue-900 dark:text-blue-200 font-semibold text-xs uppercase tracking-wider">
                                                          <ClipboardList className="h-4 w-4 text-blue-600 dark:text-blue-400" />
                                                          <span>Asignación de Aprendizaje Estándar del Paso (Paso 7)</span>
                                                        </div>
                                                        <span className={`rounded-full px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider ${
                                                          stepAssignments[st.step_id].assignmentStatus === 'ready'
                                                            ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                                                            : stepAssignments[st.step_id].assignmentStatus === 'ready_with_content_gaps'
                                                            ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                                                            : 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                        }`}>
                                                          {stepAssignments[st.step_id].assignmentStatus}
                                                        </span>
                                                      </div>

                                                      {/* Markdown Document Presentation */}
                                                      <div className="rounded-xl bg-white p-5 border border-slate-200 shadow-sm dark:bg-slate-950/60 dark:border-white/10 prose prose-slate dark:prose-invert max-w-none text-xs leading-relaxed overflow-x-auto whitespace-pre-wrap font-sans">
                                                        {stepAssignments[st.step_id].assignmentMarkdown}
                                                      </div>
                                                    </div>
                                                  )}

                                                  {/* Step 8 Action: Personalize and Assign to Employee */}
                                                  <div className="pt-3 border-t border-blue-200/60 dark:border-white/10 flex flex-col md:flex-row items-start md:items-center justify-between gap-3">
                                                    <div className="flex items-center gap-2 w-full md:w-auto">
                                                      <User className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
                                                      <select
                                                        value={selectedEmployeeIdByStep[st.step_id] || user?.personId || ''}
                                                        onChange={(e) =>
                                                          setSelectedEmployeeIdByStep((prev) => ({
                                                            ...prev,
                                                            [st.step_id]: e.target.value,
                                                          }))
                                                        }
                                                        disabled={personalizingStepId === st.step_id}
                                                        className="w-full md:w-64 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs text-slate-800 shadow-sm focus:border-emerald-500 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                                                      >
                                                        <option value="">-- Seleccionar Empleado --</option>
                                                        {tenantPeople.map((p) => (
                                                          <option key={p.personId} value={p.personId}>
                                                            {p.displayName || p.email} ({p.jobRoleTitle || 'Sin puesto'})
                                                          </option>
                                                        ))}
                                                      </select>
                                                    </div>

                                                    <div className="flex items-center gap-2">
                                                      <button
                                                        type="button"
                                                        onClick={() => handlePersonalizeAndAssign(st.step_id)}
                                                        disabled={personalizingStepId === st.step_id}
                                                        className="inline-flex items-center gap-2 rounded-lg bg-gradient-to-r from-emerald-600 to-teal-700 px-3.5 py-1.5 text-xs font-semibold text-white shadow-sm hover:from-emerald-500 hover:to-teal-600 transition disabled:opacity-50"
                                                      >
                                                        {personalizingStepId === st.step_id ? (
                                                          <>
                                                            <RefreshCw className="h-3.5 w-3.5 animate-spin" />
                                                            <span>Personalizando con CV (GPT-5 mini)...</span>
                                                          </>
                                                        ) : (
                                                          <>
                                                            <UserCheck className="h-3.5 w-3.5" />
                                                            <span>Personalizar y Asignar a Empleado (Paso 8)</span>
                                                          </>
                                                        )}
                                                      </button>

                                                      {stepEmployeeAssignments[st.step_id] && (
                                                        <span className={`text-[11px] font-bold px-2.5 py-0.5 rounded-full flex items-center gap-1 ${
                                                          stepEmployeeAssignments[st.step_id].employeeAssignmentStatus === 'assigned'
                                                            ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                                                            : stepEmployeeAssignments[st.step_id].employeeAssignmentStatus === 'assigned_with_content_gaps'
                                                            ? 'bg-amber-100 text-amber-800 dark:bg-amber-500/20 dark:text-amber-300'
                                                            : 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                        }`}>
                                                          <CheckCircle2 className="h-3.5 w-3.5" />
                                                          Estatus: {stepEmployeeAssignments[st.step_id].employeeAssignmentStatus === 'assigned'
                                                            ? 'Asignado'
                                                            : stepEmployeeAssignments[st.step_id].employeeAssignmentStatus === 'assigned_with_content_gaps'
                                                            ? 'Asignado con brechas'
                                                            : 'Bloqueado por brecha'}
                                                        </span>
                                                      )}
                                                    </div>
                                                  </div>

                                                  {/* Step 8 Error */}
                                                  {employeeAssignmentErrors[st.step_id] && (
                                                    <div className="rounded-lg bg-rose-50 p-2.5 text-xs text-rose-700 dark:bg-rose-500/10 dark:text-rose-400 flex items-center gap-2">
                                                      <AlertTriangle className="h-4 w-4 shrink-0" />
                                                      <span>{employeeAssignmentErrors[st.step_id]}</span>
                                                    </div>
                                                  )}

                                                  {/* Step 8 Display */}
                                                  {stepEmployeeAssignments[st.step_id] && (
                                                    <div className="rounded-xl border border-emerald-300 bg-emerald-50/20 p-4 space-y-4 dark:border-emerald-500/20 dark:bg-slate-900/60">
                                                      <div className="flex items-center justify-between border-b border-emerald-100 pb-2 dark:border-white/5">
                                                        <div className="flex items-center gap-2 text-emerald-900 dark:text-emerald-200 font-semibold text-xs uppercase tracking-wider">
                                                          <UserCheck className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                                                          <span>Asignación de Aprendizaje Personalizada al Empleado (Paso 8)</span>
                                                        </div>
                                                        <span className="font-mono text-[10px] text-slate-500 dark:text-white/50">
                                                          ID: {stepEmployeeAssignments[st.step_id].assignmentId}
                                                        </span>
                                                      </div>

                                                      {/* Reported experience summary */}
                                                      {stepEmployeeAssignments[st.step_id].reportedExperienceSummary.length > 0 && (
                                                        <div className="rounded-lg bg-white/90 p-3 text-xs space-y-1 dark:bg-white/5 border border-slate-200/60 dark:border-white/5">
                                                          <p className="font-semibold text-slate-800 dark:text-white">
                                                            Experiencia Reportada en Perfil / CV:
                                                          </p>
                                                          <ul className="list-inside list-disc text-slate-600 dark:text-white/70 space-y-0.5">
                                                            {stepEmployeeAssignments[st.step_id].reportedExperienceSummary.map((item, ri) => (
                                                              <li key={ri}>{item}</li>
                                                            ))}
                                                          </ul>
                                                        </div>
                                                      )}

                                                      {/* Assigned classes & engrams */}
                                                      <div className="space-y-3">
                                                        <p className="text-xs font-semibold text-slate-900 dark:text-white">
                                                          Clases y Engramas Asignados al Empleado:
                                                        </p>
                                                        {stepEmployeeAssignments[st.step_id].assignedClasses.map((ac) => (
                                                          <div
                                                            key={ac.classId}
                                                            className="rounded-lg border border-slate-200 p-3 bg-white/80 dark:bg-white/5 dark:border-white/10 space-y-2"
                                                          >
                                                            <div className="flex items-center justify-between">
                                                              <div>
                                                                <span className="rounded bg-slate-100 px-2 py-0.5 text-[10px] font-bold text-slate-700 dark:bg-white/10 dark:text-white/80">
                                                                  {ac.learningPathName}
                                                                </span>
                                                                <h6 className="mt-1 text-xs font-bold text-slate-900 dark:text-white">
                                                                  {ac.className}
                                                                </h6>
                                                              </div>
                                                              <span className={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${
                                                                ac.classAssignmentStatus === 'required'
                                                                  ? 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                                  : ac.classAssignmentStatus === 'recommended'
                                                                  ? 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300'
                                                                  : 'bg-slate-100 text-slate-700 dark:bg-white/10 dark:text-white/70'
                                                              }`}>
                                                                {ac.classAssignmentStatus === 'required' ? 'Obligatorio' : ac.classAssignmentStatus === 'recommended' ? 'Recomendado' : 'Referencia'}
                                                              </span>
                                                            </div>

                                                            <div className="space-y-1.5 pt-1">
                                                              {ac.assignedEngrams.map((ae) => (
                                                                <div
                                                                  key={ae.engramId}
                                                                  className="rounded border border-slate-200/60 p-2 text-xs dark:border-white/5 space-y-1"
                                                                >
                                                                  <div className="flex items-center justify-between">
                                                                    <span className="font-semibold text-slate-800 dark:text-white">
                                                                      {ae.engramName}
                                                                    </span>
                                                                    <div className="flex items-center gap-1.5">
                                                                      <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[9px] text-slate-600 dark:bg-white/10 dark:text-white/60">
                                                                        Evidencia: {ae.employeeEvidenceLevel}
                                                                      </span>
                                                                      <span className={`rounded-full px-2 py-0.5 text-[9px] font-bold ${
                                                                        ae.assignmentStatus === 'required'
                                                                          ? 'bg-rose-100 text-rose-800 dark:bg-rose-500/20 dark:text-rose-300'
                                                                          : ae.assignmentStatus === 'recommended'
                                                                          ? 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300'
                                                                          : 'bg-emerald-100 text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300'
                                                                      }`}>
                                                                        {ae.assignmentStatus}
                                                                      </span>
                                                                    </div>
                                                                  </div>
                                                                  <p className="text-[11px] text-slate-600 dark:text-white/70">
                                                                    {ae.assignmentReason}
                                                                  </p>
                                                                </div>
                                                              ))}
                                                            </div>
                                                          </div>
                                                        ))}
                                                      </div>

                                                      {/* Markdown Preview */}
                                                      <div className="space-y-1 pt-2">
                                                        <p className="text-xs font-semibold text-slate-900 dark:text-white">
                                                          Documento Markdown Personalizado del Empleado:
                                                        </p>
                                                        <div className="rounded-xl bg-white p-5 border border-slate-200 shadow-sm dark:bg-slate-950/60 dark:border-white/10 prose prose-slate dark:prose-invert max-w-none text-xs leading-relaxed overflow-x-auto whitespace-pre-wrap font-sans">
                                                          {stepEmployeeAssignments[st.step_id].assignmentMarkdown}
                                                        </div>
                                                      </div>
                                                    </div>
                                                  )}
                                                </div>
                                              )}
                                            </div>
                                          )}
                                        </div>
                                      )}
                                    </div>
                                  )}
                                </div>
                              ))
                            )}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              )}

              {/* Tab: Tables */}
              {activeTab === 'tables' && structure && (
                <div className="space-y-6">
                  {structure.tables.length === 0 ? (
                    <p className="text-sm italic text-slate-400 dark:text-white/40">
                      No se detectaron tablas en este documento.
                    </p>
                  ) : (
                    structure.tables.map((t) => (
                      <div
                        key={t.table_id}
                        className="overflow-x-auto rounded-xl border border-slate-200 dark:border-white/10"
                      >
                        <div className="bg-slate-50 px-4 py-2.5 text-xs font-semibold text-slate-700 dark:bg-white/5 dark:text-white/80">
                          {t.title} (Pág. {t.source_page})
                        </div>
                        <table className="min-w-full divide-y divide-slate-200 text-left text-xs dark:divide-white/10">
                          {t.headers.length > 0 && (
                            <thead className="bg-slate-100/75 dark:bg-white/5">
                              <tr>
                                {t.headers.map((h, i) => (
                                  <th key={i} className="px-3.5 py-2 font-semibold text-slate-800 dark:text-white">
                                    {h}
                                  </th>
                                ))}
                              </tr>
                            </thead>
                          )}
                          <tbody className="divide-y divide-slate-200 dark:divide-white/5">
                            {t.rows.map((row, rIdx) => (
                              <tr key={rIdx} className="hover:bg-slate-50/50 dark:hover:bg-white/5">
                                {row.map((cell, cIdx) => (
                                  <td key={cIdx} className="px-3.5 py-2 text-slate-600 dark:text-white/70">
                                    {cell}
                                  </td>
                                ))}
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    ))
                  )}
                </div>
              )}

              {/* Tab: JSON */}
              {activeTab === 'json' && structure && (
                <div className="relative">
                  <pre className="max-h-96 overflow-auto rounded-xl bg-slate-900 p-4 font-mono text-xs text-slate-100 dark:bg-black/80">
                    {JSON.stringify(structure, null, 2)}
                  </pre>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
