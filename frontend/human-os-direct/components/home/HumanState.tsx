"use client";

import { useLanguage } from "@/lib/i18n/LanguageProvider";

/** Snapshot of the human's current state (energy, mood, focus, etc.). */
export function HumanState() {
  const { locale } = useLanguage();

  return (
    <section className="rounded-2xl border border-border bg-card p-6 shadow-subtle">
      <h3 className="text-lg font-semibold tracking-tight">
        {locale === "en" ? "Your state" : "Tu estado"}
      </h3>
      <p className="mt-2 text-sm text-muted-foreground">
        {locale === "en"
          ? "Energy, mood, and focus will appear here."
          : "Aquí aparecerán tu energía, ánimo y enfoque."}
      </p>
    </section>
  );
}
