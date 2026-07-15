"use client";

import { useLanguage } from "@/lib/i18n/LanguageProvider";

type PageKey = "universe" | "capabilities" | "portfolio" | "goals" | "agents" | "evolution";

export function PageStub({ headingKey }: { headingKey?: PageKey }) {
  const { t } = useLanguage();

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-16">
      <div className="rounded-3xl border border-dashed border-border px-10 py-12 text-center shadow-subtle">
        {headingKey && (
          <p className="mb-2 text-sm font-medium text-muted-foreground">
            {t.page[headingKey]}
          </p>
        )}
        <p className="text-lg font-semibold text-foreground">
          {t.page.placeholder}
        </p>
      </div>
    </div>
  );
}
