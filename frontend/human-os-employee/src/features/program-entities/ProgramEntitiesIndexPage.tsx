import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import {
  GraduationCap,
  ArrowRight,
  Sparkles,
  BookOpen,
  Network,
} from 'lucide-react';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getPrograms, type ProgramCatalogEntry } from '@/features/programs/api/programsApi';

export function ProgramEntitiesIndexPage() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const navigate = useNavigate();

  const [programs, setPrograms] = useState<ProgramCatalogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getPrograms()
      .then((items) => {
        if (!cancelled) {
          setPrograms(items);
          if (items.length === 1) {
            navigate(`/program-entities/${items[0].programId}`, { replace: true });
          }
        }
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [navigate]);

  if (loading) {
    return (
      <div className="mx-auto max-w-6xl p-6 sm:p-12 space-y-6">
        <div className="h-32 animate-pulse rounded-3xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]" />
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-44 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl space-y-8 p-4 pb-16 sm:p-8">
      {/* Header Banner */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-amber-600 via-purple-700 to-indigo-800 p-6 text-white shadow-xl shadow-purple-900/10 dark:border-white/10 sm:p-8">
        <div className="flex items-center gap-4">
          <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
            <Network className="h-8 w-8 text-amber-200" />
          </span>
          <div>
            <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/80">
              <Sparkles className="h-3.5 w-3.5 text-amber-300" />
              {isEn ? 'Program Knowledge & Concepts' : 'Conocimiento y Ontología de Programas'}
            </p>
            <h1 className="mt-1 text-2xl sm:text-3xl font-extrabold text-white">
              {isEn ? 'Program Entities' : 'Entidades del Programa'}
            </h1>
            <p className="mt-1 max-w-2xl text-sm text-white/85">
              {isEn
                ? 'Explore the consolidated catalog of entities, definitions, articles, and concepts across all courses and engrams within each program.'
                : 'Explora el catálogo consolidado de entidades, definiciones, artículos y conceptos clave presentes en todos los cursos y engramas de cada programa.'}
            </p>
          </div>
        </div>
      </div>

      {error ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          <p>{error}</p>
        </div>
      ) : programs.length === 0 ? (
        <div className="rounded-2xl border border-slate-200 bg-white dark:border-white/10 dark:bg-white/[0.02] p-12 text-center text-slate-500">
          {isEn ? 'No training programs available yet.' : 'No hay programas de capacitación disponibles aún.'}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {programs.map((program) => (
            <Link
              key={program.programId}
              to={`/program-entities/${program.programId}`}
              className="group relative flex flex-col justify-between rounded-3xl border border-slate-200 bg-white p-6 shadow-sm transition-all hover:-translate-y-1 hover:border-amber-500/40 hover:shadow-xl hover:shadow-amber-500/5 dark:border-white/10 dark:bg-slate-900/60 dark:hover:border-amber-500/40 dark:hover:bg-slate-900/90"
            >
              <div>
                <div className="flex items-center justify-between">
                  <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-amber-500/10 text-amber-600 dark:text-amber-400 group-hover:scale-105 transition-transform">
                    <GraduationCap className="h-6 w-6" />
                  </div>
                  <span className="flex items-center gap-1.5 rounded-full bg-slate-100 dark:bg-white/5 px-3 py-1 text-xs font-semibold text-slate-600 dark:text-slate-300">
                    <BookOpen className="h-3.5 w-3.5 text-purple-600 dark:text-purple-400" />
                    {program.capabilityCount} {isEn ? 'courses' : 'cursos'}
                  </span>
                </div>

                <h3 className="mt-4 text-base font-bold text-slate-900 dark:text-white group-hover:text-amber-600 dark:group-hover:text-amber-300 transition-colors">
                  {program.name}
                </h3>

                {program.description && (
                  <p className="mt-1.5 line-clamp-2 text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
                    {program.description}
                  </p>
                )}
              </div>

              <div className="mt-6 flex items-center justify-between border-t border-slate-100 dark:border-white/5 pt-4 text-xs text-amber-600 dark:text-amber-400 font-bold">
                <span>{isEn ? 'Explore entities & citations' : 'Ver entidades y citas'}</span>
                <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-1" />
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
