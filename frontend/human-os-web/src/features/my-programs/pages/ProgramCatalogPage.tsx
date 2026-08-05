import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router';
import {
  CheckCircle2,
  GraduationCap,
  Info,
  Layers,
  Search,
  UserPlus,
  X,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { getPrograms, getProgramLogoUrl, type ProgramCatalogEntry } from '../api/programsCatalogApi';
import { getEnrolledPrograms, enrollProgram, unenrollProgram } from '../api/enrolledProgramsApi';

/** Deterministic color accent per program (same technique as the
 * Capacidades catalog's accentFor) — no design-time color field on the
 * backend yet, so the same program always gets the same gradient. */
const ACCENT_GRADIENTS = [
  'from-brand-500 via-indigo-500 to-violet-500',
  'from-amber-400 via-orange-500 to-rose-500',
  'from-emerald-400 via-teal-500 to-cyan-500',
  'from-fuchsia-500 via-pink-500 to-rose-500',
  'from-sky-500 to-cyan-600',
];

function accentFor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return ACCENT_GRADIENTS[hash % ACCENT_GRADIENTS.length];
}

function ProgramLogo({ program }: { program: ProgramCatalogEntry }) {
  const [failed, setFailed] = useState(false);
  const accent = accentFor(program.programId);
  const showPlaceholder = !program.hasLogo || failed;

  return (
    <div className={`relative h-24 w-full overflow-hidden bg-gradient-to-br ${accent}`}>
      <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.25),transparent_60%)]" />
      {!showPlaceholder && (
        <img
          src={getProgramLogoUrl(program.programId)}
          alt={program.name}
          className="h-full w-full object-cover"
          onError={() => setFailed(true)}
        />
      )}
      {showPlaceholder && (
        <div className="relative flex h-full w-full items-center justify-center">
          <GraduationCap className="h-10 w-10 text-white/85" strokeWidth={1.5} />
        </div>
      )}
      <div className="absolute bottom-2 right-2 flex items-center gap-1 rounded-full bg-black/55 px-2.5 py-1 text-xs font-semibold text-white backdrop-blur-sm">
        <Layers className="h-3.5 w-3.5" />
        {program.capabilityCount} curso{program.capabilityCount === 1 ? '' : 's'}
      </div>
    </div>
  );
}

function DetailModal({ program, onClose }: { program: ProgramCatalogEntry; onClose: () => void }) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      onClick={onClose}
    >
      <div
        className="max-h-[80vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl shadow-black/40 dark:border-white/10 dark:bg-slate-950"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{program.name}</h2>
          <button
            onClick={onClose}
            className="rounded-full p-1 text-slate-500 hover:bg-slate-100 hover:text-slate-900 dark:text-slate-400 dark:hover:bg-white/10 dark:hover:text-white"
            aria-label="Cerrar"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="mt-4 space-y-4">
          {program.description ? (
            <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">{program.description}</p>
          ) : (
            <p className="text-sm italic text-slate-500">Este programa todavía no tiene una descripción.</p>
          )}

          {program.objectives && (
            <div>
              <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Objetivos</p>
              <p className="text-sm text-slate-600 dark:text-slate-300">{program.objectives}</p>
            </div>
          )}

          {program.requirements && (
            <div>
              <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Requisitos</p>
              <p className="text-sm text-slate-600 dark:text-slate-300">{program.requirements}</p>
            </div>
          )}

          <div className="flex items-center gap-1.5 pt-1 text-xs font-medium text-slate-500">
            <Layers className="h-3.5 w-3.5" />
            {program.capabilityCount} curso{program.capabilityCount === 1 ? '' : 's'} en este programa
          </div>
        </div>
      </div>
    </div>
  );
}

/** "Programas" catalog (2026-07-25) — browse EVERY active Program and
 * "Inscribirme" directly, mirroring the Capacidades catalog's enroll flow
 * (SubjectCapabilitiesPage.tsx). Deliberately separate from "Mis
 * Programas" (/programs, MyProgramsPage.tsx), which only ever lists
 * programs the person is already enrolled in — this page is the new,
 * easier entry point INTO that list. Enrolling here never starts/opens a
 * program by itself; the person still goes to "Mis Programas" to actually
 * begin working through it, same rule as Capacidades. */
