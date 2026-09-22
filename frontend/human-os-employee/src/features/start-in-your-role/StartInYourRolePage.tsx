import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, BookOpen, BriefcaseBusiness, CheckCircle2, ChevronRight, Clock3, Cpu, Rocket, ShieldCheck, Sparkles, X, type LucideIcon } from 'lucide-react';
import { Link } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getMyCareerPlan } from '@/features/career-plan/api/careerPlanApi';
import { getJobRole } from '@/features/profile/api/jobRoleApi';
import { getPerson } from '@/features/profile/api/personProfileApi';
import { getSkills } from '@/features/capabilities/api/skillsApi';
import { getEnrolledCapabilities } from '@/features/capabilities/api/enrolledCapabilitiesApi';

const TECHNICAL_ROLE_REQUIREMENTS = {
  en: [
    { title: 'Systems and Solution Architecture', description: 'Learn how to translate business and user needs into secure, scalable, and maintainable technical solutions by connecting applications, data, APIs, cloud services, AI components, integrations, and operational requirements.' },
    { title: 'Data, AI, and Automation Practices', description: 'Learn how to prepare data, apply artificial intelligence, and automate workflows responsibly while maintaining quality, security, human oversight, measurable results, and reliable operation.' },
    { title: 'Company Security Policies', description: 'Learn how to protect company information, systems, devices, and workplaces by applying approved security practices, handling sensitive information appropriately, identifying risks, and reporting suspicious activity.' },
  ],
  es: [
    { title: 'Arquitectura de Sistemas y Soluciones', description: 'Aprende a traducir las necesidades del negocio y de los usuarios en soluciones técnicas seguras, escalables y mantenibles, conectando aplicaciones, datos, APIs, servicios en la nube, componentes de IA, integraciones y requisitos operativos.' },
    { title: 'Prácticas de Datos, IA y Automatización', description: 'Aprende a preparar datos, aplicar inteligencia artificial y automatizar flujos de trabajo de forma responsable, manteniendo la calidad, la seguridad, la supervisión humana, resultados medibles y una operación confiable.' },
    { title: 'Políticas de Seguridad de la Empresa', description: 'Aprende a proteger la información, los sistemas, los dispositivos y los espacios de trabajo de la empresa mediante prácticas de seguridad aprobadas, el manejo adecuado de información sensible, la identificación de riesgos y el reporte de actividades sospechosas.' },
  ],
} as const;

