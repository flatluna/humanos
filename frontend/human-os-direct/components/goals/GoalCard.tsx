"use client";

import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { ChevronRight } from "lucide-react";
import { GoalCapabilityList } from "./GoalCapabilityList";
import type { Goal, PersonCapability } from "@/lib/mock-goals";

interface GoalCardProps {
  goal: Goal;
  capabilities: PersonCapability[];
  connectedCapabilityIds: string[];
}

export function GoalCard({
  goal,
  capabilities,
  connectedCapabilityIds,
}: GoalCardProps) {
  const { t } = useLanguage();

  // Calculate goal progress = average of connected capabilities
  const goalProgress = calculateGoalProgress(
    connectedCapabilityIds,
    capabilities
  );

  // Helper to translate i18n keys
  const getTranslation = (key: string | undefined): string => {
    if (!key) return "";
    const parts = key.split(".");
    let value: any = t;
    for (const part of parts) {
      value = value?.[part];
      if (typeof value !== "object" && typeof value !== "string") break;
    }
    return typeof value === "string" ? value : key;
  };

  return (
    <div className="border border-gray-200 dark:border-gray-700 rounded-lg p-6 bg-white dark:bg-gray-800 hover:shadow-md transition-shadow">
      {/* Goal Title */}
      <h3 className="text-lg font-semibold text-gray-900 dark:text-white mb-1">
        🎯 {getTranslation(goal.titleKey)}
      </h3>

      {goal.descriptionKey && (
        <p className="text-sm text-gray-600 dark:text-gray-400 mb-4">
          {getTranslation(goal.descriptionKey)}
        </p>
      )}

      {/* Progress Bar */}
      <div className="mb-4">
        <div className="flex items-center justify-between mb-2">
          <span className="text-xs font-medium text-gray-600 dark:text-gray-400">
            {t.goals?.card?.progress || "Progreso"}
          </span>
          <span className="text-sm font-bold text-gray-900 dark:text-white">
            {Math.round(goalProgress)}%
          </span>
        </div>
        <div className="w-full h-2 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
          <div
            className="h-full bg-gradient-to-r from-blue-500 to-purple-500 transition-all duration-500"
            style={{ width: `${goalProgress}%` }}
          />
        </div>
      </div>

      {/* Connected Capabilities */}
      <div className="mb-6 pb-6 border-b border-gray-200 dark:border-gray-700">
        <GoalCapabilityList
          capabilityIds={connectedCapabilityIds}
          capabilities={capabilities}
          maxVisible={2}
        />
      </div>

      {/* Detail Button */}
      <button className="flex items-center gap-2 text-blue-600 dark:text-blue-400 hover:text-blue-700 dark:hover:text-blue-300 font-medium text-sm transition-colors">
        {t.goals?.card?.details || "Ver detalle"}
        <ChevronRight size={16} />
      </button>
    </div>
  );
}

/**
 * Calculate goal progress = average of its connected capabilities' progress
 */
function calculateGoalProgress(
  capabilityIds: string[],
  capabilities: PersonCapability[]
): number {
  if (capabilityIds.length === 0) return 0;

  const capabilityMap = Object.fromEntries(
    capabilities.map((c) => [c.capabilityId, c.progressPercentage])
  );

  const progresses = capabilityIds
    .map((id) => capabilityMap[id] || 0)
    .filter((p) => p > 0);

  return progresses.length > 0
    ? progresses.reduce((a, b) => a + b, 0) / progresses.length
    : 0;
}
