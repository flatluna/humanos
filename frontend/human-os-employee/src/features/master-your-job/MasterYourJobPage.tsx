import { useMemo, useState } from 'react';
import { ArrowLeft, BookOpen, GraduationCap, RefreshCw, Sparkles, type LucideIcon } from 'lucide-react';
import { Link } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { useKnowledgeNetwork } from '@/features/brain/useKnowledgeNetwork';
import { EngramDetailPanel } from '@/features/brain/EngramDetailPanel';
import type { SelectedNeuron } from '@/features/brain/types';
import { getMemoryColor, getMemoryTextColor, getRetrievability, isAutomatic } from '@/features/brain/memoryScience';

/** Prefers the leading section number ("1.1", "3.2") so the badge tells you
 * where you are in the course; falls back to the first letter for titles
 * without one — same convention as BrainRecallGrid's own node badges. */
function nodeBadge(name: string): string {
  const numberMatch = name.match(/^\d+(\.\d+)?/);
  if (numberMatch) return numberMatch[0];
  return name.charAt(0).toUpperCase();
}

/** An engram counts as "stale" once it's been this many days since it was
 * last mastered/reviewed — surfaces these so the learner can proactively
 * refresh them instead of waiting for the forgetting curve. */
const STALE_ENGRAM_DAYS = 90;

