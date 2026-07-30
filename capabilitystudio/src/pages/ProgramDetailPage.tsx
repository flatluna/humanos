import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, ArrowUp, ArrowDown, Layers, GitBranch, Trash2, X, GraduationCap, AlertTriangle } from 'lucide-react';
import {
  getProgramById,
  deleteProgram,
  updateProgramCapabilities,
  removeCapabilityFromProgram,
  type BackendProgramDetail,
} from '../lib/api/programsApi';
import { apiImageUrl } from '../lib/api/httpClient';
import LoadingSpinner from '../components/LoadingSpinner';

export default function ProgramDetailPage() {
  const { t } = useTranslation();
  const { programId } = useParams<{ programId: string }>();
  const navigate = useNavigate();
  const [program, setProgram] = useState<BackendProgramDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [reordering, setReordering] = useState(false);
  const [unlinkingId, setUnlinkingId] = useState<string | null>(null);
  const [unlinkTarget, setUnlinkTarget] = useState<{ capabilityId: string; capabilityName: string } | null>(null);

  useEffect(() => {
    if (!programId) return;
    let cancelled = false;
    setLoading(true);
    getProgramById(programId)
      .then((result) => {
        if (!cancelled) setProgram(result);
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
  }, [programId]);

  async function handleDelete() {
    if (!program) return;
    setDeleting(true);
    try {
      await deleteProgram(program.ProgramId);
      navigate('/programs');
    } catch (err) {
      setError(err instanceof Error ? err.message : t('programDetail.deleteErrorFallback'));
      setDeleting(false);
    }
  }

  async function moveCapability(index: number, direction: -1 | 1) {
    if (!program || reordering) return;
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= program.Capabilities.length) return;

    const reordered = [...program.Capabilities];
    [reordered[index], reordered[targetIndex]] = [reordered[targetIndex], reordered[index]];
    const withSortOrders = reordered.map((pc, i) => ({ ...pc, SortOrder: i + 1 }));

    setProgram({ ...program, Capabilities: withSortOrders });
    setReordering(true);
    try {
      await updateProgramCapabilities(
        program.ProgramId,
        withSortOrders.map((pc) => ({
          capabilityId: pc.CapabilityId,
          sortOrder: pc.SortOrder,
          isRequired: pc.IsRequired,
          phaseLabel: pc.PhaseLabel,
        })),
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : t('programDetail.reorderErrorFallback'));
      // Revert to the last known-good order on failure.
      const refreshed = await getProgramById(program.ProgramId).catch(() => null);
      if (refreshed) setProgram(refreshed);
    } finally {
      setReordering(false);
    }
  }

  async function handleUnlink() {
    if (!program || !unlinkTarget) return;
    setUnlinkingId(unlinkTarget.capabilityId);
    try {
      await removeCapabilityFromProgram(unlinkTarget.capabilityId, program.ProgramId);
      setProgram({
        ...program,
        Capabilities: program.Capabilities.filter((pc) => pc.CapabilityId !== unlinkTarget.capabilityId),
      });
      setUnlinkTarget(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('programDetail.unlinkErrorFallback'));
    } finally {
      setUnlinkingId(null);
    }
  }

  if (loading) return <LoadingSpinner label={t('programDetail.loading')} />;

  if (error || !program) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16 text-center">
        <p className="text-red-300">{error ?? t('programDetail.notFound')}</p>
        <Link to="/programs" className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300">
          <ArrowLeft className="h-4 w-4" /> {t('programDetail.backToPrograms')}
        </Link>
      </div>
    );
  }

  return (
    <div>
      <div className="relative h-56 w-full overflow-hidden bg-gradient-to-br from-violet-500/30 to-fuchsia-500/20">
        {program.HasLogo && (
          <img
            src={apiImageUrl(`/programs/${program.ProgramId}/logo`)}
            alt=""
            className="h-full w-full object-cover opacity-80"
          />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black via-black/40 to-transparent" />
        <div className="absolute inset-x-0 bottom-0 mx-auto max-w-7xl px-6 pb-6">
          <Link to="/programs" className="mb-3 inline-flex items-center gap-1.5 text-sm text-[#fff]/80 hover:text-[#fff]">
            <ArrowLeft className="h-4 w-4" /> {t('programDetail.programs')}
          </Link>
          <h1 className="text-3xl font-bold text-[#fff]">{program.Name}</h1>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-6 py-10">
        {program.Description && <p className="max-w-3xl text-lg text-slate-300">{program.Description}</p>}

        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2">
          {program.Objectives && (
            <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
              <h3 className="mb-2 text-sm font-semibold text-white">{t('programDetail.objectives')}</h3>
              <p className="whitespace-pre-line text-sm text-slate-400">{program.Objectives}</p>
            </div>
          )}
          {program.Requirements && (
            <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
              <h3 className="mb-2 text-sm font-semibold text-white">{t('programDetail.requirements')}</h3>
              <p className="whitespace-pre-line text-sm text-slate-400">{program.Requirements}</p>
            </div>
          )}
        </div>

        <div className="mt-8">
          <h3 className="mb-3 text-lg font-semibold text-white">{t('programDetail.sequenceHeading')}</h3>
          {program.Capabilities.length === 0 ? (
            <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-8 text-center text-sm text-slate-500">
              {t('programDetail.emptySequence')}
            </div>
          ) : (
            <div className="space-y-2">
              {program.Capabilities.map((pc, index) => (
                <div
                  key={pc.ProgramCapabilityId}
                  className="flex items-center gap-4 rounded-2xl border border-white/10 bg-white/[0.03] p-4 transition-colors hover:border-white/20 hover:bg-white/[0.06]"
                >
                  <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-500/15 text-sm font-semibold text-brand-300">
                    {pc.SortOrder}
                  </div>
                  <Link to={`/capabilities/${pc.CapabilityId}`} className="min-w-0 flex-1">
                    <div className="truncate font-medium text-white">{pc.CapabilityName}</div>
                    {pc.PhaseLabel && <div className="text-xs text-slate-500">{pc.PhaseLabel}</div>}
                  </Link>
                  <span className="flex items-center gap-1.5 text-xs text-slate-500">
                    <Layers className="h-3.5 w-3.5" />
                    {pc.LevelCount}
                  </span>
                  <span className="flex items-center gap-1.5 text-xs text-slate-500">
                    <GitBranch className="h-3.5 w-3.5" />
                    {pc.NodeCount}
                  </span>
                  <span
                    className={`shrink-0 rounded-full px-2.5 py-1 text-[11px] font-medium ${
                      pc.IsRequired ? 'bg-brand-500/15 text-brand-300' : 'bg-white/5 text-slate-400'
                    }`}
                  >
                    {pc.IsRequired ? t('programDetail.required') : t('programDetail.elective')}
                  </span>
                  <div className="flex shrink-0 flex-col">
                    <button
                      type="button"
                      onClick={() => moveCapability(index, -1)}
                      disabled={index === 0 || reordering}
                      className="rounded p-1 text-slate-400 hover:text-white disabled:opacity-30"
                    >
                      <ArrowUp className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={() => moveCapability(index, 1)}
                      disabled={index === program.Capabilities.length - 1 || reordering}
                      className="rounded p-1 text-slate-400 hover:text-white disabled:opacity-30"
                    >
                      <ArrowDown className="h-3.5 w-3.5" />
                    </button>
                  </div>
                  <button
                    type="button"
                    onClick={() => setUnlinkTarget({ capabilityId: pc.CapabilityId, capabilityName: pc.CapabilityName })}
                    disabled={unlinkingId === pc.CapabilityId}
                    title={t('programDetail.unlinkTitle')}
                    className="shrink-0 rounded-lg p-2 text-slate-500 transition-colors hover:bg-red-500/10 hover:text-red-400 disabled:opacity-30"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="mt-8 flex flex-wrap items-center gap-3">
          <Link
            to="/programs"
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02]"
          >
            <GraduationCap className="h-4 w-4" />
            {t('programDetail.viewAllPrograms')}
          </Link>
          <button
            onClick={() => setShowDeleteConfirm(true)}
            className="ml-auto inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium text-red-400 hover:bg-red-500/10 hover:text-red-300 transition-colors cursor-pointer border border-red-500/30 hover:border-red-500/50"
          >
            <Trash2 className="h-4 w-4" />
            {t('programDetail.delete')}
          </button>
        </div>

        {unlinkTarget && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" role="dialog" aria-modal="true">
            <div className="w-full max-w-lg rounded-2xl border-2 border-red-600 bg-slate-900 p-6 shadow-2xl">
              <div className="mb-4 flex items-start gap-3">
                <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full border border-red-500/30 bg-red-500/10">
                  <AlertTriangle className="text-red-500" size={28} />
                </div>
                <div>
                  <h2 className="text-xl font-bold text-red-500">{t('programDetail.unlinkModalTitle')}</h2>
                  <p className="mt-1 text-sm text-slate-400">
                    {t('programDetail.unlinkModalBody', { name: unlinkTarget.capabilityName, programName: program.Name })}
                  </p>
                </div>
              </div>
              <div className="flex justify-end gap-3">
                <button
                  onClick={() => setUnlinkTarget(null)}
                  disabled={unlinkingId !== null}
                  className="rounded-lg px-4 py-2 font-medium text-slate-300 transition-all hover:bg-white/5 disabled:opacity-50"
                >
                  {t('programDetail.cancel')}
                </button>
                <button
                  onClick={handleUnlink}
                  disabled={unlinkingId !== null}
                  className="rounded-lg bg-red-600 px-4 py-2 font-medium text-white transition-all hover:bg-red-500 disabled:opacity-50"
                >
                  {unlinkingId !== null ? t('programDetail.unlinking') : t('programDetail.unlink')}
                </button>
              </div>
            </div>
          </div>
        )}

        {showDeleteConfirm && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" role="dialog" aria-modal="true">
            <div className="w-full max-w-lg rounded-2xl border-2 border-red-600 bg-slate-900 p-6 shadow-2xl">
              <div className="mb-4 flex items-start gap-3">
                <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full border border-red-500/30 bg-red-500/10">
                  <AlertTriangle className="text-red-500" size={28} />
                </div>
                <div>
                  <h2 className="text-xl font-bold text-red-500">{t('programDetail.deleteModalTitle')}</h2>
                  <p className="mt-1 text-sm text-slate-400">
                    {t('programDetail.deleteModalBody', { name: program.Name })}
                  </p>
                </div>
              </div>
              <div className="flex justify-end gap-3">
                <button
                  onClick={() => setShowDeleteConfirm(false)}
                  disabled={deleting}
                  className="rounded-lg px-4 py-2 font-medium text-slate-300 transition-all hover:bg-white/5 disabled:opacity-50"
                >
                  {t('programDetail.cancel')}
                </button>
                <button
                  onClick={handleDelete}
                  disabled={deleting}
                  className="rounded-lg bg-red-600 px-4 py-2 font-medium text-white transition-all hover:bg-red-500 disabled:opacity-50"
                >
                  {deleting ? t('programDetail.deleting') : t('programDetail.deletePermanently')}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
