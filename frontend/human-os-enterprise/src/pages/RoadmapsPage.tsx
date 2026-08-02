import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link } from 'react-router';
import { GitBranch, Search, Sparkles, AlertCircle, CheckCircle2, BookOpen, Clock, ShieldCheck } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  getTenantPeople,
  getJobRole,
  getPersonResume,
  getPersonCareerPlan,
  generatePersonCareerPlan,
  acceptPersonCareerPlan,
  type CareerPlanRoadmapStepDto,
} from '@/api/orgStructureApi';

function errorMessage(error: unknown): string | null {
  if (!error) return null;
  if (error instanceof Error) return error.message;
  return 'Ocurrió un error inesperado.';
}

export function RoadmapsPage() {
  const { user } = useAuth();
  const tenantId = user!.tenantId;
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [selectedPersonId, setSelectedPersonId] = useState<string | null>(null);

  const peopleQuery = useQuery({
    queryKey: ['tenant-people', tenantId],
    queryFn: () => getTenantPeople(tenantId),
  });

  const people = peopleQuery.data ?? [];
  const selectedPerson = useMemo(
    () => people.find((p) => p.personId === selectedPersonId) ?? null,
    [people, selectedPersonId],
  );

  const filteredPeople = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return people;
    return people.filter(
      (p) =>
        (p.displayName ?? '').toLowerCase().includes(term) ||
        (p.email ?? '').toLowerCase().includes(term) ||
        (p.jobRoleTitle ?? '').toLowerCase().includes(term),
    );
  }, [people, search]);

  const jobRoleQuery = useQuery({
    queryKey: ['job-role', selectedPerson?.jobRoleId],
    queryFn: () => getJobRole(selectedPerson!.jobRoleId!),
    enabled: !!selectedPerson?.jobRoleId,
    staleTime: 0,
    refetchOnMount: 'always',
  });

  const resumeQuery = useQuery({
    queryKey: ['person-resume', selectedPersonId],
    queryFn: () => getPersonResume(selectedPersonId!),
    enabled: !!selectedPersonId,
  });

  const planQuery = useQuery({
    queryKey: ['person-career-plan', selectedPersonId],
    queryFn: () => getPersonCareerPlan(selectedPersonId!),
    enabled: !!selectedPersonId,
  });

  const generateMutation = useMutation({
    mutationFn: () => generatePersonCareerPlan(selectedPersonId!),
    onSuccess: (data) => {
      queryClient.setQueryData(['person-career-plan', selectedPersonId], data);
    },
  });

  const acceptMutation = useMutation({
    mutationFn: () => acceptPersonCareerPlan(selectedPersonId!),
    onSuccess: (data) => {
      queryClient.setQueryData(['person-career-plan', selectedPersonId], data);
    },
  });

  const currentSkills = resumeQuery.data?.skills ?? [];
  const requiredTechnicalSkills = jobRoleQuery.data?.technicalSkills ?? [];
  const requiredSoftSkills = jobRoleQuery.data?.softSkills ?? [];

  const plan = generateMutation.data ?? planQuery.data;

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Rutas de Carrera</h1>
        <p className="mt-1 text-sm text-slate-500 dark:text-white/50">
          Plan de desarrollo generado por IA para cada empleado: compara sus skills actuales contra las que exige su
          puesto y recomienda cursos reales del catálogo para cerrar la brecha.
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[280px_1fr]">
        {/* Employee picker */}
        <div className="space-y-3">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Buscar empleado…"
              className="w-full rounded-lg border border-slate-200 bg-white py-2 pl-9 pr-3 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
            />
          </div>

          <div className="max-h-[60vh] space-y-1 overflow-y-auto rounded-xl border border-slate-200 p-1.5 dark:border-white/10">
            {peopleQuery.isLoading && (
              <p className="px-3 py-2 text-sm text-slate-400">Cargando…</p>
            )}
            {filteredPeople.map((p) => (
              <button
                key={p.personId}
                type="button"
                onClick={() => setSelectedPersonId(p.personId)}
                className={`w-full rounded-lg px-3 py-2 text-left text-sm transition ${
                  p.personId === selectedPersonId
                    ? 'bg-emerald-500 text-white shadow-sm shadow-emerald-500/30'
                    : 'text-slate-700 hover:bg-slate-100 dark:text-white/80 dark:hover:bg-white/5'
                }`}
              >
                <span className="block font-medium">{p.displayName ?? p.email ?? 'Sin nombre'}</span>
                <span
                  className={`block text-xs ${
                    p.personId === selectedPersonId ? 'text-emerald-50/80' : 'text-slate-400 dark:text-white/40'
                  }`}
                >
                  {p.jobRoleTitle ?? 'Sin puesto asignado'}
                </span>
              </button>
            ))}
            {!peopleQuery.isLoading && filteredPeople.length === 0 && (
              <p className="px-3 py-2 text-sm text-slate-400">No se encontraron empleados.</p>
            )}
          </div>
        </div>

        {/* Selected employee panel */}
        <div className="space-y-6">
          {!selectedPerson && (
            <div className="rounded-2xl border border-dashed border-emerald-200 bg-gradient-to-br from-emerald-50/60 to-white p-8 text-center dark:border-emerald-500/20 dark:from-emerald-500/5 dark:to-transparent">
              <span className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-emerald-500 to-emerald-400 text-white shadow-md shadow-emerald-500/30">
                <GitBranch className="h-6 w-6" />
              </span>
              <p className="mt-4 text-sm font-semibold text-slate-900 dark:text-white">
                Selecciona un empleado
              </p>
              <p className="mx-auto mt-1 max-w-md text-sm text-slate-500 dark:text-white/50">
                Elige un empleado de la lista para ver o generar su plan de carrera con IA.
              </p>
            </div>
          )}

          {selectedPerson && !selectedPerson.jobRoleId && (
            <div className="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-300">
              <AlertCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
              <p>
                Este empleado no tiene un puesto asignado todavía.{' '}
                <Link to={`/personal/${selectedPerson.personId}`} className="font-medium underline">
                  Asígnale un puesto
                </Link>{' '}
                antes de generar su plan de carrera.
              </p>
            </div>
          )}

          {selectedPerson && selectedPerson.jobRoleId && (
            <>
              <div className="rounded-2xl border border-slate-200 p-5 dark:border-white/10">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                      {selectedPerson.displayName ?? selectedPerson.email}
                    </h2>
                    <p className="text-sm text-slate-500 dark:text-white/50">{selectedPerson.jobRoleTitle}</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => generateMutation.mutate()}
                    disabled={generateMutation.isPending}
                    className="inline-flex items-center gap-2 rounded-lg bg-emerald-500 px-4 py-2 text-sm font-medium text-white shadow-sm shadow-emerald-500/30 transition hover:bg-emerald-600 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    <Sparkles className="h-4 w-4" />
                    {generateMutation.isPending
                      ? 'Generando…'
                      : plan?.hasPlan
                        ? 'Regenerar plan con IA'
                        : 'Generar plan con IA'}
                  </button>
                </div>

                {errorMessage(generateMutation.error) && (
                  <p className="mt-3 text-sm text-red-600 dark:text-red-400">
                    {errorMessage(generateMutation.error)}
                  </p>
                )}

                <div className="mt-5 grid gap-5 sm:grid-cols-2">
                  <div>
                    <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-white/40">
                      Skills requeridas por el puesto
                    </h3>
                    <p className="mt-1 text-xs text-slate-400 dark:text-white/40">
                      Fuente: puesto asignado del empleado.
                    </p>

                    <p className="mt-3 text-xs font-semibold text-slate-500 dark:text-white/50">Técnicas</p>
                    <div className="mt-1 flex flex-wrap gap-1.5">
                      {requiredTechnicalSkills.length === 0 && (
                        <span className="text-sm text-slate-400">Sin skills técnicas definidas.</span>
                      )}
                      {requiredTechnicalSkills.map((skill) => (
                        <span
                          key={`tech-${skill}`}
                          className="rounded-full bg-slate-100 px-3 py-1 text-xs font-medium text-slate-700 dark:bg-white/10 dark:text-white/70"
                        >
                          {skill}
                        </span>
                      ))}
                    </div>

                    <p className="mt-3 text-xs font-semibold text-slate-500 dark:text-white/50">Soft skills</p>
                    <div className="mt-1 flex flex-wrap gap-1.5">
                      {requiredSoftSkills.length === 0 && (
                        <span className="text-sm text-slate-400">Sin soft skills definidos.</span>
                      )}
                      {requiredSoftSkills.map((skill) => (
                        <span
                          key={`soft-${skill}`}
                          className="rounded-full bg-amber-100 px-3 py-1 text-xs font-medium text-amber-700 dark:bg-amber-500/15 dark:text-amber-300"
                        >
                          {skill}
                        </span>
                      ))}
                    </div>

                    {requiredTechnicalSkills.length === 0 && requiredSoftSkills.length === 0 && (
                      <p className="mt-2 text-sm text-slate-400">Sin skills definidas para este puesto.</p>
                    )}
                  </div>
                  <div>
                    <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-white/40">
                      Skills actuales del empleado
                    </h3>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      {currentSkills.length === 0 && (
                        <span className="text-sm text-slate-400">
                          Sin skills registradas (agrégalas desde su ficha).
                        </span>
                      )}
                      {currentSkills.map((skill) => (
                        <span
                          key={skill}
                          className="rounded-full bg-sky-100 px-3 py-1 text-xs font-medium text-sky-700 dark:bg-sky-500/15 dark:text-sky-300"
                        >
                          {skill}
                        </span>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              {plan?.hasPlan && (
                <div className="space-y-5">
                  {plan.overallRationale && (
                    <div className="rounded-xl border border-emerald-200 bg-emerald-50/60 p-4 text-sm text-emerald-900 dark:border-emerald-500/20 dark:bg-emerald-500/10 dark:text-emerald-200">
                      <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-emerald-600 dark:text-emerald-400">
                        <CheckCircle2 className="h-3.5 w-3.5" /> Resumen del plan
                      </p>
                      <p className="mt-1.5">{plan.overallRationale}</p>
                    </div>
                  )}

                  <div>
                    <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-white/40">
                      Brecha de skills detectada
                    </h3>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      {plan.gapSkills.length === 0 && (
                        <span className="text-sm text-emerald-600 dark:text-emerald-400">
                          Sin brechas — cubre todas las skills requeridas.
                        </span>
                      )}
                      {plan.gapSkills.map((skill) => (
                        <span
                          key={skill}
                          className="rounded-full bg-rose-100 px-3 py-1 text-xs font-medium text-rose-700 dark:bg-rose-500/15 dark:text-rose-300"
                        >
                          {skill}
                        </span>
                      ))}
                    </div>
                  </div>

                  <div className="grid gap-5 sm:grid-cols-2">
                    <RoadmapTrack
                      title="Plan técnico"
                      accentClassName="bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70"
                      steps={plan.technicalRoadmap}
                      emptyMessage="Sin brechas técnicas — no se requiere capacitación técnica adicional."
                    />
                    <RoadmapTrack
                      title="Plan de soft skills"
                      accentClassName="bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300"
                      steps={plan.softSkillsRoadmap}
                      emptyMessage="Sin brechas de soft skills — no se requiere capacitación adicional."
                    />
                  </div>

                  <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-slate-200 p-4 dark:border-white/10">
                    <div>
                      {plan.isAccepted ? (
                        <p className="flex items-center gap-2 text-sm font-medium text-emerald-600 dark:text-emerald-400">
                          <ShieldCheck className="h-4 w-4" />
                          Plan aceptado el {plan.acceptedDate ? new Date(plan.acceptedDate).toLocaleString() : ''}
                        </p>
                      ) : (
                        <p className="flex items-center gap-2 text-sm font-medium text-amber-600 dark:text-amber-400">
                          <Clock className="h-4 w-4" />
                          Propuesta de IA pendiente de aceptación por RRHH
                        </p>
                      )}
                      {plan.generatedDate && (
                        <p className="mt-1 text-xs text-slate-400 dark:text-white/30">
                          Generado el {new Date(plan.generatedDate).toLocaleString()}
                        </p>
                      )}
                    </div>

                    {!plan.isAccepted && (
                      <button
                        type="button"
                        onClick={() => acceptMutation.mutate()}
                        disabled={acceptMutation.isPending}
                        className="inline-flex items-center gap-2 rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
                      >
                        <ShieldCheck className="h-4 w-4" />
                        {acceptMutation.isPending ? 'Aceptando…' : 'Aceptar plan'}
                      </button>
                    )}
                  </div>

                  {errorMessage(acceptMutation.error) && (
                    <p className="text-sm text-red-600 dark:text-red-400">{errorMessage(acceptMutation.error)}</p>
                  )}
                </div>
              )}

              {plan && !plan.hasPlan && !generateMutation.isPending && (
                <p className="text-sm text-slate-400">
                  Este empleado aún no tiene un plan de carrera generado. Usa el botón de arriba para crear uno.
                </p>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

interface RoadmapTrackProps {
  title: string;
  accentClassName: string;
  steps: CareerPlanRoadmapStepDto[];
  emptyMessage: string;
}

/// Ordered training roadmap for one track (technical or soft skills) —
/// each step shows whether a real catalog course already exists
/// (MatchedProgramId set) or is still pending creation, since the plan is
/// saved and displayed even before every course has been uploaded.
function RoadmapTrack({ title, accentClassName, steps, emptyMessage }: RoadmapTrackProps) {
  return (
    <div>
      <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400 dark:text-white/40">{title}</h3>
      {steps.length === 0 ? (
        <p className="mt-2 text-sm text-slate-400">{emptyMessage}</p>
      ) : (
        <ol className="mt-2 space-y-2.5">
          {steps.map((step) => (
            <li
              key={`${step.order}-${step.skill}`}
              className="flex items-start gap-3 rounded-xl border border-slate-200 p-4 dark:border-white/10"
            >
              <span
                className={`mt-0.5 flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full text-xs font-semibold ${accentClassName}`}
              >
                {step.order}
              </span>
              <div className="min-w-0 flex-1">
                <p className="text-sm font-semibold text-slate-900 dark:text-white">{step.title}</p>
                <p className="mt-0.5 text-xs text-slate-400 dark:text-white/40">Cierra: {step.skill}</p>
                <p className="mt-1 text-sm text-slate-500 dark:text-white/50">{step.description}</p>
                {step.matchedProgramId ? (
                  <span className="mt-2 inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-medium text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300">
                    <BookOpen className="h-3 w-3" /> Curso disponible en el catálogo
                  </span>
                ) : (
                  <span className="mt-2 inline-flex items-center gap-1 rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-500 dark:bg-white/5 dark:text-white/40">
                    <Clock className="h-3 w-3" /> Curso pendiente de crear
                  </span>
                )}
              </div>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}


