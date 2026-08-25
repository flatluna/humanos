import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import {
  ArrowLeft,
  ArrowRight,
  FileText,
  Globe,
  GraduationCap,
  Layers,
  MessageSquareText,
  Network,
  RefreshCw,
  SlidersHorizontal,
  Wand2,
  ListTree,
  Sparkles,
  Trash2,
} from 'lucide-react';
import {
  startStructuredLearningExtraction,
  type StructuredLearningAssessmentQuestionType,
  type StructuredLearningExtractionRunStatus,
} from '../lib/api/structuredLearningApi';
import {
  readFileAsBase64,
  DEFAULT_STUDIO_TENANT_ID,
  previewCapabilityOutline,
  refineOutlineTitles,
  type CapabilityOutlineChapter,
  type CapabilityOutlineSource,
  type DocumentTypeName,
} from '../lib/api/pdfPipelineApi';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningTopics, type StructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';
import { getSkillsSummary, type SkillSummaryDto } from '../lib/api/skillsApi';
import { getJobRoles, type JobRoleDto } from '../lib/api/jobRolesApi';
import StepIndicator from '../components/StepIndicator';
import TypeOptionCard from '../components/TypeOptionCard';
import FileDropzone from '../components/FileDropzone';
import SearchableCombobox from '../components/SearchableCombobox';
import { useAuth } from '../auth/AuthContext';

const STEPS_KEY = 'newStructuredLearning.steps';

/** Same 4 employer career-plan training tracks as NewCapabilityPage.tsx —
 * kept here as a literal copy rather than a shared import so this page
 * never depends on NewCapabilityPage.tsx (see this file's own doc comment
 * below). */
const CAREER_CATEGORIES = [
  { value: 'job-skills' as const, labelKey: 'newCapability.careerCategoryJobSkills' },
  { value: 'soft-skills' as const, labelKey: 'newCapability.careerCategorySoftSkills' },
  { value: 'department-processes' as const, labelKey: 'newCapability.careerCategoryDepartmentProcesses' },
  { value: 'hr-processes' as const, labelKey: 'newCapability.careerCategoryHrProcesses' },
];

/** Document Type Detection Engine (2026-08-17) — the 8 structural types the
 * backend classifier can recommend, shown as override options once a
 * recommendation comes back from previewCapabilityOutline. */
const DOCUMENT_TYPE_OPTIONS: DocumentTypeName[] = [
  'Contract',
  'Law',
  'Policy',
  'Procedure',
  'AcademicWithIndex',
  'AcademicNarrative',
  'ResearchPaper',
  'Unknown',
];

function jobRoleOptionLabel(role: JobRoleDto): string {
  const withLevel = role.LevelLabel ? `${role.Title} (${role.LevelLabel})` : role.Title;
  return `${withLevel} — ${role.DepartmentName}`;
}

/**
 * Studio's THIRD course-creation method (2026-08-10), Step 2 only so far:
 * new, standalone page — mirrors NewCapabilityPage.tsx's exact 5 steps
 * (Basic info / Material type / Upload content / Review / Learning path
 * design) visually, but is a SEPARATE component that never touches
 * NewCapabilityPage.tsx/NewTraditionalCoursePage.tsx. Calls the new
 * structuredLearningApi endpoints instead of the Graph/Traditional ones.
 */
