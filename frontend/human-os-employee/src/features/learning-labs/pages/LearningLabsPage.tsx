import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router';
import {
  FlaskConical,
  Search,
  CheckCircle2,
  UserPlus,
  AlertCircle,
  Loader2,
  PlayCircle,
  History,
  Award,
  LayoutGrid,
  Trash2,
  X,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import {
  listPublishedLabs,
  listMyEnrollments,
  enrollInLab,
  unenrollFromLab,
  listMyAttempts,
  deleteAttempt,
  type LabListItem,
  type AttemptHistoryItem,
} from '../api/learningLabsApi';

type LabsTab = 'catalog' | 'history';

const ACCENT_GRADIENTS = [
  'from-violet-600 via-indigo-600 to-purple-700',
  'from-blue-600 via-sky-600 to-cyan-700',
  'from-emerald-600 via-teal-600 to-cyan-700',
  'from-amber-600 via-orange-600 to-rose-700',
];

function accentFor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return ACCENT_GRADIENTS[hash % ACCENT_GRADIENTS.length];
}

/** "Learning Labs" (2026-09-14) — read-only catalog of published Simulation
 * Labs (see http://localhost:3010/labs in Human OS Studio), mirroring the
 * Programs catalog's card layout. Enrollment is persisted server-side
 * (LAB_Enrollment table) so it survives reloads — the employee subscribes
 * here and comes back later to actually run the Lab, once that flow exists. */
