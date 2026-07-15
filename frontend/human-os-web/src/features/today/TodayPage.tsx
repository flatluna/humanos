import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { EvolutionJourney } from './components/EvolutionJourney';
import { CurrentLayerHero } from './components/CurrentLayerHero';
import { GrowthAlignment } from './components/GrowthAlignment';
import { CapabilitiesSection } from './components/CapabilitiesSection';
import { TodayActionsSection } from './components/TodayActionsSection';
import { HumanStateSection } from './components/HumanStateSection';
import { todaySnapshot } from './data/mockTodayData';
import { EVOLUTION_LAYER_ORDER } from './types';

type GreetingKey = 'morning' | 'afternoon' | 'evening';

function useGreetingKey(): GreetingKey {
  const hour = new Date().getHours();
  if (hour < 12) return 'morning';
  if (hour < 18) return 'afternoon';
  return 'evening';
}

export function TodayPage() {
  const { t } = useTranslation();
  const greetingKey = useGreetingKey();

  const nextLayer = useMemo(() => {
    const currentIndex = EVOLUTION_LAYER_ORDER.indexOf(todaySnapshot.currentLayer);
    const nextIndex = Math.min(currentIndex + 1, EVOLUTION_LAYER_ORDER.length - 1);
    return EVOLUTION_LAYER_ORDER[nextIndex];
  }, []);

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#05060a]">
      <main>
        <div className="mx-auto max-w-6xl px-6 pt-8">
          <p className="text-2xl font-semibold text-slate-900 dark:text-white">
            {t(`greeting.${greetingKey}`, { name: todaySnapshot.userName })}
          </p>
        </div>

        {/* Section 1 — context before identity: the full journey comes first. */}
        <EvolutionJourney currentLayer={todaySnapshot.currentLayer} />

        {/* Section 2 — "You are here," plus the emotional pull of Future Self. */}
        <CurrentLayerHero
          currentLayer={todaySnapshot.currentLayer}
          nextLayer={nextLayer}
          progress={todaySnapshot.progressTowardNextLayer}
          futureSelf={todaySnapshot.futureSelf}
        />

        {/* Section 3 — purpose: why growth matters, to the person and (optionally) their org. */}
        <GrowthAlignment
          futureSelf={todaySnapshot.futureSelf}
          motivations={todaySnapshot.motivations}
          personalGoal={todaySnapshot.personalGoal}
          organizationInitiative={todaySnapshot.organizationInitiative}
          sharedCapabilities={todaySnapshot.sharedCapabilities}
        />

        {/* Section 4 — the concrete substance of growth, tied back to purpose. */}
        <CapabilitiesSection capabilities={todaySnapshot.capabilities} />

        {/* Section 5 — the only "do something today" surface. */}
        <TodayActionsSection actions={todaySnapshot.actions} />

        {/* Section 6 — compact, Apple-Health-style human condition check-in. */}
        <HumanStateSection metrics={todaySnapshot.humanState} />
      </main>
    </div>
  );
}