export default function ProgramCatalogPage() {
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const [programs, setPrograms] = useState<ProgramCatalogEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [detailFor, setDetailFor] = useState<ProgramCatalogEntry | null>(null);
  const [enrolledProgramIds, setEnrolledProgramIds] = useState<Set<string>>(new Set());
  const [enrollingId, setEnrollingId] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    getPrograms()
      .then(setPrograms)
      .catch((err) => setError(err instanceof Error ? err.message : 'Error al cargar los programas.'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!personId) return;
    getEnrolledPrograms(personId)
      .then((enrolled) => setEnrolledProgramIds(new Set(enrolled.map((p) => p.programId))))
      .catch(() => setEnrolledProgramIds(new Set()));
  }, [personId]);

  async function handleToggleEnroll(program: ProgramCatalogEntry) {
    if (!personId || enrollingId) return;
    const isEnrolled = enrolledProgramIds.has(program.programId);
    setEnrollingId(program.programId);
    try {
      if (isEnrolled) {
        await unenrollProgram(personId, program.programId);
        setEnrolledProgramIds((prev) => {
          const next = new Set(prev);
          next.delete(program.programId);
          return next;
        });
      } else {
        await enrollProgram(personId, program.programId);
        setEnrolledProgramIds((prev) => new Set(prev).add(program.programId));
      }
    } catch {
      // Silently ignore — the button just stays in its previous state.
    } finally {
      setEnrollingId(null);
    }
  }

  const filteredPrograms = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    if (!query) return programs;
    return programs.filter(
      (program) =>
        program.name.toLowerCase().includes(query) || (program.description ?? '').toLowerCase().includes(query),
    );
  }, [programs, searchQuery]);

  return (
    <div className="mx-auto max-w-5xl p-4 sm:p-8">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-brand-500 to-accent-500 text-white shadow-lg shadow-brand-500/30">
          <GraduationCap className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">Programas</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Explora todos los programas disponibles e inscríbete al que quieras seguir.
          </p>
        </div>
      </div>

      <div className="relative mt-4">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
        <input
          type="search"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="Buscar programas..."
          className="input pl-9"
        />
      </div>

      {loading && (
        <div className="mt-8 grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
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

      {!loading && !error && programs.length === 0 && (
        <p className="mb-6 mt-4 text-sm text-slate-500">Todavía no hay programas publicados.</p>
      )}
      {!loading && !error && programs.length > 0 && filteredPrograms.length === 0 && (
        <p className="mb-6 mt-4 text-sm text-slate-500">Ningún programa coincide con tu búsqueda.</p>
      )}

      <div className="mt-6 grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {filteredPrograms.map((program) => {
          const isEnrolled = enrolledProgramIds.has(program.programId);

          return (
            <div
              key={program.programId}
              className="group flex flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white transition-all hover:-translate-y-1 hover:border-slate-300 hover:bg-slate-100 hover:shadow-2xl hover:shadow-brand-500/10 dark:border-white/10 dark:bg-white/[0.03] dark:hover:border-white/20 dark:hover:bg-white/[0.06]"
            >
              <ProgramLogo program={program} />

              <div className="flex flex-1 flex-col gap-2 p-4">
                <h3 className="font-semibold text-slate-900 dark:text-white">{program.name}</h3>
                <p className="line-clamp-3 text-sm text-slate-500 dark:text-slate-400">
                  {program.description ?? 'Un programa curado de Engramas para avanzar paso a paso.'}
                </p>

                <div className="mt-auto flex flex-wrap items-center gap-2 pt-3">
                  <button
                    onClick={() => setDetailFor(program)}
                    className="flex items-center gap-1.5 rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-medium text-slate-600 transition hover:border-slate-300 hover:bg-slate-100 hover:text-slate-900 dark:border-white/10 dark:text-slate-300 dark:hover:border-white/20 dark:hover:bg-white/5 dark:hover:text-white"
                  >
                    <Info className="h-3.5 w-3.5" />
                    Ver detalle
                  </button>

                  {personId && (
                    <button
                      onClick={() => handleToggleEnroll(program)}
                      disabled={enrollingId === program.programId}
                      title="Inscribirte no inicia el programa — sólo lo agrega a 'Mis Programas' para seguir tu progreso."
                      className={`flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs font-medium transition disabled:opacity-60 ${
                        isEnrolled
                          ? 'border-emerald-300 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:border-emerald-500/30 dark:bg-emerald-500/10 dark:text-emerald-300'
                          : 'border-slate-200 text-slate-600 hover:border-slate-300 hover:bg-slate-100 hover:text-slate-900 dark:border-white/10 dark:text-slate-300 dark:hover:border-white/20 dark:hover:bg-white/5 dark:hover:text-white'
                      }`}
                    >
                      {isEnrolled ? (
                        <>
                          <CheckCircle2 className="h-3.5 w-3.5" />
                          Inscrito
                        </>
                      ) : (
                        <>
                          <UserPlus className="h-3.5 w-3.5" />
                          Inscribirme
                        </>
                      )}
                    </button>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {enrolledProgramIds.size > 0 && (
        <div className="mt-6 flex justify-center">
          <Link
            to="/programs"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-600 hover:gap-2 dark:text-brand-400"
          >
            <GraduationCap className="h-4 w-4" />
            Ir a Mis Programas
          </Link>
        </div>
      )}

      {detailFor && <DetailModal program={detailFor} onClose={() => setDetailFor(null)} />}
    </div>
  );
}
