import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { Layers, CalendarDays, ArrowRight, Sparkles, Star, BookOpen } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  getEnrolledCapabilities,
  type EnrolledCapability,
} from '@/features/capabilities/api/enrolledCapabilitiesApi';

/** Rotating gradient palette for card headers — same cycling approach as
 * MyProgramsPage.tsx, purely cosmetic. */
const CARD_GRADIENTS = [
  'from-emerald-400 via-teal-500 to-cyan-500',
  'from-brand-500 via-indigo-500 to-violet-500',
  'from-amber-400 via-orange-500 to-rose-500',
  'from-fuchsia-500 via-pink-500 to-rose-500',
];

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('es-ES', { year: 'numeric', month: 'long', day: 'numeric' });
  } catch {
    return iso;
  }
}

function progressOf(capability: EnrolledCapability): { percent: number } {
  const percent = capability.totalNodes > 0 ? Math.round((capability.masteredNodes / capability.totalNodes) * 100) : 0;
  return { percent };
}

/** "Mis Capacidades" (2026-07-24) — card grid of every standalone
 * Capability (no Program) the student explicitly enrolled in via
 * "Inscribirme" on /capabilities, each with its enrollment date and real
 * progress. Sibling page to "Mis Programas" (MyProgramsPage.tsx). Opening a
 * card goes straight into the Capability's graph map, same as /capabilities. */
export default function MyCapabilitiesPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const [capabilities, setCapabilities] = useState<EnrolledCapability[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!personId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    getEnrolledCapabilities(personId)
      .then((data) => {
        if (!cancelled) setCapabilities(data);
      })
      .catch(() => {
        if (!cancelled) setError('No pudimos cargar tus Engramas. Intenta de nuevo en unos minutos.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [personId]);

  const isEmpty = useMemo(() => !loading && !error && capabilities.length === 0, [loading, error, capabilities]);

  return (
    <div className="mx-auto max-w-5xl p-4 sm:p-8">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-emerald-500 to-teal-500 text-white shadow-lg shadow-emerald-500/30">
          <Layers className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">Mis Engramas</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Los Engramas sueltos en los que te inscribiste, con tu progreso real en cada uno.
          </p>
        </div>
      </div>

      {loading && (
        <div className="mt-10 grid grid-cols-1 gap-6 sm:grid-cols-2">
          {[0, 1].map((i) => (
            <div
              key={i}
              className="h-52 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]"
            />
          ))}
        </div>
      )}

      {error && !loading && (
        <p className="mt-8 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          {error}
        </p>
      )}

      {isEmpty && (
        <div className="mt-10 rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-10 text-center dark:border-white/15 dark:bg-white/[0.02]">
          <BookOpen className="mx-auto h-8 w-8 text-emerald-500" />
          <p className="mt-3 font-medium text-slate-700 dark:text-white">Todavía no te has inscrito en ningún Engrama</p>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Explora el catálogo y dale "Inscribirme" al que quieras seguir de cerca.
          </p>
          <Link
            to="/capabilities"
            className="mt-4 inline-flex items-center gap-1.5 rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            Explorar Engramas
            <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        </div>
      )}

      {!loading && !error && capabilities.length > 0 && (
        <div className="mt-8 grid grid-cols-1 gap-6 sm:grid-cols-2">
          {capabilities.map((capability, index) => {
            const { percent } = progressOf(capability);
            const gradient = CARD_GRADIENTS[index % CARD_GRADIENTS.length];

            return (
              <Link
                key={capability.capabilityId}
                to={`/capabilities/${capability.capabilityId}`}
                className="group overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm transition hover:-translate-y-0.5 hover:shadow-xl dark:border-white/10 dark:bg-white/[0.03]"
              >
                <div className={`relative h-24 bg-gradient-to-br ${gradient} p-5`}>
                  <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.25),transparent_60%)]" />
                  <div className="relative flex items-center gap-2 text-white/90">
                    <Layers className="h-4 w-4" />
                    <span className="text-xs font-semibold uppercase tracking-wide">
                      {capability.subjectName ?? 'Engrama independiente'}
                    </span>
                  </div>
                  <h2 className="relative mt-2 line-clamp-1 text-lg font-bold text-white drop-shadow-sm">
                    {capability.name}
                  </h2>
                </div>

                <div className="p-5">
                  {capability.description && (
                    <p className="line-clamp-2 text-sm text-slate-500 dark:text-slate-400">{capability.description}</p>
                  )}

                  <div className="mt-4 flex items-center gap-1.5 text-xs font-medium text-slate-500 dark:text-slate-400">
                    <CalendarDays className="h-3.5 w-3.5" />
                    Inscrito el {formatDate(capability.enrolledDate)}
                  </div>

                  {typeof capability.averageScore === 'number' && (
                    <div className="mt-3 flex items-center gap-1.5 text-xs font-semibold text-amber-600 dark:text-amber-300">
                      <Star className="h-3.5 w-3.5" fill="currentColor" />
                      Promedio hasta hoy: {capability.averageScore}/100
                    </div>
                  )}

                  <div className="mt-4">
                    <div className="flex items-center justify-between text-xs font-medium text-slate-500 dark:text-slate-400">
                      <span>Progreso</span>
                      <span>
                        {capability.masteredNodes}/{capability.totalNodes} nodos · {percent}%
                      </span>
                    </div>
                    <div className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                      <div
                        className={`h-full rounded-full bg-gradient-to-r ${gradient} transition-all`}
                        style={{ width: `${percent}%` }}
                      />
                    </div>
                  </div>

                  <div className="mt-4 flex items-center gap-1 text-sm font-medium text-brand-600 group-hover:gap-2 dark:text-brand-400">
                    Continuar
                    <ArrowRight className="h-4 w-4 transition-all" />
                  </div>
                </div>
              </Link>
            );
          })}
        </div>
      )}

      {!loading && !error && capabilities.length > 0 && (
        <div className="mt-6 flex justify-center">
          <Link
            to="/capabilities"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-600 hover:gap-2 dark:text-brand-400"
          >
            <Sparkles className="h-4 w-4" />
            Explorar más Engramas
          </Link>
        </div>
      )}
    </div>
  );
}