export function LearningLabsPage() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const orgTenantId = user?.tenantId ?? '';
  const navigate = useNavigate();

  const [labs, setLabs] = useState<LabListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [enrolledIds, setEnrolledIds] = useState<Set<string>>(new Set());
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [attempts, setAttempts] = useState<AttemptHistoryItem[]>([]);
  const [activeTab, setActiveTab] = useState<LabsTab>('catalog');
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [attemptPendingDelete, setAttemptPendingDelete] = useState<AttemptHistoryItem | null>(null);

  useEffect(() => {
    if (!orgTenantId) return;
    listMyAttempts(orgTenantId)
      .then((items) => setAttempts(items.sort((a, b) => b.FechaInicio.localeCompare(a.FechaInicio))))
      .catch(() => undefined);
  }, [orgTenantId]);

  useEffect(() => {
    if (!orgTenantId) return;
    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([
      listPublishedLabs(orgTenantId),
      listMyEnrollments(orgTenantId).catch(() => []),
    ])
      .then(([labsData, enrollments]) => {
        if (cancelled) return;
        setLabs(labsData);
        setEnrolledIds(new Set(enrollments.map((e) => e.IdLab)));
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [orgTenantId]);

  async function handleToggleEnroll(idLab: string) {
    if (pendingId || !orgTenantId) return;
    setPendingId(idLab);
    try {
      if (enrolledIds.has(idLab)) {
        await unenrollFromLab(idLab, orgTenantId);
        setEnrolledIds((prev) => {
          const next = new Set(prev);
          next.delete(idLab);
          return next;
        });
      } else {
        await enrollInLab(idLab, orgTenantId);
        setEnrolledIds((prev) => new Set(prev).add(idLab));
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setPendingId(null);
    }
  }

  async function handleDeleteAttempt(idAttempt: string) {
    if (deletingId || !orgTenantId) return;
    setDeletingId(idAttempt);
    try {
      await deleteAttempt(idAttempt, orgTenantId);
      setAttempts((prev) => prev.filter((a) => a.IdAttempt !== idAttempt));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setDeletingId(null);
      setAttemptPendingDelete(null);
    }
  }

  const filteredLabs = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    if (!q) return labs;
    return labs.filter(
      (lab) => lab.Nombre.toLowerCase().includes(q) || lab.Descripcion.toLowerCase().includes(q),
    );
  }, [labs, searchQuery]);

  const attemptsByLab = useMemo(() => {
    const map = new Map<string, AttemptHistoryItem[]>();
    for (const a of attempts) {
      if (!a.IdLab) continue;
      const list = map.get(a.IdLab) ?? [];
      list.push(a);
      map.set(a.IdLab, list);
    }
    return map;
  }, [attempts]);

  return (
    <div className="mx-auto max-w-6xl space-y-8 p-4 sm:p-8">
      <div className="flex items-center gap-3.5">
        <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-teal-600 to-cyan-600 text-white shadow-lg shadow-teal-500/25">
          <FlaskConical className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-slate-900 dark:text-white">
            {isEn ? 'Learning Labs' : 'Labs de Aprendizaje'}
          </h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {isEn
              ? 'Practice with realistic, role-based simulations and AI actors.'
              : 'Practica con simulaciones profesionales realistas y actores de IA.'}
          </p>
        </div>
      </div>

      <div className="flex items-center gap-1 rounded-xl border border-slate-200 bg-white p-1 dark:border-white/10 dark:bg-white/[0.03] sm:w-fit">
        <button
          type="button"
          onClick={() => setActiveTab('catalog')}
          className={`inline-flex items-center gap-1.5 rounded-lg px-4 py-2 text-sm font-medium transition ${
            activeTab === 'catalog'
              ? 'bg-teal-600 text-white shadow-sm'
              : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-white/[0.06]'
          }`}
        >
          <LayoutGrid className="h-4 w-4" />
          {isEn ? 'Catalog' : 'Catálogo'}
          <span
            className={`rounded-full px-1.5 py-0.5 text-[11px] font-semibold ${
              activeTab === 'catalog' ? 'bg-white/20' : 'bg-slate-200 dark:bg-white/10'
            }`}
          >
            {labs.length}
          </span>
        </button>
        <button
          type="button"
          onClick={() => setActiveTab('history')}
          className={`inline-flex items-center gap-1.5 rounded-lg px-4 py-2 text-sm font-medium transition ${
            activeTab === 'history'
              ? 'bg-teal-600 text-white shadow-sm'
              : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-white/[0.06]'
          }`}
        >
          <History className="h-4 w-4" />
          {isEn ? 'My Attempts' : 'Mis Intentos'}
          <span
            className={`rounded-full px-1.5 py-0.5 text-[11px] font-semibold ${
              activeTab === 'history' ? 'bg-white/20' : 'bg-slate-200 dark:bg-white/10'
            }`}
          >
            {attempts.length}
          </span>
        </button>
      </div>

      {activeTab === 'catalog' && (
        <div className="relative max-w-md">
          <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          <input
            type="search"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={isEn ? 'Search labs...' : 'Buscar labs...'}
            className="w-full rounded-xl border border-slate-200 bg-white pl-10 pr-4 py-2 text-sm text-slate-900 placeholder:text-slate-400 focus:border-teal-500 focus:outline-none focus:ring-2 focus:ring-teal-500/20 dark:border-white/10 dark:bg-white/[0.04] dark:text-white"
          />
        </div>
      )}

      {activeTab === 'catalog' && loading && (
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <div
              key={i}
              className="h-56 animate-pulse rounded-2xl border border-slate-200 bg-slate-100 dark:border-white/10 dark:bg-white/[0.04]"
            />
          ))}
        </div>
      )}

      {activeTab === 'catalog' && error && !loading && (
        <div className="flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
          <AlertCircle className="h-4 w-4 flex-none" />
          {error}
        </div>
      )}

      {activeTab === 'catalog' && !loading && !error && filteredLabs.length === 0 && (
        <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-10 text-center dark:border-white/15 dark:bg-white/[0.02]">
          <FlaskConical className="mx-auto h-8 w-8 text-teal-500" />
          <p className="mt-3 font-medium text-slate-700 dark:text-white">
            {isEn ? 'No labs available yet' : 'Todavía no hay labs disponibles'}
          </p>
        </div>
      )}

      {activeTab === 'catalog' && !loading && !error && filteredLabs.length > 0 && (
        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {filteredLabs.map((lab) => {
            const accent = accentFor(lab.IdLab);
            const isEnrolled = enrolledIds.has(lab.IdLab);
            const labAttempts = attemptsByLab.get(lab.IdLab) ?? [];
            const bestScore = labAttempts.reduce<number | null>(
              (best, a) => (a.ScoreFinal != null && (best == null || a.ScoreFinal > best) ? a.ScoreFinal : best),
              null,
            );

            return (
              <div
                key={lab.IdLab}
                className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm transition hover:-translate-y-0.5 hover:shadow-xl dark:border-white/10 dark:bg-white/[0.03]"
              >
                <div className={`relative h-20 bg-gradient-to-br ${accent} p-4`}>
                  <div className="absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(255,255,255,0.25),transparent_60%)]" />
                  <span className="relative inline-flex items-center gap-1.5 rounded-full bg-black/25 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-wide text-white backdrop-blur-sm">
                    {lab.Tipo}
                  </span>
                </div>

                <div className="p-5">
                  <h2 className="line-clamp-1 text-base font-bold text-slate-900 dark:text-white">{lab.Nombre}</h2>
                  <p className="mt-1 line-clamp-2 text-sm text-slate-500 dark:text-slate-400">{lab.Descripcion}</p>
                  <div className="mt-2 flex items-center justify-between">
                    <p className="text-xs font-medium uppercase tracking-wide text-slate-400">{lab.Dominio}</p>
                    {labAttempts.length > 0 && (
                      <button
                        type="button"
                        onClick={() => setActiveTab('history')}
                        className="flex items-center gap-1 text-xs font-medium text-teal-600 hover:underline dark:text-teal-300"
                      >
                        <History className="h-3 w-3" />
                        {labAttempts.length} {isEn ? 'attempts' : 'intentos'}
                        {bestScore != null && ` · ${bestScore}/10`}
                      </button>
                    )}
                  </div>

                  <div className="mt-4 flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => handleToggleEnroll(lab.IdLab)}
                      disabled={pendingId === lab.IdLab}
                      className={`inline-flex flex-1 items-center justify-center gap-1.5 rounded-lg px-3 py-2 text-sm font-medium transition disabled:opacity-60 ${
                        isEnrolled
                          ? 'bg-emerald-50 text-emerald-700 hover:bg-red-50 hover:text-red-700 dark:bg-emerald-500/10 dark:text-emerald-300 dark:hover:bg-red-500/10 dark:hover:text-red-300'
                          : 'bg-slate-900 text-white hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90'
                      }`}
                    >
                      {pendingId === lab.IdLab ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : isEnrolled ? (
                        <>
                          <CheckCircle2 className="h-4 w-4" />
                          {isEn ? 'Enrolled' : 'Inscrito'}
                        </>
                      ) : (
                        <>
                          <UserPlus className="h-4 w-4" />
                          {isEn ? 'Enroll' : 'Inscribirme'}
                        </>
                      )}
                    </button>
                    {isEnrolled && (
                      <button
                        type="button"
                        onClick={() => navigate(`/learning-labs/${lab.IdLab}/attempts`)}
                        className="inline-flex flex-1 items-center justify-center gap-1.5 rounded-lg bg-teal-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-teal-500"
                      >
                        <PlayCircle className="h-4 w-4" />
                        {isEn ? 'Start' : 'Iniciar'}
                      </button>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {activeTab === 'history' && attempts.length === 0 && (
        <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50 p-10 text-center dark:border-white/15 dark:bg-white/[0.02]">
          <History className="mx-auto h-8 w-8 text-teal-500" />
          <p className="mt-3 font-medium text-slate-700 dark:text-white">
            {isEn ? "You haven't started any labs yet" : 'Aún no has iniciado ningún lab'}
          </p>
        </div>
      )}

      {activeTab === 'history' && attempts.length > 0 && (
        <div className="space-y-3">
          <h2 className="flex items-center gap-2 text-lg font-semibold text-slate-900 dark:text-white">
            <History className="h-5 w-5 text-teal-600 dark:text-teal-300" />
            {isEn ? 'My Attempts' : 'Mis Intentos'}
          </h2>          <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-white/[0.03]">
            <ul className="divide-y divide-slate-200 dark:divide-white/10">
              {attempts.map((a) => (
                <li key={a.IdAttempt} className="flex items-center">
                  <button
                    type="button"
                    onClick={() => navigate(`/learning-labs/attempts/${a.IdAttempt}`)}
                    className="flex flex-1 items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-slate-50 dark:hover:bg-white/[0.04]"
                  >
                    <div>
                      <p className="text-sm font-medium text-slate-900 dark:text-white">
                        {a.LabNombre ?? (isEn ? 'Attempt' : 'Intento')}
                      </p>
                      <p className="text-xs text-slate-500 dark:text-slate-400">
                        {isEn ? 'Attempt' : 'Intento'} #{a.NumeroIntento}
                        {a.ScenarioNombre ? ` · ${a.ScenarioNombre}` : ''} ·{' '}
                        {new Date(a.FechaInicio).toLocaleString(isEn ? 'en-US' : 'es-MX')}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      {a.ScoreFinal != null && (
                        <span className="flex items-center gap-1 text-sm font-semibold text-teal-600 dark:text-teal-300">
                          <Award className="h-4 w-4" /> {a.ScoreFinal}/10
                        </span>
                      )}
                      <span
                        className={`rounded-full px-2.5 py-1 text-xs font-semibold uppercase tracking-wide ${
                          a.Estatus === 'COMPLETED'
                            ? 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-300'
                            : a.Estatus === 'IN_PROGRESS'
                            ? 'bg-amber-500/15 text-amber-600 dark:text-amber-300'
                            : 'bg-slate-200 text-slate-500 dark:bg-white/[0.06] dark:text-slate-400'
                        }`}
                      >
                        {a.Resultado ?? a.Estatus}
                      </span>
                    </div>
                  </button>
                  <button
                    type="button"
                    onClick={() => setAttemptPendingDelete(a)}
                    disabled={deletingId === a.IdAttempt}
                    title={isEn ? 'Delete attempt' : 'Eliminar intento'}
                    className="mr-3 inline-flex items-center justify-center rounded-lg p-2 text-slate-400 transition hover:bg-red-50 hover:text-red-600 disabled:opacity-60 dark:hover:bg-red-500/10 dark:hover:text-red-400"
                  >
                    {deletingId === a.IdAttempt ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Trash2 className="h-4 w-4" />
                    )}
                  </button>
                </li>
              ))}
            </ul>
          </div>
        </div>
      )}

      {attemptPendingDelete && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 p-4 backdrop-blur-sm"
          onClick={() => (deletingId ? undefined : setAttemptPendingDelete(null))}
        >
          <div
            role="dialog"
            aria-modal="true"
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-slate-900"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-red-50 text-red-600 dark:bg-red-500/10 dark:text-red-400">
                <Trash2 className="h-5 w-5" />
              </div>
              <button
                type="button"
                onClick={() => setAttemptPendingDelete(null)}
                disabled={!!deletingId}
                className="rounded-lg p-1.5 text-slate-400 transition hover:bg-slate-100 hover:text-slate-600 disabled:opacity-50 dark:hover:bg-white/10 dark:hover:text-slate-200"
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <h3 className="mt-4 text-base font-semibold text-slate-900 dark:text-white">
              {isEn ? 'Delete this attempt?' : '¿Eliminar este intento?'}
            </h3>
            <p className="mt-1.5 text-sm text-slate-500 dark:text-slate-400">
              {isEn
                ? 'This permanently removes the attempt and every recorded conversation turn, action, artifact, and evaluation for it. This cannot be undone.'
                : 'Esto elimina permanentemente el intento y todos sus turnos de conversación, acciones, artefactos y evaluación registrados. Esta acción no se puede deshacer.'}
            </p>
            <div className="mt-2 rounded-lg bg-slate-50 px-3 py-2 text-sm font-medium text-slate-700 dark:bg-white/[0.04] dark:text-slate-200">
              {attemptPendingDelete.LabNombre ?? (isEn ? 'Attempt' : 'Intento')} · {isEn ? 'Attempt' : 'Intento'} #
              {attemptPendingDelete.NumeroIntento}
            </div>

            <div className="mt-6 flex items-center justify-end gap-2">
              <button
                type="button"
                onClick={() => setAttemptPendingDelete(null)}
                disabled={!!deletingId}
                className="rounded-lg px-4 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-100 disabled:opacity-50 dark:text-slate-300 dark:hover:bg-white/10"
              >
                {isEn ? 'Cancel' : 'Cancelar'}
              </button>
              <button
                type="button"
                onClick={() => handleDeleteAttempt(attemptPendingDelete.IdAttempt)}
                disabled={!!deletingId}
                className="inline-flex items-center gap-1.5 rounded-lg bg-red-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-red-500 disabled:opacity-60"
              >
                {deletingId === attemptPendingDelete.IdAttempt ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Trash2 className="h-4 w-4" />
                )}
                {isEn ? 'Delete' : 'Eliminar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
