import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  BookOpen,
  ChefHat,
  CheckCircle2,
  ChevronRight,
  FlaskConical,
  Globe2,
  Landmark,
  Loader,
  PawPrint,
  Sigma,
  Sparkles,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { useAuth } from '@/auth/AuthContext';
import { SetupProgress } from '@/features/enterprise-context/components/SetupProgress';
import { useCurrentSituationStore } from '@/features/current-situation/store/useCurrentSituationStore';
import { useStartingPointStore } from './store/useStartingPointStore';
import { getSubjects, type Subject } from '@/features/capabilities/api/subjectsApi';
import type { AcceptedRecommendation } from '@/api/growthPlanApi';
import axios from 'axios';
const SUBJECT_ICONS: Record<string, LucideIcon> = {
  finanzas: Landmark,
  cocina: ChefHat,
  'recursos-humanos': Users,
  animales: PawPrint,
  ciencia: FlaskConical,
  geografia: Globe2,
  matematicas: Sigma,
};

interface RecommendationResponse {
  hasRecommendation: boolean;
  recommendationType?: string;
  programName?: string;
  programDescription?: string;
  subjectCode?: string;
  steps?: Array<{ name: string; level: string }>;
  rationale?: string;
  matchedProgramId?: string | null;
}

interface SubjectDialogState {
  subjectCode: string;
  userInput: string;
  isLoading: boolean;
  recommendation: RecommendationResponse | null;
  error: string | null;
}

function toAcceptedRecommendation(subjectCode: string, rec: RecommendationResponse): AcceptedRecommendation {
  return {
    subjectCode,
    recommendationType: rec.recommendationType ?? '',
    programName: rec.programName ?? '',
    programDescription: rec.programDescription ?? '',
    steps: rec.steps ?? [],
    rationale: rec.rationale ?? '',
    programId: rec.matchedProgramId ?? null,
  };
}

/**
 * Step 3: Interactive Program Recommendation Dialog
 * For each subject from Step 1, user describes their specific learning goal
 * Then agent recommends a training program with ordered courses
 */
