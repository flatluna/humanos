"use client";

import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { Plus } from "lucide-react";

export function GoalsHeader() {
  const { t } = useLanguage();

  return (
    <div className="mb-8">
      <h1 className="text-3xl font-bold text-gray-900 dark:text-white mb-2">
        {t.page?.goals || "My Goals"}
      </h1>
      <p className="text-gray-600 dark:text-gray-400 mb-6">
        {t.goals?.header?.subtitle || "Hacia dónde estás creciendo"}
      </p>
      
      <button className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium transition-colors">
        <Plus size={20} />
        {t.goals?.header?.newGoal || "Nueva meta"}
      </button>
    </div>
  );
}
