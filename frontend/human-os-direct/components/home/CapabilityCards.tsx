"use client";

import { useLanguage } from "@/lib/i18n/LanguageProvider";
import { mockCapabilities } from "@/lib/mock-data";

const domainBar: Record<string, string> = {
  mind: "bg-mind",
  build: "bg-build",
  home: "bg-home",
  life: "bg-life",
  value: "bg-value",
  future: "bg-future",
};

export function CapabilityCards() {
  const { locale } = useLanguage();

  return (
    <section className="rounded-2xl border border-border bg-card p-6 shadow-subtle">
      <h3 className="text-lg font-semibold tracking-tight">
        {locale === "en" ? "Capabilities" : "Capacidades"}
      </h3>
      <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
        {mockCapabilities.map((capability) => (
          <div
            key={capability.id}
            className="rounded-2xl border border-border p-4"
          >
            <p className="text-sm font-medium">{capability.name}</p>
            <div className="mt-2 h-1.5 w-full overflow-hidden rounded-full bg-muted">
              <div
                className={`h-full rounded-full ${domainBar[capability.domain]}`}
                style={{ width: `${capability.level}%` }}
              />
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
