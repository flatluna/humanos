import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import {
  Sparkles,
  Loader2,
  AlertTriangle,
  Heart,
  Monitor,
  Bot,
  ExternalLink,
  CheckCircle2,
  Save,
  Pencil,
  RefreshCw,
  X,
  FolderOutput,
} from 'lucide-react';
import type {
  ProcessWideLearningRecommendationResultDto,
  ProcessSkillGapDto,
  ProcessSkillRequirementDto,
  RecommendedCourseDto,
  CourseOutlineResultDto,
} from '@/api/processDocumentsApi';
import { getProcessSkillRequirements, saveProcessSkillRequirement, getCourseOutline, saveProcessAsCapability } from '@/api/processDocumentsApi';
import { getDepartments, getJobRoles, type DepartmentDto, type JobRoleDto } from '@/api/orgStructureApi';
import { getSkills, type SkillDto, type SkillCategory } from '@/api/skillsApi';
import { CatalogSkillPicker } from '@/components/wizard/CatalogSkillPicker';

/** A skill gap the user added manually from the catalog (the LLM did not identify it). */
type ManualSkillGap = ProcessSkillGapDto & { isManual: true };

export interface ProcessWideLearningReviewStepProps {
  processName: string;
  documentId?: string;
  tenantId?: string;
  recommendation: ProcessWideLearningRecommendationResultDto | null;
  isAnalyzing: boolean;
  onAnalyze: () => Promise<void>;
  onApproveAndContinue: () => void;
  /** Called whenever the user manually adds/removes a catalog skill, so the parent can persist the
   * updated gap list to the backend and keep it in sync across step navigation. */
  onSkillGapsChanged: (skillGaps: ProcessSkillGapDto[]) => void;
}

const COLUMN_META = {
  'soft-skills': { icon: Heart, titleKey: 'processLearning.processWideStep.categorySoftSkills', color: 'text-pink-600 bg-pink-50 border-pink-200' },
  'technology-skills': { icon: Monitor, titleKey: 'processLearning.processWideStep.categoryTechnologySkills', color: 'text-blue-600 bg-blue-50 border-blue-200' },
} as const;

/** Maps each Skills Identification column to the matching catalog skill category used for the
 * manual "add from catalog" combo box — soft-skills column ↔ catalog 'soft-skills', Tecnología ↔ 'job-skills'. */
const COLUMN_CATALOG_CATEGORY: Record<keyof typeof COLUMN_META, SkillCategory> = {
  'soft-skills': 'soft-skills',
  'technology-skills': 'job-skills',
};

/** Small styled confirmation modal (replaces the native browser window.confirm, which cannot be
 * localized/styled and looks out of place next to the rest of the UI). Fully bilingual via i18n. */
function ConfirmDialog({ message, onConfirm, onCancel }: { message: string; onConfirm: () => void; onCancel: () => void }) {
  const { t } = useTranslation();
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-sm rounded-lg bg-white p-5 shadow-xl">
        <p className="whitespace-pre-line text-sm text-gray-800">{message}</p>
        <div className="mt-4 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
          >
            {t('processLearning.common.cancel')}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className="rounded-md bg-red-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-red-700"
          >
            {t('processLearning.common.confirm')}
          </button>
        </div>
      </div>
    </div>
  );
}

/** Inline panel for scoping a skill gap to Departments/Job Roles/the whole company, plus a
 * Required/Not-Required toggle, and persisting it as a ProcessSkillRequirement linked to this process. */
