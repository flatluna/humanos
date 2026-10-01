import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  Award,
  BookOpen,
  BriefcaseBusiness,
  Calendar,
  CheckCircle2,
  Compass,
  Dumbbell,
  Eye,
  HeartHandshake,
  Lightbulb,
  RefreshCw,
  ShieldCheck,
  Sparkles,
  HelpCircle,
  Users,
  type LucideIcon,
} from "lucide-react";
import { Link } from "react-router";
import { useAuth } from "@/auth/AuthContext";
import { useI18n } from "@/features/capabilities/i18n/useI18n";
import {
  getMyProfile,
  type PersonProfileDto,
} from "@/features/profile/api/personProfileApi";
import {
  getMyResume,
  type PersonResumeDto,
} from "@/features/profile/api/resumeApi";
import { Card } from "@/components/ui/Card";
import { LoadingBackdrop } from "@/components/ui/LoadingBackdrop";
import { useBrainGraph } from "@/features/brain/useBrainGraph";
import { useKnowledgeNetwork } from "@/features/brain/useKnowledgeNetwork";
import { EngramDetailPanel } from "@/features/brain/EngramDetailPanel";
import type { SelectedNeuron } from "@/features/brain/types";
import { getRetrievability, getMemoryColor, getMemoryTextColor, isAutomatic } from "@/features/brain/memoryScience";
import { getEnrolledStructuredLearningDocuments } from "@/features/capabilities/api/enrolledStructuredLearningApi";
import { getGamificationProfile } from "@/features/gamification/api/gamificationApi";
import {
  demoTecPrograms,
  type DemoTecProgram,
} from "@/features/today/demoTecPrograms";
import {
  deleteThesis,
  downloadThesis,
  downloadThesisMarkdown,
  extractThesis,
  getThesisStatus,
  uploadThesis,
} from "./thesisApi";
import { TecContigoAgentChat } from "./TecContigoAgentChat";

type PageKey =
  "semestre" | "industria" | "objetivo" | "conocimientos" | "humanas";
type ExperienceKey = Exclude<PageKey, "objetivo">;
const pages: Record<
  PageKey,
  {
    path: string;
    category: string;
    title: string;
    description: string;
    color: string;
    icon: typeof BookOpen;
  }
> = {
  semestre: {
    path: "/tec-contigo/semestre-actual",
    category: "Ahora",
    title: "Tu semestre actual",
    description:
      "Prepárate para tus materias, proyectos y tesis antes de las evaluaciones finales.",
    color: "emerald",
    icon: BookOpen,
  },
  industria: {
    path: "/tec-contigo/industria-monterrey",
    category: "Industria",
    title: "Prepárate para la industria en Monterrey",
    description:
      "Conecta tu experiencia y tus capacidades con tecnologías y oportunidades relevantes para tu entorno profesional.",
    color: "blue",
    icon: BriefcaseBusiness,
  },
  objetivo: {
    path: "/tec-contigo/objetivo-profesional",
    category: "Tu siguiente etapa",
    title: "Alcanza tu objetivo profesional",
    description:
      "Convierte tu aspiración en una ruta de capacidades, programas y evidencia.",
    color: "violet",
    icon: Compass,
  },
  conocimientos: {
    path: "/tec-contigo/fortalecer-conocimientos",
    category: "Fortalecimiento",
    title: "Fortalece tus conocimientos",
    description:
      "Recupera y profundiza fundamentos relevantes para tu crecimiento.",
    color: "teal",
    icon: Lightbulb,
  },
  humanas: {
    path: "/tec-contigo/capacidades-humanas",
    category: "Impacto humano",
    title: "Desarrolla capacidades humanas",
    description:
      "Fortalece comunicación, liderazgo, colaboración y trabajo con clientes.",
    color: "orange",
    icon: HeartHandshake,
  },
};
const whileStudyingKeys: ExperienceKey[] = [
  "semestre",
  "industria",
  "conocimientos",
  "humanas",
];
const careerPlaceholderExperiences = [
  [
    "Prepárate para tu primer empleo",
    "Construye una base clara para presentar tu experiencia y capacidades.",
    "Perfil y evidencia",
  ],
  [
    "Prepárate para entrevistas",
    "Practica cómo explicar tus decisiones, capacidades y proyectos.",
    "Entrevistas",
  ],
  [
    "Conecta con oportunidades",
    "Explora oportunidades relacionadas con tu objetivo profesional.",
    "Oportunidades",
  ],
  [
    "Evalúa tu primera oportunidad",
    "Compara alternativas considerando aprendizaje, impacto y dirección.",
    "Decisión",
  ],
  [
    "Construye tus primeros 90 días",
    "Organiza tu transición y tus primeras prioridades profesionales.",
    "Primeros 90 días",
  ],
] as const;

function useTecProfile() {
  const { user } = useAuth();
  const personId = user?.personId ?? "";
  const profileQuery = useQuery<PersonProfileDto | null>({
    queryKey: ["my-profile", personId],
    queryFn: () => getMyProfile(personId),
    enabled: Boolean(personId),
  });
  const resumeQuery = useQuery<PersonResumeDto>({
    queryKey: ["my-resume", personId],
    queryFn: () => getMyResume(personId),
    enabled: Boolean(personId),
  });
  return { user, profileQuery, resumeQuery };
}

function clean(value: string | null | undefined) {
  return value?.trim() || "";
}
function profileText(resume: PersonResumeDto | undefined) {
  return [
    resume?.professionalObjective,
    resume?.professionalSummary,
    ...(resume?.skills ?? []),
    ...(resume?.workExperience ?? []),
    ...(resume?.education ?? []),
  ]
    .filter(Boolean)
    .join(" ");
}

type Tec360StageLink = {
  path: string;
  title: string;
  description: string;
  status: string;
  year: string;
  yearColor: string;
  icon: LucideIcon;
  active?: boolean;
};
const tec360StageLinks: Tec360StageLink[] = [
  {
    path: "/tec-contigo/mientras-estudias",
    title: "Mientras estudias",
    description:
      "Completa tu etapa académica, fortalece tus conocimientos y prepárate para tu siguiente oportunidad.",
    status: "Estás aquí",
    year: new Date().toLocaleDateString("es-MX", { day: "numeric", month: "long", year: "numeric" }),
    yearColor: "text-emerald-700 dark:text-emerald-300",
    icon: BookOpen,
    active: true,
  },
  {
    path: "/tec-contigo/comienza-tu-carrera",
    title: "Comienza tu carrera",
    description:
      "Convierte tu formación y evidencia en una transición profesional con dirección.",
    status: "Siguiente etapa",
    year: "2027",
    yearColor: "text-blue-700 dark:text-blue-300",
    icon: BriefcaseBusiness,
  },
  {
    path: "/tec-contigo/mantente-vigente",
    title: "Mantente vigente",
    description:
      "Actualiza capacidades y responde a los cambios de tu industria.",
    status: "A lo largo de tu carrera",
    year: "Siempre",
    yearColor: "text-amber-700 dark:text-amber-300",
    icon: RefreshCw,
  },
  {
    path: "/tec-contigo/practica-y-refuerza",
    title: "Practica y refuerza",
    description:
      "Repasa lo que ya aprendiste y aplica tus cursos terminados con práctica real generada por IA.",
    status: "Continuo",
    year: "Siempre",
    yearColor: "text-rose-700 dark:text-rose-300",
    icon: Dumbbell,
  },
  {
    path: "/tec-contigo/conecta-y-contribuye",
    title: "Conecta y contribuye",
    description:
      "Convierte tu experiencia en conocimiento, conexiones y oportunidades para la comunidad Tec.",
    status: "A lo largo de tu vida",
    year: "2032+",
    yearColor: "text-violet-700 dark:text-violet-300",
    icon: Users,
  },
];

function Tec360StageNavigation() {
  return (
    <section aria-labelledby="tec360-stage-title">
      <h3
        id="tec360-stage-title"
        className="text-2xl font-bold tracking-tight text-blue-600 dark:text-amber-400"
      >
        Momentos de vida
      </h3>
      <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        {tec360StageLinks.map((stage, index) => {
          const Icon = stage.icon;
          return (
            <Link
              key={stage.path}
              to={stage.path}
              aria-current={stage.active ? "page" : undefined}
              className={`rounded-2xl border p-4 transition hover:-translate-y-0.5 hover:shadow-md focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-600 ${stage.active ? "border-emerald-300 bg-emerald-50/70 dark:border-emerald-400/30 dark:bg-emerald-400/10" : "border-slate-200 bg-white dark:border-white/10 dark:bg-white/[0.03]"}`}
            >
              <div className="flex items-start justify-between gap-3">
                <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70">
                  <Icon className="h-4 w-4" aria-hidden="true" />
                </span>
                <span className="text-xs font-semibold text-slate-500 dark:text-white/55">
                  0{index + 1}
                </span>
              </div>
              <h4 className="mt-4 font-semibold text-slate-950 dark:text-white">
                {stage.title}
              </h4>
              <span
                className={`mt-1 inline-flex items-center gap-1.5 text-xs font-bold ${stage.yearColor}`}
              >
                <Calendar className="h-3.5 w-3.5" aria-hidden="true" />
                {stage.year}
              </span>
              <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/65">
                {stage.description}
              </p>
              <span
                className={`mt-4 inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${stage.active ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-400/20 dark:text-emerald-200" : "bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/65"}`}
              >
                {stage.status}
              </span>
              {(stage.title === "Mantente vigente" ||
                stage.title === "Practica y refuerza" ||
                stage.title === "Conecta y contribuye") && (
                <span className="mt-3 flex items-center gap-2 text-sm font-semibold text-blue-600 dark:text-blue-300">
                  Explorar esta etapa <ArrowRight className="h-4 w-4" />
                </span>
              )}
            </Link>
          );
        })}
      </div>
    </section>
  );
}

export { LearningExecutiveSummary };

export function TecContigoHub() {
  const { resumeQuery } = useTecProfile();
  const resume = resumeQuery.data;
  const academicStage =
    clean(resume?.studentAcademicProfile?.academicStage) ||
    "Etapa seleccionada";
  const generation = resume?.education
    ?.join(" ")
    .match(/\b((?:19|20)\d{2})\s*(?:–|-|—|a)\s*((?:19|20)\d{2})\b/i)?.[2];
  const stageDetail = academicStage.toLowerCase().includes("estudiante")
    ? academicStage
    : `Estudiante · ${academicStage}`;
  return (
    <section
      className="mx-auto max-w-6xl px-6 pt-8"
      aria-labelledby="tec-contigo-title"
    >
      <div className="mb-6 flex items-end justify-between gap-4">
        <div className="min-w-0 flex-1">
          <div className="mb-3 flex h-24 w-full items-center overflow-hidden">
            <img
              src="/tec360-logo.png"
              alt="Tec 360"
              className="h-40 w-auto max-w-none origin-left scale-150 object-contain object-left"
            />
          </div>
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-emerald-600 dark:text-emerald-300">
            Acompañamiento Tec
          </p>
          <h2
            id="tec-contigo-title"
            className="mt-2 text-2xl font-semibold text-slate-950 dark:text-white"
          >
            El Tec está contigo
          </h2>
          <p className="mt-1 max-w-3xl text-sm leading-6 text-slate-500 dark:text-white/60">
            Tu relación con el Tec evoluciona contigo. Te acompañamos mientras
            estudias, cuando comienzas tu carrera, cada vez que necesitas
            actualizarte y cuando estás listo para contribuir.
          </p>
        </div>
        <Link
          to="/tec-contigo"
          className="hidden shrink-0 text-sm font-semibold text-blue-600 sm:block"
        >
          Ver experiencia completa{" "}
          <ArrowRight className="ml-1 inline h-4 w-4" />
        </Link>
      </div>
      <Card className="mb-6 border-emerald-200 bg-emerald-50/60 p-5 dark:border-emerald-400/20 dark:bg-emerald-400/[0.08]">
        <p className="text-xs font-semibold uppercase tracking-wide text-emerald-700 dark:text-emerald-300">
          Tu etapa actual
        </p>
        <p className="mt-2 text-lg font-semibold text-slate-950 dark:text-white">
          {stageDetail}
        </p>
        {generation && (
          <p className="mt-1 text-sm text-slate-600 dark:text-white/65">
            Generación {generation}
          </p>
        )}
      </Card>
      <Tec360StageNavigation />
      <TecContigoAgentChat />
      <div className="mt-8 rounded-3xl border border-slate-800 bg-slate-900 p-6 text-white shadow-lg dark:border-white/10 dark:bg-slate-900/90">
        <h3 className="text-xl font-semibold">
          El Tec continúa contigo y tu experiencia continúa con la comunidad
        </h3>
        <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-300">
          La relación no termina cuando recibes un título o completas un
          programa. Con el tiempo, lo que has aprendido puede convertirse en
          orientación, colaboración y oportunidades para otras personas.
        </p>
        <div className="mt-5 flex flex-wrap gap-4">
          <Link
            to="/tec-contigo"
            className="inline-flex items-center gap-2 text-sm font-semibold text-blue-300 hover:text-blue-200"
          >
            Explorar etapas Tec Contigo <ArrowRight className="h-4 w-4" />
          </Link>
          <Link
            to="/tec-contigo/conecta-y-contribuye"
            className="inline-flex items-center gap-2 text-sm font-semibold text-blue-300 hover:text-blue-200"
          >
            Conecta y contribuye <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      </div>
    </section>
  );
}

