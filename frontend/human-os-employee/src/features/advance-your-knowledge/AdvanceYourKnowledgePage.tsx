import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, BookOpen, ChevronRight, Clock3, RefreshCw, Sparkles, X, type LucideIcon } from 'lucide-react';
import { Link } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getSkills } from '@/features/capabilities/api/skillsApi';
import { getEnrolledCapabilities } from '@/features/capabilities/api/enrolledCapabilitiesApi';
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
 * last mastered/reviewed — "Stay Fresh" surfaces these so the learner can
 * proactively refresh them instead of waiting for the forgetting curve. */
const STALE_ENGRAM_DAYS = 90;

/** "Stay current" catalog entries (2026-09-20) — these are tenant Skill
 * rows (see human-os-enterprise's Skills catalog), matched by exact name,
 * same lookup pattern as InYourRolePage's requirement matching. Not every
 * name has a Skill row yet, and not every Skill row has course content
 * (matchedCapabilityId) attached yet — both cases render as
 * "Coming soon" instead of being silently dropped. */
const ADVANCE_KNOWLEDGE_SKILL_NAMES = [
  'Prompt Engineering',
  'Building Effective AI Agents (Anthropic)',
  'Claude Code 101',
  'Introduction to Agent Skills',
  'Introduction to Subagents',
  'Introduction to Model Context Protocol (MCP)',
  'Building with the Claude API',
];

