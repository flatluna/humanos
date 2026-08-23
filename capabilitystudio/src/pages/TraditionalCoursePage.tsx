import { useCallback, useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import {
  BookOpen,
  FileText,
  HelpCircle,
  CheckCircle2,
  XCircle,
  Loader2,
  Menu,
  X,
  ListChecks,
  ChevronLeft,
  ChevronRight,
  Pencil,
  Plus,
  Sparkles,
  ArrowLeft,
} from 'lucide-react';
import {
  addTraditionalCourseChapter,
  addTraditionalCourseLesson,
  addTraditionalCourseLessonCard,
  deleteTraditionalCourseChapter,
  deleteTraditionalCourseLesson,
  editTraditionalCourseCardText,
  editTraditionalCourseChapterTitle,
  editTraditionalCourseLessonTitle,
  getTraditionalCourse,
  gradeChapterExam,
  setTraditionalCourseCardText,
  type TraditionalCourseCard,
  type TraditionalCourseDetail,
  type TraditionalCourseChapter,
  type TraditionalCourseLesson,
  type ChapterExamGradeResult,
} from '../lib/api/traditionalCourseApi';
import { apiImageUrl } from '../lib/api/httpClient';
import LoadingSpinner from '../components/LoadingSpinner';
import PreviewModeSwitcher from '../components/PreviewModeSwitcher';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import { usePreviewMode } from '../lib/previewMode';

/** One flattened lesson entry — used to look up "which sidebar item is
 * active" and to drive Previous/Next navigation across the whole course. */
interface FlatLessonEntry {
  chapter: TraditionalCourseChapter;
  chapterIndex: number;
  lesson: TraditionalCourseLesson;
  lessonIndex: number;
}

/**
 * Coursera-style Traditional Course viewer (redesigned 2026-08-09, replacing
 * the previous "expand every chapter, show every lesson at once" accordion).
 * A left sidebar is the course's table of contents (mirrors PreviewGraphPage's
 * course-contents sidebar) — it lists every chapter/lesson/exam so the
 * student can navigate freely, but the main panel only ever renders ONE
 * chapter's ONE lesson at a time, exactly like the Graph mode's one-node-at-
 * a-time experience. The Instructor Agent is no longer a small per-card
 * "talk about this" link — it's now the single, prominent way to consume a
 * lesson: a big orb button (VoiceTutorAgent's non-compact rendering) that
 * starts speaking the lesson content on its own shortly after arriving.
 */
type AiRewriteFeedbackState = {
  cardId: string | null;
  loading: boolean;
  message: string | null;
  error: string | null;
};

export default function TraditionalCoursePage() {
  const { t } = useTranslation();
  const { capabilityId } = useParams<{ capabilityId: string }>();
  const [mode, setMode] = usePreviewMode();
  const [course, setCourse] = useState<TraditionalCourseDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [selectedLessonId, setSelectedLessonId] = useState<string | null>(null);
  const [examChapterId, setExamChapterId] = useState<string | null>(null);
  const [addingChapter, setAddingChapter] = useState(false);
  const [addingLessonToChapterId, setAddingLessonToChapterId] = useState<string | null>(null);
  const [deletingChapterId, setDeletingChapterId] = useState<string | null>(null);
  const [deletingLessonId, setDeletingLessonId] = useState<string | null>(null);
  const [aiRewriteFeedback, setAiRewriteFeedback] = useState<AiRewriteFeedbackState>({
    cardId: null,
    loading: false,
    message: null,
    error: null,
  });

  const loadCourse = useCallback(() => {
    if (!capabilityId) return;
    getTraditionalCourse(capabilityId)
      .then((result) => {
        setCourse(result);
        setSelectedLessonId((current) => {
          if (current && result.Chapters.some((chapter) => chapter.Lessons.some((lesson) => lesson.LessonId === current))) {
            return current;
          }
          const firstLesson = result.Chapters[0]?.Lessons[0];
          return firstLesson?.LessonId ?? null;
        });
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [capabilityId]);

  useEffect(() => {
    loadCourse();
  }, [loadCourse]);

  const flatLessons = useMemo<FlatLessonEntry[]>(() => {
    if (!course) return [];
    const entries: FlatLessonEntry[] = [];
    course.Chapters.forEach((chapter, chapterIndex) => {
      chapter.Lessons.forEach((lesson, lessonIndex) => {
        entries.push({ chapter, chapterIndex, lesson, lessonIndex });
      });
    });
    return entries;
  }, [course]);

  const currentFlatIndex = flatLessons.findIndex((entry) => entry.lesson.LessonId === selectedLessonId);
  const current = currentFlatIndex >= 0 ? flatLessons[currentFlatIndex] : null;
  const previousEntry = currentFlatIndex > 0 ? flatLessons[currentFlatIndex - 1] : null;
  const nextEntry =
    currentFlatIndex >= 0 && currentFlatIndex < flatLessons.length - 1 ? flatLessons[currentFlatIndex + 1] : null;
  // The chapter's exam is only offered once its last lesson has been reached.
  const isLastLessonOfChapter = current ? current.lessonIndex === current.chapter.Lessons.length - 1 : false;
  const currentChapterHasExam = (current?.chapter.ExamQuestions.length ?? 0) > 0;

  function selectLesson(lessonId: string) {
    setSelectedLessonId(lessonId);
    setSidebarOpen(false);
  }

  function updateChapterTitle(chapterId: string, nextTitle: string) {
    setCourse((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        Chapters: prev.Chapters.map((chapter) =>
          chapter.ChapterId === chapterId ? { ...chapter, Title: nextTitle.trim() || chapter.Title } : chapter,
        ),
      };
    });
  }

  function updateLessonTitle(chapterId: string, lessonId: string, nextTitle: string) {
    setCourse((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        Chapters: prev.Chapters.map((chapter) =>
          chapter.ChapterId === chapterId
            ? {
                ...chapter,
                Lessons: chapter.Lessons.map((lesson) =>
                  lesson.LessonId === lessonId ? { ...lesson, Title: nextTitle.trim() || lesson.Title } : lesson,
                ),
              }
            : chapter,
        ),
      };
    });
  }

  function updateCardText(cardId: string, nextText: string, nextIllustrationId?: string | null) {
    setCourse((prev) => {
      if (!prev) return prev;
      return {
        ...prev,
        Chapters: prev.Chapters.map((chapter) => ({
          ...chapter,
          Lessons: chapter.Lessons.map((lesson) => ({
            ...lesson,
            Cards: lesson.Cards.map((card) =>
              card.CardId === cardId
                ? {
                    ...card,
                    Text: nextText.trim() || card.Text,
                    IllustrationId: nextIllustrationId !== undefined ? nextIllustrationId : card.IllustrationId,
                  }
                : card,
            ),
          })),
        })),
      };
    });
  }

  if (error) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16">
        <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-red-300">
          {t('traditionalCourse.loadError', { error })}
        </div>
      </div>
    );
  }

  if (!course) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <LoadingSpinner label={t('traditionalCourse.loading')} />
      </div>
    );
  }

  const examChapter = course.Chapters.find((c) => c.ChapterId === examChapterId) ?? null;

  return (
    <div className="mx-auto max-w-6xl px-6 py-12">
      <div className="mb-8 overflow-hidden rounded-3xl border border-white/10 bg-white/[0.03]">
        {course.CoverIllustrationId && (
          <div className="h-40 w-full overflow-hidden bg-slate-900">
            <img
              src={apiImageUrl(`/illustrations/traditional/${course.CoverIllustrationId}/image`)}
              alt=""
              className="h-full w-full object-contain object-center"
            />
          </div>
        )}
        <div className="p-6">
          <Link
            to="/catalog"
            className="mb-5 inline-flex items-center gap-1.5 text-sm font-medium text-slate-400 transition hover:text-white"
          >
            <ArrowLeft className="h-4 w-4" />
            {t('capabilityDetail.backToCatalog')}
          </Link>
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h1 className="text-2xl font-bold text-white">{course.Name}</h1>
              {course.Description && <p className="mt-2 text-slate-400">{course.Description}</p>}
              {course.ExecutiveSummary && (
                <p className="mt-2 rounded-xl border border-white/10 bg-white/[0.02] p-3 text-sm text-slate-300">
                  {course.ExecutiveSummary}
                </p>
              )}
            </div>
            <div className="flex flex-col items-start gap-3 sm:items-end">
              <PreviewModeSwitcher mode={mode} onChange={setMode} />
              {mode === 'edit' && (
                <button
                  type="button"
                  onClick={() => setAddingChapter(true)}
                  className="inline-flex items-center justify-center gap-2 rounded-xl border border-dashed border-white/15 bg-white/[0.03] px-4 py-2 text-sm font-medium text-slate-200 transition hover:border-brand-400/60 hover:text-brand-300"
                >
                  <Plus className="h-4 w-4" /> Add chapter
                </button>
              )}
            </div>
          </div>
          {mode === 'demo' && <p className="mt-3 text-xs text-amber-300">{t('structuredLearningMap.demoModeNotice')}</p>}
        </div>
      </div>

      {course.Chapters.length === 0 && (
        <div className="rounded-2xl border border-dashed border-white/10 bg-white/[0.02] p-8 text-center">
          <p className="text-slate-400">{t('traditionalCourse.noContent')}</p>
          {mode === 'edit' && (
            <button
              type="button"
              onClick={() => setAddingChapter(true)}
              className="mt-4 inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25"
            >
              <Plus className="h-4 w-4" /> Add first chapter
            </button>
          )}
        </div>
      )}

      {course.Chapters.length > 0 && (
        <>
          <button
            type="button"
            onClick={() => setSidebarOpen((open) => !open)}
            className="mx-auto mb-6 flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-2.5 text-sm font-medium text-slate-200 hover:bg-white/[0.06] lg:hidden"
          >
            {sidebarOpen ? <X className="h-4 w-4" /> : <Menu className="h-4 w-4" />}
            {sidebarOpen ? t('traditionalCourse.sidebarToggleHide') : t('traditionalCourse.sidebarToggleShow')}
          </button>

          <div className="flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10">
            <aside className={`${sidebarOpen ? 'block' : 'hidden'} w-full flex-none lg:sticky lg:top-6 lg:block lg:w-72 xl:w-80`}>
              <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 lg:flex lg:max-h-[calc(100vh-3rem)] lg:flex-col">
                <div className="flex items-center gap-2 text-sm font-semibold text-white">
                  <ListChecks className="h-4 w-4 text-brand-400" /> {t('traditionalCourse.sidebarTitle')}
                </div>
                <nav className="mt-3 space-y-3 overflow-y-auto pr-1 lg:min-h-0 lg:flex-1">
                  {/* Both index levels (chapter + its sections/lessons) are always
                      visible at once here — never collapsed behind a click — so
                      the sidebar reads as a real 2-level table of contents. */}
                  {course.Chapters.map((chapter, chapterIndex) => (
                    <div key={chapter.ChapterId} className="rounded-xl border border-white/10 bg-white/[0.02] p-2.5">
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0 flex-1 truncate text-[11px] font-semibold uppercase tracking-wide text-brand-300">
                          {t('traditionalCourse.chapterLabel', { number: chapterIndex + 1 })}: {chapter.Title}
                        </div>
                        {mode === 'edit' && (
                          <button
                            type="button"
                            onClick={() => setDeletingChapterId(chapter.ChapterId)}
                            className="shrink-0 text-[10px] font-medium text-red-300 transition hover:text-red-200"
                            title="Delete chapter"
                          >
                            Delete
                          </button>
                        )}
                      </div>
                      <ul className="mt-1.5 space-y-0.5">
                        {chapter.Lessons.map((lesson, lessonIndex) => {
                          const active = lesson.LessonId === selectedLessonId;
                          return (
                            <li key={lesson.LessonId}>
                              <button
                                type="button"
                                onClick={() => selectLesson(lesson.LessonId)}
                                className={`flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-xs transition ${
                                  active
                                    ? 'bg-brand-500/10 text-white ring-1 ring-inset ring-brand-400/40'
                                    : 'text-slate-300 hover:bg-white/[0.06]'
                                }`}
                              >
                                <BookOpen className="h-3.5 w-3.5 flex-none text-brand-400" />
                                <span className="line-clamp-2 flex-1">
                                  {t('traditionalCourse.lessonLabel', { number: lessonIndex + 1 })}: {lesson.Title}
                                </span>
                              </button>
                            </li>
                          );
                        })}
                        {chapter.ExamQuestions.length > 0 && (
                          <li>
                            <button
                              type="button"
                              onClick={() => setExamChapterId(chapter.ChapterId)}
                              className="flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-xs text-amber-300 transition hover:bg-white/[0.06]"
                            >
                              <HelpCircle className="h-3.5 w-3.5 flex-none" />
                              <span className="flex-1">{t('traditionalCourse.examNavLabel')}</span>
                            </button>
                          </li>
                        )}
                        {mode === 'edit' && (
                          <li>
                            <button
                              type="button"
                              onClick={() => setAddingLessonToChapterId(chapter.ChapterId)}
                              className="flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-xs text-brand-300 transition hover:bg-white/[0.06]"
                            >
                              <Plus className="h-3.5 w-3.5 flex-none" />
                              <span className="flex-1">Add lesson</span>
                            </button>
                          </li>
                        )}
                      </ul>
                    </div>
                  ))}
                </nav>
              </div>
            </aside>

            <div className="flex-1 rounded-2xl border border-white/10 bg-white/[0.02] p-6 sm:p-8">
              {current && (
                <LessonView
                  chapter={current.chapter}
                  chapterIndex={current.chapterIndex}
                  lesson={current.lesson}
                  lessonIndex={current.lessonIndex}
                  mode={mode}
                  aiRewriteFeedback={aiRewriteFeedback}
                  setAiRewriteFeedback={setAiRewriteFeedback}
                  onUpdateChapterTitle={updateChapterTitle}
                  onUpdateLessonTitle={updateLessonTitle}
                  onUpdateCardText={updateCardText}
                  onDeleteChapter={(chapterId) => setDeletingChapterId(chapterId)}
                  onDeleteLesson={(lessonId) => setDeletingLessonId(lessonId)}
                  onCourseRefresh={loadCourse}
                />
              )}

              <div className="mt-8 flex items-center justify-between gap-3 border-t border-white/10 pt-6">
                <button
                  type="button"
                  onClick={() => previousEntry && setSelectedLessonId(previousEntry.lesson.LessonId)}
                  disabled={!previousEntry}
                  className="flex items-center gap-1.5 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-2.5 text-sm font-medium text-slate-200 transition hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-30"
                >
                  <ChevronLeft className="h-4 w-4" /> {t('traditionalCourse.previousLesson')}
                </button>

                {isLastLessonOfChapter && currentChapterHasExam ? (
                  <button
                    type="button"
                    onClick={() => current && setExamChapterId(current.chapter.ChapterId)}
                    className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02]"
                  >
                    <HelpCircle className="h-4 w-4" />
                    {t('traditionalCourse.takeExam')}
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={() => nextEntry && setSelectedLessonId(nextEntry.lesson.LessonId)}
                    disabled={!nextEntry}
                    className="flex items-center gap-1.5 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-2.5 text-sm font-medium text-slate-200 transition hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-30"
                  >
                    {t('traditionalCourse.nextLesson')} <ChevronRight className="h-4 w-4" />
                  </button>
                )}
              </div>
            </div>
          </div>
        </>
      )}

      {addingChapter && capabilityId && (
        <AddTraditionalChapterModal
          capabilityId={capabilityId}
          onClose={() => setAddingChapter(false)}
          onCreated={() => {
            setAddingChapter(false);
            loadCourse();
          }}
        />
      )}

      {addingLessonToChapterId && (
        <AddTraditionalLessonModal
          chapterId={addingLessonToChapterId}
          onClose={() => setAddingLessonToChapterId(null)}
          onCreated={() => {
            setAddingLessonToChapterId(null);
            loadCourse();
          }}
        />
      )}

      {deletingChapterId && (
        <DeleteTraditionalCourseItemModal
          itemType="chapter"
          itemName={course.Chapters.find((chapter) => chapter.ChapterId === deletingChapterId)?.Title ?? 'Chapter'}
          onCancel={() => setDeletingChapterId(null)}
          onConfirm={async () => {
            await deleteTraditionalCourseChapter(deletingChapterId);
            setDeletingChapterId(null);
            loadCourse();
          }}
        />
      )}

      {deletingLessonId && (
        <DeleteTraditionalCourseItemModal
          itemType="lesson"
          itemName={
            course.Chapters.flatMap((chapter) => chapter.Lessons).find((lesson) => lesson.LessonId === deletingLessonId)?.Title ?? 'Lesson'
          }
          onCancel={() => setDeletingLessonId(null)}
          onConfirm={async () => {
            await deleteTraditionalCourseLesson(deletingLessonId);
            setDeletingLessonId(null);
            loadCourse();
          }}
        />
      )}

      {examChapter && <ExamModal chapter={examChapter} onClose={() => setExamChapterId(null)} />}

      {aiRewriteFeedback.cardId && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm">
          <div className="w-full max-w-md rounded-2xl border border-white/10 bg-slate-900 p-5 shadow-2xl">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-full bg-sky-500/15 text-sky-300">
                {aiRewriteFeedback.loading ? <Loader2 className="h-5 w-5 animate-spin" /> : <Sparkles className="h-5 w-5" />}
              </div>
              <div>
                <h3 className="text-lg font-semibold text-white">
                  {aiRewriteFeedback.loading ? 'AI rewrite running' : aiRewriteFeedback.error ? 'AI rewrite failed' : 'AI rewrite complete'}
                </h3>
                <p className="text-sm text-slate-300">
                  {aiRewriteFeedback.loading ? aiRewriteFeedback.message : aiRewriteFeedback.error ?? aiRewriteFeedback.message}
                </p>
              </div>
            </div>
            {!aiRewriteFeedback.loading && (
              <div className="mt-5 flex justify-end">
                <button
                  type="button"
                  onClick={() => setAiRewriteFeedback({ cardId: null, loading: false, message: null, error: null })}
                  className="rounded-xl border border-white/10 px-4 py-2 text-sm text-slate-200 hover:bg-white/[0.04]"
                >
                  Close
                </button>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function AddTraditionalChapterModal({
  capabilityId,
  onClose,
  onCreated,
}: {
  capabilityId: string;
  onClose: () => void;
  onCreated: () => void;
}) {
  const [title, setTitle] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit() {
    if (!title.trim() || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      await addTraditionalCourseChapter(capabilityId, title.trim());
      onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to add chapter.');
      setSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm" onClick={onClose}>
      <div className="w-full max-w-md rounded-2xl border border-white/10 bg-slate-900 p-5 shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-white">Add chapter</h3>
          <button type="button" onClick={onClose} className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>
        <label className="mb-1.5 block text-xs font-medium uppercase tracking-wide text-slate-400">Chapter title</label>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          className="input w-full"
          placeholder="e.g. Foundations"
          autoFocus
        />
        {error && <p className="mt-3 text-sm text-red-300">{error}</p>}
        <div className="mt-5 flex justify-end gap-3">
          <button type="button" onClick={onClose} className="rounded-xl border border-white/10 px-4 py-2 text-sm text-slate-200 hover:bg-white/[0.04]">
            Cancel
          </button>
          <button
            type="button"
            disabled={!title.trim() || submitting}
            onClick={handleSubmit}
            className="rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2 text-sm font-semibold text-[#fff] disabled:cursor-not-allowed disabled:opacity-40"
          >
            {submitting ? 'Saving...' : 'Add chapter'}
          </button>
        </div>
      </div>
    </div>
  );
}

function AddTraditionalLessonModal({
  chapterId,
  onClose,
  onCreated,
}: {
  chapterId: string;
  onClose: () => void;
  onCreated: () => void;
}) {
  const [title, setTitle] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit() {
    if (!title.trim() || submitting) return;
    setSubmitting(true);
    setError(null);
    try {
      await addTraditionalCourseLesson(chapterId, title.trim());
      onCreated();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to add lesson.');
      setSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm" onClick={onClose}>
      <div className="w-full max-w-md rounded-2xl border border-white/10 bg-slate-900 p-5 shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="mb-4 flex items-center justify-between">
          <h3 className="text-lg font-semibold text-white">Add lesson</h3>
          <button type="button" onClick={onClose} className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>
        <label className="mb-1.5 block text-xs font-medium uppercase tracking-wide text-slate-400">Lesson title</label>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          className="input w-full"
          placeholder="e.g. Key concepts"
          autoFocus
        />
        {error && <p className="mt-3 text-sm text-red-300">{error}</p>}
        <div className="mt-5 flex justify-end gap-3">
          <button type="button" onClick={onClose} className="rounded-xl border border-white/10 px-4 py-2 text-sm text-slate-200 hover:bg-white/[0.04]">
            Cancel
          </button>
          <button
            type="button"
            disabled={!title.trim() || submitting}
            onClick={handleSubmit}
            className="rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-2 text-sm font-semibold text-[#fff] disabled:cursor-not-allowed disabled:opacity-40"
          >
            {submitting ? 'Saving...' : 'Add lesson'}
          </button>
        </div>
      </div>
    </div>
  );
}

/** Renders exactly ONE lesson's content, then the big Instructor Agent
 * button — never the whole chapter/course at once. */
function LessonView({
  chapter,
  chapterIndex,
  lesson,
  lessonIndex,
  mode,
  aiRewriteFeedback,
  setAiRewriteFeedback,
  onUpdateChapterTitle,
  onUpdateLessonTitle,
  onUpdateCardText,
  onDeleteChapter,
  onDeleteLesson,
  onCourseRefresh,
}: {
  chapter: TraditionalCourseChapter;
  chapterIndex: number;
  lesson: TraditionalCourseLesson;
  lessonIndex: number;
  mode: 'real' | 'demo' | 'edit';
  aiRewriteFeedback: AiRewriteFeedbackState;
  setAiRewriteFeedback: Dispatch<SetStateAction<AiRewriteFeedbackState>>;
  onUpdateChapterTitle: (chapterId: string, nextTitle: string) => void;
  onUpdateLessonTitle: (chapterId: string, lessonId: string, nextTitle: string) => void;
  onUpdateCardText: (cardId: string, nextText: string, nextIllustrationId?: string | null) => void;
  onDeleteChapter: (chapterId: string) => void;
  onDeleteLesson: (lessonId: string) => void;
  onCourseRefresh: () => void;
}) {
  const { t } = useTranslation();
  const [editingChapterTitle, setEditingChapterTitle] = useState(false);
  const [chapterTitleDraft, setChapterTitleDraft] = useState(chapter.Title);
  const [editingLessonTitle, setEditingLessonTitle] = useState(false);
  const [lessonTitleDraft, setLessonTitleDraft] = useState(lesson.Title);
  const [editingCardId, setEditingCardId] = useState<string | null>(null);
  const [editingCardText, setEditingCardText] = useState('');
  const [emptyLessonDraft, setEmptyLessonDraft] = useState('');
  const [addingContent, setAddingContent] = useState(false);
  const [aiCardPrompt, setAiCardPrompt] = useState<Record<string, string>>({});

  async function saveChapterTitle() {
    const next = chapterTitleDraft.trim();
    if (!next) return;

    try {
      await editTraditionalCourseChapterTitle(chapter.ChapterId, `Rewrite this chapter title to: ${next}`);
      onUpdateChapterTitle(chapter.ChapterId, next);
      setEditingChapterTitle(false);
    } catch {
      onUpdateChapterTitle(chapter.ChapterId, next);
      setEditingChapterTitle(false);
    }
  }

  async function saveLessonTitle() {
    const next = lessonTitleDraft.trim();
    if (!next) return;

    try {
      await editTraditionalCourseLessonTitle(lesson.LessonId, `Rewrite this lesson title to: ${next}`);
      onUpdateLessonTitle(chapter.ChapterId, lesson.LessonId, next);
      setEditingLessonTitle(false);
    } catch {
      onUpdateLessonTitle(chapter.ChapterId, lesson.LessonId, next);
      setEditingLessonTitle(false);
    }
  }

  async function saveCardText(card: TraditionalCourseCard) {
    const next = editingCardText.trim();
    if (!next) return;

    try {
      const saved = await setTraditionalCourseCardText(card.CardId, next, true);
      onUpdateCardText(card.CardId, saved.Text, saved.IllustrationId);
    } catch (err) {
      console.error('Failed to save traditional course card text:', err);
    }

    setEditingCardId(null);
    setEditingCardText('');
  }

  async function applyAiCardRewrite(card: TraditionalCourseCard) {
    const prompt = (aiCardPrompt[card.CardId] ?? '').trim();
    if (!prompt) return;

    console.log('TraditionalCoursePage: AI rewrite requested', { cardId: card.CardId, prompt });
    setAiRewriteFeedback({ cardId: card.CardId, loading: true, message: 'AI rewrite in progress…', error: null });

    try {
      const updated = await editTraditionalCourseCardText(card.CardId, prompt);
      console.log('TraditionalCoursePage: AI rewrite response', { cardId: card.CardId, updated });
      onUpdateCardText(card.CardId, updated.Text, updated.IllustrationId);
      setAiRewriteFeedback({
        cardId: card.CardId,
        loading: false,
        message: 'AI rewrite applied successfully.',
        error: null,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unable to apply AI rewrite.';
      console.error('TraditionalCoursePage: Failed to apply AI rewrite', { cardId: card.CardId, err, prompt });
      setAiRewriteFeedback({
        cardId: card.CardId,
        loading: false,
        message: null,
        error: message,
      });
      return;
    }

    setAiCardPrompt((prev) => ({ ...prev, [card.CardId]: '' }));
    window.setTimeout(() => {
      setAiRewriteFeedback((prev) => (prev.cardId === card.CardId ? { cardId: null, loading: false, message: null, error: null } : prev));
    }, 1800);
  }

  function updateAiPrompt(cardId: string, nextValue: string) {
    setAiCardPrompt((prev) => ({ ...prev, [cardId]: nextValue }));
  }

  async function createLessonCardFromEmptyState() {
    const nextText = emptyLessonDraft.trim();
    if (!nextText || addingContent) return;

    setAddingContent(true);
    try {
      await addTraditionalCourseLessonCard(lesson.LessonId, lesson.Title, nextText);
      setEmptyLessonDraft('');
      onCourseRefresh();
    } catch (err) {
      console.error('Failed to add content to a new traditional course lesson:', err);
    } finally {
      setAddingContent(false);
    }
  }

  return (
    <div key={lesson.LessonId} className="animate-fade-in">
      <div className="text-[11px] uppercase tracking-wide text-slate-500">
        {t('traditionalCourse.chapterLabel', { number: chapterIndex + 1 })}: 
        {editingChapterTitle && mode === 'edit' ? (
          <span className="ml-2 inline-flex items-center gap-2">
            <input
              value={chapterTitleDraft}
              onChange={(e) => setChapterTitleDraft(e.target.value)}
              className="input inline-flex w-52 rounded-lg border border-white/10 bg-white/[0.04] px-2 py-1 text-[11px] text-white"
              autoFocus
            />
            <button type="button" onClick={saveChapterTitle} className="rounded-md bg-brand-500 px-2 py-1 text-[10px] font-semibold text-white">Save</button>
            <button type="button" onClick={() => setEditingChapterTitle(false)} className="rounded-md border border-white/10 px-2 py-1 text-[10px] text-slate-300">Cancel</button>
          </span>
        ) : (
          <span className="ml-2">{chapter.Title}</span>
        )}
        {mode === 'edit' && !editingChapterTitle && (
          <>
            <button type="button" onClick={() => { setChapterTitleDraft(chapter.Title); setEditingChapterTitle(true); }} className="ml-2 text-[10px] font-medium text-sky-300 hover:text-sky-200">Edit chapter</button>
            <button type="button" onClick={() => onDeleteChapter(chapter.ChapterId)} className="ml-2 text-[10px] font-medium text-red-300 hover:text-red-200">Delete chapter</button>
          </>
        )}
      </div>
      <h2 className="mt-1 flex items-center gap-2 text-xl font-bold text-white">
        <BookOpen className="h-5 w-5 text-brand-400" />
        {t('traditionalCourse.lessonLabel', { number: lessonIndex + 1 })}: 
        {editingLessonTitle && mode === 'edit' ? (
          <span className="inline-flex items-center gap-2">
            <input
              value={lessonTitleDraft}
              onChange={(e) => setLessonTitleDraft(e.target.value)}
              className="input inline-flex w-72 rounded-lg border border-white/10 bg-white/[0.04] px-2 py-1 text-base text-white"
              autoFocus
            />
            <button type="button" onClick={saveLessonTitle} className="rounded-md bg-brand-500 px-2 py-1 text-[10px] font-semibold text-white">Save</button>
            <button type="button" onClick={() => setEditingLessonTitle(false)} className="rounded-md border border-white/10 px-2 py-1 text-[10px] text-slate-300">Cancel</button>
          </span>
        ) : (
          <span>{lesson.Title}</span>
        )}
        {mode === 'edit' && !editingLessonTitle && (
          <>
            <button type="button" onClick={() => { setLessonTitleDraft(lesson.Title); setEditingLessonTitle(true); }} className="text-sm font-medium text-sky-300 hover:text-sky-200">Edit lesson</button>
            <button type="button" onClick={() => onDeleteLesson(lesson.LessonId)} className="text-sm font-medium text-red-300 hover:text-red-200">Delete lesson</button>
          </>
        )}
      </h2>

      {/* The Instructor Agent — the single, prominent way to actually be
          taught this lesson, shown first so the student sees/hears the
          professor right away instead of scrolling to the bottom. */}
      <VoiceTutorAgent
        key={lesson.LessonId}
        traditionalCourseLessonId={lesson.LessonId}
        startLabel={t('traditionalCourse.startTeaching')}
        autoConnect
      />

      <div className="mt-5 space-y-4">
        {lesson.Cards.length === 0 && mode === 'edit' ? (
          <div className="rounded-xl border border-dashed border-sky-400/30 bg-sky-500/[0.04] p-4">
            <div className="mb-3 flex items-center gap-2 text-sm font-semibold text-sky-300">
              <FileText className="h-4 w-4" /> Lesson content is empty
            </div>
            <p className="mb-3 text-sm text-slate-300">Add the first teaching card for this lesson.</p>
            <textarea
              value={emptyLessonDraft}
              onChange={(e) => setEmptyLessonDraft(e.target.value)}
              rows={6}
              placeholder="Write the first lesson explanation here..."
              className="input w-full"
            />
            <div className="mt-3 flex gap-2">
              <button
                type="button"
                onClick={() => {
                  void createLessonCardFromEmptyState();
                }}
                disabled={!emptyLessonDraft.trim() || addingContent}
                className="rounded-lg bg-sky-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
              >
                {addingContent ? 'Creating…' : 'Create lesson content'}
              </button>
            </div>
          </div>
        ) : (
          lesson.Cards.map((card) => (
            <div key={card.CardId} className="rounded-xl border border-white/10 bg-white/[0.02] p-4">
              {card.IllustrationId && (
                <img
                  src={apiImageUrl(`/illustrations/traditional/${card.IllustrationId}/image`)}
                  alt=""
                  className="mb-3 max-h-64 w-full rounded-lg object-contain object-center"
                />
              )}
              <div className="flex items-start gap-2">
                <FileText className="mt-0.5 h-4 w-4 shrink-0 text-slate-500" />
                <div className="w-full">
                  <div className="flex items-center justify-between gap-3">
                    <div className="text-sm font-medium text-white">{card.Title}</div>
                    {mode === 'edit' && (
                      <span className="text-[10px] uppercase tracking-wide text-sky-300">Edit card</span>
                    )}
                  </div>
                  <p className="mt-1 whitespace-pre-line text-sm text-slate-400">{card.Text}</p>

                  {mode === 'edit' && (
                    <div className="mt-3 space-y-3 rounded-xl border border-sky-400/20 bg-sky-500/[0.04] p-3">
                      <div className="flex items-center gap-2 text-xs font-semibold text-sky-300">
                        <Sparkles className="h-3.5 w-3.5" /> Edit with AI
                      </div>
                      <textarea
                        value={aiCardPrompt[card.CardId] ?? ''}
                        onInput={(e) => updateAiPrompt(card.CardId, (e.target as HTMLTextAreaElement).value)}
                        onChange={(e) => updateAiPrompt(card.CardId, e.target.value)}
                        rows={2}
                        placeholder="Tell the AI how to rewrite this card text or image direction..."
                        className="input w-full"
                      />
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={() => {
                            console.log('TraditionalCoursePage: Apply AI rewrite clicked', { cardId: card.CardId, prompt: aiCardPrompt[card.CardId] ?? '' });
                            void applyAiCardRewrite(card);
                          }}
                          disabled={!((aiCardPrompt[card.CardId] ?? '').trim()) || aiRewriteFeedback.loading}
                          className="rounded-lg bg-sky-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
                        >
                          {aiRewriteFeedback.cardId === card.CardId && aiRewriteFeedback.loading ? 'Applying…' : 'Apply AI rewrite'}
                        </button>
                        <button
                          type="button"
                          onClick={() => setEditingCardId(editingCardId === card.CardId ? null : card.CardId)}
                          className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-semibold text-slate-300 hover:bg-white/[0.08]"
                        >
                          {editingCardId === card.CardId ? 'Close manual editor' : 'Edit manually'}
                        </button>
                      </div>

                      {editingCardId === card.CardId && (
                        <div className="mt-2 rounded-xl border border-amber-400/20 bg-amber-500/[0.04] p-3">
                          <p className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold text-amber-300">
                            <Pencil className="h-3.5 w-3.5" /> Manual edit
                          </p>
                          <textarea
                            value={editingCardText || card.Text}
                            onChange={(e) => setEditingCardText(e.target.value)}
                            rows={6}
                            className="input w-full text-sm"
                          />
                          <div className="mt-2 flex gap-2">
                            <button
                              type="button"
                              onClick={() => saveCardText(card)}
                              disabled={!editingCardText.trim()}
                              className="rounded-lg bg-amber-500 px-3 py-1.5 text-xs font-semibold text-white disabled:opacity-50"
                            >
                              Save text
                            </button>
                            <button
                              type="button"
                              onClick={() => {
                                setEditingCardId(null);
                                setEditingCardText('');
                              }}
                              className="rounded-lg border border-white/10 bg-white/[0.03] px-3 py-1.5 text-xs font-semibold text-slate-300 hover:bg-white/[0.08]"
                            >
                              Cancel
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

function DeleteTraditionalCourseItemModal({
  itemType,
  itemName,
  onConfirm,
  onCancel,
}: {
  itemType: 'chapter' | 'lesson';
  itemName: string;
  onConfirm: () => Promise<void>;
  onCancel: () => void;
}) {
  const [confirmationText, setConfirmationText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isConfirmationValid = confirmationText.trim() === itemName.trim();

  async function handleConfirm() {
    if (!isConfirmationValid || isDeleting) return;
    setIsDeleting(true);
    setError(null);

    try {
      await onConfirm();
    } catch (err) {
      setError(err instanceof Error ? err.message : `Could not delete the ${itemType}.`);
      setIsDeleting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/60 p-4" role="dialog" aria-modal="true">
      <div className="w-full max-w-lg rounded-2xl border-2 border-red-600 bg-slate-900 p-6 shadow-2xl">
        <div className="mb-4 flex items-start gap-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-full border border-red-500/30 bg-red-500/10">
            <XCircle className="h-7 w-7 text-red-500" />
          </div>
          <div>
            <h3 className="text-xl font-bold text-red-500">Permanently delete {itemType}</h3>
            <p className="mt-1 text-sm text-slate-400">This action cannot be undone.</p>
          </div>
        </div>

        <div className="mb-4 rounded-lg border border-red-500/30 bg-red-500/10 p-4 text-sm text-red-200">
          <p>You are about to delete the {itemType} "{itemName}" and everything inside it.</p>
          <ul className="mt-2 list-disc space-y-1 pl-5">
            <li>{itemType === 'chapter' ? 'Its lessons and final exam questions' : 'Its lesson cards and illustrations'}</li>
            <li>{itemType === 'chapter' ? 'Any lesson card content under it' : 'The lesson’s generated illustration references'}</li>
            <li>The course numbering will be adjusted after removal.</li>
          </ul>
          <p className="mt-2 font-semibold">None of this can be recovered after confirming.</p>
        </div>

        <label className="mb-1 block text-sm font-medium text-slate-300">
          Type <span className="font-bold">{itemName}</span> to confirm:
        </label>
        <input
          type="text"
          value={confirmationText}
          onChange={(e) => setConfirmationText(e.target.value)}
          disabled={isDeleting}
          className="mb-2 w-full rounded-lg border border-white/10 bg-white/5 px-3 py-2 text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-red-500/50 disabled:opacity-50"
          autoFocus
        />

        {error && <p className="mb-2 text-sm text-red-400">{error}</p>}

        <div className="mt-6 flex justify-end gap-3">
          <button type="button" onClick={onCancel} disabled={isDeleting} className="rounded-lg px-4 py-2 font-medium text-slate-300 hover:bg-white/5 disabled:opacity-50">
            Cancel
          </button>
          <button type="button" onClick={() => void handleConfirm()} disabled={!isConfirmationValid || isDeleting} className="rounded-lg bg-red-600 px-4 py-2 font-medium text-white hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-40">
            {isDeleting ? 'Deleting...' : 'Delete permanently'}
          </button>
        </div>
      </div>
    </div>
  );
}

function ExamModal({ chapter, onClose }: { chapter: TraditionalCourseChapter; onClose: () => void }) {
  const { t } = useTranslation();
  const [answers, setAnswers] = useState<Record<string, number>>({});
  const [result, setResult] = useState<ChapterExamGradeResult | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const allAnswered = chapter.ExamQuestions.every((q) => answers[q.QuestionId] !== undefined);

  async function handleSubmit() {
    if (!allAnswered) {
      setError(t('traditionalCourse.answerAllQuestions'));
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const gradeResult = await gradeChapterExam(
        chapter.ChapterId,
        Object.entries(answers).map(([questionId, selectedOptionIndex]) => ({ questionId, selectedOptionIndex })),
      );
      setResult(gradeResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('traditionalCourse.submitError'));
    } finally {
      setSubmitting(false);
    }
  }

  function retry() {
    setResult(null);
    setAnswers({});
    setError(null);
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm">
      <div className="max-h-[85vh] w-full max-w-2xl overflow-y-auto rounded-3xl border border-white/10 bg-slate-900 p-6 shadow-2xl">
        <div className="mb-5">
          <h2 className="text-xl font-bold text-white">{t('traditionalCourse.examTitle', { title: chapter.Title })}</h2>
          <p className="mt-1 text-sm text-slate-400">{t('traditionalCourse.examSubtitle')}</p>
        </div>

        {!result && (
          <div className="space-y-5">
            {chapter.ExamQuestions.map((question, index) => (
              <div key={question.QuestionId} className="rounded-xl border border-white/10 bg-white/[0.02] p-4">
                <div className="mb-2 text-[11px] uppercase tracking-wide text-slate-500">
                  {t('traditionalCourse.questionLabel', { number: index + 1 })}
                </div>
                <div className="mb-3 font-medium text-white">{question.QuestionText}</div>
                <div className="space-y-2">
                  {question.Options.map((option, optionIndex) => {
                    const selected = answers[question.QuestionId] === optionIndex;
                    return (
                      <label
                        key={optionIndex}
                        className={`flex cursor-pointer items-center gap-2.5 rounded-lg border px-3 py-2 text-sm transition-colors ${
                          selected
                            ? 'border-brand-400/60 bg-brand-500/10 text-white'
                            : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                        }`}
                      >
                        <input
                          type="radio"
                          name={question.QuestionId}
                          checked={selected}
                          onChange={() => setAnswers((prev) => ({ ...prev, [question.QuestionId]: optionIndex }))}
                          className="h-4 w-4 text-brand-500 focus:ring-brand-500"
                        />
                        {option}
                      </label>
                    );
                  })}
                </div>
              </div>
            ))}

            {error && <p className="text-sm text-red-300">{error}</p>}

            <div className="flex items-center justify-between pt-2">
              <button type="button" onClick={onClose} className="text-sm font-medium text-slate-400 hover:text-white">
                {t('traditionalCourse.closeExam')}
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={submitting}
                className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform enabled:hover:scale-[1.02] disabled:cursor-not-allowed disabled:opacity-40"
              >
                {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
                {submitting ? t('traditionalCourse.submitting') : t('traditionalCourse.submitExam')}
              </button>
            </div>
          </div>
        )}

        {result && (
          <div className="space-y-5">
            <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5 text-center">
              <h3 className="text-sm font-semibold text-slate-400">{t('traditionalCourse.scoreTitle')}</h3>
              <div className="mt-1 text-3xl font-bold text-white">{result.ScorePercent}%</div>
              <p className="mt-1 text-sm text-slate-400">
                {t('traditionalCourse.scoreSummary', {
                  correct: result.CorrectCount,
                  total: result.TotalQuestions,
                  percent: result.ScorePercent,
                })}
              </p>
            </div>

            <div className="space-y-3">
              {chapter.ExamQuestions.map((question, index) => {
                const questionResult = result.Questions.find((q) => q.QuestionId === question.QuestionId);
                if (!questionResult) return null;
                return (
                  <div
                    key={question.QuestionId}
                    className={`rounded-xl border p-4 ${
                      questionResult.IsCorrect ? 'border-emerald-500/30 bg-emerald-500/5' : 'border-red-500/30 bg-red-500/5'
                    }`}
                  >
                    <div className="mb-2 flex items-center gap-2 text-[11px] uppercase tracking-wide text-slate-500">
                      {t('traditionalCourse.questionLabel', { number: index + 1 })}
                      {questionResult.IsCorrect ? (
                        <span className="flex items-center gap-1 text-emerald-400">
                          <CheckCircle2 className="h-3.5 w-3.5" /> {t('traditionalCourse.correctAnswer')}
                        </span>
                      ) : (
                        <span className="flex items-center gap-1 text-red-400">
                          <XCircle className="h-3.5 w-3.5" /> {t('traditionalCourse.incorrectAnswer')}
                        </span>
                      )}
                    </div>
                    <div className="mb-2 font-medium text-white">{question.QuestionText}</div>
                    <div className="space-y-1.5 text-sm">
                      {question.Options.map((option, optionIndex) => {
                        const isCorrectOption = optionIndex === questionResult.CorrectOptionIndex;
                        const isSelectedOption = optionIndex === questionResult.SelectedOptionIndex;
                        return (
                          <div
                            key={optionIndex}
                            className={`rounded-lg px-3 py-1.5 ${
                              isCorrectOption
                                ? 'bg-emerald-500/15 text-emerald-300'
                                : isSelectedOption
                                  ? 'bg-red-500/15 text-red-300'
                                  : 'text-slate-400'
                            }`}
                          >
                            {option}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                );
              })}
            </div>

            <div className="flex items-center justify-between pt-2">
              <button type="button" onClick={onClose} className="text-sm font-medium text-slate-400 hover:text-white">
                {t('traditionalCourse.backToCourse')}
              </button>
              <button
                type="button"
                onClick={retry}
                className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02]"
              >
                {t('traditionalCourse.retryExam')}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
