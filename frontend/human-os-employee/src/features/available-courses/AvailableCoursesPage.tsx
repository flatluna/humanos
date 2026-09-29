import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import {
  BookCheck,
  BookOpen,
  CheckCircle2,
  Layers,
  PlayCircle,
  Search,
  Sparkles,
  ExternalLink,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getMyCareerPlan, type CareerPlanRoadmapStepDto } from '@/features/career-plan/api/careerPlanApi';
import { getSkills } from '@/features/capabilities/api/skillsApi';
import {
  getCapabilities,
} from '@/features/capabilities/api/capabilitiesApi';
import {
  getEnrolledStructuredLearningDocuments,
} from '@/features/capabilities/api/enrolledStructuredLearningApi';
import {
  getStructuredLearningDocuments,
} from '@/features/capabilities/api/structuredLearningRuntimeApi';
import {
  getEnrolledCapabilities,
} from '@/features/capabilities/api/enrolledCapabilitiesApi';
import {
  getCourseProgramMembershipMap,
} from '@/features/programs/api/programsApi';
import { GraduationCap } from 'lucide-react';

export interface AvailableCourseCardItem {
  id: string;
  title: string;
  skillName?: string;
  description: string;
  category: 'job-skills' | 'soft-skills' | 'department-processes' | 'hr-processes' | 'general';
  categoryLabel: string;
  courseType: 'structured-learning' | 'graph' | 'program';
  targetUrl: string;
  isExternalProgram?: boolean;
  coverImageUrl?: string | null;
  coverIllustrationId?: string | null;
  status: 'Available' | 'InProgress' | 'Completed' | 'Pending';
  sectionCount?: number;
  nodeCount?: number;
  /** Distinct sections/engrams already passed — only known for enrolled
   * Structured Learning documents (see EnrolledStructuredLearningDocument). */
  completedSectionCount?: number;
  /** True when the student is actually enrolled in this course — Structured
   * Learning enrollment never updates `status` (it stays 'Available'
   * forever), so this is the real signal a "started only" filter must use
   * instead of relying on status alone. */
  isEnrolled?: boolean;
  gradient: string;
  programMembership?: { programId: string; programName: string } | null;
  /** "es"/"en" when known (Capabilities only, see Capability.ContentLanguageCode).
   *  Undefined for course types that don't track a content language yet
   *  (structured-learning docs, programs) — those always match every
   *  language filter instead of being hidden. */
  contentLanguageCode?: string;
}

const CATEGORY_STYLES: Record<
  string,
  { labelEs: string; labelEn: string; badgeClass: string; gradient: string }
> = {
  'job-skills': {
    labelEs: 'Mi Puesto',
    labelEn: 'My Role',
    badgeClass: 'bg-sky-50 text-sky-700 border-sky-200 dark:bg-sky-500/15 dark:border-sky-500/30 dark:text-sky-300',
    gradient: 'from-sky-500 via-blue-600 to-indigo-600',
  },
  'soft-skills': {
    labelEs: 'Soft Skills',
    labelEn: 'Soft Skills',
    badgeClass: 'bg-violet-50 text-violet-700 border-violet-200 dark:bg-violet-500/15 dark:border-violet-500/30 dark:text-violet-300',
    gradient: 'from-violet-500 via-purple-600 to-indigo-700',
  },
  'department-processes': {
    labelEs: 'Procesos de Depto.',
    labelEn: 'Department Processes',
    badgeClass: 'bg-teal-50 text-teal-700 border-teal-200 dark:bg-teal-500/15 dark:border-teal-500/30 dark:text-teal-300',
    gradient: 'from-emerald-500 via-teal-600 to-cyan-600',
  },
  'hr-processes': {
    labelEs: 'Procesos de RRHH',
    labelEn: 'HR Processes',
    badgeClass: 'bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-500/15 dark:border-rose-500/30 dark:text-rose-300',
    gradient: 'from-rose-500 via-pink-600 to-purple-600',
  },
  general: {
    labelEs: 'Engramas del Catálogo',
    labelEn: 'Catalog Engrams',
    badgeClass: 'bg-indigo-50 text-indigo-700 border-indigo-200 dark:bg-indigo-500/15 dark:border-indigo-500/30 dark:text-indigo-300',
    gradient: 'from-indigo-500 via-indigo-600 to-violet-600',
  },
};

