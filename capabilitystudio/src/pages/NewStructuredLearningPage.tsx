import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  ArrowLeft,
  ArrowRight,
  Building2,
  CheckCircle2,
  FileText,
  Globe,
  GraduationCap,
  MessageSquareText,
  RefreshCw,
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
import { getSkills, type SkillDto } from '../lib/api/skillsApi';
import StepIndicator from '../components/StepIndicator';
import TypeOptionCard from '../components/TypeOptionCard';
import FileDropzone from '../components/FileDropzone';
import SearchableCombobox from '../components/SearchableCombobox';
import { useAuth } from '../auth/AuthContext';

const STEPS_KEY = 'newStructuredLearning.steps';

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

function getCategoryBadgeText(category: string): string {
  switch (category) {
    case 'job-skills':
      return 'Habilidades Técnicas';
    case 'soft-skills':
      return 'Habilidades Blandas';
    case 'department-processes':
      return 'Procesos de Departamento';
    case 'hr-processes':
      return 'Procesos de RRHH';
    default:
      return category;
  }
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
  // Content depth (2026-09-18) — L100 (foundational) / L200 (intermediate) /
  // L300 (advanced), same convention Microsoft Learn uses for course levels.
  const [courseLevel, setCourseLevel] = useState<'L100' | 'L200' | 'L300'>('L100');
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

  // Perplexity AI web search opt-in — grounds each engram in real-time
  // web findings and saves source citations into the engram metadata.
  const [usePerplexitySearch, setUsePerplexitySearch] = useState(false);

  const [searchParams] = useSearchParams();
  const [autoLoadedPdfStatus, setAutoLoadedPdfStatus] = useState<string | null>(null);

  const [careerCategory, setCareerCategory] = useState<string>('');
  const [targetSkill, setTargetSkill] = useState<string>('');
  const [allCompanySkills, setAllCompanySkills] = useState<SkillDto[]>([]);
  const [selectedCategoryFilter, setSelectedCategoryFilter] = useState<string>('all');
  const [courseType, setCourseType] = useState<'enterprise' | 'academic'>(isCompanyAccount ? 'enterprise' : 'academic');
  const autoSetDocumentNameRef = useRef<string | null>(null);

  useEffect(() => {
    const urlSkillId = searchParams.get('skillId');
    const urlName = searchParams.get('name') || searchParams.get('targetSkill');
    const urlCategory = searchParams.get('category') || searchParams.get('careerCategory');
    const urlDocFileName = searchParams.get('docFileName') || 'document.pdf';

    if (urlName || urlSkillId) {
      setCourseType('enterprise');
      if (urlCategory) {
        setCareerCategory(urlCategory);
        setSelectedCategoryFilter(urlCategory);
      }
      if (urlName) {
        setTargetSkill(urlName);
        setDocumentName(urlName);
        autoSetDocumentNameRef.current = urlName;
      }

      if (urlSkillId) {
        setAutoLoadedPdfStatus(`Cargando documento PDF desde el Data Lake para "${urlName || 'Skill'}"...`);
        fetch(`http://localhost:7071/api/skills/${urlSkillId}/document`)
          .then(async (res) => {
            if (!res.ok) throw new Error('No PDF found');
            const blob = await res.blob();
            if (blob && blob.size > 0) {
              const pdfFile = new File([blob], urlDocFileName, { type: 'application/pdf' });
              setFile(pdfFile);
              setSourceType('pdf');
              setAutoLoadedPdfStatus(`PDF "${urlDocFileName}" cargado automáticamente desde el Data Lake.`);
            } else {
              setAutoLoadedPdfStatus(null);
            }
          })
          .catch(() => {
            setAutoLoadedPdfStatus(null);
          });
      }
    }
  }, [searchParams]);

  useEffect(() => {
    getSubjects().then(setSubjects).catch(() => setSubjects([]));
  }, []);

  useEffect(() => {
    if (!isCompanyAccount) return;
    let cancelled = false;
    getSkills(effectiveTenantId)
      .then((skills) => {
        if (!cancelled) setAllCompanySkills(skills);
      })
      .catch(() => {
        if (!cancelled) setAllCompanySkills([]);
      });
    return () => {
      cancelled = true;
    };
  }, [effectiveTenantId, isCompanyAccount]);

  const filteredCompanySkills = useMemo(() => {
    if (selectedCategoryFilter === 'all') return allCompanySkills;
    return allCompanySkills.filter((s) => s.Category === selectedCategoryFilter);
  }, [allCompanySkills, selectedCategoryFilter]);

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

  const canAdvanceFromStep1 =
    courseType === 'enterprise'
      ? documentName.trim().length > 0 && targetSkill.trim().length > 0
      : documentName.trim().length > 0 && !!subjectId && !!topicId;
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
        subjectId: courseType === 'academic' ? (subjectId || null) : null,
        topicId: courseType === 'academic' ? (topicId || null) : null,
        category: courseType === 'enterprise' ? (careerCategory || null) : null,
        targetSkill: courseType === 'enterprise' ? (targetSkill.trim() || null) : null,
        pedagogicalAudience,
        courseLevel,
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
        usePerplexitySearch,
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
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.courseTypeHeader')}</h2>
            <p className="mt-1 text-slate-400">
              {t('newStructuredLearning.step2Subtitle')}
            </p>
          </div>

          {/* Type Selector Cards */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <button
              type="button"
              onClick={() => {
                if (isCompanyAccount) {
                  setCourseType('enterprise');
                }
              }}
              disabled={!isCompanyAccount}
              className={`flex flex-col items-start gap-3 rounded-2xl border p-5 text-left transition-all ${
                courseType === 'enterprise'
                  ? 'border-brand-400 bg-brand-500/10 shadow-lg shadow-brand-500/10'
                  : 'border-white/10 bg-white/[0.02] text-slate-400 hover:border-white/20 hover:bg-white/[0.04]'
              } ${!isCompanyAccount ? 'opacity-50 cursor-not-allowed' : ''}`}
            >
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-500/20 text-brand-300">
                <Building2 className="h-5 w-5" />
              </div>
              <div>
                <p className="font-semibold text-white">{t('newStructuredLearning.enterpriseCourseTitle')}</p>
                <p className="mt-1 text-xs text-slate-400">
                  {t('newStructuredLearning.enterpriseCourseDesc')}
                </p>
              </div>
            </button>

            <button
              type="button"
              onClick={() => {
                setCourseType('academic');
                setTargetSkill('');
                setCareerCategory('');
              }}
              className={`flex flex-col items-start gap-3 rounded-2xl border p-5 text-left transition-all ${
                courseType === 'academic'
                  ? 'border-cyan-400 bg-cyan-500/10 shadow-lg shadow-cyan-500/10'
                  : 'border-white/10 bg-white/[0.02] text-slate-400 hover:border-white/20 hover:bg-white/[0.04]'
              }`}
            >
              <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-cyan-500/20 text-cyan-300">
                <GraduationCap className="h-5 w-5" />
              </div>
              <div>
                <p className="font-semibold text-white">{t('newStructuredLearning.academicCourseTitle')}</p>
                <p className="mt-1 text-xs text-slate-400">
                  {t('newStructuredLearning.academicCourseDesc')}
                </p>
              </div>
            </button>
          </div>

          {/* MODE 1: ENTERPRISE SKILL COURSE */}
          {courseType === 'enterprise' && (
            <div className="space-y-5 rounded-2xl border border-brand-500/20 bg-brand-500/[0.03] p-5">
              <div className="flex items-center gap-2 text-brand-300">
                <Building2 className="h-4 w-4" />
                <h3 className="text-sm font-semibold">{t('newStructuredLearning.enterpriseSkillConfigTitle')}</h3>
              </div>

              {/* Category Filter Pills */}
              <div>
                <label className="mb-2 block text-xs font-medium text-slate-400">{t('newStructuredLearning.filterSkillsLabel')}</label>
                <div className="flex flex-wrap gap-2">
                  {[
                    { id: 'all', label: t('newStructuredLearning.categoryFilterAll') },
                    { id: 'job-skills', label: t('newStructuredLearning.categoryFilterTechnical') },
                    { id: 'soft-skills', label: t('newStructuredLearning.categoryFilterSoft') },
                    { id: 'department-processes', label: t('newStructuredLearning.categoryFilterDept') },
                    { id: 'hr-processes', label: t('newStructuredLearning.categoryFilterHR') },
                  ].map((filter) => (
                    <button
                      key={filter.id}
                      type="button"
                      onClick={() => setSelectedCategoryFilter(filter.id)}
                      className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${
                        selectedCategoryFilter === filter.id
                          ? 'bg-brand-500/20 text-brand-300 border border-brand-400/40'
                          : 'bg-white/5 text-slate-400 hover:bg-white/10 hover:text-white border border-transparent'
                      }`}
                    >
                      {filter.label}
                    </button>
                  ))}
                </div>
              </div>

              <Field label={t('newStructuredLearning.companyCatalogSkillLabel')} hint={t('newStructuredLearning.companyCatalogSkillHint')} required>
                <SearchableCombobox
                  value={targetSkill}
                  onChange={(val) => setTargetSkill(val)}
                  onSelect={(option) => {
                    const skill = allCompanySkills.find((s) => s.SkillId === option.id || s.Name === option.label);
                    if (skill) {
                      setTargetSkill(skill.Name);
                      setCareerCategory(skill.Category);
                      if (!documentName.trim() || documentName === autoSetDocumentNameRef.current) {
                        setDocumentName(skill.Name);
                        autoSetDocumentNameRef.current = skill.Name;
                      }
                    }
                  }}
                  options={filteredCompanySkills.map((s) => ({
                    id: s.SkillId,
                    label: s.Name,
                    meta: getCategoryBadgeText(s.Category),
                  }))}
                  placeholder={t('newStructuredLearning.companyCatalogSkillPlaceholder')}
                  emptyOptionsText={t('newStructuredLearning.companyCatalogSkillEmpty')}
                />
              </Field>

              {careerCategory && (
                <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-white/5 px-3.5 py-2.5 text-xs text-slate-300">
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-white">{t('newStructuredLearning.assignedCategoryLabel')}</span>
                    <span className="rounded-full bg-brand-500/20 px-2.5 py-0.5 font-medium text-brand-300 border border-brand-500/30">
                      {getCategoryBadgeText(careerCategory)}
                    </span>
                  </div>
                  <span className="text-[11px] text-emerald-400 font-medium">{t('newStructuredLearning.domainSubjectNotRequired')}</span>
                </div>
              )}
            </div>
          )}

          {/* MODE 2: ACADEMIC GENERAL COURSE */}
          {courseType === 'academic' && (
            <div className="space-y-5 rounded-2xl border border-white/10 bg-white/[0.02] p-5">
              <div className="flex items-center gap-2 text-cyan-300">
                <GraduationCap className="h-4 w-4" />
                <h3 className="text-sm font-semibold">{t('newStructuredLearning.academicClassificationTitle')}</h3>
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
            </div>
          )}

          {/* COMMON FIELDS */}
          <Field label={t('newStructuredLearning.documentNameLabel')} required>
            <input
              type="text"
              value={documentName}
              onChange={(e) => setDocumentName(e.target.value)}
              placeholder={t('newStructuredLearning.documentNamePlaceholder')}
              className="input"
            />
          </Field>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label={t('newStructuredLearning.contentLanguageLabel')}>
              <div className="grid grid-cols-2 gap-2">
                {[
                  { code: 'es' as const, label: t('common.languageSpanish') },
                  { code: 'en' as const, label: t('common.languageEnglish') },
                ].map((option) => {
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

            {isCompanyAccount && (
              <Field label={t('newCapability.visibilityLabel')} hint={t('newCapability.visibilityHint')}>
                <div className="grid grid-cols-2 gap-2">
                  {[
                    { value: false, label: t('newCapability.visibilityPersonal') },
                    { value: true, label: t('newCapability.visibilityGlobal') },
                  ].map((option) => {
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
          </div>

          <WizardNav onNext={() => setStep(2)} nextDisabled={!canAdvanceFromStep1} />
        </div>
      )}

      {step === 2 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newStructuredLearning.step4Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newStructuredLearning.step4Subtitle')}</p>
          </div>

          {courseType !== 'enterprise' ? (
            <Field label={t('newCapability.pedagogicalAudienceLabel')} hint={t('newCapability.pedagogicalAudienceHint')}>
              <div className="grid grid-cols-2 gap-2">
                {[
                  { value: 'Standard' as const, label: t('newCapability.audienceStandard') },
                  { value: 'YoungLearner' as const, label: t('newCapability.audienceYoungLearner') },
                ].map((option) => {
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
          ) : (
            <div className="rounded-xl border border-white/10 bg-white/[0.02] p-3.5 text-xs text-slate-300">
              <span className="font-semibold text-white">Audiencia pedagógica:</span> Estándar (Adultos / Profesional). <span className="text-slate-400 font-normal">Young Learners (K-1/K-2) no aplica para cursos corporativos.</span>
            </div>
          )}

          <Field label="Nivel de contenido" hint="Ajusta la profundidad del contenido según la seniority del puesto — no cambia el tono, solo qué tanto se explica desde cero vs. cuánto se asume ya dominado.">
            <div className="grid grid-cols-3 gap-2">
              {[
                { value: 'L100' as const, label: 'L100 · Fundacional', hint: 'Sin conocimiento previo' },
                { value: 'L200' as const, label: 'L200 · Intermedio', hint: 'Más ejemplos aplicados' },
                { value: 'L300' as const, label: 'L300 · Avanzado', hint: 'Trade-offs y arquitectura' },
              ].map((option) => {
                const active = courseLevel === option.value;
                return (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => setCourseLevel(option.value)}
                    className={`rounded-xl border px-3 py-2.5 text-left text-sm font-medium transition-colors ${
                      active
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    <div>{option.label}</div>
                    <div className="mt-0.5 text-[11px] font-normal text-slate-400">{option.hint}</div>
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

          <div className="border-t border-white/10 pt-6">
            <h3 className="text-base font-semibold text-white">{t('newStructuredLearning.perplexitySearchTitle')}</h3>
            <p className="mt-1 text-sm text-slate-400">{t('newStructuredLearning.perplexitySearchSubtitle')}</p>
          </div>

          <div className="space-y-4 rounded-xl border-2 border-emerald-500/40 bg-emerald-500/[0.06] p-4 shadow-[0_0_0_1px_rgba(16,185,129,0.15)]">
            <div className="flex items-center gap-2">
              <Globe className="h-4 w-4 shrink-0 text-emerald-400" />
              <p className="text-xs font-semibold uppercase tracking-wide text-emerald-300">
                {t('newStructuredLearning.perplexitySearchBadge')}
              </p>
            </div>
            <p className="text-xs text-slate-300 leading-relaxed">{t('newStructuredLearning.perplexitySearchHint')}</p>

            <Field label={t('newStructuredLearning.usePerplexitySearchLabel')}>
              <label className="flex items-center gap-2.5 text-sm text-slate-200 cursor-pointer">
                <input
                  type="checkbox"
                  checked={usePerplexitySearch}
                  onChange={(e) => setUsePerplexitySearch(e.target.checked)}
                  className="h-4 w-4 rounded border-emerald-400/40 bg-white/[0.02] text-emerald-500 focus:ring-emerald-400"
                />
                <span className="font-medium text-emerald-200">{t('newStructuredLearning.usePerplexitySearchToggle')}</span>
              </label>
            </Field>
          </div>

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

          {autoLoadedPdfStatus && sourceType === 'pdf' && (
            <div className="flex items-center justify-between gap-3 rounded-xl border border-emerald-500/30 bg-emerald-500/10 p-4 text-xs text-emerald-300">
              <div className="flex items-center gap-2.5">
                <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-400" />
                <div>
                  <p className="font-semibold text-white">Documento PDF cargado automáticamente</p>
                  <p className="mt-0.5 text-emerald-300/80">{autoLoadedPdfStatus}</p>
                </div>
              </div>
              <span className="shrink-0 rounded-full bg-emerald-400/20 px-3 py-1 font-mono text-[11px] font-semibold text-emerald-200">
                Data Lake
              </span>
            </div>
          )}

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
              label={t('newStructuredLearning.summaryCourseType')}
              value={courseType === 'enterprise' ? t('newStructuredLearning.courseTypeEnterprise') : t('newStructuredLearning.courseTypeAcademic')}
            />
            {courseType === 'enterprise' && targetSkill && (
              <>
                <SummaryRow label={t('newStructuredLearning.companyCatalogSkillLabel')} value={targetSkill} />
                <SummaryRow label={t('newStructuredLearning.summarySkillCategory')} value={getCategoryBadgeText(careerCategory)} />
              </>
            )}
            {courseType === 'academic' && (
              <>
                <SummaryRow
                  label={t('newStructuredLearning.summaryDomainSubject')}
                  value={subjects.find((s) => s.SubjectId === subjectId)?.Name ?? t('structuredLearningDetail.unassigned')}
                />
                <SummaryRow
                  label={t('newStructuredLearning.summaryDomainType')}
                  value={topics.find((t) => t.StructuredLearningTopicId === topicId)?.Name ?? t('structuredLearningDetail.unassigned')}
                />
              </>
            )}
            <SummaryRow
              label={t('newStructuredLearning.summaryLanguage')}
              value={languageCode === 'en' ? t('common.languageEnglish') : t('common.languageSpanish')}
            />
            <SummaryRow
              label={t('newStructuredLearning.summarySource')}
              value={sourceType === 'pdf' ? t('newStructuredLearning.sourcePdf') : t('newStructuredLearning.sourceText')}
            />
            <SummaryRow
              label={t('newStructuredLearning.summaryPerplexitySearch')}
              value={usePerplexitySearch ? t('common.yes') : t('common.no')}
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
