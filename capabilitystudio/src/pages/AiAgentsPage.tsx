import { useEffect, useState } from 'react';
import { ArrowRight, Bot, Layers3, Sparkles } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningTopics, type StructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';
import { getSubjectGradient, getSubjectIcon } from '../lib/subjectVisuals';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

export default function AiAgentsPage() {
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [topicsBySubject, setTopicsBySubject] = useState<Record<string, StructuredLearningTopic[]>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadDomains() {
      setLoading(true);
      setError(null);
      try {
        const domainList = await getSubjects();
        const topicEntries = await Promise.all(
          domainList.map(async (subject) => [subject.SubjectId, await getStructuredLearningTopics(tenantId, subject.SubjectId)] as const)
        );
        if (cancelled) return;
        setSubjects(domainList);
        setTopicsBySubject(Object.fromEntries(topicEntries));
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load the knowledge domains.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadDomains();
    return () => {
      cancelled = true;
    };
  }, [tenantId]);

  return (
    <div className="mx-auto max-w-7xl px-6 py-10">
      <div className="relative overflow-hidden rounded-3xl border border-cyan-400/20 bg-gradient-to-br from-cyan-500/[0.14] via-white/[0.04] to-blue-500/[0.08] p-8 shadow-2xl shadow-black/20 sm:p-10">
        <div className="absolute -right-16 -top-20 h-56 w-56 rounded-full bg-cyan-400/10 blur-3xl" />
        <div className="relative flex items-start gap-4">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-cyan-400/15 text-cyan-300 ring-1 ring-cyan-400/25">
            <Bot className="h-7 w-7" />
          </div>
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-cyan-300">
              <Sparkles className="h-3.5 w-3.5" /> Agent network
            </div>
            <h1 className="mt-2 text-3xl font-bold tracking-tight text-white sm:text-4xl">AI Agents</h1>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-300">
              Start by organizing the agent network around your knowledge domains and their ontological types.
            </p>
          </div>
        </div>
      </div>

      <div className="mt-10 flex items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Knowledge domains</p>
          <h2 className="mt-2 text-2xl font-bold text-white">Choose a domain</h2>
        </div>
        {!loading && <span className="text-sm text-slate-500">{subjects.length} domains</span>}
      </div>

      {loading && <div className="mt-6 rounded-2xl border border-white/10 bg-white/[0.03] p-8 text-center text-sm text-slate-400">Loading domains...</div>}
      {error && <div className="mt-6 rounded-2xl border border-red-400/20 bg-red-500/[0.08] p-5 text-sm text-red-200">{error}</div>}

      {!loading && !error && subjects.length === 0 && (
        <div className="mt-6 rounded-2xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">No knowledge domains available yet.</div>
      )}

      {!loading && !error && subjects.length > 0 && (
        <div className="mt-6 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {subjects.map((subject) => {
            const Icon = getSubjectIcon(subject.Code);
            const topics = topicsBySubject[subject.SubjectId] ?? [];
            return (
              <Link
                key={subject.SubjectId}
                to={`/ai-agents/${encodeURIComponent(subject.Name)}`}
                className="group relative overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03] p-5 text-left transition hover:-translate-y-1 hover:border-white/25 hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-cyan-500/10"
              >
                <div className={`absolute inset-x-0 top-0 h-1 bg-gradient-to-r ${getSubjectGradient(subject.Code)}`} />
                <div className="flex items-start justify-between gap-4">
                  <div className={`flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br ${getSubjectGradient(subject.Code)} text-white shadow-lg`}>
                    <Icon className="h-6 w-6" />
                  </div>
                  <ArrowRight className="h-5 w-5 text-slate-600 transition group-hover:translate-x-1 group-hover:text-white" />
                </div>
                <h3 className="mt-5 text-xl font-semibold text-white">{subject.Name}</h3>
                <div className="mt-3 flex items-center gap-2 text-sm text-slate-400">
                  <Layers3 className="h-4 w-4 text-cyan-300" />
                  <span><strong className="text-slate-200">{topics.length}</strong> ontological type{topics.length === 1 ? '' : 's'}</span>
                </div>
                {topics.length > 0 && (
                  <div className="mt-4 flex flex-wrap gap-1.5">
                    {topics.slice(0, 3).map((topic) => <span key={topic.StructuredLearningTopicId} className="rounded-full border border-white/10 bg-white/[0.04] px-2.5 py-1 text-[11px] text-slate-400">{topic.Name}</span>)}
                    {topics.length > 3 && <span className="rounded-full border border-white/10 px-2.5 py-1 text-[11px] text-slate-500">+{topics.length - 3}</span>}
                  </div>
                )}
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
