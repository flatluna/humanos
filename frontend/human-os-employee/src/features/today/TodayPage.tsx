import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import { getEnrolledCapabilities, type EnrolledCapability } from '@/features/capabilities/api/enrolledCapabilitiesApi';
import { getActiveSession } from '@/features/capabilities/api/runtimeSessionApi';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { ProgressOverview } from './components/ProgressOverview';
import { CapabilitiesSection } from './components/CapabilitiesSection';
import { ContinueLearningSection, type ContinueLearningItem } from './components/ContinueLearningSection';
import type { CapabilityLevel, CapabilityProgress } from './types';

type GreetingKey = 'morning' | 'afternoon' | 'evening';

function useGreetingKey(): GreetingKey {
  const hour = new Date().getHours();
  if (hour < 12) return 'morning';
  if (hour < 18) return 'afternoon';
  return 'evening';
}

const GREETINGS: Record<GreetingKey, string> = {
  morning: 'Buenos días',
  afternoon: 'Buenas tardes',
  evening: 'Buenas noches',
};

/** Courses don't have a "mastery" flag — level is derived from the average
 *  score of nodes already mastered so far in that capability. */
function scoreToLevel(averageScore: number | null | undefined): CapabilityLevel {
  if (averageScore === null || averageScore === undefined) return 'none';
  if (averageScore < 60) return 'basico';
  if (averageScore < 85) return 'intermedio';
  return 'avanzado';
}

/** Employee portal dashboard ("Hoy") — ported from human-os-web's /today
 *  (2026-08-01), trimmed to what makes sense for a company employee: real
 *  progress across the courses their company assigned them, plus a
 *  "Continue Learning" resume list. No personal-goals/growth-plan or
 *  human-state check-in sections here — those are consumer-app-only
 *  features this portal never adopted (v1 scope is just the assigned
 *  learning runtime, see EmployeeSidebar's doc comment). */
export function TodayPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const greetingKey = useGreetingKey();

  const [capabilities, setCapabilities] = useState<EnrolledCapability[]>([]);
  const [continueItems, setContinueItems] = useState<ContinueLearningItem[]>([]);
  const [continueLoading, setContinueLoading] = useState(true);
  const [initialLoading, setInitialLoading] = useState(true);

  useEffect(() => {
    if (!personId) return;
    let cancelled = false;

    getEnrolledCapabilities(personId)
      .then((data) => {
        if (!cancelled) setCapabilities(data);
      })
      .finally(() => {
        if (!cancelled) setInitialLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [personId]);

  // "Continue Learning" — a real in-progress LearningSession per assigned
  // capability (RuntimeGetActiveSessionFunction), never a fabricated
  // recommendation.
  useEffect(() => {
    if (!personId || capabilities.length === 0) {
      setContinueLoading(false);
      return;
    }

    let cancelled = false;
    setContinueLoading(true);

    Promise.all(
      capabilities.map(async (cap) => {
        const active = await getActiveSession(personId, cap.capabilityId).catch(() => null);
        if (!active) return null;
        const item: ContinueLearningItem = {
          capabilityId: cap.capabilityId,
          capabilityGraphNodeId: active.CapabilityGraphNodeId,
          capabilityName: cap.name,
          subjectName: cap.subjectName ?? 'Independiente',
        };
        return item;
      }),
    ).then((results) => {
      if (cancelled) return;
      setContinueItems(results.filter((r): r is ContinueLearningItem => r !== null));
      setContinueLoading(false);
    });

    return () => {
      cancelled = true;
    };
  }, [personId, capabilities]);

  const capabilityProgress: CapabilityProgress[] = useMemo(
    () =>
      capabilities.map((cap) => ({
        id: cap.capabilityId,
        name: cap.name,
        progress: cap.totalNodes > 0 ? Math.round((cap.masteredNodes / cap.totalNodes) * 100) : 0,
        level: scoreToLevel(cap.averageScore),
        supports: [cap.subjectName ?? 'Independiente'],
      })),
    [capabilities],
  );

  const progressStats = useMemo(() => {
    const totalNodes = capabilities.reduce((sum, c) => sum + c.totalNodes, 0);
    const masteredNodes = capabilities.reduce((sum, c) => sum + c.masteredNodes, 0);
    const scores = capabilities.map((c) => c.averageScore).filter((s): s is number => s !== undefined && s !== null);
    const averageScore = scores.length > 0 ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length) : undefined;
    return {
      capabilityCount: capabilities.length,
      masteredNodes,
      totalNodes,
      averageScore,
    };
  }, [capabilities]);

  const displayName = user?.name?.split(' ')[0] ?? '';

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#05060a]">
      {initialLoading && (
        <LoadingBackdrop title="Preparando tu panel..." description="Cargando tu progreso y cursos asignados." />
      )}
      <main>
        <div className="mx-auto max-w-6xl px-6 pt-8">
          <p className="text-2xl font-semibold text-slate-900 dark:text-white">
            {GREETINGS[greetingKey]}{displayName ? `, ${displayName}` : ''}
          </p>
        </div>

        <ProgressOverview
          capabilityCount={progressStats.capabilityCount}
          masteredNodes={progressStats.masteredNodes}
          totalNodes={progressStats.totalNodes}
          averageScore={progressStats.averageScore}
        />

        <CapabilitiesSection capabilities={capabilityProgress} />

        <ContinueLearningSection items={continueItems} loading={continueLoading} />
      </main>
    </div>
  );
}
