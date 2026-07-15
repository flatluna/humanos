"use client";

import { useLanguage } from "@/lib/i18n/LanguageProvider";

/** The largest, most prominent element on the "Today" screen. */
export function EvolutionHero() {
  const { locale } = useLanguage();

  return (
    <section className="rounded-2xl border border-border bg-card p-8 shadow-subtle md:p-12">
      <p className="text-sm font-medium text-muted-foreground">
        {locale === "en" ? "Your evolution" : "Tu evolución"}
      </p>
      <h2 className="mt-2 text-3xl font-semibold tracking-tight md:text-4xl">
        {locale === "en" ? "You are growing steadily." : "Estás creciendo de forma constante."}
      </h2>
    </section>
  );
}
