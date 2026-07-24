import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router';
import { useTranslation } from 'react-i18next';
import { CheckCircle2 } from 'lucide-react';
import { SetupProgress } from '@/features/enterprise-context/components/SetupProgress';
import { useAuth } from '@/auth/AuthContext';
import { getGoals, getPersonGoals, adoptGoal, abandonPersonGoal, setPersonMotivations } from '@/api/humanOsApi';
import {
  useFutureDirectionStore,
  FUTURE_GOAL_IDS,
  MOTIVATION_IDS,
} from './store/useFutureDirectionStore';

/** Growth Plan — Step 2: "Where You Want to Go" (Hacia Dónde Quieres
 *  Llegar). A universal survey that applies to any human, not just
 *  employees — what would you like to achieve, and what motivates you.
 *  Replaces the employee-specific "Work and Experience" (job
 *  description/résumé) step in the individuals sequence; that route
 *  (/growth-plan/role-experience) still exists untouched for a future
 *  employee-specific flow. Now syncs both with backend database (via
 *  persisted store) and with Goal/Motivation catalog endpoints.
 */
export function FutureDirectionPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();

  const selectedGoalIds = useFutureDirectionStore((state) => state.selectedGoalIds);
  const selectedMotivationIds = useFutureDirectionStore((state) => state.selectedMotivationIds);
  const toggleGoal = useFutureDirectionStore((state) => state.toggleGoal);
  const toggleMotivation = useFutureDirectionStore((state) => state.toggleMotivation);
  const markCompleted = useFutureDirectionStore((state) => state.markCompleted);
  const loadFromBackend = useFutureDirectionStore((state) => state.loadFromBackend);
  const saveToBackend = useFutureDirectionStore((state) => state.saveToBackend);

  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState(false);

  // Load from backend on mount if personId is available
  useEffect(() => {
    if (user?.personId) {
      loadFromBackend(user.personId).catch(console.warn);
    }
  }, [user?.personId, loadFromBackend]);

  const canContinue = selectedGoalIds.length > 0 && !isSaving;

  async function handleContinue() {
    const personId = user?.personId;

    if (!personId) {
      // Not onboarded yet (no real Person record) — nothing to persist,
      // keep the local-only behavior so the wizard still progresses.
      markCompleted();
      navigate('/growth-plan');
      return;
    }

    setIsSaving(true);
    setSaveError(false);

    try {
      // Mark completed BEFORE saving — saveToBackend persists whatever
      // `completed` currently is, so this must happen first or the SQL
      // row is saved with completed=false forever (bug fixed 2026-07-24).
      markCompleted();

      // Save store state to backend
      console.log('🔄 Guardando Future Direction en SQL con personId:', personId);
      await saveToBackend(personId);
      console.log('✅ Guardado en SQL exitosamente');

      // Also sync with existing Goal/Motivation endpoints
      const [catalog, existingPersonGoals] = await Promise.all([
        getGoals('en'),
        getPersonGoals(personId, 'en'),
      ]);

      const goalIdByCode = new Map(catalog.map((goal) => [goal.code, goal.goalId]));
      const activeGoalsByCode = new Map(
        existingPersonGoals.filter((goal) => goal.status === 'Active').map((goal) => [goal.goalCode, goal]),
      );

      const goalsToAdopt = selectedGoalIds.filter((code) => !activeGoalsByCode.has(code));
      const goalsToAbandon = [...activeGoalsByCode.values()].filter(
        (goal) => !selectedGoalIds.includes(goal.goalCode as (typeof selectedGoalIds)[number]),
      );

      await Promise.all([
        ...goalsToAdopt.map((code) => {
          const goalId = goalIdByCode.get(code);
          return goalId ? adoptGoal(personId, goalId) : Promise.resolve();
        }),
        ...goalsToAbandon.map((goal) => abandonPersonGoal(personId, goal.personGoalId)),
        setPersonMotivations(personId, selectedMotivationIds),
      ]);

      navigate('/growth-plan');
    } catch {
      setSaveError(true);
    } finally {
      setIsSaving(false);
    }
  }


  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <SetupProgress currentStep={2} totalSteps={5} stepLabel={t('growthPlan.futureDirection.stepLabel')} />

      <h1 className="mt-6 text-3xl font-semibold tracking-tight text-slate-900 dark:text-white">
        {t('growthPlan.futureDirection.headline')}
      </h1>
      <p className="mt-3 max-w-xl text-slate-500 dark:text-white/50">
        {t('growthPlan.futureDirection.description')}
      </p>

      <section className="mt-8">
        <h2 className="font-medium text-slate-900 dark:text-white">
          {t('growthPlan.futureDirection.goalsHeading')}
        </h2>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          {t('growthPlan.futureDirection.goalsHint')}
        </p>

        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          {FUTURE_GOAL_IDS.map((id) => {
            const isSelected = selectedGoalIds.includes(id);

            return (
              <button
                key={id}
                type="button"
                onClick={() => toggleGoal(id)}
                aria-pressed={isSelected}
                className={`relative rounded-2xl border p-4 text-left transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 ${
                  isSelected
                    ? 'border-blue-400 bg-blue-50 dark:border-blue-400/50 dark:bg-blue-400/10'
                    : 'border-slate-200 bg-white hover:border-slate-300 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-white/20'
                }`}
              >
                {isSelected && (
                  <CheckCircle2 className="absolute right-3 top-3 h-4 w-4 text-blue-500 dark:text-blue-300" />
                )}
                <p className="pr-6 text-sm font-medium text-slate-900 dark:text-white">
                  {t(`growthPlan.futureDirection.goals.${id}`)}
                </p>
                <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
                  {t(`growthPlan.futureDirection.goalDescriptions.${id}`)}
                </p>
              </button>
            );
          })}
        </div>
      </section>

      <section className="mt-8">
        <h2 className="font-medium text-slate-900 dark:text-white">
          {t('growthPlan.futureDirection.motivationsHeading')}
        </h2>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          {t('growthPlan.futureDirection.motivationsHint')}
        </p>

        <div className="mt-4 flex flex-wrap gap-2">
          {MOTIVATION_IDS.map((id) => {
            const isSelected = selectedMotivationIds.includes(id);

            return (
              <button
                key={id}
                type="button"
                onClick={() => toggleMotivation(id)}
                aria-pressed={isSelected}
                className={`rounded-full border px-4 py-2 text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 ${
                  isSelected
                    ? 'border-blue-400 bg-blue-50 text-blue-700 dark:border-blue-400/50 dark:bg-blue-400/10 dark:text-blue-200'
                    : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300 dark:border-white/10 dark:bg-white/[0.03] dark:text-white/60 dark:hover:border-white/20'
                }`}
              >
                {t(`growthPlan.futureDirection.motivations.${id}`)}
              </button>
            );
          })}
        </div>
      </section>

      <div className="mt-8 flex flex-col items-start gap-3 sm:flex-row sm:items-center sm:justify-between">
        {selectedGoalIds.length === 0 && (
          <p className="text-xs text-slate-400 dark:text-white/40">
            {t('growthPlan.futureDirection.selectGoalsEmptyHint')}
          </p>
        )}
        {saveError && (
          <p className="text-xs text-red-500 dark:text-red-400">
            {t('growthPlan.futureDirection.saveError')}
          </p>
        )}
        <button
          type="button"
          disabled={!canContinue}
          onClick={handleContinue}
          className="inline-flex min-h-11 items-center justify-center gap-2 rounded-full bg-slate-900 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-40 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          {isSaving ? t('growthPlan.futureDirection.savingAction') : t('growthPlan.futureDirection.continueAction')}
        </button>
      </div>
    </div>
  );
}
