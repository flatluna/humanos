"use client";

import { useLanguage } from "@/lib/i18n/LanguageProvider";
import type { PersonCapability } from "@/lib/mock-goals";

interface GoalCapabilityListProps {
  capabilityIds: string[];
  capabilities: PersonCapability[];
  maxVisible?: number;
}

export function GoalCapabilityList({
  capabilityIds,
  capabilities,
  maxVisible = 2,
}: GoalCapabilityListProps) {
  const { t } = useLanguage();

  const capabilityMap = Object.fromEntries(
    capabilities.map((c) => [c.capabilityId, c])
  );

  const visibleCaps = capabilityIds
    .slice(0, maxVisible)
    .map((id) => capabilityMap[id])
    .filter(Boolean);

  const hiddenCount = capabilityIds.length - maxVisible;

  return (
    <div className="space-y-2">
      <p className="text-xs font-semibold text-gray-600 dark:text-gray-400 uppercase tracking-wide">
        {t.goals?.capabilityList?.title || "Capacidades que te llevan ahí:"}
      </p>

      {visibleCaps.map((cap) => (
        <div key={cap.id} className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2 flex-1">
            <span className="text-lg">🧩</span>
            <span className="text-sm font-medium text-gray-900 dark:text-white">
              {cap.capabilityNameKey ? getCapabilityName(cap.capabilityNameKey, t) : "Capability"}
            </span>
          </div>
          <div className="flex items-center gap-2">
            <div className="w-16 h-1.5 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
              <div
                className="h-full bg-gradient-to-r from-blue-500 to-purple-500 transition-all"
                style={{ width: `${cap.progressPercentage}%` }}
              />
            </div>
            <span className="text-xs font-semibold text-gray-700 dark:text-gray-300 w-7 text-right">
              {cap.progressPercentage}%
            </span>
          </div>
        </div>
      ))}

      {hiddenCount > 0 && (
        <div className="text-xs text-gray-500 dark:text-gray-400 pt-1">
          {t.goals?.capabilityList?.more || `y ${hiddenCount} más...`}
        </div>
      )}
    </div>
  );
}

/**
 * Helper to translate capability name from i18n key
 */
function getCapabilityName(key: string, t: any): string {
  // Split key like "capabilities.mock.financial" into parts
  const parts = key.split(".");
  let value: any = t;
  for (const part of parts) {
    value = value?.[part];
    if (typeof value !== "object" && typeof value !== "string") break;
  }
  return typeof value === "string" ? value : key;
}
