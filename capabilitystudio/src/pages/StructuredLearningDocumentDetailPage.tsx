import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, BookOpen, FileText, Layers, Calendar, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  addStructuredLearningDocumentToProgram,
  deleteStructuredLearningDocument,
  getProgramsForStructuredLearningDocument,
  getStructuredLearningDocument,
  removeStructuredLearningDocumentFromProgram,
  updateStructuredLearningVisibility,
  type StructuredLearningDocumentDetail,
  type StructuredLearningDocumentProgramMembership,
} from '../lib/api/structuredLearningApi';
import { getPrograms, type BackendProgram } from '../lib/api/programsApi';
import { apiImageUrl } from '../lib/api/httpClient';
import LoadingSpinner from '../components/LoadingSpinner';
import DeleteCapabilityModal from '../components/DeleteCapabilityModal';

export default function StructuredLearningDocumentDetailPage() {
  const { t } = useTranslation();
  const { documentId } = useParams<{ documentId: string }>();
  const navigate = useNavigate();
  const [document, setDocument] = useState<StructuredLearningDocumentDetail | null>(null);
  const [programs, setPrograms] = useState<BackendProgram[]>([]);
  const [memberships, setMemberships] = useState<StructuredLearningDocumentProgramMembership[]>([]);
  const [selectedProgramId, setSelectedProgramId] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [linkBusy, setLinkBusy] = useState(false);
  const [linkError, setLinkError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [visibility, setVisibility] = useState({ isGlobal: false, isPublished: false });
  const [visibilityBusy, setVisibilityBusy] = useState(false);
  const [visibilityMessage, setVisibilityMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!documentId) {
      setError('Document not found.');
      setLoading(false);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([
      getStructuredLearningDocument(documentId),
      getProgramsForStructuredLearningDocument(documentId),
      getPrograms(),
    ])
      .then(([detail, currentMemberships, allPrograms]) => {
        if (cancelled) return;
        setDocument(detail);
        setVisibility({ isGlobal: detail.IsGlobal, isPublished: detail.IsPublished });
        setMemberships(currentMemberships);
        setPrograms(allPrograms);
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load this document.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [documentId]);

  async function handleSaveVisibility() {
    if (!documentId || visibilityBusy) return;
    setVisibilityBusy(true);
    setVisibilityMessage(null);
    try {
      const saved = await updateStructuredLearningVisibility(documentId, visibility);
      setVisibility({ isGlobal: saved.IsGlobal, isPublished: saved.IsPublished });
      setVisibilityMessage(t('structuredLearningVisibility.saved'));
    } catch {
      setVisibilityMessage(t('structuredLearningVisibility.error'));
    } finally {
      setVisibilityBusy(false);
    }
  }

  async function handleAddToProgram() {
    if (!documentId || !selectedProgramId || linkBusy) return;
    setLinkBusy(true);
    setLinkError(null);

    try {
      await addStructuredLearningDocumentToProgram(documentId, selectedProgramId);
      const refreshed = await getProgramsForStructuredLearningDocument(documentId);
      setMemberships(refreshed);
      setSelectedProgramId('');
    } catch (err) {
      setLinkError(err instanceof Error ? err.message : 'Could not attach this document to the selected program.');
    } finally {
      setLinkBusy(false);
    }
  }

  async function handleRemoveFromProgram(programId: string) {
    if (!documentId || linkBusy) return;
    setLinkBusy(true);
    setLinkError(null);

    try {
      await removeStructuredLearningDocumentFromProgram(documentId, programId);
      setMemberships((prev) => prev.filter((m) => m.ProgramId !== programId));
    } catch (err) {
      setLinkError(err instanceof Error ? err.message : 'Could not remove this document from the program.');
    } finally {
      setLinkBusy(false);
    }
  }

  async function handleDeleteDocument() {
    if (!documentId || !document || deleting) return;

    setDeleting(true);
    setError(null);

    try {
      await deleteStructuredLearningDocument(documentId);
      setShowDeleteModal(false);
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not delete this structured learning course.');
    } finally {
      setDeleting(false);
    }
  }

  if (loading) return <LoadingSpinner label="Loading structured learning document..." />;

  if (error || !document) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-16 text-center">
        <p className="text-red-300">{error ?? 'This structured learning document could not be found.'}</p>
        <Link to="/" className="mt-4 inline-flex items-center gap-1.5 text-sm text-brand-400 hover:text-brand-300">
          <ArrowLeft className="h-4 w-4" /> Back to catalog
        </Link>
      </div>
    );
  }

  const createdDate = new Date().toLocaleDateString();

  return (
    <div>
      <div className="relative h-56 w-full overflow-hidden bg-gradient-to-br from-teal-500/30 to-emerald-500/20">
        {document.CoverIllustrationId && (
          <img
            src={apiImageUrl(`/illustrations/structured-learning/${document.CoverIllustrationId}/image`)}
            alt=""
            className="absolute inset-0 h-full w-full object-cover opacity-70"
          />
        )}
        <div className="absolute inset-0 bg-gradient-to-t from-black via-black/40 to-transparent" />
        <div className="absolute inset-x-0 bottom-0 mx-auto max-w-7xl px-6 pb-6">
          <Link to="/" className="mb-3 inline-flex items-center gap-1.5 text-sm text-[#fff]/80 hover:text-[#fff]">
            <ArrowLeft className="h-4 w-4" /> Back to catalog
          </Link>
          <h1 className="text-3xl font-bold text-[#fff]">{document.DocumentName}</h1>
          <span className="mt-2 inline-flex items-center gap-1.5 rounded-full bg-black/30 px-3 py-1 text-xs font-medium text-[#fff] backdrop-blur-sm">
            <FileText className="h-3.5 w-3.5" /> Structured section document
          </span>
        </div>
      </div>

      <div className="mx-auto max-w-7xl px-6 py-10">
        <div className="mt-8 grid grid-cols-2 gap-4 sm:grid-cols-3">
          <InfoCard icon={Layers} label="Sections" value={document.Sections.length} />
          <InfoCard icon={BookOpen} label="Mode" value="Structured learning" />
          <InfoCard icon={Calendar} label="Created" value={createdDate} />
        </div>

        <div className="mt-8 flex flex-wrap gap-3">
          <button
            type="button"
            onClick={() => navigate(`/structured-learning/${document.StructuredLearningDocumentId}/course`)}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-500 px-5 py-3 font-medium text-white shadow-lg shadow-brand-500/20 transition hover:bg-brand-400"
          >
            <BookOpen className="h-4 w-4" /> View course
          </button>
          <button
            type="button"
            onClick={() => setShowDeleteModal(true)}
            disabled={deleting}
            className="inline-flex items-center gap-2 rounded-xl border border-red-500/30 bg-red-500/10 px-5 py-3 font-medium text-red-200 transition hover:border-red-400/60 hover:bg-red-500/15 disabled:cursor-not-allowed disabled:opacity-60"
          >
            <Trash2 className="h-4 w-4" />
            {deleting ? 'Deleting...' : 'Delete course'}
          </button>
          <Link
            to="/"
            className="inline-flex items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-5 py-3 font-medium text-slate-200 transition hover:border-white/20 hover:bg-white/[0.06]"
          >
            <ArrowLeft className="h-4 w-4" /> Keep browsing catalog
          </Link>
        </div>

        <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.02] p-6">
          <h3 className="text-lg font-semibold text-white">{t('structuredLearningVisibility.title')}</h3>
          <p className="mt-1 text-sm text-slate-400">{t('structuredLearningVisibility.description')}</p>

          <div className="mt-5 grid gap-3 sm:grid-cols-2">
            <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-white/10 bg-white/[0.03] p-4">
              <input
                type="checkbox"
                checked={visibility.isGlobal}
                onChange={(event) => setVisibility((current) => ({ ...current, isGlobal: event.target.checked }))}
                className="mt-1 h-4 w-4 accent-cyan-400"
              />
              <span>
                <span className="block font-medium text-white">{t('structuredLearningVisibility.global')}</span>
                <span className="mt-1 block text-sm text-slate-400">{t('structuredLearningVisibility.globalHint')}</span>
              </span>
            </label>
            <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-white/10 bg-white/[0.03] p-4">
              <input
                type="checkbox"
                checked={visibility.isPublished}
                onChange={(event) => setVisibility((current) => ({ ...current, isPublished: event.target.checked }))}
                className="mt-1 h-4 w-4 accent-emerald-400"
              />
              <span>
                <span className="block font-medium text-white">{t('structuredLearningVisibility.published')}</span>
                <span className="mt-1 block text-sm text-slate-400">{t('structuredLearningVisibility.publishedHint')}</span>
              </span>
            </label>
          </div>

          <div className="mt-4 flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={handleSaveVisibility}
              disabled={visibilityBusy}
              className="rounded-xl bg-cyan-400 px-4 py-2.5 text-sm font-semibold text-slate-950 transition hover:bg-cyan-300 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {visibilityBusy ? t('structuredLearningVisibility.saving') : t('structuredLearningVisibility.save')}
            </button>
            {visibilityMessage && <span className="text-sm text-slate-300">{visibilityMessage}</span>}
          </div>
        </div>

        <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.02] p-6">
          <h3 className="text-lg font-semibold text-white">Add to program</h3>

          {memberships.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              {memberships.map((membership) => (
                <span
                  key={membership.ProgramId}
                  className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/[0.03] py-1.5 pl-3 pr-1.5 text-sm text-slate-200"
                >
                  <Link to={`/programs/${membership.ProgramId}`} className="hover:text-white">
                    {membership.ProgramName}
                    {membership.SortOrder ? ` (#${membership.SortOrder})` : ''}
                  </Link>
                  <button
                    type="button"
                    onClick={() => handleRemoveFromProgram(membership.ProgramId)}
                    className="inline-flex items-center gap-1 rounded-full border border-red-500/20 bg-red-500/5 px-2 py-1 text-[11px] font-medium text-red-200 transition hover:border-red-400/50 hover:bg-red-500/10"
                    aria-label={`Delete ${membership.ProgramName}`}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    Delete
                  </button>
                </span>
              ))}
            </div>
          )}

          <div className="mt-4 flex flex-col gap-3 sm:flex-row">
            <select
              value={selectedProgramId}
              onChange={(e) => setSelectedProgramId(e.target.value)}
              className="min-w-[220px] rounded-xl border border-white/10 bg-[#070b12] px-3 py-2 text-sm text-slate-100 outline-none ring-0 transition focus:border-brand-400"
            >
              <option value="">Select a program</option>
              {programs
                .filter((program) => !memberships.some((m) => m.ProgramId === program.ProgramId))
                .map((program) => (
                  <option key={program.ProgramId} value={program.ProgramId}>
                    {program.Name}
                  </option>
                ))}
            </select>

            <button
              type="button"
              onClick={handleAddToProgram}
              disabled={!selectedProgramId || linkBusy}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-500 px-4 py-2.5 font-medium text-white transition hover:bg-brand-400 disabled:cursor-not-allowed disabled:opacity-50"
            >
              <Plus className="h-4 w-4" />
              {linkBusy ? 'Saving...' : 'Add to program'}
            </button>
          </div>

          {linkError && <p className="mt-3 text-sm text-red-300">{linkError}</p>}
        </div>

        <div
          className={
            document.ExecutiveSummary
              ? 'mt-10 rounded-2xl border border-brand-500/20 bg-gradient-to-br from-brand-500/10 via-white/[0.02] to-accent-500/10 p-6'
              : 'mt-10 rounded-2xl border border-white/10 bg-white/[0.02] p-6'
          }
        >
          <h2 className="text-lg font-semibold text-white">Document summary</h2>
          {document.ExecutiveSummary ? (
            <p className="mt-3 whitespace-pre-line text-sm leading-relaxed text-slate-300">{document.ExecutiveSummary}</p>
          ) : (
            <p className="mt-3 text-slate-300">
              This document contains {document.Sections.length} section{document.Sections.length === 1 ? '' : 's'} ready to be reviewed and studied in order.
            </p>
          )}
          {document.PedagogicalAudience && (
            <p className="mt-2 text-sm text-slate-400">Audience: {document.PedagogicalAudience}</p>
          )}
          {document.AssessmentQuestionType && (
            <p className="mt-2 text-sm text-slate-400">Assessment: {document.AssessmentQuestionType}</p>
          )}
        </div>
      </div>

      {showDeleteModal && (
        <DeleteCapabilityModal
          capabilityName={document.DocumentName}
          capabilityId={document.StructuredLearningDocumentId}
          onConfirm={handleDeleteDocument}
          onCancel={() => setShowDeleteModal(false)}
        />
      )}
    </div>
  );
}

function InfoCard({ icon: Icon, label, value }: { icon: typeof Layers; label: string; value: string | number }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4">
      <div className="flex items-center gap-2 text-slate-400">
        <Icon className="h-4 w-4" />
        <span className="text-xs uppercase tracking-wide">{label}</span>
      </div>
      <div className="mt-3 text-xl font-semibold text-white">{value}</div>
    </div>
  );
}
