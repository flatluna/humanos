import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import {
  BookOpen,
  ChefHat,
  CheckCircle2,
  FlaskConical,
  Globe2,
  Landmark,
  PawPrint,
  Sigma,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { getSubjects, type Subject } from '@/features/capabilities/api/subjectsApi';
import { SetupProgress } from '@/features/enterprise-context/components/SetupProgress';
import { useCurrentSituationStore, type SelfAssessedLevel } from './store/useCurrentSituationStore';
import { useAuth } from '@/auth/AuthContext';

/** Same Subject → icon mapping used in SubjectCapabilitiesPage.tsx, with a
 *  generic fallback for areas not in that hand-picked list (e.g. Idiomas,
 *  Carpintería, Computación). */
const SUBJECT_ICONS: Record<string, LucideIcon> = {
  finanzas: Landmark,
  cocina: ChefHat,
  'recursos-humanos': Users,
  animales: PawPrint,
  ciencia: FlaskConical,
  geografia: Globe2,
  matematicas: Sigma,
};

const LEVEL_KEYS: { key: 'beginner' | 'intermediate' | 'advanced'; value: SelfAssessedLevel }[] = [
  { key: 'beginner', value: 'Beginner' },
  { key: 'intermediate', value: 'Intermediate' },
  { key: 'advanced', value: 'Advanced' },
];

function SubjectGridSkeleton() {
  return (
    <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3" aria-hidden="true">
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="h-24 animate-pulse rounded-2xl bg-slate-200 dark:bg-white/10" />
      ))}
    </div>
  );
}

/** Growth Plan — Step 1: "Your Current Situation" (Estado Actual).
 *  A universal onboarding survey that applies to any human, not just
 *  employees — pick the real Subjects/Areas (Matemáticas, Idiomas,
 *  Carpintería, Computación, Finanzas...) you care about right now, then
 *  self-assess where you stand in each. Grounded entirely in the real
 *  `Subject` backend entity (GET /subjects) — nothing invented. The
 *  answers are stored client-side for now (useCurrentSituationStore) and
 *  later feed StartCapabilityDevelopmentRequest.SelfAssessedLevel once the
 *  person picks specific Capabilities within these areas.
 */
