import { useQuery } from '@tanstack/react-query';
import {
  Award,
  BookOpen,
  BookText,
  Briefcase,
  Compass,
  Gem,
  GraduationCap,
  Heart,
  Lightbulb,
  Mountain,
  ShieldAlert,
  Sparkles,
  Target,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { getMyProfile, getProfilePhotoUrl } from '@/features/profile/api/personProfileApi';
import { getMyResume } from '@/features/profile/api/resumeApi';
import { getPersonAcademicPreferences } from '@/features/profile/api/personAcademicPreferencesApi';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import { getGamificationProfile } from '@/features/gamification/api/gamificationApi';
import { getThesisStatus } from '@/features/tec-contigo/thesisApi';

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
}

function ChipList({ items }: { items: string[] }) {
  if (items.length === 0) {
    return <p className="text-xs text-slate-400 dark:text-white/40">Sin datos capturados.</p>;
  }
  return (
    <div className="flex flex-wrap gap-1.5">
      {items.map((item) => (
        <span
          key={item}
          className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-600 dark:bg-white/10 dark:text-white/70"
        >
          {item}
        </span>
      ))}
    </div>
  );
}

/** "Insight Persona" — a single-glance persona sheet for the signed-in
 *  employee/student, combining Mi Perfil, academic preferences ("Intereses"),
 *  enrolled courses, gamification/grades, professional objective, and
 *  thesis status. Visually modeled after the HR console's insight-persona
 *  card deck the user shared, but read-only and self-scoped here (no edit
 *  UI — that already lives in Mi Perfil / Intereses / Tec Contigo). */
