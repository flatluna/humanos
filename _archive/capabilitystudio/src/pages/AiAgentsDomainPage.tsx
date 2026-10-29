import { useEffect, useMemo, useState } from 'react';
import { ArrowLeft, ArrowRight, Layers3, Search } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningTopics, type StructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';
import { getSubjectIcon } from '../lib/subjectVisuals';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

export default function AiAgentsDomainPage() {
  const { subjectName } = useParams<{ subjectName: string }>();
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
  const [subject, setSubject] = useState<Subject | null>(null);
  const [topics, setTopics] = useState<StructuredLearningTopic[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState('');

  const filteredTopics = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const filtered = needle ? topics.filter((topic) => topic.Name.toLowerCase().includes(needle)) : topics;
    return [...filtered].sort((a, b) => a.Name.localeCompare(b.Name));
  }, [topics, query]);

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
    <div className="mx-auto max-w-6xl px-6 py-12">
      <Link to="/ai-agents" className="inline-flex items-center gap-1.5 text-sm text-slate-400 transition hover:text-white">
        <ArrowLeft className="h-4 w-4" /> AI Agents
      </Link>

      {loading && <div className="mt-8 rounded-xl border border-white/10 bg-white/[0.02] p-8 text-center text-sm text-slate-400">Loading domain…</div>}
      {error && <div className="mt-8 rounded-xl border border-red-400/20 bg-red-500/[0.06] p-5 text-sm text-red-200">{error}</div>}

      {!loading && !error && !subject && (
        <div className="mt-8 rounded-xl border border-dashed border-white/15 p-10 text-center">
          <h1 className="text-xl font-semibold text-white">Domain not found</h1>
          <p className="mt-2 text-sm text-slate-400">This knowledge domain is not available.</p>
        </div>
      )}

      {!loading && !error && subject && (
        <>
          <div className="mt-6 border-b border-white/10 pb-8">
            <div className="flex items-start gap-4">
              <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-lg border border-white/10 bg-white/[0.03] text-brand-300">
                <Icon className="h-7 w-7" />
              </div>
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-brand-300">AI Agents / Domain</p>
                <h1 className="mt-2 text-3xl font-semibold tracking-tight text-white">{subject.Name}</h1>
                <p className="mt-2 text-sm text-slate-400">Choose an ontological type to explore its courses and specialized agents.</p>
              </div>
              <div className="ml-auto hidden items-center gap-2 rounded-lg border border-white/10 bg-white/[0.02] px-3 py-1.5 text-xs text-slate-400 sm:flex">
                <Layers3 className="h-3.5 w-3.5 text-brand-300" /> {topics.length} types
              </div>
            </div>
          </div>

          <div className="mt-10 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
            <div>
              <p className="text-[11px] font-semibold uppercase tracking-[0.24em] text-slate-500">Ontology</p>
              <h2 className="mt-1.5 text-xl font-semibold text-white">Types in {subject.Name}</h2>
            </div>
            <div className="flex items-center gap-3">
              <div className="relative">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
                <input
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="Filter types…"
                  className="w-56 rounded-lg border border-white/10 bg-white/[0.02] py-2 pl-9 pr-3 text-sm text-white placeholder:text-slate-500 focus:border-brand-400/50 focus:outline-none"
                />
              </div>
              <span className="whitespace-nowrap text-sm text-slate-500">{filteredTopics.length} of {topics.length}</span>
            </div>
          </div>

          {topics.length === 0 ? (
            <div className="mt-6 rounded-xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">No ontological types have been created for this domain yet.</div>
          ) : filteredTopics.length === 0 ? (
            <div className="mt-6 rounded-xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">No types match "{query}".</div>
          ) : (
            <div className="mt-6 grid grid-cols-1 gap-px overflow-hidden rounded-xl border border-white/10 bg-white/10 sm:grid-cols-2 lg:grid-cols-3">
              {filteredTopics.map((topic) => (
                <Link
                  key={topic.StructuredLearningTopicId}
                  to={`/ai-agents/${encodeURIComponent(subject.Name)}/${encodeURIComponent(topic.Name)}`}
                  className="group flex flex-col bg-slate-950 p-6 transition hover:bg-white/[0.03]"
                >
                  <div className="flex items-start justify-between gap-4">
                    <div className="flex h-11 w-11 items-center justify-center rounded-lg border border-white/10 bg-white/[0.03] text-brand-300">
                      <Layers3 className="h-5 w-5" />
                    </div>
                    <ArrowRight className="h-5 w-5 text-slate-600 transition group-hover:translate-x-1 group-hover:text-brand-300" />
                  </div>
                  <h3 className="mt-5 text-lg font-semibold text-white">{topic.Name}</h3>
                  <p className="mt-2 line-clamp-2 text-sm leading-6 text-slate-400">{topic.Description ?? 'Explore the courses and agents associated with this ontological type.'}</p>
                  <div className="mt-5 text-xs font-medium uppercase tracking-[0.14em] text-slate-500 transition group-hover:text-brand-300">View courses and agents</div>
                </Link>
              ))}
            </div>
          )}
        </>
      )}
    </div>
  );
}