export function StartingPointPageV2() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const personId = user?.personId;

  const selectedSubjectCodes = useCurrentSituationStore((state) => state.selectedSubjectCodes);
  const markCompleted = useStartingPointStore((state) => state.markCompleted);
  const saveToBackend = useStartingPointStore((state) => state.saveToBackend);
  const loadFromBackend = useStartingPointStore((state) => state.loadFromBackend);
  const acceptedRecommendations = useStartingPointStore((state) => state.acceptedRecommendations);
  const upsertAcceptedRecommendation = useStartingPointStore((state) => state.upsertAcceptedRecommendation);
  const removeAcceptedRecommendation = useStartingPointStore((state) => state.removeAcceptedRecommendation);

  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [allSubjects, setAllSubjects] = useState<Subject[]>([]);
  const [isLoadingSubjects, setIsLoadingSubjects] = useState(true);
  const [dialogStates, setDialogStates] = useState<Record<string, SubjectDialogState>>({});
  const [currentSubjectIndex, setCurrentSubjectIndex] = useState(0);

  // "Add another program" — lets the person get a recommendation for any
  // subject, not just the ones picked in Step 1.
  const [showAddExtra, setShowAddExtra] = useState(false);
  const [extraSubjectCode, setExtraSubjectCode] = useState('');
  const [extraState, setExtraState] = useState<SubjectDialogState>({
    subjectCode: '',
    userInput: '',
    isLoading: false,
    recommendation: null,
    error: null,
  });

  // Load previously accepted recommendations from SQL on mount
  useEffect(() => {
    if (personId) {
      loadFromBackend(personId).catch(console.warn);
    }
  }, [personId, loadFromBackend]);

  // Load subjects on mount
  useEffect(() => {
    const loadSubjects = async () => {
      try {
        setIsLoadingSubjects(true);
        const allSubjectsList = await getSubjects(i18n.language === 'es' ? 'es' : 'en');
        setAllSubjects(allSubjectsList);
        const selectedSubjects = allSubjectsList.filter((s) => selectedSubjectCodes.includes(s.code));
        setSubjects(selectedSubjects);

        // Initialize dialog states
        const initialStates: Record<string, SubjectDialogState> = {};
        selectedSubjects.forEach((subject) => {
          initialStates[subject.code] = {
            subjectCode: subject.code,
            userInput: '',
            isLoading: false,
            recommendation: null,
            error: null,
          };
        });
        setDialogStates(initialStates);
      } catch (error) {
        console.error('Error loading subjects:', error);
      } finally {
        setIsLoadingSubjects(false);
      }
    };

    loadSubjects();
  }, [selectedSubjectCodes, i18n.language]);

  const currentSubject = subjects[currentSubjectIndex];
  const currentState = currentSubject ? dialogStates[currentSubject.code] : null;

  const handleUserInputChange = (value: string) => {
    if (!currentSubject) return;

    setDialogStates((prev) => ({
      ...prev,
      [currentSubject.code]: {
        ...prev[currentSubject.code],
        userInput: value,
      },
    }));
  };

  const handleGenerateRecommendation = async () => {
    if (!currentSubject || !currentState?.userInput.trim()) return;

    const subjectCode = currentSubject.code;

    setDialogStates((prev) => ({
      ...prev,
      [subjectCode]: {
        ...prev[subjectCode],
        isLoading: true,
        error: null,
        recommendation: null,
      },
    }));

    try {
      console.log('🔄 Generando recomendación para:', subjectCode, currentState.userInput);

      const response = await axios.post<RecommendationResponse>(
        '/api/growth-plan/starting-point/recommend',
        {
          goalPrompt: currentState.userInput,
          personName: user?.name ?? 'Usuario',
          allowedSubjects: [{ code: subjectCode, name: currentSubject.name }],
          statedGoals: [],
          catalogContext: buildCatalogForSubject(subjectCode),
        },
        { timeout: 30000 },
      );

      console.log('✅ Recomendación recibida:', response.data);

      setDialogStates((prev) => ({
        ...prev,
        [subjectCode]: {
          ...prev[subjectCode],
          recommendation: response.data,
          isLoading: false,
        },
      }));
    } catch (error) {
      const errorMsg =
        error instanceof Error ? error.message : typeof error === 'object' && error ? String(error) : 'Error desconocido';
      console.error('❌ Error generando recomendación:', errorMsg);

      setDialogStates((prev) => ({
        ...prev,
        [subjectCode]: {
          ...prev[subjectCode],
          error: errorMsg,
          isLoading: false,
        },
      }));
    }
  };

  const handleAcceptRecommendation = async () => {
    if (!currentSubject || !currentState?.recommendation?.hasRecommendation) return;

    upsertAcceptedRecommendation(toAcceptedRecommendation(currentSubject.code, currentState.recommendation));

    // Move to next subject or finish
    if (currentSubjectIndex < subjects.length - 1) {
      setCurrentSubjectIndex(currentSubjectIndex + 1);
    } else {
      // All subjects done, save and finish
      await handleFinish();
    }
  };

  const handleRejectRecommendation = () => {
    if (!currentSubject) return;

    const subjectCode = currentSubject.code;

    // Clear the recommendation so user can try again
    setDialogStates((prev) => ({
      ...prev,
      [subjectCode]: {
        ...prev[subjectCode],
        recommendation: null,
        userInput: '',
      },
    }));
  };

  // ── "Agregar otro programa" — a subject not necessarily picked in Step 1 ──

  const handleExtraGenerateRecommendation = async () => {
    if (!extraSubjectCode || !extraState.userInput.trim()) return;

    const subject = allSubjects.find((s) => s.code === extraSubjectCode);
    if (!subject) return;

    setExtraState((prev) => ({ ...prev, isLoading: true, error: null, recommendation: null }));

    try {
      const response = await axios.post<RecommendationResponse>(
        '/api/growth-plan/starting-point/recommend',
        {
          goalPrompt: extraState.userInput,
          personName: user?.name ?? 'Usuario',
          allowedSubjects: [{ code: subject.code, name: subject.name }],
          statedGoals: [],
          catalogContext: buildCatalogForSubject(subject.code),
        },
        { timeout: 30000 },
      );

      setExtraState((prev) => ({ ...prev, recommendation: response.data, isLoading: false }));
    } catch (error) {
      const errorMsg =
        error instanceof Error ? error.message : typeof error === 'object' && error ? String(error) : 'Error desconocido';
      setExtraState((prev) => ({ ...prev, error: errorMsg, isLoading: false }));
    }
  };

  const handleExtraAccept = () => {
    if (!extraSubjectCode || !extraState.recommendation?.hasRecommendation) return;

    upsertAcceptedRecommendation(toAcceptedRecommendation(extraSubjectCode, extraState.recommendation));
    setShowAddExtra(false);
    setExtraSubjectCode('');
    setExtraState({ subjectCode: '', userInput: '', isLoading: false, recommendation: null, error: null });
  };

  const handleExtraCancel = () => {
    setShowAddExtra(false);
    setExtraSubjectCode('');
    setExtraState({ subjectCode: '', userInput: '', isLoading: false, recommendation: null, error: null });
  };

  const handleFinish = async () => {
    if (!personId) {
      markCompleted();
      navigate('/growth-plan');
      return;
    }

    try {
      // Mark completed BEFORE saving — saveToBackend persists whatever
      // `completed` currently is (same fix as FutureDirectionPage.tsx,
      // bug fixed 2026-07-24).
      markCompleted();

      console.log('💾 Guardando recomendaciones en SQL...');
      await saveToBackend(personId);
      console.log('✅ Guardado exitoso');

      navigate('/growth-plan');
    } catch (error) {
      console.error('❌ Error guardando:', error);
    } finally {
      // Save completed
    }
  };

  if (isLoadingSubjects) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-10">
        <SetupProgress currentStep={3} totalSteps={5} stepLabel={t('growthPlan.startingPoint.stepLabel')} />
        <div className="mt-8 flex justify-center">
          <Loader className="h-8 w-8 animate-spin text-blue-500" />
        </div>
      </div>
    );
  }

  if (subjects.length === 0) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-10">
        <SetupProgress currentStep={3} totalSteps={5} stepLabel={t('growthPlan.startingPoint.stepLabel')} />
        <Card className="mt-8 border-amber-200 bg-amber-50 p-6 dark:border-amber-400/30 dark:bg-amber-400/5">
          <p className="text-sm text-slate-600 dark:text-white/60">
            Por favor completa el Step 1 primero (selecciona áreas de interés)
          </p>
        </Card>
      </div>
    );
  }

  const isLastSubject = currentSubjectIndex === subjects.length - 1;

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <SetupProgress currentStep={3} totalSteps={5} stepLabel={t('growthPlan.startingPoint.stepLabel')} />

      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">
        ¿Por dónde empieza tu crecimiento?
      </h1>
      <p className="mt-3 max-w-xl text-slate-500 dark:text-white/50">
        Para cada área que elegiste, cuéntanos qué específicamente quieres aprender. Nuestro agente te recomendará un
        programa de capacitación adaptado a tus objetivos.
      </p>

      {/* Accepted programs so far */}
      {acceptedRecommendations.length > 0 && (
        <div className="mt-8">
          <h2 className="text-sm font-semibold uppercase tracking-widest text-slate-500 dark:text-white/50">
            Tus Programas
          </h2>
          <div className="mt-3 space-y-2">
            {acceptedRecommendations.map((rec) => {
              const subjectName =
                allSubjects.find((s) => s.code === rec.subjectCode)?.name ?? rec.subjectCode;
              return (
                <div
                  key={rec.subjectCode}
                  className="flex items-start justify-between gap-3 rounded-lg border border-green-200 bg-green-50 p-3 dark:border-green-400/30 dark:bg-green-400/5"
                >
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-wider text-green-700 dark:text-green-300">
                      {subjectName}
                    </p>
                    <p className="text-sm font-medium text-slate-900 dark:text-white">{rec.programName}</p>
                  </div>
                  <button
                    onClick={() => removeAcceptedRecommendation(rec.subjectCode)}
                    className="text-xs font-medium text-slate-500 hover:text-red-600 dark:text-white/50 dark:hover:text-red-400"
                  >
                    Quitar
                  </button>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Add another program (not limited to Step 1 subjects) */}
      <div className="mt-6">
        {!showAddExtra ? (
          <button
            onClick={() => setShowAddExtra(true)}
            className="inline-flex min-h-10 items-center gap-2 rounded-full border border-dashed border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-600 transition hover:border-blue-400 hover:text-blue-600 dark:border-white/20 dark:bg-white/5 dark:text-white/60 dark:hover:border-blue-400/50 dark:hover:text-blue-300"
          >
            <Sparkles className="h-4 w-4" />
            Agregar otro programa
          </button>
        ) : (
          <Card className="p-6">
            <h3 className="text-sm font-semibold text-slate-900 dark:text-white">Agregar otro programa</h3>

            {!extraState.recommendation?.hasRecommendation ? (
              <div className="mt-4 space-y-3">
                <select
                  value={extraSubjectCode}
                  onChange={(e) => setExtraSubjectCode(e.target.value)}
                  className="w-full rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
                >
                  {/* Native <option> popups are OS-rendered with a light
                      background regardless of the page's dark mode, so
                      options need an explicit dark, always-on text color
                      here (not `dark:text-white`) or they become invisible
                      white-on-white in dark mode (bug fixed 2026-07-24). */}
                  <option value="" className="text-slate-900" style={{ color: '#0f172a', backgroundColor: '#ffffff' }}>
                    Elige un área...
                  </option>
                  {allSubjects.map((subject) => (
                    <option
                      key={subject.code}
                      value={subject.code}
                      className="text-slate-900"
                      style={{ color: '#0f172a', backgroundColor: '#ffffff' }}
                    >
                      {subject.name}
                    </option>
                  ))}
                </select>

                <textarea
                  value={extraState.userInput}
                  onChange={(e) => setExtraState((prev) => ({ ...prev, userInput: e.target.value }))}
                  placeholder="Describe aquí qué quieres aprender específicamente..."
                  className="min-h-24 w-full rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 placeholder-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-white dark:placeholder-white/40"
                />

                {extraState.error && (
                  <div className="flex gap-3 rounded-lg border border-red-200 bg-red-50 p-3 dark:border-red-400/30 dark:bg-red-400/5">
                    <AlertCircle className="h-4 w-4 flex-none text-red-500 dark:text-red-400" />
                    <p className="text-xs text-red-600 dark:text-red-300">{extraState.error}</p>
                  </div>
                )}

                <div className="flex gap-3">
                  <button
                    onClick={handleExtraGenerateRecommendation}
                    disabled={!extraSubjectCode || !extraState.userInput.trim() || extraState.isLoading}
                    className="flex-1 inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-blue-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-blue-500 dark:hover:bg-blue-600"
                  >
                    {extraState.isLoading ? (
                      <>
                        <Loader className="h-4 w-4 animate-spin" />
                        Generando recomendación...
                      </>
                    ) : (
                      <>
                        <Sparkles className="h-4 w-4" />
                        Generar Recomendación
                      </>
                    )}
                  </button>
                  <button
                    onClick={handleExtraCancel}
                    className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full border border-slate-300 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10"
                  >
                    Cancelar
                  </button>
                </div>
              </div>
            ) : (
              <div className="mt-4 space-y-4">
                <div className="rounded-lg border border-green-200 bg-green-50 p-4 dark:border-green-400/30 dark:bg-green-400/5">
                  <h4 className="text-lg font-semibold text-slate-900 dark:text-white">
                    {extraState.recommendation.programName}
                  </h4>
                  <p className="mt-2 text-sm text-slate-600 dark:text-white/60">
                    {extraState.recommendation.programDescription}
                  </p>
                  {extraState.recommendation.rationale && (
                    <p className="mt-3 border-l-2 border-green-300 bg-white px-3 py-2 text-sm italic text-slate-600 dark:border-green-400/50 dark:bg-white/5 dark:text-white/60">
                      "{extraState.recommendation.rationale}"
                    </p>
                  )}
                </div>
                <div className="flex gap-3">
                  <button
                    onClick={handleExtraAccept}
                    className="flex-1 inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-green-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-green-700 dark:bg-green-500 dark:hover:bg-green-600"
                  >
                    <CheckCircle2 className="h-4 w-4" />
                    Aceptar
                  </button>
                  <button
                    onClick={() => setExtraState((prev) => ({ ...prev, recommendation: null, userInput: '' }))}
                    className="flex-1 inline-flex min-h-11 items-center justify-center gap-2 rounded-full border border-slate-300 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10"
                  >
                    Solicitar Otra
                  </button>
                </div>
              </div>
            )}
          </Card>
        )}
      </div>

      {/* Progress Indicator */}
      <div className="mt-8 flex items-center justify-between rounded-lg bg-slate-100 p-4 dark:bg-white/5">
        <div className="flex flex-wrap gap-2">
          {subjects.map((subject, idx) => {
            const Icon = SUBJECT_ICONS[subject.code] || BookOpen;
            const state = dialogStates[subject.code];
            const isComplete = state?.recommendation?.hasRecommendation;
            const isCurrent = idx === currentSubjectIndex;

            return (
              <button
                key={subject.code}
                onClick={() => setCurrentSubjectIndex(idx)}
                className={`inline-flex min-h-10 items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition ${
                  isCurrent
                    ? 'border-blue-400 bg-blue-100 text-blue-700 dark:border-blue-400/50 dark:bg-blue-400/20 dark:text-blue-200'
                    : isComplete
                      ? 'border-green-300 bg-green-100 text-green-700 dark:border-green-400/50 dark:bg-green-400/20 dark:text-green-200'
                      : 'border-slate-300 bg-white text-slate-600 dark:border-white/10 dark:bg-white/5 dark:text-white/60'
                }`}
              >
                <Icon className="h-3.5 w-3.5" />
                {subject.name}
                {isComplete && <CheckCircle2 className="h-3 w-3" />}
              </button>
            );
          })}
        </div>
        <span className="text-xs font-medium text-slate-600 dark:text-white/60">
          {currentSubjectIndex + 1} de {subjects.length}
        </span>
      </div>

      {/* Dialog for Current Subject */}
      {currentSubject && currentState && (
        <Card className="mt-8 p-6">
          <div className="space-y-6">
            {/* Subject Header */}
            <div>
              <div className="inline-flex items-center gap-2 rounded-full bg-blue-100 px-3 py-1 text-xs font-semibold uppercase tracking-wider text-blue-700 dark:bg-blue-400/20 dark:text-blue-200">
                {SUBJECT_ICONS[currentSubject.code] && <span>{currentSubject.name}</span>}
              </div>
              <h2 className="mt-3 text-xl font-semibold text-slate-900 dark:text-white">
                ¿Qué quieres aprender de {currentSubject.name}?
              </h2>
              <p className="mt-2 text-sm text-slate-600 dark:text-white/60">
                Describe tu objetivo de forma específica. Ej: "Quiero aprender a crear presupuestos para empresas" o "Cómo
                escribir un plan de negocios efectivo"
              </p>
            </div>

            {/* User Input or Recommendation */}
            {!currentState.recommendation?.hasRecommendation ? (
              <>
                {/* Input Mode */}
                <div>
                  <textarea
                    value={currentState.userInput}
                    onChange={(e) => handleUserInputChange(e.target.value)}
                    placeholder="Describe aquí qué quieres aprender específicamente..."
                    className="min-h-24 w-full rounded-lg border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 placeholder-slate-400 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-white dark:placeholder-white/40"
                  />
                </div>

                {/* Error Display */}
                {currentState.error && (
                  <div className="flex gap-3 rounded-lg border border-red-200 bg-red-50 p-3 dark:border-red-400/30 dark:bg-red-400/5">
                    <AlertCircle className="h-4 w-4 flex-none text-red-500 dark:text-red-400" />
                    <p className="text-xs text-red-600 dark:text-red-300">{currentState.error}</p>
                  </div>
                )}

                {/* Action Buttons */}
                <div className="flex gap-3">
                  <button
                    onClick={handleGenerateRecommendation}
                    disabled={!currentState.userInput.trim() || currentState.isLoading}
                    className="flex-1 inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-blue-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-blue-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-blue-500 dark:hover:bg-blue-600"
                  >
                    {currentState.isLoading ? (
                      <>
                        <Loader className="h-4 w-4 animate-spin" />
                        Generando recomendación...
                      </>
                    ) : (
                      <>
                        <Sparkles className="h-4 w-4" />
                        Generar Recomendación
                      </>
                    )}
                  </button>

                  {currentSubjectIndex > 0 && (
                    <button
                      onClick={() => setCurrentSubjectIndex(currentSubjectIndex - 1)}
                      className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full border border-slate-300 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10"
                    >
                      Anterior
                    </button>
                  )}
                </div>
              </>
            ) : (
              <>
                {/* Recommendation Display */}
                <div className="rounded-lg border border-green-200 bg-green-50 p-4 dark:border-green-400/30 dark:bg-green-400/5">
                  <div className="inline-flex items-center gap-2 rounded-full bg-green-100 px-3 py-1 text-xs font-semibold uppercase tracking-wider text-green-700 dark:bg-green-400/20 dark:text-green-200">
                    <CheckCircle2 className="h-3 w-3" />
                    Programa Recomendado
                  </div>

                  <h3 className="mt-3 text-lg font-semibold text-slate-900 dark:text-white">
                    {currentState.recommendation.programName}
                  </h3>

                  <p className="mt-2 text-sm text-slate-600 dark:text-white/60">
                    {currentState.recommendation.programDescription}
                  </p>

                  {currentState.recommendation.rationale && (
                    <p className="mt-3 border-l-2 border-green-300 bg-white px-3 py-2 text-sm italic text-slate-600 dark:border-green-400/50 dark:bg-white/5 dark:text-white/60">
                      "{currentState.recommendation.rationale}"
                    </p>
                  )}

                  {/* Courses */}
                  {currentState.recommendation.steps && currentState.recommendation.steps.length > 0 && (
                    <div className="mt-4">
                      <p className="text-xs font-semibold uppercase tracking-widest text-slate-500 dark:text-white/50">
                        Cursos Ordenados
                      </p>
                      <div className="mt-2 space-y-2">
                        {currentState.recommendation.steps.map((step, idx) => (
                          <div
                            key={idx}
                            className="flex items-center gap-3 rounded-lg border border-slate-200 bg-white p-3 dark:border-white/10 dark:bg-white/5"
                          >
                            <div className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-green-200 text-xs font-semibold text-green-700 dark:bg-green-400/20 dark:text-green-300">
                              {idx + 1}
                            </div>
                            <div className="flex-1">
                              <p className="text-sm font-medium text-slate-900 dark:text-white">{step.name}</p>
                              <p className="text-xs text-slate-500 dark:text-white/50">{step.level}</p>
                            </div>
                            {idx < (currentState.recommendation?.steps?.length ?? 0) - 1 && (
                              <ChevronRight className="h-4 w-4 text-slate-300 dark:text-white/20" />
                            )}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>

                {/* Recommendation Action Buttons */}
                <div className="flex gap-3">
                  <button
                    onClick={handleAcceptRecommendation}
                    className="flex-1 inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-green-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-green-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-green-500 dark:bg-green-500 dark:hover:bg-green-600"
                  >
                    <CheckCircle2 className="h-4 w-4" />
                    {isLastSubject ? 'Aceptar y Finalizar' : 'Aceptar y Continuar'}
                  </button>

                  <button
                    onClick={handleRejectRecommendation}
                    className="flex-1 inline-flex min-h-11 items-center justify-center gap-2 rounded-full border border-slate-300 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 transition hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-400 dark:border-white/10 dark:bg-white/5 dark:text-white/70 dark:hover:bg-white/10"
                  >
                    Solicitar Otra Recomendación
                  </button>
                </div>
              </>
            )}
          </div>
        </Card>
      )}
    </div>
  );
}

/**
 * Build a mock catalog of training programs for a specific subject
 * In a real app, this would come from a database
 */
function buildCatalogForSubject(subjectCode: string): string {  const catalogs: Record<string, string> = {
    finanzas: `
      Programas disponibles en Finanzas (en desarrollo):
      1. "Fundamentos de Presupuestos" (4 cursos) - Presupuesto personal, empresarial, casos prácticos
      2. "Planes de Negocio Efectivos" (5 cursos) - Estructura, análisis financiero, presentación
      3. "Análisis Financiero Básico" (3 cursos) - Estados financieros, ratios, interpretación
      4. "Inversiones para Principiantes" (4 cursos) - Acciones, bonos, portafolios, diversificación
    `,
    cocina: `
      Programas disponibles en Cocina (en desarrollo):
      1. "Técnicas Básicas de Cocina" (4 cursos)
      2. "Gastronomía Internacional" (5 cursos)
      3. "Repostería Profesional" (3 cursos)
    `,
    default: `
      Programas disponibles (en desarrollo):
      1. Programa Fundacional
      2. Programa Intermedio
      3. Programa Avanzado
    `,
  };

  return catalogs[subjectCode] || catalogs.default;
}