export function InsightPersonaPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const profileQuery = useQuery({
    queryKey: ['my-profile', personId],
    queryFn: () => getMyProfile(personId),
    enabled: Boolean(personId),
  });
  const resumeQuery = useQuery({
    queryKey: ['my-resume', personId],
    queryFn: () => getMyResume(personId),
    enabled: Boolean(personId),
  });
  const preferencesQuery = useQuery({
    queryKey: ['person-academic-preferences', personId],
    queryFn: () => getPersonAcademicPreferences(personId),
    enabled: Boolean(personId),
  });
  const enrolledQuery = useQuery({
    queryKey: ['enrolled-structured-learning', personId],
    queryFn: () => getEnrolledStructuredLearningDocuments(personId),
    enabled: Boolean(personId),
  });
  const gamificationQuery = useQuery({
    queryKey: ['gamification-profile'],
    queryFn: () => getGamificationProfile(),
    enabled: Boolean(personId),
  });
  const thesisQuery = useQuery({
    queryKey: ['thesis-status', personId],
    queryFn: () => getThesisStatus(personId),
    enabled: Boolean(personId),
  });

  const isLoading = profileQuery.isLoading || resumeQuery.isLoading;
  const profile = profileQuery.data;
  const resume = resumeQuery.data;
  const preferences = preferencesQuery.data;
  const enrolled = enrolledQuery.data ?? [];
  const gamification = gamificationQuery.data;
  const thesis = thesisQuery.data;
  const academic = resume?.studentAcademicProfile;

  const displayName = profile?.DisplayName || user?.name || 'Estudiante';
  const roleLabel = profile?.Occupation || academic?.degree || 'Rol no capturado';
  const completedCourses = enrolled.filter((doc) => (doc.completedSectionCount ?? 0) >= doc.sectionCount && doc.sectionCount > 0);

  return (
    <div className="mx-auto max-w-6xl space-y-6 pb-16">
      {isLoading && <LoadingBackdrop />}

      {/* Hero — big portrait filling most of the blue panel, compact info on the right. */}
      <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-slate-950 via-blue-950 to-indigo-900 text-white shadow-xl shadow-blue-950/20">
        <div className="grid gap-6 p-6 sm:p-8 lg:grid-cols-[minmax(0,360px)_1fr]">
          <img
            src={profile?.ProfilePhotoUrl ? getProfilePhotoUrl(personId) : '/SofiaPersona.png'}
            alt={displayName}
            className="h-64 w-full rounded-2xl object-cover ring-2 ring-white/20 lg:h-full"
          />
          <div className="flex flex-col justify-center gap-4">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-200">Insight Persona</p>
              <h1 className="mt-1 text-3xl font-bold tracking-tight sm:text-4xl">{displayName}</h1>
              <p className="mt-1 text-sm text-blue-100">{roleLabel}</p>
              {user?.email && <p className="mt-0.5 text-[11px] text-blue-300/80">{user.email}</p>}
            </div>
            <div className="flex flex-col gap-3 rounded-2xl bg-white/10 p-5 ring-1 ring-white/15">
              <p className="text-sm leading-6 text-blue-50">
                {profile?.Biography ||
                  resume?.professionalSummary ||
                  'Aún no hay biografía capturada. Se puede completar desde Mi Perfil.'}
              </p>
              <div className="flex flex-wrap gap-4 text-xs text-blue-100">
                {profile?.CountryCode && <span>País: {profile.CountryCode}</span>}
                {academic?.institution && <span>Institución: {academic.institution}</span>}
                {academic?.academicStage && <span>Etapa: {academic.academicStage}</span>}
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {/* Preferencias e intereses académicos */}
        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-100 dark:bg-blue-500/15">
              <Compass className="h-5 w-5 text-blue-600 dark:text-blue-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Intereses académicos</h2>
          </div>
          <ChipList items={preferences?.AcademicInterests ?? []} />
        </Card>

        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-rose-100 dark:bg-rose-500/15">
              <Heart className="h-5 w-5 text-rose-600 dark:text-rose-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Estilos de aprendizaje preferidos</h2>
          </div>
          <ChipList items={preferences?.LearningPreferences ?? []} />
        </Card>

        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100 dark:bg-amber-500/15">
              <Lightbulb className="h-5 w-5 text-amber-600 dark:text-amber-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Motivaciones</h2>
          </div>
          <ChipList items={preferences?.AcademicMotivations ?? []} />
        </Card>

        <Card className="space-y-3 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-red-100 dark:bg-red-500/15">
              <ShieldAlert className="h-5 w-5 text-red-600 dark:text-red-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Mis frustraciones</h2>
          </div>
          <p className="text-xs text-slate-400 dark:text-white/40">
            Lo que le genera desmotivación, bloqueos o incomodidad en su proceso de aprendizaje.
          </p>
          <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
            {preferences?.Frustrations || 'Sin datos capturados.'}
          </p>
        </Card>

        <Card className="space-y-3 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-orange-100 dark:bg-orange-500/15">
              <Mountain className="h-5 w-5 text-orange-600 dark:text-orange-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Mis principales retos</h2>
          </div>
          <p className="text-xs text-slate-400 dark:text-white/40">
            Los obstáculos concretos que enfrenta hoy y que quiere superar.
          </p>
          <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
            {preferences?.MainChallenges || 'Sin datos capturados.'}
          </p>
        </Card>

        <Card className="space-y-3 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-cyan-100 dark:bg-cyan-500/15">
              <Gem className="h-5 w-5 text-cyan-600 dark:text-cyan-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Mis valores importantes</h2>
          </div>
          <p className="text-xs text-slate-400 dark:text-white/40">
            Los principios y valores que guían sus decisiones académicas y personales.
          </p>
          <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
            {preferences?.ImportantValues || 'Sin datos capturados.'}
          </p>
        </Card>

        {/* Cursos inscritos */}
        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-100 dark:bg-emerald-500/15">
              <BookOpen className="h-5 w-5 text-emerald-600 dark:text-emerald-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Cursos inscritos</h2>
          </div>
          {enrolledQuery.isLoading ? (
            <p className="text-xs text-slate-400 dark:text-white/40">Cargando…</p>
          ) : enrolled.length === 0 ? (
            <p className="text-xs text-slate-400 dark:text-white/40">Sin cursos inscritos.</p>
          ) : (
            <ul className="space-y-1.5">
              {enrolled.slice(0, 6).map((doc) => (
                <li key={doc.structuredLearningDocumentId} className="flex items-center justify-between gap-2 text-sm">
                  <span className="truncate text-slate-700 dark:text-white/80">{doc.documentName}</span>
                  <span className="shrink-0 text-xs font-semibold text-slate-400 dark:text-white/40">
                    {doc.completedSectionCount ?? 0}/{doc.sectionCount}
                  </span>
                </li>
              ))}
            </ul>
          )}
          <p className="text-xs font-medium text-slate-500 dark:text-white/50">
            {completedCourses.length} de {enrolled.length} cursos completados
          </p>
        </Card>

        {/* Card de calificaciones */}
        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-violet-100 dark:bg-violet-500/15">
              <Award className="h-5 w-5 text-violet-600 dark:text-violet-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Calificaciones</h2>
          </div>
          {academic?.cumulativeAverage != null ? (
            <div>
              <p className="text-3xl font-bold text-slate-950 dark:text-white">
                {academic.cumulativeAverage}
                {academic.averageScale && <span className="text-base font-medium text-slate-400"> / {academic.averageScale}</span>}
              </p>
              <p className="text-xs text-slate-500 dark:text-white/50">Promedio académico acumulado</p>
            </div>
          ) : gamification ? (
            <div>
              <p className="text-3xl font-bold text-slate-950 dark:text-white">{gamification.totalPoints} pts</p>
              <p className="text-xs text-slate-500 dark:text-white/50">
                Nivel {gamification.currentLevel} · {gamification.currentLevelName} · {gamification.totalEngramsMastered} engramas dominados
              </p>
            </div>
          ) : (
            <p className="text-xs text-slate-400 dark:text-white/40">Sin calificaciones registradas.</p>
          )}
        </Card>

        {/* Card de objetivo */}
        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-teal-100 dark:bg-teal-500/15">
              <Target className="h-5 w-5 text-teal-600 dark:text-teal-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Mi objetivo</h2>
          </div>
          <p className="text-sm leading-6 text-slate-600 dark:text-white/70">
            {resume?.professionalObjective || 'Aún no hay un objetivo profesional capturado.'}
          </p>
        </Card>

        {/* Card de tesis */}
        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-sky-100 dark:bg-sky-500/15">
              <BookText className="h-5 w-5 text-sky-600 dark:text-sky-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Tesis</h2>
          </div>
          {thesisQuery.isLoading ? (
            <p className="text-xs text-slate-400 dark:text-white/40">Cargando…</p>
          ) : thesis ? (
            <p className="text-sm text-slate-600 dark:text-white/70">
              Documento cargado: <span className="font-semibold text-slate-900 dark:text-white">{thesis.fileName}</span>
            </p>
          ) : (
            <p className="text-xs text-slate-400 dark:text-white/40">Sin tesis registrada.</p>
          )}
        </Card>

        {/* Oportunidades de crecimiento */}
        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-indigo-100 dark:bg-indigo-500/15">
              <Sparkles className="h-5 w-5 text-indigo-600 dark:text-indigo-300" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Oportunidades de crecimiento</h2>
          </div>
          <ChipList items={preferences?.GrowthAreas ?? []} />
        </Card>

        {/* Ocupación / puesto */}
        <Card className="space-y-4 p-6">
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-slate-100 dark:bg-white/10">
              <Briefcase className="h-5 w-5 text-slate-600 dark:text-white/70" />
            </span>
            <h2 className="text-base font-bold text-slate-950 dark:text-white">Ocupación</h2>
          </div>
          <p className="text-sm text-slate-600 dark:text-white/70">{profile?.Occupation || 'No capturada.'}</p>
          {profile?.Company && <p className="text-xs text-slate-400 dark:text-white/40">{profile.Company}</p>}
        </Card>

        {academic && (academic.degree || academic.graduationYear) && (
          <Card className="space-y-4 p-6">
            <div className="flex items-center gap-3">
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-fuchsia-100 dark:bg-fuchsia-500/15">
                <GraduationCap className="h-5 w-5 text-fuchsia-600 dark:text-fuchsia-300" />
              </span>
              <h2 className="text-base font-bold text-slate-950 dark:text-white">Formación</h2>
            </div>
            <p className="text-sm text-slate-600 dark:text-white/70">{academic.degree}</p>
            {academic.graduationYear && (
              <p className="text-xs text-slate-400 dark:text-white/40">Graduación: {academic.graduationYear}</p>
            )}
          </Card>
        )}
      </div>
    </div>
  );
}
