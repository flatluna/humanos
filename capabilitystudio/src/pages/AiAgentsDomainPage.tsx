import { useEffect, useState } from 'react';
import { ArrowLeft, ArrowRight, Layers3 } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningTopics, type StructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';
import { getSubjectGradient, getSubjectIcon } from '../lib/subjectVisuals';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

export default function AiAgentsDomainPage() {
  const { subjectName } = useParams<{ subjectName: string }>();
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
  const [subject, setSubject] = useState<Subject | null>(null);
  const [topics, setTopics] = useState<StructuredLearningTopic[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadDomain() {
      setLoading(true);
      setError(null);
      try {
        const domainList = await getSubjects();
        const selected = domainList.find((item) => item.Name.toLowerCase() === decodeURIComponent(subjectName ?? '').toLowerCase());
        if (!selected) {
          setSubject(null);
          setTopics([]);
          return;
        }
        const topicList = await getStructuredLearningTopics(tenantId, selected.SubjectId);
        if (cancelled) return;
        setSubject(selected);
        setTopics(topicList);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load this domain.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadDomain();
    return () => {
      cancelled = true;
    };
  }, [subjectName, tenantId]);

  const Icon = getSubjectIcon(subject?.Code);

  return (
    <div className="mx-auto max-w-7xl px-6 py-10">
      <Link to="/ai-agents" className="inline-flex items-center gap-1.5 text-sm text-slate-400 transition hover:text-white">
        <ArrowLeft className="h-4 w-4" /> AI Agents
      </Link>

      {loading && <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.03] p-8 text-center text-sm text-slate-400">Loading domain...</div>}
      {error && <div className="mt-8 rounded-2xl border border-red-400/20 bg-red-500/[0.08] p-5 text-sm text-red-200">{error}</div>}

      {!loading && !error && !subject && (
        <div className="mt-8 rounded-2xl border border-dashed border-amber-400/25 bg-amber-500/[0.05] p-10 text-center">
          <h1 className="text-xl font-semibold text-white">Domain not found</h1>
          <p className="mt-2 text-sm text-slate-400">This knowledge domain is not available.</p>
        </div>
      )}

      {!loading && !error && subject && (
        <>
          <div className={`mt-6 overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-br ${getSubjectGradient(subject.Code)} p-[1px]`}>
            <div className="rounded-[calc(1.5rem-1px)] bg-slate-950/90 p-6 sm:p-8">
              <div className="flex items-start gap-4">
                <div className={`flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br ${getSubjectGradient(subject.Code)} text-white shadow-lg`}>
                  <Icon className="h-7 w-7" />
                </div>
                <div>
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-cyan-300">AI Agents / Domain</p>
                  <h1 className="mt-1 text-3xl font-bold tracking-tight text-white">{subject.Name}</h1>
                  <p className="mt-2 text-sm text-slate-400">Choose an ontological type to explore its courses and specialized agents.</p>
                </div>
                <div className="ml-auto hidden items-center gap-2 rounded-full border border-white/10 bg-white/[0.04] px-3 py-1.5 text-xs text-slate-300 sm:flex">
                  <Layers3 className="h-3.5 w-3.5 text-cyan-300" /> {topics.length} types
                </div>
              </div>
            </div>
          </div>

          <div className="mt-10 flex items-end justify-between gap-4">
            <div>
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Ontology</p>
              <h2 className="mt-2 text-2xl font-bold text-white">Types in {subject.Name}</h2>
            </div>
            <span className="text-sm text-slate-500">{topics.length} total</span>
          </div>

          {topics.length === 0 ? (
            <div className="mt-6 rounded-2xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">No ontological types have been created for this domain yet.</div>
          ) : (
            <div className="mt-6 grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {topics.map((topic) => (
                <Link
                  key={topic.StructuredLearningTopicId}
                  to={`/ai-agents/${encodeURIComponent(subject.Name)}/${encodeURIComponent(topic.Name)}`}
                  className="group rounded-2xl border border-white/10 bg-white/[0.03] p-5 transition hover:-translate-y-1 hover:border-cyan-400/30 hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-cyan-500/10"
                >
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-cyan-500/15 text-cyan-300 ring-1 ring-cyan-400/20">
                      <Layers3 className="h-5 w-5" />
                    </div>
                    <ArrowRight className="h-5 w-5 text-slate-600 transition group-hover:translate-x-1 group-hover:text-cyan-300" />
                  </div>
                  <h3 className="mt-5 text-xl font-semibold text-white">{topic.Name}</h3>
                  <p className="mt-2 line-clamp-2 text-sm leading-6 text-slate-400">{topic.Description ?? 'Explore the courses and agents associated with this ontological type.'}</p>
                  <div className="mt-5 text-xs font-medium uppercase tracking-[0.14em] text-cyan-300">View courses and agents</div>
                </Link>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}
