import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import { Layers, CalendarDays, ArrowRight, Sparkles, BookOpen } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import {
  getEnrolledStructuredLearningDocuments,
  type EnrolledStructuredLearningDocument,
} from '@/features/capabilities/api/enrolledStructuredLearningApi';
import { API_BASE_URL } from '@/features/capabilities/api/httpClient';

/** Rotating gradient palette for card headers — same cycling approach as
 * MyProgramsPage.tsx, purely cosmetic. */
const CARD_GRADIENTS = [
  'from-emerald-400 via-teal-500 to-cyan-500',
  'from-brand-500 via-indigo-500 to-violet-500',
  'from-amber-400 via-orange-500 to-rose-500',
  'from-fuchsia-500 via-pink-500 to-rose-500',
];

// Structured Learning's real student-facing course/section UI now lives
// in this app too (2026-08-14) — see StructuredLearningCoursePage.tsx /
// StructuredLearningSectionPage.tsx and their /structured-learning/:id
// route, mirroring the Capability Graph pattern.

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('es-ES', { year: 'numeric', month: 'long', day: 'numeric' });
  } catch {
    return iso;
  }
}

/** "Mis Capacidades" (2026-07-24) — card grid of every standalone
 * Capability (no Program) the student explicitly enrolled in via
 * "Inscribirme" on /capabilities, each with its enrollment date and real
 * progress. Sibling page to "Mis Programas" (MyProgramsPage.tsx). Opening a
 * card goes straight into the Capability's graph map, same as /capabilities. */
type EnrolledCourseCard = {
      kind: 'structured-learning';
      id: string;
      name: string;
      date: string;
      coverIllustrationId?: string | null;
      href: string;
      badge: string;
      gradient: string;
    };

export default function MyCapabilitiesPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const [structuredLearningDocuments, setStructuredLearningDocuments] = useState<EnrolledStructuredLearningDocument[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!personId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    getEnrolledStructuredLearningDocuments(personId)
      .then((enrolledStructuredLearningDocuments) => {
        if (!cancelled) {
          setStructuredLearningDocuments(enrolledStructuredLearningDocuments);
        }
      })
      .catch(() => {
        if (!cancelled) setError('No pudimos cargar tus cursos. Intenta de nuevo en unos minutos.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [personId]);

  const courseCards = useMemo<EnrolledCourseCard[]>(() => {
    const structuredLearningCards: EnrolledCourseCard[] = structuredLearningDocuments.map((document) => ({
      kind: 'structured-learning',
      id: document.structuredLearningDocumentId,
      name: document.documentName,
      date: document.enrolledDate,
      coverIllustrationId: document.coverIllustrationId,
      href: `/structured-learning/${document.structuredLearningDocumentId}`,
      badge: 'Engrama',
      gradient: CARD_GRADIENTS[0],
    }));

    return structuredLearningCards.sort(
      (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime(),
    );
  }, [structuredLearningDocuments]);

  const isEmpty = useMemo(() => !loading && !error && courseCards.length === 0, [loading, error, courseCards]);

  return (
    <div className="mx-auto max-w-5xl p-4 sm:p-8">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-emerald-500 to-teal-500 text-white shadow-lg shadow-emerald-500/30">
          <Layers className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">Mis cursos</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Todos los cursos y Engramas a los que ya te inscribiste, con tu progreso real en cada uno.
          </p>
        </div>
      </div>

      {loading && (
        <div className="mt-10 grid grid-cols-1 gap-6 sm:grid-cols-2">
          {[0, 1].map((i) => (
            <div
              key={i}
              className="h-52 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]"
            />
          ))}
        </div>
      )}

      {error && !loading && (
        <p className="mt-8 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          {error}
        </p>
      )}

      {isEmpty && (
        <div className="mt-10 rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-10 text-center dark:border-white/15 dark:bg-white/[0.02]">
          <BookOpen className="mx-auto h-8 w-8 text-emerald-500" />
          <p className="mt-3 font-medium text-slate-700 dark:text-white">Todavía no te has inscrito en ningún Engrama</p>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Explora el catálogo y dale "Inscribirme" al que quieras seguir de cerca.
          </p>
          <Link
            to="/capabilities"
            className="mt-4 inline-flex items-center gap-1.5 rounded-full bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            Explorar Engramas
            <ArrowRight className="h-3.5 w-3.5" />
          </Link>
        </div>
      )}

      {!loading && !error && courseCards.length > 0 && (
        <div className="mt-8 grid grid-cols-1 gap-6 sm:grid-cols-2">
          {courseCards.map((course) => {
            // Both card kinds now navigate within this app — plain react-router <Link> for all.
            const CardWrapper: any = Link;
            const cardWrapperProps = { to: course.href };

            return (
              <CardWrapper
                key={course.id + ':' + course.kind}
                {...cardWrapperProps}
                className="group overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm transition hover:-translate-y-0.5 hover:shadow-xl dark:border-white/10 dark:bg-white/[0.03]"
              >
                <div className={`relative h-24 overflow-hidden bg-gradient-to-br ${course.gradient} p-5`}>
                  {course.kind === 'structured-learning' && course.coverIllustrationId && (
                    <img
                      src={`${API_BASE_URL}/illustrations/structured-learning/${course.coverIllustrationId}/image`}
                      alt=""
                      className="absolute inset-0 h-full w-full object-cover"
                      onError={(event) => {
                        event.currentTarget.style.display = 'none';
                      }}
                    />
                  )}
                  <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.25),transparent_60%)]" />
                  <div className="relative flex items-center gap-2 text-white/90">
                    <Layers className="h-4 w-4" />
                    <span className="text-xs font-semibold uppercase tracking-wide">
                      {course.badge}
                    </span>
                  </div>
                  <h2 className="relative mt-2 line-clamp-1 text-lg font-bold text-white drop-shadow-sm">
                    {course.name}
                  </h2>
                </div>

                <div className="p-5">
                  <div className="mt-4 flex items-center gap-1.5 text-xs font-medium text-slate-500 dark:text-slate-400">
                    <CalendarDays className="h-3.5 w-3.5" />
                    Inscrito el {formatDate(course.date)}
                  </div>

                  <div className="mt-4 flex items-center gap-1 text-sm font-medium text-brand-600 group-hover:gap-2 dark:text-brand-400">
                    Continuar
                    <ArrowRight className="h-4 w-4 transition-all" />
                  </div>
                </div>
              </CardWrapper>
            );
          })}
        </div>
      )}

      {!loading && !error && courseCards.length > 0 && (
        <div className="mt-6 flex justify-center">
          <Link
            to="/capabilities"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-600 hover:gap-2 dark:text-brand-400"
          >
            <Sparkles className="h-4 w-4" />
            Explorar más cursos
          </Link>
        </div>
      )}
    </div>
  );
}
