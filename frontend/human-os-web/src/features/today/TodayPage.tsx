import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { Compass, Sparkles } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getPersonGoals, getPersonMotivations, getHumanStateToday, upsertHumanState } from '@/api/humanOsApi';
import { getEnrolledPrograms, type EnrolledProgram } from '@/features/my-programs/api/enrolledProgramsApi';
import { getEnrolledCapabilities, type EnrolledCapability } from '@/features/capabilities/api/enrolledCapabilitiesApi';
import { getActiveSession } from '@/features/capabilities/api/runtimeSessionApi';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { ProgressOverview } from './components/ProgressOverview';
import { GrowthAlignment } from './components/GrowthAlignment';
import { CapabilitiesSection } from './components/CapabilitiesSection';
import { ContinueLearningSection, type ContinueLearningItem } from './components/ContinueLearningSection';
import { HumanStateSection } from './components/HumanStateSection';
import type { CapabilityLevel, CapabilityProgress, HumanStateMetric, LocalizedText, PersonalGoal } from './types';
import type { HumanStateCheckInResponse, UpsertHumanStateRequest } from '@/api/types';

type GreetingKey = 'morning' | 'afternoon' | 'evening';

function useGreetingKey(): GreetingKey {
  const hour = new Date().getHours();
  if (hour < 12) return 'morning';
  if (hour < 18) return 'afternoon';
  return 'evening';
}

/** Goal/motivation names already come back localized in the requested
 *  language from the backend — wrap as a LocalizedText using the same
 *  string for both keys so `useLocalizedText()` renders correctly
 *  regardless of which UI language is active. */
function toLocalizedText(text: string): LocalizedText {
  return { en: text, es: text };
}

/** Courses don't have a "mastery" flag — level is derived from the average
 *  score of nodes already mastered so far in that capability. */
function scoreToLevel(averageScore: number | null | undefined): CapabilityLevel {
  if (averageScore === null || averageScore === undefined) return 'none';
  if (averageScore < 60) return 'basico';
  if (averageScore < 85) return 'intermedio';
  return 'avanzado';
}

