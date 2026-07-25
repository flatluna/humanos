import { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowLeft, CalendarDays, CheckCircle2, CircleDashed, GraduationCap, Lock, PlayCircle } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getEnrolledPrograms, type EnrolledProgram, type EnrolledProgramCapability } from '../api/enrolledProgramsApi';

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('es-ES', { year: 'numeric', month: 'long', day: 'numeric' });
  } catch {
    return iso;
  }
}

const STATUS_STYLES: Record<
  EnrolledProgramCapability['status'],
  { label: string; badge: string; Icon: typeof CheckCircle2 }
> = {
  NotStarted: {
    label: 'No iniciado',
    badge: 'bg-slate-100 text-slate-600 dark:bg-white/5 dark:text-white/50',
    Icon: CircleDashed,
  },
  InProgress: {
    label: 'En progreso',
    badge: 'bg-brand-500/10 text-brand-700 dark:text-brand-300',
    Icon: PlayCircle,
  },
  Completed: {
    label: 'Completado',
    badge: 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300',
    Icon: CheckCircle2,
  },
};

/** Detail view for a single enrolled Program — the ordered sequence of
 * "courses" (Capabilities) with each one's progress, per the "abrir el
 * programa y ejecutarlo paso a paso" flow. Clicking a course goes into the
 * EXISTING Capability graph/execution route, carrying `?programId=` so the
 * Runtime scopes the LearningSession to this Program (see
 * runtimeSessionApi.ts / InstructorRuntimeOrchestrator.StartSessionAsync). */
export default function ProgramDetailPage() {
  const { programId } = useParams();
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const [program, setProgram] = useState<EnrolledProgram | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!personId || !programId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    getEnrolledPrograms(personId)
      .then((programs) => {
        if (cancelled) return;
        const match = programs.find((p) => p.programId === programId) ?? null;
        setProgram(match);
        if (!match) setError('No encontramos este programa entre tus programas asignados.');
      })
      .catch(() => {
        if (!cancelled) setError('No pudimos cargar este programa. Intenta de nuevo en unos minutos.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [personId, programId]);

  const overallPercent = useMemo(() => {
    if (!program) return 0;
    const total = program.capabilities.reduce((sum, c) => sum + c.totalNodes, 0);
    const mastered = program.capabilities.reduce((sum, c) => sum + c.masteredNodes, 0);
    return total > 0 ? Math.round((mastered / total) * 100) : 0;
  }, [program]);

  return (
    <div className="mx-auto max-w-3xl p-4 sm:p-8">
      <Link
        to="/programs"
        className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-900 hover:underline dark:text-slate-400 dark:hover:text-white"
      >
        <ArrowLeft className="h-3.5 w-3.5" />
        Mis Programas
      </Link>

      {loading && (
        <div className="mt-6 h-40 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]" />
      )}

      {error && !loading && (
        <p className="mt-6 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          {error}
        </p>
      )}

      {!loading && !error && program && (
        <>
          <div className="mt-6 overflow-hidden rounded-2xl border border-slate-200 bg-gradient-to-br from-brand-500 via-indigo-500 to-violet-500 p-6 text-white shadow-lg dark:border-white/10">
            <div className="flex items-center gap-2 text-white/80">
              <GraduationCap className="h-4 w-4" />
              <span className="text-xs font-semibold uppercase tracking-wide">Programa</span>
            </div>
            <h1 className="mt-2 text-2xl font-bold tracking-tight">{program.name}</h1>
            {program.description && <p className="mt-2 max-w-xl text-sm text-white/85">{program.description}</p>}

            <div className="mt-4 flex flex-wrap items-center gap-4 text-xs font-medium text-white/85">
              <span className="flex items-center gap-1.5">
                <CalendarDays className="h-3.5 w-3.5" />
                Asignado el {formatDate(program.acceptedDate)}
              </span>
              <span>{program.capabilities.length} cursos</span>
              <span>{overallPercent}% completado</span>
            </div>

            <div className="mt-3 h-2 w-full overflow-hidden rounded-full bg-white/20">
              <div className="h-full rounded-full bg-white transition-all" style={{ width: `${overallPercent}%` }} />
            </div>
          </div>

          <h2 className="mt-8 text-sm font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Cursos de este programa
          </h2>

          <div className="mt-3 space-y-3">
            {program.capabilities.map((capability, index) => {
              const style = STATUS_STYLES[capability.status];
              const Icon = style.Icon;
              const percent =
                capability.totalNodes > 0 ? Math.round((capability.masteredNodes / capability.totalNodes) * 100) : 0;
              const previousRequiredIncomplete = program.capabilities
                .slice(0, index)
                .some((c) => c.isRequired && c.status !== 'Completed');
              const locked = index > 0 && previousRequiredIncomplete && capability.status === 'NotStarted';

              const card = (
                <div
                  className={`flex items-center gap-4 rounded-2xl border border-slate-200 bg-white p-4 transition dark:border-white/10 dark:bg-white/[0.03] ${
                    locked ? 'opacity-60' : 'hover:-translate-y-0.5 hover:shadow-md'
                  }`}
                >
                  <div className="flex h-11 w-11 flex-none items-center justify-center rounded-xl bg-slate-100 text-slate-500 dark:bg-white/5 dark:text-white/50">
                    {locked ? <Lock className="h-5 w-5" /> : <span className="text-sm font-bold">{index + 1}</span>}
                  </div>

                  <div className="min-w-0 flex-1">
                    <p className="truncate font-medium text-slate-900 dark:text-white">{capability.name}</p>
                    <div className="mt-1 flex items-center gap-2">
                      <span
                        className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium ${style.badge}`}
                      >
                        <Icon className="h-3 w-3" />
                        {style.label}
                      </span>
                      {capability.isRequired && (
                        <span className="text-[11px] font-medium text-slate-400 dark:text-white/30">Obligatorio</span>
                      )}
                    </div>
                    {capability.totalNodes > 0 && (
                      <div className="mt-2 h-1.5 w-full max-w-xs overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                        <div
                          className="h-full rounded-full bg-gradient-to-r from-brand-500 to-accent-500"
                          style={{ width: `${percent}%` }}
                        />
                      </div>
                    )}
                  </div>
                </div>
              );

              return locked ? (
                <div key={capability.capabilityId}>{card}</div>
              ) : (
                <Link key={capability.capabilityId} to={`/capabilities/${capability.capabilityId}?programId=${program.programId}`}>
                  {card}
                </Link>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}