export function CurrentSituationPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isError, setIsError] = useState(false);

  const selectedSubjectCodes = useCurrentSituationStore((state) => state.selectedSubjectCodes);
  const levelsBySubject = useCurrentSituationStore((state) => state.selfAssessedLevelBySubject);
  const toggleSubject = useCurrentSituationStore((state) => state.toggleSubject);
  const setLevel = useCurrentSituationStore((state) => state.setLevel);
  const markCompleted = useCurrentSituationStore((state) => state.markCompleted);
  const loadFromBackend = useCurrentSituationStore((state) => state.loadFromBackend);
  const saveToBackend = useCurrentSituationStore((state) => state.saveToBackend);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    setIsError(false);

    getSubjects(i18n.language === 'es' ? 'es' : 'en')
      .then((data) => {
        if (!cancelled) setSubjects(data);
      })
      .catch(() => {
        if (!cancelled) setIsError(true);
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [i18n.language]);

  // Load from backend on mount if personId is available
  useEffect(() => {
    if (user?.personId) {
      loadFromBackend(user.personId).catch(console.warn);
    }
  }, [user?.personId, loadFromBackend]);

  const canContinue =
    selectedSubjectCodes.length > 0 && selectedSubjectCodes.every((code) => Boolean(levelsBySubject[code]));

  async function handleContinue() {
    markCompleted();
    // Save to backend before navigating
    if (user?.personId) {
      console.log('🔄 Guardando Current Situation en SQL con personId:', user.personId);
      try {
        await saveToBackend(user.personId);
        console.log('✅ Guardado en SQL exitosamente');
      } catch (error) {
        console.error('❌ Error al guardar Current Situation:', error);
        // Still navigate even if save fails (localStorage fallback exists)
      }
    } else {
      console.warn('⚠️ No hay personId disponible, usando solo localStorage');
    }
    navigate('/growth-plan');
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <SetupProgress currentStep={1} totalSteps={5} stepLabel={t('growthPlan.currentSituation.stepLabel')} />

      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">
        {t('growthPlan.currentSituation.headline')}
      </h1>
      <p className="mt-3 max-w-xl text-slate-500 dark:text-white/50">
        {t('growthPlan.currentSituation.description')}
      </p>

      <section className="mt-8">
        <h2 className="font-medium text-slate-900 dark:text-white">
          {t('growthPlan.currentSituation.areasHeading')}
        </h2>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          {t('growthPlan.currentSituation.areasHint')}
        </p>

        {isLoading && <SubjectGridSkeleton />}

        {!isLoading && isError && (
          <Card className="mt-4 p-6 text-center text-sm text-slate-500 dark:text-white/50">
            {t('growthPlan.workContext.error.message')}
          </Card>
        )}

        {!isLoading && !isError && (
          <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
            {subjects.map((subject) => {
              const Icon = SUBJECT_ICONS[subject.code] ?? BookOpen;
              const isSelected = selectedSubjectCodes.includes(subject.code);

              return (
                <button
                  key={subject.code}
                  type="button"
                  onClick={() => toggleSubject(subject.code)}
                  aria-pressed={isSelected}
                  className={`relative flex flex-col items-start gap-2 rounded-2xl border p-4 text-left transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 ${
                    isSelected
                      ? 'border-blue-400 bg-blue-50 dark:border-blue-400/50 dark:bg-blue-400/10'
                      : 'border-slate-200 bg-white hover:border-slate-300 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-white/20'
                  }`}
                >
                  {isSelected && (
                    <CheckCircle2 className="absolute right-3 top-3 h-4 w-4 text-blue-500 dark:text-blue-300" />
                  )}
                  <Icon
                    className={`h-6 w-6 ${isSelected ? 'text-blue-600 dark:text-blue-300' : 'text-slate-400 dark:text-white/40'}`}
                  />
                  <span className="text-sm font-medium text-slate-900 dark:text-white">{subject.name}</span>
                </button>
              );
            })}
          </div>
        )}
      </section>

      {selectedSubjectCodes.length > 0 && (
        <section className="mt-8 space-y-4">
          {selectedSubjectCodes.map((code) => {
            const subject = subjects.find((s) => s.code === code);
            if (!subject) return null;

            const chosenLevel = levelsBySubject[code];

            return (
              <Card key={code} className="p-5">
                <p className="font-medium text-slate-900 dark:text-white">
                  {t('growthPlan.currentSituation.levelHeading', { subject: subject.name })}
                </p>
                <p className="mt-1 text-xs text-slate-400 dark:text-white/40">
                  {t('growthPlan.currentSituation.levelHint')}
                </p>

                <div className="mt-3 grid gap-2 sm:grid-cols-3">
                  {LEVEL_KEYS.map(({ key, value }) => {
                    const isChosen = chosenLevel === value;

                    return (
                      <button
                        key={key}
                        type="button"
                        onClick={() => setLevel(code, value)}
                        aria-pressed={isChosen}
                        className={`rounded-xl border p-3 text-left transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 ${
                          isChosen
                            ? 'border-blue-400 bg-blue-50 dark:border-blue-400/50 dark:bg-blue-400/10'
                            : 'border-slate-200 bg-white hover:border-slate-300 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-white/20'
                        }`}
                      >
                        <p className="text-sm font-medium text-slate-900 dark:text-white">
                          {t(`growthPlan.currentSituation.levels.${key}`)}
                        </p>
                        <p className="mt-0.5 text-xs text-slate-500 dark:text-white/50">
                          {t(`growthPlan.currentSituation.levelDescriptions.${key}`)}
                        </p>
                      </button>
                    );
                  })}
                </div>
              </Card>
            );
          })}
        </section>
      )}

      <div className="mt-8 flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
        {!canContinue && (
          <p className="text-xs text-slate-400 dark:text-white/40">
            {t('growthPlan.currentSituation.selectAreasEmptyHint')}
          </p>
        )}
        <button
          type="button"
          disabled={!canContinue}
          onClick={handleContinue}
          className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-slate-900 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-40 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          {t('growthPlan.currentSituation.continueAction')}
        </button>
      </div>
    </div>
  );
}
