import { useEffect, useState } from 'react';
import { ArrowLeft, ArrowRight, Bot, BookOpen, MessageCircle, Sparkles } from 'lucide-react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningTopics, type StructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';
import { getStructuredLearningDocument, getStructuredLearningDocuments, type StructuredLearningDocumentDetail } from '../lib/api/structuredLearningApi';
import { getSubjectGradient, getSubjectIcon } from '../lib/subjectVisuals';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

export default function AiAgentsTopicPage() {
  const { subjectName, topicName } = useParams<{ subjectName: string; topicName: string }>();
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;
  const [subject, setSubject] = useState<Subject | null>(null);
  const [topic, setTopic] = useState<StructuredLearningTopic | null>(null);
  const [courses, setCourses] = useState<StructuredLearningDocumentDetail[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadTopic() {
      setLoading(true);
      setError(null);
      try {
        const subjects = await getSubjects();
        const selectedSubject = subjects.find((item) => item.Name.toLowerCase() === decodeURIComponent(subjectName ?? '').toLowerCase());
        if (!selectedSubject) throw new Error('Domain not found.');
        const topics = await getStructuredLearningTopics(tenantId, selectedSubject.SubjectId);
        const selectedTopic = topics.find((item) => item.Name.toLowerCase() === decodeURIComponent(topicName ?? '').toLowerCase());
        if (!selectedTopic) throw new Error('Ontological type not found.');
        const summaries = await getStructuredLearningDocuments();
        const details = await Promise.all(summaries.map((item) => getStructuredLearningDocument(item.StructuredLearningDocumentId).catch(() => null)));
        if (cancelled) return;
        setSubject(selectedSubject);
        setTopic(selectedTopic);
        setCourses(details.filter((item): item is StructuredLearningDocumentDetail => item?.TopicId === selectedTopic.StructuredLearningTopicId));
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Could not load this ontological type.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadTopic();
    return () => { cancelled = true; };
  }, [subjectName, topicName, tenantId]);

  const DomainIcon = getSubjectIcon(subject?.Code);
  const agentName = topic?.Name ? `${topic.Name} Agent` : 'Domain Agent';

  return (
    <div className="mx-auto max-w-7xl px-6 py-10">
      <Link to={`/ai-agents/${encodeURIComponent(subjectName ?? '')}`} className="inline-flex items-center gap-1.5 text-sm text-slate-400 transition hover:text-white">
        <ArrowLeft className="h-4 w-4" /> {subject?.Name ?? 'Domain'}
      </Link>

      {loading && <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.03] p-8 text-center text-sm text-slate-400">Loading agents and courses...</div>}
      {error && <div className="mt-8 rounded-2xl border border-red-400/20 bg-red-500/[0.08] p-5 text-sm text-red-200">{error}</div>}

      {!loading && !error && topic && subject && (
        <>
          <div className="mt-6 flex flex-wrap items-center gap-2 text-xs text-slate-500">
            <span>AI Agents</span><span>/</span><span>{subject.Name}</span><span>/</span><span className="text-cyan-300">{topic.Name}</span>
          </div>
          <header className="mt-5">
            <div className="flex items-start gap-4">
              <div className={`flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br ${getSubjectGradient(subject.Code)} text-white shadow-lg`}><DomainIcon className="h-7 w-7" /></div>
              <div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-cyan-300">Ontological type</p><h1 className="mt-1 text-3xl font-bold tracking-tight text-white sm:text-4xl">{topic.Name}</h1><p className="mt-2 max-w-2xl text-sm text-slate-400">{topic.Description ?? `A learning network for ${topic.Name} in ${subject.Name}.`}</p></div>
            </div>
          </header>

          <section className="relative mt-8 overflow-hidden rounded-3xl border border-cyan-300/25 bg-gradient-to-br from-cyan-500/[0.18] via-blue-500/[0.10] to-white/[0.03] p-6 shadow-2xl shadow-cyan-950/30 sm:p-8">
            <div className="absolute -right-20 -top-24 h-64 w-64 rounded-full bg-cyan-300/10 blur-3xl" />
            <div className="relative flex flex-col gap-6 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex items-start gap-4"><div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-cyan-300/15 text-cyan-200 ring-1 ring-cyan-200/30"><Bot className="h-7 w-7" /></div><div><div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-cyan-200"><Sparkles className="h-3.5 w-3.5" /> General tutor</div><h2 className="mt-1 text-2xl font-bold text-white">{agentName}</h2><p className="mt-2 max-w-xl text-sm leading-6 text-slate-300">Your general {topic.Name} tutor. Ask questions across all courses, connect concepts, and get a recommended learning path.</p></div></div>
              <Link to={`/ai-agents/${encodeURIComponent(subjectName ?? '')}/${encodeURIComponent(topicName ?? '')}/chat`} className="inline-flex shrink-0 items-center justify-center gap-2 rounded-xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"><MessageCircle className="h-4 w-4" /> Talk to {agentName}</Link>
            </div>
          </section>

          <div className="mt-10 flex items-end justify-between gap-4"><div><p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Specialized tutors</p><h2 className="mt-2 text-2xl font-bold text-white">Courses in {topic.Name}</h2></div><span className="text-sm text-slate-500">{courses.length} courses</span></div>
          {courses.length === 0 ? <div className="mt-6 rounded-2xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">No courses are linked to this type yet.</div> : <div className="mt-6 grid grid-cols-1 gap-5 md:grid-cols-2">{courses.map((course) => <article key={course.StructuredLearningDocumentId} className="rounded-2xl border border-white/10 bg-white/[0.03] p-5 transition hover:border-white/20 hover:bg-white/[0.05]"><div className="flex items-start justify-between gap-4"><div className="flex h-11 w-11 items-center justify-center rounded-xl bg-white/[0.06] text-cyan-300"><BookOpen className="h-5 w-5" /></div><span className="rounded-full border border-emerald-400/20 bg-emerald-400/10 px-2.5 py-1 text-[11px] text-emerald-300">Agent ready</span></div><h3 className="mt-5 text-lg font-semibold text-white">{course.DocumentName}</h3><p className="mt-2 text-sm text-slate-400">{course.Sections.length} sections · Specialized course tutor</p><div className="mt-5 flex gap-2"><Link to={`/ai-agents/${encodeURIComponent(subjectName ?? '')}/${encodeURIComponent(topicName ?? '')}/chat?documentId=${course.StructuredLearningDocumentId}&courseName=${encodeURIComponent(course.DocumentName)}`} className="inline-flex items-center gap-2 rounded-xl bg-white/10 px-3 py-2 text-xs font-medium text-slate-300 transition hover:bg-white/20"><MessageCircle className="h-3.5 w-3.5" /> Talk to course agent</Link><Link to={`/structured-learning/${course.StructuredLearningDocumentId}`} className="inline-flex items-center gap-1 rounded-xl border border-white/10 px-3 py-2 text-xs text-slate-400 hover:text-white">View course <ArrowRight className="h-3.5 w-3.5" /></Link></div></article>)}</div>}
        </>
      )}
    </div>
  );
}