function Status({ children }: { children: React.ReactNode }) {
  return (
    <Card className="p-6">
      <p className="text-sm text-slate-600 dark:text-white/70">{children}</p>
    </Card>
  );
}
function TecHeader({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  const isIndustry = title.includes("industria en Monterrey");
  return (
    <>
      <nav className="mb-6 flex flex-wrap items-center gap-2 text-sm text-slate-500">
        <Link to="/tec-contigo" className="hover:text-blue-600 font-medium">
          Tec Contigo
        </Link>
        <span>/</span>
        <span className="text-slate-900 dark:text-white font-medium">
          El Tec está contigo
        </span>
        {isIndustry && (
          <>
            <span>/</span>
            <span>Industria en Monterrey</span>
          </>
        )}
      </nav>
      <div className="mb-8">
        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-emerald-600 dark:text-emerald-300">
          El Tec está contigo
        </p>
        <h1 className="mt-3 max-w-4xl text-3xl font-semibold tracking-tight text-slate-950 dark:text-white">
          {title}
        </h1>
        <p className="mt-3 max-w-3xl text-base leading-7 text-slate-600 dark:text-white/70">
          {description}
        </p>
        <Link
          to="/tec-contigo"
          className="mt-5 inline-flex items-center rounded-lg border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/10 dark:text-white"
        >
          Volver a Tec Contigo
        </Link>
      </div>
    </>
  );
}
function TecPage({
  title,
  description,
  children,
}: {
  title: string;
  description: string;
  children: React.ReactNode;
}) {
  const { profileQuery, resumeQuery } = useTecProfile();
  if (profileQuery.isLoading || resumeQuery.isLoading)
    return (
      <LoadingBackdrop
        title="Cargando tu perfil"
        description="Estamos preparando tu experiencia personalizada."
      />
    );
  if (profileQuery.isError || resumeQuery.isError)
    return (
      <div className="mx-auto max-w-5xl">
        <TecHeader title={title} description={description} />
        <Status>
          <span>No pudimos cargar tu perfil en este momento.</span>{" "}
          <Link className="font-semibold text-blue-600" to="/">
            Volver a Today
          </Link>
        </Status>
      </div>
    );
  if (!profileQuery.data && !resumeQuery.data)
    return (
      <div className="mx-auto max-w-5xl">
        <TecHeader title={title} description={description} />
        <Status>
          Completa tu perfil para recibir una experiencia más personalizada.{" "}
          <Link className="font-semibold text-blue-600" to="/profile">
            Completar mi perfil
          </Link>
        </Status>
      </div>
    );
  return (
    <div className="mx-auto max-w-5xl">
      <TecHeader title={title} description={description} />
      {children}
      <TecContigoAgentChat />
    </div>
  );
}
function Temporary({ children }: { children: React.ReactNode }) {
  return (
    <Card className="border-dashed p-5">
      <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
        {children}
      </p>
    </Card>
  );
}

function Tec360StageSummary({
  resume,
}: {
  resume: PersonResumeDto | undefined;
}) {
  const semester = resume?.studentAcademicProfile?.semesters.find((item) =>
    /actual/i.test(item.name ?? ""),
  );
  const subjects = semester?.subjects.filter((item) => clean(item.name)) ?? [];
  const objective = clean(resume?.professionalObjective);
  const average = resume?.studentAcademicProfile?.cumulativeAverage;
  const stage = clean(resume?.studentAcademicProfile?.academicStage);
  const metrics = [
    ["Etapa", stage || "Etapa seleccionada"],
    ["Materias en curso", `${subjects.length}`],
    [
      "Objetivo profesional",
      /forward deployed engineer/i.test(objective)
        ? "Forward Deployed Engineer"
        : null,
    ],
    [
      "Promedio acumulado",
      average === null || average === undefined
        ? null
        : `${average}/${resume?.studentAcademicProfile?.averageScale ?? 100}`,
    ],
  ].filter((metric): metric is [string, string] => Boolean(metric[1]));
  return (
    <section aria-labelledby="tec360-summary-title">
      <h2
        id="tec360-summary-title"
        className="text-xl font-semibold text-slate-950 dark:text-white"
      >
        Tu momento actual
      </h2>
      <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {metrics.map(([label, value]) => (
          <Card key={label} className="p-4">
            <p className="text-xs text-slate-500 dark:text-white/50">{label}</p>
            <p className="mt-2 text-lg font-semibold text-slate-950 dark:text-white">
              {value}
            </p>
          </Card>
        ))}
      </div>
    </section>
  );
}

function TecContigoExperienceCards({
  resume,
  keys = whileStudyingKeys,
}: {
  resume: PersonResumeDto | undefined;
  keys?: ExperienceKey[];
}) {
  const text = profileText(resume).toLowerCase();
  const semester = resume?.studentAcademicProfile?.semesters.find((item) =>
    /actual/i.test(item.name ?? ""),
  );
  const subjects = semester?.subjects.filter((item) => clean(item.name)) ?? [];
  const objective = clean(resume?.professionalObjective);
  const indicators = [
    `${subjects.length} materias en curso`,
    text.includes("azure") ? "Experiencia con Azure" : null,
    /forward deployed engineer/i.test(objective)
      ? "Forward Deployed Engineer"
      : null,
    "Áreas sugeridas según tu trayectoria",
    "Capacidades esenciales para tu objetivo",
  ];
  const actions = [
    "Explorar acompañamiento",
    "Explorar industria",
    "Ver fundamentos",
    "Explorar capacidades",
  ];
  return (
    <section aria-labelledby="tec360-experiences-title">
      <h2
        id="tec360-experiences-title"
        className="text-xl font-semibold text-slate-950 dark:text-white"
      >
        Cómo te acompaña el Tec en esta etapa
      </h2>
      <p className="mt-1 text-sm text-slate-500 dark:text-white/65">
        Explora el acompañamiento disponible para completar tu etapa académica,
        fortalecer tus conocimientos y preparar tu transición profesional.
      </p>
      <div className="mt-4 grid gap-4 md:grid-cols-2">
        {keys.map((key, index) => {
          const page = pages[key];
          const Icon = page.icon;
          return (
            <Card
              key={key}
              className={`flex h-full min-h-64 flex-col border-t-4 border-${page.color}-500 p-5`}
            >
              <div className="flex items-start justify-between gap-3">
                <span
                  className={`inline-flex rounded-full bg-${page.color}-50 px-2.5 py-1 text-xs font-semibold text-${page.color}-700 dark:bg-${page.color}-400/10 dark:text-${page.color}-200`}
                >
                  {page.category}
                </span>
                <Icon
                  className={`h-5 w-5 text-${page.color}-600`}
                  aria-hidden="true"
                />
              </div>
              <h3 className="mt-4 text-lg font-semibold text-slate-950 dark:text-white">
                {page.title}
              </h3>
              <p className="mt-2 min-h-10 text-sm leading-6 text-slate-600 dark:text-white/70">
                {page.description}
              </p>
              {indicators[index] && (
                <p className="mt-4 text-xs font-medium text-slate-500 dark:text-white/50">
                  {indicators[index]}
                </p>
              )}
              <Link
                to={page.path}
                className="mt-auto inline-flex items-center gap-2 pt-5 text-sm font-semibold text-blue-600 dark:text-blue-300"
              >
                {actions[index]} <ArrowRight className="h-4 w-4" />
              </Link>
            </Card>
          );
        })}
      </div>
    </section>
  );
}

/** Below this retrievability, an engram counts as "needs review soon" —
 *  mirrors BrainPage's own FADING_THRESHOLD so the two views agree. */
const NEEDS_REVIEW_THRESHOLD = 0.5;

/** Executive summary of the student's Structured Learning progress: how
 *  many courses are still in progress, how many engrams (mastered
 *  sections) they've built up, how many are fading and due for a refresh
 *  soon, and their average assessment score across everything mastered.
 *  Reuses useBrainGraph (same data source as /brain) so the numbers here
 *  always match what the student sees on the Brain page. */
function LearningExecutiveSummary() {
  const { user } = useAuth();
  const { language } = useI18n();
  const isEn = language === "en";
  const personId = user?.personId ?? "";
  const [isHelpOpen, setIsHelpOpen] = useState(false);
  const { data: rootGroups = [] } = useBrainGraph(personId);
  const { data: enrolledDocs = [] } = useQuery({
    queryKey: ["enrolled-structured-learning", personId],
    queryFn: () => getEnrolledStructuredLearningDocuments(personId),
    enabled: Boolean(personId),
  });
  const { data: gamificationProfile } = useQuery({
    queryKey: ["gamification-profile", personId],
    queryFn: () => getGamificationProfile(),
    enabled: Boolean(personId),
  });

  const structuredLearningGroups = rootGroups.filter((root) =>
    root.clusters.some((cluster) => cluster.nodes.some((node) => "isStructuredLearning" in node)),
  );
  const masteredCountByDocument = new Map<string, number>();
  for (const root of structuredLearningGroups) {
    for (const cluster of root.clusters) {
      masteredCountByDocument.set(root.rootId, cluster.nodes.length);
    }
  }
  const coursesInProgress = enrolledDocs.filter(
    (doc) => (masteredCountByDocument.get(doc.structuredLearningDocumentId) ?? 0) < doc.sectionCount,
  ).length;

  const allNodes = rootGroups.flatMap((root) => root.clusters.flatMap((cluster) => cluster.nodes));
  const engramsCompleted = allNodes.length;
  const needsReviewSoon = allNodes.filter((node) => {
    const retrievability = getRetrievability(node.lastCompletedDate, node.reviewCount ?? 1);
    return retrievability !== null && retrievability < NEEDS_REVIEW_THRESHOLD;
  }).length;
  const scoredNodes = allNodes.filter((node) => typeof node.finalScore === "number");
  const averageScore =
    scoredNodes.length > 0
      ? Math.round(scoredNodes.reduce((sum, node) => sum + (node.finalScore ?? 0), 0) / scoredNodes.length)
      : null;

  return (
    <div>
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500 dark:text-white/60">
          {isEn ? "Your learning summary" : "Tu resumen de aprendizaje"}
        </h2>
        <button
          type="button"
          onClick={() => setIsHelpOpen(true)}
          className="flex items-center gap-1 rounded-full border border-slate-200 px-2.5 py-1 text-xs font-medium text-slate-600 hover:bg-slate-50 dark:border-white/15 dark:text-white/70 dark:hover:bg-white/[0.06]"
        >
          <HelpCircle className="h-3.5 w-3.5" aria-hidden="true" />
          {isEn ? "How do points and levels work?" : "¿Cómo funcionan los puntos y niveles?"}
        </button>
      </div>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <Card className="border-blue-200 bg-blue-50/60 p-5 dark:border-blue-400/20 dark:bg-blue-400/[0.08]">
        <div className="flex items-center gap-2 text-blue-700 dark:text-blue-300">
          <BookOpen className="h-5 w-5" aria-hidden="true" />
          <span className="text-xs font-semibold uppercase tracking-wide">{isEn ? "Courses in progress" : "Cursos en progreso"}</span>
        </div>
        <p className="mt-3 text-3xl font-bold text-slate-950 dark:text-white">{coursesInProgress}</p>
      </Card>
      <Card className="border-emerald-200 bg-emerald-50/60 p-5 dark:border-emerald-400/20 dark:bg-emerald-400/[0.08]">
        <div className="flex items-center gap-2 text-emerald-700 dark:text-emerald-300">
          <CheckCircle2 className="h-5 w-5" aria-hidden="true" />
          <span className="text-xs font-semibold uppercase tracking-wide">{isEn ? "Engrams completed" : "Engramas completados"}</span>
        </div>
        <p className="mt-3 text-3xl font-bold text-slate-950 dark:text-white">{engramsCompleted}</p>
        <p className="mt-1 text-xs text-slate-500 dark:text-white/60">
          {needsReviewSoon > 0
            ? isEn
              ? `${needsReviewSoon} need review soon`
              : `${needsReviewSoon} necesitan repaso pronto`
            : isEn
              ? "All with strong memory"
              : "Todos con memoria fuerte"}
        </p>
      </Card>
      <Card className="border-amber-200 bg-amber-50/60 p-5 dark:border-amber-400/20 dark:bg-amber-400/[0.08]">
        <div className="flex items-center gap-2 text-amber-700 dark:text-amber-300">
          <Award className="h-5 w-5" aria-hidden="true" />
          <span className="text-xs font-semibold uppercase tracking-wide">{isEn ? "Average score" : "Score promedio"}</span>
        </div>
        <p className="mt-3 text-3xl font-bold text-slate-950 dark:text-white">
          {averageScore !== null ? `${averageScore}%` : "—"}
        </p>
      </Card>
      <Link
        to="/achievements"
        className="block rounded-2xl border border-violet-200 bg-violet-50/60 p-5 transition hover:-translate-y-0.5 hover:shadow-md dark:border-violet-400/20 dark:bg-violet-400/[0.08]"
      >
        <div className="flex items-center gap-2 text-violet-700 dark:text-violet-300">
          <Sparkles className="h-5 w-5" aria-hidden="true" />
          <span className="text-xs font-semibold uppercase tracking-wide">{isEn ? "My achievements" : "Mis logros"}</span>
        </div>
        <p className="mt-3 text-3xl font-bold text-slate-950 dark:text-white">
          {gamificationProfile?.totalPoints ?? 0} <span className="text-sm font-medium text-slate-500 dark:text-white/60">pts</span>
        </p>
        <p
          className="mt-1 flex items-center gap-1 text-xs text-slate-500 dark:text-white/60"
          title={isEn
            ? "Level: rises as you accumulate points by completing and reviewing engrams (1 Spark → 2 Synapse → 3 Circuit → 4 Network → 5 Master Mind). Streak: consecutive days you completed or reviewed at least one engram; it breaks if you skip a day (unless you use a 'freeze')."
            : "Nivel: sube conforme acumulas puntos por completar y repasar engramas (1 Chispa → 2 Sinapsis → 3 Circuito → 4 Red → 5 Mente Maestra). Racha: días seguidos en que completaste o repasaste al menos un engrama; se rompe si pasas un día sin actividad (salvo que uses un 'freeze')."}
        >
          {gamificationProfile
            ? isEn
              ? `Level ${gamificationProfile.currentLevel}: ${gamificationProfile.currentLevelName} · streak ${gamificationProfile.currentStreakDays}d`
              : `Nivel ${gamificationProfile.currentLevel}: ${gamificationProfile.currentLevelName} · racha ${gamificationProfile.currentStreakDays}d`
            : isEn ? "View badges and streak" : "Ver insignias y racha"}
          <HelpCircle className="h-3 w-3 shrink-0" aria-hidden="true" />
        </p>
      </Link>
      </div>
      {isHelpOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4"
          onClick={() => setIsHelpOpen(false)}
        >
          <div
            className="max-h-[80vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl dark:bg-slate-900"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-semibold text-slate-950 dark:text-white">
                {isEn ? "How do points, levels and streak work?" : "¿Cómo funcionan los puntos, niveles y racha?"}
              </h3>
              <button
                type="button"
                onClick={() => setIsHelpOpen(false)}
                className="rounded-full p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                ✕
              </button>
            </div>
            <div className="mt-4 space-y-4 text-sm leading-6 text-slate-600 dark:text-white/70">
              <div>
                <p className="font-semibold text-slate-900 dark:text-white">{isEn ? "Points" : "Puntos"}</p>
                <p>
                  {isEn
                    ? "You earn points every time you pass the assessment for an engram (a course section). Points are calculated like this:"
                    : "Ganas puntos cada vez que apruebas la evaluación de un engram (sección de un curso). Los puntos se calculan así:"}
                </p>
                <p className="mt-1 rounded-lg bg-slate-50 p-2 font-mono text-xs dark:bg-white/[0.06]">
                  {isEn ? "Points = Base × Difficulty × Memory effort × Surprise" : "Puntos = Base × Dificultad × Esfuerzo de memoria × Sorpresa"}
                </p>
                <ul className="mt-2 list-disc space-y-1 pl-5">
                  {isEn ? (
                    <>
                      <li><strong>Base:</strong> ~50-90 pts depending on how important the topic is.</li>
                      <li><strong>Difficulty:</strong> ×1.0 to ×1.5 depending on course level.</li>
                      <li><strong>Memory effort:</strong> up to ×2.5 if you review something you'd almost forgotten.</li>
                      <li><strong>Surprise:</strong> random bonus of ×1.5, ×2, or ×3 (jackpot!) to keep it exciting.</li>
                    </>
                  ) : (
                    <>
                      <li><strong>Base:</strong> ~50-90 pts según qué tan importante es el tema.</li>
                      <li><strong>Dificultad:</strong> ×1.0 a ×1.5 según el nivel del curso.</li>
                      <li><strong>Esfuerzo de memoria:</strong> hasta ×2.5 si repasas algo que casi habías olvidado.</li>
                      <li><strong>Sorpresa:</strong> bono aleatorio de ×1.5, ×2 o ×3 (¡jackpot!) para mantener la sorpresa.</li>
                    </>
                  )}
                </ul>
              </div>
              <div>
                <p className="font-semibold text-slate-900 dark:text-white">{isEn ? "Levels" : "Niveles"}</p>
                <ul className="mt-1 list-disc space-y-1 pl-5">
                  {isEn ? (
                    <>
                      <li>Level 1 · Spark: 0-499 pts</li>
                      <li>Level 2 · Synapse: 500-1,499 pts</li>
                      <li>Level 3 · Circuit: 1,500-3,499 pts</li>
                      <li>Level 4 · Neural Network: 3,500-6,999 pts</li>
                      <li>Level 5 · Master Mind: 7,000+ pts</li>
                    </>
                  ) : (
                    <>
                      <li>Nivel 1 · Neurona: 0-499 pts</li>
                      <li>Nivel 2 · Sinapsis: 500-1,499 pts</li>
                      <li>Nivel 3 · Circuito: 1,500-3,499 pts</li>
                      <li>Nivel 4 · Red Neuronal: 3,500-6,999 pts</li>
                      <li>Nivel 5 · Mente Maestra: 7,000+ pts</li>
                    </>
                  )}
                </ul>
              </div>
              <div>
                <p className="font-semibold text-slate-900 dark:text-white">{isEn ? "Streak" : "Racha"}</p>
                <p>
                  {isEn
                    ? 'Consecutive days completing or reviewing at least one engram. It breaks if you skip a day, unless you use a "freeze" to protect it.'
                    : 'Días seguidos completando o repasando al menos un engram. Se rompe si pasas un día sin actividad, salvo que uses un "freeze" para protegerla.'}
                </p>
              </div>
              <Link
                to="/achievements"
                onClick={() => setIsHelpOpen(false)}
                className="inline-block text-sm font-semibold text-blue-600 dark:text-blue-300"
              >
                {isEn ? "View my badges and achievements →" : "Ver mis insignias y logros →"}
              </Link>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export function TecMientrasEstudiasPage() {
  const { resumeQuery } = useTecProfile();
  const resume = resumeQuery.data;
  const education = resume?.education?.join(" ") ?? "";
  const degree =
    clean(resume?.studentAcademicProfile?.degree) ||
    clean(education.match(/(?:—|-|–)\s*([^()\n]+?)\s*\(/)?.[1]);
  const stage =
    clean(resume?.studentAcademicProfile?.academicStage) ||
    clean(education.match(/[úu]ltimo\s+semestre|semestre\s+en\s+curso/i)?.[0]);
  const graduationYear = education.match(
    /\b((?:19|20)\d{2})\s*(?:–|-|—|a)\s*((?:19|20)\d{2})\b/i,
  )?.[2];
  const summary =
    stage && degree
      ? `Estás cursando tu ${stage.toLowerCase()} de ${degree}.`
      : null;
  return (
    <TecPage
      title="El Tec está contigo mientras estudias"
      description="Te acompañamos para completar tu etapa académica, fortalecer tus conocimientos y preparar tu transición hacia la vida profesional."
    >
      <div className="space-y-8">
        <nav
          aria-label="Breadcrumb"
          className="-mt-5 flex flex-wrap items-center gap-2 text-sm text-slate-500"
        >
          <Link to="/tec-contigo" className="hover:text-blue-600 font-medium">
            Tec Contigo
          </Link>
          <span>/</span>
          <span>Mientras estudias</span>
        </nav>
        <Card className="border-emerald-200 bg-emerald-50/60 p-6 dark:border-emerald-400/20 dark:bg-emerald-400/[0.08]">
          <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800 dark:bg-emerald-400/20 dark:text-emerald-200">
            Tu etapa actual
          </span>
          {summary && (
            <p className="mt-4 text-lg font-semibold text-slate-950 dark:text-white">
              {summary}
            </p>
          )}
          {graduationYear && (
            <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
              Generación {graduationYear}
            </p>
          )}
        </Card>
        <LearningExecutiveSummary />
        <Tec360StageSummary resume={resume} />
        <TecContigoExperienceCards resume={resume} />
        <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 text-white shadow-lg dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold">
            Esta es la primera etapa de tu relación de por vida con el Tec
          </h2>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-300">
            Cuando termines tus estudios, el acompañamiento evolucionará contigo
            para ayudarte a comenzar tu carrera, mantenerte vigente y contribuir
            a la comunidad.
          </p>
          <div className="mt-4 flex flex-wrap gap-2 text-xs text-slate-300">
            <span>Comienza tu carrera</span>
            <span>·</span>
            <span>Mantente vigente</span>
            <span>·</span>
            <span>Conecta y contribuye</span>
          </div>
          <Link
            to="/tec-contigo"
            className="mt-5 inline-flex text-sm font-semibold text-blue-300"
          >
            Ver las etapas de mi vida con el Tec{" "}
            <ArrowRight className="ml-1 inline h-4 w-4" />
          </Link>
        </div>
      </div>
    </TecPage>
  );
}

function TecObjectiveExperience({
  resume,
}: {
  resume: PersonResumeDto | undefined;
}) {
  const page = pages.objetivo;
  const objective = clean(resume?.professionalObjective);
  const isFde = /forward deployed engineer/i.test(objective);
  const role = isFde
    ? "Forward Deployed Engineer"
    : objective
      ? objective.split(/[,.\n]/)[0].trim()
      : "Tu objetivo profesional";
  return (
    <Card className="border-2 border-violet-300 p-6 dark:border-violet-400/40">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wide text-violet-700 dark:text-violet-300">
            Dirección profesional
          </p>
          <h3 className="mt-3 text-xl font-semibold text-slate-950 dark:text-white">
            {page.title}
          </h3>
        </div>
        <page.icon
          className="h-6 w-6 shrink-0 text-violet-600"
          aria-hidden="true"
        />
      </div>
      <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-600 dark:text-white/70">
        Convierte tu aspiración en una ruta de capacidades, programas,
        oportunidades y evidencia.
      </p>
      <p className="mt-4 text-lg font-semibold text-slate-950 dark:text-white">
        {isFde ? (
          <Link
            to="/structured-learning/173dd481-7bdf-4541-b0e4-662efbbd5c04"
            className="text-violet-700 underline decoration-violet-300 underline-offset-4 hover:text-violet-800 dark:text-violet-300"
          >
            {role}
          </Link>
        ) : (
          role
        )}
      </p>
      {isFde && (
        <p className="mt-1 text-sm text-slate-600 dark:text-white/70">
          IA aplicada, desarrollo de modelos, trabajo con clientes y despliegue
          de soluciones empresariales.
        </p>
      )}
      {isFde && (
        <Link
          to="/ai-agents/173dd481-7bdf-4541-b0e4-662efbbd5c04"
          className="mt-3 inline-flex items-center gap-1.5 text-sm font-semibold text-violet-700 hover:text-violet-800 dark:text-violet-300"
        >
          Hablar con el agente de {role} <ArrowRight className="h-4 w-4" />
        </Link>
      )}
      <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
        <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800 dark:bg-emerald-400/20 dark:text-emerald-200">
          Disponible ahora
        </span>
        <Link
          to={page.path}
          className="inline-flex items-center gap-2 text-sm font-semibold text-blue-600 dark:text-blue-300"
        >
          Explorar mi ruta <ArrowRight className="h-4 w-4" />
        </Link>
      </div>
    </Card>
  );
}

export function TecComienzaTuCarreraPage() {
  const { resumeQuery } = useTecProfile();
  const resume = resumeQuery.data;
  return (
    <TecPage
      title="El Tec está contigo para comenzar tu carrera"
      description="Convierte tu formación, experiencia y objetivo profesional en una transición con dirección. Prepárate para demostrar tu valor, conectar con oportunidades y generar impacto desde tus primeros meses."
    >
      <div className="space-y-8">
        <section>
          <p className="text-xs font-semibold uppercase tracking-wide text-violet-600 dark:text-violet-300">
            Tu siguiente etapa
          </p>
          <h2 className="mt-2 text-xl font-semibold text-slate-950 dark:text-white">
            Tu punto de partida
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            Puedes comenzar a prepararte antes de graduarte. Tu objetivo
            profesional orienta las decisiones que tomarás en esta transición.
          </p>
        </section>
        <section aria-labelledby="career-objective-title">
          <h2 id="career-objective-title" className="sr-only">
            Alcanza tu objetivo profesional
          </h2>
          <TecObjectiveExperience resume={resume} />
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Otras experiencias para comenzar tu carrera
          </h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {careerPlaceholderExperiences.map(
              ([title, description, category]) =>
                title === "Prepárate para entrevistas" ? (
                  <Card
                    key={title}
                    className="flex h-full flex-col border-t-4 border-amber-500 p-5"
                  >
                    <p className="text-xs font-semibold uppercase tracking-wide text-amber-700">
                      {category}
                    </p>
                    <h3 className="mt-3 text-lg font-semibold text-slate-950 dark:text-white">
                      {title}
                    </h3>
                    <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                      {description}
                    </p>
                    <p className="mt-3 text-xs leading-5 text-slate-500 dark:text-white/55">
                      Technical &amp; Behavioral Interview — Forward Deployed
                      Engineer
                    </p>
                    <Link
                      to="/learning-labs/69e85d52-f225-4950-aa54-f392a427df85/attempts"
                      className="mt-auto inline-flex items-center gap-2 pt-5 text-sm font-semibold text-blue-600 dark:text-blue-300"
                    >
                      Abrir simulación de entrevista{" "}
                      <ArrowRight className="h-4 w-4" />
                    </Link>
                  </Card>
                ) : (
                  <Temporary key={title}>
                    <span className="font-semibold text-slate-900 dark:text-white">
                      {title}
                    </span>
                    <span className="mt-1 block text-xs font-semibold uppercase tracking-wide text-slate-500">
                      {category}
                    </span>
                    <span className="mt-2 block">{description}</span>
                  </Temporary>
                ),
            )}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Tu objetivo orienta esta etapa
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            Tu preparación profesional, entrevistas, oportunidades y primeros 90
            días deben alinearse con el tipo de rol que quieres alcanzar.
          </p>
          <div className="mt-4 flex flex-wrap items-center gap-2 text-sm font-semibold text-slate-700 dark:text-white/80">
            <span>Objetivo profesional</span>
            <span aria-hidden="true">→</span>
            <span>Perfil y evidencia</span>
            <span aria-hidden="true">→</span>
            <span>Entrevistas</span>
            <span aria-hidden="true">→</span>
            <span>Oportunidades</span>
            <span aria-hidden="true">→</span>
            <span>Decisión</span>
            <span aria-hidden="true">→</span>
            <span>Primeros 90 días</span>
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Ruta de transición
          </h2>
          <Card className="mt-4 p-5">
            <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
              Conecta tu objetivo, evidencia, conversaciones y decisiones para
              comenzar con claridad.
            </p>
          </Card>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Primeros 90 días
          </h2>
          <Card className="mt-4 p-5">
            <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
              Define prioridades iniciales, aprendizajes y señales de impacto
              para tu transición.
            </p>
          </Card>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Certificaciones y preparación
          </h2>
          <Card className="mt-4 p-5">
            <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
              Selecciona preparación que refuerce las capacidades y evidencia
              relacionadas con tu objetivo.
            </p>
          </Card>
        </section>
        <div className="rounded-3xl border border-slate-900/10 bg-slate-900 p-6 text-white shadow-sm dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold text-white">
            El Tec continúa contigo en tu siguiente etapa
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-200">
            Tu formación, experiencia y objetivo profesional pueden convertirse
            en una transición con dirección.
          </p>
        </div>
      </div>
    </TecPage>
  );
}

type VigenciaExperience = {
  number: string;
  category: string;
  title: string;
  description: string;
  topics: string[];
  indicator: string;
  action: string;
  color: string;
  icon: LucideIcon;
};

const vigenciaExperiences: VigenciaExperience[] = [
  {
    number: "01",
    category: "Señales de cambio",
    title: "Radar de tu industria",
    description:
      "Descubre tecnologías, prácticas y capacidades emergentes relacionadas con tu perfil profesional.",
    topics: [
      "Agentic AI",
      "Evaluación de aplicaciones LLM",
      "Observabilidad de IA",
      "Seguridad de IA generativa",
      "MLOps",
      "IA aplicada a manufactura",
    ],
    indicator: "Señales relacionadas con tu perfil",
    action: "Explorar mi radar",
    color: "blue",
    icon: Eye,
  },
  {
    number: "02",
    category: "Actualización profesional",
    title: "Actualiza tus capacidades",
    description:
      "Identifica qué conocimientos conviene recuperar, actualizar o profundizar conforme evoluciona tu entorno profesional.",
    topics: [
      "Azure e inteligencia artificial",
      "Arquitectura de aplicaciones LLM",
      "MLOps",
      "Seguridad de IA",
      "Evaluación y observabilidad",
    ],
    indicator: "Plan de actualización continua",
    action: "Ver mi plan de actualización",
    color: "teal",
    icon: RefreshCw,
  },
  {
    number: "03",
    category: "Profundización",
    title: "Construye tu especialización",
    description:
      "Convierte tu experiencia acumulada en un dominio profesional más profundo y diferenciador.",
    topics: [
      "IA empresarial",
      "Machine Learning en producción",
      "Arquitectura de aplicaciones LLM",
      "IA para manufactura avanzada",
      "Forward-Deployed Engineering",
    ],
    indicator: "Rutas de especialización",
    action: "Explorar especializaciones",
    color: "violet",
    icon: Compass,
  },
  {
    number: "04",
    category: "Evolución profesional",
    title: "Navega tu siguiente transición",
    description:
      "Actualiza tu ruta cuando cambien tu rol, empresa, industria, responsabilidades o prioridades.",
    topics: [
      "Applied AI Engineer",
      "Forward Deployed Engineer",
      "AI Solution Architect",
      "AI Transformation Lead",
    ],
    indicator: "Ruta profesional adaptable",
    action: "Preparar mi siguiente transición",
    color: "orange",
    icon: ArrowRight,
  },
  {
    number: "05",
    category: "Liderazgo",
    title: "Desarrolla liderazgo",
    description:
      "Prepárate para coordinar equipos, facilitar decisiones, comunicar prioridades y liderar transformación.",
    topics: [
      "Liderazgo tecnológico",
      "Comunicación ejecutiva",
      "Decisiones bajo incertidumbre",
      "Gestión de stakeholders",
      "Gobierno de IA",
      "Desarrollo de equipos",
    ],
    indicator: "Preparación para nuevas responsabilidades",
    action: "Explorar liderazgo",
    color: "emerald",
    icon: ShieldCheck,
  },
];

const vigenciaCycle = [
  [
    "Observa",
    "Identifica cambios relevantes en tecnologías, prácticas, roles e industrias.",
  ],
  [
    "Evalúa",
    "Determina qué cambios son relevantes para tu contexto, objetivo y responsabilidades.",
  ],
  [
    "Actualiza",
    "Recupera o desarrolla las capacidades que necesitas para responder al cambio.",
  ],
  [
    "Aplica",
    "Utiliza el conocimiento en una decisión, proyecto, mejora o nueva responsabilidad.",
  ],
  [
    "Demuestra",
    "Produce evidencia de que puedes utilizar la capacidad con autonomía y generar valor.",
  ],
];
const annualPractice = [
  [
    "Trimestre 1",
    "Revisa tu contexto",
    "Actualiza tu rol, responsabilidades, proyectos, herramientas y objetivos.",
  ],
  [
    "Trimestre 2",
    "Selecciona una capacidad",
    "Prioriza una capacidad relevante en lugar de intentar aprender todo al mismo tiempo.",
  ],
  [
    "Trimestre 3",
    "Aplica y produce evidencia",
    "Utiliza la capacidad en un proyecto, decisión, mejora o responsabilidad real.",
  ],
  [
    "Trimestre 4",
    "Evalúa y actualiza tu ruta",
    "Reflexiona sobre los resultados y define tu siguiente prioridad.",
  ],
];
const evidenceItems = [
  "Decisión tecnológica documentada",
  "Arquitectura actualizada",
  "Proyecto aplicado",
  "Evaluación o benchmark",
  "Plan de seguridad",
  "Estrategia de observabilidad",
  "Presentación de resultados",
  "Reflexión sobre aprendizaje",
  "Capacidad demostrada en un nuevo contexto",
];

function VigenciaPanel({
  experience,
  onClose,
}: {
  experience: VigenciaExperience;
  onClose: () => void;
}) {
  const closeRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    closeRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKeyDown);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = "";
    };
  }, [onClose]);
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="vigencia-panel-title"
    >
      <div className="max-h-[90vh] w-full max-w-xl overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900">
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-emerald-700 dark:text-emerald-300">
              {experience.category}
            </p>
            <h2
              id="vigencia-panel-title"
              className="mt-2 text-xl font-semibold text-slate-950 dark:text-white"
            >
              {experience.title}
            </h2>
          </div>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            className="rounded-lg px-2 text-xl text-slate-500 hover:bg-slate-100 dark:hover:bg-white/10"
            aria-label="Cerrar"
          >
            ×
          </button>
        </div>
        <p className="mt-5 text-sm leading-6 text-slate-600 dark:text-white/70">
          {experience.description}
        </p>
        <h3 className="mt-5 font-semibold text-slate-950 dark:text-white">
          Qué incluirá
        </h3>
        <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-white/70">
          {experience.topics.map((topic) => (
            <li key={topic}>{topic}</li>
          ))}
        </ul>
        <p className="mt-5 rounded-xl bg-emerald-50 p-4 text-sm text-emerald-900 dark:bg-emerald-400/10 dark:text-emerald-100">
          <strong>Estado:</strong> Se desarrollará en el siguiente paso.
        </p>
      </div>
    </div>
  );
}

