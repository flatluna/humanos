import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import {
  ArrowLeft,
  Check,
  Lightbulb,
  BookOpenText,
  Layers,
  Brain,
  ClipboardCheck,
  CheckCircle2,
  Loader2,
  Send,
  X,
  Network,
  Video,
  Flame,
  Zap,
  RefreshCw,
  Target,
  Info,
  Sparkles,
  Download,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import {
  getStructuredLearningDocument,
  getStructuredLearningHypothesisResponses,
  submitStructuredLearningHypothesisResponses,
  submitStructuredLearningRecallAttempt,
  getStructuredLearningRecallState,
  startStructuredLearningAssessmentRound,
  getActiveStructuredLearningAssessmentRound,
  submitStructuredLearningAssessmentAnswer,
  listStructuredLearningSectionVideos,
  type StructuredLearningDocumentDetail,
  type StructuredLearningStepDetail,
  type StructuredLearningAssessmentRoundState,
  type StructuredLearningSectionVideo,
} from '../api/structuredLearningRuntimeApi';
import {
  getEngramImpactScores,
  getLearnerProfile,
  type EngramImpactScoreDto,
  type LearnerProfileSummaryDto,
} from '../api/engramImpactApi';
import { apiImageUrl } from '../api/httpClient';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import KnowledgeGraphSection from '../components/KnowledgeGraphSection';
import { RewardCelebration } from '@/features/gamification/components/RewardCelebration';
import { ReflectionPause } from '@/features/gamification/components/ReflectionPause';
import type { GamificationRewardBundleDto } from '@/features/gamification/types';

const STEP_ORDER = ['Hypothesis', 'Teaching', 'DeepDive', 'Recall', 'Production', 'Assessment'];
const GATED_STEP_TYPES = new Set(['DeepDive', 'Recall', 'Assessment']);
const RECALL_MAX_ATTEMPTS = 5;

const STEP_META: Record<string, { labelEn: string; labelEs: string; Icon: typeof Lightbulb }> = {
  Hypothesis: { labelEn: 'Before you start', labelEs: 'Antes de iniciar', Icon: Lightbulb },
  Teaching: { labelEn: 'Learn', labelEs: 'Enseñanza', Icon: BookOpenText },
  DeepDive: { labelEn: 'Deep dive', labelEs: 'Profundización', Icon: Layers },
  Recall: { labelEn: 'Recall', labelEs: 'Evocación', Icon: Brain },
  Assessment: { labelEn: 'Assessment', labelEs: 'Evaluación', Icon: ClipboardCheck },
};

type HypothesisQuestion = {
  question: string;
  options: string[];
  correctIndex: number | null;
};

function parseHypothesisQuestions(step: StructuredLearningStepDetail): HypothesisQuestion[] | null {
  if (!step || !step.StepDataJson) return null;

  try {
    const parsed = JSON.parse(step.StepDataJson) as unknown;
    const questionList = Array.isArray(parsed)
      ? parsed
      : Array.isArray((parsed as { questions?: unknown })?.questions)
        ? (parsed as { questions: unknown[] }).questions
        : Array.isArray((parsed as { Questions?: unknown })?.Questions)
          ? (parsed as { Questions: unknown[] }).Questions
          : null;

    if (!questionList) return null;

    const normalized = questionList
      .map((item) => {
        const q = item as Record<string, unknown>;
        const question =
          typeof q.question === 'string'
            ? q.question
            : typeof q.Question === 'string'
              ? q.Question
              : typeof q.prompt === 'string'
                ? q.prompt
                : typeof q.Prompt === 'string'
                  ? q.Prompt
                  : '';
        const optionList = Array.isArray(q.options)
          ? q.options
          : Array.isArray(q.Options)
            ? q.Options
            : Array.isArray(q.choices)
              ? q.choices
              : Array.isArray(q.Choices)
                ? q.Choices
                : [];
        const options = optionList
          .map((option) => (typeof option === 'string' ? option : String(option ?? '').trim()))
          .filter((option) => option.length > 0);
        const correctIndex =
          typeof q.correctIndex === 'number'
            ? q.correctIndex
            : typeof q.CorrectIndex === 'number'
              ? q.CorrectIndex
              : typeof q.correctAnswerIndex === 'number'
                ? q.correctAnswerIndex
                : typeof q.CorrectAnswerIndex === 'number'
                  ? q.CorrectAnswerIndex
                  : null;

        if (!question.trim() || options.length === 0) return null;

        return {
          question: question.trim(),
          options,
          correctIndex: correctIndex != null && correctIndex >= 0 && correctIndex < options.length ? correctIndex : null,
        };
      })
      .filter((question): question is HypothesisQuestion => question !== null);

    return normalized.length > 0 ? normalized : null;
  } catch {
    return null;
  }
}

function HypothesisQuestionCard({
  sectionId,
  personId,
  step,
  isEn,
}: {
  sectionId: string;
  personId: string;
  step: StructuredLearningStepDetail;
  isEn: boolean;
}) {
  const questions = parseHypothesisQuestions(step);
  const [selectedAnswers, setSelectedAnswers] = useState<Record<number, number>>({});
  const [submitted, setSubmitted] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!step.StructuredLearningSectionStepId || !personId) return;
    getStructuredLearningHypothesisResponses(sectionId, personId)
      .then((saved) => {
        if (saved.length === 0) return;
        const restored = saved.reduce<Record<number, number>>((acc, r) => {
          acc[r.QuestionIndex] = r.SelectedOptionIndex;
          return acc;
        }, {});
        setSelectedAnswers(restored);
        setSubmitted(true);
      })
      .catch(() => {});
  }, [sectionId, personId, step.StructuredLearningSectionStepId]);

  if (!questions || questions.length === 0) return null;

  const answeredCount = Object.keys(selectedAnswers).length;
  const isComplete = questions.length > 0 && answeredCount === questions.length && questions.every((_, index) => selectedAnswers[index] !== undefined);

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      await submitStructuredLearningHypothesisResponses(
        sectionId,
        personId,
        Object.entries(selectedAnswers).map(([questionIndex, selectedOptionIndex]) => ({
          questionIndex: Number(questionIndex),
          selectedOptionIndex,
        }))
      );
      setSubmitted(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mt-4 space-y-4 rounded-2xl border border-indigo-200 bg-indigo-50/40 dark:border-indigo-500/20 dark:bg-indigo-500/[0.04] p-4">
      <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-indigo-700 dark:text-indigo-300">
        {isEn ? 'Before we start' : 'Antes de comenzar'}
      </p>

      {submitted ? (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm font-medium text-emerald-900 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-100">
          {isEn ? 'Thank you for your responses. We now will start learning.' : 'Gracias por tus respuestas. Ahora comenzaremos a aprender.'}
        </div>
      ) : (
        <div className="space-y-4">
          {error && <p className="text-sm text-red-600 dark:text-red-300">{error}</p>}
          {questions.map((question, index) => {
            const selectedIndex = selectedAnswers[index];

            return (
              <div key={`${question.question}-${index}`} className="rounded-xl border border-slate-200 bg-white p-3 dark:border-white/10 dark:bg-slate-950/20">
                <p className="text-sm font-semibold text-slate-900 dark:text-white">
                  {index + 1}. {question.question}
                </p>
                <div className="mt-3 space-y-2">
                  {question.options.map((option, optionIndex) => {
                    const isSelected = selectedIndex === optionIndex;

                    return (
                      <button
                        key={`${question.question}-${optionIndex}`}
                        type="button"
                        onClick={() => setSelectedAnswers((previous) => ({ ...previous, [index]: optionIndex }))}
                        className={`flex w-full items-center gap-3 rounded-xl border px-3.5 py-2.5 text-left text-sm transition ${
                          isSelected
                            ? 'border-indigo-500 bg-indigo-50 font-semibold text-indigo-950 dark:border-indigo-500/50 dark:bg-indigo-500/10 dark:text-white'
                            : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50 dark:border-white/10 dark:bg-white/[0.02] dark:text-slate-200 dark:hover:border-white/20 dark:hover:bg-white/[0.04]'
                        }`}
                      >
                        <span
                          className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold ${
                            isSelected ? 'bg-indigo-600 text-white dark:bg-indigo-500/20 dark:text-indigo-100' : 'bg-slate-100 text-slate-700 dark:bg-white/5 dark:text-slate-300'
                          }`}
                        >
                          {String.fromCharCode(65 + optionIndex)}
                        </span>
                        <span className="flex-1">{option}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            );
          })}

          <div className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3 dark:border-white/10 dark:bg-white/[0.02]">
            <p className="text-xs font-medium text-slate-600 dark:text-slate-300">
              {answeredCount}/{questions.length} {isEn ? 'answered' : 'respondidas'}
            </p>
            <button
              type="button"
              disabled={!isComplete || saving}
              onClick={handleSave}
              className="inline-flex items-center justify-center rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 px-4 py-2 text-xs font-bold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {saving ? (isEn ? 'Saving…' : 'Guardando…') : (isEn ? 'Save responses' : 'Guardar respuestas')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

export default function StructuredLearningSectionPage() {
  const navigate = useNavigate();
  const { documentId, sectionId } = useParams<{ documentId: string; sectionId: string }>();
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';

  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [stepIndex, setStepIndex] = useState(0);
  const [maxReachedIndex, setMaxReachedIndex] = useState(0);
  const [showKnowledgeGraph, setShowKnowledgeGraph] = useState(false);
  const [knowledgeGraphInitialView, setKnowledgeGraphInitialView] = useState<'graph' | 'facts'>('graph');

  // Engram Impact Score & Learner Profile state
  const [impactScores, setImpactScores] = useState<EngramImpactScoreDto[] | null>(null);
  const [learnerProfile, setLearnerProfile] = useState<LearnerProfileSummaryDto | null>(null);
  const [showScoreModal, setShowScoreModal] = useState(false);

  useEffect(() => {
    if (!documentId) return;
    getStructuredLearningDocument(documentId)
      .then(setDocument)
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [documentId]);

  useEffect(() => {
    if (!documentId) return;
    getEngramImpactScores(documentId, personId)
      .then(setImpactScores)
      .catch((err) => console.error('Failed to load engram impact scores on section page:', err));
  }, [documentId, personId]);

  useEffect(() => {
    getLearnerProfile(personId)
      .then(setLearnerProfile)
      .catch((err) => console.warn('Could not load learner profile summary:', err));
  }, [personId]);

  useEffect(() => {
    setStepIndex(0);
    setMaxReachedIndex(0);
  }, [sectionId]);

  const sections = document?.Sections ?? [];
  const currentIndex = sections.findIndex((s) => s.StructuredLearningDocumentSectionId === sectionId);
  const currentSection = currentIndex >= 0 ? sections[currentIndex] : null;
  const previousSection = currentIndex > 0 ? sections[currentIndex - 1] : null;
  const nextSection = currentIndex >= 0 && currentIndex < sections.length - 1 ? sections[currentIndex + 1] : null;

  const currentEngramScore = useMemo(() => {
    if (!impactScores || !sectionId) return null;
    return impactScores.find((s) => s.engramId.toLowerCase() === sectionId.toLowerCase()) ?? null;
  }, [impactScores, sectionId]);

  const steps = useMemo(
    () => (currentSection?.Steps ?? []).filter((s) => s.StepType !== 'Production'),
    [currentSection]
  );
  const currentStep = steps[stepIndex] ?? null;
  const isFirstStep = stepIndex === 0;
  const isLastStep = stepIndex === steps.length - 1;

  useEffect(() => {
    if (!currentStep) return;
    if (!GATED_STEP_TYPES.has(currentStep.StepType)) {
      setMaxReachedIndex((m) => Math.max(m, stepIndex + 1));
    }
  }, [currentStep, stepIndex]);

  const handleStepComplete = useCallback(() => {
    setMaxReachedIndex((m) => Math.max(m, stepIndex + 1));
  }, [stepIndex]);

  const canAdvancePastCurrentStep = stepIndex < maxReachedIndex;

  function goToStep(index: number) {
    if (index > maxReachedIndex) return;
    setStepIndex(index);
  }

  function goPrevious() {
    if (!isFirstStep) {
      setStepIndex((i) => i - 1);
      return;
    }
    if (previousSection) {
      navigate(`/structured-learning/${documentId}/sections/${previousSection.StructuredLearningDocumentSectionId}`);
    }
  }

  function goNext() {
    if (!canAdvancePastCurrentStep) return;
    if (!isLastStep) {
      setStepIndex((i) => i + 1);
      return;
    }
    if (nextSection) {
      navigate(`/structured-learning/${documentId}/sections/${nextSection.StructuredLearningDocumentSectionId}`);
    } else {
      navigate(`/structured-learning/${documentId}`);
    }
  }

  if (error) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16">
        <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-red-300">
          {error}
        </div>
      </div>
    );
  }

  if (!document) {
    return (
      <div className="flex min-h-[50vh] items-center justify-center">
        <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-indigo-500 border-t-transparent" />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:px-8">
      <Link
        to={`/structured-learning/${documentId}`}
        className="inline-flex items-center gap-1.5 text-sm font-medium text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white transition"
      >
        <ArrowLeft className="h-4 w-4" /> {isEn ? 'Back to course map' : 'Volver al mapa del curso'}
      </Link>

      <div className="mt-4 text-center">
        <p className="text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">{currentSection?.ChapterTitle}</p>
        <h1 className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">{currentSection?.SubchapterTitle}</h1>

        {/* Personalized Engram Impact Score & Details Button */}
        {currentEngramScore && (
          <div className="mt-3 flex flex-wrap items-center justify-center gap-2">
            <button
              type="button"
              onClick={() => setShowScoreModal(true)}
              className={`inline-flex items-center gap-1.5 rounded-full px-3.5 py-1 text-xs font-bold transition hover:scale-105 shadow-xs ${
                currentEngramScore.tier === 'Critical'
                  ? 'bg-rose-100 text-rose-800 border border-rose-300 dark:bg-rose-500/20 dark:text-rose-300 dark:border-rose-500/40'
                  : currentEngramScore.tier === 'Important'
                  ? 'bg-amber-100 text-amber-800 border border-amber-300 dark:bg-amber-500/20 dark:text-amber-300 dark:border-amber-500/40'
                  : currentEngramScore.tier === 'Refresher'
                  ? 'bg-sky-100 text-sky-800 border border-sky-300 dark:bg-sky-500/20 dark:text-sky-300 dark:border-sky-500/40'
                  : 'bg-slate-100 text-slate-700 border border-slate-300 dark:bg-slate-500/20 dark:text-slate-300 dark:border-slate-500/40'
              }`}
            >
              {currentEngramScore.tier === 'Critical' && <Flame className="h-3.5 w-3.5 text-rose-600 dark:text-rose-400 animate-pulse" />}
              {currentEngramScore.tier === 'Important' && <Zap className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />}
              {currentEngramScore.tier === 'Refresher' && <RefreshCw className="h-3.5 w-3.5 text-sky-600 dark:text-sky-400" />}
              <span>{currentEngramScore.score}/100 — {currentEngramScore.tier} {isEn ? 'Impact' : 'Impacto'}</span>
              <span className="text-[10px] opacity-75 font-normal">({currentEngramScore.primarySignal})</span>
            </button>

            <button
              type="button"
              onClick={() => setShowScoreModal(true)}
              className="inline-flex items-center gap-1.5 rounded-full border border-indigo-200 bg-indigo-50 px-3.5 py-1 text-xs font-semibold text-indigo-700 transition hover:bg-indigo-100 dark:border-indigo-400/30 dark:bg-indigo-500/10 dark:text-indigo-200 dark:hover:bg-indigo-500/20"
            >
              <Info className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
              <span>{isEn ? 'Score Details' : 'Detalles del Score'}</span>
            </button>
          </div>
        )}

        {documentId && sectionId && (
          <div className="mt-4 flex flex-wrap justify-center gap-2">
            <button
              type="button"
              onClick={() => {
                setKnowledgeGraphInitialView('graph');
                setShowKnowledgeGraph(true);
              }}
              className="inline-flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-3.5 py-2 text-xs font-semibold text-emerald-800 transition hover:bg-emerald-100 dark:border-emerald-400/30 dark:bg-emerald-500/10 dark:text-emerald-200 dark:hover:bg-emerald-500/20 shadow-xs"
            >
              <Network className="h-4 w-4 text-emerald-600 dark:text-emerald-400" /> {isEn ? 'View Knowledge Graph' : 'Ver Knowledge Graph'}
            </button>
            <button
              type="button"
              onClick={() => {
                setKnowledgeGraphInitialView('facts');
                setShowKnowledgeGraph(true);
              }}
              className="inline-flex items-center gap-2 rounded-xl border border-indigo-200 bg-indigo-50 px-3.5 py-2 text-xs font-semibold text-indigo-800 transition hover:bg-indigo-100 dark:border-indigo-400/30 dark:bg-indigo-500/10 dark:text-indigo-200 dark:hover:bg-indigo-500/20 shadow-xs"
            >
              <Sparkles className="h-4 w-4 text-indigo-600 dark:text-indigo-400" /> {isEn ? 'View Knowledge Facts' : 'Ver Knowledge Facts'}
            </button>
          </div>
        )}
      </div>

      {/* 1:1 Personalization Banner for the Learner */}
      {currentEngramScore && (
        <div className="mt-5 overflow-hidden rounded-2xl border border-indigo-200 bg-gradient-to-br from-indigo-50/80 via-white to-purple-50/50 p-4 shadow-sm dark:border-indigo-500/30 dark:bg-gradient-to-br dark:from-indigo-950/40 dark:via-slate-900/80 dark:to-purple-950/30 animate-in fade-in duration-200">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-2.5">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-indigo-100 text-indigo-700 border border-indigo-200 flex-none dark:bg-indigo-500/20 dark:text-indigo-400 dark:border-indigo-500/30">
                <Target className="h-4 w-4" />
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900 dark:text-white flex items-center gap-1.5">
                  <span>
                    {isEn
                      ? `🎯 Personalized for you${learnerProfile?.employeeName ? `, ${learnerProfile.employeeName}` : ''}`
                      : `🎯 Personalizado para ti${learnerProfile?.employeeName ? `, ${learnerProfile.employeeName}` : ''}`}
                  </span>
                </h3>
                {learnerProfile?.roleTitle && (
                  <p className="text-[11px] text-indigo-700 dark:text-indigo-300/80 font-medium">
                    {learnerProfile.roleTitle}
                    {learnerProfile.yearsOfExperience ? ` · ${learnerProfile.yearsOfExperience}+ ${isEn ? 'years exp' : 'años exp'}` : ''}
                  </p>
                )}
              </div>
            </div>

            <div className="flex items-center gap-2">
              <span
                className={`inline-flex items-center gap-1 rounded-full px-3 py-0.5 text-xs font-bold border ${
                  currentEngramScore.tier === 'Critical'
                    ? 'bg-rose-100 text-rose-800 border-rose-300 dark:bg-rose-500/20 dark:text-rose-300 dark:border-rose-500/40'
                    : currentEngramScore.tier === 'Important'
                    ? 'bg-amber-100 text-amber-800 border-amber-300 dark:bg-amber-500/20 dark:text-amber-300 dark:border-amber-500/40'
                    : 'bg-sky-100 text-sky-800 border-sky-300 dark:bg-sky-500/20 dark:text-sky-300 dark:border-sky-500/40'
                }`}
              >
                {currentEngramScore.tier === 'Critical' && <Flame className="h-3 w-3 text-rose-600 dark:text-rose-400" />}
                {currentEngramScore.tier === 'Important' && <Zap className="h-3 w-3 text-amber-600 dark:text-amber-400" />}
                {currentEngramScore.tier !== 'Critical' && currentEngramScore.tier !== 'Important' && <RefreshCw className="h-3 w-3 text-sky-600 dark:text-sky-400" />}
                <span>
                  {currentEngramScore.tier === 'Critical'
                    ? (isEn ? 'High Strategic Impact' : 'Alto Impacto')
                    : currentEngramScore.tier === 'Important'
                    ? (isEn ? 'Reinforces Your Role' : 'Refuerza tu Rol')
                    : (isEn ? 'Quick Refresher & Alignment' : 'Repaso / Refuerzo')}
                </span>
              </span>
            </div>
          </div>

          {/* Reason quote */}
          {currentEngramScore.reason && (
            <div className="mt-3 rounded-xl border border-indigo-100 bg-indigo-50/50 px-3.5 py-2.5 text-xs text-slate-700 dark:border-indigo-500/20 dark:bg-indigo-950/20 dark:text-slate-200">
              <p className="italic leading-relaxed">
                "{currentEngramScore.reason}"
              </p>
              {currentEngramScore.tier === 'Refresher' || currentEngramScore.score < 55 ? (
                <p className="mt-1 text-[11px] font-semibold text-sky-700 dark:text-sky-300/90">
                  {isEn
                    ? '💡 You already demonstrated foundation in this area — the tutor will move fast and focus on team alignment.'
                    : '💡 Ya dominas la base de este tema según tu perfil — el tutor avanzará con agilidad para alinear con los estándares del equipo.'}
                </p>
              ) : null}
            </div>
          )}

          {/* Tutor Adaptation Line */}
          {learnerProfile && (
            <div className="mt-2.5 flex items-center gap-1.5 text-[11px] font-medium text-indigo-800 dark:text-indigo-200/90 pt-2 border-t border-indigo-100/80 dark:border-white/5">
              <Sparkles className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400 flex-none" />
              <span>
                {isEn
                  ? `AI Tutor is personalized for your ${learnerProfile.roleTitle ? `${learnerProfile.roleTitle} ` : ''}role and experience level.`
                  : `El Tutor IA está personalizado para tu rol ${learnerProfile.roleTitle ? `de ${learnerProfile.roleTitle} ` : ''}y nivel de experiencia.`}
              </span>
            </div>
          )}
        </div>
      )}

      {/* Engram Impact Score Details Modal */}
      {showScoreModal && currentEngramScore && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm animate-in fade-in duration-150"
          role="dialog"
          aria-modal="true"
        >
          <div className="relative w-full max-w-xl max-h-[90vh] overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl text-left dark:border-white/15 dark:bg-slate-900">
            <button
              type="button"
              onClick={() => setShowScoreModal(false)}
              className="absolute right-4 top-4 rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white transition"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>

            <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
              <Sparkles className="h-3.5 w-3.5" />
              <span>{isEn ? 'AI Personalization Rationale & Score Composition' : 'Justificación y Composición del Score con IA'}</span>
            </div>

            <h3 className="mt-2 text-lg font-bold text-slate-900 dark:text-white">
              {currentSection?.SubchapterTitle ?? currentEngramScore.engramId}
            </h3>

            <div className="mt-3 flex flex-wrap items-center gap-2">
              <span
                className={`inline-flex items-center gap-1 rounded-full px-3 py-1 text-xs font-bold ${
                  currentEngramScore.tier === 'Critical'
                    ? 'bg-rose-100 text-rose-800 border border-rose-300 dark:bg-rose-900/40 dark:text-rose-200 dark:border-rose-500/40'
                    : currentEngramScore.tier === 'Important'
                    ? 'bg-amber-100 text-amber-800 border border-amber-300 dark:bg-amber-900/40 dark:text-amber-200 dark:border-amber-500/40'
                    : currentEngramScore.tier === 'Refresher'
                    ? 'bg-sky-100 text-sky-800 border border-sky-300 dark:bg-sky-900/40 dark:text-sky-200 dark:border-sky-500/40'
                    : 'bg-slate-100 text-slate-700 border border-slate-300 dark:bg-slate-800 dark:text-slate-300 dark:border-slate-700'
                }`}
              >
                {currentEngramScore.tier === 'Critical' && <Flame className="h-3.5 w-3.5 text-rose-600 dark:text-rose-400" />}
                {currentEngramScore.tier === 'Important' && <Zap className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />}
                {currentEngramScore.tier === 'Refresher' && <RefreshCw className="h-3.5 w-3.5 text-sky-600 dark:text-sky-400" />}
                {currentEngramScore.score}/100 — {currentEngramScore.tier} {isEn ? 'Impact' : 'Impacto'}
              </span>

              <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-50 border border-indigo-200 px-3 py-1 text-xs font-semibold text-indigo-700 dark:bg-indigo-950/40 dark:border-indigo-500/30 dark:text-indigo-300">
                <Target className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                <span>
                  {isEn ? 'Primary Driver:' : 'Impulsor Principal:'} <strong>{currentEngramScore.primarySignal}</strong>
                </span>
              </span>
            </div>

            {/* AI Personalization Reason Narrative */}
            <div className="mt-4 rounded-xl border border-indigo-100 bg-indigo-50/50 p-4 text-sm text-slate-800 dark:border-indigo-500/20 dark:bg-indigo-950/30 dark:text-slate-200 leading-relaxed">
              <div className="text-[11px] font-bold uppercase tracking-wider text-indigo-700 dark:text-indigo-300 mb-1">
                {isEn ? 'Why this score was assigned to you:' : 'Por qué se te asignó este puntaje:'}
              </div>
              <p className="italic">"{currentEngramScore.reason}"</p>
            </div>

            {/* Why this Score vs Higher Scores Comparison Box */}
            <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50 p-4 text-xs space-y-2.5 dark:border-slate-700/60 dark:bg-slate-950/40">
              <div className="font-bold text-slate-900 dark:text-white flex items-center gap-1.5">
                <Target className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                <span>{isEn ? `Why ${currentEngramScore.score}/100 vs higher scores?` : `¿Por qué ${currentEngramScore.score}/100 vs puntajes más altos?`}</span>
              </div>
              
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-[11px] text-slate-700 dark:text-slate-300">
                <div className="rounded-lg bg-sky-50 border border-sky-200 p-2.5 space-y-1 dark:bg-sky-950/30 dark:border-sky-500/20">
                  <div className="font-bold text-sky-800 dark:text-sky-300 flex items-center gap-1">
                    <RefreshCw className="h-3 w-3" />
                    <span>{isEn ? `This Engram (${currentEngramScore.score}/100):` : `Este Engrama (${currentEngramScore.score}/100):`}</span>
                  </div>
                  <p className="text-slate-600 dark:text-slate-300 leading-snug">
                    {isEn
                      ? 'Required for your role, but your resume already demonstrates foundational knowledge in this area. It serves as a quick alignment refresher.'
                      : 'Exigido para tu rol, pero tu CV ya demuestra conocimientos en esta área. Sirve como repaso de alineación.'}
                  </p>
                </div>

                <div className="rounded-lg bg-rose-50 border border-rose-200 p-2.5 space-y-1 dark:bg-rose-950/30 dark:border-rose-500/20">
                  <div className="font-bold text-rose-800 dark:text-rose-300 flex items-center gap-1">
                    <Flame className="h-3 w-3" />
                    <span>{isEn ? 'Higher Scores (80–100 Critical):' : 'Puntajes Más Altos (80–100 Crítico):'}</span>
                  </div>
                  <p className="text-slate-600 dark:text-slate-300 leading-snug">
                    {isEn
                      ? 'Target unverified skill gaps directly tied to your active Q4 SMART objectives and promotion milestones that you have not yet proven.'
                      : 'Apuntan a brechas no verificadas directamente ligadas a tus objetivos SMART Q4 y metas de ascenso aún no demostradas.'}
                  </p>
                </div>
              </div>
            </div>

            {/* Score Composition & Signal Breakdown */}
            <div className="mt-4 rounded-xl border border-slate-200 bg-slate-50/50 p-4 dark:border-white/10 dark:bg-white/[0.02]">
              <div className="text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-2.5">
                {isEn ? 'Scoring Signals & Weighting Framework' : 'Ponderación y Señales del Score'}
              </div>

              <div className="space-y-2">
                {/* Signal 1: Job Description & Core Responsibilities */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    currentEngramScore.primarySignal === 'Role'
                      ? 'bg-indigo-50 font-bold text-indigo-900 border border-indigo-200 dark:bg-indigo-900/40 dark:text-indigo-200 dark:border-indigo-500/40'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-sky-500" />
                    <span>{isEn ? '1. Job Description / Role' : '1. Descripción de Puesto / Rol'}</span>
                    <span className="text-[10px] text-slate-500">({isEn ? 'what role demands' : 'lo que el puesto exige'})</span>
                    {currentEngramScore.primarySignal === 'Role' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">40%</span>
                </div>

                {/* Signal 2: KPIs & SMART Objectives */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    currentEngramScore.primarySignal === 'KPI'
                      ? 'bg-indigo-50 font-bold text-indigo-900 border border-indigo-200 dark:bg-indigo-900/40 dark:text-indigo-200 dark:border-indigo-500/40'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-indigo-500" />
                    <span>{isEn ? '2. KPIs & Active Objectives' : '2. KPIs y Objetivos Activos'}</span>
                    <span className="text-[10px] text-slate-500">({isEn ? 'what is measured today' : 'lo que miden hoy'})</span>
                    {currentEngramScore.primarySignal === 'KPI' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">25%</span>
                </div>

                {/* Signal 3: Manager Coaching & Flagged Gaps */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    currentEngramScore.primarySignal === 'Coaching'
                      ? 'bg-indigo-50 font-bold text-indigo-900 border border-indigo-200 dark:bg-indigo-900/40 dark:text-indigo-200 dark:border-indigo-500/40'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-purple-500" />
                    <span>{isEn ? '3. Manager Coaching & Gaps' : '3. Coaching y Brechas'}</span>
                    <span className="text-[10px] text-slate-500">({isEn ? 'manager flagged areas' : 'gaps marcados por manager'})</span>
                    {currentEngramScore.primarySignal === 'Coaching' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">15%</span>
                </div>

                {/* Signal 4: Career Growth & Promotion Roadmap */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    currentEngramScore.primarySignal === 'CareerGrowth'
                      ? 'bg-indigo-50 font-bold text-indigo-900 border border-indigo-200 dark:bg-indigo-900/40 dark:text-indigo-200 dark:border-indigo-500/40'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-amber-500" />
                    <span>{isEn ? '4. Career Growth & Promotion' : '4. Career Growth y Ascenso'}</span>
                    <span className="text-[10px] text-slate-500">({isEn ? 'future advancement' : 'crecimiento futuro'})</span>
                    {currentEngramScore.primarySignal === 'CareerGrowth' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">12%</span>
                </div>

                {/* Signal 5: Assigned Skills & Tools */}
                <div
                  className={`flex items-center justify-between p-2 rounded-lg text-xs transition ${
                    currentEngramScore.primarySignal === 'Skill'
                      ? 'bg-indigo-50 font-bold text-indigo-900 border border-indigo-200 dark:bg-indigo-900/40 dark:text-indigo-200 dark:border-indigo-500/40'
                      : 'text-slate-600 dark:text-slate-400'
                  }`}
                >
                  <div className="flex items-center gap-2">
                    <span className="h-2 w-2 rounded-full bg-emerald-500" />
                    <span>{isEn ? '5. Assigned Skills & Tools' : '5. Habilidades Asignadas y Herramientas'}</span>
                    <span className="text-[10px] text-slate-500">({isEn ? 'supporting signal' : 'señal de apoyo'})</span>
                    {currentEngramScore.primarySignal === 'Skill' && (
                      <span className="rounded bg-indigo-600 px-1.5 py-0.2 text-[9px] font-bold text-white uppercase">
                        {isEn ? 'Primary Driver' : 'Impulsor Principal'}
                      </span>
                    )}
                  </div>
                  <span className="font-mono font-bold">8%</span>
                </div>
              </div>
            </div>

            {/* Core Purpose Rule */}
            <div className="mt-4 rounded-xl border border-indigo-100 bg-indigo-50/60 p-3.5 text-xs text-indigo-900 dark:border-indigo-500/20 dark:bg-indigo-950/20 dark:text-indigo-300">
              <span className="font-bold text-indigo-950 dark:text-indigo-200">
                {isEn ? '📌 Progression Note: ' : '📌 Nota de Progreso: '}
              </span>
              {isEn
                ? 'All engrams in this course must be completed in order. This score highlights where your mastery produces the highest strategic value for your career.'
                : 'Todos los engramas de este curso deben completarse en orden. Este puntaje resalta dónde tu maestría genera el mayor valor estratégico para tu carrera.'}
            </div>

            <div className="mt-6 flex justify-end">
              <button
                type="button"
                onClick={() => setShowScoreModal(false)}
                className="rounded-xl bg-indigo-600 px-5 py-2 text-xs font-semibold text-white hover:bg-indigo-500 transition shadow-sm"
              >
                {isEn ? 'Close' : 'Cerrar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {showKnowledgeGraph && documentId && sectionId && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-label="Knowledge Graph"
        >
          <div className="max-h-[90vh] w-full max-w-6xl overflow-y-auto rounded-2xl border border-emerald-400/25 bg-slate-950 p-4 shadow-2xl sm:p-6">
            <div className="mb-4 flex items-center justify-between gap-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-emerald-400">Engram</p>
                <h2 className="mt-1 text-lg font-semibold text-white">
                  {knowledgeGraphInitialView === 'facts' ? 'Knowledge Facts' : 'Knowledge Graph'}
                </h2>
              </div>
              <button
                type="button"
                onClick={() => setShowKnowledgeGraph(false)}
                className="inline-flex items-center gap-1.5 rounded-xl border border-red-400/40 bg-red-500/10 px-3.5 py-1.5 text-xs font-semibold text-red-200 transition hover:border-red-400 hover:bg-red-500/20 hover:text-white shadow-sm"
                aria-label={isEn ? 'Close Knowledge Graph' : 'Cerrar Knowledge Graph'}
              >
                <X className="h-4 w-4 text-red-400" />
                <span>{isEn ? 'Close' : 'Cerrar'}</span>
              </button>
            </div>
            <KnowledgeGraphSection
              documentId={documentId}
              sectionId={sectionId}
              personId={user?.personId}
              initialView={knowledgeGraphInitialView}
            />
          </div>
        </div>
      )}

      {steps.length > 0 && (
        <StepProgress
          steps={steps}
          currentIndex={stepIndex}
          maxReachedIndex={maxReachedIndex}
          freeNavigation={false}
          onSelectStep={goToStep}
          isEn={isEn}
        />
      )}

      <div className="mt-8">
        {currentStep && currentSection ? (
          <StepContent
            key={currentStep.StructuredLearningSectionStepId}
            step={currentStep}
            sectionId={currentSection.StructuredLearningDocumentSectionId}
            personId={personId}
            engramTitle={currentSection.SubchapterTitle || currentSection.ChapterTitle}
            tier={currentEngramScore?.tier}
            questionType={document.AssessmentQuestionType}
            onComplete={handleStepComplete}
            isEn={isEn}
          />
        ) : (
          <p className="text-sm text-slate-500">{isEn ? 'No steps yet.' : 'No hay pasos aún.'}</p>
        )}
      </div>

      <div className="mt-8 flex items-center justify-between gap-3">
        <button
          type="button"
          disabled={isFirstStep && !previousSection}
          onClick={goPrevious}
          className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40 dark:border-white/10 dark:bg-white/[0.03] dark:text-slate-200 dark:hover:bg-white/[0.06]"
        >
          <ArrowLeft className="h-4 w-4" /> {isEn ? 'Previous' : 'Anterior'}
        </button>
        <button
          type="button"
          onClick={goNext}
          disabled={!canAdvancePastCurrentStep}
          className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 px-5 py-2.5 text-sm font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40"
        >
          {isLastStep && !nextSection
            ? (isEn ? 'Finish Lesson' : 'Finalizar Lección')
            : (isEn ? 'Next' : 'Siguiente')}{' '}
          <ArrowLeft className="h-4 w-4 rotate-180" />
        </button>
      </div>
    </div>
  );
}

function StepProgress({
  steps,
  currentIndex,
  maxReachedIndex,
  freeNavigation,
  onSelectStep,
  isEn,
}: {
  steps: StructuredLearningStepDetail[];
  currentIndex: number;
  maxReachedIndex: number;
  freeNavigation: boolean;
  onSelectStep: (index: number) => void;
  isEn: boolean;
}) {
  const orderedSteps = [...steps].sort((a, b) => STEP_ORDER.indexOf(a.StepType) - STEP_ORDER.indexOf(b.StepType));

  return (
    <div className="mt-6 flex items-center">
      {orderedSteps.map((step, index) => {
        const meta = STEP_META[step.StepType] ?? { labelEn: step.StepType, labelEs: step.StepType, Icon: BookOpenText };
        const Icon = meta.Icon;
        const isDone = index < currentIndex;
        const isActive = index === currentIndex;
        const isReachable = freeNavigation || index <= maxReachedIndex;
        return (
          <div key={step.StructuredLearningSectionStepId} className="flex flex-1 items-center">
            <button
              type="button"
              disabled={!isReachable}
              onClick={() => onSelectStep(index)}
              className={`flex flex-col items-center gap-1 ${isReachable ? 'cursor-pointer' : 'cursor-not-allowed opacity-40'}`}
            >
              <div
                className={`flex h-9 w-9 items-center justify-center rounded-full border-2 ${
                  isDone
                    ? 'border-transparent bg-gradient-to-br from-indigo-600 to-purple-600 text-white'
                    : isActive
                      ? 'border-indigo-500 bg-indigo-50 text-indigo-700 dark:border-indigo-400 dark:bg-indigo-500/10 dark:text-indigo-300'
                      : 'border-slate-200 bg-white text-slate-400 dark:border-white/10 dark:bg-white/[0.03] dark:text-slate-500'
                }`}
              >
                {isDone ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
              </div>
              <span className={`text-[11px] font-semibold ${isActive ? 'text-slate-900 dark:text-white font-bold' : isDone ? 'text-indigo-700 dark:text-indigo-300' : 'text-slate-400 dark:text-slate-500'}`}>
                {isEn ? meta.labelEn : meta.labelEs}
              </span>
            </button>
            {index < orderedSteps.length - 1 && (
              <div className={`mx-1 h-0.5 flex-1 ${index < currentIndex ? 'bg-indigo-600 dark:bg-indigo-500' : 'bg-slate-200 dark:bg-white/10'}`} />
            )}
          </div>
        );
      })}
    </div>
  );
}

function StepContent({
  step,
  sectionId,
  personId,
  engramTitle,
  tier,
  questionType,
  onComplete,
  isEn,
}: {
  step: StructuredLearningStepDetail;
  sectionId: string;
  personId: string;
  engramTitle?: string;
  tier?: string;
  questionType: string;
  onComplete: () => void;
  isEn: boolean;
}) {
  const [teachingWizardItemType, setTeachingWizardItemType] = useState<'Text' | 'Video'>('Text');
  const meta = STEP_META[step.StepType] ?? { labelEn: step.StepType, labelEs: step.StepType, Icon: BookOpenText };
  const teachingShowsVideo = step.StepType === 'Teaching' && teachingWizardItemType === 'Video';

  return (
    <div className="space-y-4">
      {step.StepType === 'Teaching' && !teachingShowsVideo && (
        <VoiceTutorAgent
          structuredLearningSectionStepId={step.StructuredLearningSectionStepId}
          startLabel={isEn ? 'Talk to Tutor' : 'Hablar con el Tutor'}
          autoConnect={true}
        />
      )}

      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-xl dark:border-white/10 dark:bg-slate-900/80">
        {(step.StepType === 'Hypothesis' || (step.StepType === 'Teaching' && !teachingShowsVideo)) && (
          <StepIllustration stepDataJson={step.StepDataJson} />
        )}
        <div className="px-5 py-6 sm:px-8 sm:py-8">
          <p className="text-xs font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
            {isEn ? meta.labelEn : meta.labelEs}
          </p>

          {step.StepType === 'Hypothesis' && step.Content && (
            <RichStepContent content={step.Content.replace(/^ContractVersion:\s*[^\n]+\s*/i, '').trim() || step.Content} />
          )}

          {step.StepType === 'Hypothesis' && (
            <HypothesisQuestionCard sectionId={sectionId} personId={personId} step={step} isEn={isEn} />
          )}

          {step.StepType === 'Teaching' ? (
            <TeachingTimelineWizard sectionId={sectionId} step={step} onItemTypeChange={setTeachingWizardItemType} isEn={isEn} />
          ) : (
            step.StepType !== 'Hypothesis' && !(step.StepType === 'Production') && step.Content && <RichStepContent content={step.Content} />
          )}

          {step.StepType === 'DeepDive' && step.StepDataJson && (
            <DeepDiveQuiz
              sectionId={sectionId}
              stepId={step.StructuredLearningSectionStepId}
              stepDataJson={step.StepDataJson}
              onComplete={onComplete}
              isEn={isEn}
            />
          )}

          {step.StepType === 'Recall' && (
            <RecallPractice sectionId={sectionId} personId={personId} onComplete={onComplete} isEn={isEn} />
          )}

          {step.StepType === 'Assessment' && (
            <AssessmentPractice
              sectionId={sectionId}
              personId={personId}
              engramTitle={engramTitle}
              tier={tier}
              questionType={questionType}
              onComplete={onComplete}
              isEn={isEn}
            />
          )}
        </div>
      </div>
    </div>
  );
}

function RichStepContent({ content }: { content: string }) {
  const blocks = normalizeRichBlocks(content);

  return (
    <div className="mt-6 max-w-4xl space-y-6 text-[16px] leading-8 text-slate-800 dark:text-slate-200 sm:text-[17px]">
      {blocks.map((block, index) => {
        const lines = block.split('\n').map((line) => line.trim()).filter(Boolean);
        const isList = lines.length > 0 && lines.every((line) => /^([-*]|\d+[.)])\s+/.test(line));
        const heading = lines.length === 1 && (/^#{1,3}\s+/.test(lines[0]) || (lines[0].length < 90 && /:$/.test(lines[0])));

        if (heading) {
          const text = lines[0].replace(/^#{1,3}\s+/, '');
          return <h3 key={index} className="pt-2 text-lg font-bold leading-7 text-slate-900 dark:text-white">{text}</h3>;
        }

        if (isList) {
          const ordered = /^\d+[.)]/.test(lines[0]);
          const List = ordered ? 'ol' : 'ul';
          return (
            <List key={index} className={`space-y-2 pl-6 ${ordered ? 'list-decimal' : 'list-disc'}`}>
              {lines.map((line, lineIndex) => (
                <li key={lineIndex} className="pl-1 text-slate-700 dark:text-slate-300">
                  {formatInlineText(line.replace(/^([-*]|\d+[.)])\s+/, ''))}
                </li>
              ))}
            </List>
          );
        }

        return (
          <p key={index} className="text-slate-700 dark:text-slate-300">
            {lines.map((line, lineIndex) => (
              <span key={lineIndex}>
                {lineIndex > 0 && <br />}
                {formatInlineText(line)}
              </span>
            ))}
          </p>
        );
      })}
    </div>
  );
}

function splitLongBlock(block: string): string[] {
  if (!block || block.length < 420) return [block];
  const sentences = block.match(/[^.!?]+[.!?]+(?=\s|$)/g)?.map((sentence) => sentence.trim()).filter(Boolean);
  if (!sentences || sentences.length < 3) return [block];
  const groups: string[] = [];
  for (let index = 0; index < sentences.length; index += 2) {
    groups.push(sentences.slice(index, index + 2).join(' '));
  }
  return groups;
}

function cleanEngramContent(raw: string): string {
  if (!raw) return '';
  return raw
    .replace(/^ContractVersion:\s*[^\n]+\s*/i, '')
    .replace(/\[(?:Descripci[óo]n\s+de\s+imagen|Image\s+description)[^\]]*\]/gi, '')
    .replace(/(?:^|\n)#{0,6}\s*(?:\d+\.\s*)?D?ETAILED\s+DESCRIPTION:?[\s\S]*?(?=(?:\n#{1,6}\s+[^\n]+|\n\[(?:Descripci|Image)|$))/gi, '')
    .replace(/(?:^|\n)#{0,6}\s*(?:\d+\.\s*)?VERBATIM\s+TRANSCRIPTION:?\s*/gi, '\n')
    .replace(/#{0,6}\s*(?:\d+\.\s*)?D?ETAILED\s+DESCRIPTION:?\s*/gi, '')
    .trim();
}

function normalizeRichBlocks(content: string): string[] {
  const cleaned = cleanEngramContent(content);
  const normalized = cleaned
    .replace(/\r\n/g, '\n')
    .replace(/\s+•\s+/g, '\n• ')
    .replace(/\s+(Why this matters)\s+/gi, '\n\n$1\n')
    .replace(/\s+(Next, connect)\s+/gi, '\n\n$1 ')
    .replace(/\s+(Foundations of AI:|What is generative AI\?|AI value also comes from other capabilities:|Responsible AI means)/g, '\n\n$1');

  return normalized
    .split(/\n\s*\n|\n/)
    .map((block) => block.trim())
    .filter(Boolean)
    .flatMap((block) => splitLongBlock(block));
}

function formatInlineText(value: string): ReactNode {
  const parts = value.split(/(\*\*[^*]+\*\*|__[^_]+__)/g);
  return parts.map((part, index) => {
    const bold = /^\*\*[^*]+\*\*$/.test(part) || /^__[^_]+__$/.test(part);
    return bold ? (
      <strong key={index} className="font-bold text-slate-900 dark:text-white">
        {part.slice(2, -2)}
      </strong>
    ) : (
      <span key={index}>{part}</span>
    );
  });
}

type TeachingTimelineItem =
  | { Type: 'Text' }
  | { Type: 'Video'; IllustrationId: string };

function parseTeachingTimeline(timelineJson: string | null | undefined): TeachingTimelineItem[] | null {
  if (!timelineJson) return null;
  try {
    const parsed = JSON.parse(timelineJson) as unknown;
    if (!Array.isArray(parsed)) return null;
    return parsed.filter(
      (item): item is TeachingTimelineItem =>
        !!item && typeof item === 'object' && ((item as { Type?: string }).Type === 'Text' || (item as { Type?: string }).Type === 'Video')
    );
  } catch {
    return null;
  }
}

function TeachingTimelineWizard({
  sectionId,
  step,
  onItemTypeChange,
  isEn,
}: {
  sectionId: string;
  step: StructuredLearningStepDetail;
  onItemTypeChange: (type: 'Text' | 'Video') => void;
  isEn: boolean;
}) {
  const [videos, setVideos] = useState<StructuredLearningSectionVideo[]>([]);
  const [loadingVideos, setLoadingVideos] = useState(true);
  const [index, setIndex] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listStructuredLearningSectionVideos(sectionId)
      .then((list) => {
        if (cancelled) return;
        setVideos(list.filter((v) => v.IsPublished));
      })
      .catch(() => {
        if (!cancelled) setVideos([]);
      })
      .finally(() => {
        if (!cancelled) setLoadingVideos(false);
      });
    return () => {
      cancelled = true;
    };
  }, [sectionId]);

  useEffect(() => {
    setIndex(0);
  }, [step.StructuredLearningSectionStepId]);

  const saved = parseTeachingTimeline(step.TimelineJson);
  const validSaved = saved?.filter((item) => item.Type === 'Text' || videos.some((v) => v.IllustrationId === item.IllustrationId)) ?? null;
  const defaultItems: TeachingTimelineItem[] = [
    { Type: 'Text' },
    ...videos.map((v): TeachingTimelineItem => ({ Type: 'Video', IllustrationId: v.IllustrationId })),
  ];
  const missingFromSaved = validSaved
    ? defaultItems.filter((item) => !validSaved.some((s) => (item.Type === 'Text' ? s.Type === 'Text' : s.Type === 'Video' && s.IllustrationId === item.IllustrationId)))
    : [];
  const items = validSaved && validSaved.length > 0 ? [...validSaved, ...missingFromSaved] : defaultItems;
  const current = items[Math.min(index, items.length - 1)] as TeachingTimelineItem | undefined;

  useEffect(() => {
    if (current) onItemTypeChange(current.Type);
  }, [current, onItemTypeChange]);

  if (loadingVideos) return null;
  if (!current) return null;

  const isFirst = index === 0;
  const isLast = index >= items.length - 1;

  return (
    <div className="mt-6 space-y-4">
      {items.length > 1 && (
        <div className="flex flex-wrap gap-2">
          {items.map((item, i) => (
            <button
              key={item.Type === 'Text' ? 'text' : `video-${item.IllustrationId}`}
              type="button"
              onClick={() => setIndex(i)}
              className={`flex h-10 min-w-[2.5rem] items-center justify-center rounded-full border px-2.5 text-sm font-bold transition ${
                i === index
                  ? 'border-indigo-500 bg-indigo-50 text-indigo-700 dark:border-indigo-400 dark:bg-indigo-500/20 dark:text-indigo-200 shadow-xs'
                  : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50 dark:border-white/10 dark:bg-white/[0.02] dark:text-slate-400 dark:hover:border-white/20'
              }`}
            >
              {item.Type === 'Video' ? <Video className="h-5 w-5" /> : i + 1}
            </button>
          ))}
        </div>
      )}

      {current.Type === 'Video' ? (
        <div className="space-y-2">
          <p className="flex items-center gap-1.5 text-sm font-semibold text-indigo-700 dark:text-indigo-300">
            <Video className="h-4 w-4" /> {isEn ? 'Watch the video lesson below' : 'Mira la lección en video a continuación'}
          </p>
          <video
            src={apiImageUrl(`/illustrations/structured-learning/${current.IllustrationId}/image`)}
            controls
            className="w-full rounded-2xl bg-black shadow-md"
          />
        </div>
      ) : (
        <RichStepContent content={step.Content} />
      )}

      {items.length > 1 && (
        <div className="flex items-center justify-between gap-3 pt-2">
          <button
            type="button"
            disabled={isFirst}
            onClick={() => setIndex((i) => Math.max(0, i - 1))}
            className="inline-flex items-center gap-1.5 rounded-xl border border-slate-200 bg-white px-3.5 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40 dark:border-white/10 dark:bg-white/[0.03] dark:text-slate-200 dark:hover:bg-white/[0.06]"
          >
            <ArrowLeft className="h-3.5 w-3.5" /> {isEn ? 'Previous' : 'Anterior'}
          </button>
          <span className="text-xs font-medium text-slate-500 dark:text-slate-400">
            {index + 1} / {items.length}
          </span>
          <button
            type="button"
            disabled={isLast}
            onClick={() => setIndex((i) => Math.min(items.length - 1, i + 1))}
            className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 px-3.5 py-1.5 text-xs font-semibold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {isEn ? 'Next' : 'Siguiente'} <ArrowLeft className="h-3.5 w-3.5 rotate-180" />
          </button>
        </div>
      )}
    </div>
  );
}

interface DeepDiveCard {
  Title: string;
  Text: string;
  Emoji?: string | null;
  IllustrationId?: string | null;
  IllustrationIds?: string[] | null;
}

function StepIllustration({ stepDataJson }: { stepDataJson: string | null }) {
  if (!stepDataJson) return null;
  let illustrationId: string | null = null;
  try {
    illustrationId = (JSON.parse(stepDataJson) as { IllustrationId?: string | null }).IllustrationId ?? null;
  } catch {
    return null;
  }
  if (!illustrationId) return null;

  return <ZoomableIllustration illustrationId={illustrationId} alt="" className="h-72 w-full rounded-xl bg-slate-950/50 object-contain object-center" />;
}

async function downloadImageFile(url: string, filename = 'lesson-illustration.png') {
  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error('Fetch failed');
    const blob = await res.blob();
    const blobUrl = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(blobUrl);
  } catch {
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.target = '_blank';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
}

function ZoomableIllustration({ illustrationId, alt, className }: { illustrationId: string; alt: string; className: string }) {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open]);

  const src = apiImageUrl(`/illustrations/structured-learning/${illustrationId}/image`);
  const filename = `${(alt ? alt.replace(/[^a-zA-Z0-9_-]/g, '_').slice(0, 50) : 'illustration') || 'illustration'}.png`;

  return (
    <div className="group relative block w-full">
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="block w-full cursor-zoom-in"
        aria-label="Enlarge image"
      >
        <img src={src} alt={alt} className={className} />
        <span className="pointer-events-none absolute bottom-2 right-2 rounded-lg bg-black/65 px-2 py-1 text-[10px] font-semibold text-white opacity-0 transition group-hover:opacity-100">
          Enlarge
        </span>
      </button>
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          e.preventDefault();
          void downloadImageFile(src, filename);
        }}
        title="Download / Save image to local file"
        className="absolute top-2 right-2 z-10 flex items-center gap-1.5 rounded-lg border border-white/20 bg-slate-900/85 px-2.5 py-1 text-[11px] font-medium text-slate-200 shadow-md backdrop-blur-xs opacity-0 transition hover:bg-slate-800 hover:text-white group-hover:opacity-100 cursor-pointer"
        aria-label="Save image to local file"
      >
        <Download className="h-3.5 w-3.5" />
        <span>Save image</span>
      </button>
      {open && (
        <div
          className="fixed inset-0 z-[70] flex items-center justify-center bg-black/85 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          onClick={() => setOpen(false)}
        >
          <div className="relative flex max-h-[95vh] max-w-[95vw] items-center justify-center" onClick={(event) => event.stopPropagation()}>
            <img src={src} alt={alt} className="max-h-[90vh] max-w-[92vw] rounded-xl object-contain shadow-2xl" />
            <button
              type="button"
              onClick={(event) => {
                event.stopPropagation();
                void downloadImageFile(src, filename);
              }}
              className="absolute -top-2 right-12 flex items-center gap-1.5 rounded-full border border-white/20 bg-slate-900 px-3.5 py-1.5 text-xs font-medium text-white shadow-lg hover:bg-slate-800 cursor-pointer"
              aria-label="Download image"
            >
              <Download className="h-3.5 w-3.5" />
              <span>Download</span>
            </button>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="absolute -right-2 -top-2 rounded-full border border-white/20 bg-slate-900 p-2 text-white shadow-lg hover:bg-slate-800 cursor-pointer"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function DeepDiveQuiz({
  stepId,
  stepDataJson,
  onComplete,
  isEn,
}: {
  sectionId?: string;
  stepId: string;
  stepDataJson: string;
  onComplete: () => void;
  isEn: boolean;
}) {
  let cards: DeepDiveCard[] = [];
  try {
    cards = JSON.parse(stepDataJson) as DeepDiveCard[];
  } catch {
    cards = [];
  }

  const completedRef = useRef(false);
  const [cardIndex, setCardIndex] = useState(0);

  const markCompleteOnce = () => {
    if (!completedRef.current) {
      completedRef.current = true;
      onComplete();
    }
  };

  if (cards.length === 0) return null;

  const freeCard = cards[Math.min(cardIndex, cards.length - 1)];
  return (
    <div className="mt-3 space-y-3">
      {cards.length > 1 && (
        <div className="flex flex-wrap gap-1.5">
          {cards.map((c, index) => (
            <button
              key={index}
              type="button"
              onClick={() => setCardIndex(index)}
              title={c.Title}
              className={`flex h-7 min-w-[1.75rem] items-center justify-center rounded-full border px-2 text-[11px] font-bold transition ${
                index === cardIndex
                  ? 'border-indigo-500 bg-indigo-50 text-indigo-700 dark:border-indigo-400 dark:bg-indigo-500/20 dark:text-indigo-200 shadow-xs'
                  : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:bg-white/[0.02] dark:text-slate-400 dark:hover:bg-white/[0.06]'
              }`}
            >
              {index + 1}
            </button>
          ))}
        </div>
      )}
      <div className="rounded-2xl border border-slate-200 bg-slate-50/60 p-5 dark:border-white/10 dark:bg-white/[0.03]">
        {freeCard.IllustrationIds && freeCard.IllustrationIds.length > 0 ? (
          <div className="mb-3 grid grid-cols-1 gap-2 sm:grid-cols-2">
            {freeCard.IllustrationIds.map((id, i) => (
              <div key={id} className="space-y-1">
                <ZoomableIllustration
                  illustrationId={id}
                  alt={`${freeCard.Title} — step ${i + 1}`}
                  className="h-auto max-h-48 w-full rounded-xl object-contain bg-slate-100 dark:bg-black/20"
                />
                <p className="text-center text-[11px] font-medium text-slate-500 dark:text-slate-400">Step {i + 1}</p>
              </div>
            ))}
          </div>
        ) : freeCard.IllustrationId ? (
          <ZoomableIllustration
            illustrationId={freeCard.IllustrationId}
            alt={freeCard.Title}
            className="mb-3 h-auto max-h-72 w-full rounded-xl object-contain bg-slate-100 dark:bg-black/20"
          />
        ) : (
          freeCard.Emoji && <div className="mb-3 text-3xl leading-none">{freeCard.Emoji}</div>
        )}
        <p className="text-base font-bold text-slate-900 dark:text-white">{freeCard.Title}</p>
        <p className="mt-1.5 whitespace-pre-wrap text-sm leading-relaxed text-slate-700 dark:text-slate-300">{freeCard.Text}</p>

        <VoiceTutorAgent
          structuredLearningSectionStepId={stepId}
          exampleCardIndex={cardIndex}
          reviewOnly
          autoConnect={false}
          compact
        />
      </div>
      <div className="flex items-center justify-between gap-3 pt-2">
        <button
          type="button"
          disabled={cardIndex === 0}
          onClick={() => setCardIndex((i) => Math.max(0, i - 1))}
          className="rounded-xl border border-slate-200 bg-white px-3.5 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-30 dark:border-white/10 dark:bg-white/[0.03] dark:text-slate-300 dark:hover:bg-white/[0.06]"
        >
          {isEn ? 'Previous' : 'Anterior'}
        </button>
        {cardIndex < cards.length - 1 ? (
          <button
            type="button"
            onClick={() => setCardIndex((i) => Math.min(cards.length - 1, i + 1))}
            className="rounded-xl border border-slate-200 bg-white px-3.5 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/[0.03] dark:text-slate-300 dark:hover:bg-white/[0.06]"
          >
            {isEn ? 'Next' : 'Siguiente'}
          </button>
        ) : (
          <button
            type="button"
            onClick={() => markCompleteOnce()}
            className="rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 px-4 py-1.5 text-xs font-bold text-white shadow-md shadow-indigo-500/20 transition hover:brightness-110"
          >
            {isEn ? 'Continue' : 'Continuar'}
          </button>
        )}
      </div>
    </div>
  );
}

function RecallPractice({
  sectionId,
  personId,
  onComplete,
  isEn,
}: {
  sectionId: string;
  personId: string;
  onComplete: () => void;
  isEn: boolean;
}) {
  const startedRef = useRef(false);
  const completedRef = useRef(false);
  const [question, setQuestion] = useState<string | null>(null);
  const [response, setResponse] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [attemptsUsed, setAttemptsUsed] = useState(0);
  const [itemsMastered, setItemsMastered] = useState(0);
  const [itemsRequired, setItemsRequired] = useState(3);
  const [complete, setComplete] = useState(false);
  const [lastMastered, setLastMastered] = useState<boolean | null>(null);
  const [error, setError] = useState<string | null>(null);

  const markCompleteOnce = () => {
    setComplete(true);
    if (!completedRef.current) {
      completedRef.current = true;
      onComplete();
    }
  };

  useEffect(() => {
    if (startedRef.current || !personId) return;
    startedRef.current = true;
    getStructuredLearningRecallState(sectionId, personId)
      .then((result) => {
        setQuestion(result.NextQuestion);
        setItemsRequired(result.ItemsRequired);
        setItemsMastered(result.ItemsMastered);
        setAttemptsUsed(result.AttemptsUsedForItem);
        if (result.Complete) markCompleteOnce();
      })
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [sectionId, personId]);

  async function handleSubmit() {
    if (!response.trim()) return;
    setSubmitting(true);
    setError(null);
    try {
      const result = await submitStructuredLearningRecallAttempt(sectionId, personId, response);
      setQuestion(result.NextQuestion);
      setItemsMastered(result.ItemsMastered);
      setItemsRequired(result.ItemsRequired);
      setAttemptsUsed(result.AttemptsUsedForItem);
      setLastMastered(result.MasteredThisAttempt);
      setResponse('');
      if (result.Complete) {
        markCompleteOnce();
      } else {
        setComplete(false);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (error) return <p className="mt-3 text-sm text-red-300">{error}</p>;

  if (complete) {
    return (
      <div className="mt-3 flex items-center gap-2 rounded-xl border border-emerald-400/30 bg-emerald-500/10 p-3 text-sm text-emerald-200">
        <CheckCircle2 className="h-4 w-4" /> {isEn ? 'Recall completed! Great work.' : '¡Evocación completada! Excelente trabajo.'}
      </div>
    );
  }

  if (!question) {
    return (
      <div className="flex items-center gap-2 p-4 text-xs text-slate-400">
        <Loader2 className="h-4 w-4 animate-spin" /> {isEn ? 'Preparing active recall question...' : 'Preparando pregunta de evocación...'}
      </div>
    );
  }

  return (
    <div className="mt-3 space-y-3">
      <p className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
        {isEn ? `Attempt ${attemptsUsed + 1} of ${RECALL_MAX_ATTEMPTS}` : `Intento ${attemptsUsed + 1} de ${RECALL_MAX_ATTEMPTS}`}
      </p>
      <div className="flex items-center gap-2">
        <div className="h-2 flex-1 overflow-hidden rounded-full bg-slate-200 dark:bg-white/10">
          <div
            className="h-full rounded-full bg-fuchsia-500 transition-all"
            style={{ width: `${Math.min(100, (itemsMastered / itemsRequired) * 100)}%` }}
          />
        </div>
        <span className="text-xs font-semibold text-slate-600 dark:text-slate-400">
          {itemsMastered} / {itemsRequired} {isEn ? 'mastered' : 'dominados'}
        </span>
      </div>
      {lastMastered === true && (
        <p className="text-xs font-bold text-emerald-700 dark:text-emerald-300">{isEn ? '✓ Great explanation!' : '✓ ¡Excelente explicación!'}</p>
      )}
      <p className="text-sm font-medium leading-relaxed text-slate-800 dark:text-slate-200">{question}</p>
      <textarea
        value={response}
        onChange={(e) => setResponse(e.target.value)}
        rows={3}
        placeholder={isEn ? 'Explain in your own words...' : 'Explica con tus propias palabras...'}
        className="w-full rounded-2xl border border-slate-200 bg-white p-3.5 text-sm text-slate-900 placeholder:text-slate-400 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 focus:outline-none dark:border-white/10 dark:bg-slate-950/40 dark:text-white dark:placeholder:text-slate-500"
      />
      <button
        type="button"
        onClick={handleSubmit}
        disabled={submitting || !response.trim()}
        className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-fuchsia-600 to-pink-600 px-5 py-2.5 text-sm font-bold text-white shadow-md shadow-fuchsia-500/20 transition hover:brightness-110 disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
        {isEn ? 'Submit Explanation' : 'Enviar Explicación'}
      </button>
    </div>
  );
}

function AssessmentPractice({
  sectionId,
  personId,
  engramTitle,
  tier,
  questionType,
  onComplete,
  isEn,
}: {
  sectionId: string;
  personId: string;
  engramTitle?: string;
  tier?: string;
  questionType: string;
  onComplete: () => void;
  isEn: boolean;
}) {
  const completedRef = useRef(false);
  const [round, setRound] = useState<StructuredLearningAssessmentRoundState | null | undefined>(undefined);
  const [selectedOption, setSelectedOption] = useState<number | null>(null);
  const [openAnswer, setOpenAnswer] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [lastGrade, setLastGrade] = useState<{ Correctness: string; Feedback: string } | null>(null);
  const [roundResult, setRoundResult] = useState<{ Passed: boolean; FinalScore: number } | null>(null);
  const [gamificationBundle, setGamificationBundle] = useState<GamificationRewardBundleDto | null>(null);
  const [showReflection, setShowReflection] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const passed =
    round && !round.CurrentQuestion ? (roundResult?.Passed ?? round.Status === 'Passed') : false;

  useEffect(() => {
    if (passed && !completedRef.current) {
      completedRef.current = true;
      onComplete();
    }
  }, [passed, onComplete]);

  useEffect(() => {
    if (!personId) return;
    getActiveStructuredLearningAssessmentRound(sectionId, personId)
      .then((active) => setRound(active))
      .catch((err) => setError(err instanceof Error ? err.message : String(err)));
  }, [sectionId, personId]);

  async function handleStart() {
    setSubmitting(true);
    setError(null);
    try {
      const state = await startStructuredLearningAssessmentRound(sectionId, personId);
      setRound(state);
      setRoundResult(null);
      setLastGrade(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  async function handleSubmitAnswer() {
    if (!round?.CurrentQuestion) return;
    const isMultipleChoice = questionType === 'MultipleChoice';
    if (isMultipleChoice && selectedOption === null) return;
    if (!isMultipleChoice && !openAnswer.trim()) return;
    const answer = isMultipleChoice ? String(selectedOption) : openAnswer;

    setSubmitting(true);
    setError(null);
    try {
      const result = await submitStructuredLearningAssessmentAnswer(
        round.CurrentQuestion.StructuredLearningAssessmentQuestionAttemptId,
        answer
      );
      setLastGrade(result.Grade);
      setSelectedOption(null);
      setOpenAnswer('');

      const gami = result.Gamification ?? result.gamification;
      if (result.Passed && gami && !(gami.alreadyEarned || gami.AlreadyEarned)) {
        const effectiveTier = gami.reward?.tier ?? gami.Reward?.Tier ?? gami.tier ?? gami.Tier ?? tier;
        const isCriticalOrImportant = effectiveTier === 'Critical' || effectiveTier === 'Important';
        if (isCriticalOrImportant) {
          setShowReflection(true);
        }
        setGamificationBundle(gami);
      }

      if (result.RoundComplete) {
        setRoundResult(result.Passed !== null ? { Passed: result.Passed, FinalScore: result.FinalScore ?? 0 } : null);
        if (result.NextQuestion && result.NewStructuredLearningAssessmentRoundId) {
          setRound({
            StructuredLearningAssessmentRoundId: result.NewStructuredLearningAssessmentRoundId,
            RoundNumber: result.NewRoundNumber ?? round.RoundNumber + 1,
            Status: 'InProgress',
            FinalScore: null,
            TotalQuestions: round.TotalQuestions,
            CurrentQuestion: result.NextQuestion,
          });
        } else {
          setRound({
            ...round,
            CurrentQuestion: null,
            Status: result.Passed ? 'Passed' : 'Failed',
            FinalScore: result.FinalScore ?? null,
          });
        }
      } else {
        setRound({ ...round, CurrentQuestion: result.NextQuestion });
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (error) return <p className="mt-3 text-sm text-red-300">{error}</p>;

  if (round === undefined) {
    return (
      <div className="flex items-center gap-2 p-4 text-xs text-slate-400">
        <Loader2 className="h-4 w-4 animate-spin" /> {isEn ? 'Loading assessment...' : 'Cargando evaluación...'}
      </div>
    );
  }

  if (round === null) {
    return (
      <button
        type="button"
        onClick={handleStart}
        disabled={submitting}
        className="mt-3 inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <ClipboardCheck className="h-4 w-4" />}
        {isEn ? 'Start Assessment' : 'Iniciar Evaluación'}
      </button>
    );
  }

  if (!round.CurrentQuestion) {
    return (
      <div
        className={`mt-3 rounded-xl border p-4 text-sm ${
          passed ? 'border-emerald-400/30 bg-emerald-500/10 text-emerald-200' : 'border-red-400/30 bg-red-500/10 text-red-200'
        }`}
      >
        <p className="font-semibold">
          {passed
            ? (isEn ? 'Assessment Passed! Engram Mastered.' : '¡Evaluación Aprobada! Engrama Dominado.')
            : (isEn ? 'Assessment not passed. You can retry.' : 'Evaluación no aprobada. Puedes intentarlo de nuevo.')}
        </p>
        {(roundResult?.FinalScore ?? round.FinalScore) != null && (
          <p className="mt-0.5 text-xs">
            {isEn ? 'Final Score' : 'Puntuación Final'}: {roundResult?.FinalScore ?? round.FinalScore}%
          </p>
        )}
        {passed && (
          <button type="button" onClick={handleStart} className="mt-2 text-xs font-medium underline">
            {isEn ? 'Retake Assessment' : 'Repetir Evaluación'}
          </button>
        )}

        {showReflection && gamificationBundle && (
          <ReflectionPause
            engramTitle={engramTitle}
            onComplete={() => setShowReflection(false)}
            onSkip={() => setShowReflection(false)}
          />
        )}

        {!showReflection && gamificationBundle && (
          <RewardCelebration
            bundle={gamificationBundle}
            onClose={() => setGamificationBundle(null)}
          />
        )}
      </div>
    );
  }

  const q = round.CurrentQuestion;
  const gradeColor =
    lastGrade?.Correctness === 'Correct'
      ? 'border-emerald-400/30 bg-emerald-500/10 text-emerald-200'
      : lastGrade?.Correctness === 'PartiallyCorrect'
        ? 'border-amber-400/30 bg-amber-500/10 text-amber-200'
        : 'border-red-400/30 bg-red-500/10 text-red-200';

  return (
    <div className="mt-3 space-y-3">
      <p className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
        {isEn ? `Question ${q.QuestionIndex + 1} of ${round.TotalQuestions} (Round ${round.RoundNumber})` : `Pregunta ${q.QuestionIndex + 1} de ${round.TotalQuestions} (Ronda ${round.RoundNumber})`}
      </p>
      <p className="text-sm font-semibold text-slate-900 dark:text-white leading-relaxed">{q.QuestionText}</p>

      {lastGrade && <div className={`rounded-xl border p-3 text-xs font-medium ${gradeColor}`}>{lastGrade.Feedback}</div>}

      {questionType === 'MultipleChoice' && q.Options ? (
        <div className="space-y-2">
          {q.Options.map((opt, index) => (
            <label
              key={index}
              className={`flex cursor-pointer items-center gap-2.5 rounded-xl border p-3 text-sm transition ${
                selectedOption === index
                  ? 'border-emerald-500 bg-emerald-50 font-semibold text-emerald-950 dark:border-emerald-400/60 dark:bg-emerald-500/10 dark:text-white'
                  : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:bg-white/[0.02] dark:text-slate-300 dark:hover:bg-white/[0.04]'
              }`}
            >
              <input
                type="radio"
                name={`sl-assessment-${q.StructuredLearningAssessmentQuestionAttemptId}`}
                checked={selectedOption === index}
                onChange={() => setSelectedOption(index)}
                className="h-4 w-4 accent-emerald-600"
              />
              {opt}
            </label>
          ))}
        </div>
      ) : (
        <textarea
          value={openAnswer}
          onChange={(e) => setOpenAnswer(e.target.value)}
          rows={3}
          placeholder={isEn ? 'Enter your answer...' : 'Ingresa tu respuesta...'}
          className="w-full rounded-2xl border border-slate-200 bg-white p-3.5 text-sm text-slate-900 placeholder:text-slate-400 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 focus:outline-none dark:border-white/10 dark:bg-slate-950/40 dark:text-white dark:placeholder:text-slate-500"
        />
      )}

      <button
        type="button"
        onClick={handleSubmitAnswer}
        disabled={submitting}
        className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 px-5 py-2.5 text-sm font-bold text-white shadow-md shadow-emerald-500/20 transition hover:brightness-110 disabled:opacity-50"
      >
        {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
        {isEn ? 'Submit Answer' : 'Enviar Respuesta'}
      </button>
    </div>
  );
}
