import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { GraduationCap, CalendarDays, Layers, ArrowRight, Sparkles, Star } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getEnrolledPrograms, type EnrolledProgram } from '../api/enrolledProgramsApi';

/** Rotating gradient palette for card headers — purely cosmetic, cycles by index so
 * a page with several enrolled programs still reads as a varied, colorful grid. */
const CARD_GRADIENTS = [
  'from-brand-500 via-indigo-500 to-violet-500',
  'from-amber-400 via-orange-500 to-rose-500',
  'from-emerald-400 via-teal-500 to-cyan-500',
  'from-fuchsia-500 via-pink-500 to-rose-500',
];

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('es-ES', { year: 'numeric', month: 'long', day: 'numeric' });
  } catch {
    return iso;
  }
}

function programProgress(program: EnrolledProgram): { mastered: number; total: number; percent: number } {
  const mastered = program.capabilities.reduce((sum, c) => sum + c.masteredNodes, 0);
  const total = program.capabilities.reduce((sum, c) => sum + c.totalNodes, 0);
  const percent = total > 0 ? Math.round((mastered / total) * 100) : 0;
  return { mastered, total, percent };
}

/** "Mis Programas" — card grid of every Program the student is enrolled in
 * (via the Growth Plan's Starting Point step), each with its assigned date
 * and aggregate progress. Opening a card goes to its ProgramDetailPage,
 * which lists the Program's ordered courses (Capabilities). */
export default function MyProgramsPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const [programs, setPrograms] = useState<EnrolledProgram[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!personId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    getEnrolledPrograms(personId)
      .then((data) => {
        if (!cancelled) setPrograms(data);
      })
      .catch(() => {
        if (!cancelled) setError('No pudimos cargar tus programas. Intenta de nuevo en unos minutos.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [personId]);

  const isEmpty = useMemo(() => !loading && !error && programs.length === 0, [loading, error, programs]);

  return (
    <div className="mx-auto max-w-5xl p-4 sm:p-8">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-brand-500 to-accent-500 text-white shadow-lg shadow-brand-500/30">
          <GraduationCap className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">Mis Programas</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Los programas de aprendizaje a los que estás inscrito, con tu progreso en cada uno.
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
          <Sparkles className="mx-auto h-8 w-8 text-brand-500" />
          <p className="mt-3 font-medium text-slate-700 dark:text-white">Todavía no tienes programas asignados</p>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Completa tu Plan de Crecimiento para recibir un programa recomendado para ti.
          </p>
          <Link
            to="/growth-plan/starting-point"
            className="mt-4 inline-flex items-center gap-1.5 rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            Ir a mi Plan de Crecimiento
            <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        </div>
      )}

      {!loading && !error && programs.length > 0 && (
        <div className="mt-8 grid grid-cols-1 gap-6 sm:grid-cols-2">
          {programs.map((program, index) => {
            const { mastered, total, percent } = programProgress(program);
            const gradient = CARD_GRADIENTS[index % CARD_GRADIENTS.length];

            return (
              <Link
                key={program.programId}
                to={`/programs/${program.programId}`}
                className="group overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm transition hover:-translate-y-0.5 hover:shadow-xl dark:border-white/10 dark:bg-white/[0.03]"
              >
                <div className={`relative h-24 bg-gradient-to-br ${gradient} p-5`}>
                  <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.25),transparent_60%)]" />
                  <div className="relative flex items-center gap-2 text-white/90">
                    <Layers className="h-4 w-4" />
                    <span className="text-xs font-semibold uppercase tracking-wide">
                      {program.capabilities.length} curso{program.capabilities.length === 1 ? '' : 's'}
                    </span>
                  </div>
                  <h2 className="relative mt-2 line-clamp-1 text-lg font-bold text-white drop-shadow-sm">
                    {program.name}
                  </h2>
                </div>

                <div className="p-5">
                  {program.description && (
                    <p className="line-clamp-2 text-sm text-slate-500 dark:text-slate-400">{program.description}</p>
                  )}

                  <div className="mt-4 flex items-center gap-1.5 text-xs font-medium text-slate-500 dark:text-slate-400">
                    <CalendarDays className="h-3.5 w-3.5" />
                    Asignado el {formatDate(program.acceptedDate)}
                  </div>

                  {typeof program.averageScore === 'number' && (
                    <div className="mt-3 flex items-center gap-1.5 text-xs font-semibold text-amber-600 dark:text-amber-300">
                      <Star className="h-3.5 w-3.5" fill="currentColor" />
                      Promedio hasta hoy: {program.averageScore}/100
                    </div>
                  )}

                  <div className="mt-4">
                    <div className="flex items-center justify-between text-xs font-medium text-slate-500 dark:text-slate-400">
                      <span>Progreso</span>
                      <span>
                        {mastered}/{total} nodos · {percent}%
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
                    Abrir programa
                    <ArrowRight className="h-4 w-4 transition-all" />
                  </div>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