export function TecMantenteVigentePage() {
  const { resumeQuery } = useTecProfile();
  const [selectedExperience, setSelectedExperience] =
    useState<VigenciaExperience | null>(null);
  const resume = resumeQuery.data;
  const text = profileText(resume).toLowerCase();
  const objective = clean(resume?.professionalObjective);
  const prioritySkills = [
    "Python",
    "Machine Learning",
    "Deep Learning",
    "Microsoft Azure",
    "Azure OpenAI",
    "Aplicaciones LLM",
    "Prompt Engineering",
    "Power BI",
  ];
  const skills = prioritySkills
    .filter((skill) =>
      (resume?.skills ?? []).some((item) =>
        item.toLowerCase().includes(skill.toLowerCase()),
      ),
    )
    .slice(0, 6);
  const hasManufacturing = /nemak|mantenimiento predictivo|manufactura/i.test(
    text,
  );
  const interests = [
    "Inteligencia artificial",
    "Machine Learning",
    "Nube",
    "Manufactura avanzada",
    "Transformación digital",
  ].filter(
    (interest) =>
      text.includes(
        interest.toLowerCase().replace("inteligencia artificial", "ia"),
      ) ||
      (interest === "Nube" && /azure|cloud|nube/i.test(text)) ||
      (interest === "Inteligencia artificial" &&
        /ia|artificial intelligence|machine learning/i.test(text)),
  );
  return (
    <TecPage
      title="El Tec está contigo para mantenerte vigente"
      description="Conectamos tu perfil, tu experiencia y los cambios de tu industria para ayudarte a actualizar capacidades, construir una especialización y prepararte para nuevas responsabilidades."
    >
      <div className="space-y-10">
        <nav
          aria-label="Breadcrumb"
          className="-mt-5 flex flex-wrap items-center gap-2 text-sm text-slate-500"
        >
          <Link to="/tec-contigo" className="hover:text-blue-600 font-medium">
            Tec Contigo
          </Link>
          <span>/</span>
          <span>Mantente vigente</span>
        </nav>
        <Card className="border-emerald-200 bg-emerald-50/60 p-6 dark:border-emerald-400/20 dark:bg-emerald-400/[0.08]">
          <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800 dark:bg-emerald-400/20 dark:text-emerald-200">
            A lo largo de tu carrera
          </span>
          <p className="mt-4 text-sm leading-6 text-slate-700 dark:text-white/75">
            Puedes comenzar desde ahora a desarrollar una práctica de
            aprendizaje y actualización continua.
          </p>
        </Card>
        <section aria-labelledby="vigencia-start-title">
          <h2
            id="vigencia-start-title"
            className="text-xl font-semibold text-slate-950 dark:text-white"
          >
            Tu punto de partida
          </h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <Card className="p-5">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                Objetivo profesional
              </p>
              <p className="mt-2 text-lg font-semibold text-slate-950 dark:text-white">
                {/forward deployed engineer/i.test(objective)
                  ? "Forward Deployed Engineer"
                  : objective
                    ? objective.split(/[,\.\n]/)[0]
                    : "Completa tu perfil para conectar esta etapa con tu objetivo."}
              </p>
            </Card>
            <Card className="p-5">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                Capacidades actuales
              </p>
              {skills.length ? (
                <div className="mt-3 flex flex-wrap gap-2">
                  {skills.map((skill) => (
                    <span
                      key={skill}
                      className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-700 dark:bg-white/10 dark:text-white/75"
                    >
                      {skill}
                    </span>
                  ))}
                </div>
              ) : (
                <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
                  Completa tu perfil para conectar esta etapa con tus
                  capacidades y experiencia.{" "}
                  <Link to="/profile" className="font-semibold text-blue-600">
                    Completar mi perfil
                  </Link>
                </p>
              )}
            </Card>
            {hasManufacturing && (
              <Card className="p-5">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                  Experiencia relevante
                </p>
                <p className="mt-2 text-sm leading-6 text-slate-700 dark:text-white/75">
                  IA aplicada a manufactura y mantenimiento predictivo
                </p>
              </Card>
            )}
            {interests.length > 0 && (
              <Card className="p-5">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                  Áreas de interés
                </p>
                <div className="mt-3 flex flex-wrap gap-2">
                  {interests.map((interest) => (
                    <span
                      key={interest}
                      className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-semibold text-blue-700 dark:bg-blue-400/10 dark:text-blue-200"
                    >
                      {interest}
                    </span>
                  ))}
                </div>
              </Card>
            )}
          </div>
        </section>
        <section aria-labelledby="ai-intro-course-title">
          <h2
            id="ai-intro-course-title"
            className="text-xl font-semibold text-slate-950 dark:text-white"
          >
            Curso recomendado para mantenerte vigente
          </h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/65">
            Fundamentos y actualización continua en inteligencia artificial para
            tu perfil profesional.
          </p>
          <div className="mt-4">
            <Link
              to="/structured-learning/eac67e4c-b0db-47e5-8966-ee86160de5d9"
              className="group block cursor-pointer"
            >
              <Card className="flex flex-col justify-between border-l-4 border-l-teal-500 p-6 transition-all duration-200 hover:border-teal-400 hover:shadow-md dark:hover:border-teal-400">
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <div className="space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="rounded-full bg-teal-50 px-2.5 py-0.5 text-xs font-semibold text-teal-700 dark:bg-teal-400/10 dark:text-teal-300">
                        Inteligencia Artificial
                      </span>
                      <span className="text-xs text-slate-400">·</span>
                      <span className="text-xs text-slate-500 dark:text-white/60">
                        Estructurado
                      </span>
                    </div>
                    <h3 className="text-lg font-semibold text-slate-950 group-hover:text-teal-600 dark:text-white dark:group-hover:text-teal-300">
                      AI Introduction
                    </h3>
                    <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
                      Aprende los principios fundamentales de la inteligencia
                      artificial, casos de uso prácticos y cómo aplicarla en
                      escenarios del mundo real.
                    </p>
                  </div>
                  <div className="mt-4 flex shrink-0 items-center gap-2 text-sm font-semibold text-teal-600 group-hover:translate-x-1 transition-transform dark:text-teal-400 sm:mt-0">
                    <span>Iniciar curso</span>
                    <ArrowRight className="h-4 w-4" />
                  </div>
                </div>
              </Card>
            </Link>
          </div>
        </section>
        <section aria-labelledby="vigencia-experiences-title">
          <h2
            id="vigencia-experiences-title"
            className="text-xl font-semibold text-slate-950 dark:text-white"
          >
            Cómo te acompaña el Tec para mantenerte vigente
          </h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/65">
            Explora distintas formas de actualizarte, profundizar tu experiencia
            y prepararte para tu siguiente etapa.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {vigenciaExperiences.map((experience) => {
              const Icon = experience.icon;
              return (
                <Card
                  key={experience.number}
                  className="flex min-h-72 flex-col border-t-4 border-emerald-500 p-5"
                >
                  <div className="flex items-start justify-between gap-3">
                    <span className="text-xs font-bold text-slate-500">
                      {experience.number}
                    </span>
                    <Icon
                      className="h-5 w-5 text-emerald-600"
                      aria-hidden="true"
                    />
                  </div>
                  <p className="mt-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {experience.category}
                  </p>
                  <h3 className="mt-2 text-lg font-semibold text-slate-950 dark:text-white">
                    {experience.title}
                  </h3>
                  <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                    {experience.description}
                  </p>
                  <p className="mt-3 text-xs font-medium text-slate-500">
                    {experience.indicator}
                  </p>
                  <button
                    type="button"
                    onClick={() => setSelectedExperience(experience)}
                    className="mt-auto inline-flex items-center gap-2 pt-5 text-left text-sm font-semibold text-blue-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600 dark:text-blue-300"
                  >
                    {experience.action}
                    <ArrowRight className="h-4 w-4" />
                  </button>
                </Card>
              );
            })}
          </div>
        </section>
        <section aria-labelledby="vigencia-cycle-title">
          <h2
            id="vigencia-cycle-title"
            className="text-xl font-semibold text-slate-950 dark:text-white"
          >
            Tu ciclo de actualización profesional
          </h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/65">
            Mantenerte vigente no significa acumular cursos. Significa observar,
            decidir, aprender, aplicar y demostrar.
          </p>
          <div className="mt-4 grid gap-3 md:grid-cols-5">
            {vigenciaCycle.map(([title, description], index) => (
              <Card key={title} className="p-4">
                <span className="text-xs font-bold text-emerald-600">
                  0{index + 1}
                </span>
                <h3 className="mt-2 font-semibold text-slate-950 dark:text-white">
                  {title}
                </h3>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {description}
                </p>
              </Card>
            ))}
          </div>
          <p className="mt-3 text-xs text-slate-500">
            Ciclo conceptual para esta experiencia.
          </p>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Señales que podrían ser relevantes para tu perfil
          </h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/65">
            Ejemplos de cómo Human OS puede conectar cambios tecnológicos con tu
            experiencia.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {vigenciaExperiences[0].topics.map((topic) => (
              <Card key={topic} className="p-5">
                <h3 className="font-semibold text-slate-950 dark:text-white">
                  {topic}
                </h3>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  Conecta con capacidades, experiencia y decisiones de
                  arquitectura profesional.
                </p>
                <p className="mt-3 text-xs font-medium text-blue-600">
                  Explorar fundamentos y patrones de arquitectura.
                </p>
              </Card>
            ))}
          </div>
          <p className="mt-3 text-xs text-slate-500">
            Contenido conceptual. Las señales no representan un análisis de
            mercado en tiempo real.
          </p>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Una práctica anual para mantenerte vigente
          </h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-4">
            {annualPractice.map(([quarter, title, description]) => (
              <Card key={quarter} className="p-5">
                <p className="text-xs font-bold uppercase tracking-wide text-emerald-600">
                  {quarter}
                </p>
                <h3 className="mt-2 font-semibold text-slate-950 dark:text-white">
                  {title}
                </h3>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {description}
                </p>
              </Card>
            ))}
          </div>
          <p className="mt-3 text-xs text-slate-500">
            Ejemplo de práctica anual.
          </p>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Haz visible tu evolución
          </h2>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-600 dark:text-white/70">
            Mantenerte vigente no depende solamente de completar contenido.
            Requiere demostrar que puedes comprender un cambio, aplicarlo y
            generar valor.
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            {evidenceItems.map((item) => (
              <span
                key={item}
                className="rounded-full border border-slate-200 px-3 py-1.5 text-xs text-slate-700 dark:border-white/10 dark:text-white/70"
              >
                {item}
              </span>
            ))}
          </div>
        </section>
        <div className="rounded-3xl border border-slate-900/10 bg-slate-900 p-6 text-white shadow-sm dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold text-white">
            El Tec continúa contigo cuando el mundo cambia
          </h2>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-300">
            Tu título representa una etapa importante, pero tu crecimiento
            continúa. El Tec puede ayudarte a comprender qué está cambiando,
            decidir qué aprender y convertir nuevas capacidades en resultados
            visibles.
          </p>
          <Link
            to="/tec-contigo"
            className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-blue-300"
          >
            Volver a las etapas Tec 360 <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      </div>
      {selectedExperience && (
        <VigenciaPanel
          experience={selectedExperience}
          onClose={() => setSelectedExperience(null)}
        />
      )}
    </TecPage>
  );
}

type ContributionExperience = {
  number: string;
  category: string;
  title: string;
  description: string;
  topics: string[];
  indicator: string;
  action: string;
  icon: LucideIcon;
};
const contributionExperiences: ContributionExperience[] = [
  {
    number: "01",
    category: "Comunidad",
    title: "Conecta con la comunidad Tec",
    description:
      "Encuentra personas, comunidades, eventos y conversaciones relacionadas con tus intereses, industria y etapa profesional.",
    topics: [
      "Comunidades profesionales",
      "Grupos por industria",
      "Eventos",
      "Encuentros",
      "Conversaciones con EXATEC",
      "Redes por intereses",
    ],
    indicator: "Construye conexiones que evolucionen contigo",
    action: "Explorar comunidad",
    icon: Users,
  },
  {
    number: "02",
    category: "Mentoría",
    title: "Encuentra y ofrece mentoría",
    description:
      "Recibe orientación de personas con mayor experiencia y, cuando estés lista, comparte lo que has aprendido con otras generaciones.",
    topics: [
      "Encontrar una persona mentora",
      "Preparar preguntas",
      "Definir un objetivo de mentoría",
      "Registrar aprendizajes",
      "Convertirte en mentora en el futuro",
    ],
    indicator: "Aprende hoy y acompaña a otros mañana",
    action: "Explorar mentoría",
    icon: HeartHandshake,
  },
  {
    number: "03",
    category: "Conocimiento",
    title: "Comparte tu experiencia",
    description:
      "Convierte proyectos, decisiones y aprendizajes en conocimiento útil para estudiantes, profesionales y comunidades.",
    topics: [
      "Participar en un panel",
      "Compartir un caso",
      "Revisar un proyecto",
      "Crear una sesión",
      "Documentar aprendizajes",
      "Participar en una comunidad de práctica",
    ],
    indicator: "Tu experiencia puede ayudar a otros",
    action: "Explorar cómo compartir",
    icon: Lightbulb,
  },
  {
    number: "04",
    category: "Colaboración",
    title: "Colabora con el Tec",
    description:
      "Participa en investigación, proyectos, retos empresariales y experiencias que conecten a la comunidad con problemas reales.",
    topics: [
      "Investigación aplicada",
      "Retos empresariales",
      "Revisión de proyectos",
      "Proyectos interdisciplinarios",
      "Comunidades de práctica",
      "Vinculación con profesores",
    ],
    indicator: "Convierte experiencia en colaboración",
    action: "Explorar colaboración",
    icon: Compass,
  },
  {
    number: "05",
    category: "Creación de oportunidades",
    title: "Crea oportunidades para otros",
    description:
      "Cuando desarrolles experiencia e influencia profesional, podrás abrir caminos para estudiantes, egresados y equipos de la comunidad Tec.",
    topics: [
      "Proponer una práctica profesional",
      "Compartir una vacante",
      "Patrocinar un reto",
      "Proponer un proyecto empresarial",
      "Conectar talento",
      "Apoyar una investigación",
      "Crear oportunidades de aprendizaje",
    ],
    indicator: "Transforma tu experiencia en oportunidades",
    action: "Explorar cómo crear oportunidades",
    icon: BriefcaseBusiness,
  },
];
const contributionCycle = [
  [
    "Conecta",
    "Encuentra personas, comunidades y perspectivas relacionadas con tu desarrollo.",
  ],
  [
    "Aprende",
    "Recibe orientación, retroalimentación y experiencia de otras personas.",
  ],
  ["Aplica", "Convierte el aprendizaje en decisiones, proyectos y resultados."],
  [
    "Comparte",
    "Transforma tu experiencia en conocimiento útil para la comunidad.",
  ],
  [
    "Crea oportunidades",
    "Ayuda a que otras personas encuentren espacios para aprender, contribuir y crecer.",
  ],
];
const participationStages: Array<[string, string[]]> = [
  [
    "Como estudiante",
    [
      "Participar en comunidades",
      "Asistir a eventos",
      "Recibir mentoría",
      "Colaborar en proyectos",
      "Construir conexiones",
    ],
  ],
  [
    "Como recién egresada",
    [
      "Mantener contacto con la comunidad",
      "Compartir aprendizajes de la transición profesional",
      "Participar en paneles de orientación",
      "Conectar con otras personas EXATEC",
      "Apoyar a estudiantes que comienzan",
    ],
  ],
  [
    "Como profesional con experiencia",
    [
      "Ser mentora",
      "Revisar proyectos",
      "Compartir casos",
      "Proponer retos",
      "Crear oportunidades profesionales",
    ],
  ],
  [
    "Como líder y creadora de oportunidades",
    [
      "Contratar talento",
      "Patrocinar proyectos",
      "Colaborar en investigación",
      "Impartir experiencias",
      "Conectar organizaciones con el Tec",
      "Desarrollar a nuevas generaciones",
    ],
  ],
];
const contributionTypes: Array<[string, string]> = [
  [
    "Caso profesional",
    "Documenta un problema, las decisiones tomadas, el resultado y los aprendizajes.",
  ],
  [
    "Mentoría",
    "Acompaña a otra persona con preguntas, experiencia, reflexión y orientación.",
  ],
  [
    "Revisión de proyecto",
    "Ofrece retroalimentación sobre decisiones, riesgos, evidencia y oportunidades de mejora.",
  ],
  [
    "Reto u oportunidad",
    "Conecta a estudiantes o egresados con un problema relevante que pueda convertirse en aprendizaje aplicado.",
  ],
  [
    "Comunidad de práctica",
    "Participa en conversaciones recurrentes sobre una capacidad, rol, tecnología o industria.",
  ],
];
const responsibilityPrinciples = [
  "Proteger información confidencial",
  "Solicitar consentimiento",
  "Diferenciar experiencia de opinión",
  "Reconocer límites e incertidumbre",
  "Evitar conflictos de interés",
  "Respetar la autonomía de otras personas",
  "Compartir evidencia verificable",
  "No sustituir decisiones profesionales",
  "Crear oportunidades inclusivas",
  "Aprender también de la comunidad",
];
const contributionEvidence = [
  "Participación en una comunidad",
  "Mentoría realizada",
  "Proyecto revisado",
  "Caso profesional compartido",
  "Evento o panel",
  "Reto empresarial propuesto",
  "Oportunidad profesional creada",
  "Colaboración de investigación",
  "Conexión entre personas",
  "Aprendizaje generado para la comunidad",
];

function ContributionPanel({
  experience,
  onClose,
}: {
  experience: ContributionExperience;
  onClose: () => void;
}) {
  const closeRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    closeRef.current?.focus();
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKeyDown);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = "";
    };
  }, [onClose]);
  const start =
    experience.number === "02"
      ? [
          "Definir qué quieres aprender",
          "Preparar preguntas concretas",
          "Identificar el tipo de experiencia que buscas",
          "Registrar acuerdos",
          "Reflexionar sobre los aprendizajes",
        ]
      : experience.number === "05"
        ? [
            "Compartir una oportunidad profesional",
            "Proponer un reto",
            "Ofrecer una práctica",
            "Conectar talento",
            "Patrocinar un proyecto",
            "Apoyar una investigación",
          ]
        : experience.topics.slice(0, 5);
  const purpose =
    experience.number === "01"
      ? "Encontrar comunidades, personas y conversaciones relevantes para tu etapa profesional."
      : experience.number === "02"
        ? "Convertir preguntas, experiencia y reflexión en una relación de aprendizaje entre personas."
        : experience.number === "03"
          ? "Transformar experiencias reales en conocimiento útil para otras personas."
          : experience.number === "04"
            ? "Conectar experiencia profesional, investigación y aprendizaje con problemas reales."
            : "Utilizar experiencia, red e influencia para abrir oportunidades de aprendizaje y desarrollo.";
  const when =
    experience.number === "01"
      ? "Desde ahora y durante toda tu carrera."
      : experience.number === "02"
        ? "Como estudiante puedes recibir mentoría. Con experiencia acumulada podrás acompañar a otras personas."
        : experience.number === "03"
          ? "Después de completar un proyecto, resolver un problema o aprender algo que pueda ayudar a otros."
          : experience.number === "04"
            ? "Durante proyectos académicos, investigación, vida profesional o participación comunitaria."
            : "Cuando tengas experiencia, responsabilidades, proyectos o conexiones que puedan beneficiar a otras personas.";
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="contribution-panel-title"
    >
      <div className="max-h-[90vh] w-full max-w-xl overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900">
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-emerald-700 dark:text-emerald-300">
              {experience.category}
            </p>
            <h2
              id="contribution-panel-title"
              className="mt-2 text-xl font-semibold text-slate-950 dark:text-white"
            >
              {experience.title}
            </h2>
          </div>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            className="rounded-lg px-2 text-xl text-slate-500 hover:bg-slate-100 dark:hover:bg-white/10"
            aria-label="Cerrar"
          >
            ×
          </button>
        </div>
        <p className="mt-5 text-sm leading-6 text-slate-600 dark:text-white/70">
          <strong>Propósito:</strong> {purpose}
        </p>
        <p className="mt-4 text-sm leading-6 text-slate-600 dark:text-white/70">
          <strong>Cuándo puede ser relevante:</strong> {when}
        </p>
        <h3 className="mt-5 font-semibold text-slate-950 dark:text-white">
          Cómo puede comenzar Sofia
        </h3>
        <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-white/70">
          {start.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
        <p className="mt-5 rounded-xl bg-emerald-50 p-4 text-sm text-emerald-900 dark:bg-emerald-400/10 dark:text-emerald-100">
          Experiencia conceptual. Las conexiones y acciones todavía no están
          habilitadas. Esta experiencia se conectará con servicios y
          oportunidades del ecosistema Tec en una siguiente fase.
        </p>
      </div>
    </div>
  );
}

interface RealCompletedCourse {
  capabilityId: string;
  name: string;
  averageScore?: number | null;
  isStructuredLearning: boolean;
}

