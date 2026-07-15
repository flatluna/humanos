"use client";

import { useState } from "react";
import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { ChevronDown, CheckCircle2 } from "lucide-react";
import type { Goal } from "@/lib/mock-goals";

interface AchievedGoalsProps {
  goals: Goal[];
}

export function AchievedGoals({ goals }: AchievedGoalsProps) {
  const { t } = useLanguage();
  const [isOpen, setIsOpen] = useState(false);

  const achievedGoals = goals.filter((g) => g.isAchieved);

  if (achievedGoals.length === 0) return null;

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
    <div className="border border-gray-200 dark:border-gray-700 rounded-lg bg-white dark:bg-gray-800 overflow-hidden">
      {/* Header */}
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="w-full flex items-center justify-between p-4 hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors"
      >
        <div className="flex items-center gap-3">
          <CheckCircle2 className="text-green-600 dark:text-green-400" size={20} />
          <span className="font-semibold text-gray-900 dark:text-white">
            {t.goals?.achieved?.title || "Metas Logradas"}
          </span>
          <span className="text-xs bg-green-100 dark:bg-green-900 text-green-700 dark:text-green-300 px-2 py-1 rounded-full font-medium">
            {achievedGoals.length}
          </span>
        </div>
        <ChevronDown
          size={20}
          className={`text-gray-600 dark:text-gray-400 transition-transform ${
            isOpen ? "rotate-180" : ""
          }`}
        />
      </button>

      {/* Content */}
      {isOpen && (
        <div className="border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900">
          <ul className="divide-y divide-gray-200 dark:divide-gray-700">
            {achievedGoals.map((goal) => (
              <li
                key={goal.id}
                className="p-4 flex items-start gap-3 hover:bg-gray-100 dark:hover:bg-gray-800 transition-colors"
              >
                <CheckCircle2 className="text-green-600 dark:text-green-400 mt-1 flex-shrink-0" size={18} />
                <div className="flex-1">
                  <p className="font-medium text-gray-900 dark:text-white">
                    ✅ {getTranslation(goal.titleKey)}
                  </p>
                  {goal.descriptionKey && (
                    <p className="text-sm text-gray-600 dark:text-gray-400 mt-1">
                      {getTranslation(goal.descriptionKey)}
                    </p>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