export function MasterYourJobPage() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';
  const [openNeuron, setOpenNeuron] = useState<SelectedNeuron | null>(null);

  const knowledgeNetworkQuery = useKnowledgeNetwork(personId);

  const staleEngrams = useMemo(() => {
    const cutoff = Date.now() - STALE_ENGRAM_DAYS * 24 * 60 * 60 * 1000;
    return (knowledgeNetworkQuery.data?.masteredEngrams ?? [])
      .filter((engram) => engram.completedDate && new Date(engram.completedDate).getTime() <= cutoff)
      .sort((a, b) => new Date(a.completedDate!).getTime() - new Date(b.completedDate!).getTime());
  }, [knowledgeNetworkQuery.data]);

  const metrics: { label: string; value: string; icon: LucideIcon }[] = [
    {
      label: isEn ? 'Engrams +90 days without review' : 'Engramas con +90 días sin repasar',
      value: String(staleEngrams.length),
      icon: RefreshCw,
    },
    {
      label: isEn ? 'Total mastered engrams' : 'Engramas dominados totales',
      value: String(knowledgeNetworkQuery.data?.masteredEngrams?.length ?? 0),
      icon: BookOpen,
    },
  ];

  return (
    <div className="min-h-screen bg-[#f5f7fb] pb-16 dark:bg-[#05060a]">
      <main className="mx-auto max-w-6xl px-6 pt-8 sm:pt-12">
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300"
        >
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          {isEn ? 'Back to your journey' : 'Volver a tu recorrido'}
        </Link>

        {/* Hero Banner */}
        <section className="mt-6 overflow-hidden rounded-3xl bg-gradient-to-br from-cyan-600 via-teal-600 to-blue-700 p-7 text-white shadow-xl shadow-cyan-900/20 sm:p-10">
          <div className="flex flex-wrap items-start justify-between gap-6">
            <div className="max-w-3xl">
              <div className="flex items-center gap-2 text-sm font-semibold text-cyan-100">
                <Sparkles className="h-4 w-4" aria-hidden="true" />
                {isEn ? 'Seniority & Mastery' : 'Madurez y Dominio'}
              </div>
              <h1 className="mt-4 text-3xl font-bold tracking-tight sm:text-5xl">
                {isEn ? 'Master your job' : 'Domina tu puesto'}
              </h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-cyan-50 sm:text-lg">
                {isEn
                  ? 'Keep your mastered engrams sharp. Review engrams that have gone over 90 days without practice to prevent forgetting.'
                  : 'Mantén frescos tus engramas dominados. Repasa los conceptos que llevan más de 90 días sin práctica para evitar la curva del olvido.'}
              </p>
              <div className="mt-4">
                <span className="inline-flex rounded-full bg-white/15 px-3 py-1 text-xs font-semibold text-white backdrop-blur">
                  {isEn ? 'Retention & Spaced Practice' : 'Retención y Práctica Espaciada'}
                </span>
              </div>
            </div>
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
              <GraduationCap className="h-8 w-8 text-white" aria-hidden="true" />
            </div>
          </div>
        </section>

        {/* Metrics Row */}
        <section className="mt-6 grid gap-4 sm:grid-cols-2">
          {metrics.map(({ label, value, icon: MetricIcon }) => (
            <Card key={String(label)} className="border-slate-200/80 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
              <MetricIcon className="h-5 w-5 text-cyan-600 dark:text-cyan-300" aria-hidden="true" />
              <p className="mt-4 text-xs font-semibold uppercase tracking-[0.16em] text-slate-400 dark:text-white/45">{label}</p>
              <p className="mt-2 text-lg font-bold text-slate-950 dark:text-white">{value}</p>
            </Card>
          ))}
        </section>

        {/* Stay Fresh / +90 Days Engrams Section */}
        <section
          className="mt-6 flex flex-col overflow-hidden rounded-2xl border border-cyan-200/70 bg-white shadow-sm shadow-cyan-900/5 dark:border-cyan-400/20 dark:bg-white/[0.04]"
          aria-labelledby="stay-fresh-title"
        >
          <div className="flex items-center gap-3 border-b border-cyan-100 bg-cyan-50/60 px-5 py-4 dark:border-white/5 dark:bg-cyan-500/10">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-cyan-600 text-white shadow-sm">
              <RefreshCw className="h-4.5 w-4.5" aria-hidden="true" />
            </span>
            <div className="min-w-0">
              <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-cyan-700 dark:text-cyan-300">
                {isEn ? 'Recall your knowledge' : 'Repasa tu conocimiento'}
              </p>
              <h2 id="stay-fresh-title" className="truncate text-base font-bold text-slate-950 dark:text-white">
                {isEn ? 'Engrams without review in 90+ days' : 'Engramas con más de 90 días sin repasar'}
              </h2>
            </div>
          </div>

          <div className="p-6">
            {knowledgeNetworkQuery.isLoading ? (
              <div className="py-8 text-center">
                <RefreshCw className="mx-auto h-6 w-6 animate-spin text-cyan-600" />
                <p className="mt-3 text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn ? 'Checking your mastered engrams...' : 'Revisando tus engramas dominados...'}
                </p>
              </div>
            ) : staleEngrams.length === 0 ? (
              <div className="rounded-xl border border-dashed border-slate-200 p-8 text-center dark:border-white/10">
                <p className="text-sm font-medium text-slate-600 dark:text-white/70">
                  {isEn
                    ? `No engrams are ${STALE_ENGRAM_DAYS}+ days old without review — your knowledge is up to date!`
                    : `No tienes engramas con más de ${STALE_ENGRAM_DAYS} días sin repasar — ¡tu conocimiento está al día!`}
                </p>
              </div>
            ) : (
              <div className="flex flex-wrap gap-4">
                {staleEngrams.map((engram) => {
                  const retrievability = getRetrievability(engram.completedDate, engram.reviewCount ?? 1);
                  const memoryColor = getMemoryColor(retrievability);
                  const memoryTextColor = getMemoryTextColor(retrievability);
                  const percent = retrievability === null ? null : Math.round(retrievability * 100);
                  const isFading = percent !== null && percent < 30;
                  const automatic = isAutomatic(engram.reviewCount ?? 0);
                  return (
                    <button
                      key={engram.sectionId}
                      type="button"
                      onClick={() => setOpenNeuron(engram.neuron)}
                      title={engram.title}
                      className="group flex w-20 flex-col items-center gap-1.5 text-center transition hover:-translate-y-0.5 focus:outline-none"
                    >
                      <span
                        className={`flex h-11 w-11 items-center justify-center rounded-full border border-white/20 text-xs font-bold leading-none shadow-md transition group-hover:scale-105 ${
                          isFading ? 'animate-pulse' : ''
                        }`}
                        style={{ background: memoryColor, color: memoryTextColor }}
                      >
                        {nodeBadge(engram.title)}
                      </span>
                      {percent !== null && (
                        <span className="text-[11px] font-bold" style={{ color: automatic ? undefined : memoryColor }}>
                          {automatic ? 'Auto' : `${percent}%`}
                        </span>
                      )}
                      <span className="line-clamp-2 text-[10px] font-medium leading-tight text-slate-600 dark:text-white/60">
                        {engram.title}
                      </span>
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        </section>
      </main>

      {/* Engram Detail Modal */}
      {openNeuron && (
        <EngramDetailPanel
          personId={personId}
          neuron={openNeuron}
          onClose={() => setOpenNeuron(null)}
          onSelectNeuron={setOpenNeuron}
        />
      )}
    </div>
  );
}