function TecPracticeTab({ personId }: { personId: string }) {
  const [enrolledQuery, setEnrolledQuery] = useState<{
    loading: boolean;
    items: RealCompletedCourse[];
  }>({ loading: true, items: [] });
  const [selectedCapabilityId, setSelectedCapabilityId] = useState<string | null>(null);
  const [session, setSession] = useState<import("./api/tecPracticeApi").TecPracticeSessionView | null>(null);
  const [sessionLoading, setSessionLoading] = useState(false);
  const [loadingMessage, setLoadingMessage] = useState("Consultando tus cursos terminados...");
  const [answers, setAnswers] = useState<Record<number, string>>({});
  const [submittingIndex, setSubmittingIndex] = useState<number | null>(null);
  const [sessionError, setSessionError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      import("@/features/capabilities/api/enrolledCapabilitiesApi").then(({ getEnrolledCapabilities }) => getEnrolledCapabilities(personId)),
      import("@/features/capabilities/api/enrolledStructuredLearningApi").then(({ getEnrolledStructuredLearningDocuments }) =>
        getEnrolledStructuredLearningDocuments(personId),
      ),
    ]).then(([capabilities, documents]) => {
      if (cancelled) return;
      // Real completed courses can live in either data model — a standalone Capability
      // (Completed when masteredNodes === totalNodes) or a Structured Learning document
      // (Completed when completedSectionCount === sectionCount, e.g. Sofia's "Inteligencia
      // Emocional y Mindfulness"). No fabricated/local content — only what the real APIs return.
      const completedCapabilities: RealCompletedCourse[] = capabilities
        .filter((c) => c.status === "Completed")
        .map((c) => ({ capabilityId: c.capabilityId, name: c.name, averageScore: c.averageScore, isStructuredLearning: false }));
      const completedDocuments: RealCompletedCourse[] = documents
        .filter((d) => d.sectionCount > 0 && d.completedSectionCount === d.sectionCount)
        .map((d) => ({ capabilityId: d.structuredLearningDocumentId, name: d.documentName, isStructuredLearning: true }));
      setEnrolledQuery({ loading: false, items: [...completedCapabilities, ...completedDocuments] });
    });
    return () => {
      cancelled = true;
    };
  }, [personId]);

  async function openCourse(capabilityId: string) {
    setSelectedCapabilityId(capabilityId);
    setSession(null);
    setAnswers({});
    setSessionError(null);
    setLoadingMessage("Buscando la práctica guardada para este curso...");
    setSessionLoading(true);
    try {
      const { getTecPracticeSession } = await import("./api/tecPracticeApi");
      const existing = await getTecPracticeSession(personId, capabilityId);
      setSession(existing);
    } catch {
      setSessionError("No pudimos cargar la práctica de este curso. Intenta de nuevo.");
    } finally {
      setSessionLoading(false);
    }
  }

  async function generate(capabilityId: string) {
    setSessionError(null);
    setLoadingMessage("La IA está preparando ejercicios personalizados...");
    setSessionLoading(true);
    try {
      const { generateTecPracticeSession } = await import("./api/tecPracticeApi");
      const generated = await generateTecPracticeSession(personId, capabilityId);
      setSession(generated);
      setAnswers({});
    } catch {
      setSessionError("No pudimos generar la práctica con IA para este curso. Intenta de nuevo.");
    } finally {
      setSessionLoading(false);
    }
  }

  async function submitAnswer(exerciseIndex: number) {
    if (!session) return;
    const answer = answers[exerciseIndex]?.trim();
    if (!answer) return;
    setSubmittingIndex(exerciseIndex);
    const { submitTecPracticeAnswer } = await import("./api/tecPracticeApi");
    const graded = await submitTecPracticeAnswer(personId, session.practiceSessionId, exerciseIndex, answer);
    setSession((prev) =>
      prev
        ? { ...prev, exercises: prev.exercises.map((e) => (e.index === exerciseIndex ? graded : e)) }
        : prev,
    );
    setSubmittingIndex(null);
  }

  if (enrolledQuery.loading) {
    return (
      <PracticeLoadingModal message="Consultando tus cursos terminados..." />
    );
  }

  if (enrolledQuery.items.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-slate-200 p-8 text-center dark:border-white/10">
        <p className="text-sm font-medium text-slate-600 dark:text-white/70">
          Aún no tienes cursos terminados — cuando completes uno, aquí podrás practicar con
          ejercicios generados por IA.
        </p>
      </div>
    );
  }

  return (
    <>
      {sessionLoading && <PracticeLoadingModal message={loadingMessage} />}
      <div className="grid gap-4 lg:grid-cols-[280px_1fr]">
      <div className="space-y-2">
        {enrolledQuery.items.map((course) => (
          <div
            key={course.capabilityId}
            className={`w-full rounded-xl border p-3 text-sm font-semibold transition ${
              selectedCapabilityId === course.capabilityId
                ? "border-rose-300 bg-rose-50 text-rose-800 dark:border-rose-400/30 dark:bg-rose-400/10 dark:text-rose-200"
                : "border-slate-200 bg-white text-slate-700 hover:border-rose-200 dark:border-white/10 dark:bg-white/[0.03] dark:text-white/75"
            }`}
          >
            <button type="button" onClick={() => openCourse(course.capabilityId)} className="block w-full text-left">
              {course.name}
            </button>
            <Link
              to={course.isStructuredLearning ? `/structured-learning/${course.capabilityId}` : `/capabilities/${course.capabilityId}`}
              className="mt-1 inline-block text-xs font-medium text-blue-600 hover:underline dark:text-blue-300"
            >
              Ver curso
            </Link>
          </div>
        ))}
      </div>

      <div>
        {!selectedCapabilityId ? (
          <div className="rounded-xl border border-dashed border-slate-200 p-8 text-center dark:border-white/10">
            <p className="text-sm text-slate-500 dark:text-white/60">
              Elige un curso terminado para ver o generar tu práctica.
            </p>
          </div>
        ) : sessionLoading ? (
          <div className="py-8 text-center text-sm text-slate-500 dark:text-white/60">Cargando...</div>
        ) : sessionError ? (
          <div className="rounded-xl border border-dashed border-rose-200 p-8 text-center dark:border-rose-400/20">
            <p className="text-sm font-medium text-rose-700 dark:text-rose-300">{sessionError}</p>
            <button
              type="button"
              onClick={() => openCourse(selectedCapabilityId)}
              className="mt-4 inline-flex items-center gap-2 rounded-xl bg-rose-600 px-5 py-2.5 text-sm font-semibold text-white shadow hover:bg-rose-700"
            >
              Reintentar
            </button>
          </div>
        ) : !session ? (
          <div className="rounded-xl border border-dashed border-slate-200 p-8 text-center dark:border-white/10">
            <p className="text-sm text-slate-600 dark:text-white/70">
              Aún no has generado práctica para este curso.
            </p>
            <button
              type="button"
              onClick={() => generate(selectedCapabilityId)}
              className="mt-4 inline-flex items-center gap-2 rounded-xl bg-rose-600 px-5 py-2.5 text-sm font-semibold text-white shadow hover:bg-rose-700"
            >
              <Dumbbell className="h-4 w-4" /> Generar práctica con IA
            </button>
          </div>
        ) : (
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-slate-950 dark:text-white">{session.capabilityName}</h3>
              <button
                type="button"
                onClick={() => generate(selectedCapabilityId)}
                className="inline-flex items-center gap-1.5 text-xs font-semibold text-rose-600 hover:underline dark:text-rose-300"
              >
                <RefreshCw className="h-3.5 w-3.5" /> Regenerar
              </button>
            </div>
            {session.exercises.map((exercise) => (
              <Card key={exercise.index} className="border-slate-200/80 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
                <h4 className="text-sm font-bold text-slate-950 dark:text-white">{exercise.title}</h4>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">{exercise.instructions}</p>
                {exercise.score != null ? (
                  <div className="mt-3 rounded-xl bg-emerald-50 p-3 text-sm dark:bg-emerald-400/10">
                    <p className="font-semibold text-emerald-800 dark:text-emerald-200">
                      Puntaje: {exercise.score}/100
                    </p>
                    <p className="mt-1 text-emerald-900 dark:text-emerald-100">{exercise.feedback}</p>
                  </div>
                ) : (
                  <div className="mt-3 space-y-2">
                    <textarea
                      value={answers[exercise.index] ?? ""}
                      onChange={(e) => setAnswers((prev) => ({ ...prev, [exercise.index]: e.target.value }))}
                      rows={3}
                      placeholder="Escribe tu respuesta..."
                      className="w-full rounded-xl border border-slate-200 p-3 text-sm dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
                    />
                    <button
                      type="button"
                      disabled={submittingIndex === exercise.index}
                      onClick={() => submitAnswer(exercise.index)}
                      className="inline-flex items-center gap-1.5 rounded-lg bg-rose-600 px-4 py-2 text-xs font-semibold text-white hover:bg-rose-700 disabled:opacity-50"
                    >
                      {submittingIndex === exercise.index ? "Calificando..." : "Enviar respuesta"}
                    </button>
                  </div>
                )}
              </Card>
            ))}
          </div>
        )}
      </div>
      </div>
    </>
  );
}

function PracticeLoadingModal({ message }: { message: string }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/45 p-4 backdrop-blur-sm" role="status" aria-live="polite">
      <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-2xl dark:bg-slate-900">
        <div className="flex items-center gap-3">
          <div className="h-5 w-5 animate-spin rounded-full border-2 border-rose-200 border-t-rose-600" />
          <h3 className="text-base font-bold text-slate-950 dark:text-white">Preparando tu experiencia</h3>
        </div>
        <p className="mt-4 text-sm font-medium text-slate-700 dark:text-white/80">{message}</p>
        <ol className="mt-5 space-y-3 text-sm text-slate-500 dark:text-white/60">
          <li>1. Revisamos tu avance académico.</li>
          <li>2. Identificamos el curso que quieres reforzar.</li>
          <li>3. Organizamos ejercicios para practicar.</li>
        </ol>
      </div>
    </div>
  );
}

const TEC_PRACTICE_DAY_MS = 24 * 60 * 60 * 1000;

/** "hace X días/meses" relative label for a stale engram's completion date —
 * same convention as BrainRecallGrid's own relativeLabel. */
function tecPracticeRelativeLabel(completedDate: string | undefined): string {
  if (!completedDate) return "—";
  const days = Math.floor((Date.now() - new Date(completedDate).getTime()) / TEC_PRACTICE_DAY_MS);
  const rtf = new Intl.RelativeTimeFormat("es", { numeric: "auto" });
  if (days < 1) return rtf.format(0, "day");
  if (days < 30) return rtf.format(-days, "day");
  if (days < 365) return rtf.format(-Math.round(days / 30), "month");
  return rtf.format(-Math.round(days / 365), "year");
}

export function TecPracticaYRefuerzaPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? "";
  const [tab, setTab] = useState<"stale" | "practice">("stale");
  const knowledgeNetworkQuery = useKnowledgeNetwork(personId);
  const STALE_ENGRAM_DAYS = 30;
  const staleEngrams = useMemo(() => {
    const cutoff = Date.now() - STALE_ENGRAM_DAYS * 24 * 60 * 60 * 1000;
    return (knowledgeNetworkQuery.data?.masteredEngrams ?? [])
      .filter((engram) => engram.completedDate && new Date(engram.completedDate).getTime() <= cutoff)
      // Weakest memory (lowest retrievability %) first — same convention as
      // BrainRecallGrid. Sorting by raw date alone isn't monotonic with %
      // since retrievability also depends on reviewCount/stability.
      .sort((a, b) => {
        const aR = getRetrievability(a.completedDate, a.reviewCount ?? 1) ?? -1;
        const bR = getRetrievability(b.completedDate, b.reviewCount ?? 1) ?? -1;
        return aR - bR;
      });
  }, [knowledgeNetworkQuery.data]);
  const [openNeuron, setOpenNeuron] = useState<SelectedNeuron | null>(null);

  return (
    <TecPage
      title="Practica y refuerza"
      description="Repasa lo que ya aprendiste y aplica tus cursos terminados con práctica real, generada por IA para tu momento actual."
    >
      <div className="space-y-6">
        <nav aria-label="Breadcrumb" className="-mt-5 flex flex-wrap items-center gap-2 text-sm text-slate-500">
          <Link to="/tec-contigo" className="hover:text-blue-600 font-medium">
            Tec Contigo
          </Link>
          <span>/</span>
          <span>Practica y refuerza</span>
        </nav>

        <div className="flex gap-2 border-b border-slate-200 dark:border-white/10">
          <button
            type="button"
            onClick={() => setTab("stale")}
            className={`px-4 py-2 text-sm font-semibold ${tab === "stale" ? "border-b-2 border-rose-600 text-rose-700 dark:text-rose-300" : "text-slate-500 dark:text-white/55"}`}
          >
            Engramas para repasar
          </button>
          <button
            type="button"
            onClick={() => setTab("practice")}
            className={`px-4 py-2 text-sm font-semibold ${tab === "practice" ? "border-b-2 border-rose-600 text-rose-700 dark:text-rose-300" : "text-slate-500 dark:text-white/55"}`}
          >
            Cursos terminados
          </button>
        </div>

        {tab === "stale" ? (
          knowledgeNetworkQuery.isLoading ? (
            <div className="py-8 text-center text-sm text-slate-500 dark:text-white/60">
              Revisando tus engramas dominados...
            </div>
          ) : staleEngrams.length === 0 ? (
            <div className="rounded-xl border border-dashed border-slate-200 p-8 text-center dark:border-white/10">
              <p className="text-sm font-medium text-slate-600 dark:text-white/70">
                No tienes engramas con más de {STALE_ENGRAM_DAYS} días sin repasar — ¡tu conocimiento está al día!
              </p>
            </div>
          ) : (
            <div className="flex flex-wrap gap-4">
              {staleEngrams.map((engram) => {
                const retrievability = getRetrievability(engram.completedDate, engram.reviewCount ?? 1);
                const memoryColor = getMemoryColor(retrievability);
                const memoryTextColor = getMemoryTextColor(retrievability);
                const percent = retrievability === null ? null : Math.round(retrievability * 100);
                const automatic = isAutomatic(engram.reviewCount ?? 0);
                return (
                  <button
                    key={engram.sectionId}
                    type="button"
                    onClick={() => setOpenNeuron(engram.neuron)}
                    title={engram.title}
                    className="group flex w-24 flex-col items-center gap-1.5 text-center transition hover:-translate-y-0.5 focus:outline-none"
                  >
                    <span
                      className="flex h-11 w-11 items-center justify-center rounded-full border border-white/20 text-xs font-bold leading-none shadow-md transition group-hover:scale-105"
                      style={{ background: memoryColor, color: memoryTextColor }}
                    >
                      {engram.title.charAt(0).toUpperCase()}
                    </span>
                    {percent !== null && (
                      <span className="text-[11px] font-bold" style={{ color: automatic ? undefined : memoryColor }}>
                        {automatic ? "Auto" : `${percent}%`}
                      </span>
                    )}
                    <span className="line-clamp-2 text-[10px] font-medium leading-tight text-slate-600 dark:text-white/60">
                      {engram.title}
                    </span>
                    <span className="text-[9px] font-medium text-slate-400 dark:text-white/40">
                      {tecPracticeRelativeLabel(engram.completedDate)}
                    </span>
                  </button>
                );
              })}
            </div>
          )
        ) : (
          <TecPracticeTab personId={personId} />
        )}
      </div>

      {openNeuron && (
        <EngramDetailPanel personId={personId} neuron={openNeuron} onClose={() => setOpenNeuron(null)} onSelectNeuron={setOpenNeuron} />
      )}
    </TecPage>
  );
}

export function TecConectaYContribuyePage() {
  const { resumeQuery } = useTecProfile();
  const [selectedExperience, setSelectedExperience] =
    useState<ContributionExperience | null>(null);
  const resume = resumeQuery.data;
  const text = profileText(resume).toLowerCase();
  const education = resume?.education?.join(" ") ?? "";
  const degree =
    clean(resume?.studentAcademicProfile?.degree) ||
    clean(education.match(/(?:—|-|–)\s*([^()\n]+?)\s*\(/)?.[1]);
  const stage = clean(resume?.studentAcademicProfile?.academicStage);
  const areas = [
    "Inteligencia artificial",
    "Machine Learning",
    "Microsoft Azure",
    "Aplicaciones LLM",
    "Manufactura",
    "Mantenimiento predictivo",
    "Transformación digital",
    "Power BI",
  ]
    .filter(
      (area) =>
        text.includes(
          area
            .toLowerCase()
            .replace("inteligencia artificial", "artificial intelligence"),
        ) ||
        (area === "Inteligencia artificial" &&
          /ia|artificial intelligence|machine learning/i.test(text)),
    )
    .slice(0, 5);
  return (
    <TecPage
      title="El Tec está contigo para conectar y contribuir"
      description="Tu experiencia puede convertirse en conocimiento, conexiones y oportunidades para la comunidad Tec. Con el tiempo, podrás aprender de otras personas, compartir lo que sabes y ayudar a que nuevas generaciones crezcan."
    >
      <div className="space-y-10">
        <nav
          aria-label="Breadcrumb"
          className="-mt-5 flex flex-wrap items-center gap-2 text-sm text-slate-500"
        >
          <Link to="/tec-contigo" className="hover:text-blue-600 font-medium">
            Tec Contigo
          </Link>
          <span>/</span>
          <span>Conecta y contribuye</span>
        </nav>
        <Card className="border-emerald-200 bg-emerald-50/60 p-6 dark:border-emerald-400/20 dark:bg-emerald-400/[0.08]">
          <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-800 dark:bg-emerald-400/20 dark:text-emerald-200">
            A lo largo de tu vida
          </span>
          <p className="mt-4 text-sm leading-6 text-slate-700 dark:text-white/75">
            Hoy puedes comenzar conectándote con la comunidad. A medida que
            desarrolles experiencia, encontrarás nuevas formas de compartir,
            colaborar y crear oportunidades.
          </p>
        </Card>
        <section aria-labelledby="contribution-start-title">
          <h2
            id="contribution-start-title"
            className="text-xl font-semibold text-slate-950 dark:text-white"
          >
            Tu punto de partida
          </h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <Card className="p-5">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                Formación
              </p>
              <p className="mt-2 text-lg font-semibold text-slate-950 dark:text-white">
                {degree ||
                  "Completa tu perfil para conectar esta etapa con tu formación."}
              </p>
              {stage && (
                <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
                  {stage}
                </p>
              )}
            </Card>
            <Card className="p-5">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                Áreas de experiencia
              </p>
              {areas.length ? (
                <div className="mt-3 flex flex-wrap gap-2">
                  {areas.map((area) => (
                    <span
                      key={area}
                      className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-semibold text-slate-700 dark:bg-white/10 dark:text-white/75"
                    >
                      {area}
                    </span>
                  ))}
                </div>
              ) : (
                <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
                  Completa tu perfil para conectar esta etapa con tu experiencia
                  e intereses.{" "}
                  <Link to="/profile" className="font-semibold text-blue-600">
                    Completar mi perfil
                  </Link>
                </p>
              )}
            </Card>
          </div>
          <p className="mt-4 text-sm text-slate-600 dark:text-white/70">
            En el futuro podrás elegir cómo quieres participar y contribuir.
          </p>
        </section>
        <section aria-labelledby="contribution-experiences-title">
          <h2
            id="contribution-experiences-title"
            className="text-xl font-semibold text-slate-950 dark:text-white"
          >
            Cómo puedes conectar y contribuir
          </h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/65">
            Explora distintas formas de aprender de la comunidad, compartir
            experiencia y crear oportunidades para otras personas.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {contributionExperiences.map((experience) => {
              const Icon = experience.icon;
              return (
                <Card
                  key={experience.number}
                  className="flex min-h-72 flex-col border-t-4 border-emerald-500 p-5"
                >
                  <div className="flex items-start justify-between gap-3">
                    <span className="text-xs font-bold text-slate-500">
                      {experience.number}
                    </span>
                    <Icon
                      className="h-5 w-5 text-emerald-600"
                      aria-hidden="true"
                    />
                  </div>
                  <p className="mt-3 text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {experience.category}
                  </p>
                  <h3 className="mt-2 text-lg font-semibold text-slate-950 dark:text-white">
                    {experience.title}
                  </h3>
                  <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                    {experience.description}
                  </p>
                  <p className="mt-3 text-xs font-medium text-slate-500">
                    {experience.indicator}
                  </p>
                  <button
                    type="button"
                    onClick={() => setSelectedExperience(experience)}
                    className="mt-auto inline-flex items-center gap-2 pt-5 text-left text-sm font-semibold text-blue-600 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600 dark:text-blue-300"
                  >
                    {experience.action}
                    <ArrowRight className="h-4 w-4" />
                  </button>
                </Card>
              );
            })}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Tu círculo de crecimiento con el Tec
          </h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-white/65">
            La relación evoluciona cuando lo que recibes se transforma en
            experiencia y lo que aprendes se convierte en una oportunidad para
            otros.
          </p>
          <div className="mt-4 grid gap-3 md:grid-cols-5">
            {contributionCycle.map(([title, description], index) => (
              <Card key={title} className="p-4">
                <span className="text-xs font-bold text-emerald-600">
                  0{index + 1}
                </span>
                <h3 className="mt-2 font-semibold text-slate-950 dark:text-white">
                  {title}
                </h3>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {description}
                </p>
              </Card>
            ))}
          </div>
          <p className="mt-3 text-xs text-slate-500">
            Ciclo conceptual para esta experiencia.
          </p>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Cómo puede evolucionar tu participación
          </h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-4">
            {participationStages.map(([title, items]) => (
              <Card key={title} className="p-5">
                <h3 className="font-semibold text-slate-950 dark:text-white">
                  {title}
                </h3>
                <ul className="mt-3 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-white/70">
                  {items.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </Card>
            ))}
          </div>
          <p className="mt-3 text-xs text-slate-500">
            Estas posibilidades representan una visión de evolución. No indican
            el estado actual de Sofia.
          </p>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Convierte tu experiencia en una contribución útil
          </h2>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-600 dark:text-white/70">
            Compartir experiencia no significa solamente contar una historia.
            Significa aportar contexto, decisiones, evidencia, resultados y
            aprendizajes que otras personas puedan utilizar.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {contributionTypes.map(([title, description]) => (
              <Card key={title} className="p-5">
                <h3 className="font-semibold text-slate-950 dark:text-white">
                  {title}
                </h3>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {description}
                </p>
              </Card>
            ))}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Contribuye con responsabilidad
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            Contribuir implica compartir valor sin exponer información, imponer
            decisiones o sustituir el aprendizaje de otras personas.
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            {responsibilityPrinciples.map((principle) => (
              <span
                key={principle}
                className="rounded-full border border-slate-200 px-3 py-1.5 text-xs text-slate-700 dark:border-white/10 dark:text-white/70"
              >
                {principle}
              </span>
            ))}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Haz visible tu contribución
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            Ejemplos conceptuales de evidencia futura, no acciones ya realizadas
            por Sofia.
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            {contributionEvidence.map((item) => (
              <span
                key={item}
                className="rounded-full border border-slate-200 px-3 py-1.5 text-xs text-slate-700 dark:border-white/10 dark:text-white/70"
              >
                {item}
              </span>
            ))}
          </div>
        </section>
        <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 text-white shadow-lg dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold">
            El Tec continúa contigo y tu experiencia continúa con la comunidad
          </h2>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-300">
            La relación no termina cuando recibes un título o completas un
            programa. Con el tiempo, lo que has aprendido puede convertirse en
            orientación, colaboración y oportunidades para otras personas.
          </p>
          <div className="mt-5 flex flex-wrap gap-4">
            <Link
              to="/tec-contigo"
              className="inline-flex items-center gap-2 text-sm font-semibold text-blue-300"
            >
              Volver a las etapas Tec 360 <ArrowRight className="h-4 w-4" />
            </Link>
            <Link
              to="/"
              className="inline-flex items-center gap-2 text-sm font-semibold text-blue-300"
            >
              Volver a Today <ArrowRight className="h-4 w-4" />
            </Link>
          </div>
        </div>
      </div>
      {selectedExperience && (
        <ContributionPanel
          experience={selectedExperience}
          onClose={() => setSelectedExperience(null)}
        />
      )}
    </TecPage>
  );
}

export function Tec360FutureStagePage({
  title,
  message = "Esta etapa se desarrollará posteriormente.",
}: {
  title: string;
  message?: string;
}) {
  return (
    <TecPage title={title} description={message}>
      <Temporary>{message}</Temporary>
    </TecPage>
  );
}

export function TecContigoIndexPage() {
  return (
    <div>
      <TecHeader
        title="El Tec está contigo"
        description="Tu formación no termina al graduarte. Explora cómo el Tec puede acompañarte en tu semestre actual, tu preparación profesional, el fortalecimiento de tus conocimientos y el desarrollo de las capacidades que necesitas para tu siguiente etapa."
      />
      <div className="grid gap-4 md:grid-cols-2">
        {(Object.keys(pages) as PageKey[]).map((key) => {
          const page = pages[key];
          return (
            <Link key={key} to={page.path}>
              <Card className="h-full p-6 transition hover:border-blue-300 hover:shadow-md">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-emerald-600">
                  {page.category}
                </p>
                <h2 className="mt-3 text-xl font-semibold text-slate-950 dark:text-white">
                  {page.title}
                </h2>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {page.description}
                </p>
                <span className="mt-5 inline-flex items-center gap-2 text-sm font-semibold text-blue-600">
                  Explorar <ArrowRight className="h-4 w-4" />
                </span>
              </Card>
            </Link>
          );
        })}
      </div>
    </div>
  );
}

type SubjectConfig = {
  subjectKey: string;
  purpose: string;
  outcomes: string[];
  priorityProgramId: string;
  additionalProgramIds: string[];
  agent: {
    id: string;
    name: string;
    type: string;
    description: string;
    can: string[];
    cannot: string[];
    activity: string;
    nextStep: string;
  };
};

const subjectConfigs: SubjectConfig[] = [
  {
    subjectKey: "Seminario de Investigación",
    purpose:
      "Definir y estructurar una investigación académica relevante, viable y respaldada por evidencia.",
    outcomes: [
      "Delimitar el problema de investigación.",
      "Formular la pregunta principal.",
      "Definir objetivos.",
      "Seleccionar una metodología.",
      "Identificar fuentes relevantes.",
      "Construir un mapa inicial de evidencia.",
    ],
    priorityProgramId: "applied-research-methodology",
    additionalProgramIds: [
      "thesis-research-writing-defense",
      "literature-review-evidence",
      "academic-presentations",
    ],
    agent: {
      id: "research-methodology-agent",
      name: "Agente de Investigación y Metodología",
      type: "Agente de IA para acompañamiento académico",
      description:
        "Te acompaña a delimitar el problema, formular preguntas, relacionar objetivos con evidencia y revisar la coherencia metodológica.",
      can: [
        "Evaluar si una pregunta es demasiado amplia.",
        "Revisar la relación entre objetivos y metodología.",
        "Organizar un mapa inicial de investigación.",
      ],
      cannot: [
        "Escribir la tesis completa.",
        "Inventar fuentes o fabricar evidencia.",
        "Sustituir la revisión del asesor.",
      ],
      activity:
        "Escribe tu pregunta de investigación actual y analiza si define con claridad el problema, la población, el contexto y el resultado esperado.",
      nextStep:
        "Define una versión inicial de tu pregunta de investigación y relaciónala con tres objetivos específicos.",
    },
  },
  {
    subjectKey: "Tesis Profesional",
    purpose:
      "Desarrollar, documentar y defender una investigación profesional con evidencia, rigor y claridad.",
    outcomes: [
      "Consolidar el marco teórico.",
      "Estructurar capítulos.",
      "Gestionar evidencia.",
      "Documentar resultados.",
      "Preparar conclusiones.",
      "Preparar la defensa.",
    ],
    priorityProgramId: "thesis-research-writing-defense",
    additionalProgramIds: [
      "literature-review-evidence",
      "applied-research-methodology",
      "academic-presentations",
    ],
    agent: {
      id: "thesis-progress-agent",
      name: "Agente de Avance de Tesis",
      type: "Agente de IA para acompañamiento académico",
      description:
        "Te ayuda a organizar el avance, detectar secciones incompletas y preparar preguntas concretas para tu asesor.",
      can: [
        "Organizar la estructura de capítulos.",
        "Relacionar afirmaciones con evidencia.",
        "Preparar preguntas de defensa.",
      ],
      cannot: [
        "Escribir la tesis por Sofia.",
        "Inventar citas o resultados.",
        "Sustituir al asesor de tesis.",
      ],
      activity:
        "Construye un mapa del estado actual de cada capítulo e identifica la siguiente evidencia que necesitas producir.",
      nextStep:
        "Organiza tu tesis por capítulos, evidencia disponible, dudas abiertas y siguiente entregable.",
    },
  },
  {
    subjectKey: "Arquitecturas de IA Empresarial",
    purpose:
      "Diseñar soluciones completas de inteligencia artificial alineadas con necesidades empresariales, restricciones y criterios operativos.",
    outcomes: [
      "Comprender requerimientos.",
      "Diseñar componentes.",
      "Conectar datos, modelos y servicios.",
      "Incorporar seguridad.",
      "Definir evaluación y observabilidad.",
      "Documentar decisiones técnicas.",
      "Comunicar trade-offs.",
    ],
    priorityProgramId: "enterprise-ai-architecture",
    additionalProgramIds: [
      "llm-enterprise-architecture",
      "enterprise-ai-security",
      "mlops-model-observability",
    ],
    agent: {
      id: "enterprise-ai-architecture-agent",
      name: "Agente de Revisión de Arquitectura de IA",
      type: "Agente de IA para acompañamiento técnico",
      description:
        "Te ayuda a revisar componentes, dependencias, riesgos, decisiones y trade-offs de una arquitectura de inteligencia artificial.",
      can: [
        "Revisar si faltan componentes.",
        "Comparar alternativas.",
        "Identificar decisiones no documentadas.",
      ],
      cannot: [
        "Diseñar toda la solución sin participación de Sofia.",
        "Inventar requerimientos.",
        "Ocultar riesgos o incertidumbre.",
      ],
      activity:
        "Describe el problema empresarial y construye una primera lista de componentes, datos, restricciones y decisiones por validar.",
      nextStep:
        "Crea una arquitectura inicial e identifica tres decisiones técnicas que necesitan justificación.",
    },
  },
  {
    subjectKey: "Liderazgo Tecnológico",
    purpose:
      "Desarrollar la capacidad de coordinar personas, comunicar decisiones y movilizar equipos tecnológicos.",
    outcomes: [
      "Comunicar objetivos.",
      "Facilitar decisiones.",
      "Coordinar responsabilidades.",
      "Gestionar desacuerdos.",
      "Dar y recibir retroalimentación.",
      "Presentar riesgos.",
      "Construir acuerdos.",
    ],
    priorityProgramId: "technology-leadership",
    additionalProgramIds: [
      "executive-technical-communication",
      "academic-presentations",
      "integrative-project-management",
    ],
    agent: {
      id: "technology-leadership-agent",
      name: "Agente de Preparación para Conversaciones de Liderazgo",
      type: "Agente de IA para preparación y reflexión",
      description:
        "Te ayuda a preparar conversaciones, analizar perspectivas y reflexionar sobre decisiones de liderazgo.",
      can: [
        "Preparar una conversación difícil.",
        "Identificar stakeholders.",
        "Anticipar objeciones.",
      ],
      cannot: [
        "Hablar con el equipo en lugar de Sofia.",
        "Tomar decisiones humanas por ella.",
        "Manipular conversaciones.",
      ],
      activity:
        "Selecciona una decisión pendiente de tu proyecto e identifica participantes, intereses, riesgos y el acuerdo que necesitas construir.",
      nextStep:
        "Prepara una conversación de liderazgo relacionada con una decisión real de tu proyecto.",
    },
  },
  {
    subjectKey: "Proyecto Integrador",
    purpose:
      "Integrar conocimientos técnicos, gestión, comunicación y evidencia en una solución completa.",
    outcomes: [
      "Definir alcance.",
      "Organizar entregables.",
      "Asignar responsabilidades.",
      "Diseñar la solución.",
      "Gestionar riesgos.",
      "Validar resultados.",
      "Comunicar impacto.",
      "Documentar decisiones.",
    ],
    priorityProgramId: "integrative-project-management",
    additionalProgramIds: [
      "enterprise-ai-architecture",
      "technology-leadership",
      "academic-presentations",
    ],
    agent: {
      id: "integrative-project-agent",
      name: "Agente de Ejecución del Proyecto Integrador",
      type: "Agente de IA para organización y seguimiento",
      description:
        "Te ayuda a organizar entregables, dependencias, riesgos, decisiones y evidencia del proyecto.",
      can: [
        "Revisar el alcance.",
        "Organizar entregables.",
        "Identificar dependencias.",
      ],
      cannot: [
        "Ejecutar el proyecto por Sofia.",
        "Asignar responsabilidades sin aprobación.",
        "Marcar entregables como completos sin evidencia.",
      ],
      activity:
        "Define el objetivo del proyecto, los entregables principales, los responsables y los riesgos que necesitan seguimiento.",
      nextStep:
        "Construye una vista del proyecto con alcance, entregables, responsables, riesgos y criterios de éxito.",
    },
  },
];

function AgentModal({
  config,
  subject,
  onClose,
}: {
  config: SubjectConfig["agent"];
  subject: string;
  onClose: () => void;
}) {
  const closeRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    closeRef.current?.focus();
    const close = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", close);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", close);
      document.body.style.overflow = "";
    };
  }, [onClose]);
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="agent-modal-title"
    >
      <div className="max-h-[90vh] w-full max-w-xl overflow-y-auto rounded-2xl border border-teal-200 bg-white p-6 shadow-2xl dark:bg-slate-900">
        <div className="flex justify-between gap-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-teal-700">
              Agente de demostración
            </p>
            <h2
              id="agent-modal-title"
              className="mt-2 text-xl font-semibold text-slate-950 dark:text-white"
            >
              {config.name}
            </h2>
            <p className="mt-1 text-sm text-slate-500">
              {subject} · {config.type}
            </p>
          </div>
          <button
            ref={closeRef}
            type="button"
            onClick={onClose}
            className="text-xl text-slate-500"
            aria-label="Cerrar"
          >
            ×
          </button>
        </div>
        <p className="mt-5 text-sm leading-6 text-slate-600 dark:text-white/70">
          {config.description}
        </p>
        <h3 className="mt-5 font-semibold">Puede ayudarte a</h3>
        <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-white/70">
          {config.can.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
        <h3 className="mt-5 font-semibold">No hará</h3>
        <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-white/70">
          {config.cannot.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
        <p className="mt-5 rounded-xl bg-teal-50 p-4 text-sm text-teal-900 dark:bg-teal-400/10 dark:text-teal-100">
          <strong>Actividad inicial:</strong> {config.activity}
        </p>
        <p className="mt-5 text-xs font-semibold text-slate-500">
          Experiencia de demostración. El agente todavía no está conectado.
        </p>
      </div>
    </div>
  );
}

