"use client";

import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { CheckCircle2, Target, TrendingUp } from "lucide-react";
import type { Goal, PersonCapability } from "@/lib/mock-goals";

interface GoalsStatsProps {
  goals: Goal[];
  goalCapabilities: Record<string, string[]>;
  capabilities: PersonCapability[];
}

export function GoalsStats({
  goals,
  goalCapabilities,
  capabilities,
}: GoalsStatsProps) {
  const { t } = useLanguage();

  const activeGoals = goals.filter((g) => !g.isAchieved).length;
  const achievedGoals = goals.filter((g) => g.isAchieved).length;

  // Calculate average progress across all active goals
  const avgProgress = calculateAverageProgress(
    goals.filter((g) => !g.isAchieved),
    goalCapabilities,
    capabilities
  );

  return (
    <div className="grid grid-cols-3 gap-4 mb-8 p-4 bg-gray-50 dark:bg-gray-900 rounded-lg">
      {/* Active Goals */}
      <div className="flex items-center gap-3">
        <Target className="text-blue-600 dark:text-blue-400" size={24} />
        <div>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {t.goals?.stats?.active || "Activas"}
          </p>
          <p className="text-2xl font-bold text-gray-900 dark:text-white">
            {activeGoals}
          </p>
        </div>
      </div>

      {/* Achieved Goals */}
      <div className="flex items-center gap-3">
        <CheckCircle2 className="text-green-600 dark:text-green-400" size={24} />
        <div>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {t.goals?.stats?.achieved || "Logradas"}
          </p>
          <p className="text-2xl font-bold text-gray-900 dark:text-white">
            {achievedGoals}
          </p>
        </div>
      </div>

      {/* Average Progress */}
      <div className="flex items-center gap-3">
        <TrendingUp className="text-purple-600 dark:text-purple-400" size={24} />
        <div>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {t.goals?.stats?.average || "Promedio"}
          </p>
          <p className="text-2xl font-bold text-gray-900 dark:text-white">
            {Math.round(avgProgress)}%
          </p>
        </div>
      </div>
    </div>
  );
}

/**
 * Calculate average progress of active goals.
 * Each goal's progress = average of its connected capabilities' progress.
 */
function calculateAverageProgress(
  activeGoals: Goal[],
  goalCapabilities: Record<string, string[]>,
  capabilities: PersonCapability[]
): number {
  const capabilityMap = Object.fromEntries(
    capabilities.map((c) => [c.capabilityId, c.progressPercentage])
  );

  const goalProgresses = activeGoals.map((goal) => {
    const capIds = goalCapabilities[goal.id] || [];
    const capProgresses = capIds
      .map((capId) => capabilityMap[capId] || 0)
      .filter((p) => p > 0);

    return capProgresses.length > 0
      ? capProgresses.reduce((a, b) => a + b, 0) / capProgresses.length
      : 0;
  });

  return goalProgresses.length > 0
    ? goalProgresses.reduce((a, b) => a + b, 0) / goalProgresses.length
    : 0;
}
