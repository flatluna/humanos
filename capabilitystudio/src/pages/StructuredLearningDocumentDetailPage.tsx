import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, BookOpen, FileText, Layers, Calendar, Plus, Trash2, Pencil, Check, X, Loader2, Sparkles, Building2, Award, GraduationCap, RotateCcw, UploadCloud } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  addStructuredLearningDocumentToProgram,
  deleteStructuredLearningDocument,
  getProgramsForStructuredLearningDocument,
  getStructuredLearningDocument,
  updateStructuredLearningClassification,
  updateStructuredLearningDocumentName,
  removeStructuredLearningDocumentFromProgram,
  updateStructuredLearningVisibility,
  startStructuredLearningPedagogy,
  getActiveStructuredLearningPedagogyRun,
  getActiveStructuredLearningExtractionRunForDocument,
  updateStructuredLearningPedagogyConfig,
  type StructuredLearningDocumentDetail,
  type StructuredLearningDocumentProgramMembership,
  type StructuredLearningPedagogyRunStatus,
  type StructuredLearningExtractionRunStatus,
} from '../lib/api/structuredLearningApi';
import { getPrograms, type BackendProgram } from '../lib/api/programsApi';
import { readFileAsBase64 } from '../lib/api/pdfPipelineApi';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningTopics, type StructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';
import { getSkills, type SkillDto } from '../lib/api/skillsApi';
import SearchableCombobox from '../components/SearchableCombobox';
import { DEFAULT_STUDIO_TENANT_ID } from '../lib/api/pdfPipelineApi';
import { useAuth } from '../auth/AuthContext';
import { apiImageUrl } from '../lib/api/httpClient';
import LoadingSpinner from '../components/LoadingSpinner';
import DeleteCapabilityModal from '../components/DeleteCapabilityModal';

