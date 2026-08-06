import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, ExternalLink, PlayCircle, Layers, GitBranch, Calendar, Trash2, GraduationCap, X, Baby, Globe } from 'lucide-react';
import {
  getCapabilities,
  deleteCapability,
  updateCapabilityPedagogicalAudience,
  updateCapabilityVisibility,
  type BackendCapability,
} from '../lib/api/capabilitiesApi';
import {
  getPrograms,
  getProgramsForCapability,
  addCapabilityToProgram,
  removeCapabilityFromProgram,
  type BackendProgram,
  type CapabilityProgramMembership,
} from '../lib/api/programsApi';
import { apiImageUrl } from '../lib/api/httpClient';
import LoadingSpinner from '../components/LoadingSpinner';
import DeleteCapabilityModal from '../components/DeleteCapabilityModal';
import { getSubjectGradient, getSubjectIcon } from '../lib/subjectVisuals';

const LEARN_APP_BASE_URL = 'http://localhost:3001';

export default function CapabilityDetailPage() {
  const { t, i18n } = useTranslation();
  const { capabilityId } = useParams<{ capabilityId: string }>();
  const navigate = useNavigate();
  const [capability, setCapability] = useState<BackendCapability | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showDeleteModal, setShowDeleteModal] = useState(false);

  const [memberships, setMemberships] = useState<CapabilityProgramMembership[]>([]);
  const [allPrograms, setAllPrograms] = useState<BackendProgram[]>([]);
  const [programToAdd, setProgramToAdd] = useState('');
  const [linkBusy, setLinkBusy] = useState(false);
  const [linkError, setLinkError] = useState<string | null>(null);
  const [audienceBusy, setAudienceBusy] = useState(false);
  const [audienceError, setAudienceError] = useState<string | null>(null);
  const [visibilityBusy, setVisibilityBusy] = useState(false);
  const [visibilityError, setVisibilityError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getCapabilities()
      .then((all) => {
        if (cancelled) return;
        const match = all.find((c) => c.CapabilityId === capabilityId) ?? null;
        setCapability(match);
        if (!match) setError(t('capabilityDetail.notFound'));
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
  }, [capabilityId]);

  const handleDelete = async () => {
    if (!capability) return;
    await deleteCapability(capability.CapabilityId);
    // Redirect to catalog after successful deletion
    navigate('/');
  };

  useEffect(() => {
    if (!capabilityId) return;
    getProgramsForCapability(capabilityId).then(setMemberships).catch(() => setMemberships([]));
    getPrograms().then(setAllPrograms).catch(() => setAllPrograms([]));
  }, [capabilityId]);

  async function handleAddToProgram() {
    if (!capabilityId || !programToAdd) return;
    setLinkBusy(true);
    setLinkError(null);
    try {
      await addCapabilityToProgram(capabilityId, programToAdd);
      const refreshed = await getProgramsForCapability(capabilityId);
      setMemberships(refreshed);
      setProgramToAdd('');
    } catch (err) {
      setLinkError(err instanceof Error ? err.message : t('capabilityDetail.addToProgramErrorFallback'));
    } finally {
      setLinkBusy(false);
    }
  }

  async function handleRemoveFromProgram(programId: string) {
    if (!capabilityId || linkBusy) return;
    setLinkBusy(true);
    setLinkError(null);
    try {
      await removeCapabilityFromProgram(capabilityId, programId);
      setMemberships((prev) => prev.filter((m) => m.ProgramId !== programId));
    } catch (err) {
      setLinkError(err instanceof Error ? err.message : t('capabilityDetail.removeFromProgramErrorFallback'));
    } finally {
      setLinkBusy(false);
    }
  }

  async function handleSetAudience(next: 'Standard' | 'YoungLearner') {
    if (!capability || audienceBusy) return;
    const current = capability.PedagogicalAudience === 'YoungLearner' ? 'YoungLearner' : 'Standard';
    if (current === next) return;
    setAudienceBusy(true);
    setAudienceError(null);
    try {
      await updateCapabilityPedagogicalAudience(capability.CapabilityId, next === 'YoungLearner' ? 'YoungLearner' : null);
      setCapability({ ...capability, PedagogicalAudience: next === 'YoungLearner' ? 'YoungLearner' : null });
    } catch (err) {
      setAudienceError(err instanceof Error ? err.message : t('capabilityDetail.audienceErrorFallback'));
    } finally {
      setAudienceBusy(false);
    }
  }

  async function handleSetVisibility(nextIsGlobal: boolean) {
    if (!capability || visibilityBusy) return;
    const currentIsGlobal = capability.OwnerPersonId === null;
    if (currentIsGlobal === nextIsGlobal) return;
    setVisibilityBusy(true);
    setVisibilityError(null);
    try {
      await updateCapabilityVisibility(capability.CapabilityId, nextIsGlobal);
      // Re-fetch instead of guessing the new OwnerPersonId client-side —
      // marking Personal assigns whichever caller identity the backend
      // resolved from the request's X-Azure-OID/X-Azure-TID headers.
      const all = await getCapabilities();
      const refreshed = all.find((c) => c.CapabilityId === capability.CapabilityId);
      if (refreshed) setCapability(refreshed);
    } catch (err) {
      setVisibilityError(err instanceof Error ? err.message : t('capabilityDetail.visibilityErrorFallback'));
    } finally {
      setVisibilityBusy(false);
    }
  }

  if (loading) return <LoadingSpinner label={t('capabilityDetail.loading')} />;

  if (error || !capability) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16 text-center">
        <p className="text-red-300">{error ?? t('capabilityDetail.notFound')}</p>
        <Link to="/" className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300">
          <ArrowLeft className="h-4 w-4" /> {t('capabilityDetail.backToCatalog')}
        </Link>
      </div>
    );
  }

  const SubjectIcon = getSubjectIcon(capability.SubjectCode);
  const gradient = getSubjectGradient(capability.SubjectCode);

  return (
    <div>
      <div className={`relative h-56 w-full overflow-hidden bg-gradient-to-br ${gradient}`}>
        {capability.HasCoverImage && (
          <img
            src={apiImageUrl(`/capabilities/${capability.CapabilityId}/cover-image`)}
            alt=""
            className="h-full w-full object-cover opacity-80"
          />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black via-black/40 to-transparent" />
        <div className="absolute inset-x-0 bottom-0 mx-auto max-w-7xl px-6 pb-6">
          <Link to="/" className="mb-3 inline-flex items-center gap-1.5 text-sm text-[#fff]/80 hover:text-[#fff]">
            <ArrowLeft className="h-4 w-4" /> {t('capabilityDetail.catalog')}
          </Link>
          <h1 className="text-3xl font-bold text-[#fff]">{capability.Name}</h1>
          {capability.SubjectCode && (
            <span className="mt-2 inline-flex items-center gap-1.5 rounded-full bg-black/30 px-3 py-1 text-xs font-medium text-[#fff] backdrop-blur-sm">
              <SubjectIcon className="h-3.5 w-3.5" />
              {capability.SubjectCode}
            </span>
          )}
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-6 py-10">
        {capability.LearningSummary && (
          <p className="max-w-3xl text-lg text-slate-300">{capability.LearningSummary}</p>
        )}
        {capability.Description && <p className="mt-3 max-w-3xl text-sm text-slate-400">{capability.Description}</p>}

        <div className="mt-8 grid grid-cols-2 gap-4 sm:grid-cols-3">
          <StatCard icon={Layers} label={t('capabilityDetail.levels')} value={capability.LevelCount} />
          <StatCard icon={GitBranch} label={t('capabilityDetail.nodes')} value={capability.NodeCount} />
          <StatCard
            icon={Calendar}
            label={t('capabilityDetail.updated')}
            value={new Date(capability.UpdatedDate).toLocaleDateString(i18n.language === 'en' ? 'en-US' : 'es-MX')}
          />
        </div>

        <div className="mt-8">
          <h3 className="mb-3 text-lg font-semibold text-white">{t('capabilityDetail.programsHeading')}</h3>

          {memberships.length > 0 && (
            <div className="mb-3 flex flex-wrap gap-2">
              {memberships.map((m) => (
                <span
                  key={m.ProgramId}
                  className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/[0.03] py-1.5 pl-3 pr-1.5 text-sm text-slate-200"
                >
                  <Link to={`/programs/${m.ProgramId}`} className="hover:text-white">
                    {m.ProgramName}
                    {m.SortOrder ? ` (#${m.SortOrder})` : ''}
                  </Link>
                  <button
                    type="button"
                    onClick={() => handleRemoveFromProgram(m.ProgramId)}
                    disabled={linkBusy}
                    className="rounded-full p-1 text-slate-500 hover:bg-red-500/10 hover:text-red-300 disabled:opacity-40"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                </span>
              ))}
            </div>
          )}

          <div className="flex flex-wrap items-center gap-2">
            <select
              value={programToAdd}
              onChange={(e) => setProgramToAdd(e.target.value)}
              className="input max-w-xs"
            >
              <option value="">{t('capabilityDetail.addToProgramPlaceholder')}</option>
              {allPrograms
                .filter((p) => !memberships.some((m) => m.ProgramId === p.ProgramId))
                .map((p) => (
                  <option key={p.ProgramId} value={p.ProgramId}>
                    {p.Name}
                  </option>
                ))}
            </select>
            <button
              type="button"
              onClick={handleAddToProgram}
              disabled={!programToAdd || linkBusy}
              className="inline-flex items-center gap-1.5 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-2 text-sm font-medium text-slate-200 hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-40"
            >
              <GraduationCap className="h-4 w-4" />
              {t('capabilityDetail.add')}
            </button>
          </div>

          {linkError && <p className="mt-2 text-sm text-red-300">{linkError}</p>}
        </div>

        <div className="mt-8">
          <h3 className="mb-1 text-lg font-semibold text-white">{t('capabilityDetail.audienceHeading')}</h3>
          <p className="mb-3 text-sm text-slate-400">{t('capabilityDetail.audienceHint')}</p>
          <div className="grid max-w-md grid-cols-2 gap-2">
            {(
              [
                { value: 'Standard' as const, label: t('newCapability.audienceStandard') },
                { value: 'YoungLearner' as const, label: t('newCapability.audienceYoungLearner') },
              ]
            ).map((option) => {
              const active = (capability.PedagogicalAudience === 'YoungLearner' ? 'YoungLearner' : 'Standard') === option.value;
              return (
                <button
                  key={option.value}
                  type="button"
                  disabled={audienceBusy}
                  onClick={() => handleSetAudience(option.value)}
                  className={`flex items-center justify-center gap-2 rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${
                    active
                      ? 'border-brand-400/60 bg-brand-500/10 text-white'
                      : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                  }`}
                >
                  <Baby className="h-4 w-4 shrink-0" />
                  {option.label}
                </button>
              );
            })}
          </div>
          {audienceError && <p className="mt-2 text-sm text-red-300">{audienceError}</p>}
        </div>

        <div className="mt-8">
          <h3 className="mb-1 text-lg font-semibold text-white">{t('capabilityDetail.visibilityHeading')}</h3>
          <p className="mb-3 text-sm text-slate-400">{t('newCapability.visibilityHint')}</p>
          <div className="grid max-w-md grid-cols-2 gap-2">
            {(
              [
                { value: false, label: t('newCapability.visibilityPersonal') },
                { value: true, label: t('newCapability.visibilityGlobal') },
              ]
            ).map((option) => {
              const active = (capability.OwnerPersonId === null) === option.value;
              return (
                <button
                  key={String(option.value)}
                  type="button"
                  disabled={visibilityBusy}
                  onClick={() => handleSetVisibility(option.value)}
                  className={`flex items-center justify-center gap-2 rounded-xl border px-3 py-2.5 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-40 ${
                    active
                      ? 'border-brand-400/60 bg-brand-500/10 text-white'
                      : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
                  }`}
                >
                  <Globe className="h-4 w-4 shrink-0" />
                  {option.label}
                </button>
              );
            })}
          </div>
          {visibilityError && <p className="mt-2 text-sm text-red-300">{visibilityError}</p>}
        </div>

        <div className="mt-8 flex flex-wrap items-center gap-3">
          <Link
            to={`/capabilities/${capability.CapabilityId}/preview`}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02]"
          >
            <PlayCircle className="h-4 w-4" />
            {t('capabilityDetail.tryAsStudent')}
          </Link>
          <a
            href={`${LEARN_APP_BASE_URL}/capabilities/${capability.CapabilityId}`}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1.5 text-sm text-slate-400 hover:text-white"
          >
            {t('capabilityDetail.openInStudentApp')}
            <ExternalLink className="h-3.5 w-3.5" />
          </a>
          <button
            onClick={() => setShowDeleteModal(true)}
            className="ml-auto inline-flex items-center gap-1.5 px-4 py-2 rounded-lg text-sm font-medium text-red-400 hover:bg-red-500/10 hover:text-red-300 transition-colors cursor-pointer border border-red-500/30 hover:border-red-500/50"
          >
            <Trash2 className="h-4 w-4" />
            {t('capabilityDetail.delete')}
          </button>
        </div>

        {showDeleteModal && (
          <DeleteCapabilityModal
            capabilityName={capability.Name}
            capabilityId={capability.CapabilityId}
            onConfirm={handleDelete}
            onCancel={() => setShowDeleteModal(false)}
          />
        )}
      </div>
    </div>
  );
}

function StatCard({ icon: Icon, label, value }: { icon: typeof Layers; label: string; value: string | number }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
      <Icon className="h-4 w-4 text-brand-400" />
      <div className="mt-2 text-xl font-semibold text-white">{value}</div>
      <div className="text-xs text-slate-400">{label}</div>
    </div>
  );
}
