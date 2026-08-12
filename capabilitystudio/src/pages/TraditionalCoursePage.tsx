import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router-dom';
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
} from 'lucide-react';
import {
  getTraditionalCourse,
  gradeChapterExam,
  type TraditionalCourseDetail,
  type TraditionalCourseChapter,
  type TraditionalCourseLesson,
  type ChapterExamGradeResult,
} from '../lib/api/traditionalCourseApi';
import { apiImageUrl } from '../lib/api/httpClient';
import LoadingSpinner from '../components/LoadingSpinner';
import VoiceTutorAgent from '../components/VoiceTutorAgent';

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
export default function TraditionalCoursePage() {
  const { t } = useTranslation();
  const { capabilityId } = useParams<{ capabilityId: string }>();
  const [course, setCourse] = useState<TraditionalCourseDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [selectedLessonId, setSelectedLessonId] = useState<string | null>(null);
  const [examChapterId, setExamChapterId] = useState<string | null>(null);

  useEffect(() => {
    if (!capabilityId) return;
    getTraditionalCourse(capabilityId)
      .then((result) => {
        setCourse(result);
        const firstLesson = result.Chapters[0]?.Lessons[0];
        if (firstLesson) setSelectedLessonId(firstLesson.LessonId);
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [capabilityId]);

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
          <h1 className="text-2xl font-bold text-white">{course.Name}</h1>
          {course.Description && <p className="mt-2 text-slate-400">{course.Description}</p>}
        </div>
      </div>

      {course.Chapters.length === 0 && (
        <p className="text-center text-slate-400">{t('traditionalCourse.noContent')}</p>
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
                      <div className="truncate text-[11px] font-semibold uppercase tracking-wide text-brand-300">
                        {t('traditionalCourse.chapterLabel', { number: chapterIndex + 1 })}: {chapter.Title}
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

      {examChapter && <ExamModal chapter={examChapter} onClose={() => setExamChapterId(null)} />}
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
}: {
  chapter: TraditionalCourseChapter;
  chapterIndex: number;
  lesson: TraditionalCourseLesson;
  lessonIndex: number;
}) {
  const { t } = useTranslation();
  return (
    <div key={lesson.LessonId} className="animate-fade-in">
      <div className="text-[11px] uppercase tracking-wide text-slate-500">
        {t('traditionalCourse.chapterLabel', { number: chapterIndex + 1 })}: {chapter.Title}
      </div>
      <h2 className="mt-1 flex items-center gap-2 text-xl font-bold text-white">
        <BookOpen className="h-5 w-5 text-brand-400" />
        {t('traditionalCourse.lessonLabel', { number: lessonIndex + 1 })}: {lesson.Title}
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
        {lesson.Cards.map((card) => (
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
              <div>
                <div className="text-sm font-medium text-white">{card.Title}</div>
                <p className="mt-1 whitespace-pre-line text-sm text-slate-400">{card.Text}</p>
              </div>
            </div>
          </div>
        ))}
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
