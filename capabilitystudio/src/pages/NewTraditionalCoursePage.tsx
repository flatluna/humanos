import { useEffect, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, Link } from 'react-router-dom';
import {
  ArrowLeft,
  ArrowRight,
  FileText,
  MessageSquareText,
  Globe,
  Sparkles,
  Pencil,
  Trash2,
  RefreshCw,
  Wand2,
  ListTree,
  UploadCloud,
} from 'lucide-react';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getCapabilityDomains, type CapabilityDomain } from '../lib/api/domainsApi';
import {
  startTraditionalCourse,
  getActiveTraditionalCourseRun,
} from '../lib/api/traditionalCourseApi';
import {
  readFileAsBase64,
  DEFAULT_STUDIO_TENANT_ID,
  previewCapabilityOutline,
  type CapabilityOutlineChapter,
  type CapabilityOutlineSource,
} from '../lib/api/pdfPipelineApi';
import StepIndicator from '../components/StepIndicator';
import TypeOptionCard from '../components/TypeOptionCard';
import FileDropzone from '../components/FileDropzone';
import SearchableCombobox from '../components/SearchableCombobox';
import { useAuth } from '../auth/AuthContext';

const STEPS_KEY = 'newTraditionalCourse.steps';

export default function NewTraditionalCoursePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const effectiveTenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;

  const [step, setStep] = useState(1);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [domains, setDomains] = useState<CapabilityDomain[]>([]);

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [capabilityDomainId, setCapabilityDomainId] = useState('');
  const [subjectLabel, setSubjectLabel] = useState('');
  const [subjectId, setSubjectId] = useState<string | null>(null);
  const [languageCode, setLanguageCode] = useState<'es' | 'en'>('es');
  const [questionsPerChapterExam, setQuestionsPerChapterExam] = useState(5);
  const [isGlobal, setIsGlobal] = useState(false);

  const [sourceType, setSourceType] = useState<'pdf' | 'text'>('pdf');
  const [file, setFile] = useState<File | null>(null);
  const [ideaDescription, setIdeaDescription] = useState('');
  const [enableWebEnrichment, setEnableWebEnrichment] = useState(false);
  const [strictSyllabusMode, setStrictSyllabusMode] = useState(false);
  const [referenceImageFile, setReferenceImageFile] = useState<File | null>(null);
  const [referenceImagePreviewUrl, setReferenceImagePreviewUrl] = useState<string | null>(null);
  const [referenceImageUsageInstruction, setReferenceImageUsageInstruction] = useState('');

  // Paso 3 "Diseño del curso" (mirrors newCapability's Paso 5): the
  // reviewed/approved (possibly hand-edited) chapter/lesson outline — reuses
  // the SAME generic Chapter/Node preview endpoint the Graph wizard uses
  // (a "Node" here just means "Lesson"). Null means "not loaded yet" or
  // "designer chose to continue without an approved outline".
  const [outline, setOutline] = useState<CapabilityOutlineChapter[] | null>(null);
  const [outlineSource, setOutlineSource] = useState<CapabilityOutlineSource | null>(null);
  const [outlineLoading, setOutlineLoading] = useState(false);
  const [outlineError, setOutlineError] = useState<string | null>(null);
  const [outlineSkipped, setOutlineSkipped] = useState(false);
  // Set only for the "Texto/idea" flow — the full document IdeaToDocumentAgent
  // expanded ideaDescription into, must be resent verbatim as
  // preExpandedDocumentText at submit time so the approved outline's
  // StartMarkers still resolve against the SAME text.
  const [expandedDocumentText, setExpandedDocumentText] = useState<string | null>(null);

  const [acceptedLegalDisclosure, setAcceptedLegalDisclosure] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [conflictingRunId, setConflictingRunId] = useState<string | null>(null);

  useEffect(() => {
    getSubjects(languageCode).then(setSubjects).catch(() => setSubjects([]));
    getCapabilityDomains(languageCode).then((result) => {
      setDomains(result);
      setCapabilityDomainId((prev) => prev || result[0]?.CapabilityDomainId || '');
    }).catch(() => setDomains([]));
  }, [languageCode]);

  const canAdvanceFromStep1 = name.trim().length > 0 && !!capabilityDomainId;
  const canAdvanceFromStep2 = sourceType === 'pdf' ? !!file : ideaDescription.trim().length > 0;
  const canSubmit = canAdvanceFromStep1 && canAdvanceFromStep2 && acceptedLegalDisclosure;

  /** Runs the cheap detection-only outline preview — called once when the
   * designer first reaches the design step, and again on "Regenerar". Does
   * NOT run the expensive generation pipeline. */
  async function loadOutlinePreview() {
    setOutlineLoading(true);
    setOutlineError(null);
    setOutlineSkipped(false);
    setExpandedDocumentText(null);
    try {
      let result;
      if (sourceType === 'pdf' && file) {
        const contentBase64 = await readFileAsBase64(file);
        result = await previewCapabilityOutline({ fileName: file.name, contentBase64 });
      } else if (sourceType === 'text' && ideaDescription.trim()) {
        result = await previewCapabilityOutline({
          text: ideaDescription.trim(),
          capabilityName: name.trim(),
          languageCode,
          strictSyllabusMode,
        });
        setExpandedDocumentText(result.ExpandedDocumentText ?? null);
      } else {
        setOutline(null);
        setOutlineSource(null);
        return;
      }

      setOutlineSource(result.Source);
      if (result.Source === 'None' || result.Chapters.length === 0) {
        setOutline(null);
        setOutlineError(result.RejectionReason ?? t('newTraditionalCourse.outlineNotFound'));
      } else {
        setOutline(result.Chapters);
      }
    } catch (err) {
      setOutline(null);
      setOutlineSource(null);
      setOutlineError(err instanceof Error ? err.message : t('newTraditionalCourse.outlineLoadError'));
    } finally {
      setOutlineLoading(false);
    }
  }

  /** Moves from step 2 (content) into step 3 (design/approval), kicking off
   * the outline preview the first time it's reached. */
  async function goToDesignStep() {
    setStep(3);
    if (outline === null && outlineSource === null && !outlineLoading) {
      await loadOutlinePreview();
    }
  }

  function updateChapterTitle(chapterIndex: number, newTitle: string) {
    setOutline((prev) =>
      prev ? prev.map((chapter, i) => (i === chapterIndex ? { ...chapter, Title: newTitle } : chapter)) : prev,
    );
  }

  function updateLessonTitle(chapterIndex: number, lessonIndex: number, newTitle: string) {
    setOutline((prev) =>
      prev
        ? prev.map((chapter, i) =>
            i === chapterIndex
              ? { ...chapter, Nodes: chapter.Nodes.map((node, j) => (j === lessonIndex ? { ...node, Title: newTitle } : node)) }
              : chapter,
          )
        : prev,
    );
  }

  function removeLesson(chapterIndex: number, lessonIndex: number) {
    setOutline((prev) =>
      prev
        ? prev
            .map((chapter, i) =>
              i === chapterIndex ? { ...chapter, Nodes: chapter.Nodes.filter((_, j) => j !== lessonIndex) } : chapter,
            )
            .filter((chapter) => chapter.Nodes.length > 0)
        : prev,
    );
  }

  function removeChapter(chapterIndex: number) {
    setOutline((prev) => (prev ? prev.filter((_, i) => i !== chapterIndex) : prev));
  }

  async function handleSubmit() {
    if (referenceImageFile && !referenceImageUsageInstruction.trim()) {
      setSubmitError('Describe how the reference image should be used in the generated chapter and lesson illustrations.');
      setSubmitting(false);
      return;
    }

    setSubmitting(true);
    setSubmitError(null);
    setConflictingRunId(null);
    try {
      const normalizedQuestionsPerChapterExam = Math.max(1, Math.min(20, questionsPerChapterExam || 5));
      const hasApprovedOutline = !outlineSkipped && outline && outline.length > 0;
      const uploadedImageBase64 = referenceImageFile ? await readFileAsBase64(referenceImageFile) : null;
      const basePayload = {
        tenantId: effectiveTenantId,
        capabilityDomainId,
        subjectId,
        capabilityName: name.trim(),
        capabilityDescription: description.trim() || null,
        enableWebEnrichment,
        languageCode,
        questionsPerChapterExam: normalizedQuestionsPerChapterExam,
        strictSyllabusMode,
        approvedOutline: hasApprovedOutline ? outline : null,
        preExpandedDocumentText: hasApprovedOutline && sourceType === 'text' ? expandedDocumentText : null,
        uploadedImageBase64,
        uploadedImageContentType: referenceImageFile?.type ?? null,
        referenceImageUsageInstruction: referenceImageFile ? referenceImageUsageInstruction.trim() || null : null,
        acceptedLegalDisclosure,
        isGlobal,
      };

      const run = sourceType === 'pdf' && file
        ? await startTraditionalCourse({
            ...basePayload,
            fileName: file.name,
            contentBase64: await readFileAsBase64(file),
          })
        : await startTraditionalCourse({
            ...basePayload,
            description: ideaDescription.trim(),
          });

      navigate(`/traditional-runs/${run.RunId}`);
    } catch (err) {
      const activeRun = await getActiveTraditionalCourseRun().catch(() => null);
      if (activeRun) {
        setConflictingRunId(activeRun.RunId);
        setSubmitError(t('newTraditionalCourse.errorAlreadyGenerating'));
      } else {
        setSubmitError(err instanceof Error ? err.message : t('newTraditionalCourse.errorGenericStart'));
      }
      setSubmitting(false);
    }
  }

  const steps = t(STEPS_KEY, { returnObjects: true }) as string[];

  return (
    <div className="mx-auto max-w-3xl px-6 py-12">
      <div className="mb-10">
        <StepIndicator steps={steps} currentStep={step} />
      </div>

      {step === 1 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newTraditionalCourse.step1Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newTraditionalCourse.step1Subtitle')}</p>
          </div>

          <Field label={t('newTraditionalCourse.nameLabel')}>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('newTraditionalCourse.namePlaceholder')}
              className="input"
            />
          </Field>

          <Field label={t('newTraditionalCourse.descriptionLabel')}>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              placeholder={t('newTraditionalCourse.descriptionPlaceholder')}
              className="input resize-none"
            />
          </Field>

          <Field label={t('newTraditionalCourse.contentLanguageLabel')}>
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

          <Field label={t('newTraditionalCourse.domainLabel')}>
            <select value={capabilityDomainId} onChange={(e) => setCapabilityDomainId(e.target.value)} className="input">
              {domains.length === 0 && <option value="">{t('newTraditionalCourse.domainNoOptions')}</option>}
              {domains.map((domain) => (
                <option key={domain.CapabilityDomainId} value={domain.CapabilityDomainId}>
                  {domain.Name}
                </option>
              ))}
            </select>
          </Field>

          <Field label={t('newTraditionalCourse.subjectLabel')}>
            <SearchableCombobox
              value={subjectLabel}
              onChange={(value) => {
                setSubjectLabel(value);
                if (value === '') setSubjectId(null);
              }}
              onSelect={(option) => setSubjectId(String(option.id))}
              options={subjects.map((subject) => ({ id: subject.SubjectId, label: subject.Name }))}
              placeholder={t('newTraditionalCourse.subjectPlaceholder')}
              emptyOptionsText={t('newTraditionalCourse.subjectNoMatches')}
            />
          </Field>

          <Field
            label={t('newTraditionalCourse.questionsPerChapterLabel')}
            hint={t('newTraditionalCourse.questionsPerChapterHint')}
          >
            <input
              type="number"
              min={1}
              max={20}
              step={1}
              value={questionsPerChapterExam}
              onChange={(e) => setQuestionsPerChapterExam(Number.parseInt(e.target.value, 10) || 5)}
              className="input"
            />
          </Field>

          <Field label={t('newTraditionalCourse.visibilityLabel')} hint={t('newTraditionalCourse.visibilityHint')}>
            <div className="grid grid-cols-2 gap-2">
              {(
                [
                  { value: false, label: t('newTraditionalCourse.visibilityPersonal') },
                  { value: true, label: t('newTraditionalCourse.visibilityGlobal') },
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

          <Field label="Reference image for chapter and lesson illustrations">
            <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.02] p-4">
              {referenceImagePreviewUrl ? (
                <div className="flex items-center gap-3">
                  <img src={referenceImagePreviewUrl} alt="Reference" className="h-16 w-16 rounded-lg object-cover" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm text-slate-300">{referenceImageFile?.name}</p>
                    <button
                      type="button"
                      onClick={() => {
                        if (referenceImagePreviewUrl) URL.revokeObjectURL(referenceImagePreviewUrl);
                        setReferenceImagePreviewUrl(null);
                        setReferenceImageFile(null);
                        setReferenceImageUsageInstruction('');
                      }}
                      className="mt-1 text-xs font-medium text-brand-400 hover:text-brand-300"
                    >
                      Remove
                    </button>
                  </div>
                </div>
              ) : (
                <label className="flex w-full cursor-pointer items-center justify-center gap-2 rounded-xl border border-dashed border-white/15 py-3 text-sm text-slate-400 hover:border-white/25 hover:text-slate-300">
                  <UploadCloud className="h-4 w-4" />
                  Upload image reference
                  <input
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0] ?? null;
                      setReferenceImageFile(file);
                      setReferenceImagePreviewUrl((prev) => {
                        if (prev) URL.revokeObjectURL(prev);
                        return file ? URL.createObjectURL(file) : null;
                      });
                    }}
                  />
                </label>
              )}

              {referenceImageFile && (
                <div>
                  <label className="mb-1.5 block text-xs font-medium text-slate-400">How should this image be used?</label>
                  <textarea
                    value={referenceImageUsageInstruction}
                    onChange={(e) => setReferenceImageUsageInstruction(e.target.value)}
                    rows={2}
                    placeholder="e.g. use this exact logo on the students' uniforms or place the product in the scene"
                    className="input w-full"
                  />
                </div>
              )}
            </div>
          </Field>

          <WizardNav onNext={() => setStep(2)} nextDisabled={!canAdvanceFromStep1} />
        </div>
      )}

      {step === 2 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newTraditionalCourse.step2Title')}</h2>
            <p className="mt-1 text-slate-400">
              {sourceType === 'pdf'
                ? t('newTraditionalCourse.step2SubtitlePdf')
                : t('newTraditionalCourse.step2SubtitleText')}
            </p>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <TypeOptionCard
              icon={FileText}
              title={t('newTraditionalCourse.materialTypes.pdfTitle')}
              description={t('newTraditionalCourse.materialTypes.pdfDescription')}
              selected={sourceType === 'pdf'}
              onClick={() => setSourceType('pdf')}
            />
            <TypeOptionCard
              icon={MessageSquareText}
              title={t('newTraditionalCourse.materialTypes.textTitle')}
              description={t('newTraditionalCourse.materialTypes.textDescription')}
              selected={sourceType === 'text'}
              onClick={() => setSourceType('text')}
            />
          </div>

          {sourceType === 'pdf' && <FileDropzone file={file} onFileSelected={setFile} />}

          {sourceType === 'text' && (
            <Field label={t('newTraditionalCourse.ideaLabel')}>
              <textarea
                value={ideaDescription}
                onChange={(e) => setIdeaDescription(e.target.value)}
                rows={5}
                placeholder={t('newTraditionalCourse.ideaPlaceholder')}
                className="input resize-none"
              />
            </Field>
          )}

          <label className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.02] p-4">
            <input
              type="checkbox"
              checked={enableWebEnrichment}
              onChange={(e) => setEnableWebEnrichment(e.target.checked)}
              className="h-4 w-4 rounded border-white/20 bg-white/5 text-brand-500 focus:ring-brand-500"
            />
            <div className="flex items-center gap-2 text-sm text-white">
              <Globe className="h-4 w-4 text-slate-400" />
              {t('newTraditionalCourse.webEnrichmentLabel')}
            </div>
          </label>

          {sourceType === 'pdf' && (
            <Field
              label={t('newTraditionalCourse.strictSyllabusModeLabel')}
            >
              <label className="flex items-center gap-2 text-sm text-slate-300">
                <input
                  type="checkbox"
                  checked={strictSyllabusMode}
                  onChange={(e) => setStrictSyllabusMode(e.target.checked)}
                  className="h-4 w-4 rounded border-white/20 bg-white/[0.02]"
                />
                {t('newTraditionalCourse.strictSyllabusModeToggle')}
              </label>
            </Field>
          )}

          <WizardNav onBack={() => setStep(1)} onNext={goToDesignStep} nextDisabled={!canAdvanceFromStep2} />
        </div>
      )}

      {step === 3 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newTraditionalCourse.step3Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newTraditionalCourse.step3Subtitle')}</p>
          </div>

          {outlineLoading && (
            <div className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.03] p-6 text-sm text-slate-300">
              <RefreshCw className="h-4 w-4 animate-spin text-brand-400" />
              {t('newTraditionalCourse.outlineLoading')}
            </div>
          )}

          {!outlineLoading && outline && outline.length > 0 && (
            <>
              <div
                className={`flex items-center gap-2 rounded-xl border p-3 text-xs font-medium ${
                  outlineSource === 'InferredByAI'
                    ? 'border-amber-500/20 bg-amber-500/5 text-amber-300'
                    : 'border-emerald-500/20 bg-emerald-500/5 text-emerald-300'
                }`}
              >
                {outlineSource === 'InferredByAI' ? <Wand2 className="h-4 w-4 shrink-0" /> : <ListTree className="h-4 w-4 shrink-0" />}
                {outlineSource === 'InferredByAI'
                  ? t('newTraditionalCourse.outlineSourceInferred')
                  : t('newTraditionalCourse.outlineSourceDetected')}
              </div>

              <div className="space-y-4">
                {outline.map((chapter, chapterIndex) => (
                  <div key={chapterIndex} className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                    <div className="flex items-start gap-2">
                      <Pencil className="mt-2.5 h-3.5 w-3.5 shrink-0 text-slate-500" />
                      <input
                        type="text"
                        value={chapter.Title}
                        onChange={(e) => updateChapterTitle(chapterIndex, e.target.value)}
                        className="input flex-1 font-semibold text-white"
                      />
                      <button
                        type="button"
                        title={t('newTraditionalCourse.outlineRemoveChapter')}
                        onClick={() => removeChapter(chapterIndex)}
                        className="mt-1 rounded-lg p-1.5 text-slate-500 transition-colors hover:bg-red-500/10 hover:text-red-400"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>

                    <div className="mt-3 space-y-2 pl-6">
                      {chapter.Nodes.map((lesson, lessonIndex) => (
                        <div key={lessonIndex} className="flex items-center gap-2">
                          <span className="h-1.5 w-1.5 shrink-0 rounded-full bg-brand-400" />
                          <input
                            type="text"
                            value={lesson.Title}
                            onChange={(e) => updateLessonTitle(chapterIndex, lessonIndex, e.target.value)}
                            className="input flex-1 py-1.5 text-sm"
                          />
                          <button
                            type="button"
                            title={t('newTraditionalCourse.outlineRemoveLesson')}
                            onClick={() => removeLesson(chapterIndex, lessonIndex)}
                            className="rounded-lg p-1.5 text-slate-500 transition-colors hover:bg-red-500/10 hover:text-red-400"
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>

              <button
                type="button"
                onClick={loadOutlinePreview}
                className="inline-flex items-center gap-1.5 text-xs font-medium text-slate-400 hover:text-slate-200"
              >
                <RefreshCw className="h-3.5 w-3.5" />
                {t('newTraditionalCourse.outlineRegenerate')}
              </button>
            </>
          )}

          {!outlineLoading && (!outline || outline.length === 0) && (
            <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-6 text-sm text-slate-300">
              <p>{outlineError ?? t('newTraditionalCourse.outlineNotFound')}</p>
              <div className="flex flex-wrap gap-3">
                <button
                  type="button"
                  onClick={loadOutlinePreview}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.02] px-3 py-1.5 text-xs font-medium text-slate-300 hover:border-white/20"
                >
                  <RefreshCw className="h-3.5 w-3.5" />
                  {t('newTraditionalCourse.outlineRetry')}
                </button>
                <button
                  type="button"
                  onClick={() => setOutlineSkipped(true)}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/[0.02] px-3 py-1.5 text-xs font-medium text-slate-300 hover:border-white/20"
                >
                  {t('newTraditionalCourse.outlineSkip')}
                </button>
              </div>
              {outlineSkipped && <p className="text-xs text-emerald-300">{t('newTraditionalCourse.outlineSkipped')}</p>}
            </div>
          )}

          <WizardNav
            onBack={() => setStep(2)}
            onNext={() => setStep(4)}
            nextDisabled={outlineLoading || (!outline && !outlineSkipped)}
          />
        </div>
      )}

      {step === 4 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newTraditionalCourse.step4Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newTraditionalCourse.step4Subtitle')}</p>
          </div>

          <div className="space-y-2 rounded-2xl border border-white/10 bg-white/[0.03] p-4">
            <SummaryRow label={t('newTraditionalCourse.summaryName')} value={name} />
            <SummaryRow
              label={t('newTraditionalCourse.summaryDomain')}
              value={domains.find((d) => d.CapabilityDomainId === capabilityDomainId)?.Name ?? '—'}
            />
            <SummaryRow label={t('newTraditionalCourse.summarySubject')} value={subjectLabel || '—'} />
            <SummaryRow
              label={t('newTraditionalCourse.summaryLanguage')}
              value={languageCode === 'en' ? t('common.languageEnglish') : t('common.languageSpanish')}
            />
            <SummaryRow
              label={t('newTraditionalCourse.summaryQuestionsPerChapter')}
              value={String(questionsPerChapterExam)}
            />
            <SummaryRow
              label={t('newTraditionalCourse.summaryVisibility')}
              value={isGlobal ? t('newTraditionalCourse.visibilityGlobal') : t('newTraditionalCourse.visibilityPersonal')}
            />
            <SummaryRow
              label={t('newTraditionalCourse.summarySource')}
              value={sourceType === 'pdf' ? t('newTraditionalCourse.sourcePdf') : t('newTraditionalCourse.sourceText')}
            />
          </div>

          {submitError && (
            <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-4 text-sm text-red-300">
              {submitError}
              {conflictingRunId && (
                <Link
                  to={`/traditional-runs/${conflictingRunId}`}
                  className="ml-1.5 inline-flex items-center gap-1 font-medium text-brand-400 hover:text-brand-300"
                >
                  {t('newTraditionalCourse.viewProgress')} <ArrowRight className="h-3.5 w-3.5" />
                </Link>
              )}
            </div>
          )}

          <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
            <h3 className="text-sm font-semibold text-white">{t('newTraditionalCourse.legalDisclosureTitle')}</h3>
            <ul className="mt-1.5 list-disc space-y-1 pl-4 text-xs text-slate-400">
              {(t('newTraditionalCourse.legalDisclosurePoints', { returnObjects: true }) as string[]).map((point, index) => (
                <li key={index}>{point}</li>
              ))}
            </ul>
            <label className="mt-3 flex cursor-pointer items-start gap-2 text-xs text-slate-300">
              <input
                type="checkbox"
                checked={acceptedLegalDisclosure}
                onChange={(e) => setAcceptedLegalDisclosure(e.target.checked)}
                className="mt-0.5 h-4 w-4 shrink-0 rounded border-white/20 bg-white/[0.02] text-brand-500 focus:ring-brand-500"
              />
              {t('newTraditionalCourse.legalDisclosureCheckboxLabel')}
            </label>
          </div>

          <WizardNav
            onBack={() => setStep(3)}
            onNext={handleSubmit}
            nextDisabled={!canSubmit || submitting}
            nextLabel={submitting ? t('newTraditionalCourse.starting') : t('newTraditionalCourse.createCourse')}
            nextIcon={Sparkles}
          />
        </div>
      )}
    </div>
  );
}

function Field({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <div>
      <label className="mb-1.5 block text-sm font-medium text-slate-300">{label}</label>
      {children}
      {hint && <p className="mt-1.5 text-xs text-slate-500">{hint}</p>}
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
