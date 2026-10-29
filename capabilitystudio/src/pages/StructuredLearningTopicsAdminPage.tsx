import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Plus, Pencil, Trash2, X, Check, Layers3, Sparkles } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getSubjectIcon } from '../lib/subjectVisuals';
import {
  getStructuredLearningTopics,
  createStructuredLearningTopic,
  updateStructuredLearningTopic,
  deleteStructuredLearningTopic,
  type StructuredLearningTopic,
} from '../lib/api/structuredLearningTopicsApi';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

/** Knowledge Graph — Catálogo de Tipos/Temas admin page (2026-08-16).
 * One Subject (domain, e.g. "Leyes") has many Topics (e.g. "Ley del IVA",
 * "Ley del ISR") — used to scope master-registry entity resolution for
 * document-structural entities so e.g. "Artículo 1o." of different laws
 * never collide. Full CRUD (add/edit/delete) per the user's request. */
export default function StructuredLearningTopicsAdminPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const { subjectName } = useParams<{ subjectName?: string }>();
  const isDomainPage = Boolean(subjectName);
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;

  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [selectedSubjectId, setSelectedSubjectId] = useState<string>('');
  const [topics, setTopics] = useState<StructuredLearningTopic[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [creating, setCreating] = useState(false);
  const [newName, setNewName] = useState('');
  const [newDescription, setNewDescription] = useState('');

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState('');
  const [editDescription, setEditDescription] = useState('');

  useEffect(() => {
    getSubjects().then((list) => {
      setSubjects(list);
      const selected = subjectName ? list.find((subject) => subject.Name.toLowerCase() === decodeURIComponent(subjectName).toLowerCase()) : list[0];
      if (selected) setSelectedSubjectId(selected.SubjectId);
    }).catch(() => setSubjects([]));
  }, [subjectName]);

  async function refreshTopics(subjectId: string) {
    setLoading(true);
    setError(null);
    try {
      const list = await getStructuredLearningTopics(tenantId, subjectId);
      setTopics(list);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setTopics([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (!selectedSubjectId) return;
    void refreshTopics(selectedSubjectId);
  }, [selectedSubjectId, tenantId]);

  async function handleCreate() {
    if (!newName.trim() || !selectedSubjectId) return;
    try {
      await createStructuredLearningTopic(tenantId, selectedSubjectId, newName.trim(), newDescription.trim() || null);
      setNewName('');
      setNewDescription('');
      setCreating(false);
      await refreshTopics(selectedSubjectId);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  function startEdit(topic: StructuredLearningTopic) {
    setEditingId(topic.StructuredLearningTopicId);
    setEditName(topic.Name);
    setEditDescription(topic.Description ?? '');
  }

  async function handleUpdate(topicId: string) {
    if (!editName.trim() || !selectedSubjectId) return;
    try {
      await updateStructuredLearningTopic(topicId, editName.trim(), editDescription.trim() || null);
      setEditingId(null);
      await refreshTopics(selectedSubjectId);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  async function handleDelete(topicId: string) {
    if (!selectedSubjectId) return;
    try {
      await deleteStructuredLearningTopic(topicId);
      await refreshTopics(selectedSubjectId);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  const selectedSubject = subjects.find((s) => s.SubjectId === selectedSubjectId);
  const SubjectIcon = selectedSubject ? getSubjectIcon(selectedSubject.Code) : Layers3;

  return (
    <div className="mx-auto max-w-5xl px-4 py-8 sm:px-6 lg:py-12">
      <Link to={isDomainPage ? '/structured-learning-topics' : '/'} className="inline-flex items-center gap-1.5 text-sm text-slate-400 hover:text-white">
        <ArrowLeft className="h-4 w-4" /> {isDomainPage ? t('structuredLearningTopics.allDomains') : t('common.back', 'Back')}
      </Link>

      {isDomainPage ? (
        <div className="mt-6 overflow-hidden rounded-3xl border border-emerald-400/20 bg-gradient-to-br from-emerald-500/[0.14] via-white/[0.04] to-sky-500/[0.08] p-6 sm:p-8">
          <div className="flex items-start gap-4">
            <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-emerald-400/15 text-emerald-300">
              <SubjectIcon className="h-7 w-7" />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-emerald-300">{t('structuredLearningTopics.domainLabel')}</p>
              <h1 className="mt-1 text-3xl font-bold tracking-tight text-white">{subjectName}</h1>
              <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">{t('structuredLearningTopics.domainDescription')}</p>
            </div>
            <div className="ml-auto hidden items-center gap-2 rounded-full border border-white/10 bg-black/10 px-3 py-1.5 text-xs text-slate-300 sm:flex">
              <Layers3 className="h-3.5 w-3.5 text-emerald-300" /> {t('structuredLearningTopics.typesCount', { count: topics.length })}
            </div>
          </div>
        </div>
      ) : (
        <div className="mt-6">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-emerald-400">{t('structuredLearningTopics.knowledgeCatalog')}</p>
          <h1 className="mt-2 text-3xl font-bold tracking-tight text-white">{t('structuredLearningTopics.title')}</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-400">{t('structuredLearningTopics.subtitle')}</p>
        </div>
      )}

      {!isDomainPage && <div className="mt-8 grid grid-cols-2 gap-3 sm:grid-cols-3">
        {subjects.map((subject) => {
          const Icon = getSubjectIcon(subject.Code);
          const active = selectedSubjectId === subject.SubjectId;
          return (
            <button
              key={subject.SubjectId}
              type="button"
              onClick={() => navigate(`/structured-learning-topics/${encodeURIComponent(subject.Name)}`)}
              className={`flex items-center gap-3 rounded-2xl border p-4 text-left text-sm transition-colors ${
                active
                  ? 'border-brand-400/60 bg-brand-500/10 text-white'
                  : 'border-white/10 bg-white/[0.02] text-slate-300 hover:border-white/20'
              }`}
            >
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white/5"><Icon className="h-5 w-5 text-emerald-300" /></span>
              <span className="min-w-0"><span className="block truncate font-medium">{subject.Name}</span><span className="mt-0.5 block text-[11px] text-slate-500">{t('structuredLearningTopics.exploreTypes')}</span></span>
            </button>
          );
        })}
      </div>}

      {isDomainPage && subjects.length > 0 && !selectedSubjectId && (
        <div className="mt-8 rounded-2xl border border-dashed border-amber-400/25 bg-amber-500/[0.05] px-6 py-10 text-center">
          <h2 className="text-lg font-semibold text-white">{t('structuredLearningTopics.domainNotFound')}</h2>
          <p className="mt-2 text-sm text-slate-400">{t('structuredLearningTopics.domainNotFoundDescription', { name: subjectName })}</p>
          <Link to="/structured-learning-topics" className="mt-5 inline-flex items-center gap-2 rounded-xl bg-amber-500 px-4 py-2.5 text-sm font-semibold text-slate-950 hover:bg-amber-400">
            <ArrowLeft className="h-4 w-4" /> {t('structuredLearningTopics.viewDomains')}
          </Link>
        </div>
      )}

      {selectedSubjectId && (
        <div className={`${isDomainPage ? 'mt-8' : 'mt-6'} rounded-2xl border border-white/10 bg-white/[0.03] p-5 sm:p-6`}>
          <div className="flex items-center justify-between">
            <h2 className="flex items-center gap-2 text-sm font-semibold text-white">
              <Sparkles className="h-4 w-4 text-emerald-300" /> {t('structuredLearningTopics.typesOf', { name: selectedSubject?.Name ?? '' })}
            </h2>
            <button
              type="button"
              onClick={() => isDomainPage ? navigate(`/structured-learning-topics/${encodeURIComponent(subjects.find((s) => s.SubjectId === selectedSubjectId)?.Name ?? '')}/new`) : setCreating(true)}
              className="flex items-center gap-1 rounded-lg border border-brand-400/40 bg-brand-500/10 px-2.5 py-1.5 text-xs font-medium text-brand-200 hover:bg-brand-500/20"
            >
              <Plus className="h-3.5 w-3.5" /> {t('structuredLearningTopics.addType')}
            </button>
          </div>

          {error && <p className="mt-3 text-xs text-red-400">{error}</p>}
          {loading && <p className="mt-3 text-xs text-slate-500">{t('structuredLearningTopics.loading')}</p>}

          {creating && (
            <div className="mt-3 space-y-2 rounded-xl border border-white/10 bg-white/[0.02] p-3">
              <input
                type="text"
                value={newName}
                onChange={(e) => setNewName(e.target.value)}
                placeholder={t('structuredLearningTopics.namePlaceholder')}
                className="w-full rounded-lg border border-white/10 bg-slate-950/40 px-3 py-2 text-sm text-white placeholder:text-slate-500"
              />
              <textarea
                value={newDescription}
                onChange={(e) => setNewDescription(e.target.value)}
                placeholder={t('structuredLearningTopics.descriptionPlaceholder')}
                rows={2}
                className="w-full rounded-lg border border-white/10 bg-slate-950/40 px-3 py-2 text-sm text-white placeholder:text-slate-500"
              />
              <div className="flex gap-2">
                <button type="button" onClick={handleCreate} className="flex items-center gap-1 rounded-lg bg-brand-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-400">
                  <Check className="h-3.5 w-3.5" /> {t('structuredLearningTopics.save')}
                </button>
                <button type="button" onClick={() => setCreating(false)} className="flex items-center gap-1 rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:border-white/20">
                  <X className="h-3.5 w-3.5" /> {t('structuredLearningTopics.cancel')}
                </button>
              </div>
            </div>
          )}

          <div className={isDomainPage ? 'mt-4 grid gap-3 sm:grid-cols-2' : 'mt-3 space-y-2'}>
            {topics.length === 0 && !loading && (
              <div className="rounded-xl border border-dashed border-emerald-400/25 bg-emerald-500/[0.04] px-5 py-8 text-center">
                <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-400/10 text-emerald-300">
                  <Layers3 className="h-6 w-6" />
                </div>
                <p className="mt-4 text-base font-semibold text-white">{t('structuredLearningTopics.emptyTitle', { name: selectedSubject?.Name ?? t('structuredLearningTopics.thisDomain') })}</p>
                <p className="mx-auto mt-1 max-w-md text-sm text-slate-400">
                  {t('structuredLearningTopics.emptyDescription', { name: selectedSubject?.Name ?? t('structuredLearningTopics.thisDomain') })}
                </p>
                <p className="mx-auto mt-4 max-w-lg text-xs text-slate-500">
                  {selectedSubject?.Name?.toLowerCase().includes('ley')
                    ? t('structuredLearningTopics.emptyExamplesLaw')
                    : t('structuredLearningTopics.emptyExamplesGeneric', { name: selectedSubject?.Name ?? '' })}
                </p>
                <Link
                  to={`/structured-learning-topics/${encodeURIComponent(subjects.find((s) => s.SubjectId === selectedSubjectId)?.Name ?? '')}/new`}
                  className="mt-5 inline-flex items-center gap-2 rounded-xl bg-emerald-500 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-400"
                >
                  <Plus className="h-4 w-4" /> {t('structuredLearningTopics.addFirstType')}
                </Link>
              </div>
            )}
            {topics.map((topic) => (
              <div key={topic.StructuredLearningTopicId} className="rounded-xl border border-white/10 bg-white/[0.02] p-4">
                {editingId === topic.StructuredLearningTopicId ? (
                  <div className="space-y-2">
                    <input
                      type="text"
                      value={editName}
                      onChange={(e) => setEditName(e.target.value)}
                      className="w-full rounded-lg border border-white/10 bg-slate-950/40 px-3 py-2 text-sm text-white"
                    />
                    <textarea
                      value={editDescription}
                      onChange={(e) => setEditDescription(e.target.value)}
                      rows={2}
                      className="w-full rounded-lg border border-white/10 bg-slate-950/40 px-3 py-2 text-sm text-white"
                    />
                    <div className="flex gap-2">
                      <button type="button" onClick={() => handleUpdate(topic.StructuredLearningTopicId)} className="flex items-center gap-1 rounded-lg bg-brand-500 px-3 py-1.5 text-xs font-medium text-white hover:bg-brand-400">
                        <Check className="h-3.5 w-3.5" /> {t('structuredLearningTopics.save')}
                      </button>
                      <button type="button" onClick={() => setEditingId(null)} className="flex items-center gap-1 rounded-lg border border-white/10 px-3 py-1.5 text-xs text-slate-300 hover:border-white/20">
                        <X className="h-3.5 w-3.5" /> {t('structuredLearningTopics.cancel')}
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-white">{topic.Name}</p>
                      {topic.Description && <p className="mt-0.5 text-xs text-slate-400">{topic.Description}</p>}
                    </div>
                    <div className="flex shrink-0 gap-1">
                      <button type="button" onClick={() => startEdit(topic)} className="rounded-lg p-1.5 text-slate-400 hover:bg-white/5 hover:text-white">
                        <Pencil className="h-3.5 w-3.5" />
                      </button>
                      <button type="button" onClick={() => handleDelete(topic.StructuredLearningTopicId)} className="rounded-lg p-1.5 text-slate-400 hover:bg-red-500/10 hover:text-red-400">
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
