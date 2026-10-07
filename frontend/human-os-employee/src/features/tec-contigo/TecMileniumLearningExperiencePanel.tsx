import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
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

/** Professional Growth Pathway panel — designs an interactive, adaptive,
 *  personalized development path aligned with the employee's role and company catalog. */
export function TecMileniumLearningExperiencePanel() {
  const { user } = useAuth();
  const { i18n } = useTranslation();
  const isEn = i18n.language?.startsWith('en');
  const companyName = user?.tenantName?.trim() || (isEn ? "your organization" : "tu empresa");
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
    <section aria-labelledby="learning-experience-title" className="space-y-4">
      <div>
        <p className="text-xs font-semibold uppercase tracking-wide text-emerald-600 dark:text-emerald-300">
          {isEn ? "Professional Growth Pathway" : "Ruta de desarrollo profesional"}
        </p>
        <h2 id="learning-experience-title" className="mt-1 text-xl font-semibold text-slate-950 dark:text-white">
          {isEn ? "Your Personalized Training & Growth Plan" : "Tu plan de capacitación y desarrollo"}
        </h2>
        <p className="mt-1 text-sm leading-6 text-slate-600 dark:text-white/70">
          {isEn
            ? `Continuous, adaptive, capability-driven learning: we recommend courses and labs available in ${companyName} to elevate your performance and accelerate your career growth, while identifying high-leverage areas for new topics.`
            : `Aprendizaje continuo, adaptativo y enfocado en capacidades: te recomendamos cursos y labs disponibles en ${companyName} para fortalecer tu desempeño y acelerar tu crecimiento profesional, e identificamos áreas clave para proponer nuevos temas.`}
        </p>
      </div>

      <Card className="p-5">
        <label htmlFor="learning-experience-prompt" className="text-sm font-medium text-slate-700 dark:text-white/80">
          {isEn ? "What skills or career goals do you want to focus on? (optional)" : "¿En qué habilidades o metas deseas enfocarte? (opcional)"}
        </label>
        <textarea
          id="learning-experience-prompt"
          value={prompt}
          onChange={(event) => setPrompt(event.target.value)}
          rows={2}
          placeholder={
            isEn
              ? "E.g. I want to strengthen my skills in technical leadership, software architectures, and AI-driven project management."
              : "Ej. Deseo fortalecer mis habilidades en liderazgo técnico, arquitecturas de software y gestión de proyectos con IA."
          }
          className="mt-2 w-full rounded-xl border border-slate-200 p-3 text-sm dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
        />
        <button
          type="button"
          disabled={!personId || generateMutation.isPending}
          onClick={() => generateMutation.mutate()}
          className="mt-3 inline-flex items-center gap-2 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800 disabled:opacity-50"
        >
          <Sparkles className="h-4 w-4" aria-hidden="true" />
          {generateMutation.isPending
            ? (isEn ? "Designing your plan..." : "Diseñando tu plan...")
            : (isEn ? "Generate My Growth Plan" : "Diseñar mi ruta de desarrollo")}
        </button>
        {generateMutation.isError && (
          <p className="mt-2 text-sm text-rose-700 dark:text-rose-300">
            {isEn ? "We could not generate your growth plan at this moment." : "No pudimos generar tu plan en este momento."}
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
              {isEn ? `Available Courses in ${companyName}` : `Cursos disponibles en ${companyName}`}
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
                      {isEnrolled ? (isEn ? "Enrolled" : "Inscrito") : (isEn ? "Enroll" : "Inscribirme")}
                    </button>
                  </Card>
                );
              })}
              {plan.catalogCourses.length === 0 && (
                <p className="text-sm text-slate-500">
                  {isEn ? "No matching catalog courses found to recommend." : "No encontramos cursos del catálogo para recomendar."}
                </p>
              )}
            </div>
          </div>

          <div>
            <h4 className="text-sm font-semibold uppercase tracking-wide text-slate-500 dark:text-white/60">
              {isEn ? "Recommended Additional Topics to Add" : "Temas complementarios recomendados"}
            </h4>
            <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
              {isEn
                ? `Select one or more topics to request their addition to ${companyName}'s catalog.`
                : `Selecciona uno o varios temas para solicitar su incorporación al catálogo de ${companyName}.`}
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
                        {isEn ? `Request sent to ${companyName}` : `Solicitud enviada a ${companyName}`}
                      </p>
                    )}
                  </Card>
                );
              })}
              {plan.externalCourses.length === 0 && (
                <p className="text-sm text-slate-500">
                  {isEn ? "No additional catalog gaps detected at this time." : "No detectamos vacíos adicionales en el catálogo por ahora."}
                </p>
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
                  ? (isEn ? "Sending request..." : "Enviando solicitud...")
                  : (isEn
                      ? `Request for ${companyName} (${selectedExternalNames.size})`
                      : `Solicitar a ${companyName} (${selectedExternalNames.size})`)}
              </button>
            )}
            {requestMutation.isError && (
              <p className="mt-2 text-sm text-rose-700 dark:text-rose-300">
                {isEn ? `We could not send your request to ${companyName} at this time.` : `No pudimos enviar la solicitud a ${companyName} en este momento.`}
              </p>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
