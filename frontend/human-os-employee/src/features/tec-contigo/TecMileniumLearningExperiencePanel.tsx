import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { CheckCircle2, Mail, Sparkles } from "lucide-react";
import { Card } from "@/components/ui/Card";
import { useAuth } from "@/auth/AuthContext";
import {
  acceptTecMileniumCatalogCourse,
  generateTecMileniumPlan,
  requestTecMileniumExternalCourse,
  type TecMileniumCatalogCourseDto,
  type TecMileniumExternalCourseDto,
  type TecMileniumPlanDto,
} from "./api/tecMileniumApi";

/** "2. Diseño de la experiencia de aprendizaje" (2026-09-28) — Tec Contigo
 *  "Mientras estudias" AI plan: designs an interactive, adaptive, stackable,
 *  personalized learning path from the student's current semester/grades,
 *  interests and the real catalog, then lets them enroll directly in
 *  catalog courses or request the Tec develop external ones (auto-emailed). */
export function TecMileniumLearningExperiencePanel() {
  const { user } = useAuth();
  const personId = user?.personId ?? "";
  const [prompt, setPrompt] = useState("");
  const [plan, setPlan] = useState<TecMileniumPlanDto | null>(null);
  const [enrolledIds, setEnrolledIds] = useState<Set<string>>(new Set());
  const [requestedNames, setRequestedNames] = useState<Set<string>>(new Set());
  const [selectedExternalNames, setSelectedExternalNames] = useState<Set<string>>(new Set());

  const generateMutation = useMutation({
    mutationFn: () => generateTecMileniumPlan(personId, prompt || undefined),
    onSuccess: (result) => {
      setPlan(result);
      setSelectedExternalNames(new Set());
    },
  });

  const acceptMutation = useMutation({
    mutationFn: (course: TecMileniumCatalogCourseDto) =>
      acceptTecMileniumCatalogCourse(personId, course.catalogItemId, course.catalogType),
    onSuccess: (_data, course) =>
      setEnrolledIds((current) => new Set(current).add(course.catalogItemId)),
  });

  const requestMutation = useMutation({
    mutationFn: (courses: TecMileniumExternalCourseDto[]) =>
      requestTecMileniumExternalCourse(personId, courses),
    onSuccess: (_data, courses) => {
      setRequestedNames((current) => {
        const next = new Set(current);
        courses.forEach((course) => next.add(course.name));
        return next;
      });
      setSelectedExternalNames(new Set());
    },
  });

  function toggleExternalSelection(name: string) {
    setSelectedExternalNames((current) => {
      const next = new Set(current);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }

  return (
    <section aria-labelledby="tec-milenium-title" className="space-y-4">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-emerald-600 dark:text-emerald-300">
          2. Diseño de la experiencia de aprendizaje
        </p>
        <h2 id="tec-milenium-title" className="mt-1 text-xl font-semibold text-slate-950 dark:text-white">
          Tu ruta de aprendizaje personalizada
        </h2>
        <p className="mt-1 text-sm leading-6 text-slate-600 dark:text-white/70">
          Aprendizaje interactivo, adaptativo, apilable y personalizado: te recomendamos
          cursos y labs del catálogo del Tec para mejorar tus calificaciones y prepararte
          para el mercado laboral, y detectamos cursos que aún no tenemos para que el Tec
          los desarrolle.
        </p>
      </div>

      <Card className="p-5">
        <label htmlFor="tec-milenium-prompt" className="text-sm font-medium text-slate-700 dark:text-white/80">
          ¿En qué quieres enfocarte? (opcional)
        </label>
        <textarea
          id="tec-milenium-prompt"
          value={prompt}
          onChange={(event) => setPrompt(event.target.value)}
          rows={2}
          placeholder="Ej. Quiero mejorar mi calificación en Arquitecturas de IA y prepararme para trabajar en la industria de manufactura."
          className="mt-2 w-full rounded-xl border border-slate-200 p-3 text-sm dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
        />
        <button
          type="button"
          disabled={!personId || generateMutation.isPending}
          onClick={() => generateMutation.mutate()}
          className="mt-3 inline-flex items-center gap-2 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800 disabled:opacity-50"
        >
          <Sparkles className="h-4 w-4" aria-hidden="true" />
          {generateMutation.isPending ? "Diseñando tu ruta..." : "Diseñar mi ruta de aprendizaje"}
        </button>
        {generateMutation.isError && (
          <p className="mt-2 text-sm text-rose-700 dark:text-rose-300">
            No pudimos generar tu ruta en este momento.
          </p>
        )}
      </Card>

      {plan && (
        <div className="space-y-4">
          <Card className="border-emerald-200 bg-emerald-50/60 p-5 dark:border-emerald-400/20 dark:bg-emerald-400/[0.08]">
            <h3 className="font-semibold text-slate-950 dark:text-white">{plan.title}</h3>
            <p className="mt-2 text-sm leading-6 text-slate-700 dark:text-white/75">{plan.overallSummary}</p>
          </Card>

          <div>
            <h4 className="text-sm font-semibold uppercase tracking-wide text-slate-500 dark:text-white/60">
              Cursos del catálogo del Tec
            </h4>
            <div className="mt-2 grid gap-3 md:grid-cols-2">
              {plan.catalogCourses.map((course) => {
                const isEnrolled = enrolledIds.has(course.catalogItemId);
                return (
                  <Card key={course.catalogItemId} className="p-4">
                    <p className="font-semibold text-slate-900 dark:text-white">{course.name}</p>
                    <p className="mt-1 text-sm text-slate-600 dark:text-white/70">{course.reason}</p>
                    <button
                      type="button"
                      disabled={isEnrolled || acceptMutation.isPending}
                      onClick={() => acceptMutation.mutate(course)}
                      className="mt-3 inline-flex items-center gap-1.5 rounded-lg bg-blue-700 px-3 py-1.5 text-sm font-semibold text-white hover:bg-blue-800 disabled:opacity-50"
                    >
                      <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                      {isEnrolled ? "Inscrito" : "Inscribirme"}
                    </button>
                  </Card>
                );
              })}
              {plan.catalogCourses.length === 0 && (
                <p className="text-sm text-slate-500">No encontramos cursos del catálogo para recomendar.</p>
              )}
            </div>
          </div>

          <div>
            <h4 className="text-sm font-semibold uppercase tracking-wide text-slate-500 dark:text-white/60">
              Cursos que el Tec aún no tiene
            </h4>
            <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
              Selecciona uno o varios y envía una sola solicitud al Tec con todo el contenido.
            </p>
            <div className="mt-2 grid gap-3 md:grid-cols-2">
              {plan.externalCourses.map((course) => {
                const isRequested = requestedNames.has(course.name);
                const isSelected = selectedExternalNames.has(course.name);
                return (
                  <Card key={course.name} className="border-amber-200 p-4 dark:border-amber-400/20">
                    <label className="flex items-start gap-2">
                      <input
                        type="checkbox"
                        checked={isSelected}
                        disabled={isRequested}
                        onChange={() => toggleExternalSelection(course.name)}
                        className="mt-1"
                      />
                      <span>
                        <p className="font-semibold text-slate-900 dark:text-white">{course.name}</p>
                        <p className="mt-1 text-xs font-medium text-amber-700 dark:text-amber-300">{course.provider}</p>
                        <p className="mt-2 text-sm text-slate-600 dark:text-white/70">{course.reason}</p>
                      </span>
                    </label>
                    {isRequested && (
                      <p className="mt-3 text-sm font-semibold text-emerald-700 dark:text-emerald-300">
                        Solicitud enviada al Tec
                      </p>
                    )}
                  </Card>
                );
              })}
              {plan.externalCourses.length === 0 && (
                <p className="text-sm text-slate-500">No detectamos vacíos adicionales en el catálogo por ahora.</p>
              )}
            </div>
            {plan.externalCourses.length > 0 && (
              <button
                type="button"
                disabled={selectedExternalNames.size === 0 || requestMutation.isPending}
                onClick={() =>
                  requestMutation.mutate(
                    plan.externalCourses.filter((course) => selectedExternalNames.has(course.name)),
                  )
                }
                className="mt-3 inline-flex items-center gap-1.5 rounded-lg border border-amber-600 px-4 py-2 text-sm font-semibold text-amber-800 hover:bg-amber-50 disabled:opacity-50 dark:text-amber-200"
              >
                <Mail className="h-4 w-4" aria-hidden="true" />
                {requestMutation.isPending
                  ? "Enviando solicitud..."
                  : `Solicitar al Tec (${selectedExternalNames.size})`}
              </button>
            )}
            {requestMutation.isError && (
              <p className="mt-2 text-sm text-rose-700 dark:text-rose-300">
                No pudimos enviar la solicitud al Tec en este momento.
              </p>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