export default function StructuredLearningDocumentDetailPage() {
  const { t, i18n } = useTranslation();
  const { documentId } = useParams<{ documentId: string }>();
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
  const navigate = useNavigate();
  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [programs, setPrograms] = useState<BackendProgram[]>([]);
  const [memberships, setMemberships] = useState<StructuredLearningDocumentProgramMembership[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [topics, setTopics] = useState<StructuredLearningTopic[]>([]);
  const [selectedProgramId, setSelectedProgramId] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [linkBusy, setLinkBusy] = useState(false);
  const [linkError, setLinkError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [visibility, setVisibility] = useState({ isGlobal: false, isPublished: false, showKnowledgeGraphToLearners: true });
  const [visibilityBusy, setVisibilityBusy] = useState(false);
  const [visibilityMessage, setVisibilityMessage] = useState<string | null>(null);
  const [selectedSubjectId, setSelectedSubjectId] = useState('');
  const [selectedTopicId, setSelectedTopicId] = useState('');
  const [classificationType, setClassificationType] = useState<'enterprise' | 'academic'>('enterprise');
  const [selectedCategory, setSelectedCategory] = useState<string>('job-skills');
  const [selectedTargetSkill, setSelectedTargetSkill] = useState<string>('');
  const [allCompanySkills, setAllCompanySkills] = useState<SkillDto[]>([]);
  const [classificationBusy, setClassificationBusy] = useState(false);
  const [classificationMessage, setClassificationMessage] = useState<string | null>(null);
  const [editingTitle, setEditingTitle] = useState(false);
  const [titleDraft, setTitleDraft] = useState('');
  const [titleSaving, setTitleSaving] = useState(false);
  const [titleError, setTitleError] = useState<string | null>(null);
  // Pedagogy generation resume (2026-08-30) — lets a designer who lost the
  // original run's page (or whose func host restarted mid-run, orphaning any
  // in-flight sections) find their way back and continue where it left off,
  // instead of the document being silently stuck forever with no visible way
  // to finish it. Polls the SAME active-run endpoint the global banner uses.
  const [pedagogyRun, setPedagogyRun] = useState<StructuredLearningPedagogyRunStatus | null>(null);
  const [pedagogyStarting, setPedagogyStarting] = useState(false);
  const [pedagogyError, setPedagogyError] = useState<string | null>(null);

  // Step 2 (extraction/knowledge-graph build) in-progress indicator
  // (2026-10-27) — distinct from pedagogyRun above, which only tracks Step 3.
  // Without this, a document whose Sections rows already exist mid-Step-2
  // (e.g. while the knowledge graph's entity relations are still being built)
  // misleadingly showed "N of N sections still need pedagogy content /
  // Continue generating" even though nothing has stalled — Step 2 simply
  // hasn't finished yet.
  const [extractionRun, setExtractionRun] = useState<StructuredLearningExtractionRunStatus | null>(null);

  // "Restart engrams" (2026-09-19) — forces a full regeneration of EVERY
  // section, or just a chosen From/To engram range, while letting the
  // designer fix the language and optionally upload a company logo/reference
  // image reused across every generated illustration. Backend now deletes
  // each section's old steps/illustrations/hypothesis/assessment history
  // atomically before writing the fresh content (StructuredLearningPedagogyService).
  const [showRestartModal, setShowRestartModal] = useState(false);
  const [restartLanguage, setRestartLanguage] = useState<'es' | 'en'>('es');
  const [restartImageFile, setRestartImageFile] = useState<File | null>(null);
  const [restartImagePreviewUrl, setRestartImagePreviewUrl] = useState<string | null>(null);
  const [restartImageInstruction, setRestartImageInstruction] = useState('');
  const [restartFromEngram, setRestartFromEngram] = useState('');
  const [restartToEngram, setRestartToEngram] = useState('');
  const [restartRecallGenerationMode, setRestartRecallGenerationMode] = useState<'Static' | 'Dynamic'>('Dynamic');
  const [restarting, setRestarting] = useState(false);
  const [restartError, setRestartError] = useState<string | null>(null);

  useEffect(() => {
    if (!documentId) {
      setError('Document not found.');
      setLoading(false);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([
      getStructuredLearningDocument(documentId),
      getProgramsForStructuredLearningDocument(documentId),
      getPrograms(),
      getSubjects(),
      getSkills(effectiveTenantId).catch(() => []),
    ])
      .then(([detail, currentMemberships, allPrograms, allSubjects, companySkills]) => {
        if (cancelled) return;
        setDocument(detail);
        setVisibility({ isGlobal: detail.IsGlobal, isPublished: detail.IsPublished, showKnowledgeGraphToLearners: detail.ShowKnowledgeGraphToLearners });
        setMemberships(currentMemberships);
        setPrograms(allPrograms);
        setSubjects(allSubjects);
        setAllCompanySkills(companySkills);

        const isEnterprise = !!(detail.Category || detail.TargetSkill);
        setClassificationType(isEnterprise ? 'enterprise' : 'academic');
        setSelectedCategory(detail.Category || 'job-skills');
        setSelectedTargetSkill(detail.TargetSkill || '');

        const initialSubjectId = detail.SubjectId ?? '';
        setSelectedSubjectId(initialSubjectId);
        setSelectedTopicId(detail.TopicId ?? '');
        getStructuredLearningTopics(effectiveTenantId, initialSubjectId || undefined)
          .then(setTopics)
          .catch(() => setTopics([]));
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load this document.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [documentId]);

  useEffect(() => {
    if (!documentId) return;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;
    let wasRunning = false;

    async function poll() {
      const run = await getActiveStructuredLearningPedagogyRun().catch(() => null);
      if (cancelled) return;
      const belongsToThisDocument = run?.DocumentId === documentId;
      setPedagogyRun(belongsToThisDocument ? run : null);
      if (belongsToThisDocument && run?.Stage === 'Running') {
        wasRunning = true;
        timer = setTimeout(poll, 3000);
      } else {
        if (wasRunning && documentId) {
          wasRunning = false;
          getStructuredLearningDocument(documentId)
            .then((detail) => { if (!cancelled) setDocument(detail); })
            .catch(() => {});
        }
      }
    }

    poll();
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [documentId]);

  useEffect(() => {
    if (!documentId) return;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;
    let wasRunning = false;

    async function poll() {
      const run = await getActiveStructuredLearningExtractionRunForDocument(documentId!).catch(() => null);
      if (cancelled) return;
      setExtractionRun(run);
      if (run?.Stage === 'Running') {
        wasRunning = true;
        timer = setTimeout(poll, 3000);
      } else if (wasRunning && documentId) {
        wasRunning = false;
        getStructuredLearningDocument(documentId)
          .then((detail) => { if (!cancelled) setDocument(detail); })
          .catch(() => {});
      }
    }

    poll();
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  }, [documentId]);

  async function handleResumePedagogy() {
    if (!documentId || pedagogyStarting) return;
    setPedagogyStarting(true);
    setPedagogyError(null);
    try {
      const run = await startStructuredLearningPedagogy(documentId);
      setPedagogyRun(run);
    } catch (err) {
      setPedagogyError(err instanceof Error ? err.message : 'Could not start pedagogy generation.');
    } finally {
      setPedagogyStarting(false);
    }
  }

  function openRestartModal() {
    if (document) {
      try {
        const snapshot = document.ConfigSnapshotJson ? JSON.parse(document.ConfigSnapshotJson) : null;
        const savedLanguage = snapshot?.LanguageCode ?? snapshot?.languageCode;
        if (savedLanguage === 'es' || savedLanguage === 'en') setRestartLanguage(savedLanguage);
      } catch {
        // Keep the current default.
      }
      setRestartRecallGenerationMode(document.RecallGenerationMode ?? 'Dynamic');
    }
    setRestartError(null);
    setShowRestartModal(true);
  }

  function closeRestartModal() {
    if (restarting) return;
    setShowRestartModal(false);
    setRestartError(null);
    if (restartImagePreviewUrl) URL.revokeObjectURL(restartImagePreviewUrl);
    setRestartImagePreviewUrl(null);
    setRestartImageFile(null);
    setRestartImageInstruction('');
  }

  async function handleRestartFromFirstEngram() {
    if (!documentId || restarting) return;
    if (restartImageFile && !restartImageInstruction.trim()) {
      setRestartError('Describe how the logo/reference image should be used (e.g. "use this logo on clothing/signage in every illustration").');
      return;
    }
    const fromNum = restartFromEngram.trim() ? Number(restartFromEngram.trim()) : null;
    const toNum = restartToEngram.trim() ? Number(restartToEngram.trim()) : null;
    if ((fromNum != null && (!Number.isInteger(fromNum) || fromNum < 1)) || (toNum != null && (!Number.isInteger(toNum) || toNum < 1))) {
      setRestartError('Engram range must be positive whole numbers (e.g. 1 to 10).');
      return;
    }
    if (fromNum != null && toNum != null && fromNum > toNum) {
      setRestartError('"From" engram must be less than or equal to "To" engram.');
      return;
    }
    setRestarting(true);
    setRestartError(null);
    try {
      // Persist the chosen Recall generation mode BEFORE restarting (2026-10-26)
      // — EnsureStaticItemsAsync only (re)generates Recall cards when the
      // document's RecallGenerationMode is Static, so a designer switching a
      // course that was created as Dynamic needs this saved first, or the
      // restart silently does nothing for Recall at all.
      if (document && restartRecallGenerationMode !== (document.RecallGenerationMode ?? 'Dynamic')) {
        // Cast to loosely-typed access for a few learner-facing-agent toggle
        // fields the shared StructuredLearningDocumentDetail type doesn't
        // declare yet (pre-existing gap, same one CourseDesignSummaryPanel
        // already works around) — read them back unchanged so this save
        // only actually changes RecallGenerationMode.
        const doc = document as unknown as Record<string, unknown>;
        await updateStructuredLearningPedagogyConfig(documentId, {
          pedagogicalAudience: document.PedagogicalAudience,
          maxDeepDiveCardsPerNode: document.MaxDeepDiveCardsPerNode,
          includeProductionStep: document.IncludeProductionStep,
          includeRecallStep: document.IncludeRecallStep,
          recallItemsRequired: document.RecallItemsRequired,
          includeAssessmentStep: document.IncludeAssessmentStep,
          assessmentQuestionType: document.AssessmentQuestionType,
          assessmentQuestionsPerRound: document.AssessmentQuestionsPerRound,
          recallGenerationMode: restartRecallGenerationMode,
          assessmentGenerationMode: document.AssessmentGenerationMode,
          showQaProfessorStep: (doc.ShowQaProfessorStep as boolean | undefined) ?? true,
          showDeepDiveAgentChat: (doc.ShowDeepDiveAgentChat as boolean | undefined) ?? true,
          enableQaProfessorVoice: (doc.EnableQaProfessorVoice as boolean | undefined) ?? true,
          enableDeepDiveAgentVoice: (doc.EnableDeepDiveAgentVoice as boolean | undefined) ?? true,
          includeWorkplaceApplicationStep: (doc.IncludeWorkplaceApplicationStep as boolean | undefined) ?? true,
        });
      }
      const uploadedImageBase64 = restartImageFile ? await readFileAsBase64(restartImageFile) : null;
      const run = await startStructuredLearningPedagogy(
        documentId,
        restartLanguage,
        true,
        uploadedImageBase64,
        restartImageFile?.type ?? null,
        restartImageFile ? restartImageInstruction.trim() || null : null,
        undefined,
        fromNum,
        toNum,
      );
      setPedagogyRun(run);
      setShowRestartModal(false);
      if (restartImagePreviewUrl) URL.revokeObjectURL(restartImagePreviewUrl);
      setRestartImagePreviewUrl(null);
      setRestartImageFile(null);
      setRestartImageInstruction('');
      setRestartFromEngram('');
      setRestartToEngram('');
    } catch (err) {
      setRestartError(err instanceof Error ? err.message : 'Could not start the full regeneration.');
    } finally {
      setRestarting(false);
    }
  }

  async function handleSaveVisibility() {
    if (!documentId || visibilityBusy) return;
    setVisibilityBusy(true);
    setVisibilityMessage(null);
    try {
      const saved = await updateStructuredLearningVisibility(documentId, visibility);
      setVisibility({ isGlobal: saved.IsGlobal, isPublished: saved.IsPublished, showKnowledgeGraphToLearners: saved.ShowKnowledgeGraphToLearners });
      setVisibilityMessage(t('structuredLearningVisibility.saved'));
    } catch {
      setVisibilityMessage(t('structuredLearningVisibility.error'));
    } finally {
      setVisibilityBusy(false);
    }
  }

  function handleSubjectChange(newSubjectId: string) {
    setSelectedSubjectId(newSubjectId);
    setSelectedTopicId('');
    getStructuredLearningTopics(effectiveTenantId, newSubjectId || undefined)
      .then(setTopics)
      .catch(() => setTopics([]));
  }

  async function handleSaveClassification() {
    if (!documentId || classificationBusy) return;
    setClassificationBusy(true);
    setClassificationMessage(null);
    try {
      const isEnterprise = classificationType === 'enterprise';
      const saved = await updateStructuredLearningClassification(
        documentId,
        isEnterprise ? null : (selectedSubjectId || null),
        isEnterprise ? null : (selectedTopicId || null),
        isEnterprise ? (selectedCategory || null) : null,
        isEnterprise ? (selectedTargetSkill.trim() || null) : null,
      );
      setDocument((current) =>
        current
          ? {
              ...current,
              SubjectId: saved.SubjectId,
              TopicId: saved.TopicId,
              Category: saved.Category,
              TargetSkill: saved.TargetSkill,
            }
          : current,
      );
      setClassificationMessage(t('structuredLearningDetail.classificationSuccess'));
    } catch (err) {
      setClassificationMessage(err instanceof Error ? err.message : t('structuredLearningDetail.classificationError'));
    } finally {
      setClassificationBusy(false);
    }
  }

  function startEditingTitle() {
    if (!document) return;
    setTitleDraft(document.DocumentName);
    setTitleError(null);
    setEditingTitle(true);
  }

  async function handleSaveTitle() {
    if (!documentId || titleSaving) return;
    const trimmed = titleDraft.trim();
    if (!trimmed) {
      setTitleError(t('structuredLearningDetail.titleEmptyError'));
      return;
    }
    setTitleSaving(true);
    setTitleError(null);
    try {
      const saved = await updateStructuredLearningDocumentName(documentId, trimmed);
      setDocument((current) => current ? { ...current, DocumentName: saved.DocumentName } : current);
      setEditingTitle(false);
    } catch (err) {
      setTitleError(err instanceof Error ? err.message : t('structuredLearningDetail.titleUpdateError'));
    } finally {
      setTitleSaving(false);
    }
  }

  async function handleAddToProgram() {
    if (!documentId || !selectedProgramId || linkBusy) return;
    setLinkBusy(true);
    setLinkError(null);

    try {
      await addStructuredLearningDocumentToProgram(documentId, selectedProgramId);
      const refreshed = await getProgramsForStructuredLearningDocument(documentId);
      setMemberships(refreshed);
      setSelectedProgramId('');
    } catch (err) {
      setLinkError(err instanceof Error ? err.message : 'Could not attach this document to the selected program.');
    } finally {
      setLinkBusy(false);
    }
  }

  async function handleRemoveFromProgram(programId: string) {
    if (!documentId || linkBusy) return;
    setLinkBusy(true);
    setLinkError(null);

    try {
      await removeStructuredLearningDocumentFromProgram(documentId, programId);
      setMemberships((prev) => prev.filter((m) => m.ProgramId !== programId));
    } catch (err) {
      setLinkError(err instanceof Error ? err.message : 'Could not remove this document from the program.');
    } finally {
      setLinkBusy(false);
    }
  }

  async function handleDeleteDocument() {
    if (!documentId || !document || deleting) return;

    setDeleting(true);
    setError(null);

    try {
      await deleteStructuredLearningDocument(documentId);
      setShowDeleteModal(false);
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete this structured learning course.');
    } finally {
      setDeleting(false);
    }
  }

  if (loading) return <LoadingSpinner label="Loading structured learning document..." />;

  if (error || !document) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16 text-center">
        <p className="text-red-300">{error ?? 'This structured learning document could not be found.'}</p>
        <Link to="/" className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300">
          <ArrowLeft className="h-4 w-4" /> Back to catalog
        </Link>
      </div>
    );
  }

  const createdDate = new Date().toLocaleDateString();
  const isSpanish = i18n.language?.startsWith('es');
  // Studio's own Subject/Topic taxonomy fields are a separate classification
  // from the Family-origin data (country/education system/stage) a course may
  // have been created from. When nobody has manually assigned a Subject/Topic
  // here, fall back to showing where the course actually came from (e.g. a
  // Family course has no Subject/Topic but DOES have a known stage/grade) so
  // this never reads as a dead-end "Unassigned" when the origin is known.
  const subjectName = subjects.find((subject) => subject.SubjectId === document.SubjectId)?.Name
    ?? (document.FamilyOrigin ? t('structuredLearningDetail.familyOriginLabel') : t('structuredLearningDetail.unassigned'));
  const topicName = topics.find((topic) => topic.StructuredLearningTopicId === document.TopicId)?.Name
    ?? (document.FamilyOrigin ? (isSpanish ? document.FamilyOrigin.StageNameEs : document.FamilyOrigin.StageNameEn) ?? t('structuredLearningDetail.unassigned') : t('structuredLearningDetail.unassigned'));
  const isEnterpriseCourse = !!(document.TargetSkill || document.Category);

  return (
    <div>
      <div className="relative h-56 w-full overflow-hidden bg-gradient-to-br from-teal-500/30 to-emerald-500/20">
        {document.CoverIllustrationId && (
          <img
            src={apiImageUrl(`/illustrations/structured-learning/${document.CoverIllustrationId}/image`)}
            alt=""
            className="absolute inset-0 h-full w-full object-cover opacity-70"
          />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black via-black/40 to-transparent" />
        <div className="absolute inset-x-0 bottom-0 mx-auto max-w-7xl px-6 pb-6">
          <Link to="/" className="mb-3 inline-flex items-center gap-1.5 text-sm text-[#fff]/80 hover:text-[#fff]">
            <ArrowLeft className="h-4 w-4" /> Back to catalog
          </Link>
          {editingTitle ? (
            <div className="flex flex-wrap items-center gap-2">
              <input
                autoFocus
                value={titleDraft}
                onChange={(event) => setTitleDraft(event.target.value)}
                onKeyDown={(event) => { if (event.key === 'Enter') void handleSaveTitle(); if (event.key === 'Escape') setEditingTitle(false); }}
                className="rounded-lg border border-white/20 bg-black/40 px-3 py-1.5 text-2xl font-bold text-white outline-none focus:border-brand-400"
              />
              <button type="button" onClick={handleSaveTitle} disabled={titleSaving} title="Save" className="rounded-lg bg-brand-500 p-2 text-white hover:bg-brand-400 disabled:opacity-60">
                <Check className="h-4 w-4" />
              </button>
              <button type="button" onClick={() => setEditingTitle(false)} title="Cancel" className="rounded-lg bg-white/10 p-2 text-white hover:bg-white/20">
                <X className="h-4 w-4" />
              </button>
            </div>
          ) : (
            <h1 className="group flex items-center gap-2 text-3xl font-bold text-[#fff]">
              {document.DocumentName}
              <button type="button" onClick={startEditingTitle} title="Edit title" className="rounded-lg p-1.5 text-white/60 opacity-0 transition group-hover:opacity-100 hover:bg-white/10 hover:text-white">
                <Pencil className="h-4 w-4" />
              </button>
            </h1>
          )}
          {titleError && <p className="mt-1 text-sm text-red-300">{titleError}</p>}
          <span className="mt-2 inline-flex items-center gap-1.5 rounded-full bg-black/30 px-3 py-1 text-xs font-medium text-[#fff] backdrop-blur-sm">
            <FileText className="h-3.5 w-3.5" /> Structured section document
          </span>
          <span className="mt-2 ml-2 inline-flex items-center gap-2 rounded-full border border-sky-300/70 bg-sky-100 px-4 py-2 text-sm font-bold text-sky-900 shadow-sm dark:border-amber-300/70 dark:bg-amber-400 dark:text-amber-950">
            Language: {document.LanguageCode === 'en' ? 'English' : 'Spanish'}
          </span>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-6 py-10">
        <div className="mt-8 grid grid-cols-2 gap-4 sm:grid-cols-3">
          {isEnterpriseCourse ? (
            <>
              <InfoCard icon={Building2} label={t('structuredLearningDetail.skillLabel')} value={document.TargetSkill || t('structuredLearningDetail.unassigned')} />
              <InfoCard icon={Award} label={t('structuredLearningDetail.skillCategoryLabel')} value={getCategoryLabel(document.Category, t) || t('structuredLearningDetail.unassigned')} />
            </>
          ) : (
            <>
              <InfoCard icon={BookOpen} label={t('structuredLearningDetail.domainLabel')} value={subjectName} />
              <InfoCard icon={Layers} label={t('structuredLearningDetail.domainTypeLabel')} value={topicName} />
            </>
          )}
          <InfoCard icon={Layers} label="Sections" value={document.Sections.length} />
          <InfoCard icon={BookOpen} label="Mode" value="Structured learning" />
          <InfoCard icon={Calendar} label="Created" value={createdDate} />
        </div>

        <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.02] p-6">
          <h3 className="text-lg font-semibold text-white">{t('structuredLearningDetail.classificationTitle')}</h3>
          <p className="mt-1 text-sm text-slate-400">{t('structuredLearningDetail.classificationSubtitle')}</p>

          {/* Mode Switch Cards */}
          <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
            <button
              type="button"
              onClick={() => setClassificationType('enterprise')}
              className={`flex items-start gap-3 rounded-xl border p-4 text-left transition-all ${
                classificationType === 'enterprise'
                  ? 'border-brand-400 bg-brand-500/10 text-white shadow-lg shadow-brand-500/10'
                  : 'border-white/10 bg-white/[0.02] text-slate-400 hover:border-white/20 hover:bg-white/[0.04]'
              }`}
            >
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-500/20 text-brand-300">
                <Building2 className="h-4 w-4" />
              </div>
              <div>
                <p className="font-semibold text-white">{t('newStructuredLearning.enterpriseCourseTitle')}</p>
                <p className="mt-0.5 text-xs text-slate-400">{t('newStructuredLearning.enterpriseCourseDesc')}</p>
              </div>
            </button>

            <button
              type="button"
              onClick={() => setClassificationType('academic')}
              className={`flex items-start gap-3 rounded-xl border p-4 text-left transition-all ${
                classificationType === 'academic'
                  ? 'border-cyan-400 bg-cyan-500/10 text-white shadow-lg shadow-cyan-500/10'
                  : 'border-white/10 bg-white/[0.02] text-slate-400 hover:border-white/20 hover:bg-white/[0.04]'
              }`}
            >
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-cyan-500/20 text-cyan-300">
                <GraduationCap className="h-4 w-4" />
              </div>
              <div>
                <p className="font-semibold text-white">{t('newStructuredLearning.academicCourseTitle')}</p>
                <p className="mt-0.5 text-xs text-slate-400">{t('newStructuredLearning.academicCourseDesc')}</p>
              </div>
            </button>
          </div>

          {/* ENTERPRISE CLASSIFICATION FORM */}
          {classificationType === 'enterprise' && (
            <div className="mt-5 space-y-4 rounded-xl border border-brand-500/20 bg-brand-500/[0.03] p-4">
              <div>
                <label className="mb-2 block text-xs font-medium text-slate-400">{t('newStructuredLearning.filterSkillsLabel')}</label>
                <div className="flex flex-wrap gap-2">
                  {[
                    { id: 'job-skills', label: t('newStructuredLearning.categoryFilterTechnical') },
                    { id: 'soft-skills', label: t('newStructuredLearning.categoryFilterSoft') },
                    { id: 'department-processes', label: t('newStructuredLearning.categoryFilterDept') },
                    { id: 'hr-processes', label: t('newStructuredLearning.categoryFilterHR') },
                  ].map((cat) => (
                    <button
                      key={cat.id}
                      type="button"
                      onClick={() => setSelectedCategory(cat.id)}
                      className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                        selectedCategory === cat.id
                          ? 'bg-brand-500/20 text-brand-300 border border-brand-400/40'
                          : 'bg-white/5 text-slate-400 hover:bg-white/10 hover:text-white border border-transparent'
                      }`}
                    >
                      {cat.label}
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <label className="mb-1.5 block text-xs font-medium text-slate-300">
                  {t('newStructuredLearning.companyCatalogSkillLabel')}
                </label>
                <SearchableCombobox
                  value={selectedTargetSkill}
                  onChange={(val) => setSelectedTargetSkill(val)}
                  onSelect={(option) => {
                    const skill = allCompanySkills.find((s) => s.SkillId === option.id || s.Name === option.label);
                    if (skill) {
                      setSelectedTargetSkill(skill.Name);
                      setSelectedCategory(skill.Category);
                    }
                  }}
                  options={allCompanySkills
                    .filter((s) => !selectedCategory || s.Category === selectedCategory)
                    .map((s) => ({
                      id: s.SkillId,
                      label: s.Name,
                      meta: getCategoryBadgeText(s.Category),
                    }))}
                  placeholder={t('newStructuredLearning.companyCatalogSkillPlaceholder')}
                  emptyOptionsText={t('newStructuredLearning.companyCatalogSkillEmpty')}
                />
              </div>

              <div className="flex items-center gap-2 text-xs font-medium text-emerald-400">
                <Check className="h-4 w-4" />
                <span>{t('newStructuredLearning.domainSubjectNotRequired')}</span>
              </div>
            </div>
          )}

          {/* ACADEMIC CLASSIFICATION FORM */}
          {classificationType === 'academic' && (
            <div className="mt-5 flex flex-wrap items-end gap-3">
              <label className="min-w-64 flex-1 text-sm text-slate-300">
                {t('structuredLearningDetail.domainSubjectLabel')}
                <select
                  value={selectedSubjectId}
                  onChange={(event) => handleSubjectChange(event.target.value)}
                  className="input mt-2"
                >
                  <option value="">{t('structuredLearningDetail.unassigned')}</option>
                  {subjects.map((subject) => (
                    <option key={subject.SubjectId} value={subject.SubjectId}>
                      {subject.Name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="min-w-64 flex-1 text-sm text-slate-300">
                {t('structuredLearningDetail.domainTypeLabel')}
                <select
                  value={selectedTopicId}
                  onChange={(event) => setSelectedTopicId(event.target.value)}
                  className="input mt-2"
                >
                  <option value="">{t('structuredLearningDetail.unassigned')}</option>
                  {topics.map((topic) => (
                    <option key={topic.StructuredLearningTopicId} value={topic.StructuredLearningTopicId}>
                      {topic.Name}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}

          <div className="mt-5 flex items-center gap-3">
            <button
              type="button"
              onClick={handleSaveClassification}
              disabled={classificationBusy}
              className="rounded-xl bg-brand-500 px-4 py-2.5 text-sm font-medium text-white hover:bg-brand-400 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {classificationBusy ? t('structuredLearningDetail.updating') : t('structuredLearningDetail.update')}
            </button>
            {classificationMessage && <span className="text-sm text-slate-300">{classificationMessage}</span>}
          </div>
        </div>

        {(() => {
          const totalSections = document.Sections.length;
          const pendingSections = document.Sections.filter((s) => s.PedagogyStatus !== 'Completed').length;
          const isExtractionRunningHere = extractionRun?.Stage === 'Running';
          if (isExtractionRunningHere) {
            // Step 2 (extraction/knowledge-graph build) hasn't finished yet —
            // show its own progress instead of the "needs pedagogy" banner
            // below, which would otherwise be misleading (and whose
            // "Continue generating"/"Restart" actions aren't safe to run
            // while Step 2 is still writing to these same Sections rows).
            return (
              <div className="mt-8 flex items-center gap-3 rounded-xl border border-sky-400/30 bg-sky-500/10 px-5 py-3 text-sm text-sky-100">
                <Loader2 className="h-4 w-4 flex-none animate-spin text-sky-400" />
                <div className="min-w-0 flex-1">
                  <span>
                    Extraction still in progress — {extractionRun?.CurrentStepDescription || 'processing sections'}
                    {extractionRun?.TotalSections != null && extractionRun?.CompletedSections != null
                      ? ` (${extractionRun.CompletedSections}/${extractionRun.TotalSections})`
                      : ''}
                  </span>
                </div>
              </div>
            );
          }
          const isRunningHere = pedagogyRun?.Stage === 'Running';
          if (isRunningHere) {
            const targetCount = pedagogyRun?.TotalSections ?? (pendingSections > 0 ? pendingSections : totalSections);
            const doneCount = pedagogyRun?.CompletedSections ?? 0;
            const isRangeRedo = pedagogyRun?.FromEngramNumber != null || pedagogyRun?.ToEngramNumber != null;
            const rangeFrom = pedagogyRun?.FromEngramNumber ?? 1;
            const rangeTo = pedagogyRun?.ToEngramNumber ?? totalSections;
            return (
              <div className="mt-8 flex items-center gap-3 rounded-xl border border-emerald-400/30 bg-emerald-500/10 px-5 py-3 text-sm text-emerald-100">
                <Loader2 className="h-4 w-4 flex-none animate-spin text-emerald-400" />
                <div className="min-w-0 flex-1">
                  <span>
                    {isRangeRedo
                      ? `Redoing engrams ${rangeFrom} to ${rangeTo}… ${doneCount}/${targetCount} done`
                      : `Generating pedagogy… ${doneCount}/${targetCount} remaining sections done`}
                  </span>
                  {pedagogyRun?.ActiveSectionTitles && pedagogyRun.ActiveSectionTitles.length > 0 && (
                    <p className="mt-0.5 truncate text-xs text-emerald-300/80">
                      Processing: {pedagogyRun.ActiveSectionTitles.join(', ')}
                    </p>
                  )}
                </div>
              </div>
            );
          }
          if (totalSections > 0 && pendingSections > 0) {
            return (
              <div className="mt-8 flex flex-wrap items-center gap-3 rounded-xl border border-amber-400/30 bg-amber-500/10 px-5 py-3 text-sm text-amber-100">
                <span>
                  {pendingSections} of {totalSections} sections still need pedagogy content.
                </span>
                <button
                  type="button"
                  onClick={handleResumePedagogy}
                  disabled={pedagogyStarting}
                  className="inline-flex items-center gap-2 rounded-lg bg-amber-500 px-3 py-1.5 text-xs font-semibold text-slate-950 hover:bg-amber-400 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  <Sparkles className="h-3.5 w-3.5" />
                  {pedagogyStarting ? 'Starting…' : 'Continue generating'}
                </button>
                <button
                  type="button"
                  onClick={openRestartModal}
                  className="inline-flex items-center gap-2 rounded-lg border border-amber-400/40 bg-transparent px-3 py-1.5 text-xs font-semibold text-amber-100 hover:bg-amber-500/10"
                >
                  <RotateCcw className="h-3.5 w-3.5" />
                  Restart engrams…
                </button>
                {pedagogyError && <span className="text-red-300">{pedagogyError}</span>}
              </div>
            );
          }
          return null;
        })()}

        <div className="mt-8 flex flex-wrap gap-3">
          <button
            type="button"
            onClick={() => navigate(`/structured-learning/${document.StructuredLearningDocumentId}/course`)}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-500 px-5 py-3 font-medium text-white shadow-lg shadow-brand-500/20 transition hover:bg-brand-400"
          >
            <BookOpen className="h-4 w-4" /> View course
          </button>
          <button
            type="button"
            onClick={() => setShowDeleteModal(true)}
            disabled={deleting}
            className="inline-flex items-center gap-2 rounded-xl border border-red-500/30 bg-red-500/10 px-5 py-3 font-medium text-red-200 transition hover:border-red-400/60 hover:bg-red-500/15 disabled:cursor-not-allowed disabled:opacity-60"
          >
            <Trash2 className="h-4 w-4" />
            {deleting ? 'Deleting...' : 'Delete course'}
          </button>
          <button
            type="button"
            onClick={openRestartModal}
            disabled={pedagogyRun?.Stage === 'Running'}
            className="inline-flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-5 py-3 font-medium text-slate-200 transition hover:border-white/20 hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-40"
          >
            <RotateCcw className="h-4 w-4" /> Restart engrams…
          </button>
          <Link
            to="/"
            className="inline-flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-5 py-3 font-medium text-slate-200 transition hover:border-white/20 hover:bg-white/[0.06]"
          >
            <ArrowLeft className="h-4 w-4" /> Keep browsing catalog
          </Link>
        </div>

        {showRestartModal && (
          <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
            onClick={closeRestartModal}
          >
            <div
              className="max-h-[85vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-white/10 bg-slate-950 p-6 shadow-2xl"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h2 className="text-lg font-semibold text-white">Restart engrams</h2>
                  <p className="mt-1 text-sm text-slate-400">
                    Regenerates every section from scratch — or just a chosen range below — the old cards,
                    illustrations, hypothesis answers and assessment/recall history for each engram are deleted and
                    replaced atomically.
                  </p>
                </div>
                <button onClick={closeRestartModal} className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white" aria-label="Close">
                  <X className="h-5 w-5" />
                </button>
              </div>

              <div className="mt-5 space-y-4">
                <div>
                  <label className="mb-1.5 block text-xs font-medium text-slate-400">Language</label>
                  <select
                    value={restartLanguage}
                    onChange={(e) => setRestartLanguage(e.target.value as 'es' | 'en')}
                    className="input"
                  >
                    <option value="es">Español</option>
                    <option value="en">English</option>
                  </select>
                </div>

                <div>
                  <label className="mb-1.5 block text-xs font-medium text-slate-400">Engram range (optional)</label>
                  <div className="flex items-center gap-2">
                    <input
                      type="number"
                      min={1}
                      value={restartFromEngram}
                      onChange={(e) => setRestartFromEngram(e.target.value)}
                      placeholder="From (e.g. 1)"
                      className="input"
                    />
                    <span className="text-slate-500">to</span>
                    <input
                      type="number"
                      min={1}
                      value={restartToEngram}
                      onChange={(e) => setRestartToEngram(e.target.value)}
                      placeholder="To (e.g. 10)"
                      className="input"
                    />
                  </div>
                  <p className="mt-1 text-[11px] text-slate-500">Leave both blank to restart every engram in the course.</p>
                </div>

                <div>
                  <label className="mb-1.5 block text-xs font-medium text-slate-400">Recall cards</label>
                  <select
                    value={restartRecallGenerationMode}
                    onChange={(e) => setRestartRecallGenerationMode(e.target.value as 'Static' | 'Dynamic')}
                    className="input"
                  >
                    <option value="Static">Static (pre-generated image + question per card, graded instantly by voice)</option>
                    <option value="Dynamic">Dynamic (the AI agent improvises each question live)</option>
                  </select>
                  <p className="mt-1 text-[11px] text-slate-500">
                    Switching this here saves it to the course before restarting, so the new cards are
                    actually generated in the mode you pick — not just whatever mode the course already had.
                  </p>
                </div>

                <div className="space-y-3 rounded-xl border border-white/10 bg-white/[0.02] p-3">
                  <label className="block text-xs font-medium text-slate-400">Company logo / reference image (optional)</label>
                  {restartImagePreviewUrl ? (
                    <div className="flex items-center gap-3">
                      <img src={restartImagePreviewUrl} alt="reference" className="h-16 w-16 rounded-lg object-cover" />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm text-slate-300">{restartImageFile?.name}</p>
                        <button
                          type="button"
                          onClick={() => {
                            if (restartImagePreviewUrl) URL.revokeObjectURL(restartImagePreviewUrl);
                            setRestartImagePreviewUrl(null);
                            setRestartImageFile(null);
                          }}
                          className="mt-1 text-xs font-medium text-brand-400 hover:text-brand-300"
                        >
                          Remove
                        </button>
                      </div>
                    </div>
                  ) : (
                    <label className="flex w-full cursor-pointer items-center justify-center gap-2 rounded-lg border border-dashed border-white/15 py-3 text-sm text-slate-400 hover:border-white/25 hover:text-slate-300">
                      <UploadCloud className="h-4 w-4" />
                      Upload logo / reference image
                      <input
                        type="file"
                        accept="image/png,image/jpeg,image/webp"
                        className="hidden"
                        onChange={(e) => {
                          const file = e.target.files?.[0] ?? null;
                          setRestartImageFile(file);
                          setRestartImagePreviewUrl((prev) => {
                            if (prev) URL.revokeObjectURL(prev);
                            return file ? URL.createObjectURL(file) : null;
                          });
                        }}
                      />
                    </label>
                  )}
                  {restartImageFile && (
                    <div>
                      <label className="mb-1.5 block text-xs font-medium text-slate-400">How should this image be used?</label>
                      <textarea
                        value={restartImageInstruction}
                        onChange={(e) => setRestartImageInstruction(e.target.value)}
                        rows={2}
                        placeholder="e.g. use this exact logo on clothing/signage in every illustration"
                        className="input w-full"
                      />
                    </div>
                  )}
                </div>

                {restartError && (
                  <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-3 text-sm text-red-300">{restartError}</div>
                )}

                <button
                  type="button"
                  onClick={handleRestartFromFirstEngram}
                  disabled={restarting}
                  className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-brand-500/25 transition-transform enabled:hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-40"
                >
                  <RotateCcw className="h-4 w-4" />
                  {restarting
                    ? 'Starting…'
                    : restartFromEngram.trim() || restartToEngram.trim()
                    ? 'Restart selected engrams now'
                    : 'Restart all sections now'}
                </button>
              </div>
            </div>
          </div>
        )}

        <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.02] p-6">
          <h3 className="text-lg font-semibold text-white">{t('structuredLearningVisibility.title')}</h3>
          <p className="mt-1 text-sm text-slate-400">{t('structuredLearningVisibility.description')}</p>

          <div className="mt-5 grid gap-3 sm:grid-cols-2">
            <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-white/10 bg-white/[0.03] p-4">
              <input
                type="checkbox"
                checked={visibility.isGlobal}
                onChange={(event) => setVisibility((current) => ({ ...current, isGlobal: event.target.checked }))}
                className="mt-1 h-4 w-4 accent-cyan-400"
              />
              <span>
                <span className="block font-medium text-white">{t('structuredLearningVisibility.global')}</span>
                <span className="mt-1 block text-sm text-slate-400">{t('structuredLearningVisibility.globalHint')}</span>
              </span>
            </label>
            <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-white/10 bg-white/[0.03] p-4">
              <input
                type="checkbox"
                checked={visibility.isPublished}
                onChange={(event) => setVisibility((current) => ({ ...current, isPublished: event.target.checked }))}
                className="mt-1 h-4 w-4 accent-emerald-400"
              />
              <span>
                <span className="block font-medium text-white">{t('structuredLearningVisibility.published')}</span>
                <span className="mt-1 block text-sm text-slate-400">{t('structuredLearningVisibility.publishedHint')}</span>
              </span>
            </label>
            <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-white/10 bg-white/[0.03] p-4">
              <input
                type="checkbox"
                checked={visibility.showKnowledgeGraphToLearners}
                onChange={(event) => setVisibility((current) => ({ ...current, showKnowledgeGraphToLearners: event.target.checked }))}
                className="mt-1 h-4 w-4 accent-violet-400"
              />
              <span>
                <span className="block font-medium text-white">{t('structuredLearningVisibility.showKnowledgeGraph')}</span>
                <span className="mt-1 block text-sm text-slate-400">{t('structuredLearningVisibility.showKnowledgeGraphHint')}</span>
              </span>
            </label>
          </div>

          <div className="mt-4 flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={handleSaveVisibility}
              disabled={visibilityBusy}
              className="rounded-xl bg-brand-500 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-brand-400 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {visibilityBusy ? t('structuredLearningVisibility.saving') : t('structuredLearningVisibility.save')}
            </button>
            {visibilityMessage && <span className="text-sm text-slate-300">{visibilityMessage}</span>}
          </div>
        </div>

        <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.02] p-6">
          <h3 className="text-lg font-semibold text-white">Add to program</h3>

          {memberships.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              {memberships.map((membership) => (
                <span
                  key={membership.ProgramId}
                  className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/[0.03] py-1.5 pl-3 pr-1.5 text-sm text-slate-200"
                >
                  <Link to={`/programs/${membership.ProgramId}`} className="hover:text-white">
                    {membership.ProgramName}
                    {membership.SortOrder ? ` (#${membership.SortOrder})` : ''}
                  </Link>
                  <button
                    type="button"
                    onClick={() => handleRemoveFromProgram(membership.ProgramId)}
                    className="inline-flex items-center gap-1 rounded-full border border-red-500/20 bg-red-500/5 px-2 py-1 text-[11px] font-medium text-red-200 transition hover:border-red-400/50 hover:bg-red-500/10"
                    aria-label={`Delete ${membership.ProgramName}`}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    Delete
                  </button>
                </span>
              ))}
            </div>
          )}

          <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center">
            <select
              value={selectedProgramId}
              onChange={(e) => setSelectedProgramId(e.target.value)}
              className="input w-auto min-w-[260px] max-w-sm"
            >
              <option value="">Select a program</option>
              {programs
                .filter((program) => !memberships.some((m) => m.ProgramId === program.ProgramId))
                .filter((program) => !effectiveTenantId || !program.TenantId || program.TenantId.toLowerCase() === effectiveTenantId.toLowerCase())
                .map((program) => (
                  <option key={program.ProgramId} value={program.ProgramId}>
                    {program.Name}
                  </option>
                ))}
            </select>

            <button
              type="button"
              onClick={handleAddToProgram}
              disabled={!selectedProgramId || linkBusy}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-500 px-4 py-2.5 font-medium text-white transition hover:bg-brand-400 disabled:cursor-not-allowed disabled:opacity-50"
            >
              <Plus className="h-4 w-4" />
              {linkBusy ? 'Saving...' : 'Add to program'}
            </button>
          </div>

          {linkError && <p className="mt-3 text-sm text-red-300">{linkError}</p>}
        </div>

        <div
          className={
            document.ExecutiveSummary
              ? 'mt-10 rounded-2xl border border-brand-500/20 bg-gradient-to-br from-brand-500/10 via-white/[0.02] to-accent-500/10 p-6'
              : 'mt-10 rounded-2xl border border-white/10 bg-white/[0.02] p-6'
          }
        >
          <h2 className="text-lg font-semibold text-white">Document summary</h2>
          {document.ExecutiveSummary ? (
            <p className="mt-3 whitespace-pre-line text-sm leading-relaxed text-slate-300">{document.ExecutiveSummary}</p>
          ) : (
            <p className="mt-3 text-slate-300">
              This document contains {document.Sections.length} section{document.Sections.length === 1 ? '' : 's'} ready to be reviewed and studied in order.
            </p>
          )}
          {document.PedagogicalAudience && (
            <p className="mt-2 text-sm text-slate-400">Audience: {document.PedagogicalAudience}</p>
          )}
          {document.AssessmentQuestionType && (
            <p className="mt-2 text-sm text-slate-400">Assessment: {document.AssessmentQuestionType}</p>
          )}
        </div>
      </div>

      {showDeleteModal && (
        <DeleteCapabilityModal
          capabilityName={document.DocumentName}
          capabilityId={document.StructuredLearningDocumentId}
          onConfirm={handleDeleteDocument}
          onCancel={() => setShowDeleteModal(false)}
        />
      )}
    </div>
  );
}

function getCategoryBadgeText(category: string): string {
  switch (category) {
    case 'job-skills':
      return 'Técnica';
    case 'soft-skills':
      return 'Blanda';
    case 'department-processes':
      return 'Proceso Dpto';
    case 'hr-processes':
      return 'Proceso RRHH';
    default:
      return category;
  }
}

function getCategoryLabel(category: string | null | undefined, t: (key: string) => string): string {
  if (!category) return '';
  switch (category) {
    case 'job-skills':
      return t('newStructuredLearning.categoryFilterTechnical');
    case 'soft-skills':
      return t('newStructuredLearning.categoryFilterSoft');
    case 'department-processes':
      return t('newStructuredLearning.categoryFilterDept');
    case 'hr-processes':
      return t('newStructuredLearning.categoryFilterHR');
    default:
      return category;
  }
}

function InfoCard({ icon: Icon, label, value }: { icon: typeof Layers; label: string; value: string | number }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4">
      <div className="flex items-center gap-2 text-slate-400">
        <Icon className="h-4 w-4" />
        <span className="text-xs uppercase tracking-wide">{label}</span>
      </div>
      <div className="mt-3 text-xl font-semibold text-white">{value}</div>
    </div>
  );
}