export function StartInYourRolePage() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === 'en';
  const personId = user?.personId ?? '';
  const [activeRequirement, setActiveRequirement] = useState<{ title: string; description: string; href?: string; external?: boolean; available?: boolean; engramProgress?: { mastered: number; total: number } } | null>(null);
  const planQuery = useQuery({
    queryKey: ['my-career-plan-start-in-your-role', personId],
    queryFn: () => getMyCareerPlan(personId),
    enabled: Boolean(personId),
  });
  const roleQuery = useQuery({
    queryKey: ['my-job-role-start-in-your-role', user?.jobRoleId],
    queryFn: () => getJobRole(user!.jobRoleId!),
    enabled: Boolean(user?.jobRoleId),
  });
  const personQuery = useQuery({
    queryKey: ['person-start-in-your-role', personId],
    queryFn: () => getPerson(personId),
    enabled: Boolean(personId),
  });
  const skillsQuery = useQuery({
    queryKey: ['skills', user?.tenantId, 'start-in-your-role'],
    queryFn: () => getSkills(user!.tenantId!),
    enabled: Boolean(user?.tenantId),
  });
  const enrolledQuery = useQuery({
    queryKey: ['enrolled-capabilities', personId, 'start-in-your-role'],
    queryFn: () => getEnrolledCapabilities(personId),
    enabled: Boolean(personId),
  });

  const plan = planQuery.data;
  const technical = plan?.technicalRoadmap.filter((step) => step.matchedCapabilityId || step.matchedProgramId) ?? [];
  const softSkills = plan?.softSkillsRoadmap.filter((step) => step.matchedCapabilityId || step.matchedProgramId) ?? [];
  void technical;
  void softSkills;
  const roleTitle = roleQuery.data?.title || plan?.jobRoleTitle || user?.jobRoleTitle || (isEn ? 'Your role' : 'Tu puesto');
  const dueDate = useMemo(() => {
    const createdDate = personQuery.data?.createdDate;
    if (!createdDate) return null;
    const date = new Date(createdDate);
    if (Number.isNaN(date.getTime())) return null;
    date.setDate(date.getDate() + 90);
    return new Intl.DateTimeFormat(isEn ? 'en-US' : 'es-MX', { dateStyle: 'medium' }).format(date);
  }, [isEn, personQuery.data?.createdDate]);
  const dueDateLabel = dueDate
    ? (isEn ? `Due by ${dueDate}` : `Fecha límite: ${dueDate}`)
    : (isEn ? 'Due within your first 90 days' : 'Fecha límite dentro de tus primeros 90 días');

  const requiredTraining = useMemo(() => {
    const enrolledByCapabilityId = new Map((enrolledQuery.data ?? []).map((item) => [item.capabilityId, item]));
    const excludedNames = ['information security', 'responsible use of ai', 'company security policies', 'data privacy and protection', 'data, ai, and automation practices'];
    const filtered = (skillsQuery.data ?? [])
      .filter((skill) => skill.assignmentScope === 'COMPANY_WIDE_ESSENTIAL')
      .filter((skill) => !excludedNames.some((excluded) => skill.name.trim().toLowerCase().startsWith(excluded)))
      .sort((a, b) => {
        const orderA = a.displayOrder ?? Number.MAX_SAFE_INTEGER;
        const orderB = b.displayOrder ?? Number.MAX_SAFE_INTEGER;
        return orderA - orderB || a.name.localeCompare(b.name);
      })
      .map((skill) => {
        const enrolled = skill.matchedCapabilityId ? enrolledByCapabilityId.get(skill.matchedCapabilityId) : undefined;
        return { skill, status: enrolled?.status, engramProgress: enrolled ? { mastered: enrolled.masteredNodes, total: enrolled.totalNodes } : undefined };
      });

    const sexualHarassmentSkill: (typeof filtered)[number]['skill'] = {
      skillId: 'sexual-harassment-prevention',
      name: isEn ? 'Sexual Harassment Prevention' : 'Prevención del Acoso Sexual',
      assignmentScope: 'COMPANY_WIDE_ESSENTIAL',
      displayOrder: -1,
      matchedCapabilityId: undefined,
    } as unknown as (typeof filtered)[number]['skill'];

    return [{ skill: sexualHarassmentSkill, status: undefined, engramProgress: undefined }, ...filtered];
  }, [enrolledQuery.data, isEn, skillsQuery.data]);

  if (planQuery.isLoading) {
    return <LoadingBackdrop title={isEn ? 'Preparing your first step...' : 'Preparando tu primer paso...'} description={isEn ? 'Loading your role and available courses.' : 'Cargando tu puesto y cursos disponibles.'} />;
  }

  const firstIncompleteTraining = requiredTraining.find(({ skill, status }) => !skill.matchedCapabilityId || status !== 'Completed');
  void firstIncompleteTraining;
  const metrics: { label: string; value: string; icon: LucideIcon }[] = [
    { label: isEn ? 'Role' : 'Puesto', value: roleTitle, icon: BriefcaseBusiness },
    { label: isEn ? 'HR & Compliance' : 'RR. HH. y cumplimiento', value: String(requiredTraining.length), icon: ShieldCheck },
    { label: isEn ? 'Technical capabilities' : 'Capacidades técnicas', value: String(TECHNICAL_ROLE_REQUIREMENTS[isEn ? 'en' : 'es'].length), icon: Cpu },
    { label: isEn ? 'Onboarding & Essentials' : 'Onboarding y esenciales', value: '2', icon: Rocket },
  ];

  return (
    <div className="min-h-screen bg-[#f5f7fb] pb-16 dark:bg-[#05060a]">
      <main className="mx-auto max-w-6xl px-6 pt-8 sm:pt-12">
        <Link to="/" className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300">
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          {isEn ? 'Back to your journey' : 'Volver a tu recorrido'}
        </Link>

        <section className="mt-6 overflow-hidden rounded-3xl bg-gradient-to-br from-sky-600 via-blue-600 to-indigo-700 p-7 text-white shadow-xl shadow-blue-900/20 sm:p-10">
          <div className="flex flex-wrap items-start justify-between gap-6">
            <div className="max-w-3xl">
              <div className="flex items-center gap-2 text-sm font-semibold text-blue-100">
                <Sparkles className="h-4 w-4" aria-hidden="true" />
                {isEn ? 'Your first 90 days' : 'Tus primeros 90 días'}
              </div>
              <h1 className="mt-4 text-3xl font-bold tracking-tight sm:text-5xl">{isEn ? 'Start in Your Role' : 'Comienza en tu rol'}</h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-blue-50 sm:text-lg">
                {isEn
                  ? 'Get to know the company, complete your essential training, and begin developing the knowledge required for your role.'
                  : 'Conoce la empresa, completa tu capacitación esencial y comienza a desarrollar los conocimientos que requiere tu puesto.'}
              </p>
            </div>
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
              <BriefcaseBusiness className="h-8 w-8" aria-hidden="true" />
            </div>
          </div>
        </section>

        <section className="mt-6 grid gap-4 sm:grid-cols-3">
          {metrics.map(({ label, value, icon: MetricIcon }) => {
            return (
              <Card key={String(label)} className="border-slate-200/80 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
                <MetricIcon className="h-5 w-5 text-blue-600 dark:text-blue-300" aria-hidden="true" />
                <p className="mt-4 text-xs font-semibold uppercase tracking-[0.16em] text-slate-400 dark:text-white/45">{label}</p>
                <p className="mt-2 text-lg font-bold text-slate-950 dark:text-white">{value}</p>
              </Card>
            );
          })}
        </section>

        <section className="mt-6 overflow-hidden rounded-3xl border border-amber-300/60 bg-gradient-to-br from-amber-500 via-orange-600 to-rose-600 shadow-xl shadow-orange-900/20">
          <div className="flex flex-col gap-6 p-7 text-white sm:flex-row sm:items-center sm:justify-between sm:p-8">
            <div className="flex items-start gap-5">
              <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/30 backdrop-blur">
                <Rocket className="h-8 w-8" aria-hidden="true" />
              </span>
              <div className="max-w-2xl">
                <span className="inline-flex items-center gap-1.5 rounded-full bg-white/15 px-3 py-1 text-[11px] font-bold uppercase tracking-[0.14em] ring-1 ring-white/30">
                  <Sparkles className="h-3.5 w-3.5" aria-hidden="true" />
                  {isEn ? 'Essential for your role' : 'Esencial para tu puesto'}
                </span>
                <h2 className="mt-3 text-2xl font-bold tracking-tight sm:text-3xl">
                  {isEn ? 'Forward Deployment Engineering (FDE)' : 'Forward Deployment Engineering (FDE)'}
                </h2>
                <p className="mt-2 text-sm leading-6 text-amber-50 sm:text-base">
                  {isEn
                    ? 'The core training path for your role: deploy, adapt, and support solutions directly in the field alongside customers.'
                    : 'La ruta de capacitación central para tu puesto: despliega, adapta y da soporte a soluciones directamente en campo junto a los clientes.'}
                </p>
              </div>
            </div>
            <button
              type="button"
              onClick={() => setActiveRequirement({
                title: isEn ? 'Forward Deployment Engineering (FDE)' : 'Forward Deployment Engineering (FDE)',
                description: isEn
                  ? 'Forward Deployment Engineering (FDE) is the essential training path for this role. It covers how to deploy, configure, and support solutions directly with customers in the field, translating real-world requirements into working implementations, troubleshooting issues on-site, and iterating quickly with product and engineering teams. Completing this course is a core requirement for your first 90 days.'
                  : 'Forward Deployment Engineering (FDE) es la ruta de capacitación esencial para este puesto. Cubre cómo desplegar, configurar y dar soporte a soluciones directamente con los clientes en campo, traduciendo requisitos reales en implementaciones funcionales, resolviendo problemas en sitio e iterando rápidamente con los equipos de producto e ingeniería. Completar este curso es un requisito central de tus primeros 90 días.',
                href: 'http://localhost:5180/structured-learning/173dd481-7bdf-4541-b0e4-662efbbd5c04',
                external: true,
                available: true,
              })}
              className="inline-flex shrink-0 items-center justify-center gap-2 rounded-xl bg-white px-6 py-3 text-sm font-bold text-orange-700 shadow-lg shadow-orange-900/20 transition hover:-translate-y-0.5 hover:bg-amber-50 focus:outline-none focus:ring-2 focus:ring-white focus:ring-offset-2 focus:ring-offset-orange-600"
            >
              {isEn ? 'View details' : 'Ver detalles'}
              <ArrowRight className="h-4 w-4" aria-hidden="true" />
            </button>
          </div>
        </section>

        <div className="mt-6 grid gap-4 lg:grid-cols-3">
        <section className="flex flex-col overflow-hidden rounded-2xl border border-indigo-200/70 bg-white shadow-sm shadow-indigo-900/5 dark:border-indigo-400/20 dark:bg-white/[0.04]" aria-labelledby="company-essentials-title">
          <div className="flex items-center gap-3 border-b border-indigo-100 bg-indigo-50/60 px-5 py-4 dark:border-white/5 dark:bg-indigo-500/10">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-indigo-600 text-white shadow-sm">
              <BookOpen className="h-4.5 w-4.5" aria-hidden="true" />
            </span>
            <div className="min-w-0">
              <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-indigo-600 dark:text-indigo-300">
                {isEn ? 'Company essentials' : 'Conoce la empresa'}
              </p>
              <h2 id="company-essentials-title" className="truncate text-sm font-bold text-slate-950 dark:text-white">
                {isEn ? 'Get to know how we work' : 'Comprende quiénes somos y cómo trabajamos'}
              </h2>
            </div>
          </div>
          <div className="flex-1 space-y-2 p-4">
            <button
              type="button"
              onClick={() => setActiveRequirement({
                title: 'Onboarding',
                description: isEn
                  ? 'This demo welcome guide describes who HermanScience is, what products and solutions it offers, who it serves, and the expected behaviors and processes for using AI at work. The document is organized into clear sections: "Who We Are", "What We Do", "Who We Serve", "How We Work", practical expectations, and a first-30-days plan with recommended resources. Its purpose is to orient new employees on values (Trust, Teamwork, Groundbreaking Approach), safe AI usage practices, and concrete steps to get integrated and start contributing. It also lists the company\'s solutions (for example HermanPrompt and HermanAgents) and flags operational resources that should be replaced with production data.'
                  : 'Esta guía de bienvenida demo describe quién es HermanScience, qué productos y soluciones ofrece, a quién sirve y las conductas y procesos esperados al usar IA en el trabajo. El documento está organizado en secciones claras: "Who We Are", "What We Do", "Who We Serve", "How We Work", expectativas prácticas y un plan de los primeros 30 días con recursos recomendados. Su propósito es orientar a nuevos empleados sobre valores (Trust, Teamwork, Groundbreaking Approach), prácticas seguras de uso de IA y pasos concretos para integrarse y empezar a contribuir. También lista las soluciones de la compañía (por ejemplo HermanPrompt y HermanAgents) y señala recursos operativos que deben reemplazarse por datos de producción.',
                href: '/structured-learning/b123fcff-0cb8-41a2-acd1-0797d57ad299',
                available: true,
              })}
              className="flex w-full items-center gap-3 rounded-xl border border-slate-200/70 bg-white p-3 text-left transition hover:-translate-y-0.5 hover:border-indigo-300 hover:shadow-md dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-indigo-400/40"
            >
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-emerald-100 text-emerald-700 dark:bg-emerald-400/15 dark:text-emerald-300">
                <CheckCircle2 className="h-4.5 w-4.5" aria-hidden="true" />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-semibold text-slate-900 dark:text-white">{isEn ? 'Onboarding' : 'Onboarding'}</span>
                <span className="mt-0.5 block text-[11px] font-semibold text-emerald-700 dark:text-emerald-300">{isEn ? 'Available' : 'Disponible'}</span>
              </span>
              <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
            </button>
          </div>
        </section>

        <section className="flex flex-col overflow-hidden rounded-2xl border border-violet-200/70 bg-white shadow-sm shadow-violet-900/5 dark:border-violet-400/20 dark:bg-white/[0.04]" aria-labelledby="required-training-title">
          <div className="flex items-center gap-3 border-b border-violet-100 bg-violet-50/60 px-5 py-4 dark:border-white/5 dark:bg-violet-500/10">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-violet-600 text-white shadow-sm">
              <ShieldCheck className="h-4.5 w-4.5" aria-hidden="true" />
            </span>
            <div className="min-w-0">
              <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-violet-600 dark:text-violet-300">
                {isEn ? 'HR & Compliance Essentials' : 'RR. HH. y cumplimiento'}
              </p>
              <h2 id="required-training-title" className="truncate text-sm font-bold text-slate-950 dark:text-white">
                {isEn ? 'Complete your required training' : 'Completa tu capacitación requerida'}
              </h2>
            </div>
          </div>
          <div className="flex-1 space-y-2 p-4">
            {requiredTraining.length === 0 ? (
              <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                {isEn ? 'Required training has not been assigned yet.' : 'La capacitación requerida aún no ha sido asignada.'}
              </p>
            ) : (
              requiredTraining.map(({ skill, status, engramProgress }) => {
                const statusLabel = status === 'Completed'
                  ? (isEn ? 'Completed' : 'Completado')
                  : status === 'InProgress'
                    ? (isEn ? 'In progress' : 'En progreso')
                    : skill.matchedCapabilityId
                      ? (isEn ? 'Not started' : 'Sin empezar')
                      : (isEn ? 'Coming soon' : 'Próximamente');
                const isDone = status === 'Completed';
                const engramLabel = engramProgress
                  ? (isEn ? `${engramProgress.mastered}/${engramProgress.total} engrams` : `${engramProgress.mastered}/${engramProgress.total} engramas`)
                  : null;
                return (
                  <button
                    key={skill.skillId}
                    type="button"
                    onClick={() => setActiveRequirement({
                      title: skill.name,
                      description: isEn
                        ? `Understand the policies, responsibilities, and standards covered by ${skill.name}.`
                        : `Comprende las políticas, responsabilidades y estándares cubiertos por ${skill.name}.`,
                      href: skill.matchedCapabilityId ? `/capabilities/${skill.matchedCapabilityId}` : undefined,
                      available: Boolean(skill.matchedCapabilityId),
                      engramProgress,
                    })}
                    className="flex w-full items-center gap-3 rounded-xl border border-slate-200/70 bg-white p-3 text-left transition hover:-translate-y-0.5 hover:border-violet-300 hover:shadow-md dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-violet-400/40"
                  >
                    <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${isDone ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-400/15 dark:text-emerald-300' : skill.matchedCapabilityId ? 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70' : 'bg-amber-100 text-amber-700 dark:bg-amber-400/15 dark:text-amber-300'}`}>
                      {isDone ? <CheckCircle2 className="h-4.5 w-4.5" aria-hidden="true" /> : skill.matchedCapabilityId ? <ShieldCheck className="h-4.5 w-4.5" aria-hidden="true" /> : <Clock3 className="h-4.5 w-4.5" aria-hidden="true" />}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-semibold text-slate-900 dark:text-white">{skill.name}</span>
                      <span className={`mt-0.5 flex items-center gap-1.5 text-[11px] font-semibold ${isDone ? 'text-emerald-700 dark:text-emerald-300' : skill.matchedCapabilityId ? 'text-slate-500 dark:text-white/55' : 'text-amber-700 dark:text-amber-300'}`}>
                        {statusLabel}
                        {engramLabel && <span className="text-slate-400 dark:text-white/40">· {engramLabel}</span>}
                      </span>
                    </span>
                    <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
                  </button>
                );
              })
            )}
          </div>
        </section>

        {/* Technical Skills for Your Role Card */}
        <section className="flex flex-col overflow-hidden rounded-2xl border border-sky-200/70 bg-white shadow-sm shadow-sky-900/5 dark:border-sky-400/20 dark:bg-white/[0.04]" aria-labelledby="technical-skills-title">
          <div className="flex items-center gap-3 border-b border-sky-100 bg-sky-50/60 px-5 py-4 dark:border-white/5 dark:bg-sky-500/10">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-sky-600 text-white shadow-sm">
              <Cpu className="h-4.5 w-4.5" aria-hidden="true" />
            </span>
            <div className="min-w-0">
              <p className="text-[11px] font-bold uppercase tracking-[0.14em] text-sky-600 dark:text-sky-300">
                {isEn ? 'Role Capabilities' : 'Capacidades del Puesto'}
              </p>
              <h2 id="technical-skills-title" className="truncate text-sm font-bold text-slate-950 dark:text-white">
                {isEn ? 'Technical Skills for Your Role' : 'Habilidades Técnicas'}
              </h2>
            </div>
          </div>
          <div className="flex-1 space-y-2 p-4">
            {TECHNICAL_ROLE_REQUIREMENTS[isEn ? 'en' : 'es'].map((requirement) => (
              <button
                key={requirement.title}
                type="button"
                onClick={() => setActiveRequirement(requirement)}
                className="flex w-full items-center gap-3 rounded-xl border border-slate-200/70 bg-white p-3 text-left transition hover:-translate-y-0.5 hover:border-sky-300 hover:shadow-md dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-sky-400/40"
              >
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-amber-100 text-amber-700 dark:bg-amber-400/15 dark:text-amber-300">
                  <Clock3 className="h-4.5 w-4.5" aria-hidden="true" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-semibold text-slate-900 dark:text-white">{requirement.title}</span>
                  <span className="mt-0.5 block text-[11px] font-semibold text-amber-700 dark:text-amber-300">{isEn ? 'Coming soon' : 'Próximamente'}</span>
                </span>
                <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
              </button>
            ))}
          </div>
        </section>
        </div>
      </main>

      {activeRequirement && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/50 p-4"
          role="dialog"
          aria-modal="true"
          aria-labelledby="requirement-modal-title"
          onClick={() => setActiveRequirement(null)}
        >
          <div
            className="w-full max-w-lg rounded-3xl bg-white p-6 shadow-2xl dark:bg-slate-900 sm:p-8"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4">
              <h3 id="requirement-modal-title" className="text-xl font-bold text-slate-950 dark:text-white">
                {activeRequirement.title}
              </h3>
              <button
                type="button"
                onClick={() => setActiveRequirement(null)}
                aria-label={isEn ? 'Close' : 'Cerrar'}
                className="shrink-0 rounded-full p-1 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-white/10 dark:hover:text-white"
              >
                <X className="h-5 w-5" aria-hidden="true" />
              </button>
            </div>
            <p className="mt-4 text-sm leading-6 text-slate-600 dark:text-white/70">{activeRequirement.description}</p>
            <div className="mt-5 flex items-center gap-2 text-xs font-semibold">
              {activeRequirement.available ? (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-100 px-2.5 py-1 text-emerald-800 dark:bg-emerald-400/15 dark:text-emerald-300">
                  <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                  {isEn ? 'Available' : 'Disponible'}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-100 px-2.5 py-1 text-amber-800 dark:bg-amber-400/15 dark:text-amber-300">
                  <Clock3 className="h-3.5 w-3.5" aria-hidden="true" />
                  {isEn ? 'Coming soon' : 'Próximamente'}
                </span>
              )}
              <span className="text-slate-400 dark:text-white/40">{dueDateLabel}</span>
            </div>
            {activeRequirement.engramProgress && (
              <div className="mt-3">
                <div className="flex items-center justify-between text-xs font-semibold text-slate-500 dark:text-white/55">
                  <span>{isEn ? 'Engrams mastered (tracked in Brain)' : 'Engramas dominados (registrado en Brain)'}</span>
                  <span>{activeRequirement.engramProgress.mastered}/{activeRequirement.engramProgress.total}</span>
                </div>
                <div className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-violet-500 to-indigo-600"
                    style={{ width: `${activeRequirement.engramProgress.total > 0 ? Math.round((activeRequirement.engramProgress.mastered / activeRequirement.engramProgress.total) * 100) : 0}%` }}
                  />
                </div>
              </div>
            )}
            {activeRequirement.available && activeRequirement.href ? (
              activeRequirement.external ? (
                <a
                  href={activeRequirement.href}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-6 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-indigo-600/20 transition hover:bg-indigo-700"
                >
                  {isEn ? 'Go to class' : 'Ir a la clase'}
                  <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </a>
              ) : (
                <Link
                  to={activeRequirement.href}
                  onClick={() => setActiveRequirement(null)}
                  className="mt-6 inline-flex w-full items-center justify-center gap-2 rounded-xl bg-indigo-600 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-indigo-600/20 transition hover:bg-indigo-700"
                >
                  {isEn ? 'Go to class' : 'Ir a la clase'}
                  <ArrowRight className="h-4 w-4" aria-hidden="true" />
                </Link>
              )
            ) : (
              <a
                href="#"
                onClick={(event) => event.preventDefault()}
                aria-disabled="true"
                className="mt-6 inline-flex w-full cursor-not-allowed items-center justify-center gap-2 rounded-xl bg-slate-200 px-5 py-3 text-sm font-semibold text-slate-500 dark:bg-white/10 dark:text-white/50"
              >
                {isEn ? 'Go to class · Coming soon' : 'Ir a la clase · Próximamente'}
                <ArrowRight className="h-4 w-4" aria-hidden="true" />
              </a>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
