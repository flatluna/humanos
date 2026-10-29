import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router-dom';
import {
  GraduationCap,
  ArrowRight,
  Sparkles,
  BookOpen,
} from 'lucide-react';
import { getPrograms, type BackendProgram } from '../lib/api/programsApi';
import LoadingSpinner from '../components/LoadingSpinner';

export default function ProgramEntitiesIndexPage() {
  const { i18n } = useTranslation();
  const isEs = (i18n.language || 'es').startsWith('es');
  const navigate = useNavigate();

  const [programs, setPrograms] = useState<BackendProgram[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getPrograms()
      .then((items) => {
        if (!cancelled) {
          setPrograms(items);
          // If only 1 program exists, navigate directly to it for convenience
          if (items.length === 1) {
            navigate(`/programs/${items[0].ProgramId}/entities`, { replace: true });
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
      <div className="flex min-h-[50vh] items-center justify-center">
        <LoadingSpinner />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 lg:px-8">
      {/* Header */}
      <div className="mb-8 border-b border-white/10 pb-6">
        <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-400">
          <Sparkles className="h-4 w-4" />
          <span>{isEs ? 'Ontología de Programas' : 'Program Ontology'}</span>
        </div>
        <h1 className="mt-2 text-2xl font-bold tracking-tight text-white sm:text-3xl">
          {isEs ? 'Entidades por Programa' : 'Program Entities'}
        </h1>
        <p className="mt-2 text-sm text-slate-400 max-w-2xl">
          {isEs
            ? 'Explora el catálogo consolidado de entidades, términos clave y conceptos presentes en todos los cursos y engrams de cada programa formativo.'
            : 'Explore the consolidated catalog of entities, key terms, and concepts across all courses and engrams within each learning program.'}
        </p>
      </div>

      {error ? (
        <div className="rounded-2xl border border-red-500/20 bg-red-500/10 p-6 text-center text-red-300">
          <p>{error}</p>
        </div>
      ) : programs.length === 0 ? (
        <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-500">
          {isEs ? 'No hay programas creados todavía.' : 'No programs have been created yet.'}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {programs.map((program) => (
            <Link
              key={program.ProgramId}
              to={`/programs/${program.ProgramId}/entities`}
              className="group relative flex flex-col justify-between rounded-2xl border border-white/10 bg-slate-900/60 p-5 transition-all hover:border-amber-500/40 hover:bg-slate-900/90 hover:shadow-xl hover:shadow-amber-500/5"
            >
              <div>
                <div className="flex items-center justify-between">
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-500/10 text-amber-400 group-hover:scale-105 transition-transform">
                    <GraduationCap className="h-5 w-5" />
                  </div>
                  <span className="flex items-center gap-1 rounded-full bg-white/5 px-2.5 py-1 text-xs text-slate-400">
                    <BookOpen className="h-3 w-3 text-sky-400" />
                    {program.CapabilityCount} {isEs ? 'cursos' : 'courses'}
                  </span>
                </div>

                <h3 className="mt-4 text-base font-semibold text-white group-hover:text-amber-300 transition-colors">
                  {program.Name}
                </h3>

                {program.Description && (
                  <p className="mt-1 line-clamp-2 text-xs text-slate-400">
                    {program.Description}
                  </p>
                )}
              </div>

              <div className="mt-5 flex items-center justify-between border-t border-white/5 pt-3 text-xs text-amber-400 font-medium">
                <span>{isEs ? 'Ver todas las entidades' : 'View all entities'}</span>
                <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-1" />
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
