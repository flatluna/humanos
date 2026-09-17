import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { Bot, GraduationCap, Layers, Sparkles, BookOpen, MessageSquare, ArrowRight } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getPrograms, getProgramById, getProgramLogoUrl, type ProgramCatalogEntry } from '@/features/programs/api/programsApi';

export interface ProgramAgentCardItem extends ProgramCatalogEntry {
  courseCount: number;
  totalEngrams: number;
}

export function ProgramAgentsPage() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const [programs, setPrograms] = useState<ProgramAgentCardItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    getPrograms()
      .then(async (list) => {
        const enriched = await Promise.all(
          list.map(async (prog) => {
            try {
              const detail = await getProgramById(prog.programId);
              const totalEngrams = (detail.capabilities ?? []).reduce(
                (sum, c) => sum + (c.nodeCount || c.levelCount || 0),
                0
              );
              return {
                ...prog,
                courseCount: detail.capabilities?.length ?? prog.capabilityCount ?? 0,
                totalEngrams,
              };
            } catch {
              return {
                ...prog,
                courseCount: prog.capabilityCount ?? 0,
                totalEngrams: 0,
              };
            }
          })
        );
        if (!cancelled) {
          setPrograms(enriched);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : 'Error loading programs');
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [personId]);

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-4 pb-16 sm:p-8">
      {/* Header Banner */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-purple-700 via-indigo-700 to-blue-800 p-6 text-white shadow-xl shadow-purple-900/10 dark:border-white/10 sm:p-8">
        <div className="flex items-center gap-4">
          <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
            <Bot className="h-8 w-8 text-white" />
          </span>
          <div>
            <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/80">
              <Sparkles className="h-3.5 w-3.5 text-amber-300" />
              {isEn ? 'Multi-Course Program Intelligence' : 'Inteligencia Integral de Programas'}
            </p>
            <h1 className="mt-1 text-2xl font-bold text-white">
              {isEn ? 'Program AI Agents' : 'Agentes de Programa'}
            </h1>
            <p className="mt-1 max-w-2xl text-sm text-white/80">
              {isEn
                ? 'Master-level agents with global memory over all courses and engrams within each program. Ask complex, cross-cutting questions.'
                : 'Agentes especializados con memoria y dominio de todos los cursos y engramas de cada programa. Pregunta temas transversales o específicos.'}
            </p>
          </div>
        </div>
      </div>

      {loading && (
        <LoadingBackdrop
          title={isEn ? 'Loading program agents...' : 'Cargando agentes de programa...'}
          description={isEn ? 'Preparing program knowledge graph.' : 'Preparando el grafo de conocimiento del programa.'}
        />
      )}

      {error && (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-5 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          {error}
        </div>
      )}

      {!loading && !error && programs.length === 0 && (
        <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
          <BookOpen className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">
            {isEn ? 'No programs available' : 'No hay programas disponibles'}
          </h3>
          <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
            {isEn ? 'New programs will be listed here soon.' : 'Pronto se publicarán nuevos programas de capacitación.'}
          </p>
        </Card>
      )}

      {!loading && !error && programs.length > 0 && (
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {programs.map((program) => (
            <div
              key={program.programId}
              className="group flex flex-col justify-between overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-xs transition-all duration-200 hover:-translate-y-1 hover:border-purple-300 hover:shadow-xl hover:shadow-purple-500/10 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-purple-500/30"
            >
              {/* Card Header */}
              <div className="relative h-32 w-full overflow-hidden bg-gradient-to-br from-purple-600 via-indigo-600 to-blue-700">
                {program.hasLogo ? (
                  <img
                    src={getProgramLogoUrl(program.programId)}
                    alt=""
                    className="h-full w-full object-cover opacity-90 transition-transform duration-500 group-hover:scale-105"
                    onError={(e) => {
                      (e.currentTarget as HTMLImageElement).style.display = 'none';
                    }}
                  />
                ) : (
                  <div className="flex h-full w-full items-center justify-center">
                    <GraduationCap className="h-12 w-12 text-white/80" strokeWidth={1.5} />
                  </div>
                )}
                <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-transparent to-transparent" />
                <div className="absolute bottom-2.5 left-3 flex items-center gap-1.5 rounded-full bg-black/60 backdrop-blur-md px-2.5 py-0.5 text-[11px] font-medium text-white">
                  <Bot className="h-3.5 w-3.5 text-purple-300" />
                  <span>{isEn ? 'Cross-Course Agent' : 'Agente Multi-Curso'}</span>
                </div>
              </div>

              {/* Card Body */}
              <div className="flex flex-1 flex-col p-5">
                <h3 className="font-bold text-base text-slate-900 group-hover:text-purple-600 transition dark:text-white dark:group-hover:text-purple-400 line-clamp-2">
                  {program.name}
                </h3>
                <p className="mt-2 text-xs leading-relaxed text-slate-500 dark:text-white/60 line-clamp-2">
                  {program.description || (isEn ? 'Program knowledge assistant.' : 'Asistente experto en todo el contenido del programa.')}
                </p>

                {/* Metrics */}
                <div className="mt-4 flex items-center gap-3 pt-3 border-t border-slate-100 dark:border-white/5 text-xs text-slate-500 dark:text-slate-400">
                  <div className="flex items-center gap-1 font-medium">
                    <Layers className="h-3.5 w-3.5 text-purple-500" />
                    <span>{program.courseCount} {program.courseCount === 1 ? (isEn ? 'course' : 'curso') : (isEn ? 'courses' : 'cursos')}</span>
                  </div>
                  {program.totalEngrams > 0 && (
                    <div className="flex items-center gap-1 font-medium">
                      <Sparkles className="h-3.5 w-3.5 text-amber-500" />
                      <span>{program.totalEngrams} {isEn ? 'engrams' : 'engramas'}</span>
                    </div>
                  )}
                </div>

                {/* Action button */}
                <Link
                  to={`/program-agents/${program.programId}`}
                  className="mt-5 inline-flex w-full items-center justify-center gap-2 rounded-2xl bg-gradient-to-r from-purple-600 to-indigo-600 px-4 py-2.5 text-xs font-bold text-white shadow-md shadow-purple-600/20 transition hover:brightness-110 hover:shadow-lg"
                >
                  <MessageSquare className="h-4 w-4" />
                  <span>{isEn ? 'Chat with Program Agent' : 'Consultar Agente del Programa'}</span>
                  <ArrowRight className="h-3.5 w-3.5" />
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