export default function NewStructuredLearningPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;

  const [step, setStep] = useState(1);

  const [documentName, setDocumentName] = useState('');
  const [languageCode, setLanguageCode] = useState<'es' | 'en'>('es');

  // Step 1 config (2026-08-14): domain/subject + career-skill linking and
  // initial pedagogy defaults — same fields/conventions as NewCapabilityPage,
  // now available up front instead of only after Step 2 extraction.
  const isCompanyAccount = !!user?.tenantId;
  // Global/personal visibility (2026-08-19) — same convention as
  // NewCapabilityPage's isGlobal: defaults to false (Personal/tenant-only).
  const [isGlobal, setIsGlobal] = useState(false);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [subjectId, setSubjectId] = useState<string>('');
  const [topics, setTopics] = useState<StructuredLearningTopic[]>([]);
  const [topicId, setTopicId] = useState<string>('');
  const [pedagogicalAudience, setPedagogicalAudience] = useState<'Standard' | 'YoungLearner'>('Standard');
  const [maxDeepDiveCardsPerNode, setMaxDeepDiveCardsPerNode] = useState<number>(3);
  const [includeProductionStep, setIncludeProductionStep] = useState(true);
  const [includeHypothesisStep, setIncludeHypothesisStep] = useState(true);
  const [includeRecallStep, setIncludeRecallStep] = useState(true);
  const [recallItemsRequired, setRecallItemsRequired] = useState('');
  const [includeAssessmentStep, setIncludeAssessmentStep] = useState(true);
  const [assessmentQuestionType, setAssessmentQuestionType] = useState<StructuredLearningAssessmentQuestionType>('MultipleChoice');
  const [assessmentQuestionsPerRound, setAssessmentQuestionsPerRound] = useState('');

  // Knowledge Graph opt-in (2026-08-14): split into two independent
  // phases — entities (nodes) and connections (edges) — the designer can
  // skip either from Step 1 instead of it always running automatically.
  const [includeKnowledgeGraphEntities, setIncludeKnowledgeGraphEntities] = useState(true);
  const [includeKnowledgeGraphConnections, setIncludeKnowledgeGraphConnections] = useState(true);

  const [careerCategory, setCareerCategory] = useState<string>('');
  const [targetSkill, setTargetSkill] = useState<string>('');
  const [catalogSkills, setCatalogSkills] = useState<SkillSummaryDto[]>([]);
  const [jobRoles, setJobRoles] = useState<JobRoleDto[]>([]);
  const [targetJobRoleSearch, setTargetJobRoleSearch] = useState('');

  useEffect(() => {
    getSubjects().then(setSubjects).catch(() => setSubjects([]));
  }, []);

  // Load the types belonging to the selected domain, matching the domain
  // detail page's catalog.
  useEffect(() => {
    setTopicId('');
    if (!subjectId) {
      setTopics([]);
      return;
    }
    let cancelled = false;
    getStructuredLearningTopics(effectiveTenantId, subjectId)
      .then((list) => { if (!cancelled) setTopics(list); })
      .catch(() => { if (!cancelled) setTopics([]); });
    return () => { cancelled = true; };
  }, [effectiveTenantId, subjectId]);

  useEffect(() => {
    if (!careerCategory || careerCategory === 'job-skills') {
      setCatalogSkills([]);
      return;
    }
    let cancelled = false;
    getSkillsSummary(effectiveTenantId, careerCategory)
      .then((skills) => {
        if (!cancelled) setCatalogSkills(skills);
      })
      .catch(() => {
        if (!cancelled) setCatalogSkills([]);
      });
    return () => {
      cancelled = true;
    };
  }, [careerCategory, effectiveTenantId]);

  useEffect(() => {
    if (careerCategory !== 'job-skills') {
      setJobRoles([]);
      return;
    }
    let cancelled = false;
    getJobRoles(effectiveTenantId)
      .then((roles) => {
        if (!cancelled) setJobRoles(roles);
      })
      .catch(() => {
        if (!cancelled) setJobRoles([]);
      });
    return () => {
      cancelled = true;
    };
  }, [careerCategory, effectiveTenantId]);

  useEffect(() => {
    if (!careerCategory) {
      setTargetSkill('');
    }
    if (careerCategory !== 'job-skills') {
      setTargetJobRoleSearch('');
    }
  }, [careerCategory]);

  const selectedJobRole = useMemo(
    () => jobRoles.find((role) => jobRoleOptionLabel(role) === targetJobRoleSearch) ?? null,
    [jobRoles, targetJobRoleSearch],
  );

  const skipFirstJobRoleReset = useRef(true);
  useEffect(() => {
    if (skipFirstJobRoleReset.current) {
      skipFirstJobRoleReset.current = false;
      return;
    }
    setTargetSkill('');
  }, [selectedJobRole?.JobRoleId]);

  const [sourceType, setSourceType] = useState<'pdf' | 'text'>('pdf');
  const [file, setFile] = useState<File | null>(null);
  const [ideaText, setIdeaText] = useState('');

  // Same "Learning path design" outline preview the Graph/Traditional
  // wizards already use — reused as-is, no changes to that endpoint.
  const [outline, setOutline] = useState<CapabilityOutlineChapter[] | null>(null);
  const [outlineSource, setOutlineSource] = useState<CapabilityOutlineSource | null>(null);
  const [outlineLoading, setOutlineLoading] = useState(false);
  const [outlineError, setOutlineError] = useState<string | null>(null);
  const [expandedDocumentText, setExpandedDocumentText] = useState<string | null>(null);

  // Document Type Detection Engine (2026-08-17, Paso 3a) — the engine's
  // recommendation from the same outline-preview call, plus the
  // designer's own final choice (defaults to the recommendation, editable
  // via a dropdown; re-fetches the outline using that type's dedicated
  // splitting strategy when changed).
  const [detectedDocumentType, setDetectedDocumentType] = useState<DocumentTypeName | null>(null);
  const [detectedDocumentTypeConfidence, setDetectedDocumentTypeConfidence] = useState<number | null>(null);
  const [confirmedDocumentType, setConfirmedDocumentType] = useState<DocumentTypeName | null>(null);

  // Opt-in "Generar contenido con IA" (2026-08-17) — for sections flagged
  // HasSparseContent (bare outline entries with no real developed
  // content). Never automatic; the designer must explicitly opt in.
  const [expandSparseContent, setExpandSparseContent] = useState(false);
  const [contentExpansionModel, setContentExpansionModel] = useState<'Main' | 'Economy'>('Main');
  const [useWebGroundingForExpansion, setUseWebGroundingForExpansion] = useState(false);

  // Size estimate (2026-08-17) — document-wide totals from the same
  // outline-preview call, shown so the designer can gauge cost/scope
  // before approving and paying for real generation.
  const [outlineTotalCharacters, setOutlineTotalCharacters] = useState<number | null>(null);
  const [outlineTotalTokens, setOutlineTotalTokens] = useState<number | null>(null);
  const [refiningTitles, setRefiningTitles] = useState(false);
  const [refineTitlesError, setRefineTitlesError] = useState<string | null>(null);
  const [refineTitlesInstruction, setRefineTitlesInstruction] = useState('');

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const canAdvanceFromStep1 = documentName.trim().length > 0;
  const canAdvanceFromStep2 = !!subjectId && !!topicId;
  const canAdvanceFromStep8 = sourceType === 'pdf' ? !!file : ideaText.trim().length > 0;

  /** Runs the cheap detection-only outline preview — same generic
   * endpoint the Graph/Traditional wizards use. Passes documentType when
   * the designer already picked one (overrideDocumentType, e.g. from the
   * Step 5 dropdown) so the backend uses that type's dedicated
   * chapter-splitting strategy instead of re-classifying. */
  async function loadOutlinePreview(overrideDocumentType?: DocumentTypeName) {
    setOutlineLoading(true);
    setOutlineError(null);
    setExpandedDocumentText(null);
    try {
      let result;
      if (sourceType === 'pdf' && file) {
        const contentBase64 = await readFileAsBase64(file);
        result = await previewCapabilityOutline({ fileName: file.name, contentBase64, documentType: overrideDocumentType });
      } else if (sourceType === 'text' && ideaText.trim()) {
        result = await previewCapabilityOutline({
          text: ideaText.trim(),
          capabilityName: documentName.trim(),
          languageCode,
          documentType: overrideDocumentType,
        });
        setExpandedDocumentText(result.ExpandedDocumentText ?? null);
      } else {
        setOutline(null);
        setOutlineSource(null);
        return;
      }

      setOutlineSource(result.Source);
      if (result.DetectedDocumentType) {
        setDetectedDocumentType(result.DetectedDocumentType);
        setDetectedDocumentTypeConfidence(result.DetectedDocumentTypeConfidence);
        setConfirmedDocumentType((previous) => previous ?? result.DetectedDocumentType);
      } else if (overrideDocumentType) {
        setConfirmedDocumentType(overrideDocumentType);
      }
      if (result.Source === 'None' || result.Chapters.length === 0) {
        setOutline(null);
        setOutlineError(result.RejectionReason ?? t('newStructuredLearning.outlineNotFound'));
        setOutlineTotalCharacters(null);
        setOutlineTotalTokens(null);
      } else {
        setOutline(result.Chapters);
        setOutlineTotalCharacters(result.TotalCharacters);
        setOutlineTotalTokens(result.TotalTokens);
      }
    } catch (err) {
      setOutline(null);
      setOutlineSource(null);
      setOutlineError(err instanceof Error ? err.message : t('newStructuredLearning.outlineLoadError'));
    } finally {
      setOutlineLoading(false);
    }
  }

  async function goToDesignStep() {
    setStep(6);
    if (outline === null && outlineSource === null && !outlineLoading) {
      await loadOutlinePreview();
    }
  }

  /** Lets the designer hand-edit a detected/proposed chapter title (2026-08-17) —
   * safe because StartMarker (used to locate the real text) is stored
   * separately from Title and never touched here; the backend uses
   * whatever Title is approved VERBATIM regardless of edits. */
  function updateChapterTitle(chapterIndex: number, title: string) {
    setOutline((previous) =>
      previous
        ? previous.map((chapter, index) => (index === chapterIndex ? { ...chapter, Title: title } : chapter))
        : previous,
    );
  }

  function updateNodeTitle(chapterIndex: number, nodeIndex: number, title: string) {
    setOutline((previous) =>
      previous
        ? previous.map((chapter, cIndex) =>
            cIndex === chapterIndex
              ? { ...chapter, Nodes: chapter.Nodes.map((node, nIndex) => (nIndex === nodeIndex ? { ...node, Title: title } : node)) }
              : chapter,
          )
        : previous,
    );
  }

  /** Lets the designer remove a whole chapter/level (2026-08-17) — for
   * building a shorter course than what was auto-detected. Never touches
   * the backend directly; just removes it from the outline the designer
   * will approve. */
  function deleteChapter(chapterIndex: number) {
    setOutline((previous) => (previous ? previous.filter((_, index) => index !== chapterIndex) : previous));
  }

  /** Removes one section/article/node from its chapter — same rationale
   * as deleteChapter, at finer granularity. */
  function deleteNode(chapterIndex: number, nodeIndex: number) {
    setOutline((previous) =>
      previous
        ? previous.map((chapter, cIndex) =>
            cIndex === chapterIndex ? { ...chapter, Nodes: chapter.Nodes.filter((_, nIndex) => nIndex !== nodeIndex) } : chapter,
          )
        : previous,
    );
  }

  /** Opt-in "Mejorar títulos con IA" (2026-08-17) — the only LLM call in
   * this otherwise-free Paso 5 flow, only when the designer clicks it.
   * One batched call regardless of chapter/node count. */
  async function handleRefineTitles() {
    if (!outline || outline.length === 0) return;
    setRefiningTitles(true);
    setRefineTitlesError(null);
    try {
      const refined = await refineOutlineTitles(confirmedDocumentType ?? detectedDocumentType, outline, refineTitlesInstruction);
      setOutline(refined);
    } catch (err) {
      setRefineTitlesError(err instanceof Error ? err.message : t('newStructuredLearning.refineTitlesError'));
    } finally {
      setRefiningTitles(false);
    }
  }

  async function handleSubmit() {
    const sessionTenantId = user?.tenantId?.trim();
    if (!sessionTenantId) {
      setSubmitError(t('newStructuredLearning.errorMissingTenant'));
      return;
    }

    if (!outline || outline.length === 0) {
      setSubmitError(t('newStructuredLearning.errorMissingOutline'));
      return;
    }

    setSubmitting(true);
    setSubmitError(null);
    try {
      const basePayload = {
        tenantId: sessionTenantId,
        createdByOid: user?.oid ?? null,
        createdByEmail: user?.email ?? null,
        isGlobal,
        documentName: documentName.trim(),
        languageCode,
        approvedOutline: outline,
        preExpandedDocumentText: sourceType === 'text' ? expandedDocumentText : null,
        subjectId: subjectId || null,
        topicId: topicId || null,
        category: careerCategory || null,
        targetSkill: careerCategory ? targetSkill.trim() || null : null,
        pedagogicalAudience,
        maxDeepDiveCardsPerNode: Math.max(1, Math.min(10, maxDeepDiveCardsPerNode)),
        includeProductionStep,
        includeHypothesisStep,
        includeRecallStep,
        recallItemsRequired: recallItemsRequired.trim() === '' ? null : Number.parseInt(recallItemsRequired, 10),
        includeAssessmentStep,
        assessmentQuestionType,
        assessmentQuestionsPerRound:
          assessmentQuestionsPerRound.trim() === '' ? null : Number.parseInt(assessmentQuestionsPerRound, 10),
        includeKnowledgeGraphEntities,
        includeKnowledgeGraphConnections: includeKnowledgeGraphEntities && includeKnowledgeGraphConnections,
        confirmedDocumentType,
        expandSparseContent,
        contentExpansionModel,
        useWebGroundingForExpansion,
      };

      let run: StructuredLearningExtractionRunStatus;
      if (sourceType === 'pdf' && file) {
        run = await startStructuredLearningExtraction({
          ...basePayload,
          fileName: file.name,
          contentBase64: await readFileAsBase64(file),
        });
      } else {
        run = await startStructuredLearningExtraction({
          ...basePayload,
          description: ideaText.trim(),
        });
      }

      navigate(`/structured-learning-runs/${run.RunId}`);
    } catch (err) {
      setSubmitError(
        err instanceof Error && err.message.includes('409')
          ? t('newStructuredLearning.errorAlreadyGenerating')
          : err instanceof Error
            ? err.message
            : t('newStructuredLearning.errorGenericStart'),
      );
      setSubmitting(false);
    }
  }

  const steps = t(STEPS_KEY, { returnObjects: true }) as string[];

  return (
    <div className="mx-auto max-w-3xl px-6 py-12">
      <h1 className="mb-8 whitespace-nowrap bg-gradient-to-r from-emerald-300 via-sky-400 to-violet-400 bg-clip-text text-2xl font-extrabold tracking-tight text-transparent sm:text-3xl">
        {t('newStructuredLearning.studioBuilderTitle')}
      </h1>
      <div className="mb-10">
        <StepIndicator steps={steps} currentStep={step} />
      </div>

      {step === 1 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step1Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step1Subtitle')}</p>
          </div>

          <Field label={t('newStructuredLearning.documentNameLabel')}>
            <input
              type="text"
              value={documentName}
              onChange={(e) => setDocumentName(e.target.value)}
              placeholder={t('newStructuredLearning.documentNamePlaceholder')}
              className="input"
            />
          </Field>

          <Field label={t('newStructuredLearning.contentLanguageLabel')}>
            <div className="grid grid-cols-2 gap-2">
              {(
                [
                  { code: 'es' as const, label: t('common.languageSpanish') },
                  { code: 'en' as const, label: t('common.languageEnglish') },
                ]
              ).map((option) => {
                const active = languageCode === option.code;
                return (
                  <button
                    key={option.code}
                    type="button"
                    onClick={() => setLanguageCode(option.code)}
                    className={`rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                      active
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    {option.label}
                  </button>
                );
              })}
            </div>
          </Field>

          <div className="border-t border-white/10 pt-6">
            <h3 className="text-base font-semibold text-white">{t('newStructuredLearning.step2Title')}</h3>
            <p className="mt-1 text-sm text-slate-400">{t('newStructuredLearning.step2Subtitle')}</p>
          </div>

          <Field label={t('newCapability.domainLabel')} required>
            <SearchableCombobox
              value={subjects.find((subject) => subject.SubjectId === subjectId)?.Name ?? ''}
              onChange={(value) => {
                if (!value) {
                  setSubjectId('');
                  setTopicId('');
                }
              }}
              onSelect={(option) => {
                if (option.id !== subjectId) setTopicId('');
                setSubjectId(option.id);
              }}
              options={subjects.map((subject) => ({ id: subject.SubjectId, label: subject.Name }))}
              placeholder={t('newStructuredLearning.domainPlaceholder')}
              emptyOptionsText={t('newStructuredLearning.domainNoOptions')}
            />
          </Field>

          {subjectId && (
            <Field
              label={t('newStructuredLearning.domainTypeLabel')}
              hint={t('newStructuredLearning.domainTypeHint')}
              required
            >
              <SearchableCombobox
                value={topics.find((topic) => topic.StructuredLearningTopicId === topicId)?.Name ?? ''}
                onChange={(value) => {
                  if (!value) setTopicId('');
                }}
                onSelect={(option) => setTopicId(option.id)}
                options={topics.map((topic) => ({ id: topic.StructuredLearningTopicId, label: topic.Name }))}
                placeholder={t('newStructuredLearning.domainTypePlaceholder')}
                emptyOptionsText={t('newStructuredLearning.domainTypeNoOptions')}
              />
            </Field>
          )}

          <div className="border-t border-white/10 pt-6">
            <h3 className="text-base font-semibold text-white">{t('newStructuredLearning.step3Title')}</h3>
            <p className="mt-1 text-sm text-slate-400">{t('newStructuredLearning.step3Subtitle')}</p>
          </div>

          {!isCompanyAccount && (
            <p className="rounded-xl border border-white/10 bg-white/[0.02] p-4 text-sm text-slate-400">
              {t('newStructuredLearning.step3PersonalAccountHint')}
            </p>
          )}

          {isCompanyAccount && (
            <Field label={t('newCapability.visibilityLabel')} hint={t('newCapability.visibilityHint')}>
              <div className="grid grid-cols-2 gap-2">
                {(
                  [
                    { value: false, label: t('newCapability.visibilityPersonal') },
                    { value: true, label: t('newCapability.visibilityGlobal') },
                  ]
                ).map((option) => {
                  const active = isGlobal === option.value;
                  return (
                    <button
                      key={String(option.value)}
                      type="button"
                      onClick={() => setIsGlobal(option.value)}
                      className={`flex items-center justify-center gap-2 rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                        active
                          ? 'border-brand-400/60 bg-brand-500/10 text-white'
                          : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                      }`}
                    >
                      <Globe className="h-4 w-4 shrink-0" />
                      {option.label}
                    </button>
                  );
                })}
              </div>
            </Field>
          )}

          {isCompanyAccount && (
            <>
              <Field label={t('newCapability.careerCategoryLabel')} hint={t('newCapability.careerCategoryHint')}>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                  <button
                    type="button"
                    onClick={() => setCareerCategory('')}
                    className={`rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                      careerCategory === ''
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    {t('newCapability.careerCategoryNone')}
                  </button>
                  {CAREER_CATEGORIES.map((option) => {
                    const active = careerCategory === option.value;
                    return (
                      <button
                        key={option.value}
                        type="button"
                        onClick={() => setCareerCategory(active ? '' : option.value)}
                        className={`rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                          active
                            ? 'border-brand-400/60 bg-brand-500/10 text-white'
                            : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                        }`}
                      >
                        {t(option.labelKey)}
                      </button>
                    );
                  })}
                </div>
              </Field>

              {careerCategory === 'job-skills' && (
                <div className="space-y-4 rounded-xl border border-white/10 bg-white/[0.02] p-4">
                  <Field label={t('newCapability.targetJobRoleLabel')} hint={t('newCapability.targetJobRoleHint')}>
                    <SearchableCombobox
                      value={targetJobRoleSearch}
                      onChange={setTargetJobRoleSearch}
                      options={jobRoles.map((role) => ({ id: role.JobRoleId, label: jobRoleOptionLabel(role) }))}
                      placeholder={t('newCapability.targetJobRolePlaceholder')}
                      emptyOptionsText={t('newCapability.targetJobRoleNoOptions')}
                    />
                  </Field>

                  <Field
                    label={t('newCapability.targetSkillLabel')}
                    hint={selectedJobRole ? t('newCapability.targetSkillRoleHint') : undefined}
                  >
                    <SearchableCombobox
                      value={targetSkill}
                      onChange={setTargetSkill}
                      disabled={!selectedJobRole}
                      options={(selectedJobRole?.TechnicalSkills ?? []).map((skill) => ({ id: skill, label: skill }))}
                      placeholder={t('newCapability.targetSkillPlaceholder')}
                      emptyOptionsText={t('newCapability.targetSkillNoMatches')}
                    />
                    {!selectedJobRole && (
                      <p className="mt-1 text-xs text-slate-500">{t('newCapability.targetSkillNoRoleSelected')}</p>
                    )}
                  </Field>
                </div>
              )}

              {careerCategory && careerCategory !== 'job-skills' && (
                <div className="space-y-4 rounded-xl border border-white/10 bg-white/[0.02] p-4">
                  <Field label={t('newCapability.targetSkillLabel')} hint={t('newCapability.targetSkillCatalogHint')}>
                    <SearchableCombobox
                      value={targetSkill}
                      onChange={setTargetSkill}
                      options={catalogSkills.map((skill) => ({ id: skill.SkillId, label: skill.Name, meta: `${skill.JobRoleCount}` }))}
                      placeholder={t('newCapability.targetSkillPlaceholder')}
                      emptyOptionsText={t('newCapability.targetSkillNoMatches')}
                    />
                  </Field>
                </div>
              )}
            </>
          )}

          <WizardNav onNext={() => setStep(2)} nextDisabled={!canAdvanceFromStep1 || !canAdvanceFromStep2} />
        </div>
      )}

      {step === 2 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step4Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step4Subtitle')}</p>
          </div>

          <Field label={t('newCapability.pedagogicalAudienceLabel')} hint={t('newCapability.pedagogicalAudienceHint')}>
            <div className="grid grid-cols-2 gap-2">
              {(
                [
                  { value: 'Standard' as const, label: t('newCapability.audienceStandard') },
                  { value: 'YoungLearner' as const, label: t('newCapability.audienceYoungLearner') },
                ]
              ).map((option) => {
                const active = pedagogicalAudience === option.value;
                return (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => setPedagogicalAudience(option.value)}
                    className={`rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                      active
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    {option.label}
                  </button>
                );
              })}
            </div>
          </Field>

          <Field label={t('newCapability.maxDeepDiveCardsLabel')} hint={t('newCapability.maxDeepDiveCardsHint')}>
            <input
              type="number"
              min={1}
              max={10}
              step={1}
              value={maxDeepDiveCardsPerNode}
              onChange={(e) => {
                const parsed = Number.parseInt(e.target.value, 10);
                setMaxDeepDiveCardsPerNode(Number.isNaN(parsed) ? 1 : Math.max(1, Math.min(10, parsed)));
              }}
              className="input"
            />
          </Field>

          <Field label={t('newCapability.includeProductionStepLabel')} hint={t('newCapability.includeProductionStepHint')}>
            <label className="flex items-center gap-2 text-sm text-slate-300">
              <input
                type="checkbox"
                checked={includeProductionStep}
                onChange={(e) => setIncludeProductionStep(e.target.checked)}
                className="h-4 w-4 rounded border-white/20 bg-white/[0.02]"
              />
              {includeProductionStep
                ? t('newCapability.includeProductionStepOn')
                : t('newCapability.includeProductionStepOff')}
            </label>
          </Field>

          <div className="border-t border-white/10 pt-6">
            <h3 className="text-base font-semibold text-white">{t('newStructuredLearning.step5Title')}</h3>
            <p className="mt-1 text-sm text-slate-400">{t('newStructuredLearning.step5Subtitle')}</p>
          </div>

          <div className="space-y-4 rounded-xl border-2 border-brand-400/50 bg-brand-500/[0.07] p-4 shadow-[0_0_0_1px_rgba(99,102,241,0.15)]">
            <div className="flex items-center gap-2">
              <Sparkles className="h-4 w-4 shrink-0 text-brand-300" />
              <p className="text-xs font-semibold uppercase tracking-wide text-brand-300">
                {t('newStructuredLearning.accessibilityStepsBadge')}
              </p>
            </div>
            <p className="text-xs text-slate-400">{t('newStructuredLearning.accessibilityStepsHint')}</p>

            <Field label={t('newStructuredLearning.includeHypothesisStepLabel')} hint={t('newStructuredLearning.includeHypothesisStepHint')}>
              <label className="flex items-center gap-2 text-sm text-slate-300">
                <input
                  type="checkbox"
                  checked={includeHypothesisStep}
                  onChange={(e) => setIncludeHypothesisStep(e.target.checked)}
                  className="h-4 w-4 rounded border-white/20 bg-white/[0.02]"
                />
                {t('newStructuredLearning.includeHypothesisStepToggle')}
              </label>
            </Field>

            <Field label={t('newStructuredLearning.includeRecallStepLabel')}>
              <label className="flex items-center gap-2 text-sm text-slate-300">
                <input
                  type="checkbox"
                  checked={includeRecallStep}
                  onChange={(e) => setIncludeRecallStep(e.target.checked)}
                  className="h-4 w-4 rounded border-white/20 bg-white/[0.02]"
                />
                {t('newStructuredLearning.includeRecallStepToggle')}
              </label>
            </Field>

            <Field label={t('newCapability.recallItemsRequiredLabel')} hint={t('newCapability.recallItemsRequiredHint')}>
              <input
                type="number"
                min={1}
                max={10}
                step={1}
                placeholder={t('newCapability.useDefaultPlaceholder')}
                value={recallItemsRequired}
                onChange={(e) => setRecallItemsRequired(e.target.value)}
                disabled={!includeRecallStep}
                className="input"
              />
            </Field>

            <Field label={t('newStructuredLearning.includeAssessmentStepLabel')}>
              <label className="flex items-center gap-2 text-sm text-slate-300">
                <input
                  type="checkbox"
                  checked={includeAssessmentStep}
                  onChange={(e) => setIncludeAssessmentStep(e.target.checked)}
                  className="h-4 w-4 rounded border-white/20 bg-white/[0.02]"
                />
                {t('newStructuredLearning.includeAssessmentStepToggle')}
              </label>
            </Field>
          </div>

          <Field label={t('newStructuredLearning.assessmentQuestionTypeLabel')}>
            <div className="grid grid-cols-2 gap-2" style={includeAssessmentStep ? undefined : { opacity: 0.5, pointerEvents: 'none' }}>
              {(
                [
                  { value: 'MultipleChoice' as const, label: t('newStructuredLearning.assessmentQuestionTypeMultipleChoice') },
                  { value: 'OpenEnded' as const, label: t('newStructuredLearning.assessmentQuestionTypeOpenEnded') },
                ]
              ).map((option) => {
                const active = assessmentQuestionType === option.value;
                return (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => setAssessmentQuestionType(option.value)}
                    className={`rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                      active
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    {option.label}
                  </button>
                );
              })}
            </div>
          </Field>

          <Field
            label={t('newCapability.assessmentQuestionsPerRoundLabel')}
            hint={t('newCapability.assessmentQuestionsPerRoundHint')}
          >
            <input
              type="number"
              min={1}
              max={10}
              step={1}
              placeholder={t('newCapability.useDefaultPlaceholder')}
              value={assessmentQuestionsPerRound}
              onChange={(e) => setAssessmentQuestionsPerRound(e.target.value)}
              disabled={!includeAssessmentStep}
              className="input"
            />
          </Field>

          <div className="border-t border-white/10 pt-6">
            <h3 className="text-base font-semibold text-white">{t('newStructuredLearning.step6Title')}</h3>
            <p className="mt-1 text-sm text-slate-400">{t('newStructuredLearning.step6Subtitle')}</p>
          </div>

          <Field label={t('newStructuredLearning.knowledgeGraphEntitiesLabel')} hint={t('newStructuredLearning.knowledgeGraphEntitiesHint')}>
            <label className="flex items-center gap-2 text-sm text-slate-300">
              <input
                type="checkbox"
                checked={includeKnowledgeGraphEntities}
                onChange={(e) => setIncludeKnowledgeGraphEntities(e.target.checked)}
                className="h-4 w-4 rounded border-white/20 bg-white/[0.02]"
              />
              {t('newStructuredLearning.knowledgeGraphEntitiesToggle')}
            </label>
          </Field>

          <Field label={t('newStructuredLearning.knowledgeGraphConnectionsLabel')} hint={t('newStructuredLearning.knowledgeGraphConnectionsHint')}>
            <label className="flex items-center gap-2 text-sm text-slate-300">
              <input
                type="checkbox"
                checked={includeKnowledgeGraphConnections}
                onChange={(e) => setIncludeKnowledgeGraphConnections(e.target.checked)}
                disabled={!includeKnowledgeGraphEntities}
                className="h-4 w-4 rounded border-white/20 bg-white/[0.02] disabled:opacity-40"
              />
              {t('newStructuredLearning.knowledgeGraphConnectionsToggle')}
            </label>
          </Field>

          <WizardNav onBack={() => setStep(1)} onNext={() => setStep(3)} />
        </div>
      )}

      {step === 3 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step7Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step7Subtitle')}</p>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <TypeOptionCard
              icon={FileText}
              title={t('newStructuredLearning.materialTypes.pdfTitle')}
              description={t('newStructuredLearning.materialTypes.pdfDescription')}
              selected={sourceType === 'pdf'}
              onClick={() => setSourceType('pdf')}
            />
            <TypeOptionCard
              icon={MessageSquareText}
              title={t('newStructuredLearning.materialTypes.textTitle')}
              description={t('newStructuredLearning.materialTypes.textDescription')}
              selected={sourceType === 'text'}
              onClick={() => setSourceType('text')}
            />
          </div>

          <WizardNav onBack={() => setStep(2)} onNext={() => setStep(4)} />
        </div>
      )}

      {step === 4 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step8Title')}</h2>
            <p className="mt-1 text-slate-400">
              {sourceType === 'pdf'
                ? t('newStructuredLearning.step8SubtitlePdf')
                : t('newStructuredLearning.step8SubtitleText')}
            </p>
          </div>

          {sourceType === 'pdf' && <FileDropzone file={file} onFileSelected={setFile} />}

          {sourceType === 'text' && (
            <Field label={t('newStructuredLearning.ideaLabel')}>
              <textarea
                value={ideaText}
                onChange={(e) => setIdeaText(e.target.value)}
                rows={8}
                placeholder={t('newStructuredLearning.ideaPlaceholder')}
                className="input resize-none"
              />
            </Field>
          )}

          <WizardNav onBack={() => setStep(3)} onNext={() => setStep(5)} nextDisabled={!canAdvanceFromStep8} />
        </div>
      )}

      {step === 5 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step9Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step9Subtitle')}</p>
          </div>

          <div className="space-y-2 rounded-2xl border border-white/10 bg-white/[0.03] p-4">
            <SummaryRow label={t('newStructuredLearning.summaryDocumentName')} value={documentName} />
            <SummaryRow
              label={t('newStructuredLearning.summaryLanguage')}
              value={languageCode === 'en' ? t('common.languageEnglish') : t('common.languageSpanish')}
            />
            <SummaryRow
              label={t('newStructuredLearning.summarySource')}
              value={sourceType === 'pdf' ? t('newStructuredLearning.sourcePdf') : t('newStructuredLearning.sourceText')}
            />
          </div>

          <WizardNav onBack={() => setStep(4)} onNext={goToDesignStep} />
        </div>
      )}

      {step === 6 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step10Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step10Subtitle')}</p>
          </div>

          {outlineLoading && (
            <div className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.03] p-6 text-sm text-slate-300">
              <RefreshCw className="h-4 w-4 animate-spin text-brand-400" />
              {t('newStructuredLearning.outlineLoading')}
            </div>
          )}

          {!outlineLoading && outline && outline.length > 0 && (
            <>
              {detectedDocumentType && (
                <div className="rounded-xl border border-white/10 bg-white/[0.03] p-3 text-xs">
                  <div className="flex flex-wrap items-center gap-2 text-slate-300">
                    <span className="font-medium text-white">{t('newStructuredLearning.documentTypeLabel')}:</span>
                    <select
                      value={confirmedDocumentType ?? detectedDocumentType}
                      onChange={(e) => {
                        const nextType = e.target.value as DocumentTypeName;
                        setConfirmedDocumentType(nextType);
                        void loadOutlinePreview(nextType);
                      }}
                      className="rounded-lg border border-white/10 bg-slate-800 px-2 py-1 text-xs text-white"
                    >
                      {DOCUMENT_TYPE_OPTIONS.map((option) => (
                        <option key={option} value={option} className="bg-slate-800 text-white">
                          {option}
                        </option>
                      ))}
                    </select>
                    {detectedDocumentTypeConfidence !== null && (
                      <span className="text-slate-500">
                        ({Math.round(detectedDocumentTypeConfidence * 100)}% {t('newStructuredLearning.documentTypeConfidence')})
                      </span>
                    )}
                  </div>
                </div>
              )}

              <div
                className={`flex items-center gap-2 rounded-xl border p-3 text-xs font-medium ${
                  outlineSource === 'InferredByAI'
                    ? 'border-amber-500/20 bg-amber-500/5 text-amber-300'
                    : 'border-emerald-500/20 bg-emerald-500/5 text-emerald-300'
                }`}
              >
                {outlineSource === 'InferredByAI' ? <Wand2 className="h-4 w-4 shrink-0" /> : <ListTree className="h-4 w-4 shrink-0" />}
                {outlineSource === 'InferredByAI'
                  ? t('newStructuredLearning.outlineSourceInferred')
                  : t('newStructuredLearning.outlineSourceDetected')}
              </div>

              {outlineTotalTokens !== null && (
                <div className="rounded-xl border border-white/10 bg-white/[0.03] p-3 text-xs text-slate-400">
                  {t('newStructuredLearning.documentSizeLabel')}: {outlineTotalCharacters?.toLocaleString()}{' '}
                  {t('newStructuredLearning.documentSizeCharacters')} · {outlineTotalTokens.toLocaleString()}{' '}
                  {t('newStructuredLearning.documentSizeTokens')}
                </div>
              )}

              <div className="flex flex-wrap items-center gap-4">
                <button
                  type="button"
                  onClick={() => loadOutlinePreview()}
                  className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-slate-200"
                >
                  <RefreshCw className="h-3.5 w-3.5" />
                  {t('newStructuredLearning.outlineRegenerate')}
                </button>
              </div>
              <div className="flex flex-col gap-2">
                <textarea
                  value={refineTitlesInstruction}
                  onChange={(e) => setRefineTitlesInstruction(e.target.value)}
                  placeholder={t('newStructuredLearning.refineTitlesPlaceholder')}
                  rows={4}
                  className="w-full resize-y rounded-lg border border-white/10 bg-white/[0.03] px-2.5 py-2 text-xs text-white placeholder:text-slate-500"
                />
                <button
                  type="button"
                  onClick={handleRefineTitles}
                  disabled={refiningTitles}
                  className="inline-flex w-fit shrink-0 items-center gap-1.5 text-xs font-medium text-brand-300 hover:text-brand-200 disabled:opacity-50"
                >
                  {refiningTitles ? <RefreshCw className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
                  {t('newStructuredLearning.refineTitlesButton')}
                </button>
              </div>
              {refineTitlesError && <div className="text-xs text-red-300">{refineTitlesError}</div>}

              {outline.some((chapter) => chapter.Nodes.some((node) => node.HasSparseContent)) && (
                <div className="space-y-2 rounded-xl border border-amber-500/20 bg-amber-500/5 p-3 text-xs text-amber-200">
                  <div>
                    ⚠️ {outline.reduce((sum, c) => sum + c.Nodes.filter((n) => n.HasSparseContent).length, 0)}{' '}
                    {t('newStructuredLearning.sparseContentWarning')}
                  </div>
                  <label className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={expandSparseContent}
                      onChange={(e) => setExpandSparseContent(e.target.checked)}
                    />
                    {t('newStructuredLearning.expandSparseContentLabel')}
                  </label>
                  {expandSparseContent && (
                    <div className="flex flex-wrap items-center gap-4 pl-6">
                      <label className="flex items-center gap-2">
                        {t('newStructuredLearning.contentExpansionModelLabel')}:
                        <select
                          value={contentExpansionModel}
                          onChange={(e) => setContentExpansionModel(e.target.value as 'Main' | 'Economy')}
                          className="rounded-lg border border-white/10 bg-slate-800 px-2 py-1 text-white"
                        >
                          <option value="Main" className="bg-slate-800 text-white">
                            {t('newStructuredLearning.contentExpansionModelMain')}
                          </option>
                          <option value="Economy" className="bg-slate-800 text-white">
                            {t('newStructuredLearning.contentExpansionModelEconomy')}
                          </option>
                        </select>
                      </label>
                      <label className="flex items-center gap-2">
                        <input
                          type="checkbox"
                          checked={useWebGroundingForExpansion}
                          onChange={(e) => setUseWebGroundingForExpansion(e.target.checked)}
                        />
                        {t('newStructuredLearning.useWebGroundingLabel')}
                      </label>
                    </div>
                  )}
                </div>
              )}

              <div className="space-y-4">
                {outline.map((chapter, chapterIndex) => (
                  <div key={chapterIndex} className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                    <div className="flex items-center gap-2">
                      <input
                        type="text"
                        value={chapter.Title}
                        onChange={(e) => updateChapterTitle(chapterIndex, e.target.value)}
                        className="w-full bg-transparent font-semibold text-white outline-none focus:underline"
                      />
                      <span className="shrink-0 text-xs text-slate-500">{chapter.TotalTokens.toLocaleString()} tok</span>
                      <button
                        type="button"
                        onClick={() => deleteChapter(chapterIndex)}
                        title={t('newStructuredLearning.deleteChapterButton')}
                        className="shrink-0 text-slate-500 hover:text-red-400"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                    <div className="mt-3 space-y-2 pl-4">
                      {chapter.Nodes.map((node, nodeIndex) => (
                        <div key={nodeIndex} className="flex items-center gap-2 text-sm text-slate-300">
                          <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-brand-400" />
                          <input
                            type="text"
                            value={node.Title}
                            onChange={(e) => updateNodeTitle(chapterIndex, nodeIndex, e.target.value)}
                            className="w-full bg-transparent text-sm text-slate-300 outline-none focus:underline"
                          />
                          {node.HasSparseContent && (
                            <span className="shrink-0 rounded-full bg-amber-500/10 px-2 py-0.5 text-[10px] text-amber-300" title={t('newStructuredLearning.sparseContentBadge')}>
                              ⚠️
                            </span>
                          )}
                          <button
                            type="button"
                            onClick={() => deleteNode(chapterIndex, nodeIndex)}
                            title={t('newStructuredLearning.deleteNodeButton')}
                            className="shrink-0 text-slate-500 hover:text-red-400"
                          >
                            <Trash2 className="h-3 w-3" />
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}

          {!outlineLoading && (!outline || outline.length === 0) && (
            <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-6 text-sm text-slate-300">
              <p>{outlineError ?? t('newStructuredLearning.outlineNotFound')}</p>
              <button
                type="button"
                onClick={() => loadOutlinePreview()}
                className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.02] px-3 py-1.5 text-xs font-medium text-slate-300 hover:border-white/20"
              >
                <RefreshCw className="h-3.5 w-3.5" />
                {t('newStructuredLearning.outlineRetry')}
              </button>
            </div>
          )}

          {submitError && (
            <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-4 text-sm text-red-300">{submitError}</div>
          )}

          <WizardNav
            onBack={() => setStep(5)}
            onNext={handleSubmit}
            nextDisabled={outlineLoading || !outline || outline.length === 0 || submitting}
            nextLabel={submitting ? t('newStructuredLearning.starting') : t('newStructuredLearning.extractSections')}
            nextIcon={Sparkles}
          />
        </div>
      )}
    </div>
  );
}

function Field({ label, hint, required, children }: { label: string; hint?: string; required?: boolean; children: ReactNode }) {
  return (
    <div>
      <label className="mb-1.5 block text-sm font-medium text-slate-300">
        {label}{required && <span className="ml-1 text-red-400" aria-hidden="true">*</span>}
      </label>
      {hint && <p className="mb-1.5 text-xs text-slate-500">{hint}</p>}
      {children}
    </div>
  );
}

function SummaryRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-4 text-sm">
      <span className="text-slate-400">{label}</span>
      <span className="text-right font-medium text-white">{value}</span>
    </div>
  );
}

function WizardNav({
  onBack,
  onNext,
  nextDisabled,
  nextLabel,
  nextIcon: NextIcon = ArrowRight,
}: {
  onBack?: () => void;
  onNext: () => void;
  nextDisabled?: boolean;
  nextLabel?: string;
  nextIcon?: typeof ArrowRight;
}) {
  const { t } = useTranslation();
  const resolvedNextLabel = nextLabel ?? t('common.continue');
  return (
    <div className="flex items-center justify-between pt-4">
      {onBack ? (
        <button
          type="button"
          onClick={onBack}
          className="flex items-center gap-1.5 rounded-xl px-4 py-2.5 text-sm font-medium text-slate-400 hover:text-white"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('common.back')}
        </button>
      ) : (
        <span />
      )}
      <button
        type="button"
        onClick={onNext}
        disabled={nextDisabled}
        className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform enabled:hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-40"
      >
        {resolvedNextLabel}
        <NextIcon className="h-4 w-4" />
      </button>
    </div>
  );
}
