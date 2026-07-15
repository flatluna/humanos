"use client";

import { Sparkles } from "lucide-react";
import { useLanguage } from "@/lib/i18n/LanguageProvider";

/** Contextual nudge/support from the growth agent. */
export function AgentSupport() {
  const { locale } = useLanguage();

  return (
    <section className="rounded-2xl border border-border bg-card p-6 shadow-subtle">
      <div className="flex items-center gap-2">
        <Sparkles className="size-4 text-future" aria-hidden />
        <h3 className="text-lg font-semibold tracking-tight">
          {locale === "en" ? "Agent support" : "Apoyo del agente"}
        </h3>
      </div>
      <p className="mt-2 text-sm text-muted-foreground">
        {locale === "en"
          ? "Your growth agent's suggestions will appear here."
          : "Aquí aparecerán las sugerencias de tu agente de crecimiento."}
      </p>
    </section>
  );
}
