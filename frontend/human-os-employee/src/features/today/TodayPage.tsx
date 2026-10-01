import { useEffect, useMemo, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate } from 'react-router';
import { UserPlus, Briefcase, Camera, RefreshCw, Users, GraduationCap, Calendar, type LucideIcon } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getEnrolledCapabilities, type EnrolledCapability } from '@/features/capabilities/api/enrolledCapabilitiesApi';
import { getMyProfile, getProfilePhotoUrl, uploadProfilePhoto, type PersonProfileDto } from '@/features/profile/api/personProfileApi';
import { getMyResume, type PersonResumeDto } from '@/features/profile/api/resumeApi';
import { getLearnerProfile } from '@/features/capabilities/api/engramImpactApi';
import { getPersonAcademicPreferences } from '@/features/profile/api/personAcademicPreferencesApi';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { ProgressBar } from '@/components/ui/ProgressBar';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import type { CapabilityLevel, CapabilityProgress } from './types';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { demoTecPrograms } from './demoTecPrograms';
import { TecContigoHub, LearningExecutiveSummary } from '@/features/tec-contigo';
import { TecContigoAgentChat } from '@/features/tec-contigo/TecContigoAgentChat';
import { TecContigoVoiceModal } from '@/features/tec-contigo/TecContigoVoiceModal';
import { BuddyVoiceModal } from './components/BuddyVoiceModal';

type GreetingKey = 'morning' | 'afternoon' | 'evening';

interface AcademicSummary {
  institution?: string;
  degree?: string;
  graduationYear?: string;
  academicStage?: string;
  average?: string;
}

interface AcademicMetrics {
  average?: string;
  bestGrade?: string;
  worstGrade?: string;
  semester?: string;
}

interface ParsedAcademicGrade {
  name: string;
  grade: number;
}

function formatAcademicValue(value: number | null | undefined, decimals = 1): string {
  if (value === null || value === undefined) return 'En curso';
  const normalized = value > 10 ? value / 10 : value;
  return `${normalized.toFixed(decimals)}/10`;
}

function parseAcademicGrades(text: string | null | undefined): ParsedAcademicGrade[] {
  if (!text) return [];
  const start = text.search(/cardex\s+acad[eé]mico/i);
  if (start < 0) return [];
  const academicText = text.slice(start).split(/(?:distinciones\s+acad[eé]micas|declaraci[oó]n\s+profesional|professional\s+objective|professional\s+summary|\bskills\b|work\s+experience|\beducation\b)/i)[0];
  const excluded = /^(semestre\b|promedio\b|generaci[oó]n\b|top\s+5|reducci[oó]n\s+estimada|materia\s+calificaci[oó]n)/i;
  const tools = /azure\s+functions|azure\s+data\s+factory|\bgit\b|\bdocker\b|power\s+bi|visual\s+studio\s+code|jupyter\s+notebook|\blinux\b/i;
  return academicText.split(/\r?\n/).map((line) => line.trim()).flatMap((line) => {
    if (!line || excluded.test(line) || tools.test(line) || /\b(?:19|20)\d{2}\b|\d+%|\b(?:NA|null|undefined)\b/i.test(line)) return [];
    const match = line.match(/^(.+?)\s+(\d{2}(?:\.\d+)?)\s*(?:\/\s*100)?\s*$/);
    if (!match || [...line.matchAll(/\d{2}(?:\.\d+)?/g)].length !== 1) return [];
    const name = match[1].trim();
    if (name.length > 80 || name.includes('•') || name.split(/\s+/).length > 10 || /promedio|cardex|semestre|materia\s+calificaci[oó]n|skills|herramientas|work\s+experience|education/i.test(name) || /[.!?].*[.!?]/.test(name)) return [];
    const grade = Number(match[2]);
    return grade >= 0 && grade <= 100 ? [{ name, grade }] : [];
  });
}