export function AdvanceYourKnowledgePage() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';
  const [activeCapability, setActiveCapability] = useState<{ title: string; description: string; href?: string; available?: boolean; engramProgress?: { mastered: number; total: number } } | null>(null);
  const [openNeuron, setOpenNeuron] = useState<SelectedNeuron | null>(null);

  const skillsQuery = useQuery({
    queryKey: ['skills', user?.tenantId, 'advance-your-knowledge'],
    queryFn: () => getSkills(user!.tenantId!),
    enabled: Boolean(user?.tenantId),
  });
  const enrolledQuery = useQuery({
    queryKey: ['enrolled-capabilities', personId, 'advance-your-knowledge'],
    queryFn: () => getEnrolledCapabilities(personId),
    enabled: Boolean(personId),
  });
  const knowledgeNetworkQuery = useKnowledgeNetwork(personId);

  const staleEngrams = useMemo(() => {
    const cutoff = Date.now() - STALE_ENGRAM_DAYS * 24 * 60 * 60 * 1000;
    return (knowledgeNetworkQuery.data?.masteredEngrams ?? [])
      .filter((engram) => engram.completedDate && new Date(engram.completedDate).getTime() <= cutoff)
      .sort((a, b) => new Date(a.completedDate!).getTime() - new Date(b.completedDate!).getTime());
  }, [knowledgeNetworkQuery.data]);

  const capabilities = useMemo(() => {
    const enrolledByCapabilityId = new Map((enrolledQuery.data ?? []).map((item) => [item.capabilityId, item]));
    const skillsByLowerName = new Map((skillsQuery.data ?? []).map((skill) => [skill.name.trim().toLowerCase(), skill]));
    return ADVANCE_KNOWLEDGE_SKILL_NAMES.map((name) => {
      const skill = skillsByLowerName.get(name.trim().toLowerCase());
      const enrolled = skill?.matchedCapabilityId ? enrolledByCapabilityId.get(skill.matchedCapabilityId) : undefined;
      return {
        name,
        description: skill?.description ?? null,
        matchedCapabilityId: skill?.matchedCapabilityId ?? null,
        matchedCourseType: skill?.matchedCourseType ?? null,
        status: enrolled?.status,
        engramProgress: enrolled ? { mastered: enrolled.masteredNodes, total: enrolled.totalNodes } : undefined,
      };
    });
  }, [skillsQuery.data, enrolledQuery.data]);

  if (skillsQuery.isLoading) {
    return <LoadingBackdrop title={isEn ? 'Loading your knowledge path...' : 'Cargando tu ruta de conocimiento...'} description={isEn ? 'Loading capabilities to keep you current.' : 'Cargando capacidades para mantenerte vigente.'} />;
  }

  const metrics: { label: string; value: string; icon: LucideIcon }[] = [
    { label: isEn ? 'Capabilities available' : 'Capacidades disponibles', value: String(capabilities.length), icon: BookOpen },
  ];

  return (
    <div className="min-h-screen bg-[#f5f7fb] pb-16 dark:bg-[#05060a]">
      <main className="mx-auto max-w-6xl px-6 pt-8 sm:pt-12">
        <Link to="/" className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300">
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          {isEn ? 'Back to your journey' : 'Volver a tu recorrido'}
        </Link>

        <section className="mt-6 overflow-hidden rounded-3xl bg-gradient-to-br from-amber-500 via-orange-600 to-rose-600 p-7 text-white shadow-xl shadow-amber-900/20 sm:p-10">
          <div className="flex flex-wrap items-start justify-between gap-6">
            <div className="max-w-3xl">
              <div className="flex items-center gap-2 text-sm font-semibold text-amber-100">
                <Sparkles className="h-4 w-4" aria-hidden="true" />
                {isEn ? 'Always' : 'Siempre'}
              </div>
              <h1 className="mt-4 text-3xl font-bold tracking-tight sm:text-5xl">{isEn ? 'Advance Your Knowledge' : 'Avanza tu Conocimiento'}</h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-amber-50 sm:text-lg">
                {isEn
                  ? 'Update your capabilities as your role and industry evolve.'
                  : 'Actualiza tus capacidades conforme evolucionan tu puesto y tu industria.'}
              </p>
            </div>
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
              <RefreshCw className="h-8 w-8" aria-hidden="true" />
            </div>
          </div>
        </section>

        <section className="mt-6 grid gap-4 sm:grid-cols-2">
          {metrics.map(({ label, value, icon: MetricIcon }) => (
            <Card key={String(label)} className="border-slate-200/80 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
              <MetricIcon className="h-5 w-5 text-amber-600 dark:text-amber-300" aria-hidden="true" />
              <p className="mt-4 text-xs font-semibold uppercase tracking-[0.16em] text-slate-400 dark:text-white/45">{label}</p>
              <p className="mt-2 text-lg font-bold text-slate-950 dark:text-white">{value}</p>
            </Card>
          ))}
        </section>

        <div className="mt-6 grid gap-4 lg:grid-cols-2">
          <section className="flex flex-col overflow-hidden rounded-2xl border border-amber-200/70 bg-white shadow-sm shadow-amber-900/5 dark:border-amber-400/20 dark:bg-white/[0.04]" aria-labelledby="advance-knowledge-title">
            <div className="flex items-center gap-3 border-b border-amber-100 bg-amber-50/60 px-5 py-4 dark:border-white/5 dark:bg-amber-500/10">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-amber-600 text-white shadow-sm">
                <BookOpen className="h-4.5 w-4.5" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-amber-600 dark:text-amber-300">
                  {isEn ? 'Stay current' : 'Mantente vigente'}
                </p>
                <h2 id="advance-knowledge-title" className="truncate text-sm font-bold text-slate-950 dark:text-white">
                  {isEn ? 'Advance Your Knowledge' : 'Avanza tu Conocimiento'}
                </h2>
              </div>
            </div>
            <div className="flex-1 space-y-2 p-4">
              {capabilities.length === 0 ? (
                <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn ? 'These capabilities have not been published yet.' : 'Estas capacidades aún no han sido publicadas.'}
                </p>
              ) : (
                capabilities.map(({ name, description, matchedCapabilityId, matchedCourseType, status, engramProgress }) => {
                  const statusLabel = status === 'Completed'
                    ? (isEn ? 'Completed' : 'Completado')
                    : status === 'InProgress'
                      ? (isEn ? 'In progress' : 'En progreso')
                      : matchedCapabilityId
                        ? (isEn ? 'Available' : 'Disponible')
                        : (isEn ? 'Coming soon' : 'Próximamente');
                  const isDone = status === 'Completed';
                  const engramLabel = engramProgress
                    ? (isEn ? `${engramProgress.mastered}/${engramProgress.total} engrams` : `${engramProgress.mastered}/${engramProgress.total} engramas`)
                    : null;
                  return (
                    <button
                      key={name}
                      type="button"
                      onClick={() => setActiveCapability({
                        title: name,
                        description: description ?? (isEn
                          ? `Explore the key concepts covered in ${name}.`
                          : `Explora los conceptos clave cubiertos en ${name}.`),
                        href: matchedCapabilityId
                          ? (matchedCourseType === 'structured-learning' ? `/structured-learning/${matchedCapabilityId}` : `/capabilities/${matchedCapabilityId}`)
                          : undefined,
                        available: Boolean(matchedCapabilityId),
                        engramProgress,
                      })}
                      className="flex w-full items-center gap-3 rounded-xl border border-slate-200/70 bg-white p-3 text-left transition hover:-translate-y-0.5 hover:border-amber-300 hover:shadow-md dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-amber-400/40"
                    >
                      <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${isDone ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-400/15 dark:text-emerald-300' : matchedCapabilityId ? 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70' : 'bg-amber-100 text-amber-700 dark:bg-amber-400/15 dark:text-amber-300'}`}>
                        {matchedCapabilityId ? <BookOpen className="h-4.5 w-4.5" aria-hidden="true" /> : <Clock3 className="h-4.5 w-4.5" aria-hidden="true" />}
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-semibold text-slate-900 dark:text-white">{name}</span>
                        <span className={`mt-0.5 flex items-center gap-1.5 text-[11px] font-semibold ${isDone ? 'text-emerald-700 dark:text-emerald-300' : matchedCapabilityId ? 'text-slate-500 dark:text-white/55' : 'text-amber-700 dark:text-amber-300'}`}>
                          {statusLabel}
                          {engramLabel && <span className="text-slate-400 dark:text-white/40">· {engramLabel}</span>}
                        </span>
                      </span>
                      <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
                    </button>
                  );
                })
              )}
            </div>
          </section>

          <section className="flex flex-col overflow-hidden rounded-2xl border border-sky-200/70 bg-white shadow-sm shadow-sky-900/5 dark:border-sky-400/20 dark:bg-white/[0.04]" aria-labelledby="stay-fresh-title">
            <div className="flex items-center gap-3 border-b border-sky-100 bg-sky-50/60 px-5 py-4 dark:border-white/5 dark:bg-sky-500/10">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-sky-600 text-white shadow-sm">
                <RefreshCw className="h-4.5 w-4.5" aria-hidden="true" />
              </span>
              <div className="min-w-0">
                <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-sky-600 dark:text-sky-300">
                  {isEn ? 'Recall your knowledge' : 'Repasa tu conocimiento'}
                </p>
                <h2 id="stay-fresh-title" className="truncate text-sm font-bold text-slate-950 dark:text-white">
                  {isEn ? 'Stay Fresh' : 'Mantente Fresco'}
                </h2>
              </div>
            </div>
            <div className="flex-1 p-4">
              {knowledgeNetworkQuery.isLoading ? (
                <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn ? 'Checking your mastered engrams...' : 'Revisando tus engramas dominados...'}
                </p>
              ) : staleEngrams.length === 0 ? (
                <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn
                    ? `No engrams are ${STALE_ENGRAM_DAYS}+ days old yet — nothing to refresh right now.`
                    : `Aún no hay engramas con ${STALE_ENGRAM_DAYS}+ días de antigüedad — nada que repasar por ahora.`}
                </p>
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
                        className="flex w-16 flex-col items-center gap-1.5 text-center transition hover:-translate-y-0.5"
                      >
                        <span
                          className={`flex h-9 w-9 items-center justify-center rounded-full border border-white/20 text-[10px] font-bold leading-none shadow-md ${isFading ? 'animate-pulse' : ''}`}
                          style={{ background: memoryColor, color: memoryTextColor }}
                        >
                          {nodeBadge(engram.title)}
                        </span>
                        {percent !== null && (
                          <span className="text-[10px] font-bold" style={{ color: automatic ? undefined : memoryColor }}>
                            {automatic ? (isEn ? 'Auto' : 'Auto') : `${percent}%`}
                          </span>
                        )}
                        <span className="line-clamp-2 text-[9px] font-medium leading-tight text-slate-600 dark:text-white/60">{engram.title}</span>
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          </section>
        </div>
      </main>

      {activeCapability && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4"
          role="dialog"
          aria-modal="true"
          aria-labelledby="capability-modal-title"
          onClick={() => setActiveCapability(null)}
        >
          <div
            className="w-full max-w-lg rounded-3xl bg-white p-6 shadow-2xl dark:bg-slate-900 sm:p-8"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <h3 id="capability-modal-title" className="text-xl font-bold text-slate-950 dark:text-white">
                {activeCapability.title}
              </h3>
              <button
                type="button"
                onClick={() => setActiveCapability(null)}
                aria-label={isEn ? 'Close' : 'Cerrar'}
                className="shrink-0 rounded-full p-1 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-white/10 dark:hover:text-white"
              >
                <X className="h-5 w-5" aria-hidden="true" />
              </button>
            </div>
            <p className="mt-4 text-sm leading-6 text-slate-600 dark:text-white/70">{activeCapability.description}</p>
            <div className="mt-5 flex items-center gap-2 text-xs font-semibold">
              {activeCapability.available ? (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-2.5 py-1 text-emerald-800 dark:bg-emerald-400/15 dark:text-emerald-300">
                  {isEn ? 'Available' : 'Disponible'}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-100 px-2.5 py-1 text-amber-800 dark:bg-amber-400/15 dark:text-amber-300">
                  <Clock3 className="h-3.5 w-3.5" aria-hidden="true" />
                  {isEn ? 'Coming soon' : 'Próximamente'}
                </span>
              )}
            </div>
            {activeCapability.engramProgress && (
              <div className="mt-3">
                <div className="flex items-center justify-between text-xs font-semibold text-slate-500 dark:text-white/55">
                  <span>{isEn ? 'Engrams mastered (tracked in Brain)' : 'Engramas dominados (registrado en Brain)'}</span>
                  <span>{activeCapability.engramProgress.mastered}/{activeCapability.engramProgress.total}</span>
                </div>
                <div className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-amber-500 to-rose-600"
                    style={{ width: `${activeCapability.engramProgress.total > 0 ? Math.round((activeCapability.engramProgress.mastered / activeCapability.engramProgress.total) * 100) : 0}%` }}
                  />
                </div>
              </div>
            )}
            {activeCapability.available && activeCapability.href && (
              <Link
                to={activeCapability.href}
                onClick={() => setActiveCapability(null)}
                className="mt-6 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-indigo-600/20 transition hover:bg-indigo-700"
              >
                {isEn ? 'Go to class' : 'Ir a la clase'}
                <ArrowRight className="h-4 w-4" aria-hidden="true" />
              </Link>
            )}
          </div>
        </div>
      )}

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
