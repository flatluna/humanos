import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, Link, useSearchParams } from 'react-router-dom';
import { FileText, MessageSquareText, Video, Link2, ArrowLeft, ArrowRight, Sparkles, Globe, Baby } from 'lucide-react';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getCapabilityDomains, type CapabilityDomain } from '../lib/api/domainsApi';
import { getPrograms, getProgramById, type BackendProgram, type BackendProgramDetail } from '../lib/api/programsApi';
import { getSkillsSummary, type SkillSummaryDto } from '../lib/api/skillsApi';
import {
  DEFAULT_STUDIO_TENANT_ID,
  readFileAsBase64,
  startPdfCapabilityGraph,
  startCapabilityGraphFromDescription,
  getActiveCapabilityGraphRun,
} from '../lib/api/pdfPipelineApi';
import StepIndicator from '../components/StepIndicator';
import TypeOptionCard from '../components/TypeOptionCard';
import FileDropzone from '../components/FileDropzone';
import { getSubjectIcon } from '../lib/subjectVisuals';

type CapabilityType = 'pdf' | 'text' | 'video' | 'link';

/// The 4 employer career-plan training tracks (2026-08-01 "cursos de
/// empresa" feature) — same kebab-case ids used by the career-plan
/// frontends (human-os-employee/human-os-enterprise) as their tab/category
/// ids, and by the backend's Capability.Category /
/// PersonCareerPlanService.AutoMatchCapabilitiesAsync matching logic.
const CAREER_CATEGORIES = [
  { value: 'job-skills' as const, labelKey: 'newCapability.careerCategoryJobSkills' },
  { value: 'soft-skills' as const, labelKey: 'newCapability.careerCategorySoftSkills' },
  { value: 'department-processes' as const, labelKey: 'newCapability.careerCategoryDepartmentProcesses' },
  { value: 'hr-processes' as const, labelKey: 'newCapability.careerCategoryHrProcesses' },
];