function ThesisUpload({ personId }: { personId: string }) {
  const [file, setFile] = useState<File | null>(null);
  const [existingFileName, setExistingFileName] = useState<string | null>(null);
  const [checkingExisting, setCheckingExisting] = useState(true);
  const [status, setStatus] = useState<
    "idle" | "uploading" | "uploaded" | "error"
  >("idle");
  const [message, setMessage] = useState("");
  const [processing, setProcessing] = useState(false);
  const [markdownReady, setMarkdownReady] = useState(false);

  useEffect(() => {
    if (!personId) {
      setCheckingExisting(false);
      return;
    }
    let cancelled = false;
    getThesisStatus(personId)
      .then((result) => {
        if (!cancelled && result) {
          setExistingFileName(result.fileName);
          setStatus("uploaded");
        }
      })
      .catch(() => {})
      .finally(() => {
        if (!cancelled) setCheckingExisting(false);
      });
    return () => {
      cancelled = true;
    };
  }, [personId]);

  const activeFileName = file?.name ?? existingFileName ?? "";

  const handleUpload = async () => {
    if (!file || !personId) return;
    setStatus("uploading");
    setMessage("");
    try {
      await uploadThesis(personId, file);
      setExistingFileName(file.name);
      setStatus("uploaded");
      setMessage("Tesis guardada correctamente.");
    } catch (error) {
      setStatus("error");
      const responseData = (
        error as { response?: { data?: { message?: string; error?: string } } }
      ).response?.data;
      setMessage(
        responseData?.message ??
          responseData?.error ??
          (error instanceof Error
            ? error.message
            : "No se pudo guardar la tesis."),
      );
    }
  };

  const handleExtract = async () => {
    if (!activeFileName) return;
    setProcessing(true);
    try {
      const result = await extractThesis(personId, activeFileName);
      setMarkdownReady(true);
      setMessage(
        `Texto extraído y guardado como Markdown (${result.pageCount} páginas).`,
      );
    } catch (error) {
      setStatus("error");
      setMessage(
        error instanceof Error ? error.message : "No se pudo extraer el texto.",
      );
    } finally {
      setProcessing(false);
    }
  };

  const handleDelete = async () => {
    if (
      !activeFileName ||
      !window.confirm("¿Borrar el PDF y el Markdown extraído?")
    )
      return;
    setProcessing(true);
    try {
      await deleteThesis(personId, activeFileName);
      setFile(null);
      setExistingFileName(null);
      setStatus("idle");
      setMarkdownReady(false);
      setMessage("Tesis eliminada del Data Lake.");
    } catch (error) {
      setStatus("error");
      setMessage(
        error instanceof Error
          ? error.message
          : "No se pudo eliminar la tesis.",
      );
    } finally {
      setProcessing(false);
    }
  };

  return (
    <section className="mt-5 rounded-xl border border-emerald-200 bg-emerald-50/60 p-4 dark:border-emerald-400/20 dark:bg-emerald-400/[0.08]">
      <h4 className="font-semibold text-slate-900 dark:text-white">
        Entrega de tesis profesional
      </h4>
      <p className="mt-1 text-sm text-slate-600 dark:text-white/70">
        Sube la versión actual de tu tesis en PDF para guardarla en tus
        documentos académicos.
      </p>
      {checkingExisting && (
        <p className="mt-2 text-xs text-slate-500">
          Buscando tu tesis en el Data Lake...
        </p>
      )}
      {!checkingExisting && existingFileName && !file && (
        <p className="mt-2 text-sm font-medium text-emerald-800 dark:text-emerald-200">
          Ya tienes una tesis cargada: {existingFileName}
        </p>
      )}
      <div className="mt-3 flex flex-col gap-3 sm:flex-row sm:items-center">
        <input
          type="file"
          accept=".pdf,application/pdf"
          disabled={status === "uploading"}
          onChange={(event) => {
            const selected = event.target.files?.[0] ?? null;
            setFile(
              selected && selected.type === "application/pdf" ? selected : null,
            );
            setStatus(
              selected && selected.type === "application/pdf"
                ? "idle"
                : "error",
            );
            setMessage(
              selected && selected.type === "application/pdf"
                ? ""
                : "Selecciona un archivo PDF.",
            );
          }}
          className="block w-full text-sm text-slate-600 file:mr-3 file:rounded-lg file:border-0 file:bg-emerald-700 file:px-3 file:py-2 file:text-sm file:font-semibold file:text-white hover:file:bg-emerald-800 dark:text-white/70"
        />
        <button
          type="button"
          disabled={!file || status === "uploading"}
          onClick={() => void handleUpload()}
          className="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50"
        >
          {status === "uploading"
            ? "Guardando..."
            : existingFileName
              ? "Reemplazar PDF"
              : "Guardar PDF"}
        </button>
      </div>
      {file && (
        <p className="mt-2 text-xs text-slate-500">
          Archivo seleccionado: {file.name}
        </p>
      )}
      {message && (
        <p
          className={`mt-2 text-sm ${status === "error" ? "text-rose-700" : "text-emerald-700"}`}
        >
          {message}
        </p>
      )}
      {status === "uploaded" && activeFileName && (
        <div className="mt-3 flex flex-wrap gap-2">
          <button
            type="button"
            disabled={processing}
            onClick={() => void handleExtract()}
            className="rounded-lg bg-blue-700 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {processing ? "Procesando..." : "Extraer texto a Markdown"}
          </button>
          {markdownReady && (
            <button
              type="button"
              disabled={processing}
              onClick={() =>
                void downloadThesisMarkdown(personId, activeFileName)
              }
              className="rounded-lg border border-blue-700 px-4 py-2 text-sm font-semibold text-blue-800 hover:bg-blue-50 disabled:opacity-50"
            >
              Descargar Markdown
            </button>
          )}
          <button
            type="button"
            disabled={processing}
            onClick={() => void downloadThesis(personId, activeFileName)}
            className="rounded-lg border border-emerald-700 px-4 py-2 text-sm font-semibold text-emerald-800 hover:bg-emerald-100 disabled:opacity-50"
          >
            Descargar PDF
          </button>
          <button
            type="button"
            disabled={processing}
            onClick={() => void handleDelete()}
            className="rounded-lg border border-rose-700 px-4 py-2 text-sm font-semibold text-rose-700 hover:bg-rose-50 disabled:opacity-50"
          >
            Borrar archivos
          </button>
        </div>
      )}
    </section>
  );
}

function SubjectPanel({
  config,
  programs,
  openProgram,
  onOpenAgent,
}: {
  config: SubjectConfig;
  programs: DemoTecProgram[];
  openProgram: (program: DemoTecProgram) => void;
  onOpenAgent: () => void;
}) {
  const priority = programs.find(
    (program) => program.id === config.priorityProgramId,
  );
  const additional = config.additionalProgramIds.map((id) => ({
    id,
    program: programs.find((program) => program.id === id),
  }));
  return (
    <div className="mt-4 grid gap-5 lg:grid-cols-[1.5fr_1fr]">
      <div>
        <section>
          <h4 className="font-semibold text-slate-900 dark:text-white">
            Propósito de la materia
          </h4>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            {config.purpose}
          </p>
        </section>
        <section className="mt-5">
          <h4 className="font-semibold text-slate-900 dark:text-white">
            Qué necesitas lograr
          </h4>
          <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-white/70">
            {config.outcomes.map((outcome) => (
              <li key={outcome}>{outcome}</li>
            ))}
          </ul>
        </section>
        <section className="mt-5">
          <h4 className="font-semibold text-slate-900 dark:text-white">
            Programa prioritario
          </h4>
          {config.subjectKey === "Tesis Profesional" && (
            <div className="mt-2 rounded-xl border-2 border-emerald-300 bg-emerald-50/70 p-4 dark:border-emerald-400/30 dark:bg-emerald-400/[0.08]">
              <div className="flex items-center gap-2">
                <span className="rounded-full bg-emerald-600 px-2.5 py-1 text-[11px] font-semibold text-white">
                  Disponible ahora
                </span>
                <span className="text-xs font-semibold text-emerald-800 dark:text-emerald-200">
                  Simulador Lab · Learning Labs
                </span>
              </div>
              <h5 className="mt-3 font-semibold text-slate-900 dark:text-white">
                Simulación: Defensa de Tesis Profesional
              </h5>
              <p className="mt-2 text-sm leading-6 text-slate-700 dark:text-white/80">
                <strong>Objetivo:</strong> El estudiante se presenta en una sala
                de conferencias grande del Tecnológico de Monterrey, ante un
                jurado formal de sinodales sentados frente a una mesa, con
                proyector y pantalla para la presentación. El ambiente es
                solemne y académico, propio de un examen de grado. El sinodal ha
                leído previamente la tesis completa (documento cargado) y
                comienza a cuestionar al estudiante sobre el planteamiento del
                problema, las hipótesis, la metodología, los resultados del
                ensamble y las limitaciones, exigiendo justificaciones claras y
                señalando cualquier inconsistencia en el documento.
              </p>
              <Link
                to="/learning-labs"
                className="mt-4 inline-flex items-center gap-1.5 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800"
              >
                Practicar en Learning Labs
              </Link>
            </div>
          )}
          <div className="mt-4 flex items-center gap-2">
            <span className="rounded-full bg-amber-50 px-2.5 py-1 text-[11px] font-semibold text-amber-700 dark:bg-amber-400/10 dark:text-amber-300">
              En desarrollo por el Tec
            </span>
            <span className="text-xs text-slate-500">
              Curso todavía no disponible para tomar
            </span>
          </div>
          {priority ? (
            <div className="mt-2">
              <SemesterProgramCard
                program={priority}
                onOpen={() => openProgram(priority)}
                added={false}
              />
            </div>
          ) : (
            <p className="mt-2 text-sm text-slate-500">
              Programa no disponible en el catálogo de demostración.
            </p>
          )}
        </section>
        <section className="mt-5">
          <h4 className="font-semibold text-slate-900 dark:text-white">
            Otros programas relacionados
          </h4>
          <div className="mt-2 space-y-2">
            {additional.map(({ id, program }) => (
              <div
                key={id}
                className="flex flex-col gap-2 rounded-xl border border-slate-200 p-3 sm:flex-row sm:items-center sm:justify-between dark:border-white/10"
              >
                <p className="text-sm text-slate-600 dark:text-white/70">
                  {program
                    ? program.title.es
                    : "Programa no disponible en el catálogo de demostración."}
                </p>
                {program && (
                  <button
                    type="button"
                    onClick={() => openProgram(program)}
                    className="text-left text-sm font-semibold text-blue-600"
                  >
                    Ver
                  </button>
                )}
              </div>
            ))}
          </div>
        </section>
      </div>
      <aside className="space-y-4">
        <Card className="border-teal-200 bg-teal-50/60 p-5 dark:border-teal-400/20 dark:bg-teal-400/[0.08]">
          <p className="text-xs font-semibold uppercase tracking-wide text-teal-700">
            Agente de acompañamiento
          </p>
          <p className="mt-2 text-lg font-semibold text-slate-900 dark:text-white">
            {config.agent.name}
          </p>
          <span className="mt-2 inline-block rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-teal-700 dark:bg-white/10 dark:text-teal-200">
            Agente de demostración
          </span>
          <p className="mt-3 text-sm text-slate-600 dark:text-white/70">
            {config.agent.description}
          </p>
          <ul className="mt-3 list-disc space-y-1 pl-5 text-xs text-slate-600 dark:text-white/70">
            {config.agent.can.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ul>
          <button
            type="button"
            onClick={onOpenAgent}
            className="mt-4 w-full rounded-lg bg-teal-700 px-4 py-2 text-sm font-semibold text-white hover:bg-teal-800"
          >
            Abrir acompañamiento
          </button>
        </Card>
        <Card className="p-5">
          <p className="text-xs font-semibold uppercase tracking-wide text-blue-700">
            Siguiente paso recomendado
          </p>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            {config.agent.nextStep}
          </p>
        </Card>
      </aside>
    </div>
  );
}

function demoInstructor(program: DemoTecProgram) {
  if (program.id === "executive-ai-communication")
    return {
      name: "Laura Villarreal",
      specialty: "Comunicación Ejecutiva y Consultoría Tecnológica",
    };
  if (program.id === "client-discovery-technical-professionals")
    return {
      name: "Javier Cárdenas",
      specialty: "Consultoría Tecnológica y Descubrimiento de Necesidades",
    };
  if (program.id === "technical-storytelling")
    return {
      name: "Dra. Elena Garza",
      specialty: "Comunicación y Presentaciones Profesionales",
    };
  if (program.id === "technology-stakeholder-management")
    return {
      name: "Marcos Treviño",
      specialty: "Transformación Digital y Gestión de Stakeholders",
    };
  if (program.id === "technology-leadership-decisions")
    return {
      name: "Ing. Carlos Mendoza",
      specialty: "Liderazgo de Equipos Tecnológicos",
    };
  if (program.id === "applied-research-methodology")
    return {
      name: "Dra. Mariana Salazar",
      specialty: "Metodología de Investigación",
    };
  if (program.id === "enterprise-ai-architecture")
    return {
      name: "Dr. Alejandro Torres",
      specialty: "Arquitectura Empresarial de Inteligencia Artificial",
    };
  if (program.id === "technology-leadership")
    return {
      name: "Laura Villarreal",
      specialty: "Liderazgo y Comunicación Ejecutiva",
    };
  if (program.id === "academic-presentations")
    return { name: "Dra. Elena Garza", specialty: "Comunicación Académica" };
  return null;
}

function SemesterProgramModal({
  program,
  onClose,
  showDemoLabel = true,
  recommendationReason,
}: {
  program: DemoTecProgram;
  added?: boolean;
  onAdd?: () => void;
  onClose: () => void;
  showDemoLabel?: boolean;
  recommendationReason?: string;
  actionLabel?: string;
}) {
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    closeButtonRef.current?.focus();
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", closeOnEscape);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", closeOnEscape);
      document.body.style.overflow = "";
    };
  }, [onClose]);
  const instructor = demoInstructor(program);
  const isHumanOs = program.modality === "Curso Human OS";
  // isHumanCapabilitiesProgram removed
  // action label replaced by direct course link
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="semester-program-title"
    >
      <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl dark:bg-slate-900">
        <div className="flex items-start justify-between gap-4">
          <div className="flex flex-wrap gap-2">
            <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700">
              {program.category}
            </span>
            {showDemoLabel && (
              <span className="rounded-full bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-700">
                Programa de demostración
              </span>
            )}
          </div>
          <button
            ref={closeButtonRef}
            type="button"
            onClick={onClose}
            className="rounded-lg px-2 text-xl text-slate-500 hover:bg-slate-100"
            aria-label="Cerrar"
          >
            ×
          </button>
        </div>
        <h2
          id="semester-program-title"
          className="mt-5 text-2xl font-semibold text-slate-950 dark:text-white"
        >
          {program.title.es}
        </h2>
        <div className="mt-3 flex flex-wrap gap-2 text-xs font-medium text-slate-600 dark:text-white/70">
          <span>{program.modality}</span>
          <span>·</span>
          <span>{program.duration}</span>
          <span>·</span>
          <span>{program.level}</span>
          {program.id === "technology-leadership" ||
          program.id === "academic-presentations" ? (
            <>
              <span>·</span>
              <span>Campus Monterrey</span>
            </>
          ) : null}
        </div>
        <p className="mt-5 text-sm leading-6 text-slate-600 dark:text-white/70">
          {program.shortDescription.es}
        </p>
        {instructor && (
          <div className="mt-5 rounded-xl border border-slate-200 p-4 dark:border-white/10">
            <p className="text-xs font-semibold uppercase tracking-wide text-slate-500">
              Perfil de instructor para esta experiencia
            </p>
            <p className="mt-2 font-semibold text-slate-900 dark:text-white">
              {instructor.name}
            </p>
            <p className="text-sm text-slate-500">{instructor.specialty}</p>
          </div>
        )}
        {isHumanOs && (
          <div className="mt-5 rounded-xl border border-teal-200 bg-teal-50 p-4 text-sm text-teal-900 dark:border-teal-400/20 dark:bg-teal-400/10 dark:text-teal-100">
            <p className="font-semibold">
              Memory Paradox · Graph Learning · Engrams
            </p>
            <p className="mt-2">
              Cada Engram incorpora sus propios assessments, recall y mecanismos
              de aprendizaje.
            </p>
            {program.id === "human-os-executive-communication" && (
              <ol className="mt-3 list-decimal space-y-1 pl-5">
                {[
                  "Comprende a tu audiencia",
                  "Estructura el mensaje",
                  "Traduce tecnología en valor",
                  "Comunica decisiones y alternativas",
                  "Responde preguntas y objeciones",
                  "Presenta una solución técnica",
                ].map((engram) => (
                  <li key={engram}>{engram}</li>
                ))}
              </ol>
            )}
          </div>
        )}
        <div className="mt-5 grid gap-5 sm:grid-cols-2">
          <section>
            <h3 className="font-semibold text-slate-900 dark:text-white">
              Por qué se recomienda
            </h3>
            <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
              {recommendationReason ?? program.recommendationReason?.es}
            </p>
          </section>
          <section>
            <h3 className="font-semibold text-slate-900 dark:text-white">
              Materias relacionadas
            </h3>
            <div className="mt-2 flex flex-wrap gap-2">
              {program.relatedSubjects.map((subject) => (
                <span
                  key={subject}
                  className="rounded-full bg-slate-100 px-2.5 py-1 text-xs text-slate-700 dark:bg-white/10 dark:text-white/70"
                >
                  {subject}
                </span>
              ))}
            </div>
          </section>
        </div>
        <section className="mt-5">
          <h3 className="font-semibold text-slate-900 dark:text-white">
            Evidencia esperada
          </h3>
          <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-white/70">
            {program.evidenceOutcomes.es.map((outcome) => (
              <li key={outcome}>{outcome}</li>
            ))}
          </ul>
        </section>
        <section className="mt-5">
          <h3 className="font-semibold text-slate-900 dark:text-white">
            Actividades
          </h3>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            {program.skills.slice(0, 3).join(" · ")}
          </p>
        </section>
        {program.evidenceOutcomes.es[0] &&
          !program.evidenceOutcomes.es[0].includes(
            "resolver un problema aplicado",
          ) && (
            <p className="mt-5 text-sm text-slate-600 dark:text-white/70">
              <strong>Resultado esperado:</strong>{" "}
              {program.evidenceOutcomes.es[0]}
            </p>
          )}
        <div className="mt-6 flex flex-col gap-3 border-t border-slate-200 pt-5 dark:border-white/10 sm:flex-row sm:items-center">
          <Link
            to={
              program.id === "artificial-intelligence-foundations"
                ? "/structured-learning/eac67e4c-b0db-47e5-8966-ee86160de5d9"
                : "/available-courses"
            }
            className="inline-flex items-center gap-2 rounded-lg bg-teal-600 px-4 py-2 text-sm font-semibold text-white hover:bg-teal-700"
          >
            Ir a curso <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      </div>
    </div>
  );
}