const STATUS_BADGES: Record<string, { labelEs: string; labelEn: string; className: string }> = {
  Completed: {
    labelEs: 'Completado',
    labelEn: 'Completed',
    className: 'bg-emerald-100 text-emerald-800 border-emerald-200 dark:bg-emerald-500/20 dark:border-emerald-500/30 dark:text-emerald-300',
  },
  InProgress: {
    labelEs: 'En Progreso',
    labelEn: 'In Progress',
    className: 'bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-500/20 dark:border-amber-500/30 dark:text-amber-300',
  },
  Available: {
    labelEs: 'Listo para Iniciar',
    labelEn: 'Ready to Start',
    className: 'bg-sky-100 text-sky-800 border-sky-200 dark:bg-sky-500/20 dark:border-sky-500/30 dark:text-sky-300',
  },
  Pending: {
    labelEs: 'Disponible',
    labelEn: 'Available',
    className: 'bg-indigo-100 text-indigo-800 border-indigo-200 dark:bg-indigo-500/20 dark:border-indigo-500/30 dark:text-indigo-300',
  },
};

/** Last-resort language guess for courses with no real language data anywhere
 * (e.g. career-plan/skill-matched courses that are neither published nor
 * enrolled, so no backend query ever returns their ConfigSnapshotJson) — same
 * word-frequency heuristic as the backend's StructuredLearningLanguageHeuristic.
 * Without this, such courses had contentLanguageCode=undefined and silently
 * matched EVERY language filter instead of being classified for real. */
function detectLanguageHeuristic(sampleText: string): 'es' | 'en' {
  const padded = ` ${sampleText.toLowerCase()} `;
  const englishWords = [' the ', ' and ', ' you ', ' is ', ' to ', ' your ', ' with ', ' that ', ' this '];
  const spanishWords = [' el ', ' la ', ' de ', ' que ', ' los ', ' las ', ' para ', ' con ', ' es ', ' tu '];
  const englishHits = englishWords.filter((w) => padded.includes(w)).length;
  const spanishHits = spanishWords.filter((w) => padded.includes(w)).length;
  return englishHits > spanishHits ? 'en' : 'es';
}