export default function NewCapabilityPage() {
  const { t } = useTranslation();
  const STEPS = t('newCapability.steps', { returnObjects: true }) as string[];
  const navigate = useNavigate();
  const [step, setStep] = useState(1);

  // Optional prefill (2026-08-01) — lets an external link (e.g. the HR
  // enterprise console's "Generar curso" button for a career-plan step
  // that has no matching Program yet) open this wizard with the title/
  // description already filled in, so HR just reviews and picks a domain
  // instead of retyping. Read once on mount only.
  const [searchParams] = useSearchParams();

  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [domains, setDomains] = useState<CapabilityDomain[]>([]);
  const [programs, setPrograms] = useState<BackendProgram[]>([]);
  const [catalogSkills, setCatalogSkills] = useState<SkillSummaryDto[]>([]);

  const [name, setName] = useState(() => searchParams.get('name') ?? '');
  const [description, setDescription] = useState(() => searchParams.get('description') ?? '');
  const [subjectId, setSubjectId] = useState<string>('');
  const [capabilityDomainId, setCapabilityDomainId] = useState<string>('');
  const [languageCode, setLanguageCode] = useState<'es' | 'en'>('es');
  const [pedagogicalAudience, setPedagogicalAudience] = useState<'Standard' | 'YoungLearner'>('Standard');
  // Deep Dive (Examples) step is now ALWAYS generated for every course,
  // regardless of subject/audience (cooking, carpentry, law, K-12, etc.) —
  // no longer an optional toggle the designer has to remember to enable.
  const includeExamplesStep = true;
  // Defaults to 3 (not 1) so a designer who never touches this stepper
  // still gets the feature's originally-intended "3+ example cards" per
  // node (see includeExamplesStepHint) instead of silently capping every
  // course to a single Deep Dive card (2026-08-03 bug fix).
  const [maxDeepDiveCardsPerNode, setMaxDeepDiveCardsPerNode] = useState<number>(3);
  // Strict syllabus mode (2026-08-03): for a formal course whose source
  // PDF has its own fixed, numbered curriculum (e.g. "Unidad 1" with
  // subsections 1.0-1.8), forces one graph node per detected chapter/
  // subsection instead of letting the AI merge/omit some based on its own
  // judgment. Off by default — most courses want the AI's own concept-
  // based graph design.
  const [strictSyllabusMode, setStrictSyllabusMode] = useState(false);
  const [programId, setProgramId] = useState<string>('');
  const [programDetail, setProgramDetail] = useState<BackendProgramDetail | null>(null);
  const [programSequenceNumber, setProgramSequenceNumber] = useState<number | null>(null);
  const [capabilityObjectives, setCapabilityObjectives] = useState('');
  const [capabilityRequirements, setCapabilityRequirements] = useState('');

  // Career-plan linking (2026-08-01): career category is a plain string,
  // '' meaning "not an employer course" (Category stays null on the
  // backend). targetSkill is only meaningful once a career category is
  // chosen — HR picks it straight from the shared Skill catalog, which
  // already knows every job role that requires it, so no separate
  // job-role picker is needed here.
  const [careerCategory, setCareerCategory] = useState<string>('');
  const [targetSkill, setTargetSkill] = useState<string>('');

  const MAX_PROGRAM_SEQUENCE_SLOTS = 20;

  const [capabilityType, setCapabilityType] = useState<CapabilityType>('pdf');
  const [file, setFile] = useState<File | null>(null);
  const [ideaDescription, setIdeaDescription] = useState('');
  const [enableWebEnrichment, setEnableWebEnrichment] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [conflictingRunId, setConflictingRunId] = useState<string | null>(null);

  useEffect(() => {
    getSubjects().then(setSubjects).catch(() => setSubjects([]));
    getCapabilityDomains().then(setDomains).catch(() => setDomains([]));
    getPrograms().then(setPrograms).catch(() => setPrograms([]));
  }, []);

  const selectedProgram = useMemo(
    () => programs.find((p) => p.ProgramId === programId) ?? null,
    [programs, programId],
  );

  // As soon as HR picks a career category (e.g. "Soft Skills"), fetch the
  // reusable Skill catalog for that category (2026-08-01 "cierra el
  // ciclo" feature) — lists every skill/process HR has already authored
  // across ALL job roles in this category, so picking one doesn't require
  // choosing a specific job role first.
  useEffect(() => {
    if (!careerCategory) {
      setCatalogSkills([]);
      return;
    }
    let cancelled = false;
    getSkillsSummary(DEFAULT_STUDIO_TENANT_ID, careerCategory)
      .then((skills) => {
        if (!cancelled) setCatalogSkills(skills);
      })
      .catch(() => {
        if (!cancelled) setCatalogSkills([]);
      });
    return () => {
      cancelled = true;
    };
  }, [careerCategory]);

  // Resetting career category clears any target skill that no longer
  // makes sense (e.g. switching from "Mi Puesto" to "Ninguna").
  useEffect(() => {
    if (!careerCategory) {
      setTargetSkill('');
    }
  }, [careerCategory]);

  // Map of occupied sequence slots (1..20) → the capability name already
  // sitting there, so the picker below can disable them without forcing
  // the designer to fill gaps in order (e.g. picking #8 while #6 is still
  // empty is perfectly valid).
  const occupiedSlots = useMemo(() => {
    const map = new Map<number, string>();
    for (const pc of programDetail?.Capabilities ?? []) {
      map.set(pc.SortOrder, pc.CapabilityName);
    }
    return map;
  }, [programDetail]);

  useEffect(() => {
    if (!programId) {
      setProgramDetail(null);
      setProgramSequenceNumber(null);
      return;
    }
    let cancelled = false;
    getProgramById(programId)
      .then((detail) => {
        if (cancelled) return;
        setProgramDetail(detail);
        // Auto-suggest the smallest free slot, but the designer can still
        // freely override it below — never forced to be contiguous.
        const taken = new Set(detail.Capabilities.map((c) => c.SortOrder));
        let suggested = 1;
        while (taken.has(suggested) && suggested <= MAX_PROGRAM_SEQUENCE_SLOTS) suggested += 1;
        setProgramSequenceNumber(suggested <= MAX_PROGRAM_SEQUENCE_SLOTS ? suggested : null);
      })
      .catch(() => {
        if (!cancelled) setProgramDetail(null);
      });
    return () => {
      cancelled = true;
    };
  }, [programId]);

  const canAdvanceFromStep1 = name.trim().length > 0 && capabilityDomainId.length > 0;
  const canAdvanceFromStep3 =
    capabilityType === 'pdf' ? file !== null : capabilityType === 'text' ? ideaDescription.trim().length > 0 : false;

  const canSubmit = useMemo(() => {
    if (!canAdvanceFromStep1) return false;
    if (capabilityType === 'pdf') return file !== null;
    if (capabilityType === 'text') return ideaDescription.trim().length > 0;
    return false;
  }, [canAdvanceFromStep1, capabilityType, file, ideaDescription]);

  async function handleSubmit() {
    setSubmitting(true);
    setSubmitError(null);
    setConflictingRunId(null);
    try {
      const normalizedMaxDeepDiveCardsPerNode = Math.max(1, Math.min(10, maxDeepDiveCardsPerNode));
      if (capabilityType === 'pdf') {
        if (!file) return;
        const contentBase64 = await readFileAsBase64(file);
        const run = await startPdfCapabilityGraph({
          tenantId: DEFAULT_STUDIO_TENANT_ID,
          capabilityDomainId,
          subjectId: subjectId || null,
          capabilityName: name.trim(),
          capabilityDescription: description.trim() || null,
          fileName: file.name,
          contentBase64,
          enableWebEnrichment,
          programId: programId || null,
          programSequenceNumber: programId ? programSequenceNumber : null,
          capabilityObjectives: programId ? capabilityObjectives.trim() || null : null,
          capabilityRequirements: programId ? capabilityRequirements.trim() || null : null,
          languageCode,
          pedagogicalAudience: pedagogicalAudience === 'YoungLearner' ? 'YoungLearner' : null,
          includeExamplesStep,
          category: careerCategory || null,
          targetSkill: targetSkill || null,
          maxDeepDiveCardsPerNode: normalizedMaxDeepDiveCardsPerNode,
          strictSyllabusMode,
        });
        navigate(`/runs/${run.RunId}`);
      } else if (capabilityType === 'text') {
        const run = await startCapabilityGraphFromDescription({
          tenantId: DEFAULT_STUDIO_TENANT_ID,
          capabilityDomainId,
          subjectId: subjectId || null,
          capabilityName: name.trim(),
          description: ideaDescription.trim(),
          capabilityDescription: description.trim() || null,
          enableWebEnrichment,
          programId: programId || null,
          programSequenceNumber: programId ? programSequenceNumber : null,
          capabilityObjectives: programId ? capabilityObjectives.trim() || null : null,
          capabilityRequirements: programId ? capabilityRequirements.trim() || null : null,
          languageCode,
          pedagogicalAudience: pedagogicalAudience === 'YoungLearner' ? 'YoungLearner' : null,
          includeExamplesStep,
          category: careerCategory || null,
          targetSkill: targetSkill || null,
          maxDeepDiveCardsPerNode: normalizedMaxDeepDiveCardsPerNode,
          strictSyllabusMode,
        });
        navigate(`/runs/${run.RunId}`);
      }
    } catch (err) {
      // Only one capability generation is allowed at a time — if this
      // failed because another run is still in progress, link straight to
      // it instead of just showing a generic error message.
      const activeRun = await getActiveCapabilityGraphRun().catch(() => null);
      if (activeRun) {
        setConflictingRunId(activeRun.RunId);
        setSubmitError(t('newCapability.errorAlreadyGenerating'));
      } else {
        setSubmitError(err instanceof Error ? err.message : t('newCapability.errorGenericStart'));
      }
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-12">
      <div className="mb-10">
        <StepIndicator steps={STEPS} currentStep={step} />
      </div>

      {step === 1 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newCapability.step1Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newCapability.step1Subtitle')}</p>
          </div>

          <Field label={t('newCapability.nameLabel')}>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={t('newCapability.namePlaceholder')}
              className="input"
            />
          </Field>

          <Field label={t('newCapability.descriptionLabel')}>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              placeholder={t('newCapability.descriptionPlaceholder')}
              className="input resize-none"
            />
          </Field>

          <Field
            label={t('newCapability.contentLanguageLabel')}
            hint={t('newCapability.contentLanguageHint')}
          >
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

          <Field
            label={t('newCapability.pedagogicalAudienceLabel')}
            hint={t('newCapability.pedagogicalAudienceHint')}
          >
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
                    className={`flex items-center justify-center gap-2 rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors ${
                      active
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    <Baby className="h-4 w-4 shrink-0" />
                    {option.label}
                  </button>
                );
              })}
            </div>
          </Field>

          <Field
            label={t('newCapability.maxDeepDiveCardsLabel')}
            hint={t('newCapability.maxDeepDiveCardsHint')}
          >
            <input
              type="number"
              min={1}
              max={10}
              step={1}
              value={maxDeepDiveCardsPerNode}
              onChange={(e) => {
                const parsed = Number.parseInt(e.target.value, 10);
                if (Number.isNaN(parsed)) {
                  setMaxDeepDiveCardsPerNode(1);
                  return;
                }
                setMaxDeepDiveCardsPerNode(Math.max(1, Math.min(10, parsed)));
              }}
              className="input"
            />
          </Field>

          <Field
            label={t('newCapability.strictSyllabusModeLabel')}
            hint={t('newCapability.strictSyllabusModeHint')}
          >
            <label className="flex items-center gap-2 text-sm text-slate-300">
              <input
                type="checkbox"
                checked={strictSyllabusMode}
                onChange={(e) => setStrictSyllabusMode(e.target.checked)}
                className="h-4 w-4 rounded border-white/20 bg-white/[0.02]"
              />
              {t('newCapability.strictSyllabusModeToggle')}
            </label>
          </Field>

          <Field label={t('newCapability.domainLabel')}>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
              {subjects.map((subject) => {
                const Icon = getSubjectIcon(subject.Code);
                const active = subjectId === subject.SubjectId;
                return (
                  <button
                    key={subject.SubjectId}
                    type="button"
                    onClick={() => setSubjectId(active ? '' : subject.SubjectId)}
                    className={`flex items-center gap-2 rounded-xl border px-3 py-2.5 text-left text-sm transition-colors ${
                      active
                        ? 'border-brand-400/60 bg-brand-500/10 text-white'
                        : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                    }`}
                  >
                    <Icon className="h-4 w-4 shrink-0" />
                    <span className="truncate">{subject.Name}</span>
                  </button>
                );
              })}
            </div>
          </Field>

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

          {careerCategory && (
            <div className="space-y-4 rounded-xl border border-white/10 bg-white/[0.02] p-4">
              <Field label={t('newCapability.targetSkillLabel')} hint={t('newCapability.targetSkillCatalogHint')}>
                {catalogSkills.length > 0 ? (
                  <select value={targetSkill} onChange={(e) => setTargetSkill(e.target.value)} className="input">
                    <option value="">{t('newCapability.targetSkillPlaceholder')}</option>
                    {catalogSkills.map((skill) => (
                      <option key={skill.SkillId} value={skill.Name}>
                        {skill.Name}
                        {skill.JobRoleCount > 0 ? ` (${skill.JobRoleCount})` : ''}
                      </option>
                    ))}
                  </select>
                ) : (
                  <p className="text-xs text-slate-500">{t('newCapability.targetSkillNoOptions')}</p>
                )}
              </Field>
            </div>
          )}

          <Field label={t('newCapability.authoringCategoryLabel')} hint={t('newCapability.authoringCategoryHint')}>
            <select value={capabilityDomainId} onChange={(e) => setCapabilityDomainId(e.target.value)} className="input">
              <option value="">{t('newCapability.categoryPlaceholder')}</option>
              {domains.map((domain) => (
                <option key={domain.CapabilityDomainId} value={domain.CapabilityDomainId}>
                  {domain.Name}
                </option>
              ))}
            </select>
          </Field>

          <Field label={t('newCapability.programLabel')} hint={t('newCapability.programHint')}>
            <select value={programId} onChange={(e) => setProgramId(e.target.value)} className="input">
              <option value="">{t('newCapability.programNone')}</option>
              {programs.map((program) => (
                <option key={program.ProgramId} value={program.ProgramId}>
                  {program.Name}
                </option>
              ))}
            </select>
          </Field>

          {selectedProgram && (
            <div className="space-y-4 rounded-xl border border-white/10 bg-white/[0.02] p-4 text-sm">
              <p className="text-slate-300">
                {t('newCapability.connectedToProgram', { name: selectedProgram.Name })}
              </p>
              {selectedProgram.Objectives && (
                <p className="text-slate-400">
                  <span className="font-medium text-slate-300">{t('newCapability.programObjectivesLabel')}</span>
                  {selectedProgram.Objectives}
                </p>
              )}

              <div>
                <label className="mb-1.5 block text-sm font-medium text-slate-300">
                  {t('newCapability.sequenceNumberLabel')}
                </label>
                <p className="mb-2 text-xs text-slate-500">
                  {t('newCapability.sequenceHint', { max: MAX_PROGRAM_SEQUENCE_SLOTS })}
                </p>
                <div className="grid grid-cols-5 gap-2 sm:grid-cols-10">
                  {Array.from({ length: MAX_PROGRAM_SEQUENCE_SLOTS }, (_, i) => i + 1).map((slot) => {
                    const takenBy = occupiedSlots.get(slot);
                    const selected = programSequenceNumber === slot;
                    return (
                      <button
                        key={slot}
                        type="button"
                        disabled={!!takenBy}
                        title={takenBy ? t('newCapability.occupiedBy', { name: takenBy }) : t('newCapability.usePosition', { slot })}
                        onClick={() => setProgramSequenceNumber(slot)}
                        className={`rounded-lg border px-2 py-2 text-xs font-medium transition-colors ${
                          takenBy
                            ? 'cursor-not-allowed border-white/5 bg-white/[0.02] text-slate-600 line-through'
                            : selected
                              ? 'border-brand-400/60 bg-brand-500/10 text-white'
                              : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                        }`}
                      >
                        {slot}
                      </button>
                    );
                  })}
                </div>
              </div>

              <Field
                label={t('newCapability.capabilityObjectivesLabel')}
                hint={t('newCapability.capabilityObjectivesHint')}
              >
                <textarea
                  value={capabilityObjectives}
                  onChange={(e) => setCapabilityObjectives(e.target.value)}
                  rows={2}
                  placeholder={t('newCapability.capabilityObjectivesPlaceholder')}
                  className="input resize-none"
                />
              </Field>

              <Field
                label={t('newCapability.capabilityRequirementsLabel')}
                hint={t('newCapability.capabilityRequirementsHint')}
              >
                <textarea
                  value={capabilityRequirements}
                  onChange={(e) => setCapabilityRequirements(e.target.value)}
                  rows={2}
                  placeholder={t('newCapability.capabilityRequirementsPlaceholder')}
                  className="input resize-none"
                />
              </Field>
            </div>
          )}

          <WizardNav onNext={() => setStep(2)} nextDisabled={!canAdvanceFromStep1} />
        </div>
      )}

      {step === 2 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newCapability.step2Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newCapability.step2Subtitle')}</p>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <TypeOptionCard
              icon={FileText}
              title={t('newCapability.materialTypes.pdfTitle')}
              description={t('newCapability.materialTypes.pdfDescription')}
              selected={capabilityType === 'pdf'}
              onClick={() => setCapabilityType('pdf')}
            />
            <TypeOptionCard
              icon={MessageSquareText}
              title={t('newCapability.materialTypes.textTitle')}
              description={t('newCapability.materialTypes.textDescription')}
              selected={capabilityType === 'text'}
              onClick={() => setCapabilityType('text')}
            />
            <TypeOptionCard
              icon={Video}
              title={t('newCapability.materialTypes.videoTitle')}
              description={t('newCapability.materialTypes.videoDescription')}
              selected={capabilityType === 'video'}
              disabled
              onClick={() => {}}
            />
            <TypeOptionCard
              icon={Link2}
              title={t('newCapability.materialTypes.linkTitle')}
              description={t('newCapability.materialTypes.linkDescription')}
              selected={capabilityType === 'link'}
              disabled
              onClick={() => {}}
            />
          </div>

          <WizardNav onBack={() => setStep(1)} onNext={() => setStep(3)} />
        </div>
      )}

      {step === 3 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newCapability.step3Title')}</h2>
            <p className="mt-1 text-slate-400">
              {capabilityType === 'pdf'
                ? t('newCapability.step3SubtitlePdf')
                : t('newCapability.step3SubtitleText')}
            </p>
          </div>

          {capabilityType === 'pdf' && <FileDropzone file={file} onFileSelected={setFile} />}

          {capabilityType === 'text' && (
            <Field label={t('newCapability.ideaLabel')}>
              <textarea
                value={ideaDescription}
                onChange={(e) => setIdeaDescription(e.target.value)}
                rows={5}
                placeholder={t('newCapability.ideaPlaceholder')}
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
              {t('newCapability.webEnrichmentLabel')}
            </div>
          </label>

          <WizardNav onBack={() => setStep(2)} onNext={() => setStep(4)} nextDisabled={!canAdvanceFromStep3} />
        </div>
      )}

      {step === 4 && (
        <div className="animate-fade-in space-y-6">
          <div>
            <h2 className="text-2xl font-bold text-white">{t('newCapability.step4Title')}</h2>
            <p className="mt-1 text-slate-400">{t('newCapability.step4Subtitle')}</p>
          </div>

          <div className="space-y-3 rounded-2xl border border-white/10 bg-white/[0.03] p-5">
            <SummaryRow label={t('newCapability.summaryName')} value={name} />
            {description && <SummaryRow label={t('newCapability.summaryDescription')} value={description} />}
            <SummaryRow
              label={t('newCapability.summaryDomain')}
              value={subjects.find((s) => s.SubjectId === subjectId)?.Name ?? t('newCapability.notSpecified')}
            />
            <SummaryRow
              label={t('newCapability.summaryCategory')}
              value={domains.find((d) => d.CapabilityDomainId === capabilityDomainId)?.Name ?? t('newCapability.dash')}
            />
            <SummaryRow
              label={t('newCapability.summaryCareerCategory')}
              value={
                careerCategory
                  ? t(CAREER_CATEGORIES.find((c) => c.value === careerCategory)?.labelKey ?? '')
                  : t('newCapability.careerCategoryNone')
              }
            />
            {targetSkill && <SummaryRow label={t('newCapability.summaryTargetSkill')} value={targetSkill} />}
            <SummaryRow label={t('newCapability.summaryContentLanguage')} value={languageCode === 'en' ? t('common.languageEnglish') : t('common.languageSpanish')} />
            <SummaryRow
              label={t('newCapability.summaryPedagogicalAudience')}
              value={pedagogicalAudience === 'YoungLearner' ? t('newCapability.audienceYoungLearner') : t('newCapability.audienceStandard')}
            />
            <SummaryRow
              label={t('newCapability.summaryMaxDeepDiveCards')}
              value={String(Math.max(1, Math.min(10, maxDeepDiveCardsPerNode)))}
            />
            {capabilityType === 'pdf' ? (
              <SummaryRow label={t('newCapability.summaryFile')} value={file?.name ?? t('newCapability.dash')} />
            ) : (
              <SummaryRow label={t('newCapability.summaryIdea')} value={ideaDescription || t('newCapability.dash')} />
            )}
            <SummaryRow label={t('newCapability.summaryWebEnrichment')} value={enableWebEnrichment ? t('newCapability.enabled') : t('newCapability.disabled')} />
            {selectedProgram && (
              <>
                <SummaryRow label={t('newCapability.summaryProgram')} value={selectedProgram.Name} />
                <SummaryRow
                  label={t('newCapability.summarySequence')}
                  value={programSequenceNumber ? `#${programSequenceNumber}` : t('newCapability.unassigned')}
                />
                {capabilityObjectives && <SummaryRow label={t('newCapability.summaryObjectives')} value={capabilityObjectives} />}
                {capabilityRequirements && (
                  <SummaryRow label={t('newCapability.summaryRequirements')} value={capabilityRequirements} />
                )}
              </>
            )}
          </div>

          {submitError && (
            <div className="rounded-xl border border-red-500/20 bg-red-500/5 p-4 text-sm text-red-300">
              {submitError}
              {conflictingRunId && (
                <Link
                  to={`/runs/${conflictingRunId}`}
                  className="ml-1.5 inline-flex items-center gap-1 font-medium text-brand-400 hover:text-brand-300"
                >
                  {t('newCapability.viewProgress')} <ArrowRight className="h-3.5 w-3.5" />
                </Link>
              )}
            </div>
          )}

          <WizardNav
            onBack={() => setStep(3)}
            onNext={handleSubmit}
            nextDisabled={!canSubmit || submitting}
            nextLabel={submitting ? t('newCapability.starting') : t('newCapability.createCapability')}
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