function SemesterProgramCard({
  program,
  compact,
  onOpen,
  added,
}: {
  program: DemoTecProgram;
  compact?: boolean;
  onOpen: () => void;
  added: boolean;
}) {
  const { user } = useAuth();
  return (
    <Card className={compact ? "min-w-[260px] snap-start p-4" : "p-5"}>
      <div className="flex items-start justify-between gap-3">
        <span className="text-xs font-semibold uppercase tracking-wide text-blue-700">
          {program.category}
        </span>
        <span className="text-[10px] font-semibold text-amber-700">
          Programa de demostración
        </span>
      </div>
      <h3 className="mt-3 font-semibold text-indigo-700 dark:text-indigo-300">
        {program.title.es}
      </h3>
      <p className="mt-2 text-xs text-slate-500">
        {program.modality} · {program.duration}
      </p>
      {!compact && (
        <>
          <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
            {program.shortDescription.es}
          </p>
          <p className="mt-3 text-xs text-slate-500">
            {program.relatedSubjects.slice(0, 2).join(" · ")}
          </p>
          <p className="mt-3 text-sm text-slate-600 dark:text-white/70">
            {program.recommendationReason?.es}
          </p>
          {program.id === "thesis-research-writing-defense" && (
            <ThesisUpload personId={user?.personId ?? ""} />
          )}
        </>
      )}
      <button
        type="button"
        onClick={onOpen}
        className="mt-4 text-sm font-semibold text-blue-600"
      >
        {compact ? "Ver" : "Ver recomendación"} {added ? "· Agregado" : ""}
      </button>
    </Card>
  );
}