export function AvailableCoursesPage() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const tenantId = user?.tenantId ?? '';

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedLanguage, setSelectedLanguage] = useState<'all' | 'es' | 'en'>('all');
  const [startedOnly, setStartedOnly] = useState(false);

  // Queries
  const planQuery = useQuery({
    queryKey: ['my-career-plan-available', personId],
    queryFn: () => getMyCareerPlan(personId),
    enabled: Boolean(personId),
  });

  const skillsQuery = useQuery({
    queryKey: ['catalog-skills-available', tenantId],
    queryFn: () => getSkills(tenantId),
    enabled: Boolean(tenantId),
  });

  const enrolledSlQuery = useQuery({
    queryKey: ['my-enrolled-sl-available', personId],
    queryFn: () => getEnrolledStructuredLearningDocuments(personId),
    enabled: Boolean(personId),
  });

  const publishedSlQuery = useQuery({
    queryKey: ['published-structured-learning-available'],
    queryFn: getStructuredLearningDocuments,
  });

  const enrolledCapsQuery = useQuery({
    queryKey: ['my-enrolled-caps-available', personId],
    queryFn: () => getEnrolledCapabilities(personId),
    enabled: Boolean(personId),
  });

  const publishedCapsQuery = useQuery({
    queryKey: ['published-caps-available'],
    queryFn: () => getCapabilities(),
  });

  const programMembershipQuery = useQuery({
    queryKey: ['course-program-memberships'],
    queryFn: () => getCourseProgramMembershipMap(),
  });

  const isLoading =
    planQuery.isLoading ||
    skillsQuery.isLoading ||
    enrolledSlQuery.isLoading ||
    publishedSlQuery.isLoading ||
    enrolledCapsQuery.isLoading ||
    publishedCapsQuery.isLoading ||
    programMembershipQuery.isLoading;

  // Build the list of ONLY READY / AVAILABLE courses (excluding ones "In Development" / with no course)
  const availableCourses = useMemo<AvailableCourseCardItem[]>(() => {
    const itemsMap = new Map<string, AvailableCourseCardItem>();

    // 1. Career Plan Roadmaps (only steps that have a matched course in catalog)
    if (planQuery.data?.hasPlan) {
      const plan = planQuery.data;
      const allRoadmapSteps: { step: CareerPlanRoadmapStepDto; category: AvailableCourseCardItem['category'] }[] = [
        ...plan.technicalRoadmap.map((s) => ({ step: s, category: 'job-skills' as const })),
        ...plan.softSkillsRoadmap.map((s) => ({ step: s, category: 'soft-skills' as const })),
        ...plan.departmentProcessesRoadmap.map((s) => ({ step: s, category: 'department-processes' as const })),
        ...plan.hrProcessesRoadmap.map((s) => ({ step: s, category: 'hr-processes' as const })),
      ];

      for (const { step, category } of allRoadmapSteps) {
        // Only include if it actually has a real matched course
        if (step.matchedCapabilityId) {
          const key = `cap-${step.matchedCapabilityId}`;
          const catStyle = CATEGORY_STYLES[category] || CATEGORY_STYLES.general;
          itemsMap.set(key, {
            id: step.matchedCapabilityId,
            title: step.matchedCapabilityName || step.title,
            skillName: step.skill,
            description: step.description,
            category,
            categoryLabel: isEn ? catStyle.labelEn : catStyle.labelEs,
            courseType: 'structured-learning',
            targetUrl: `/structured-learning/${step.matchedCapabilityId}`,
            status: (step.trainingStatus as any) || 'Available',
            gradient: catStyle.gradient,
            contentLanguageCode: step.language ?? undefined,
          });
        } else if (step.matchedProgramId) {
          const key = `prog-${step.matchedProgramId}`;
          const catStyle = CATEGORY_STYLES[category] || CATEGORY_STYLES.general;
          itemsMap.set(key, {
            id: step.matchedProgramId,
            title: step.matchedProgramName || step.title,
            skillName: step.skill,
            description: step.description,
            category,
            categoryLabel: isEn ? catStyle.labelEn : catStyle.labelEs,
            courseType: 'program',
            targetUrl: `/programs/${step.matchedProgramId}`,
            isExternalProgram: false,
            status: (step.trainingStatus as any) || 'Available',
            gradient: catStyle.gradient,
          });
        }
      }
    }

    // 2. Catalog Skills that have a matched Capability / Course
    if (skillsQuery.data) {
      for (const skill of skillsQuery.data) {
        if (skill.matchedCapabilityId) {
          const key = `cap-${skill.matchedCapabilityId}`;
          if (!itemsMap.has(key)) {
            const rawCat = skill.category || 'job-skills';
            const cat =
              rawCat === 'soft-skills' || rawCat === 'department-processes' || rawCat === 'hr-processes'
                ? rawCat
                : 'job-skills';
            const catStyle = CATEGORY_STYLES[cat] || CATEGORY_STYLES.general;

            itemsMap.set(key, {
              id: skill.matchedCapabilityId,
              title: skill.matchedCapabilityName || skill.name,
              skillName: skill.name,
              description: skill.description || '',
              category: cat,
              categoryLabel: isEn ? catStyle.labelEn : catStyle.labelEs,
              courseType: skill.matchedCourseType === 'graph' ? 'graph' : 'structured-learning',
              targetUrl:
                skill.matchedCourseType === 'graph'
                  ? `/capabilities/${skill.matchedCapabilityId}`
                  : `/structured-learning/${skill.matchedCapabilityId}`,
              status: 'Available',
              gradient: catStyle.gradient,
            });
          }
        }
      }
    }

    // 3. Published Capabilities across the Catalog
    if (publishedCapsQuery.data) {
      for (const cap of publishedCapsQuery.data) {
        const key = `cap-${cap.capabilityId}`;
        const existingCap = itemsMap.get(key);
        if (existingCap) {
          // Career-plan/skill-matched items (steps 1-2) never carry a language —
          // always backfill it here so the ES/EN filter isn't bypassed for them.
          existingCap.contentLanguageCode = existingCap.contentLanguageCode ?? cap.contentLanguageCode;
        } else {
          const rawCat = cap.subjectCode || 'general';
          const cat =
            rawCat === 'soft-skills' || rawCat === 'department-processes' || rawCat === 'hr-processes' || rawCat === 'job-skills'
              ? rawCat
              : 'general';
          const catStyle = CATEGORY_STYLES[cat] || CATEGORY_STYLES.general;

          itemsMap.set(key, {
            id: cap.capabilityId,
            title: cap.name,
            description: cap.learningSummary || cap.description || (isEn ? 'Interactive capability training.' : 'Capacitación interactiva en plataforma.'),
            category: cat,
            categoryLabel: isEn ? catStyle.labelEn : catStyle.labelEs,
            courseType: 'graph',
            targetUrl: `/capabilities/${cap.capabilityId}`,
            nodeCount: cap.nodeCount,
            status: 'Available',
            gradient: catStyle.gradient,
            contentLanguageCode: cap.contentLanguageCode,
          });
        }
      }
    }

    // 4. Enrolled Structured Learning Documents
    if (enrolledSlQuery.data) {
      for (const doc of enrolledSlQuery.data) {
        const key = `cap-${doc.structuredLearningDocumentId}`;
        const existing = itemsMap.get(key);
        if (existing) {
          existing.sectionCount = doc.sectionCount;
          existing.completedSectionCount = doc.completedSectionCount;
          existing.coverIllustrationId = doc.coverIllustrationId;
          existing.isEnrolled = true;
          existing.contentLanguageCode = existing.contentLanguageCode ?? doc.language;
        } else {
          itemsMap.set(key, {
            id: doc.structuredLearningDocumentId,
            title: doc.documentName,
            description: isEn ? 'Interactive structured learning engram.' : 'Engrama interactivo de aprendizaje estructurado.',
            category: 'general',
            categoryLabel: isEn ? CATEGORY_STYLES.general.labelEn : CATEGORY_STYLES.general.labelEs,
            courseType: 'structured-learning',
            targetUrl: `/structured-learning/${doc.structuredLearningDocumentId}`,
            coverIllustrationId: doc.coverIllustrationId,
            sectionCount: doc.sectionCount,
            completedSectionCount: doc.completedSectionCount,
            status: 'Available',
            isEnrolled: true,
            gradient: CATEGORY_STYLES.general.gradient,
            contentLanguageCode: doc.language,
          });
        }
      }
    }

    // 5. Published Structured Learning Documents, including courses created
    // in Human OS Studio that are not enrolled by the current person yet.
    if (publishedSlQuery.data) {
      for (const doc of publishedSlQuery.data) {
        const key = `cap-${doc.structuredLearningDocumentId}`;
        const existingSl = itemsMap.get(key);
        if (existingSl) {
          existingSl.contentLanguageCode = existingSl.contentLanguageCode ?? doc.language;
        } else {
          itemsMap.set(key, {
            id: doc.structuredLearningDocumentId,
            title: doc.documentName,
            description: doc.executiveSummary || (isEn ? 'Interactive structured learning course.' : 'Curso interactivo de aprendizaje estructurado.'),
            category: 'general',
            categoryLabel: isEn ? CATEGORY_STYLES.general.labelEn : CATEGORY_STYLES.general.labelEs,
            courseType: 'structured-learning',
            targetUrl: `/structured-learning/${doc.structuredLearningDocumentId}`,
            coverIllustrationId: doc.coverIllustrationId,
            sectionCount: doc.sectionCount,
            status: 'Available',
            gradient: CATEGORY_STYLES.general.gradient,
            contentLanguageCode: doc.language,
          });
        }
      }
    }

    // 6. Enrolled Capabilities
    if (enrolledCapsQuery.data) {
      for (const cap of enrolledCapsQuery.data) {
        const key = `cap-${cap.capabilityId}`;
        const existing = itemsMap.get(key);
        if (existing) {
          existing.status = (cap.status as any) || existing.status;
          existing.nodeCount = cap.totalNodes;
          existing.isEnrolled = true;
        }
      }
    }

    // 7. Enrich with Program Membership (if course is part of a program)
    const membershipMap = programMembershipQuery.data;
    if (membershipMap) {
      for (const item of itemsMap.values()) {
        const membership = membershipMap.get(item.id);
        if (membership) {
          item.programMembership = membership;
        }
      }
    }

    return Array.from(itemsMap.values());
  }, [
    planQuery.data,
    skillsQuery.data,
    publishedCapsQuery.data,
    enrolledSlQuery.data,
    publishedSlQuery.data,
    enrolledCapsQuery.data,
    programMembershipQuery.data,
    isEn,
  ]);

  // Filtered by search, language & "started" state
  const filteredCourses = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return availableCourses.filter((course) => {
      const matchStarted = !startedOnly || course.isEnrolled || course.status === 'InProgress' || course.status === 'Completed';
      const effectiveLanguage = course.contentLanguageCode?.toLowerCase() || detectLanguageHeuristic(`${course.title} ${course.description}`);
      const matchLang = selectedLanguage === 'all' || effectiveLanguage.startsWith(selectedLanguage);
      const matchQ =
        !q ||
        course.title.toLowerCase().includes(q) ||
        (course.skillName && course.skillName.toLowerCase().includes(q)) ||
        course.description.toLowerCase().includes(q);
      return matchStarted && matchLang && matchQ;
    });
  }, [availableCourses, startedOnly, selectedLanguage, searchQuery]);

  const totalEngramsCompleted = useMemo(
    () => availableCourses
      .reduce((sum, c) => {
        if (typeof c.completedSectionCount === 'number') return sum + c.completedSectionCount;
        return c.status === 'Completed' ? sum + (c.sectionCount ?? c.nodeCount ?? 0) : sum;
      }, 0),
    [availableCourses],
  );
  const completedCount = availableCourses.filter((c) => c.status === 'Completed').length;
  const inProgressCount = availableCourses.filter(
    (c) => c.status !== 'Completed' && (c.status === 'InProgress' || c.isEnrolled),
  ).length;

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-4 pb-16 sm:p-8">
      {/* Header Banner */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-emerald-600 via-teal-600 to-indigo-700 p-6 text-white shadow-lg shadow-emerald-600/20 dark:border-white/10 sm:p-8">
        <div className="flex flex-col gap-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-4">
            <span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/15 text-2xl font-bold ring-1 ring-white/25 backdrop-blur">
              <BookCheck className="h-8 w-8 text-white" />
            </span>
            <div>
              <p className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wide text-white/80">
                <Sparkles className="h-3.5 w-3.5 text-emerald-200" />
                {isEn ? 'Catalog & Learning Roadmaps' : 'Catálogo y Rutas de Aprendizaje'}
              </p>
              <h1 className="mt-1 text-2xl font-bold text-white">
                {isEn ? 'Available Courses' : 'Cursos Disponibles'}
              </h1>
              <p className="mt-1 text-sm text-white/80 max-w-xl">
                {isEn
                  ? 'Explore all courses, engrams, and training modules that are fully developed and ready to start.'
                  : 'Explora todos los cursos, engramas y módulos de capacitación que ya están listos y disponibles en el catálogo.'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-4 rounded-2xl bg-white/10 px-5 py-4 ring-1 ring-white/20 backdrop-blur">
            <div className="text-right">
              <p className="text-2xl font-bold text-white">{totalEngramsCompleted}</p>
              <p className="text-xs text-white/70">
                {isEn ? 'Engrams completed' : 'Engramas completados'}
              </p>
            </div>
            <div className="h-9 w-px bg-white/20" />
            <div>
              <p className="text-xs font-medium text-emerald-200">
                ✓ {completedCount} {isEn ? 'Completed' : 'Completados'}
              </p>
              <p className="text-xs font-medium text-amber-200">
                ⏳ {inProgressCount} {isEn ? 'In Progress' : 'En Progreso'}
              </p>
            </div>
          </div>
        </div>
      </div>

      {isLoading && (
        <LoadingBackdrop
          title={isEn ? 'Loading Available Courses...' : 'Cargando cursos disponibles...'}
          description={isEn ? 'Retrieving ready training modules from the catalog.' : 'Obteniendo cursos y engramas listos del catálogo.'}
        />
      )}

      {/* Filters and Search */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        {/* Search */}
        <div className="relative flex-1 max-w-md">
          <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="search"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={isEn ? 'Search by course or skill name...' : 'Buscar por nombre de curso o skill...'}
            className="w-full rounded-2xl border border-slate-200 bg-white py-2.5 pl-10 pr-4 text-xs font-medium text-slate-900 outline-none transition focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
          />
        </div>

        {/* Language Pills */}
        <div className="flex flex-wrap items-center gap-1.5">
          {(['all', 'es', 'en'] as const).map((lang) => (
            <button
              key={lang}
              type="button"
              onClick={() => setSelectedLanguage(lang)}
              className={`rounded-full px-3 py-1.5 text-xs font-semibold transition ${
                selectedLanguage === lang
                  ? 'bg-emerald-600 text-white shadow-sm'
                  : 'border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70'
              }`}
            >
              {lang === 'all' ? (isEn ? 'All Languages' : 'Todos los Idiomas') : lang === 'es' ? 'Español' : 'English'}
            </button>
          ))}
        </div>

        {/* Started-only toggle */}
        <div className="flex flex-wrap items-center gap-1.5">
          <button
            type="button"
            onClick={() => setStartedOnly(false)}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold transition ${
              !startedOnly
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70'
            }`}
          >
            {isEn ? 'All courses' : 'Todos los cursos'}
          </button>
          <button
            type="button"
            onClick={() => setStartedOnly(true)}
            className={`rounded-full px-3 py-1.5 text-xs font-semibold transition ${
              startedOnly
                ? 'bg-indigo-600 text-white shadow-sm'
                : 'border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white/70'
            }`}
          >
            {isEn ? 'Started only' : 'Solo iniciados'}
          </button>
        </div>
      </div>

      {/* Course Cards Grid */}
      {filteredCourses.length === 0 ? (
        <Card className="flex flex-col items-center justify-center p-12 text-center border-dashed">
          <BookOpen className="h-10 w-10 text-slate-300 dark:text-white/20" />
          <h3 className="mt-3 text-base font-semibold text-slate-900 dark:text-white">
            {isEn ? 'No available courses found' : 'No se encontraron cursos disponibles'}
          </h3>
          <p className="mt-1 text-xs text-slate-500 dark:text-white/50 max-w-sm">
            {isEn
              ? 'Try modifying your search or category filter.'
              : 'Prueba ajustando el término de búsqueda o el filtro de categoría.'}
          </p>
        </Card>
      ) : (
        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {filteredCourses.map((course) => {
            const effectiveStatus = course.status === 'Available' && course.isEnrolled ? 'InProgress' : course.status;
            const statusMeta = STATUS_BADGES[effectiveStatus] || STATUS_BADGES.Available;

            return (
              <div
                key={course.id + '-' + course.category}
                className="group flex flex-col justify-between overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-xs transition-all hover:-translate-y-1 hover:border-slate-300 hover:shadow-xl dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-white/20"
              >
                <div>
                  {/* Card Header with Gradient */}
                  <div className={`relative h-32 overflow-hidden bg-gradient-to-br ${course.gradient} p-4`}>
                    {course.coverIllustrationId && (
                      <img
                        src={`/api/illustrations/structured-learning/${course.coverIllustrationId}/image`}
                        alt=""
                        className="absolute inset-0 h-full w-full object-cover"
                        onError={(e) => {
                          e.currentTarget.style.display = 'none';
                        }}
                      />
                    )}
                    <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.25),transparent_60%)]" />
                    <div className="relative flex items-center justify-between gap-2">
                      <span className="rounded-full bg-black/40 px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white backdrop-blur-sm">
                        {course.categoryLabel}
                      </span>
                      {course.programMembership ? (
                        <span className="flex items-center gap-1 rounded-full border border-purple-400/40 bg-purple-900/80 px-2 py-0.5 text-[10px] font-bold text-purple-200 backdrop-blur-sm shadow-xs">
                          <GraduationCap className="h-3 w-3" />
                          {isEn ? 'Program Course' : 'Parte de un Programa'}
                        </span>
                      ) : (
                        <span className={`rounded-full border px-2 py-0.5 text-[10px] font-bold ${statusMeta.className}`}>
                          {isEn ? statusMeta.labelEn : statusMeta.labelEs}
                        </span>
                      )}
                    </div>
                  </div>

                  {/* Card Content */}
                  <div className="p-5 space-y-2.5">
                    <h3 className="text-base font-bold text-slate-900 dark:text-white line-clamp-2 leading-snug">
                      {course.title}
                    </h3>

                    {course.programMembership && (
                      <div className="rounded-xl border border-purple-200/80 bg-purple-50/70 p-2.5 text-xs text-purple-800 dark:border-purple-500/20 dark:bg-purple-500/10 dark:text-purple-300">
                        <div className="flex items-center gap-1.5 font-semibold">
                          <GraduationCap className="h-3.5 w-3.5 text-purple-600 dark:text-purple-400 shrink-0" />
                          <span>{isEn ? 'Included in Program:' : 'Incluido en el Programa:'}</span>
                        </div>
                        <p className="mt-0.5 text-[11px] font-medium text-purple-700 dark:text-purple-300 line-clamp-1">
                          {course.programMembership.programName}
                        </p>
                        <p className="mt-1 text-[10px] text-purple-600/90 dark:text-purple-400/80">
                          {isEn
                            ? 'Cannot be taken standalone. Follow its chronological sequence inside the program.'
                            : 'No se puede cursar de forma aislada. Sigue su orden cronológico en el programa.'}
                        </p>
                      </div>
                    )}

                    {course.skillName && !course.programMembership && (
                      <div className="flex items-center gap-1.5 text-xs text-indigo-600 dark:text-indigo-400 font-semibold">
                        <Sparkles className="h-3.5 w-3.5 shrink-0" />
                        <span className="truncate">{course.skillName}</span>
                      </div>
                    )}

                    {course.description && (
                      <p className="text-xs text-slate-500 dark:text-white/60 line-clamp-2 leading-relaxed">
                        {course.description}
                      </p>
                    )}

                    {(course.sectionCount || course.nodeCount) && (
                      <div className="flex items-center gap-2 pt-1 text-[11px] text-slate-400 dark:text-white/40">
                        <Layers className="h-3.5 w-3.5" />
                        <span>
                          {course.sectionCount
                            ? `${course.sectionCount} ${isEn ? 'engrams' : 'engramas'}`
                            : `${course.nodeCount} ${isEn ? 'learning nodes' : 'nodos de aprendizaje'}`}
                        </span>
                      </div>
                    )}

                    {typeof course.completedSectionCount === 'number' && course.completedSectionCount > 0 && (
                      <div className="flex items-center gap-2 text-[11px] font-semibold text-emerald-600 dark:text-emerald-400">
                        <CheckCircle2 className="h-3.5 w-3.5" />
                        <span>
                          {isEn
                            ? `${course.completedSectionCount} of ${course.sectionCount ?? course.completedSectionCount} engrams completed`
                            : `${course.completedSectionCount} de ${course.sectionCount ?? course.completedSectionCount} engramas completados`}
                        </span>
                      </div>
                    )}
                  </div>
                </div>

                {/* Card Action Footer */}
                <div className="border-t border-slate-100 p-4 pt-3 dark:border-white/5">
                  {course.programMembership ? (
                    <Link
                      to={`/programs/${course.programMembership.programId}`}
                      className="flex w-full items-center justify-center gap-2 rounded-2xl bg-purple-600 py-2.5 text-xs font-bold text-white shadow-md shadow-purple-600/20 transition hover:bg-purple-700 hover:shadow-lg"
                    >
                      <GraduationCap className="h-4 w-4" />
                      <span>{isEn ? 'View in Program' : 'Ver en su Programa'}</span>
                    </Link>
                  ) : course.isExternalProgram ? (
                    <a
                      href={course.targetUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="flex w-full items-center justify-center gap-2 rounded-2xl bg-indigo-600 py-2.5 text-xs font-bold text-white shadow-md shadow-indigo-600/20 transition hover:bg-indigo-700 hover:shadow-lg"
                    >
                      <PlayCircle className="h-4 w-4" />
                      <span>{isEn ? 'Open Program' : 'Abrir en Engram'}</span>
                      <ExternalLink className="h-3.5 w-3.5" />
                    </a>
                  ) : (
                    <Link
                      to={course.targetUrl}
                      className="flex w-full items-center justify-center gap-2 rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 py-2.5 text-xs font-bold text-white shadow-md shadow-indigo-600/20 transition hover:brightness-110 hover:shadow-lg"
                    >
                      <PlayCircle className="h-4 w-4" />
                      <span>
                        {effectiveStatus === 'Completed'
                          ? isEn ? 'Review Course' : 'Repasar Curso'
                          : effectiveStatus === 'InProgress'
                            ? isEn ? 'Continue Course' : 'Continuar Curso'
                            : isEn ? 'Start Course' : 'Iniciar Curso'}
                      </span>
                    </Link>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
