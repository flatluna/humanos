import { useEffect, useMemo, useState } from 'react';
import { ArrowRight, Bot, Layers3, Search } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningTopics, type StructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';
import { getSubjectIcon } from '../lib/subjectVisuals';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

export default function AiAgentsPage() {
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [topicsBySubject, setTopicsBySubject] = useState<Record<string, StructuredLearningTopic[]>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState('');

  const filteredSubjects = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const filtered = needle ? subjects.filter((subject) => subject.Name.toLowerCase().includes(needle)) : subjects;
    return [...filtered].sort((a, b) => a.Name.localeCompare(b.Name));
  }, [subjects, query]);

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
    <div className="mx-auto max-w-6xl px-6 py-12">
      <div className="border-b border-white/10 pb-8">
        <div className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.28em] text-brand-300">
          <Bot className="h-3.5 w-3.5" /> Agent network
        </div>
        <h1 className="mt-3 text-4xl font-semibold tracking-tight text-white">AI Agents</h1>
        <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-400">
          A catalog of specialized tutoring agents, organized by knowledge domain. Select a domain to browse
          its subjects and start a session.
        </p>
      </div>

      <div className="mt-10 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.24em] text-slate-500">Knowledge domains</p>
          <h2 className="mt-1.5 text-xl font-semibold text-white">Choose a domain</h2>
        </div>
        <div className="flex items-center gap-3">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
            <input
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Filter domains…"
              className="w-56 rounded-lg border border-white/10 bg-white/[0.02] py-2 pl-9 pr-3 text-sm text-white placeholder:text-slate-500 focus:border-brand-400/50 focus:outline-none"
            />
          </div>
          {!loading && <span className="whitespace-nowrap text-sm text-slate-500">{filteredSubjects.length} of {subjects.length}</span>}
        </div>
      </div>

      {loading && (
        <div className="mt-6 rounded-xl border border-white/10 bg-white/[0.02] p-8 text-center text-sm text-slate-400">
          Loading domains…
        </div>
      )}
      {error && <div className="mt-6 rounded-xl border border-red-400/20 bg-red-500/[0.06] p-5 text-sm text-red-200">{error}</div>}

      {!loading && !error && subjects.length === 0 && (
        <div className="mt-6 rounded-xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">
          No knowledge domains available yet.
        </div>
      )}

      {!loading && !error && subjects.length > 0 && filteredSubjects.length === 0 && (
        <div className="mt-6 rounded-xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">
          No domains match "{query}".
        </div>
      )}

      {!loading && !error && filteredSubjects.length > 0 && (
        <div className="mt-6 grid grid-cols-1 gap-px overflow-hidden rounded-xl border border-white/10 bg-white/10 sm:grid-cols-2 lg:grid-cols-3">
          {filteredSubjects.map((subject, index) => {
            const Icon = getSubjectIcon(subject.Code);
            const topics = topicsBySubject[subject.SubjectId] ?? [];
            return (
              <Link
                key={subject.SubjectId}
                to={`/ai-agents/${encodeURIComponent(subject.Name)}`}
                className="group relative flex flex-col bg-slate-950 p-6 transition hover:bg-white/[0.03]"
              >
                <div className="flex items-start justify-between gap-4">
                  <span className="font-mono text-xs text-slate-600">{String(index + 1).padStart(2, '0')}</span>
                  <div className="flex h-11 w-11 items-center justify-center rounded-lg border border-white/10 bg-white/[0.03] text-brand-300">
                    <Icon className="h-5 w-5" />
                  </div>
                </div>
                <h3 className="mt-5 text-lg font-semibold text-white">{subject.Name}</h3>
                <div className="mt-2 flex items-center gap-1.5 text-xs text-slate-500">
                  <Layers3 className="h-3.5 w-3.5" />
                  <span><strong className="font-semibold text-slate-300">{topics.length}</strong> ontological type{topics.length === 1 ? '' : 's'}</span>
                </div>
                {topics.length > 0 && (
                  <div className="mt-4 flex flex-wrap gap-1.5">
                    {topics.slice(0, 3).map((topic) => (
                      <span key={topic.StructuredLearningTopicId} className="rounded-md border border-white/10 bg-white/[0.02] px-2 py-1 text-[11px] text-slate-400">
                        {topic.Name}
                      </span>
                    ))}
                    {topics.length > 3 && <span className="rounded-md border border-white/10 px-2 py-1 text-[11px] text-slate-500">+{topics.length - 3}</span>}
                  </div>
                )}
                <div className="mt-5 flex items-center gap-1.5 text-xs font-medium text-slate-500 transition group-hover:text-brand-300">
                  Explore domain <ArrowRight className="h-3.5 w-3.5 transition group-hover:translate-x-0.5" />
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}

