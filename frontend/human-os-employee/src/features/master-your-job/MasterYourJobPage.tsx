import { ArrowLeft, ArrowRight, BookOpen, FlaskConical, GraduationCap, Sparkles } from 'lucide-react';
import { Link } from 'react-router';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

export function MasterYourJobPage() {
  const { language } = useI18n();
  const isEn = language === 'en';

  const courseTitle = isEn
    ? 'Global FDE Methodology (v2.0)'
    : 'Metodología Global FDE (v2.0)';

  const courseDefinition = isEn
    ? 'This "Global FDE Methodology" manual (v2.0) teaches a practical methodology for Forward-Deployed Engineering: from discovery through operation and product evolution, emphasizing Azure, IaC, GitHub, and responsible AI use (Claude). It is organized into fifteen parts (Foundations, Discovery, Modeling, Architecture, Azure/IaC, Security, GitHub, Claude, Module Design, Automation/AI, Responsible AI, AI Observability, Incremental Construction, Operations, and Governance) plus appendices with gates and templates. Each section explains what to do, why, who participates, how to execute it, expected artifacts, common pitfalls, and checkpoints to move forward with evidence. The purpose is to enable multidisciplinary teams to transform ambiguous human needs into operable, secure, and reusable capabilities, governed in repositories and ready to integrate AI only when it adds real value.'
    : 'Este manual "Metodología Global FDE" (v2.0) enseña una metodología práctica para Forward‑Deployed Engineering: desde el discovery hasta la operación y la evolución del producto, con énfasis en Azure, IaC, GitHub y uso responsable de IA (Claude). Está organizado en quince partes (Fundamentos, Descubrimiento, Modelado, Arquitectura, Azure/IaC, Seguridad, GitHub, Claude, Diseño de módulos, Automatización/IA, IA responsable, Observabilidad de IA, Construcción incremental, Operación y Gobierno) más apéndices con gates y plantillas. Cada sección explica qué hacer, por qué, quién participa, cómo ejecutarlo, artefactos esperados, errores comunes y puntos de control para avanzar con evidencia. El propósito es permitir que equipos multidisciplinares transformen necesidades humanas ambiguas en capacidades operables, seguras y reutilizables, gobernadas en repositorios y listas para integrar IA solo cuando aporte valor real.';

  return (
    <div className="min-h-screen bg-[#f5f7fb] pb-16 dark:bg-[#05060a]">
      <main className="mx-auto max-w-6xl px-6 pt-8 sm:pt-12">
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300"
        >
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          {isEn ? 'Back to your journey' : 'Volver a tu recorrido'}
        </Link>

        {/* Hero Banner */}
        <section className="mt-6 overflow-hidden rounded-3xl bg-gradient-to-br from-cyan-600 via-teal-600 to-blue-700 p-7 text-white shadow-xl shadow-cyan-900/20 sm:p-10">
          <div className="flex flex-wrap items-start justify-between gap-6">
            <div className="max-w-3xl">
              <div className="flex items-center gap-2 text-sm font-semibold text-cyan-100">
                <Sparkles className="h-4 w-4" aria-hidden="true" />
                {isEn ? 'Seniority & Mastery' : 'Madurez y Dominio'}
              </div>
              <h1 className="mt-4 text-3xl font-bold tracking-tight sm:text-5xl">
                {isEn ? 'Master your job' : 'Domina tu puesto'}
              </h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-cyan-50 sm:text-lg">
                {isEn
                  ? 'Reach peak performance in your role and prepare for leadership or specialization.'
                  : 'Alcanza el máximo desempeño en tu puesto y prepárate para el liderazgo o la especialización.'}
              </p>
              <div className="mt-4">
                <span className="inline-flex rounded-full bg-white/15 px-3 py-1 text-xs font-semibold text-white backdrop-blur">
                  {isEn ? 'Before your next chapter' : 'Antes de tu siguiente etapa'}
                </span>
              </div>
            </div>
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
              <GraduationCap className="h-8 w-8 text-white" aria-hidden="true" />
            </div>
          </div>
        </section>

        {/* Full-width Course Card */}
        <section className="mt-8 w-full">
          <div className="w-full rounded-2xl border border-slate-200/80 bg-white p-6 shadow-sm transition hover:border-cyan-300 hover:shadow-md dark:border-white/10 dark:bg-white/[0.03] sm:p-8">
            <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex-1 space-y-4">
                <div className="flex flex-wrap items-center gap-3">
                  <span className="inline-flex h-10 w-10 items-center justify-center rounded-xl bg-cyan-100 text-cyan-700 dark:bg-cyan-900/40 dark:text-cyan-300">
                    <BookOpen className="h-5 w-5" aria-hidden="true" />
                  </span>
                  <div>
                    <span className="text-xs font-semibold uppercase tracking-wider text-cyan-600 dark:text-cyan-400">
                      {isEn ? 'Structured Learning' : 'Aprendizaje Estructurado'}
                    </span>
                    <h2 className="text-xl font-bold text-slate-900 dark:text-white sm:text-2xl">
                      {courseTitle}
                    </h2>
                  </div>
                </div>

                <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300 sm:text-base">
                  {courseDefinition}
                </p>
              </div>

              <div className="shrink-0 pt-2 lg:pt-0">
                <Link
                  to="/structured-learning/173dd481-7bdf-4541-b0e4-662efbbd5c04"
                  className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-cyan-600 px-6 py-3.5 text-sm font-semibold text-white shadow-md shadow-cyan-600/20 transition hover:bg-cyan-500 focus:outline-none focus:ring-2 focus:ring-cyan-500 focus:ring-offset-2 dark:focus:ring-offset-slate-900 sm:w-auto"
                >
                  <span>{isEn ? 'Go to Course' : 'Ir al Curso'}</span>
                  <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </Link>
              </div>
            </div>
          </div>
        </section>

        {/* Learning Lab Simulation Card */}
        <section className="mt-6 w-full">
          <div className="w-full rounded-2xl border border-blue-200/80 bg-gradient-to-br from-white via-blue-50/30 to-indigo-50/20 p-6 shadow-sm transition hover:border-blue-300 hover:shadow-md dark:border-white/10 dark:bg-white/[0.03] dark:from-white/[0.02] dark:to-blue-950/20 sm:p-8">
            <div className="flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex-1 space-y-4">
                <div className="flex flex-wrap items-center gap-3">
                  <span className="inline-flex h-10 w-10 items-center justify-center rounded-xl bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300">
                    <FlaskConical className="h-5 w-5" aria-hidden="true" />
                  </span>
                  <div>
                    <span className="text-xs font-semibold uppercase tracking-wider text-blue-600 dark:text-blue-400">
                      {isEn ? 'Interactive Simulation Lab' : 'Lab de Simulación Interactiva'}
                    </span>
                    <h2 className="text-xl font-bold text-slate-900 dark:text-white sm:text-2xl">
                      {isEn
                        ? 'Ready for your next job: Practice your interview with this agent'
                        : 'Listo para tu siguiente trabajo: Practica tu entrevista con este agente'}
                    </h2>
                  </div>
                </div>

                <div className="space-y-1">
                  <h3 className="text-sm font-bold text-slate-800 dark:text-slate-200 sm:text-base">
                    Technical &amp; Behavioral Interview — Forward Deployed Engineer
                  </h3>
                  <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300 sm:text-base">
                    {isEn
                      ? 'Put your skills to the test with real-time voice AI interviewers evaluating your technical depth, architecture decisions, and behavioral competencies for the Forward Deployed Engineer role.'
                      : 'Pon a prueba tus habilidades con evaluadores de IA en tiempo real por voz que evaluarán tu profundidad técnica, decisiones de arquitectura y competencias conductuales para el rol de Forward Deployed Engineer.'}
                  </p>
                </div>
              </div>

              <div className="shrink-0 pt-2 lg:pt-0">
                <Link
                  to="/learning-labs/69e85d52-f225-4950-aa54-f392a427df85/attempts"
                  className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-blue-600 px-6 py-3.5 text-sm font-semibold text-white shadow-md shadow-blue-600/20 transition hover:bg-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 dark:focus:ring-offset-slate-900 sm:w-auto"
                >
                  <span>{isEn ? 'Practice Interview' : 'Practicar Entrevista'}</span>
                  <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </Link>
              </div>
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}