function SemesterTimeline({
  findProgram,
}: {
  findProgram: (id: string) => DemoTecProgram | undefined;
}) {
  const steps = [
    [
      "Define",
      "Fortalece la pregunta, el alcance y la metodología de tu investigación.",
      ["applied-research-methodology", "thesis-research-writing-defense"],
    ],
    [
      "Organiza",
      "Conecta literatura, evidencia, entregables y decisiones del proyecto.",
      ["literature-review-evidence", "integrative-project-management"],
    ],
    [
      "Construye",
      "Diseña y documenta la arquitectura del proyecto de inteligencia artificial.",
      ["enterprise-ai-architecture"],
    ],
    [
      "Comunica",
      "Presenta tus decisiones, evidencia, resultados y aprendizajes.",
      ["academic-presentations", "technology-leadership"],
    ],
  ] as const;
  return (
    <div className="grid gap-4 md:grid-cols-4">
      {steps.map(([title, text, ids], index) => (
        <div
          key={title}
          className="relative rounded-xl border border-slate-200 p-4 dark:border-white/10"
        >
          <span className="text-xs font-bold text-blue-600">
            Paso {index + 1}
          </span>
          <h3 className="mt-2 font-semibold text-slate-900 dark:text-white">
            {title}
          </h3>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            {text}
          </p>
          <div className="mt-3 flex flex-wrap gap-1">
            {ids.map((id) => (
              <span key={id} className="text-xs text-slate-500">
                {findProgram(id)?.title.es}
              </span>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}

export function TecSemestreActualPage() {
  const { resumeQuery } = useTecProfile();
  const semester = resumeQuery.data?.studentAcademicProfile?.semesters.find(
    (item) => /actual/i.test(item.name ?? ""),
  );
  const subjects = semester?.subjects.filter((item) => clean(item.name)) ?? [];
  const programs = useMemo(() => demoTecPrograms, []);
  const configBySubject = new Map(
    subjectConfigs.map((config) => [config.subjectKey, config]),
  );
  const [openSubject, setOpenSubject] = useState(subjects[0]?.name ?? "");
  const [selectedProgram, setSelectedProgram] = useState<DemoTecProgram | null>(
    null,
  );
  const [selectedAgent, setSelectedAgent] = useState<{
    config: SubjectConfig["agent"];
    subject: string;
  } | null>(null);
  const openProgram = (program: DemoTecProgram) => setSelectedProgram(program);
  const relatedPrograms = (subject: string) =>
    programs.filter(
      (program) =>
        subjectConfigs.find((config) => config.subjectKey === subject)
          ?.priorityProgramId === program.id ||
        subjectConfigs
          .find((config) => config.subjectKey === subject)
          ?.additionalProgramIds.includes(program.id),
    );
  const summaryProgramCount = new Set(
    subjects.flatMap((subject) =>
      relatedPrograms(subject.name ?? "").map((program) => program.id),
    ),
  ).size;
  return (
    <TecPage
      title="El Tec está contigo en tu semestre actual"
      description="Conectamos tus materias actuales con programas y experiencias que pueden ayudarte a fortalecer conocimientos, avanzar en tus proyectos y prepararte para completar con éxito esta etapa académica."
    >
      <div className="space-y-8">
        <Card className="p-6">
          <h2 className="text-lg font-semibold text-slate-950 dark:text-white">
            Resumen de tu semestre
          </h2>
          <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
            Estás cursando {subjects.length} materias. Estas recomendaciones
            pueden ayudarte a prepararte antes de que estén disponibles las
            evaluaciones finales.
          </p>
          <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div>
              <p className="text-xs text-slate-500">Materias en curso</p>
              <p className="mt-1 text-xl font-semibold">{subjects.length}</p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Programas relacionados</p>
              <p className="mt-1 text-xl font-semibold">
                {summaryProgramCount}
              </p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Etapa</p>
              <p className="mt-1 text-sm font-semibold">
                {resumeQuery.data?.studentAcademicProfile?.academicStage ||
                  "No disponible"}
              </p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Estado</p>
              <p className="mt-1 text-sm font-semibold text-emerald-700">
                En progreso
              </p>
            </div>
          </div>
        </Card>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Tus materias y su acompañamiento
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Selecciona una materia para conocer los programas, recursos y
            próximos pasos relacionados con ella.
          </p>
          <div className="mt-4 space-y-3">
            {subjects.map((subject, index) => {
              const subjectName = subject.name ?? "";
              const config = configBySubject.get(subjectName);
              const related = relatedPrograms(subjectName);
              const isOpen = openSubject === subjectName;
              return (
                <Card key={subjectName} className="overflow-hidden">
                  <button
                    type="button"
                    className="flex w-full items-start justify-between gap-4 p-5 text-left focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600"
                    aria-expanded={isOpen}
                    aria-controls={`subject-panel-${index}`}
                    onClick={() => setOpenSubject(isOpen ? "" : subjectName)}
                  >
                    <span>
                      <span className="block text-base font-semibold text-indigo-700 dark:text-indigo-300">
                        {subjectName}
                      </span>
                      <span className="mt-2 flex flex-wrap gap-2 text-xs">
                        <span className="font-semibold text-emerald-700">
                          En curso
                        </span>
                        <span className="text-slate-500">
                          {related.length} programas relacionados
                        </span>
                        {config && (
                          <span className="text-slate-500">
                            {configBySubject.get(subjectName)?.priorityProgramId
                              ? programs.find(
                                  (program) =>
                                    program.id === config.priorityProgramId,
                                )?.title.es
                              : ""}
                          </span>
                        )}
                      </span>
                    </span>
                    <span className="text-xl text-slate-500" aria-hidden="true">
                      {isOpen ? "−" : "+"}
                    </span>
                  </button>
                  {isOpen && config && (
                    <div
                      id={`subject-panel-${index}`}
                      className="border-t border-slate-200 px-5 pb-5 dark:border-white/10"
                    >
                      <SubjectPanel
                        config={config}
                        programs={programs}
                        openProgram={openProgram}
                        onOpenAgent={() =>
                          setSelectedAgent({
                            config: config.agent,
                            subject: subjectName,
                          })
                        }
                      />
                    </div>
                  )}
                </Card>
              );
            })}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Ruta sugerida para tu semestre
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Una secuencia de demostración conectada con tus materias.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-4">
            {[
              [
                "Define",
                "Seminario de Investigación · Tesis Profesional",
                "Metodología de Investigación Aplicada · Preparación, Escritura y Defensa de Tesis",
              ],
              [
                "Organiza",
                "Tesis Profesional · Proyecto Integrador",
                "Revisión de Literatura y Gestión de Evidencia · Gestión de Proyectos Integradores",
              ],
              [
                "Construye",
                "Arquitecturas de IA Empresarial · Proyecto Integrador",
                "Arquitectura Empresarial de Soluciones de IA",
              ],
              [
                "Comunica",
                "Liderazgo Tecnológico · Tesis Profesional · Proyecto Integrador",
                "Liderazgo Tecnológico para Equipos de Alto Impacto · Presentaciones Académicas de Alto Impacto",
              ],
            ].map(([title, subjectsText, programsText], index) => (
              <Card key={title} className="p-4">
                <span className="text-xs font-bold text-blue-600">
                  Paso {index + 1}
                </span>
                <h3 className="mt-2 font-semibold text-slate-900 dark:text-white">
                  {title}
                </h3>
                <p className="mt-2 text-sm font-medium text-slate-700 dark:text-white/80">
                  {subjectsText}
                </p>
                <p className="mt-2 text-xs leading-5 text-slate-500">
                  {programsText}
                </p>
              </Card>
            ))}
          </div>
        </section>
        <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 text-white shadow-lg dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold">
            El Tec continúa contigo durante esta etapa
          </h2>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-300">
            No necesitas esperar a la calificación final para fortalecer tu
            preparación. Conectamos tus materias actuales con experiencias que
            pueden ayudarte a avanzar con mayor claridad, evidencia y confianza.
          </p>
          <Link
            to="/available-courses"
            className="mt-4 inline-block text-sm font-semibold text-blue-300"
          >
            Explorar todos los programas
          </Link>
        </div>
      </div>
      {selectedProgram && (
        <SemesterProgramModal
          program={selectedProgram}
          added={false}
          onAdd={() => undefined}
          onClose={() => setSelectedProgram(null)}
        />
      )}
      {selectedAgent && (
        <AgentModal
          config={selectedAgent.config}
          subject={selectedAgent.subject}
          onClose={() => setSelectedAgent(null)}
        />
      )}
    </TecPage>
  );
}

export function TecSemestreActualLegacyPage() {
  const { resumeQuery } = useTecProfile();
  const semester = resumeQuery.data?.studentAcademicProfile?.semesters.find(
    (s) => /actual/i.test(s.name ?? ""),
  );
  const subjects = semester?.subjects.filter((s) => clean(s.name)) ?? [];
  const semesterPrograms = useMemo(
    () =>
      demoTecPrograms.filter((program) =>
        [
          "thesis-research-writing-defense",
          "applied-research-methodology",
          "literature-review-evidence",
          "enterprise-ai-architecture",
          "technology-leadership",
          "academic-presentations",
          "integrative-project-management",
        ].includes(program.id),
      ),
    [],
  );
  const programsBySubject = (subject: string) =>
    semesterPrograms.filter((program) =>
      program.relatedSubjects.includes(subject),
    );
  const [selectedProgram, setSelectedProgram] = useState<DemoTecProgram | null>(
    null,
  );
  const [addedProgramIds, setAddedProgramIds] = useState<Set<string>>(
    () => new Set(),
  );
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const openProgram = (
    program: DemoTecProgram,
    trigger?: HTMLButtonElement,
  ) => {
    triggerRef.current = trigger ?? null;
    setSelectedProgram(program);
  };
  const closeProgram = () => {
    setSelectedProgram(null);
    triggerRef.current?.focus();
  };
  const priorityIds = [
    "thesis-research-writing-defense",
    "enterprise-ai-architecture",
    "technology-leadership",
  ];
  const additionalIds = [
    "applied-research-methodology",
    "literature-review-evidence",
    "academic-presentations",
    "integrative-project-management",
  ];
  return (
    <TecPage
      title="El Tec está contigo en tu semestre actual"
      description="Conectamos tus materias actuales con programas y experiencias que pueden ayudarte a fortalecer conocimientos, avanzar en tus proyectos y prepararte para completar con éxito esta etapa académica."
    >
      <div className="space-y-8">
        <Card className="p-6">
          <h2 className="text-lg font-semibold text-slate-950 dark:text-white">
            Resumen de tu semestre
          </h2>
          <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
            Estás cursando {subjects.length} materias. Estas recomendaciones
            pueden ayudarte a prepararte antes de que estén disponibles las
            evaluaciones finales.
          </p>
          <div className="mt-5 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div>
              <p className="text-xs text-slate-500">Materias en curso</p>
              <p className="mt-1 text-xl font-semibold">{subjects.length}</p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Programas relacionados</p>
              <p className="mt-1 text-xl font-semibold">
                {
                  semesterPrograms.filter((program) =>
                    subjects.some((subject) =>
                      program.relatedSubjects.includes(subject.name ?? ""),
                    ),
                  ).length
                }
              </p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Etapa</p>
              <p className="mt-1 text-sm font-semibold">
                {resumeQuery.data?.studentAcademicProfile?.academicStage ||
                  "No disponible"}
              </p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Estado</p>
              <p className="mt-1 text-sm font-semibold text-emerald-700">
                En progreso
              </p>
            </div>
          </div>
        </Card>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Materias en curso
          </h2>
          <div className="mt-4 grid gap-3 sm:grid-cols-2">
            {subjects.map((subject) => {
              const related = programsBySubject(subject.name ?? "");
              return (
                <Card key={subject.name} className="p-4">
                  <p className="font-semibold text-slate-900 dark:text-white">
                    {subject.name}
                  </p>
                  <div className="mt-2 flex items-center justify-between gap-3">
                    <span className="text-xs font-semibold text-emerald-700">
                      En curso
                    </span>
                    <span className="text-xs text-slate-500">
                      {related.length} programas relacionados
                    </span>
                  </div>
                  <button
                    type="button"
                    onClick={(event) =>
                      related[0] && openProgram(related[0], event.currentTarget)
                    }
                    className="mt-4 text-sm font-semibold text-blue-600"
                  >
                    Ver programas relacionados
                  </button>
                </Card>
              );
            })}
          </div>
        </section>
        <section>
          <div className="flex items-center justify-between gap-3">
            <div>
              <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
                Prioridades para completar esta etapa
              </h2>
              <p className="mt-1 text-xs font-semibold text-amber-700">
                Prioridades de demostración
              </p>
            </div>
          </div>
          <div className="mt-4 grid gap-3 md:grid-cols-2">
            {[
              "Estructurar y avanzar tu tesis.",
              "Organizar evidencia y revisión de literatura.",
              "Diseñar y defender una arquitectura empresarial de IA.",
              "Fortalecer liderazgo, comunicación y ejecución del proyecto.",
            ].map((priority) => (
              <Card
                key={priority}
                className="p-4 text-sm text-slate-700 dark:text-white/70"
              >
                {priority}
              </Card>
            ))}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Programas para acompañar tu semestre
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Seleccionamos experiencias relacionadas con tus materias, tu tesis y
            tu proyecto integrador.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {priorityIds.map((id) => {
              const program = semesterPrograms.find((item) => item.id === id)!;
              return (
                <SemesterProgramCard
                  key={id}
                  program={program}
                  added={addedProgramIds.has(id)}
                  onOpen={() => openProgram(program)}
                />
              );
            })}
          </div>
          <h3 className="mt-6 text-lg font-semibold text-slate-950 dark:text-white">
            Más apoyo para tu semestre
          </h3>
          <div className="mt-3 flex snap-x gap-4 overflow-x-auto pb-3 [scroll-snap-type:x_mandatory]">
            {additionalIds.map((id) => {
              const program = semesterPrograms.find((item) => item.id === id)!;
              return (
                <SemesterProgramCard
                  key={id}
                  program={program}
                  compact
                  added={addedProgramIds.has(id)}
                  onOpen={() => openProgram(program)}
                />
              );
            })}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Cómo te acompañamos en cada materia
          </h2>
          <div className="mt-4 space-y-3">
            {subjects.map((subject) => (
              <Card key={subject.name} className="p-4">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <p className="font-semibold text-slate-900 dark:text-white">
                      {subject.name}
                    </p>
                    <p className="mt-1 text-xs font-semibold text-emerald-700">
                      En curso
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {programsBySubject(subject.name ?? "").map((program) => (
                      <button
                        key={program.id}
                        type="button"
                        onClick={() => openProgram(program)}
                        className="rounded-full bg-slate-100 px-3 py-1 text-left text-xs text-slate-700 hover:bg-blue-50 hover:text-blue-700 dark:bg-white/10 dark:text-white/70"
                      >
                        {program.title.es} · {program.modality}
                      </button>
                    ))}
                  </div>
                </div>
              </Card>
            ))}
          </div>
        </section>
        <section>
          <div className="flex items-center gap-3">
            <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
              Ruta sugerida para tu semestre
            </h2>
            <span className="rounded-full bg-amber-50 px-3 py-1 text-xs font-semibold text-amber-700">
              Ruta de demostración
            </span>
          </div>
          <p className="mt-1 text-sm text-slate-500">
            Una secuencia de demostración para organizar tu preparación.
          </p>
          <div className="mt-4">
            <SemesterTimeline
              findProgram={(id) =>
                semesterPrograms.find((program) => program.id === id)
              }
            />
          </div>
        </section>
        <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 text-white shadow-lg dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold">
            El Tec continúa contigo durante esta etapa
          </h2>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-slate-300">
            No necesitas esperar a la calificación final para fortalecer tu
            preparación. Conectamos tus materias actuales con experiencias que
            pueden ayudarte a avanzar con mayor claridad, evidencia y confianza.
          </p>
          <Link
            to="/available-courses"
            className="mt-4 inline-block text-sm font-semibold text-blue-300"
          >
            Explorar todos los programas
          </Link>
        </div>
      </div>
      {selectedProgram && (
        <SemesterProgramModal
          program={selectedProgram}
          added={addedProgramIds.has(selectedProgram.id)}
          onAdd={() =>
            setAddedProgramIds((current) =>
              new Set(current).add(selectedProgram.id),
            )
          }
          onClose={closeProgram}
        />
      )}
    </TecPage>
  );
}

export function TecIndustriaMonterreyPage() {
  const { resumeQuery } = useTecProfile();
  const resume = resumeQuery.data;
  const text = profileText(resume).toLowerCase();
  const industryProgramIds = [
    "azure-ai-fundamentals",
    "llm-enterprise-architecture",
    "neural-networks-deep-learning",
    "enterprise-prompt-engineering",
    "machine-learning-production",
  ];
  const programs = industryProgramIds
    .map((id) => demoTecPrograms.find((program) => program.id === id))
    .filter((program): program is DemoTecProgram => Boolean(program));
  const skills = resume?.skills ?? [];
  const relevantSkills = skills.filter((skill) =>
    /python|machine learning|deep learning|azure|power bi|prompt|llm|openai|data factory/i.test(
      skill,
    ),
  );
  const [showAllSkills, setShowAllSkills] = useState(false);
  const [selectedProgram, setSelectedProgram] = useState<DemoTecProgram | null>(
    null,
  );
  const hasNemak = /nemak/i.test(text);
  const hasAzure = /azure|azure openai/i.test(text);
  const hasAzureMl = /azure machine learning|azure ml studio/i.test(text);
  const hasDataFactory = /azure data factory/i.test(text);
  const hasLlm = /llm|large language model|aplicaciones llm/i.test(text);
  const hasMachineLearning = /machine learning/i.test(text);
  const hasDeepLearning = /deep learning/i.test(text);
  const hasPrompt = /prompt engineering/i.test(text);
  const hasGenerativeAi = /generative ai|ia generativa|azure openai/i.test(
    text,
  );
  const reasonFor = (program: DemoTecProgram) => {
    if (program.id === "azure-ai-fundamentals")
      return hasNemak && hasAzureMl && hasDataFactory
        ? "Tu experiencia en Nemak incluye Azure Machine Learning y Azure Data Factory. Este programa fortalece los fundamentos de Azure AI relacionados con esa experiencia."
        : hasAzure
          ? "Tus capacidades en Microsoft Azure se relacionan con los fundamentos y patrones de solución abordados por este programa."
          : "Este programa permite explorar fundamentos de inteligencia artificial y patrones de solución dentro del ecosistema Azure.";
    if (program.id === "llm-enterprise-architecture")
      return hasLlm && hasAzure && hasMachineLearning
        ? "Tus capacidades actuales en aplicaciones LLM, Azure y Machine Learning se relacionan con esta especialización."
        : hasLlm
          ? "Tu experiencia con aplicaciones LLM puede profundizarse mediante arquitectura, evaluación, seguridad, observabilidad y despliegue."
          : "Este programa permite explorar el diseño integral de aplicaciones empresariales basadas en modelos de lenguaje.";
    if (program.id === "neural-networks-deep-learning")
      return hasMachineLearning && hasDeepLearning
        ? "Tu perfil incluye Machine Learning y Deep Learning. Este curso profundiza tu dominio técnico en arquitecturas neuronales, entrenamiento y evaluación."
        : hasMachineLearning
          ? "Este curso puede ampliar tus fundamentos de Machine Learning hacia arquitecturas neuronales y sistemas de Deep Learning."
          : "Este curso permite profundizar en arquitecturas neuronales, entrenamiento, evaluación e interpretación de modelos.";
    return hasPrompt || hasLlm || /azure openai/i.test(text)
      ? "Tus capacidades en Prompt Engineering, aplicaciones LLM y Azure OpenAI pueden fortalecerse mediante prácticas sistemáticas de diseño, contexto y evaluación."
      : hasGenerativeAi
        ? "Tu experiencia con inteligencia artificial generativa puede profundizarse mediante prácticas estructuradas de diseño y evaluación de instrucciones."
        : "Este curso permite desarrollar prácticas sistemáticas para diseñar y evaluar instrucciones en aplicaciones empresariales de IA generativa.";
  };
  const visibleSkills = showAllSkills
    ? relevantSkills
    : relevantSkills.slice(0, 9);
  return (
    <TecPage
      title="El Tec está contigo para prepararte para la industria en Monterrey"
      description="Conectamos tu perfil, tu experiencia y tus capacidades con tecnologías, industrias y oportunidades profesionales relevantes para tu siguiente etapa."
    >
      <div className="space-y-8">
        <p className="text-xs text-slate-500">
          Catálogo conceptual para esta experiencia
        </p>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Tu conexión con la industria
          </h2>
          <Card className="mt-4 p-6">
            <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
              Tu experiencia y capacidades pueden convertirse en una base para
              participar en proyectos empresariales de alto impacto.
            </p>
            {hasNemak &&
              hasAzure &&
              /mantenimiento predictivo|predictive maintenance/i.test(text) && (
                <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
                  Tu experiencia conecta inteligencia artificial, datos
                  industriales, servicios de nube y mantenimiento predictivo.
                </p>
              )}
          </Card>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Capacidades presentes en tu experiencia
          </h2>
          <div className="mt-4 flex flex-wrap gap-2">
            {visibleSkills.map((skill) => (
              <span
                key={skill}
                className="rounded-full bg-blue-50 px-3 py-1 text-xs font-medium text-blue-700 dark:bg-blue-400/10 dark:text-blue-200"
              >
                {skill}
              </span>
            ))}
          </div>
          {relevantSkills.length > 9 && (
            <button
              type="button"
              onClick={() => setShowAllSkills((value) => !value)}
              className="mt-4 text-sm font-semibold text-blue-600"
            >
              {showAllSkills ? "Mostrar menos" : "Ver todas"}
            </button>
          )}
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Programas para fortalecer tu preparación para la industria
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Explora experiencias relacionadas con las tecnologías, capacidades y
            tipos de soluciones presentes en tu perfil.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            {programs.map((program) => {
              const isReadyDemo =
                program.id === "enterprise-prompt-engineering";
              return (
                <Card
                  key={program.id}
                  className={
                    isReadyDemo
                      ? "flex h-full flex-col border-t-4 border-emerald-500 bg-emerald-50/60 p-5 dark:bg-emerald-400/10"
                      : "flex h-full flex-col border-t-4 border-blue-500 p-5"
                  }
                >
                  <div className="flex items-center justify-between gap-2">
                    <p className="text-xs font-semibold uppercase tracking-wide text-blue-700">
                      {program.recommendationType}
                    </p>
                    {isReadyDemo && (
                      <span className="rounded-full bg-emerald-600 px-2.5 py-1 text-[11px] font-semibold text-white">
                        Disponible ahora
                      </span>
                    )}
                  </div>
                  <h3 className="mt-3 text-lg font-semibold text-slate-950 dark:text-white">
                    {program.title.es}
                  </h3>
                  <p className="mt-1 text-xs text-slate-500">
                    {program.modality === "Virtual"
                      ? "Curso virtual autónomo del Tec"
                      : program.modality === "Live virtual faculty"
                        ? "Profesor virtual en vivo"
                        : program.modality === "Human OS course"
                          ? "Curso Human OS"
                          : program.modality}{" "}
                    · {program.duration}
                  </p>
                  <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
                    {program.shortDescription.es}
                  </p>
                  {program.id === "neural-networks-deep-learning" ||
                  program.id === "enterprise-prompt-engineering" ? (
                    <p className="mt-3 text-xs font-medium text-teal-700">
                      Memory Paradox · Graph Learning · Engrams
                    </p>
                  ) : null}
                  <div className="mt-4 rounded-lg bg-slate-50 p-3 text-xs text-slate-600 dark:bg-white/[0.06] dark:text-white/70">
                    <strong>Por qué te lo recomendamos</strong>
                    <p className="mt-1">{reasonFor(program)}</p>
                  </div>
                  {isReadyDemo && (
                    <div className="mt-4 flex flex-wrap gap-2">
                      <Link
                        to="/structured-learning/64d033e9-06d2-4497-9d34-d3ee385bd3c7"
                        className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white hover:bg-emerald-800"
                      >
                        Ir al curso <ArrowRight className="h-4 w-4" />
                      </Link>
                      <Link
                        to="/ai-agents/64d033e9-06d2-4497-9d34-d3ee385bd3c7"
                        className="inline-flex items-center gap-1.5 rounded-lg border border-emerald-700 px-4 py-2 text-sm font-semibold text-emerald-800 hover:bg-emerald-100 dark:text-emerald-200 dark:hover:bg-emerald-400/10"
                      >
                        Hablar con el agente de {program.title.es}{" "}
                        <ArrowRight className="h-4 w-4" />
                      </Link>
                    </div>
                  )}
                  <button
                    type="button"
                    onClick={() => setSelectedProgram(program)}
                    className="mt-auto pt-4 text-left text-sm font-semibold text-blue-600"
                  >
                    Ver recomendación
                  </button>
                </Card>
              );
            })}
          </div>
        </section>
        <section>
          <p className="text-xs text-slate-500">
            Ruta sugerida para esta experiencia
          </p>
          <h2 className="mt-2 text-xl font-semibold text-slate-950 dark:text-white">
            Ruta sugerida para fortalecer tu preparación
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Una secuencia para conectar fundamentos, profundidad técnica y
            diseño de soluciones.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-4">
            {[
              [
                "Consolida",
                "Fortalece los fundamentos de servicios y patrones de inteligencia artificial en nube.",
                "AI-900: Microsoft Azure AI Fundamentals",
              ],
              [
                "Profundiza",
                "Amplía tu dominio técnico en modelos, arquitecturas neuronales y evaluación.",
                "Redes Neuronales y Sistemas de Deep Learning",
              ],
              [
                "Diseña",
                "Construye prácticas sistemáticas para aplicaciones de inteligencia artificial generativa.",
                "Prompt Engineering para IA Empresarial",
              ],
              [
                "Integra",
                "Conecta datos, modelos, evaluación, seguridad y despliegue dentro de una arquitectura empresarial completa.",
                "Arquitectura Empresarial de Aplicaciones LLM",
              ],
            ].map(([title, body, program]) => (
              <Card key={title} className="p-4">
                <p className="text-xs font-bold text-blue-600">{title}</p>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {body}
                </p>
                <p className="mt-3 text-sm font-semibold text-slate-900 dark:text-white">
                  {program}
                </p>
              </Card>
            ))}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Evidencia que puedes desarrollar
          </h2>
          <div className="mt-4 grid gap-3 sm:grid-cols-2">
            <Card className="p-4 text-sm">Mapa de servicios de Azure AI.</Card>
            <Card className="p-4 text-sm">
              Diseño documentado de una red neuronal.
            </Card>
            <Card className="p-4 text-sm">
              Biblioteca evaluada de instrucciones.
            </Card>
            <Card className="p-4 text-sm">
              Diagrama de arquitectura empresarial LLM.
            </Card>
            <Card className="p-4 text-sm">Plan de evaluación.</Card>
            <Card className="p-4 text-sm">
              Checklist de seguridad y despliegue.
            </Card>
          </div>
          <p className="mt-4 text-sm leading-6 text-slate-600 dark:text-white/70">
            Estas evidencias pueden ayudarte a hacer visible tu capacidad para
            diseñar, evaluar y explicar soluciones de inteligencia artificial.
          </p>
        </section>
        <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 text-white shadow-lg dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold">
            El Tec continúa contigo en tu transición hacia la industria
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-300">
            Tu preparación puede seguir evolucionando después de graduarte.
            Conectamos tu experiencia, tus capacidades y nuevas oportunidades de
            aprendizaje para ayudarte a mantenerte vigente.
          </p>
          <Link
            to="/available-courses"
            className="mt-4 inline-block text-sm font-semibold text-blue-300"
          >
            Explorar todos los programas
          </Link>
        </div>
      </div>
      {selectedProgram && (
        <SemesterProgramModal
          program={selectedProgram}
          added={false}
          onAdd={() => undefined}
          onClose={() => setSelectedProgram(null)}
          showDemoLabel={false}
        />
      )}
    </TecPage>
  );
}

export function TecObjetivoProfesionalPage() {
  const { resumeQuery } = useTecProfile();
  const resume = resumeQuery.data;
  const objective = clean(resume?.professionalObjective);
  const machineLearningProgram = demoTecPrograms.find(
    (program) => program.id === "machine-learning-production",
  );
  const profileEvidence = profileText(resume).toLowerCase();
  const hasPredictiveMaintenance =
    /mantenimiento\s+predictiv|predictive\s+maintenance|mantenimiento\s+de\s+activos|condition\s+monitor/i.test(
      profileEvidence,
    );
  const hasModelBuilding =
    /constru(?:ir|cción)|desarrollo\s+de\s+model|model(?:os|ing)|entrenamiento\s+de\s+model|machine\s+learning|deep\s+learning/i.test(
      profileEvidence,
    );
  const hasIndustrialExperience =
    /nemak|industrial|industria|manufactur|planta|producci[oó]n|operaci[oó]n|f[aá]brica|automotriz|supply\s+chain/i.test(
      profileEvidence,
    );
  const hasPersonalizedEvidence =
    hasPredictiveMaintenance && hasModelBuilding && hasIndustrialExperience;
  const personalizedReason =
    "Tu experiencia en mantenimiento predictivo demuestra capacidad para construir modelos. Este curso fortalece su despliegue, monitoreo y operación en entornos productivos.";
  const fallbackReason =
    "Este curso fortalece las capacidades necesarias para desplegar, monitorear y operar modelos de Machine Learning en entornos productivos.";
  const [addedToProfessionalPath, setAddedToProfessionalPath] = useState(false);

  if (!machineLearningProgram) return null;

  return (
    <TecPage
      title="El Tec está contigo para alcanzar tu objetivo profesional"
      description="Convertimos tu aspiración profesional en una ruta de capacidades, aprendizaje, aplicación y evidencia."
    >
      <div className="space-y-8">
        <Card className="p-6">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-emerald-600">
            Tu objetivo profesional
          </p>
          <h2 className="mt-3 text-xl font-semibold text-slate-950 dark:text-white">
            {objective ||
              "Define tu objetivo profesional para comenzar a construir tu ruta."}
          </h2>
          <Link
            to="/objectives"
            className="mt-4 inline-block text-sm font-semibold text-blue-600"
          >
            Gestionar mis objetivos
          </Link>
        </Card>
        <section aria-labelledby="fde-main-program">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-600">
                Programa principal
              </p>
              <h2
                id="fde-main-program"
                className="mt-2 text-2xl font-semibold text-slate-950 dark:text-white"
              >
                Metodología Global FDE v2.0
              </h2>
              <Link
                to="/structured-learning/173dd481-7bdf-4541-b0e4-662efbbd5c04"
                className="mt-2 inline-flex text-sm font-semibold text-blue-600 hover:text-blue-800"
              >
                Ver programa
              </Link>
            </div>
            <span className="rounded-full bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700">
              Programa principal
            </span>
          </div>
          <Card className="mt-4 border-blue-200 bg-blue-50/50 p-6 dark:border-blue-400/20 dark:bg-blue-400/[0.08]">
            <p className="text-sm leading-6 text-slate-700 dark:text-white/75">
              Metodología integral para transformar una necesidad en una
              capacidad operable.
            </p>
            <div className="mt-5 rounded-xl border border-blue-200 bg-white/70 p-4 dark:border-blue-400/20 dark:bg-white/[0.05]">
              <p className="text-sm font-semibold text-slate-900 dark:text-white">
                Etapa: Operar y evolucionar
              </p>
              <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                Conecta la operación, el aprendizaje y la evolución continua de
                las capacidades empresariales.
              </p>
            </div>
          </Card>
        </section>
        <section aria-labelledby="complementary-program">
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-teal-600">
            Programa complementario
          </p>
          <h2
            id="complementary-program"
            className="mt-2 text-xl font-semibold text-slate-950 dark:text-white"
          >
            Programa complementario para tu ruta
          </h2>
          <Card className="mt-4 border-t-4 border-teal-500 p-6">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
              <div>
                <h3 className="text-lg font-semibold text-slate-950 dark:text-white">
                  {machineLearningProgram.title.es}
                </h3>
                <p className="mt-2 text-sm font-medium text-slate-600 dark:text-white/70">
                  Curso Human OS · 12 horas estimadas
                </p>
                <p className="mt-2 text-xs font-semibold uppercase tracking-wide text-teal-700">
                  De modelos a capacidades operativas
                </p>
              </div>
              <span className="rounded-full bg-teal-50 px-3 py-1 text-xs font-semibold text-teal-700">
                Programa complementario
              </span>
            </div>
            <p className="mt-4 text-sm leading-6 text-slate-600 dark:text-white/70">
              Lleva modelos de Machine Learning desde la experimentación hasta
              su implementación, monitoreo y operación en entornos productivos.
            </p>
            <div className="mt-5 rounded-xl bg-slate-50 p-4 dark:bg-white/[0.06]">
              <h3 className="font-semibold text-slate-900 dark:text-white">
                Por qué te lo recomendamos
              </h3>
              <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                {hasPersonalizedEvidence ? personalizedReason : fallbackReason}
              </p>
            </div>
            <div className="mt-5 border-t border-slate-200 pt-5 dark:border-white/10">
              <h3 className="font-semibold text-slate-900 dark:text-white">
                Cómo complementa tu ruta FDE
              </h3>
              <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
                Metodología Global FDE te ayuda a comprender, diseñar,
                construir, operar y gobernar una capacidad empresarial completa.
                Machine Learning en Producción profundiza específicamente en el
                despliegue, monitoreo y operación de los modelos que forman
                parte de esa capacidad.
              </p>
              <div className="mt-4 grid gap-3 sm:grid-cols-2">
                <div className="rounded-lg border border-blue-200 p-3 text-sm dark:border-blue-400/20">
                  <strong>Metodología Global FDE</strong>
                  <p className="mt-1 text-slate-600 dark:text-white/70">
                    Metodología integral para transformar una necesidad en una
                    capacidad operable.
                  </p>
                </div>
                <div className="rounded-lg border border-teal-200 p-3 text-sm dark:border-teal-400/20">
                  <strong>Machine Learning en Producción</strong>
                  <p className="mt-1 text-slate-600 dark:text-white/70">
                    Profundización técnica para desplegar, monitorear y mantener
                    modelos.
                  </p>
                </div>
              </div>
            </div>
            <div className="mt-5">
              <h3 className="font-semibold text-slate-900 dark:text-white">
                Metodología del curso
              </h3>
              <p className="mt-2 text-sm font-medium text-teal-700">
                Memory Paradox · Graph Learning · Engrams
              </p>
              <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                Cada Engram incorpora sus propios assessments, recall y
                mecanismos de aprendizaje.
              </p>
            </div>
            <div className="mt-5">
              <h3 className="font-semibold text-slate-900 dark:text-white">
                Capacidades que desarrollarás
              </h3>
              <p className="mt-1 text-xs text-slate-500">
                Capacidades del curso por desarrollar, no capacidades ya
                dominadas.
              </p>
              <ul className="mt-3 grid gap-2 text-sm text-slate-600 dark:text-white/70 sm:grid-cols-2">
                {machineLearningProgram.skills.map((skill) => (
                  <li
                    key={skill}
                    className="rounded-lg bg-slate-50 px-3 py-2 dark:bg-white/[0.06]"
                  >
                    {skill}
                  </li>
                ))}
              </ul>
            </div>
            <div className="mt-5">
              <h3 className="font-semibold text-slate-900 dark:text-white">
                Evidencia que podrás desarrollar
              </h3>
              <p className="mt-1 text-xs text-slate-500">
                Evidencia futura del proceso de aprendizaje, no evidencia ya
                obtenida.
              </p>
              <ul className="mt-3 space-y-2 text-sm text-slate-600 dark:text-white/70">
                {machineLearningProgram.evidenceOutcomes.es.map((outcome) => (
                  <li
                    key={outcome}
                    className="rounded-lg bg-slate-50 px-3 py-2 dark:bg-white/[0.06]"
                  >
                    {outcome}
                  </li>
                ))}
              </ul>
            </div>
            <div className="mt-6 flex flex-wrap items-center gap-3">
              <Link
                to={`/programs/${machineLearningProgram.id}`}
                className="rounded-lg bg-teal-700 px-4 py-2 text-sm font-semibold text-white hover:bg-teal-800"
              >
                Ver programa
              </Link>
              <button
                type="button"
                onClick={() => setAddedToProfessionalPath(true)}
                className="rounded-lg border border-teal-200 px-4 py-2 text-sm font-semibold text-teal-700 hover:bg-teal-50 dark:border-teal-400/30 dark:text-teal-200"
              >
                {addedToProfessionalPath
                  ? "Agregado a mi ruta profesional"
                  : "Agregar a mi ruta profesional"}
              </button>
            </div>
            {addedToProfessionalPath && (
              <p className="mt-3 text-xs text-slate-500">
                Acción de demostración. Los cambios no se guardan.
              </p>
            )}
          </Card>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Ruta de dominio
          </h2>
          <Card className="mt-4 p-5">
            <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
              Operar y evolucionar una capacidad empresarial requiere conectar
              metodología integral con profundidad técnica en modelos,
              despliegue, monitoreo y operación.
            </p>
          </Card>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Evidencia profesional
          </h2>
          <Card className="mt-4 p-5">
            <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
              La evidencia del programa puede ayudarte a hacer visible tu
              preparación profesional; todavía no representa logros producidos
              por Sofia.
            </p>
          </Card>
        </section>
      </div>
    </TecPage>
  );
}

export function TecFortalecerConocimientosPage() {
  const { resumeQuery } = useTecProfile();
  const resume = resumeQuery.data;
  const academicProfile = resume?.studentAcademicProfile;
  const subjects =
    academicProfile?.semesters.flatMap((semester) => semester.subjects) ?? [];
  const subjectByName = new Map(
    subjects
      .filter((subject) => clean(subject.name))
      .map((subject) => [subject.name!.trim().toLowerCase(), subject]),
  );
  const priorities = [
    {
      name: "Inteligencia Artificial",
      courseId: "artificial-intelligence-foundations",
      priority: "Prioridad alta",
      explanation:
        "Es un fundamento central para Machine Learning, IA generativa, arquitectura de soluciones y toma de decisiones técnicas.",
    },
    {
      name: "Machine Learning",
      courseId: "machine-learning-foundations",
      priority: "Prioridad media-alta",
      explanation:
        "Conecta datos, modelado, evaluación y aplicación de soluciones predictivas en contextos empresariales.",
    },
    {
      name: "Generative AI",
      courseId: "generative-ai-foundations",
      priority: "Prioridad alta",
      explanation:
        "Es relevante para aplicaciones LLM, Prompt Engineering, Azure OpenAI y soluciones empresariales de inteligencia artificial generativa.",
    },
  ]
    .map((item) => ({
      ...item,
      subject: subjectByName.get(item.name.toLowerCase()),
      course: demoTecPrograms.find((program) => program.id === item.courseId),
    }))
    .filter((item) => item.subject && item.course);
  const courses = [
    "artificial-intelligence-foundations",
    "machine-learning-foundations",
    "generative-ai-foundations",
    "azure-ai-fundamentals",
    "enterprise-prompt-engineering",
  ]
    .map((id) => demoTecPrograms.find((program) => program.id === id))
    .filter((program): program is DemoTecProgram => Boolean(program));
  const [selectedProgram, setSelectedProgram] = useState<DemoTecProgram | null>(
    null,
  );
  const [addedIds, setAddedIds] = useState<Set<string>>(() => new Set());
  const [isGradesModalOpen, setIsGradesModalOpen] = useState(false);
  const hasRelevantCapabilities =
    /artificial intelligence|inteligencia artificial|machine learning|azure|software|desarrollo/i.test(
      profileText(resume),
    );
  const formatGrade = (grade: number | null | undefined) =>
    grade === null || grade === undefined
      ? "En curso"
      : `${(grade > 10 ? grade / 10 : grade).toFixed(1)}/10`;
  const modality = (program: DemoTecProgram) =>
    program.modality === "Human OS course"
      ? "Curso Human OS"
      : program.modality === "Live virtual faculty"
        ? "Profesor virtual en vivo"
        : program.modality === "Virtual"
          ? "Curso virtual autónomo del Tec"
          : program.modality;
  const addToPlan = (id: string) =>
    setAddedIds((current) => new Set(current).add(id));
  return (
    <TecPage
      title="El Tec está contigo para fortalecer tus conocimientos"
      description="Utilizamos tu desempeño académico para identificar conocimientos que conviene recuperar, conectar y aplicar antes de avanzar hacia capacidades más complejas."
    >
      <div className="space-y-8">
        <p className="text-sm text-slate-600 dark:text-white/70">
          Este plan no define tu potencial. Te ayuda a enfocar tu esfuerzo donde
          puede generar mayor crecimiento.
        </p>
        <Link
          to="/profile"
          className="inline-flex text-sm font-semibold text-blue-600"
        >
          Ver mis semestres y calificaciones
        </Link>
        <button
          type="button"
          onClick={() => setIsGradesModalOpen(true)}
          className="ml-3 inline-flex items-center gap-1.5 rounded-lg border border-slate-200 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 dark:border-white/15 dark:text-white/80 dark:hover:bg-white/[0.06]"
        >
          Ver calificaciones
        </button>
        <Card className="border-teal-200 bg-teal-50/60 p-6 dark:border-teal-400/20 dark:bg-teal-400/[0.08]">
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Tu plan personalizado
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            Este plan utiliza tus resultados académicos para ayudarte a
            recuperar fundamentos, conectar conceptos y demostrar dominio antes
            de continuar con especializaciones más avanzadas.
          </p>
          <div className="mt-5 grid grid-cols-3 gap-4">
            <div>
              <p className="text-xs text-slate-500">Materias prioritarias</p>
              <p className="mt-1 text-xl font-semibold">{priorities.length}</p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Cursos recomendados</p>
              <p className="mt-1 text-xl font-semibold">{courses.length}</p>
            </div>
            <div>
              <p className="text-xs text-slate-500">Etapas</p>
              <p className="mt-1 text-xl font-semibold">3</p>
            </div>
          </div>
          <div className="mt-5 grid gap-3 sm:grid-cols-2">
            <div>
              <p className="font-semibold text-red-700">Prioridad alta</p>
              <p className="mt-1 text-sm text-slate-600 dark:text-white/70">
                Inteligencia Artificial y Generative AI comparten la
                calificación más baja dentro del conjunto analizado.
              </p>
            </div>
            <div>
              <p className="font-semibold text-orange-700">
                Prioridad media-alta
              </p>
              <p className="mt-1 text-sm text-slate-600 dark:text-white/70">
                Machine Learning requiere fortalecimiento porque conecta los
                fundamentos de IA con el desarrollo, evaluación y despliegue de
                modelos.
              </p>
            </div>
          </div>
        </Card>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Cómo construimos tu plan
          </h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <Card className="p-5">
              <h3 className="font-semibold">Desempeño relativo</h3>
              <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                Identificamos materias concluidas con calificaciones
                relativamente menores dentro de tu propia trayectoria.
              </p>
            </Card>
            <Card className="p-5">
              <h3 className="font-semibold">Relevancia profesional</h3>
              <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                Priorizamos conocimientos relacionados con tu carrera y con tu
                objetivo de convertirte en Forward Deployed Engineer.
              </p>
            </Card>
            {hasRelevantCapabilities && (
              <Card className="p-5">
                <h3 className="font-semibold">
                  Conexión con capacidades actuales
                </h3>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  Relacionamos los fundamentos con tus capacidades en
                  inteligencia artificial, Machine Learning, Azure y desarrollo
                  de software.
                </p>
              </Card>
            )}
            <Card className="p-5">
              <h3 className="font-semibold">Aplicación futura</h3>
              <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                Seleccionamos oportunidades que puedan convertirse en mejores
                decisiones, proyectos y evidencia de dominio.
              </p>
            </Card>
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Materias con mayor oportunidad de mejora
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Estas materias presentan la mayor oportunidad de mejora dentro de tu
            trayectoria académica.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-3">
            {priorities.map(
              ({ name, priority, explanation, subject, course }) => (
                <Card key={name} className="border-t-4 border-red-500 p-5">
                  <h3 className="font-semibold text-slate-950 dark:text-white">
                    {name}
                  </h3>
                  <p className="mt-2 text-xl font-bold text-red-700">
                    {formatGrade(subject?.grade)}
                  </p>
                  <p className="mt-2 text-xs font-semibold text-red-700">
                    {priority}
                  </p>
                  <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
                    {explanation}
                  </p>
                  <div className="mt-4 flex flex-wrap items-center gap-3">
                    <button
                      type="button"
                      onClick={() => course && setSelectedProgram(course)}
                      className="text-sm font-semibold text-blue-600 hover:text-blue-700"
                    >
                      Ver recomendación
                    </button>
                    {name === "Inteligencia Artificial" && (
                      <Link
                        to="/structured-learning/eac67e4c-b0db-47e5-8966-ee86160de5d9"
                        className="inline-flex items-center gap-1.5 text-sm font-semibold text-teal-600 hover:text-teal-700 dark:text-teal-400"
                      >
                        Ir a curso <ArrowRight className="h-3.5 w-3.5" />
                      </Link>
                    )}
                  </div>
                </Card>
              ),
            )}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Cursos recomendados
          </h2>
          <div className="mt-4 grid gap-4 md:grid-cols-3">
            <Link
              to="/structured-learning/eac67e4c-b0db-47e5-8966-ee86160de5d9"
              className="group block cursor-pointer"
            >
              <Card className="flex h-full flex-col justify-between border-t-4 border-teal-500 p-5 transition-all duration-200 hover:border-teal-400 hover:shadow-md dark:hover:border-teal-400">
                <div className="space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="rounded-full bg-teal-50 px-2.5 py-0.5 text-xs font-semibold text-teal-700 dark:bg-teal-400/10 dark:text-teal-300">
                      Inteligencia Artificial
                    </span>
                    <span className="text-xs text-slate-400">·</span>
                    <span className="text-xs text-slate-500 dark:text-white/60">
                      Estructurado
                    </span>
                  </div>
                  <h3 className="text-lg font-semibold text-slate-950 group-hover:text-teal-600 dark:text-white dark:group-hover:text-teal-300">
                    AI Introduction
                  </h3>
                  <p className="text-xs text-slate-500">
                    Curso Human OS · 2 semanas
                  </p>
                  <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
                    Aprende los principios fundamentales de la inteligencia
                    artificial, casos de uso prácticos y cómo aplicarla en
                    escenarios del mundo real.
                  </p>
                  <p className="text-xs font-medium text-teal-700">
                    Memory Paradox · Graph Learning · Engrams
                  </p>
                  <p className="text-xs text-slate-500">
                    Resultado esperado: Dominio práctico de fundamentos de IA
                  </p>
                </div>
                <div className="mt-4 flex items-center gap-2 pt-2 text-sm font-semibold text-teal-600 group-hover:translate-x-1 transition-transform dark:text-teal-400">
                  <span>Iniciar curso</span>
                  <ArrowRight className="h-4 w-4" />
                </div>
              </Card>
            </Link>
            {courses.map((course, index) => (
              <Card
                key={course.id}
                className={index < 3 ? "border-t-4 border-teal-500 p-5" : "p-5"}
              >
                <p className="text-xs font-semibold text-teal-700">
                  {course.relatedSubjects[0] ?? "Aplicación y especialización"}
                </p>
                <h3 className="mt-2 font-semibold text-slate-950 dark:text-white">
                  {course.title.es}
                </h3>
                <p className="mt-1 text-xs text-slate-500">
                  {modality(course)} · {course.duration}
                </p>
                <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {course.shortDescription.es}
                </p>
                {course.modality === "Human OS course" && (
                  <p className="mt-3 text-xs font-medium text-teal-700">
                    Memory Paradox · Graph Learning · Engrams
                  </p>
                )}
                <p className="mt-3 text-xs text-slate-500">
                  Resultado esperado: {course.evidenceOutcomes.es[0]}
                </p>
                <button
                  type="button"
                  onClick={() => setSelectedProgram(course)}
                  className="mt-4 text-sm font-semibold text-blue-600"
                >
                  Ver recomendación
                </button>
              </Card>
            ))}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Tu secuencia de fortalecimiento
          </h2>
          <div className="mt-4 grid gap-4 md:grid-cols-3">
            {[
              [
                "Diagnostica",
                "Identifica qué conceptos de IA, Machine Learning e IA generativa permanecen disponibles y cuáles necesitan recuperación.",
              ],
              [
                "Recupera",
                "Reconstruye los fundamentos mediante Graph Learning, Engrams, recall y aplicación progresiva.",
              ],
              [
                "Demuestra",
                "Produce evidencia de que puedes explicar, seleccionar, construir y evaluar soluciones con autonomía.",
              ],
            ].map(([title, text]) => (
              <Card key={title} className="p-5">
                <p className="text-xs font-bold text-blue-600">{title}</p>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {text}
                </p>
              </Card>
            ))}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Cómo demostrarás el fortalecimiento
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            El objetivo no es solamente completar contenido. Es hacer visible
            que puedes recuperar, conectar y aplicar el conocimiento con
            autonomía.
          </p>
          <div className="mt-4 grid gap-3 sm:grid-cols-2">
            {[
              "Mapa de conceptos",
              "Recuperación sin asistencia",
              "Resolución de problemas",
              "Explicación de decisiones",
              "Aplicación en un caso técnico",
              "Assessment de dominio dentro de cada Engram",
              "Artefacto final relacionado con el curso",
            ].map((item) => (
              <Card key={item} className="p-4 text-sm">
                {item}
              </Card>
            ))}
          </div>
        </section>
        <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 text-white shadow-lg dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold">
            El Tec está contigo para recuperar y demostrar dominio
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-300">
            En Tec 360 nos interesa que sigas aprendiendo, conectando
            conocimientos y desarrollando nuevas capacidades. Una calificación
            es sólo una parte de tu trayectoria: lo más importante es ayudarte a
            maximizar tu potencial humano y convertir lo que aprendes en
            capacidades que puedas aplicar y demostrar.
          </p>
          <p className="mt-3 text-sm leading-6 text-slate-300">
            Tu potencial no queda definido por un número. Estamos contigo para
            enfocar tu esfuerzo, recuperar fundamentos y prepararte para la
            siguiente etapa.
          </p>
          <Link
            to="/available-courses"
            className="mt-4 inline-block text-sm font-semibold text-blue-300"
          >
            Explorar todos los programas
          </Link>
        </div>
      </div>
      {isGradesModalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4"
          onClick={() => setIsGradesModalOpen(false)}
        >
          <div
            className="max-h-[80vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl dark:bg-slate-900"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-semibold text-slate-950 dark:text-white">
                Tus calificaciones
              </h3>
              <button
                type="button"
                onClick={() => setIsGradesModalOpen(false)}
                className="rounded-full p-1 text-slate-400 hover:bg-slate-100 dark:hover:bg-white/10"
              >
                ✕
              </button>
            </div>
            <p className="mt-1 text-xs text-slate-500 dark:text-white/50">
              Resaltamos en rojo las materias con calificación menor a 8.0.
            </p>
            <ul className="mt-4 divide-y divide-slate-100 dark:divide-white/10">
              {subjects
                .filter((subject) => clean(subject.name))
                .map((subject) => {
                  const gradeOutOf10 =
                    subject.grade === null || subject.grade === undefined
                      ? null
                      : subject.grade > 10
                        ? subject.grade / 10
                        : subject.grade;
                  const isLow = gradeOutOf10 !== null && gradeOutOf10 < 8;
                  return (
                    <li
                      key={subject.name}
                      className={`flex items-center justify-between gap-3 py-2.5 ${isLow ? "rounded-lg bg-red-50 px-2 dark:bg-red-400/10" : "px-2"}`}
                    >
                      <span className={`text-sm ${isLow ? "font-semibold text-red-700 dark:text-red-300" : "text-slate-700 dark:text-white/80"}`}>
                        {subject.name}
                      </span>
                      <span className={`text-sm font-bold ${isLow ? "text-red-700 dark:text-red-300" : "text-slate-900 dark:text-white"}`}>
                        {formatGrade(subject.grade)}
                      </span>
                    </li>
                  );
                })}
            </ul>
          </div>
        </div>
      )}
      {selectedProgram && (
        <SemesterProgramModal
          program={selectedProgram}
          added={addedIds.has(selectedProgram.id)}
          onAdd={() => addToPlan(selectedProgram.id)}
          onClose={() => setSelectedProgram(null)}
        />
      )}
    </TecPage>
  );
}

export function TecCapacidadesHumanasLegacyPage() {
  const { resumeQuery } = useTecProfile();
  const objective = clean(resumeQuery.data?.professionalObjective);
  const capabilities = [
    "Comunicación ejecutiva",
    "Storytelling técnico",
    "Descubrimiento con clientes",
    "Escucha activa",
    "Gestión de stakeholders",
    "Liderazgo tecnológico",
    "Manejo de objeciones",
    "Presentaciones de alto impacto",
    "Colaboración interdisciplinaria",
  ];
  return (
    <TecPage
      title="El Tec está contigo para desarrollar capacidades humanas indispensables"
      description="El conocimiento técnico genera mayor impacto cuando puedes comprender necesidades, comunicar decisiones, colaborar con otros y conectar una solución con el valor que necesita una organización."
    >
      <div className="space-y-5">
        <Card className="p-6">
          <h2 className="text-lg font-semibold text-slate-950 dark:text-white">
            Capacidades humanas para tu objetivo
          </h2>
          {/forward deployed engineer/i.test(objective) && (
            <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
              Tu objetivo profesional requiere trabajar directamente con
              clientes, equipos técnicos y líderes empresariales.
            </p>
          )}
          <p className="mt-3 text-xs font-semibold text-orange-700">
            Capacidades de demostración relacionadas con el rol objetivo
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            {capabilities.map((capability) => (
              <span
                key={capability}
                className="rounded-full bg-orange-50 px-3 py-1 text-xs text-orange-800 dark:bg-orange-400/10 dark:text-orange-100"
              >
                {capability}
              </span>
            ))}
          </div>
        </Card>
        <Temporary>
          En el siguiente paso conectaremos estas capacidades con programas
          impartidos por profesores presenciales o virtuales.
        </Temporary>
      </div>
    </TecPage>
  );
}

export function TecCapacidadesHumanasPage() {
  const { resumeQuery } = useTecProfile();
  const objective = clean(resumeQuery.data?.professionalObjective);
  const isFde = /forward deployed engineer/i.test(objective);
  const ids = [
    "human-os-executive-communication",
    "executive-ai-communication",
    "client-discovery-technical-professionals",
    "technical-storytelling",
    "technology-stakeholder-management",
    "technology-leadership-decisions",
  ];
  const programs = ids
    .map((id) => demoTecPrograms.find((program) => program.id === id))
    .filter((program): program is DemoTecProgram => Boolean(program));
  const byId = (id: string) => programs.find((program) => program.id === id);
  const [selectedProgram, setSelectedProgram] = useState<DemoTecProgram | null>(
    null,
  );
  const [addedIds, setAddedIds] = useState<Set<string>>(() => new Set());
  const humanOs = byId("human-os-executive-communication");
  const professorPrograms = [
    "executive-ai-communication",
    "client-discovery-technical-professionals",
    "technical-storytelling",
    "technology-stakeholder-management",
    "technology-leadership-decisions",
  ]
    .map(byId)
    .filter((program): program is DemoTecProgram => Boolean(program));
  const dimensions = [
    [
      "Comprender",
      "Comprende el problema y el contexto antes de recomendar una solución.",
      [
        "Descubrimiento con clientes",
        "Escucha activa",
        "Formulación de preguntas",
        "Identificación de necesidades",
      ],
    ],
    [
      "Comunicar",
      "Traduce decisiones técnicas en mensajes claros, relevantes y orientados a una acción.",
      [
        "Comunicación ejecutiva",
        "Storytelling técnico",
        "Presentaciones de alto impacto",
        "Comunicación de riesgos",
      ],
    ],
    [
      "Colaborar",
      "Conecta perspectivas, responsabilidades y prioridades diferentes.",
      [
        "Gestión de stakeholders",
        "Colaboración interdisciplinaria",
        "Facilitación de decisiones",
        "Construcción de acuerdos",
      ],
    ],
    [
      "Liderar",
      "Moviliza conversaciones y decisiones aunque no tengas autoridad formal.",
      [
        "Liderazgo tecnológico",
        "Manejo de objeciones",
        "Toma de decisiones",
        "Orientación a resultados",
      ],
    ],
  ] as const;
  const engrams = [
    "Comprende a tu audiencia",
    "Estructura el mensaje",
    "Traduce tecnología en valor",
    "Comunica decisiones y alternativas",
    "Responde preguntas y objeciones",
    "Presenta una solución técnica",
  ];
  const capabilityMap: Record<string, string[]> = {
    "Comunicación ejecutiva": [
      "human-os-executive-communication",
      "executive-ai-communication",
    ],
    "Storytelling técnico": [
      "human-os-executive-communication",
      "technical-storytelling",
    ],
    "Descubrimiento con clientes": ["client-discovery-technical-professionals"],
    "Escucha activa": [
      "client-discovery-technical-professionals",
      "human-os-executive-communication",
    ],
    "Gestión de stakeholders": ["technology-stakeholder-management"],
    "Liderazgo tecnológico": ["technology-leadership-decisions"],
    "Manejo de objeciones": [
      "executive-ai-communication",
      "human-os-executive-communication",
    ],
    "Presentaciones de alto impacto": [
      "technical-storytelling",
      "executive-ai-communication",
    ],
    "Colaboración interdisciplinaria": [
      "technology-stakeholder-management",
      "technology-leadership-decisions",
    ],
  };
  const addToPath = (id: string) =>
    setAddedIds((current) => new Set(current).add(id));
  const open = (id: string) => {
    const program = byId(id);
    if (program) setSelectedProgram(program);
  };
  const card = (program: DemoTecProgram, compact = false) => (
    <Card
      key={program.id}
      className={compact ? "p-4" : "border-t-4 border-teal-500 p-5"}
    >
      <p className="text-xs font-semibold uppercase tracking-wide text-teal-700">
        {program.category}
      </p>
      <h3 className="mt-2 font-semibold text-slate-950 dark:text-white">
        {program.title.es}
      </h3>
      <p className="mt-1 text-xs text-slate-500">
        {program.modality === "Human OS course"
          ? "Curso Human OS"
          : program.modality}{" "}
        · {program.duration}
      </p>
      {program.modality !== "Human OS course" && (
        <p className="mt-1 text-xs text-slate-500">
          {program.modality === "Profesor presencial"
            ? "Campus Monterrey"
            : "Experiencia virtual en vivo"}
        </p>
      )}
      <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
        {program.shortDescription.es}
      </p>
      {!compact && (
        <>
          <p className="mt-3 text-xs font-semibold text-slate-500">
            Por qué se recomienda
          </p>
          <p className="mt-1 text-sm text-slate-600 dark:text-white/70">
            {program.recommendationReason?.es}
          </p>
          <p className="mt-3 text-xs text-slate-500">
            Resultado esperado: {program.evidenceOutcomes.es[0]}
          </p>
        </>
      )}
      <button
        type="button"
        onClick={() => setSelectedProgram(program)}
        className="mt-4 text-sm font-semibold text-blue-600"
      >
        {compact ? "Ver" : "Ver recomendación"}
      </button>
    </Card>
  );
  return (
    <TecPage
      title="El Tec está contigo para desarrollar capacidades humanas indispensables"
      description="El conocimiento técnico genera mayor impacto cuando puedes comprender necesidades, comunicar decisiones, colaborar con otros y conectar una solución con el valor que necesita una organización."
    >
      <div className="space-y-8">
        <p className="text-xs text-slate-500">
          Programas conceptuales para esta experiencia.
        </p>
        <Card className="p-6">
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Capacidades humanas para tu objetivo
          </h2>
          <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
            {isFde
              ? "Tu objetivo profesional requiere combinar profundidad técnica con la capacidad de trabajar directamente con clientes, comprender necesidades ambiguas, comunicar decisiones y construir acuerdos."
              : "Estas capacidades pueden ayudarte a convertir conocimiento técnico en colaboración, decisiones e impacto profesional."}
          </p>
          {isFde && (
            <p className="mt-3 text-sm leading-6 text-slate-600 dark:text-white/70">
              Un Forward Deployed Engineer no solamente construye tecnología.
              También descubre problemas, traduce necesidades, facilita
              decisiones y explica el valor de una solución.
            </p>
          )}
        </Card>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Capacidades humanas prioritarias
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Organizamos las capacidades relevantes para tu objetivo en cuatro
            dimensiones.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            {dimensions.map(([title, description, capabilities]) => (
              <Card key={title} className="p-5">
                <h3 className="font-semibold text-slate-950 dark:text-white">
                  {title}
                </h3>
                <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
                  {description}
                </p>
                <div className="mt-3 flex flex-wrap gap-2">
                  {capabilities.map((capability) => (
                    <span
                      key={capability}
                      className="rounded-full bg-orange-50 px-3 py-1 text-xs text-orange-800 dark:bg-orange-400/10 dark:text-orange-100"
                    >
                      {capability}
                    </span>
                  ))}
                </div>
              </Card>
            ))}
          </div>
        </section>
        <section id="human-os-engrams">
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Programa principal para fortalecer tu comunicación
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Una experiencia autónoma para convertir información técnica compleja
            en mensajes ejecutivos claros, relevantes y orientados a decisiones.
          </p>
          {humanOs && (
            <Card className="mt-4 border-2 border-teal-400 bg-teal-50/50 p-6 dark:bg-teal-400/[0.08]">
              <p className="text-xs font-semibold uppercase tracking-wide text-teal-700">
                Programa principal · Curso Human OS
              </p>
              <h3 className="mt-2 text-xl font-semibold">{humanOs.title.es}</h3>
              <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
                {humanOs.shortDescription.es}
              </p>
              <p className="mt-4 text-sm font-semibold text-teal-700">
                Memory Paradox · Graph Learning · Engrams
              </p>
              <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
                Cada Engram conecta un reto de comunicación con conceptos,
                práctica, recuperación, aplicación y evidencia de dominio.
              </p>
              <div className="mt-4 grid gap-2 sm:grid-cols-2">
                {engrams.map((engram, index) => (
                  <div
                    key={engram}
                    className="rounded-lg border border-teal-200 bg-white/70 p-3 text-sm dark:border-teal-400/20 dark:bg-white/[0.05]"
                  >
                    <span className="text-xs font-bold text-teal-700">
                      Engram {index + 1}
                    </span>
                    <p className="mt-1 font-semibold">{engram}</p>
                    <p className="mt-1 text-xs text-slate-500">
                      Assessment, recall, práctica y evidencia propios.
                    </p>
                  </div>
                ))}
              </div>
              <button
                type="button"
                onClick={() => setSelectedProgram(humanOs)}
                className="mt-5 rounded-lg bg-teal-700 px-4 py-2 text-sm font-semibold text-white"
              >
                Ver estructura del curso
              </button>
              <Link
                to="/structured-learning/c480e9ab-0e04-4039-bd09-70e5905b3871"
                className="ml-3 inline-flex items-center rounded-lg border border-teal-700 px-4 py-2 text-sm font-semibold text-teal-800 hover:bg-teal-100 dark:border-teal-300 dark:text-teal-200 dark:hover:bg-teal-400/10"
              >
                Comunicación Ejecutiva para profesionales técnicos
              </Link>
            </Card>
          )}
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Programas con acompañamiento de profesores
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Practica conversaciones, presentaciones y decisiones con
            retroalimentación humana.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-3">
            {professorPrograms.slice(0, 3).map((program) => card(program))}
          </div>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            {professorPrograms.slice(3).map((program) => card(program, true))}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Encuentra el programa adecuado para cada capacidad
          </h2>
          <div className="mt-4 grid gap-3 md:grid-cols-2">
            {Object.entries(capabilityMap).map(([capability, programIds]) => (
              <Card key={capability} className="p-4">
                <h3 className="font-semibold">{capability}</h3>
                <div className="mt-2 flex flex-wrap gap-2">
                  {programIds.map((id) => (
                    <button
                      key={id}
                      type="button"
                      onClick={() => open(id)}
                      className="text-left text-xs font-semibold text-blue-600"
                    >
                      {byId(id)?.title.es}
                    </button>
                  ))}
                </div>
              </Card>
            ))}
          </div>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Tu ruta para convertir conocimiento técnico en impacto
          </h2>
          <p className="mt-1 text-sm text-slate-500">
            Una secuencia para comprender, comunicar, conectar y liderar.
          </p>
          <div className="mt-4 grid gap-4 md:grid-cols-4">
            {[
              [
                "Comprende",
                "Descubre necesidades, escucha perspectivas y define el problema antes de proponer una solución.",
                "client-discovery-technical-professionals",
              ],
              [
                "Comunica",
                "Transforma decisiones técnicas en mensajes claros, relevantes y orientados a una acción.",
                "human-os-executive-communication",
              ],
              [
                "Conecta",
                "Construye narrativas, gestiona stakeholders y facilita acuerdos entre participantes.",
                "technology-stakeholder-management",
              ],
              [
                "Lidera",
                "Facilita decisiones, comunica prioridades y moviliza al equipo hacia resultados compartidos.",
                "technology-leadership-decisions",
              ],
            ].map(([title, text, id]) => (
              <Card key={title} className="p-4">
                <p className="text-xs font-bold text-blue-600">{title}</p>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
                  {text}
                </p>
                <button
                  type="button"
                  onClick={() => open(id)}
                  className="mt-3 text-left text-xs font-semibold text-blue-600"
                >
                  {byId(id)?.title.es}
                </button>
              </Card>
            ))}
          </div>
          <p className="mt-3 text-xs text-slate-500">
            Ruta conceptual para esta experiencia.
          </p>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Practica antes de enfrentar una situación real
          </h2>
          <div className="mt-4 grid gap-3 md:grid-cols-2">
            {[
              [
                "Conversación de descubrimiento con un cliente",
                "Descubrimiento y escucha activa",
                "client-discovery-technical-professionals",
                "Guía de preguntas y definición del problema.",
              ],
              [
                "Presentación de una solución de IA a ejecutivos",
                "Comunicación ejecutiva",
                "executive-ai-communication",
                "Presentación ejecutiva y recomendación técnica.",
              ],
              [
                "Manejo de una objeción sobre costo, riesgo o viabilidad",
                "Manejo de objeciones",
                "human-os-executive-communication",
                "Registro de preguntas, respuestas y alternativas.",
              ],
              [
                "Conversación de decisión con stakeholders",
                "Facilitación y construcción de acuerdos",
                "technology-stakeholder-management",
                "Mapa de stakeholders y registro de acuerdos.",
              ],
              [
                "Presentación de avances y riesgos a un equipo directivo",
                "Liderazgo y comunicación",
                "technology-leadership-decisions",
                "Agenda de decisión, riesgos y siguientes acciones.",
              ],
            ].map(([situation, capability, id, evidence]) => (
              <Card key={situation} className="p-4">
                <h3 className="font-semibold">{situation}</h3>
                <p className="mt-2 text-xs text-orange-700">{capability}</p>
                <p className="mt-2 text-sm text-slate-600 dark:text-white/70">
                  Evidencia: {evidence}
                </p>
                <button
                  type="button"
                  onClick={() => open(id)}
                  className="mt-3 text-xs font-semibold text-blue-600"
                >
                  Ver programa relacionado
                </button>
              </Card>
            ))}
          </div>
          <p className="mt-3 text-xs text-slate-500">
            Estas prácticas forman parte de los programas relacionados.
          </p>
        </section>
        <section>
          <h2 className="text-xl font-semibold text-slate-950 dark:text-white">
            Haz visible tu capacidad para trabajar con otros
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">
            El objetivo no es solamente conocer técnicas de comunicación. Es
            demostrar que puedes comprender necesidades, presentar decisiones,
            responder preguntas y construir acuerdos.
          </p>
          <div className="mt-4 grid gap-3 sm:grid-cols-2">
            {[
              "Guía de descubrimiento",
              "Mapa de stakeholders",
              "Resumen ejecutivo",
              "Narrativa de valor",
              "Presentación técnica",
              "Recomendación con alternativas",
              "Registro de preguntas y respuestas",
              "Plan de comunicación",
              "Registro de acuerdos y responsables",
              "Reflexión posterior a una conversación",
            ].map((item) => (
              <Card key={item} className="p-4 text-sm">
                {item}
              </Card>
            ))}
          </div>
        </section>
        <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6 text-white shadow-lg dark:border-white/10 dark:bg-slate-900/90">
          <h2 className="text-xl font-semibold">
            El Tec continúa contigo para convertir conocimiento en impacto
          </h2>
          <p className="mt-2 text-sm leading-6 text-slate-300">
            Tu preparación profesional no depende solamente de lo que sabes
            construir. También depende de tu capacidad para comprender a las
            personas, comunicar decisiones, colaborar y generar confianza.
          </p>
          <Link
            to="/available-courses"
            className="mt-4 inline-block text-sm font-semibold text-blue-300"
          >
            Explorar todos los programas
          </Link>
        </div>
      </div>
      {selectedProgram && (
        <SemesterProgramModal
          program={selectedProgram}
          added={addedIds.has(selectedProgram.id)}
          onAdd={() => addToPath(selectedProgram.id)}
          onClose={() => setSelectedProgram(null)}
        />
      )}
    </TecPage>
  );
}