function SaveRequirementPanel({
  gap,
  documentId,
  departments,
  jobRoles,
  existing,
  selectedCourse,
  selectedOutline,
  onSaved,
  onCancel,
}: {
  gap: ProcessSkillGapDto;
  documentId: string;
  departments: DepartmentDto[];
  jobRoles: JobRoleDto[];
  existing?: ProcessSkillRequirementDto;
  selectedCourse?: RecommendedCourseDto;
  selectedOutline?: CourseOutlineResultDto | null;
  onSaved: (saved: ProcessSkillRequirementDto) => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();
  const [appliesToAll, setAppliesToAll] = useState(existing?.appliesToAllDepartments ?? false);
  const [selectedDepartmentIds, setSelectedDepartmentIds] = useState(new Set(existing?.departmentIds ?? []));
  const [selectedJobRoleIds, setSelectedJobRoleIds] = useState(new Set(existing?.jobRoleIds ?? []));
  const [isRequired, setIsRequired] = useState(existing?.isRequired ?? true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const toggleDepartment = (id: string) => {
    const next = new Set(selectedDepartmentIds);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSelectedDepartmentIds(next);
  };

  const toggleJobRole = (id: string) => {
    const next = new Set(selectedJobRoleIds);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setSelectedJobRoleIds(next);
  };

  const handleSave = async () => {
    setIsSaving(true);
    setError(null);
    try {
      const saved = await saveProcessSkillRequirement(
        documentId,
        {
          skillName: gap.name,
          category: gap.category || 'soft-skills',
          isAiSkill: !!gap.isAiSkill,
          reason: gap.reason,
          appliesToAllDepartments: appliesToAll,
          isRequired,
          departmentIds: appliesToAll ? [] : Array.from(selectedDepartmentIds),
          jobRoleIds: appliesToAll ? [] : Array.from(selectedJobRoleIds),
          // Case (a): catalog match — reuse the existing Skill, nothing new is created.
          matchedSkillId: gap.isFromCatalog && gap.matchedSkillId ? gap.matchedSkillId : existing?.matchedSkillId || undefined,
          // Case (b): new internet course — create a new Skill from the chosen course + its outline.
          isNewFromInternet: !gap.isFromCatalog && !existing?.matchedSkillId,
          sourceCourseTitle: !gap.isFromCatalog ? selectedCourse?.title : undefined,
          sourceCourseProvider: !gap.isFromCatalog ? selectedCourse?.provider : undefined,
          sourceCourseUrl: !gap.isFromCatalog ? selectedCourse?.url : undefined,
          sourceOutlineModules: !gap.isFromCatalog ? selectedOutline?.modules : undefined,
          sourceOutlineSource: !gap.isFromCatalog ? selectedOutline?.source : undefined,
        },
        existing?.processSkillRequirementId,
      );
      onSaved(saved);
    } catch (err) {
      // The process is still a draft (not yet persisted via "Approve & Save") when the backend
      // can't find documentId in dbo.ProcessDocument — guide the user to save the process first
      // instead of showing a generic/confusing error.
      const isDocumentNotFound = isAxiosError(err) && err.response?.status === 404;
      setError(t(isDocumentNotFound ? 'processLearning.processWideStep.saveErrorDocumentNotSaved' : 'processLearning.processWideStep.saveError'));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="mt-3 rounded-lg border border-indigo-200 bg-indigo-50/40 p-3">
      <h5 className="text-xs font-bold uppercase tracking-wide text-indigo-700">
        {t('processLearning.processWideStep.saveRequirementTitle')}
      </h5>
      <p className="mt-1 text-xs text-gray-600">
        {gap.isFromCatalog
          ? t('processLearning.processWideStep.assignFromCatalogNote', { skillName: gap.matchedSkillName || gap.name })
          : t('processLearning.processWideStep.createFromInternetNote')}
      </p>
      <p className="mt-1 text-xs text-gray-600">{t('processLearning.processWideStep.saveScopeSubtitle')}</p>

      <div className="mt-2 flex gap-4 text-sm">
        <label className="flex items-center gap-1.5">
          <input
            type="radio"
            checked={isRequired}
            onChange={() => setIsRequired(true)}
            className="h-4 w-4 border-gray-300 text-indigo-600 focus:ring-indigo-500"
          />
          {t('processLearning.processWideStep.requiredLabel')}
        </label>
        <label className="flex items-center gap-1.5">
          <input
            type="radio"
            checked={!isRequired}
            onChange={() => setIsRequired(false)}
            className="h-4 w-4 border-gray-300 text-indigo-600 focus:ring-indigo-500"
          />
          {t('processLearning.processWideStep.notRequiredLabel')}
        </label>
      </div>

      <label className="mt-3 flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm font-medium text-gray-800">
        <input
          type="checkbox"
          checked={appliesToAll}
          onChange={() => setAppliesToAll(!appliesToAll)}
          className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
        />
        {t('processLearning.classifyStep.appliesToAllDepartments')}
      </label>

      {!appliesToAll && (
        <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <h6 className="text-[11px] font-bold uppercase tracking-wide text-gray-500">
              {t('processLearning.classifyStep.departments')}
            </h6>
            <div className="mt-1 max-h-32 space-y-1 overflow-y-auto rounded-lg border border-gray-200 bg-white p-2">
              {departments.length === 0 && (
                <p className="px-1 py-1 text-xs text-gray-400">{t('processLearning.classifyStep.noDepartments')}</p>
              )}
              {departments.map((d) => (
                <label key={d.departmentId} className="flex items-center gap-2 rounded px-1 py-0.5 text-xs text-gray-700 hover:bg-gray-50">
                  <input
                    type="checkbox"
                    checked={selectedDepartmentIds.has(d.departmentId)}
                    onChange={() => toggleDepartment(d.departmentId)}
                    className="h-3.5 w-3.5 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  {d.name}
                </label>
              ))}
            </div>
          </div>

          <div>
            <h6 className="text-[11px] font-bold uppercase tracking-wide text-gray-500">
              {t('processLearning.classifyStep.jobRoles')}
            </h6>
            <div className="mt-1 max-h-32 space-y-1 overflow-y-auto rounded-lg border border-gray-200 bg-white p-2">
              {jobRoles.length === 0 && (
                <p className="px-1 py-1 text-xs text-gray-400">{t('processLearning.classifyStep.noJobRoles')}</p>
              )}
              {jobRoles.map((r) => (
                <label key={r.jobRoleId} className="flex items-center gap-2 rounded px-1 py-0.5 text-xs text-gray-700 hover:bg-gray-50">
                  <input
                    type="checkbox"
                    checked={selectedJobRoleIds.has(r.jobRoleId)}
                    onChange={() => toggleJobRole(r.jobRoleId)}
                    className="h-3.5 w-3.5 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  {r.title}
                </label>
              ))}
            </div>
          </div>
        </div>
      )}

      {error && <p className="mt-2 text-xs text-red-600">{error}</p>}

      <div className="mt-3 flex items-center gap-2">
        <button
          type="button"
          onClick={handleSave}
          disabled={isSaving}
          className="inline-flex items-center gap-1.5 rounded-md bg-indigo-600 px-3 py-1.5 text-xs font-medium text-white shadow-sm hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {isSaving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Save className="h-3.5 w-3.5" />}
          {isSaving ? t('processLearning.processWideStep.savingText') : t('processLearning.processWideStep.saveBtn')}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
        >
          {t('processLearning.processWideStep.cancelBtn')}
        </button>
      </div>
    </div>
  );
}

/** One skill-gap card: name, reason, affected steps, and 1-2 real internet course links. */
/**
 * Per-course index: EVERY recommended course must have an outline. On mount, automatically tries to
 * scrape the course's own syllabus/module page (e.g. a Coursera "#modules" section); if that fails, the
 * backend asks the LLM to draft a plausible outline instead. The result (either way) is meant as a
 * starting draft EngramsIQ Studio can use to auto-generate the course content — so it is always labeled
 * with its source ("scraped" vs "ai-suggested").
 */
function CourseOutlinePanel({
  course,
  skillName,
  processName,
  onOutlineLoaded,
}: {
  course: RecommendedCourseDto;
  skillName: string;
  processName: string;
  onOutlineLoaded?: (outline: CourseOutlineResultDto | null) => void;
}) {
  const { t } = useTranslation();
  const [isLoading, setIsLoading] = useState(false);
  const [outline, setOutline] = useState<CourseOutlineResultDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleGenerate = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await getCourseOutline({
        skillName,
        courseTitle: course.title,
        provider: course.provider,
        url: course.url,
        processName,
      });
      setOutline(result);
      onOutlineLoaded?.(result);
    } catch {
      setError(t('processLearning.processWideStep.outlineError'));
    } finally {
      setIsLoading(false);
    }
  };

  // Every recommended course must have an index — generate it automatically as soon as the course appears,
  // instead of waiting for the user to click a button.
  useEffect(() => {
    handleGenerate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [course.title, course.url]);

  return (
    <div className="mt-1">
      {isLoading && !outline && (
        <p className="inline-flex items-center gap-1 text-[11px] text-gray-500">
          <Loader2 className="h-3 w-3 animate-spin" />
          {t('processLearning.processWideStep.generatingOutline')}
        </p>
      )}

      {error && !isLoading && (
        <button
          type="button"
          onClick={handleGenerate}
          className="inline-flex items-center gap-1 rounded border border-red-200 px-2 py-0.5 text-[11px] font-medium text-red-600 hover:bg-red-50"
        >
          <RefreshCw className="h-3 w-3" />
          {error} — {t('processLearning.processWideStep.retryBtn')}
        </button>
      )}

      {error && <p className="mt-1 text-[11px] text-red-600">{error}</p>}

      {outline && (
        <div className="mt-1 rounded border border-gray-200 bg-gray-50 p-2">
          <div className="mb-1 flex items-center justify-between">
            <span
              className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium ${
                outline.source === 'scraped' ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'
              }`}
            >
              {outline.source === 'scraped'
                ? t('processLearning.processWideStep.outlineScraped')
                : t('processLearning.processWideStep.outlineAiSuggested')}
            </span>
          </div>

          {outline.modules.length === 0 ? (
            <p className="text-[11px] text-amber-600">{t('processLearning.processWideStep.outlineEmpty')}</p>
          ) : (
            <ol className="list-decimal space-y-1 pl-4">
              {outline.modules.map((module, mIdx) => (
                <li key={mIdx} className="text-[12px] text-gray-800">
                  {module.title}
                  {module.lessons.length > 0 && (
                    <ul className="list-disc space-y-0.5 pl-4 text-[11px] text-gray-600">
                      {module.lessons.map((lesson, lIdx) => (
                        <li key={lIdx}>{lesson}</li>
                      ))}
                    </ul>
                  )}
                </li>
              ))}
            </ol>
          )}

          {outline.warnings.length > 0 && (
            <ul className="mt-1 space-y-0.5">
              {outline.warnings.map((warning, wIdx) => (
                <li key={wIdx} className="text-[11px] text-amber-600">
                  {warning}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}

function SkillGapCard({
  gap,
  documentId,
  processName,
  departments,
  jobRoles,
  savedRequirement,
  onSaved,
  onRemove,
  onRemoveCourse,
}: {
  gap: ProcessSkillGapDto;
  documentId?: string;
  processName: string;
  departments: DepartmentDto[];
  jobRoles: JobRoleDto[];
  savedRequirement?: ProcessSkillRequirementDto;
  onSaved: (saved: ProcessSkillRequirementDto) => void;
  onRemove?: () => void;
  onRemoveCourse?: (courseIndex: number) => void;
}) {
  const { t } = useTranslation();
  const [openPanelCourseIndex, setOpenPanelCourseIndex] = useState<number | null>(null);
  const [outlinesByIndex, setOutlinesByIndex] = useState<Record<number, CourseOutlineResultDto | null>>({});
  // The backend only tracks one ProcessSkillRequirement per skill name (not per course), so we
  // remember locally, for this session, which specific course the user just linked — purely a
  // display nicety so the right course shows the "linked" checkmark instead of all/none of them.
  const [linkedCourseIndex, setLinkedCourseIndex] = useState<number | null>(null);

  const selectedCourse = openPanelCourseIndex !== null ? gap.recommendedCourses[openPanelCourseIndex] : undefined;
  const selectedOutline = openPanelCourseIndex !== null ? (outlinesByIndex[openPanelCourseIndex] ?? null) : null;

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <div className="flex items-start gap-2">
        <h3 className="flex-1 font-medium text-gray-900">
          {gap.isFromCatalog ? gap.matchedSkillName || gap.name : gap.name}
        </h3>
        {gap.isFromCatalog ? (
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-medium text-emerald-700">
            <CheckCircle2 className="h-3 w-3" />
            {t('processLearning.processWideStep.catalogBadge')}
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 rounded-full bg-sky-50 px-2 py-0.5 text-[10px] font-medium text-sky-700">
            {t('processLearning.processWideStep.internetBadge')}
          </span>
        )}
        {gap.isAiSkill && (
          <span className="inline-flex items-center gap-1 rounded-full bg-violet-50 px-2 py-0.5 text-[10px] font-medium text-violet-700">
            <Bot className="h-3 w-3" />
            {t('processLearning.processWideStep.aiSkillBadge')}
          </span>
        )}
        {onRemove && (
          <span className="inline-flex items-center gap-1 rounded-full bg-gray-50 px-2 py-0.5 text-[10px] font-medium text-gray-600">
            {t('processLearning.processWideStep.manualBadge')}
          </span>
        )}
        {onRemove && (
          <button
            type="button"
            onClick={onRemove}
            title={t('processLearning.processWideStep.removeManualSkill')}
            className="rounded p-1 text-gray-400 hover:bg-red-50 hover:text-red-600"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        )}
      </div>

      <p className="mt-2 text-sm text-gray-700">{gap.reason}</p>

      {gap.affectedSteps.length > 0 && (
        <p className="mt-2 text-xs text-gray-400">
          {t('processLearning.processWideStep.affectedSteps')}: {gap.affectedSteps.join(', ')}
        </p>
      )}

      {gap.isFromCatalog ? (
        <div className="mt-3 rounded-lg border border-emerald-100 bg-emerald-50/50 p-2 text-xs text-emerald-800">
          {t('processLearning.processWideStep.alreadyInCatalog', { skillName: gap.matchedSkillName || gap.name })}
        </div>
      ) : (
        <div className="mt-3">
          <h4 className="text-xs font-semibold uppercase tracking-wide text-gray-500">
            {t('processLearning.processWideStep.recommendedCourses')}
          </h4>
          {gap.recommendedCourses.length === 0 ? (
            <p className="mt-1 flex items-center gap-1 text-xs text-amber-600">
              <AlertTriangle className="h-3.5 w-3.5" />
              {t('processLearning.processWideStep.noCourseFound')}
            </p>
          ) : (
            <ul className="mt-1 space-y-2">
              {gap.recommendedCourses.map((course, cIdx) => {
                // Each recommended course is linked to the process independently, since a gap can
                // have several distinct course options and the user may want to link more than one.
                const isThisCourseSaved = linkedCourseIndex === cIdx || (!!savedRequirement && linkedCourseIndex === null && cIdx === 0);
                const isThisPanelOpen = openPanelCourseIndex === cIdx;
                return (
                  <li key={cIdx} className="text-sm">
                    <div className="flex flex-wrap items-center gap-1">
                      {course.url ? (
                        <a
                          href={course.url}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex items-center gap-1 text-indigo-600 hover:underline"
                        >
                          {course.title}
                          <ExternalLink className="h-3 w-3" />
                        </a>
                      ) : (
                        <span className="text-gray-800">{course.title}</span>
                      )}
                      {course.provider && <span className="text-gray-400">({course.provider})</span>}
                      {onRemoveCourse && (
                        <button
                          type="button"
                          onClick={() => onRemoveCourse(cIdx)}
                          title={t('processLearning.processWideStep.removeCourse')}
                          className="ml-auto rounded p-1 text-gray-400 hover:bg-red-50 hover:text-red-600"
                        >
                          <X className="h-3.5 w-3.5" />
                        </button>
                      )}
                    </div>
                    <CourseOutlinePanel
                      course={course}
                      skillName={gap.name}
                      processName={processName}
                      onOutlineLoaded={(result) => setOutlinesByIndex((prev) => ({ ...prev, [cIdx]: result }))}
                    />
                    {documentId && (
                      <div className="mt-1.5">
                        {!isThisPanelOpen && (
                          <button
                            type="button"
                            onClick={() => setOpenPanelCourseIndex(cIdx)}
                            className={`inline-flex items-center gap-1.5 rounded-md border px-3 py-1.5 text-xs font-medium ${
                              isThisCourseSaved
                                ? 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                                : 'border-gray-300 text-gray-700 hover:bg-gray-50'
                            }`}
                          >
                            {isThisCourseSaved ? (
                              <>
                                <Pencil className="h-3.5 w-3.5" />
                                {t('processLearning.processWideStep.editSavedBtn')}
                              </>
                            ) : (
                              <>
                                <Save className="h-3.5 w-3.5" />
                                {t('processLearning.processWideStep.saveBtn')}
                              </>
                            )}
                          </button>
                        )}
                        {isThisCourseSaved && !isThisPanelOpen && (
                          <span className="ml-2 inline-flex items-center gap-1 text-xs text-emerald-700">
                            <CheckCircle2 className="h-3.5 w-3.5" />
                            {t('processLearning.processWideStep.savedBtn')}
                          </span>
                        )}
                        {isThisPanelOpen && (
                          <SaveRequirementPanel
                            gap={gap}
                            documentId={documentId}
                            departments={departments}
                            jobRoles={jobRoles}
                            existing={isThisCourseSaved ? savedRequirement : undefined}
                            selectedCourse={selectedCourse}
                            selectedOutline={selectedOutline}
                            onSaved={(saved) => {
                              onSaved(saved);
                              setLinkedCourseIndex(cIdx);
                              setOpenPanelCourseIndex(null);
                            }}
                            onCancel={() => setOpenPanelCourseIndex(null)}
                          />
                        )}
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}

      {documentId && gap.isFromCatalog && (
        <div className="mt-3 border-t border-gray-100 pt-3">
          {openPanelCourseIndex !== -1 && (
            <button
              type="button"
              onClick={() => setOpenPanelCourseIndex(-1)}
              className={`inline-flex items-center gap-1.5 rounded-md border px-3 py-1.5 text-xs font-medium ${
                savedRequirement
                  ? 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                  : 'border-gray-300 text-gray-700 hover:bg-gray-50'
              }`}
            >
              {savedRequirement ? (
                <>
                  <Pencil className="h-3.5 w-3.5" />
                  {t('processLearning.processWideStep.editSavedBtn')}
                </>
              ) : (
                <>
                  <Save className="h-3.5 w-3.5" />
                  {t('processLearning.processWideStep.saveBtn')}
                </>
              )}
            </button>
          )}

          {savedRequirement && openPanelCourseIndex !== -1 && (
            <span className="ml-2 inline-flex items-center gap-1 text-xs text-emerald-700">
              <CheckCircle2 className="h-3.5 w-3.5" />
              {t('processLearning.processWideStep.savedBtn')}
            </span>
          )}

          {openPanelCourseIndex === -1 && (
            <SaveRequirementPanel
              gap={gap}
              documentId={documentId}
              departments={departments}
              jobRoles={jobRoles}
              existing={savedRequirement}
              selectedCourse={selectedCourse}
              selectedOutline={selectedOutline}
              onSaved={(saved) => {
                onSaved(saved);
                setOpenPanelCourseIndex(null);
              }}
              onCancel={() => setOpenPanelCourseIndex(null)}
            />
          )}
        </div>
      )}
    </div>
  );
}

/** One of the two columns of the simplified Skills Identification screen: Soft Skills or Tecnología. */
function SkillColumn({
  category,
  gaps,
  documentId,
  processName,
  departments,
  jobRoles,
  savedByName,
  onSaved,
  catalogSkills,
  onAddManual,
  onRemoveManual,
  onRemoveCourse,
}: {
  category: keyof typeof COLUMN_META;
  gaps: ProcessSkillGapDto[];
  documentId?: string;
  processName: string;
  departments: DepartmentDto[];
  jobRoles: JobRoleDto[];
  savedByName: Map<string, ProcessSkillRequirementDto>;
  onSaved: (saved: ProcessSkillRequirementDto) => void;
  catalogSkills: SkillDto[];
  onAddManual: (skills: SkillDto[]) => void;
  onRemoveManual: (name: string) => void;
  onRemoveCourse: (gapName: string, courseIndex: number) => void;
}) {
  const { t } = useTranslation();
  const meta = COLUMN_META[category];
  const Icon = meta.icon;
  const catalogGaps = gaps.filter((g) => g.isFromCatalog);
  const internetGaps = gaps.filter((g) => !g.isFromCatalog);
  const existingNames = useMemo(() => new Set(gaps.map((g) => g.name.trim().toLowerCase())), [gaps]);

  const renderGapList = (list: ProcessSkillGapDto[]) => (
    <div className="space-y-3">
      {list.map((gap, idx) => {
        const isManual = (gap as ManualSkillGap).isManual === true;
        return (
          <SkillGapCard
            key={idx}
            gap={gap}
            documentId={documentId}
            processName={processName}
            departments={departments}
            jobRoles={jobRoles}
            savedRequirement={savedByName.get(gap.name.trim().toLowerCase())}
            onSaved={onSaved}
            onRemove={isManual ? () => onRemoveManual(gap.name) : undefined}
            onRemoveCourse={!gap.isFromCatalog ? (cIdx) => onRemoveCourse(gap.name, cIdx) : undefined}
          />
        );
      })}
    </div>
  );

  return (
    <div className="flex-1 space-y-3">
      <div className={`flex items-center justify-between gap-2 rounded-lg border px-3 py-2 ${meta.color}`}>
        <div className="flex items-center gap-2">
          <Icon className="h-4 w-4" />
          <h3 className="text-sm font-semibold">{t(meta.titleKey)}</h3>
        </div>
        <CatalogSkillPicker
          skills={catalogSkills}
          existingNames={existingNames}
          onAdd={onAddManual}
          label={t('processLearning.processWideStep.addFromCatalogBtn')}
          searchPlaceholder={t('processLearning.processWideStep.addFromCatalogSearchPlaceholder')}
          addBtnLabel={t('processLearning.processWideStep.addFromCatalogAddBtn')}
          cancelBtnLabel={t('processLearning.processWideStep.addFromCatalogCancelBtn')}
          noResultsLabel={t('processLearning.processWideStep.addFromCatalogNoResults')}
          accentClass="border-white/60 bg-white/70 hover:bg-white"
        />
      </div>
      {gaps.length === 0 ? (
        <p className="rounded-lg border border-dashed border-gray-200 p-4 text-center text-xs text-gray-400">
          {t('processLearning.processWideStep.noGapsInColumn', 'Sin brechas identificadas en esta categoría.')}
        </p>
      ) : (
        <div className="space-y-4">
          <div>
            <h4 className="mb-2 text-[11px] font-bold uppercase tracking-wide text-emerald-700">
              {t('processLearning.processWideStep.quadrantCatalog')} ({catalogGaps.length})
            </h4>
            {catalogGaps.length === 0 ? (
              <p className="rounded-lg border border-dashed border-gray-200 p-3 text-center text-xs text-gray-400">
                {t('processLearning.processWideStep.noGapsInColumn', 'Sin brechas identificadas en esta categoría.')}
              </p>
            ) : (
              renderGapList(catalogGaps)
            )}
          </div>
          <div>
            <h4 className="mb-2 text-[11px] font-bold uppercase tracking-wide text-sky-700">
              {t('processLearning.processWideStep.quadrantInternet')} ({internetGaps.length})
            </h4>
            {internetGaps.length === 0 ? (
              <p className="rounded-lg border border-dashed border-gray-200 p-3 text-center text-xs text-gray-400">
                {t('processLearning.processWideStep.noGapsInColumn', 'Sin brechas identificadas en esta categoría.')}
              </p>
            ) : (
              renderGapList(internetGaps)
            )}
          </div>
        </div>
      )}
    </div>
  );
}

/**
 * Wizard Step 4 "Skills Identification": a simple, process-wide (not per-step) view of the skill gaps
 * needed for the whole process, split into exactly two columns — Soft Skills and Tecnología (named tools/
 * platforms, e.g. "Salesforce"). Bounded on purpose (backend caps at ~5 per column) and course
 * recommendations come only from real internet search findings, never invented.
 */
export function ProcessWideLearningReviewStep({
  processName,
  documentId,
  tenantId,
  recommendation,
  isAnalyzing,
  onAnalyze,
  onApproveAndContinue,
  onSkillGapsChanged,
}: ProcessWideLearningReviewStepProps) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const { data: departments = [] } = useQuery({
    queryKey: ['departments', tenantId],
    queryFn: () => getDepartments(tenantId!),
    enabled: !!tenantId,
  });

  const { data: jobRoles = [] } = useQuery({
    queryKey: ['jobRoles', tenantId],
    queryFn: () => getJobRoles(tenantId!),
    enabled: !!tenantId,
  });

  const { data: allCatalogSkills = [] } = useQuery({
    queryKey: ['skills', tenantId],
    queryFn: () => getSkills(tenantId!),
    enabled: !!tenantId,
  });

  const softCatalogSkills = useMemo(
    () => allCatalogSkills.filter((s) => s.category === COLUMN_CATALOG_CATEGORY['soft-skills'] && s.isActive !== false),
    [allCatalogSkills],
  );
  const technologyCatalogSkills = useMemo(
    () => allCatalogSkills.filter((s) => s.category === COLUMN_CATALOG_CATEGORY['technology-skills'] && s.isActive !== false),
    [allCatalogSkills],
  );

  const { data: savedRequirements = [] } = useQuery({
    queryKey: ['processSkillRequirements', documentId],
    queryFn: () => getProcessSkillRequirements(documentId!),
    enabled: !!documentId,
  });

  const savedByName = useMemo(() => {
    const map = new Map<string, ProcessSkillRequirementDto>();
    savedRequirements.forEach((r) => map.set(r.skillName.trim().toLowerCase(), r));
    return map;
  }, [savedRequirements]);

  const handleSaved = () => {
    if (documentId) {
      queryClient.invalidateQueries({ queryKey: ['processSkillRequirements', documentId] });
    }
  };

  const handleAddManualSkill = (columnCategory: keyof typeof COLUMN_META, skillsToAdd: SkillDto[]) => {
    if (skillsToAdd.length === 0) return;
    const newGaps: ManualSkillGap[] = skillsToAdd.map((s) => ({
      name: s.name,
      category: columnCategory,
      isAiSkill: false,
      reason: t('processLearning.processWideStep.manuallyAddedReason'),
      affectedSteps: [],
      recommendedCourses: [],
      isFromCatalog: true,
      matchedSkillId: s.skillId,
      matchedSkillName: s.name,
      isManual: true,
    }));
    onSkillGapsChanged([...(recommendation?.skillGaps || []), ...newGaps]);
  };

  const [pendingRemoveSkill, setPendingRemoveSkill] = useState<string | null>(null);
  const [pendingReRun, setPendingReRun] = useState(false);

  // "Guardar en Catálogo de Capabilities" — saves the WHOLE process (not an individual skill
  // gap) as a Department Process capability, prompting for the authoring department.
  const [showSaveCapabilityModal, setShowSaveCapabilityModal] = useState(false);
  const [capabilityDepartmentId, setCapabilityDepartmentId] = useState('');
  const [capabilitySavedSkillId, setCapabilitySavedSkillId] = useState<string | null>(null);

  const saveCapabilityMutation = useMutation({
    mutationFn: () => saveProcessAsCapability(documentId!, capabilityDepartmentId),
    onSuccess: (result) => {
      setCapabilitySavedSkillId(result.skillId);
      setShowSaveCapabilityModal(false);
      queryClient.invalidateQueries({ queryKey: ['skills', tenantId] });
    },
  });

  const handleRemoveManualSkill = (name: string) => {
    setPendingRemoveSkill(name);
  };

  const confirmRemoveManualSkill = () => {
    if (!pendingRemoveSkill) return;
    const filtered = (recommendation?.skillGaps || []).filter(
      (g) => g.name.trim().toLowerCase() !== pendingRemoveSkill.trim().toLowerCase(),
    );
    onSkillGapsChanged(filtered);
    setPendingRemoveSkill(null);
  };

  // Removes a single recommended course from a gap (not the whole skill gap), so the user can
  // discard a course they don't like while keeping the others and the gap itself.
  const [pendingRemoveCourse, setPendingRemoveCourse] = useState<{
    gapName: string;
    courseIndex: number;
    courseTitle: string;
  } | null>(null);

  const handleRemoveCourse = (gapName: string, courseIndex: number) => {
    const gap = (recommendation?.skillGaps || []).find(
      (g) => g.name.trim().toLowerCase() === gapName.trim().toLowerCase(),
    );
    const courseTitle = gap?.recommendedCourses[courseIndex]?.title || '';
    setPendingRemoveCourse({ gapName, courseIndex, courseTitle });
  };

  const confirmRemoveCourse = () => {
    if (!pendingRemoveCourse) return;
    const { gapName, courseIndex } = pendingRemoveCourse;
    const updated = (recommendation?.skillGaps || []).map((g) => {
      if (g.name.trim().toLowerCase() !== gapName.trim().toLowerCase()) return g;
      return { ...g, recommendedCourses: g.recommendedCourses.filter((_, i) => i !== courseIndex) };
    });
    onSkillGapsChanged(updated);
    setPendingRemoveCourse(null);
  };

  const softSkillGaps = useMemo(() => {
    return recommendation?.skillGaps.filter((g) => (g.category || 'soft-skills') === 'soft-skills') || [];
  }, [recommendation]);
  const technologySkillGaps = useMemo(() => {
    return recommendation?.skillGaps.filter((g) => g.category === 'technology-skills') || [];
  }, [recommendation]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-xl font-semibold text-gray-900">{t('processLearning.processWideStep.title')}</h2>
          <p className="mt-1 text-sm text-gray-500">{t('processLearning.processWideStep.subtitle')}</p>
        </div>
        {documentId && (
          <button
            type="button"
            onClick={() => setShowSaveCapabilityModal(true)}
            className="inline-flex shrink-0 items-center gap-2 rounded-md border border-indigo-300 bg-white px-3 py-2 text-sm font-medium text-indigo-700 shadow-sm hover:bg-indigo-50"
          >
            <FolderOutput className="h-4 w-4" />
            {t('processLearning.processWideStep.saveAsCapabilityBtn')}
          </button>
        )}
      </div>

      {!recommendation && (
        <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 p-8 text-center">
          <p className="mb-4 text-sm text-gray-600">{t('processLearning.processWideStep.intro', { processName })}</p>
          <button
            type="button"
            onClick={onAnalyze}
            disabled={isAnalyzing}
            className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isAnalyzing ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin" />
                {t('processLearning.processWideStep.analyzingText')}
              </>
            ) : (
              <>
                <Sparkles className="h-4 w-4" />
                {t('processLearning.processWideStep.analyzeBtn')}
              </>
            )}
          </button>
        </div>
      )}

      {recommendation && (
        <>
          {recommendation.analyzedAt && (
            <p className="text-xs text-gray-500">
              {t('processLearning.processWideStep.lastAnalyzedAt', {
                date: new Date(recommendation.analyzedAt).toLocaleString(),
              })}
            </p>
          )}
          <div className="rounded-lg border border-indigo-100 bg-indigo-50 p-4">
            <p className="text-sm text-indigo-900">{recommendation.overallSummary}</p>
          </div>

          {recommendation.skillGaps.length === 0 ? (
            <div className="rounded-lg border border-gray-200 p-6 text-center text-sm text-gray-500">
              {t('processLearning.processWideStep.noGaps')}
            </div>
          ) : (
            <div className="flex flex-col gap-6 sm:flex-row">
              <SkillColumn
                category="soft-skills"
                gaps={softSkillGaps}
                documentId={documentId}
                processName={processName}
                departments={departments}
                jobRoles={jobRoles}
                savedByName={savedByName}
                onSaved={handleSaved}
                catalogSkills={softCatalogSkills}
                onAddManual={(skills) => handleAddManualSkill('soft-skills', skills)}
                onRemoveManual={handleRemoveManualSkill}
                onRemoveCourse={handleRemoveCourse}
              />
              <SkillColumn
                category="technology-skills"
                gaps={technologySkillGaps}
                documentId={documentId}
                processName={processName}
                departments={departments}
                jobRoles={jobRoles}
                savedByName={savedByName}
                onSaved={handleSaved}
                catalogSkills={technologyCatalogSkills}
                onAddManual={(skills) => handleAddManualSkill('technology-skills', skills)}
                onRemoveManual={handleRemoveManualSkill}
                onRemoveCourse={handleRemoveCourse}
              />
            </div>
          )}

          {recommendation.warnings.length > 0 && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-4">
              <h4 className="flex items-center gap-1 text-sm font-medium text-amber-800">
                <AlertTriangle className="h-4 w-4" />
                {t('processLearning.processWideStep.warningsHeader')}
              </h4>
              <ul className="mt-1 list-inside list-disc text-xs text-amber-700">
                {recommendation.warnings.map((w, i) => (
                  <li key={i}>{w}</li>
                ))}
              </ul>
            </div>
          )}

          <div className="flex items-center justify-between border-t border-gray-100 pt-4">
            <button
              type="button"
              onClick={() => setPendingReRun(true)}
              disabled={isAnalyzing}
              className="inline-flex items-center gap-2 rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isAnalyzing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
              {t('processLearning.processWideStep.reRunBtn')}
            </button>
            <button
              type="button"
              onClick={onApproveAndContinue}
              className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700"
            >
              <CheckCircle2 className="h-4 w-4" />
              {t('processLearning.processWideStep.approveBtn')}
            </button>
          </div>
        </>
      )}

      {pendingRemoveSkill && (
        <ConfirmDialog
          message={t('processLearning.processWideStep.removeManualSkillConfirm', { skillName: pendingRemoveSkill })}
          onConfirm={confirmRemoveManualSkill}
          onCancel={() => setPendingRemoveSkill(null)}
        />
      )}

      {pendingRemoveCourse && (
        <ConfirmDialog
          message={t('processLearning.processWideStep.removeCourseConfirm', {
            courseTitle: pendingRemoveCourse.courseTitle,
          })}
          onConfirm={confirmRemoveCourse}
          onCancel={() => setPendingRemoveCourse(null)}
        />
      )}

      {pendingReRun && (
        <ConfirmDialog
          message={t('processLearning.processWideStep.reRunConfirm')}
          onConfirm={() => {
            setPendingReRun(false);
            void onAnalyze();
          }}
          onCancel={() => setPendingReRun(false)}
        />
      )}

      {showSaveCapabilityModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-white p-6 shadow-xl">
            <h3 className="text-base font-semibold text-gray-900">
              {t('processLearning.processWideStep.saveAsCapabilityModalTitle')}
            </h3>
            <p className="mt-2 text-sm text-gray-600">
              {t('processLearning.processWideStep.saveAsCapabilityModalDescription')}
            </p>
            <label className="mt-4 block text-sm font-medium text-gray-700">
              {t('processLearning.processWideStep.saveAsCapabilityDepartmentLabel')}
            </label>
            <select
              value={capabilityDepartmentId}
              onChange={(e) => setCapabilityDepartmentId(e.target.value)}
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-indigo-500 focus:outline-none"
            >
              <option value="">{t('processLearning.processWideStep.saveAsCapabilityDepartmentPlaceholder')}</option>
              {departments.map((d: DepartmentDto) => (
                <option key={d.departmentId} value={d.departmentId}>
                  {d.name}
                </option>
              ))}
            </select>
            {saveCapabilityMutation.isError && (
              <p className="mt-2 text-sm text-red-600">{t('processLearning.processWideStep.saveAsCapabilityError')}</p>
            )}
            <div className="mt-6 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setShowSaveCapabilityModal(false)}
                className="rounded-md border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                {t('common.cancel')}
              </button>
              <button
                type="button"
                disabled={!capabilityDepartmentId || saveCapabilityMutation.isPending}
                onClick={() => saveCapabilityMutation.mutate()}
                className="inline-flex items-center gap-2 rounded-md bg-indigo-600 px-3 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {saveCapabilityMutation.isPending && <Loader2 className="h-4 w-4 animate-spin" />}
                {t('processLearning.processWideStep.saveAsCapabilityConfirmBtn')}
              </button>
            </div>
          </div>
        </div>
      )}

      {capabilitySavedSkillId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-lg bg-white p-6 text-center shadow-xl">
            <CheckCircle2 className="mx-auto h-10 w-10 text-green-600" />
            <h3 className="mt-3 text-base font-semibold text-gray-900">
              {t('processLearning.processWideStep.saveAsCapabilitySuccessTitle')}
            </h3>
            <p className="mt-2 text-sm text-gray-600">
              {t('processLearning.processWideStep.saveAsCapabilitySuccessDescription')}
            </p>
            <button
              type="button"
              onClick={() => setCapabilitySavedSkillId(null)}
              className="mt-5 rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-700"
            >
              {t('common.close')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