export function TodayPage() {
  const { t, i18n } = useTranslation();
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const greetingKey = useGreetingKey();

  const [programs, setPrograms] = useState<EnrolledProgram[]>([]);
  // Standalone Capability enrollments (2026-07-24, "Inscribirme" on
  // /capabilities) — same real-progress data as "Mis Capacidades", never
  // tied to a Program. Shown alongside program-based capabilities here so
  // /today reflects ALL of a person's real learning, not just Programs.
  const [standaloneCapabilities, setStandaloneCapabilities] = useState<EnrolledCapability[]>([]);
  const [personalGoal, setPersonalGoal] = useState<PersonalGoal | null>(null);
  const [motivations, setMotivations] = useState<LocalizedText[]>([]);
  const [humanState, setHumanState] = useState<HumanStateCheckInResponse | null>(null);
  const [savingCheckIn, setSavingCheckIn] = useState(false);
  const [continueItems, setContinueItems] = useState<ContinueLearningItem[]>([]);
  const [continueLoading, setContinueLoading] = useState(true);
  // Tracks the initial batch of page-wide fetches (programs, capabilities,
  // goals, motivations, human state) so we can show a clear "in progress"
  // modal instead of leaving the user staring at an empty/stale page while
  // several requests are still in flight.
  const [initialLoading, setInitialLoading] = useState(true);

  useEffect(() => {
    if (!personId) return;
    let cancelled = false;

    Promise.allSettled([
      getEnrolledPrograms(personId).then((data) => {
        if (!cancelled) setPrograms(data);
      }),
      getEnrolledCapabilities(personId).then((data) => {
        if (!cancelled) setStandaloneCapabilities(data);
      }),
      getPersonGoals(personId, i18n.language).then((goals) => {
        if (cancelled) return;
        const active = goals.find((g) => g.status === 'Active');
        setPersonalGoal(active ? { id: active.personGoalId, title: toLocalizedText(active.goalName) } : null);
      }),
      getPersonMotivations(personId, i18n.language).then((data) => {
        if (!cancelled) setMotivations(data.map((m) => toLocalizedText(m.motivationName)));
      }),
      getHumanStateToday(personId).then((data) => {
        if (!cancelled) setHumanState(data);
      }),
    ]).finally(() => {
      if (!cancelled) setInitialLoading(false);
    });

    return () => {
      cancelled = true;
    };
  }, [personId, i18n.language]);

  // "Continue Learning" — a real in-progress LearningSession per enrolled
  // capability (RuntimeGetActiveSessionFunction), never a fabricated
  // recommendation. Checked once enrolled programs are known.
  useEffect(() => {
    if (!personId || (programs.length === 0 && standaloneCapabilities.length === 0)) {
      setContinueLoading(false);
      return;
    }

    let cancelled = false;
    setContinueLoading(true);

    const programChecks = programs.flatMap((program) =>
      program.capabilities.map(async (cap) => {
        const active = await getActiveSession(personId, cap.capabilityId, program.programId).catch(() => null);
        if (!active) return null;
        const item: ContinueLearningItem = {
          capabilityId: cap.capabilityId,
          capabilityGraphNodeId: active.CapabilityGraphNodeId,
          capabilityName: toLocalizedText(cap.name),
          programName: toLocalizedText(program.name),
          programId: program.programId,
        };
        return item;
      }),
    );

    // Standalone (no Program) enrolled capabilities — same active-session
    // check, just without a programId, so it only matches sessions started
    // from "Mis Capacidades" rather than from within a Program.
    const standaloneChecks = standaloneCapabilities.map(async (cap) => {
      const active = await getActiveSession(personId, cap.capabilityId).catch(() => null);
      if (!active) return null;
      const item: ContinueLearningItem = {
        capabilityId: cap.capabilityId,
        capabilityGraphNodeId: active.CapabilityGraphNodeId,
        capabilityName: toLocalizedText(cap.name),
        programName: toLocalizedText(cap.subjectName ?? t('capabilities.standalone')),
      };
      return item;
    });

    Promise.all([...programChecks, ...standaloneChecks]).then((results) => {
      if (cancelled) return;
      setContinueItems(results.filter((r): r is ContinueLearningItem => r !== null));
      setContinueLoading(false);
    });

    return () => {
      cancelled = true;
    };
  }, [personId, programs, standaloneCapabilities, t]);

  const capabilities: CapabilityProgress[] = useMemo(
    () => [
      ...programs.flatMap((program) =>
        program.capabilities.map((cap) => ({
          id: cap.capabilityId,
          name: toLocalizedText(cap.name),
          progress: cap.totalNodes > 0 ? Math.round((cap.masteredNodes / cap.totalNodes) * 100) : 0,
          level: scoreToLevel(cap.averageScore),
          supports: [toLocalizedText(program.name)],
        })),
      ),
      ...standaloneCapabilities.map((cap) => ({
        id: cap.capabilityId,
        name: toLocalizedText(cap.name),
        progress: cap.totalNodes > 0 ? Math.round((cap.masteredNodes / cap.totalNodes) * 100) : 0,
        level: scoreToLevel(cap.averageScore),
        supports: [toLocalizedText(cap.subjectName ?? t('capabilities.standalone'))],
      })),
    ],
    [programs, standaloneCapabilities, t],
  );

  const progressStats = useMemo(() => {
    const allCapabilities = programs.flatMap((p) => p.capabilities);
    const totalNodes = allCapabilities.reduce((sum, c) => sum + c.totalNodes, 0) + standaloneCapabilities.reduce((sum, c) => sum + c.totalNodes, 0);
    const masteredNodes = allCapabilities.reduce((sum, c) => sum + c.masteredNodes, 0) + standaloneCapabilities.reduce((sum, c) => sum + c.masteredNodes, 0);
    const scores = [
      ...programs.map((p) => p.averageScore),
      ...standaloneCapabilities.map((c) => c.averageScore),
    ].filter((s): s is number => s !== undefined && s !== null);
    const averageScore = scores.length > 0 ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length) : undefined;
    return {
      programCount: programs.length,
      capabilityCount: standaloneCapabilities.length,
      masteredNodes,
      totalNodes,
      averageScore,
    };
  }, [programs, standaloneCapabilities]);

  const humanStateMetrics: HumanStateMetric[] = humanState?.hasCheckedInToday
    ? [
        { key: 'focus', value: humanState.focus ?? 0 },
        { key: 'energy', value: humanState.energy ?? 0 },
        { key: 'purpose', value: humanState.purpose ?? 0 },
        { key: 'confidence', value: humanState.confidence ?? 0 },
      ]
    : [];

  async function handleCheckInSubmit(values: UpsertHumanStateRequest) {
    if (!personId) return;
    setSavingCheckIn(true);
    try {
      const result = await upsertHumanState(personId, values);
      setHumanState(result);
    } finally {
      setSavingCheckIn(false);
    }
  }

  const displayName = user?.name?.split(' ')[0] ?? '';

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#05060a]">
      {initialLoading && (
        <LoadingBackdrop title={t('todayPage.loadingTitle')} description={t('todayPage.loadingDescription')} />
      )}
      <main>
        <div className="mx-auto max-w-6xl px-6 pt-8">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1 text-xs font-medium text-slate-500 dark:border-white/10 dark:bg-white/5 dark:text-white/60">
            <Sparkles className="h-3.5 w-3.5 text-blue-500 dark:text-blue-400" />
            {t('todayPage.tagline')}
          </span>
          <p className="mt-3 text-2xl font-semibold text-slate-900 dark:text-white">
            {t(`greeting.${greetingKey}`, { name: displayName })}
          </p>
        </div>

        {/* Section 1 — real progress stats across all enrolled programs
         *  (same numbers already shown on /programs), never an invented
         *  "layer" taxonomy. */}
        <ProgressOverview
          programCount={progressStats.programCount}
          capabilityCount={progressStats.capabilityCount}
          masteredNodes={progressStats.masteredNodes}
          totalNodes={progressStats.totalNodes}
          averageScore={progressStats.averageScore}
        />

        {/* Section 2 — purpose: why growth matters. Real goal/motivations from
         *  the Growth Plan when the person has any; otherwise a real empty
         *  state pointing at the wizard, rather than invented content. */}
        {personalGoal ? (
          <GrowthAlignment
            futureSelf={personalGoal.title}
            motivations={motivations}
            personalGoal={personalGoal}
            organizationInitiative={null}
            sharedCapabilities={[]}
          />
        ) : (
          <section className="mx-auto max-w-6xl px-6 pt-10">
            <Card className="flex flex-wrap items-center justify-between gap-4 p-6">
              <div className="flex items-center gap-3">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-500/10 text-blue-600 dark:text-blue-300">
                  <Compass className="h-5 w-5" />
                </div>
                <div>
                  <p className="font-medium text-slate-900 dark:text-white">{t('alignment.sectionTitle')}</p>
                  <p className="text-sm text-slate-500 dark:text-white/50">{t('alignment.helpsYouMessage')}</p>
                </div>
              </div>
              <Link
                to="/growth-plan/future-direction"
                className="rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
              >
                {t('growthPlan.overview.steps.futureDirection.title')}
              </Link>
            </Card>
          </section>
        )}

        {/* Section 3 — the concrete substance of growth, tied back to purpose. */}
        <CapabilitiesSection capabilities={capabilities} />

        {/* Section 4 — the only "do something today" surface: a real
         *  in-progress session to resume, or an honest empty state. */}
        <ContinueLearningSection items={continueItems} loading={continueLoading} />

        {/* Section 5 — compact, Apple-Health-style human condition check-in. */}
        <HumanStateSection
          metrics={humanStateMetrics}
          hasCheckedInToday={humanState?.hasCheckedInToday ?? false}
          streak={humanState?.streak ?? 0}
          saving={savingCheckIn}
          onSubmit={handleCheckInSubmit}
        />
      </main>
    </div>
  );
}