function extractAcademicSummary(education: string | null | undefined): AcademicSummary {
  if (!education?.trim()) return {};
  const text = education.trim();
  const institution = text.match(/tecnol[oó]gico\s+de\s+monterrey/i)?.[0];
  const degree = text.match(/(?:—|-|–)\s*([^()\n]+?)\s*\(/)?.[1]?.trim();
  const graduationYear = extractExpectedGraduationYear(text);
  const stageMatch = text.match(/[úu]ltimo\s+semestre|semestre\s+en\s+curso|graduad[oa]|en\s+curso/i);
  const averageMatch = text.match(/(?:promedio\s+(?:general\s+)?acumulado|gpa)\s*:\s*(\d+(?:[.,]\d+)?)\s*\/\s*100/i);
  return {
    institution,
    degree,
    graduationYear,
    academicStage: stageMatch?.[0]?.trim(),
    average: averageMatch ? `${averageMatch[1].replace(',', '.')}/100` : undefined,
  };
}

function extractExpectedGraduationYear(text: string): string | undefined {
  const range = text.match(/\b((?:19|20)\d{2})\s*(?:–|-|—|a)\s*((?:19|20)\d{2})\b/i);
  if (range) return range[2];
  const singleYear = text.match(/\b((?:19|20)\d{2})\b/i);
  if (singleYear && /generaci[oó]n|graduaci[oó]n|esperad[oa]|expected/i.test(text)) return singleYear[1];
  return undefined;
}

function useGreetingKey(): GreetingKey {
  const hour = new Date().getHours();
  if (hour < 12) return 'morning';
  if (hour < 18) return 'afternoon';
  return 'evening';
}

/** Courses don't have a "mastery" flag — level is derived from the average
 *  score of nodes already mastered so far in that capability. */
function scoreToLevel(averageScore: number | null | undefined): CapabilityLevel {
  if (averageScore === null || averageScore === undefined) return 'none';
  if (averageScore < 60) return 'basico';
  if (averageScore < 85) return 'intermedio';
  return 'avanzado';
}

/** Employee portal dashboard ("Hoy") — ported from human-os-web's /today
 *  (2026-08-01), trimmed to what makes sense for a company employee: real
 *  progress across the courses their company assigned them, plus a
 *  "Continue Learning" resume list. No personal-goals/growth-plan or
 *  human-state check-in sections here — those are consumer-app-only
 *  features this portal never adopted (v1 scope is just the assigned
 *  learning runtime, see EmployeeSidebar's doc comment). */

type CompanyStageLink = {
  title: string;
  description: string;
  status: string;
  moment: string;
  momentColor: string;
  icon: LucideIcon;
  active?: boolean;
  href?: string;
};

/** Corporate mirror of tec-contigo's Tec360StageNavigation — one company
 * relationship lifecycle instead of the academic one, shown only for
 * non-academic tenants (see TecContigoHub's isAcademicTenant gate below). */
function CompanyLifecycleNavigation({ tenantName, isEn }: { tenantName: string; isEn: boolean }) {
  const navigate = useNavigate();
  const stages: CompanyStageLink[] = [
    {
      title: isEn ? 'Start in Your Role' : 'Comienza en tu rol',
      description: isEn
        ? 'Get to know the company, complete your essential training, and begin developing the knowledge required for your role.'
        : 'Conoce la empresa, completa tu capacitación esencial y comienza a desarrollar los conocimientos que requiere tu puesto.',
      status: isEn ? 'You are here' : 'Estás aquí',
      moment: isEn ? 'First 90 days' : 'Primeros 90 días',
      momentColor: 'text-emerald-700 dark:text-emerald-300',
      icon: UserPlus,
      active: true,
      href: '/start-in-your-role',
    },
    {
      title: isEn ? 'In your role' : 'En tu puesto',
      description: isEn
        ? 'Grow in your day-to-day: assigned courses, real objectives, and evidence of your progress.'
        : 'Crece en tu día a día: cursos asignados, objetivos reales y evidencia de tu progreso.',
      status: isEn ? 'Ongoing' : 'En curso',
      moment: isEn ? 'Every day' : 'Cada día',
      momentColor: 'text-blue-700 dark:text-blue-300',
      icon: Briefcase,
      href: '/in-your-role',
    },
    {
      title: isEn ? 'Stay current' : 'Mantente vigente',
      description: isEn
        ? 'Update your capabilities as your role and industry evolve.'
        : 'Actualiza tus capacidades conforme evolucionan tu puesto y tu industria.',
      status: isEn ? 'Throughout your career' : 'A lo largo de tu carrera',
      moment: isEn ? 'Always' : 'Siempre',
      momentColor: 'text-amber-700 dark:text-amber-300',
      icon: RefreshCw,
      href: '/advance-your-knowledge',
    },
    {
      title: isEn ? 'Master your job' : 'Domina tu puesto',
      description: isEn
        ? 'Reach peak performance in your role and prepare for leadership or specialization.'
        : 'Alcanza el máximo desempeño en tu puesto y prepárate para el liderazgo o la especialización.',
      status: isEn ? 'Before your next chapter' : 'Antes de tu siguiente etapa',
      moment: isEn ? 'Seniority & Mastery' : 'Madurez y Dominio',
      momentColor: 'text-cyan-700 dark:text-cyan-300',
      icon: GraduationCap,
      href: '/master-your-job',
    },
    {
      title: isEn ? `After ${tenantName}` : `Después de ${tenantName}`,
      description: isEn
        ? 'What you built here stays with you — turn it into your next opportunity, wherever it is.'
        : 'Lo que construiste aquí te acompaña — conviértelo en tu siguiente oportunidad, donde sea que esté.',
      status: isEn ? 'Whenever that day comes' : 'Cuando llegue ese momento',
      moment: isEn ? 'Alumni' : 'Alumni',
      momentColor: 'text-violet-700 dark:text-violet-300',
      icon: Users,
    },
  ];

  return (
    <section className="mx-auto max-w-6xl px-6 pt-8" aria-labelledby="company-lifecycle-title">
      <h3 id="company-lifecycle-title" className="text-2xl font-bold tracking-tight text-blue-600 dark:text-amber-400">
        {isEn ? `Life Moments` : `Momentos de vida`}
      </h3>
      <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        {stages.map((stage, index) => {
          const Icon = stage.icon;
          return (
            <div
              key={stage.title}
              className={`rounded-2xl border p-4 ${stage.active ? 'border-emerald-300 bg-emerald-50/70 dark:border-emerald-400/30 dark:bg-emerald-400/10' : 'border-slate-200 bg-white dark:border-white/10 dark:bg-white/[0.03]'} ${stage.href ? 'cursor-pointer' : ''}`}
              onClick={stage.href ? () => navigate(stage.href!) : undefined}
              onKeyDown={stage.href ? (event) => { if (event.key === 'Enter' || event.key === ' ') navigate(stage.href!); } : undefined}
              role={stage.href ? 'link' : undefined}
              tabIndex={stage.href ? 0 : undefined}
            >
              <div className="flex items-start justify-between gap-3">
                <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/70">
                  <Icon className="h-4 w-4" aria-hidden="true" />
                </span>
                <span className="text-xs font-semibold text-slate-500 dark:text-white/55">0{index + 1}</span>
              </div>
              <h4 className="mt-4 font-semibold text-slate-950 dark:text-white">{stage.title}</h4>
              <span className={`mt-1 inline-flex items-center gap-1.5 text-xs font-bold ${stage.momentColor}`}>
                <Calendar className="h-3.5 w-3.5" aria-hidden="true" />
                {stage.moment}
              </span>
              <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/65">{stage.description}</p>
              <span
                className={`mt-4 inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${stage.active ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-400/20 dark:text-emerald-200' : 'bg-slate-100 text-slate-600 dark:bg-white/10 dark:text-white/65'}`}
              >
                {stage.status}
              </span>
            </div>
          );
        })}
      </div>
    </section>
  );
}

export function TodayPage() {
  const [isAcademicDetailsOpen, setIsAcademicDetailsOpen] = useState(false);
  const [selectedProgramId, setSelectedProgramId] = useState<string | null>(null);
  const [showEngramVideo, setShowEngramVideo] = useState(false);
  const [addedProgramIds, setAddedProgramIds] = useState<Set<string>>(() => new Set());
  const [isBuddyOpen, setIsBuddyOpen] = useState(false);
  const [isTecBotOpen, setIsTecBotOpen] = useState(false);
  const [isTecBotVoiceOpen, setIsTecBotVoiceOpen] = useState(false);
  const { user } = useAuth();
  const { t, language } = useI18n();
  const personId = user?.personId ?? '';
  const greetingKey = useGreetingKey();
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const profileQuery = useQuery<PersonProfileDto | null>({
    queryKey: ['my-profile', personId],
    queryFn: () => getMyProfile(personId),
    enabled: !!personId,
  });
  const resumeQuery = useQuery<PersonResumeDto>({
    queryKey: ['my-resume', personId],
    queryFn: () => getMyResume(personId),
    enabled: !!personId,
  });
  const learnerProfileQuery = useQuery({
    queryKey: ['learner-profile', personId],
    queryFn: () => getLearnerProfile(personId),
    enabled: !!personId,
  });
  const academicPreferencesQuery = useQuery({
    queryKey: ['person-academic-preferences', personId],
    queryFn: () => getPersonAcademicPreferences(personId),
    enabled: !!personId,
  });

  const uploadPhotoMutation = useMutation({
    mutationFn: (file: File) => uploadProfilePhoto(personId, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-profile', personId] });
    },
  });

  function handlePhotoChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) uploadPhotoMutation.mutate(file);
  }

  const greetings: Record<GreetingKey, string> = useMemo(() => ({
    morning: t.todayGreetingMorning,
    afternoon: t.todayGreetingAfternoon,
    evening: t.todayGreetingEvening,
  }), [t]);

  const [capabilities, setCapabilities] = useState<EnrolledCapability[]>([]);
  const [initialLoading, setInitialLoading] = useState(true);

  useEffect(() => {
    if (!personId) return;
    let cancelled = false;

    getEnrolledCapabilities(personId)
      .then((data) => {
        if (!cancelled) setCapabilities(data);
      })
      .finally(() => {
        if (!cancelled) setInitialLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [personId]);

  const capabilityProgress: CapabilityProgress[] = useMemo(
    () =>
      capabilities.map((cap) => ({
        id: cap.capabilityId,
        name: cap.name,
        progress: cap.totalNodes > 0 ? Math.round((cap.masteredNodes / cap.totalNodes) * 100) : 0,
        level: scoreToLevel(cap.averageScore),
        supports: [cap.subjectName ?? 'Independiente'],
      })),
    [capabilities],
  );

  const profile = profileQuery.data;
  const resume = resumeQuery.data;
  const profileName = profile?.DisplayName || [profile?.FirstName, profile?.LastName].filter(Boolean).join(' ');
  const emailName = user?.email?.split('@')[0]
    ?.split(/[._-]/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
    .join(' ');
  // Prefer the real profile name, then a name derived from the email —
  // the MSAL account "name" claim can be a bare username/test-account
  // string (e.g. for demo accounts) instead of the person's actual name.
  const safeName = profileName || emailName || user?.name;
  const displayName = safeName?.split(' ')[0] ?? '';
  const highlightedSkills = resume?.skills?.slice(0, 5) ?? [];
  const additionalSkills = Math.max((resume?.skills?.length ?? 0) - highlightedSkills.length, 0);
  const education = resume?.education?.[0]?.trim();
  const academicSource = [resume?.rawResumeText, ...(resume?.education ?? [])].filter(Boolean).join('\n');
  const academic = extractAcademicSummary(education);
  const academicProfile = resume?.studentAcademicProfile;
  const isStudent = learnerProfileQuery.data?.isStudent ?? false;
  const parsedAcademicGrades = parseAcademicGrades(academicSource);
  const academicGrades = parsedAcademicGrades.map(({ grade }) => grade);
  const currentSemester = academicProfile?.semesters.find((semester) => /actual/i.test(semester.name ?? ''));
  const officialAverage = academicSource.match(/promedio\s+general\s+acumulado\s*:\s*(\d+(?:[.,]\d+)?)\s*\/\s*100/i)?.[1];
  const academicMetrics: AcademicMetrics = {
    average: officialAverage
      ? `${(Number(officialAverage.replace(',', '.')) / 10).toFixed(2)}/10`
      : undefined,
    bestGrade: academicGrades.length ? `${(Math.max(...academicGrades) / 10).toFixed(1)}/10` : undefined,
    worstGrade: academicGrades.length ? `${(Math.min(...academicGrades) / 10).toFixed(1)}/10` : undefined,
    semester: currentSemester?.name?.replace(/\s*\(Actual\)/i, '') ?? undefined,
  };
  const currentSubjects = currentSemester?.subjects.filter((subject) => subject.name?.trim()) ?? [];
  const recommendationStyles: Record<string, { card: string; badge: string; accent: string }> = {
    'thesis-preparation': { card: 'border-indigo-200/80 dark:border-indigo-400/25', badge: 'border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-400/25 dark:bg-indigo-400/10 dark:text-indigo-200', accent: 'bg-indigo-500' },
    'executive-technical-communication': { card: 'border-amber-200/80 dark:border-amber-400/25', badge: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-400/25 dark:bg-amber-400/10 dark:text-amber-200', accent: 'bg-amber-500' },
    'azure-ai-fundamentals': { card: 'border-sky-200/80 dark:border-sky-400/25', badge: 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-400/25 dark:bg-sky-400/10 dark:text-sky-200', accent: 'bg-sky-500' },
    'llm-enterprise-architecture': { card: 'border-violet-200/80 dark:border-violet-400/25', badge: 'border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-400/25 dark:bg-violet-400/10 dark:text-violet-200', accent: 'bg-violet-500' },
    'neural-networks-deep-learning': { card: 'border-indigo-200/80 dark:border-indigo-400/25', badge: 'border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-400/25 dark:bg-indigo-400/10 dark:text-indigo-200', accent: 'bg-indigo-500' },
    'enterprise-prompt-engineering': { card: 'border-violet-200/80 dark:border-violet-400/25', badge: 'border-violet-200 bg-violet-50 text-violet-700 dark:border-violet-400/25 dark:bg-violet-400/10 dark:text-violet-200', accent: 'bg-violet-500' },
    'machine-learning-production': { card: 'border-indigo-200/80 dark:border-indigo-400/25', badge: 'border-indigo-200 bg-indigo-50 text-indigo-700 dark:border-indigo-400/25 dark:bg-indigo-400/10 dark:text-indigo-200', accent: 'bg-indigo-500' },
  };
  void recommendationStyles;
  const hasAcademicMetrics = Object.values(academicMetrics).some(Boolean);
  const selectedProgram = demoTecPrograms.find((program) => program.id === selectedProgramId);
  const selectedProgramIndex = selectedProgram ? demoTecPrograms.indexOf(selectedProgram) : -1;
  const selectedRelationships = selectedProgram?.relatedSubjects.filter((subject) => currentSubjects.some((current) => current.name?.toLowerCase() === subject.toLowerCase())) ?? [];
  const recommendationContext = demoTecPrograms.map((program) => program.recommendationType);
  const recommendationReasons = demoTecPrograms.map((program) => program.recommendationReason?.es ?? 'Sugerido para explorar capacidades relacionadas con tu ruta profesional.');

  useEffect(() => {
    if (!selectedProgram) return;
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') setSelectedProgramId(null); };
    document.addEventListener('keydown', closeOnEscape);
    document.body.style.overflow = 'hidden';
    return () => { document.removeEventListener('keydown', closeOnEscape); document.body.style.overflow = ''; };
  }, [selectedProgram]);


  useEffect(() => {
    if (!showEngramVideo) return;
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') setShowEngramVideo(false); };
    document.addEventListener('keydown', closeOnEscape);
    document.body.style.overflow = 'hidden';
    return () => { document.removeEventListener('keydown', closeOnEscape); document.body.style.overflow = ''; };
  }, [showEngramVideo]);

  return (
    <div className="min-h-screen bg-[#f5f7fb] dark:bg-[#05060a]">
      {initialLoading && (
        <LoadingBackdrop title={t.todayLoadingTitle} description={t.todayLoadingDesc} />
      )}
      <main>
        <style>{`span[class*="text-[10px]"] { display: none; }`}</style>
        <div className="mx-auto max-w-6xl px-6 pt-10">
          {user?.isAcademicTenant && (
            <img src="/TecMOnterreyLogo.png" alt="Tecnológico de Monterrey" className="mb-5 h-10 sm:h-12 w-auto object-contain object-left" />
          )}
          <div className="mb-4 flex flex-wrap items-center gap-2">
            <Link
              to="/structured-learning/9e6fb348-c614-41d1-820c-8f792ea3ead9"
              className="inline-flex items-center gap-1.5 rounded-lg border border-blue-300 bg-blue-50 px-3 py-1.5 text-xs font-semibold text-blue-600 underline-offset-4 hover:underline dark:border-blue-500/30 dark:bg-blue-500/10 dark:text-blue-400"
            >
              {language === 'es' ? '¿Qué es Human OS y cómo funciona?' : 'What is Human OS and how does it work?'}
            </Link>
            <button
              type="button"
              onClick={() => setShowEngramVideo(true)}
              className="inline-flex items-center gap-1.5 rounded-lg border border-blue-300 bg-blue-50 px-3 py-1.5 text-xs font-semibold text-blue-600 underline-offset-4 hover:underline dark:border-blue-500/30 dark:bg-blue-500/10 dark:text-blue-400"
            >
              {language === 'es' ? '¿Qué es un Engram? (ver video)' : 'What is an Engram? (watch video)'}
            </button>
          </div>
          {!isStudent && (
            <div className="mb-6 flex items-center">
              <img
                src={user?.tenantId ? `/api/tenants/${user.tenantId}/logo` : '/HermanLogo.png'}
                onError={(e) => {
                  (e.currentTarget as HTMLImageElement).src = '/HermanLogo.png';
                }}
                alt={user?.tenantName ?? (language === 'es' ? 'Crece con nosotros' : 'Grow With Us')}
                className="h-16 w-auto max-w-[240px] rounded-xl object-contain object-left bg-white/80 dark:bg-white/5 p-1.5 shadow-sm border border-slate-200/80 dark:border-white/10"
              />
            </div>
          )}
          <p className="bg-gradient-to-r from-emerald-600 to-teal-500 bg-clip-text text-sm font-semibold uppercase tracking-[0.18em] text-transparent dark:from-emerald-400 dark:to-teal-300">
            {language === 'es' ? 'Crece con nosotros' : 'Grow With Us'}
          </p>
          <p className="mt-3 text-3xl font-semibold tracking-tight text-slate-950 dark:text-white">
            {greetings[greetingKey]}{displayName ? `, ${displayName}` : ''}
          </p>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500 dark:text-white/60">
            {language === 'es'
              ? 'Tu ruta de crecimiento conecta tu perfil, tus objetivos y las capacidades que necesitas para tu siguiente etapa profesional.'
              : 'We want to help you maximize your full potential.'}
          </p>
          {(academic.degree || academic.graduationYear || academic.academicStage) && (
            <p className="mt-4 text-sm font-medium text-slate-600 dark:text-white/60">
              {[academic.degree, academic.graduationYear && `${language === 'es' ? 'Generación' : 'Class of'} ${academic.graduationYear}`, academic.academicStage].filter(Boolean).join(' · ')}
            </p>
          )}
        </div>

        <section className="mx-auto max-w-6xl px-6 pt-8">
          <Card className="!border-0 !bg-slate-950 p-7 text-white shadow-xl shadow-blue-950/10 dark:!border dark:border-white/10 dark:!bg-white/[0.05] dark:shadow-none">
            <div className="flex items-start justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="group relative h-40 w-40 shrink-0">
                  {profile?.ProfilePhotoUrl ? (
                    <img
                      src={getProfilePhotoUrl(personId, profile.UpdatedDate)}
                      alt={safeName}
                      className="h-40 w-40 rounded-2xl object-cover ring-1 ring-white/25"
                    />
                  ) : (
                    <span className="flex h-40 w-40 items-center justify-center rounded-2xl bg-white/15 text-5xl font-bold ring-1 ring-white/25 backdrop-blur">
                      {safeName?.trim().charAt(0).toUpperCase() || '?'}
                    </span>
                  )}
                  <button
                    type="button"
                    onClick={() => fileInputRef.current?.click()}
                    disabled={uploadPhotoMutation.isPending}
                    aria-label={language === 'es' ? 'Cambiar foto de perfil' : 'Change profile photo'}
                    title={language === 'es' ? 'Cambiar foto de perfil' : 'Change profile photo'}
                    className="absolute -bottom-2 -right-2 flex h-9 w-9 items-center justify-center rounded-full border-2 border-slate-950 bg-blue-600 text-white shadow-md transition hover:bg-blue-700 disabled:opacity-60 dark:border-slate-900"
                  >
                    <Camera className="h-4 w-4" />
                  </button>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/png,image/jpeg,image/webp"
                    className="hidden"
                    onChange={handlePhotoChange}
                  />
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-300">{language === 'es' ? 'Tu perfil hoy' : 'Your profile today'}</p>
                  <h2 className="mt-3 text-2xl font-semibold">{safeName}</h2>
                </div>
              </div>
              <Link to="/profile" className="rounded-full border border-white/20 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-white/10">
                {language === 'es' ? 'Ver perfil' : 'View profile'}
              </Link>
            </div>
            {profile?.Occupation && <p className="mt-3 text-sm text-slate-300">{profile.Occupation}</p>}
            {academic.degree && <p className="mt-5 text-base font-medium text-white">{academic.degree}</p>}
            {(academic.academicStage || academic.graduationYear) && <p className="mt-1 text-sm font-medium text-white/90 dark:text-white/75">{[academic.academicStage, academic.graduationYear && `${language === 'es' ? 'Generación' : 'Class of'} ${academic.graduationYear}`].filter(Boolean).join(' · ')}</p>}
            {highlightedSkills.length > 0 && <div className="mt-5 flex flex-wrap gap-2">{highlightedSkills.map((skill) => <Badge key={skill}>{skill}</Badge>)}{additionalSkills > 0 && <Badge>+{additionalSkills} {language === 'es' ? 'capacidades' : 'capabilities'}</Badge>}</div>}
          </Card>
        </section>

        {hasAcademicMetrics && (
          <section className="mx-auto max-w-6xl px-6 pt-6">
            <Card className="border-0 p-6 shadow-sm shadow-slate-200/70 dark:border dark:border-white/10 dark:shadow-none">
              <div className="flex items-center justify-between gap-4">
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-600 dark:text-blue-300">{language === 'es' ? 'Aspecto académico' : 'Academic snapshot'}</p>
                  <h2 className="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{language === 'es' ? 'Tu rendimiento en contexto' : 'Your performance in context'}</h2>
                </div>
                <button type="button" onClick={() => setIsAcademicDetailsOpen(true)} className="text-sm font-medium text-blue-600 dark:text-blue-300">{language === 'es' ? 'Ver detalle' : 'View details'}</button>
              </div>
              <div className="mt-5 grid grid-cols-2 gap-4 sm:grid-cols-4">
                {[
                  [language === 'es' ? 'Promedio acumulado' : 'Cumulative average', academicMetrics.average],
                  [language === 'es' ? 'Mejor calificación' : 'Best grade', academicMetrics.bestGrade],
                  [language === 'es' ? 'Peor calificación' : 'Lowest grade', academicMetrics.worstGrade],
                  [language === 'es' ? 'Semestre' : 'Semester', academicMetrics.semester],
                ].filter((metric): metric is [string, string] => Boolean(metric[1])).map(([label, value]) => (
                  <div key={label} className="rounded-2xl bg-slate-50 p-4 dark:bg-white/[0.06]">
                    <p className="text-xs text-slate-500 dark:text-white/50">{label}</p>
                    <p className="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{value}</p>
                  </div>
                ))}
              </div>
            </Card>
          </section>
        )}

        {/* TODO: Después de validar las páginas de Tec Contigo, las listas detalladas podrán moverse desde Today hacia sus páginas especializadas. */}

        {academicPreferencesQuery.data && (() => {
          const prefs = academicPreferencesQuery.data;
          const groups: { label: string; values: string[] }[] = [
            { label: language === 'es' ? 'Intereses académicos' : 'Academic interests', values: prefs.AcademicInterests },
            { label: language === 'es' ? 'Preferencias de aprendizaje' : 'Learning preferences', values: prefs.LearningPreferences },
            { label: language === 'es' ? 'Motivaciones' : 'Motivations', values: prefs.AcademicMotivations },
            { label: language === 'es' ? 'Áreas de crecimiento' : 'Growth areas', values: prefs.GrowthAreas },
          ].filter((group) => group.values.length > 0);

          if (groups.length === 0) return null;

          return (
            <section className="mx-auto max-w-6xl px-6 pt-8">
              <Card className="border-0 p-6 shadow-sm shadow-slate-200/70 dark:border dark:border-white/10 dark:shadow-none">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-600 dark:text-blue-300">
                  {language === 'es' ? 'Tu perfil académico' : 'Your academic profile'}
                </p>
                <h2 className="mt-2 text-xl font-semibold text-slate-950 dark:text-white">
                  {language === 'es' ? 'Tus intereses y preferencias' : 'Your interests and preferences'}
                </h2>
                <div className="mt-5 grid gap-4 sm:grid-cols-2">
                  {groups.map((group) => (
                    <div key={group.label}>
                      <p className="text-xs text-slate-500 dark:text-white/50">{group.label}</p>
                      <div className="mt-2 flex flex-wrap gap-2">
                        {group.values.map((value) => (
                          <Badge key={value}>{value}</Badge>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              </Card>
            </section>
          );
        })()}

        {/* tecBot is ONLY for students (Academic tenant) — every other user
            (employee, HR admin, manager, etc.) gets Buddy instead, never tecBot. */}
        {user?.isAcademicTenant ? (
          <section className="mx-auto max-w-6xl px-6 pt-8">
            <Card className="overflow-hidden border border-emerald-200/80 bg-gradient-to-br from-white via-emerald-50/40 to-teal-50/60 p-6 shadow-md dark:border-emerald-500/20 dark:from-slate-900 dark:via-slate-900/90 dark:to-emerald-950/40 sm:p-8">
              <div className="flex flex-col items-center gap-6 sm:flex-row sm:items-start sm:gap-8">
                <div className="relative shrink-0">
                  <img
                    src="/TecBot.png"
                    alt="tecBot"
                    className="h-40 w-40 object-contain sm:h-52 sm:w-52"
                  />
                  <span className="absolute -bottom-2 -right-2 rounded-full bg-emerald-500 px-2.5 py-0.5 text-[11px] font-bold text-white shadow">
                    Online
                  </span>
                </div>
                <div className="flex-1 text-center sm:text-left">
                  <div className="inline-flex items-center gap-2 rounded-full bg-emerald-100/80 px-3 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-400/10 dark:text-emerald-300">
                    <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
                    Te conoce, te entiende y te acompaña durante toda tu vida de aprendizaje
                  </div>
                  <h3 className="mt-3 text-2xl font-bold tracking-tight text-slate-950 dark:text-white sm:text-3xl">
                    tecBot
                  </h3>
                  <p className="mt-1 text-sm font-semibold text-emerald-700 dark:text-emerald-300">Tu Lifelong Learning Partner</p>
                  <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-600 dark:text-white/75 sm:text-base">
                    {language === 'es'
                      ? 'tecBot conoce tus calificaciones, los cursos que has tomado y la carrera que estás cursando. Pregúntale qué cursos te recomienda y qué necesitas mejorar para seguir avanzando en tu aprendizaje.'
                      : 'tecBot knows your grades, the courses you have taken, and the career you are pursuing. Ask which courses it recommends and what you need to improve to keep progressing in your learning.'}
                  </p>
                  <div className="mt-5 flex flex-wrap items-center justify-center gap-3 sm:justify-start">
                    <button
                      type="button"
                      onClick={() => setIsTecBotOpen(true)}
                      className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-5 py-2.5 text-sm font-semibold text-white shadow-md shadow-emerald-600/25 transition hover:bg-emerald-700"
                    >
                      <span>{language === 'es' ? 'Escríbele a tecBot' : 'Chat with tecBot'}</span>
                      <span aria-hidden="true">&rarr;</span>
                    </button>
                    <button
                      type="button"
                      onClick={() => setIsTecBotVoiceOpen(true)}
                      className="inline-flex items-center gap-2 rounded-xl border border-emerald-300 bg-white px-5 py-2.5 text-sm font-semibold text-emerald-700 shadow-sm transition hover:bg-emerald-50 dark:border-emerald-400/30 dark:bg-white/5 dark:text-emerald-300 dark:hover:bg-white/10"
                    >
                      <span>{language === 'es' ? 'Habla por voz con tecBot' : 'Talk to tecBot by voice'}</span>
                      <span aria-hidden="true">&rarr;</span>
                    </button>
                    <Link
                      to="/learning-labs"
                      className="inline-flex items-center gap-2 rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 dark:border-white/10 dark:bg-white/5 dark:text-white dark:hover:bg-white/10"
                    >
                      {language === 'es' ? 'Explorar todos los Labs' : 'Explore all Labs'}
                    </Link>
                  </div>
                </div>
              </div>
            </Card>
          </section>
        ) : (
          <section className="mx-auto max-w-6xl px-6 pt-8">
            <Card className="overflow-hidden border border-blue-200/80 bg-gradient-to-br from-white via-blue-50/40 to-indigo-50/60 p-6 shadow-md dark:border-blue-500/20 dark:from-slate-900 dark:via-slate-900/90 dark:to-blue-950/40 sm:p-8">
              <div className="flex flex-col items-center gap-6 sm:flex-row sm:items-start sm:gap-8">
                <div className="relative shrink-0">
                  <img
                    src="/buddy.png"
                    alt="Buddy"
                    className="h-40 w-40 object-contain sm:h-52 sm:w-52"
                  />
                  <span className="absolute -bottom-2 -right-2 rounded-full bg-blue-500 px-2.5 py-0.5 text-[11px] font-bold text-white shadow">
                    Online
                  </span>
                </div>
                <div className="flex-1 text-center sm:text-left">
                  <h3 className="mt-3 text-2xl font-bold tracking-tight text-slate-950 dark:text-white sm:text-3xl">
                    Buddy
                  </h3>
                  <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-600 dark:text-white/75 sm:text-base">
                    {language === 'es'
                      ? 'Buddy conoce tu perfil, tus habilidades, tu jefe y tu departamento. Pregúntale sobre tu rol o la logística general de tu empresa.'
                      : 'Buddy knows your profile, your skills, your manager and your department. Ask it about your role or general company logistics.'}
                  </p>
                  <div className="mt-5 flex flex-wrap items-center justify-center gap-3 sm:justify-start">
                    <button
                      type="button"
                      onClick={() => setIsBuddyOpen(true)}
                      className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white shadow-md shadow-blue-600/25 transition hover:bg-blue-700"
                    >
                      <span>{language === 'es' ? 'Habla con Buddy' : 'Talk to Buddy'}</span>
                      <span aria-hidden="true">&rarr;</span>
                    </button>
                  </div>
                </div>
              </div>
            </Card>
          </section>
        )}

        <BuddyVoiceModal
          open={isBuddyOpen}
          onClose={() => setIsBuddyOpen(false)}
          personName={displayName}
          language={language === 'en' ? 'en' : 'es'}
        />

        {user?.isAcademicTenant && <TecContigoAgentChat open={isTecBotOpen} onOpenChange={setIsTecBotOpen} />}

        {user?.isAcademicTenant && (
          <TecContigoVoiceModal
            open={isTecBotVoiceOpen}
            onClose={() => setIsTecBotVoiceOpen(false)}
            personId={personId || undefined}
            personName={displayName}
            language={language === 'en' ? 'en' : 'es'}
          />
        )}

        {user?.isAcademicTenant ? (
          <TecContigoHub />
        ) : (
          <CompanyLifecycleNavigation tenantName={user?.tenantName ?? 'tu empresa'} isEn={language === 'en'} />
        )}

        {capabilityProgress.length > 0 && (
          <section className="mx-auto max-w-6xl px-6 pt-8">
            <div className="mb-4 flex items-center justify-between gap-4">
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{language === 'es' ? 'Progreso actual' : 'Current progress'}</h2>
              <Link to="/capabilities" className="text-sm font-medium text-blue-600 dark:text-blue-300">{language === 'es' ? 'Ver plan' : 'View plan'}</Link>
            </div>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {capabilityProgress.slice(0, 5).map((capability) => (
                <Link key={capability.id} to={`/capabilities/${capability.id}`}>
                  <Card className="p-4 transition hover:border-blue-300 dark:hover:border-blue-400/40">
                    <div className="flex items-center justify-between gap-3"><span className="truncate text-sm font-medium text-slate-900 dark:text-white">{capability.name}</span><span className="text-xs text-slate-500">{capability.progress}%</span></div>
                    <div className="mt-3"><ProgressBar value={capability.progress} /></div>
                  </Card>
                </Link>
              ))}
            </div>
          </section>
        )}

        {/* Moved to the end of the page per explicit product request (2026-09-24). */}
        <section className="mx-auto max-w-6xl px-6 pt-8">
          <LearningExecutiveSummary />
        </section>

      </main>
      {isAcademicDetailsOpen && academicProfile && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-label={language === 'es' ? 'Detalle académico' : 'Academic details'} onClick={() => setIsAcademicDetailsOpen(false)}>
          <div className="max-h-[85vh] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl dark:bg-slate-900" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 className="text-xl font-semibold text-slate-950 dark:text-white">{language === 'es' ? 'Detalle académico' : 'Academic details'}</h2>
                <p className="mt-1 text-sm text-slate-500 dark:text-white/60">{academicProfile.institution} · {academicProfile.degree}</p>
              </div>
              <button type="button" onClick={() => setIsAcademicDetailsOpen(false)} className="text-sm font-semibold text-slate-500 hover:text-slate-900 dark:hover:text-white" aria-label={language === 'es' ? 'Cerrar' : 'Close'}>X</button>
            </div>
            <div className="mt-5 grid gap-3 sm:grid-cols-2">
              {academicProfile.semesters.map((semester) => (
                <div key={semester.name} className="rounded-xl border border-slate-200 p-4 dark:border-white/10">
                  <div className="flex justify-between gap-3 text-sm font-semibold text-slate-900 dark:text-white"><span>{semester.name}</span><span>{formatAcademicValue(semester.average, 2)}</span></div>
                  <div className="mt-3 space-y-1 text-xs text-slate-600 dark:text-white/70">{semester.subjects.map((subject) => <div key={subject.name} className="flex justify-between gap-3"><span>{subject.name}</span><span>{formatAcademicValue(subject.grade)}</span></div>)}</div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
      {selectedProgram && selectedProgramIndex >= 0 && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-3 backdrop-blur-sm" role="dialog" aria-modal="true" aria-labelledby="recommendation-dialog-title" onClick={() => setSelectedProgramId(null)}>
          <div className="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl dark:bg-slate-900" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-start justify-between gap-4"><div className="flex flex-wrap gap-2"><Badge>{recommendationContext[selectedProgramIndex]}</Badge><Badge>{language === 'es' ? 'Curso de demostración' : 'Demo course'}</Badge></div><button autoFocus type="button" onClick={() => setSelectedProgramId(null)} className="text-sm font-semibold text-slate-500 hover:text-slate-900 dark:hover:text-white">{language === 'es' ? 'Cerrar' : 'Close'}</button></div>
            <h2 id="recommendation-dialog-title" className="mt-4 text-2xl font-semibold text-slate-950 dark:text-white">{selectedProgram.title[language]}</h2><p className="mt-2 text-sm text-slate-500">{selectedProgram.type} · {selectedProgram.duration} · {selectedProgram.modality} · {selectedProgram.level}</p>
            <div className="mt-6 space-y-6 text-sm"><section><h3 className="font-semibold text-slate-950 dark:text-white">{language === 'es' ? 'Acerca de este programa' : 'About this program'}</h3><p className="mt-2 leading-6 text-slate-600 dark:text-white/70">{selectedProgram.shortDescription[language]}</p></section><section><h3 className="font-semibold text-slate-950 dark:text-white">{language === 'es' ? 'Por qué te lo recomendamos' : 'Why this is recommended'}</h3><p className="mt-2 leading-6 text-slate-600 dark:text-white/70">{recommendationReasons[selectedProgramIndex]}</p><p className="mt-2 text-xs text-slate-400">{language === 'es' ? 'Recomendación de demostración basada en tu perfil actual.' : 'Demo recommendation based on your current profile.'}</p></section><section><h3 className="font-semibold text-slate-950 dark:text-white">{language === 'es' ? 'Capacidades que desarrollarás' : 'Capabilities you will develop'}</h3><div className="mt-2 flex flex-wrap gap-2">{selectedProgram.skills.slice(0, 6).map((skill) => <Badge key={skill}>{skill}</Badge>)}{selectedProgram.skills.length > 6 && <Badge>+{selectedProgram.skills.length - 6} {language === 'es' ? 'más' : 'more'}</Badge>}</div></section>{selectedRelationships.length > 0 && <section><h3 className="font-semibold text-slate-950 dark:text-white">{language === 'es' ? 'Conexión con tu perfil' : 'Connection to your profile'}</h3><p className="mt-2 text-slate-600 dark:text-white/70">{language === 'es' ? `Se relaciona con ${selectedRelationships.join(', ')}.` : `Supports ${selectedRelationships.join(', ')}.`}</p></section>}<section><h3 className="font-semibold text-slate-950 dark:text-white">{language === 'es' ? 'Evidencia que podrías crear' : 'Evidence you could create'}</h3><ul className="mt-2 list-disc space-y-1 pl-5 text-slate-600 dark:text-white/70">{selectedProgram.evidenceOutcomes[language].map((outcome) => <li key={outcome}>{outcome}</li>)}</ul></section><section><h3 className="font-semibold text-slate-950 dark:text-white">{language === 'es' ? 'Cómo apoya tu ruta' : 'How this supports your path'}</h3><p className="mt-2 text-slate-600 dark:text-white/70">{selectedProgram.pathContribution[language]}</p></section></div>
            <div className="mt-6 flex flex-col gap-3 border-t border-slate-200 pt-5 dark:border-white/10 sm:flex-row sm:items-center"><button type="button" onClick={() => setAddedProgramIds((current) => new Set(current).add(selectedProgram.id))} className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700">{addedProgramIds.has(selectedProgram.id) ? (language === 'es' ? 'Agregado a mi ruta de crecimiento' : 'Added to my growth path') : (language === 'es' ? 'Agregar a mi ruta de crecimiento' : 'Add to my growth path')}</button><Link to={selectedProgram.type === 'Learning Lab' ? '/learning-labs' : '/available-courses'} className="text-center text-sm font-semibold text-blue-600 dark:text-blue-300">{selectedProgram.type === 'Learning Lab' ? (language === 'es' ? 'Explorar Learning Labs' : 'Explore Learning Labs') : (language === 'es' ? 'Explorar cursos disponibles' : 'Explore available courses')}</Link><span className="text-xs text-slate-400">{language === 'es' ? 'Acción de demostración. Los cambios no se guardan.' : 'Demo action. Changes are not saved.'}</span></div>
          </div>
        </div>
      )}
      {showEngramVideo && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 p-3 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="engram-video-dialog-title"
          onClick={() => setShowEngramVideo(false)}
        >
          <div className="w-full max-w-3xl rounded-2xl bg-slate-950 p-4 shadow-2xl" onClick={(event) => event.stopPropagation()}>
            <div className="mb-3 flex items-center justify-between">
              <h2 id="engram-video-dialog-title" className="text-sm font-semibold text-white">
                {language === 'es' ? '¿Qué es un Engram?' : 'What is an Engram?'}
              </h2>
              <button
                autoFocus
                type="button"
                onClick={() => setShowEngramVideo(false)}
                className="text-sm font-semibold text-white/70 hover:text-white"
              >
                {language === 'es' ? 'Cerrar' : 'Close'}
              </button>
            </div>
            <video src="/EngramFinal.mp4" controls autoPlay className="max-h-[75vh] w-full rounded-xl bg-black" />
          </div>
        </div>
      )}
    </div>
  );
}
