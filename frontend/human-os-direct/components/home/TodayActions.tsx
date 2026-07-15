"use client";

import { Circle, CircleCheck } from "lucide-react";
import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { mockTodayActions } from "@/lib/mock-data";

const domainDot: Record<string, string> = {
  mind: "text-mind",
  build: "text-build",
  home: "text-home",
  life: "text-life",
  value: "text-value",
  future: "text-future",
};

/** The most important section on the "Today" screen: what to do right now. */
export function TodayActions() {
  const { locale } = useLanguage();

  return (
    <section className="rounded-2xl border border-border bg-card p-6 shadow-subtle">
      <h3 className="text-lg font-semibold tracking-tight">
        {locale === "en" ? "Today's actions" : "Acciones de hoy"}
      </h3>
      <ul className="mt-4 space-y-2">
        {mockTodayActions.map((action) => (
          <li
            key={action.id}
            className="flex items-center gap-3 rounded-2xl px-3 py-2 hover:bg-accent"
          >
            {action.isComplete ? (
              <CircleCheck className={`size-4 ${domainDot[action.domain]}`} aria-hidden />
            ) : (
              <Circle className={`size-4 ${domainDot[action.domain]}`} aria-hidden />
            )}
            <span
              className={action.isComplete ? "text-sm text-muted-foreground line-through" : "text-sm"}
            >
              {action.title}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
