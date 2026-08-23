import { useEffect, useMemo, useState } from 'react';
import { Bot, BookOpen, ChevronDown, Layers3, MessageCircle, Search, Sparkles } from 'lucide-react';
import { Link } from 'react-router';
import { getStructuredLearningDocuments, type StructuredLearningDocumentSummary } from '@/features/capabilities/api/structuredLearningApi';

export default function AiAgentsPage() {
  const [courses, setCourses] = useState<StructuredLearningDocumentSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [domain, setDomain] = useState('');
  const [subdomain, setSubdomain] = useState('');

  useEffect(() => {
    getStructuredLearningDocuments()
      .then(setCourses)
      .catch((err) => setError(err instanceof Error ? err.message : 'No se pudieron cargar los agentes.'))
      .finally(() => setLoading(false));
  }, []);

  const domains = useMemo(() => [...new Set(courses.map((course) => course.domainName).filter(Boolean))].sort() as string[], [courses]);
  const subdomains = useMemo(
    () => [...new Set(courses.filter((course) => !domain || course.domainName === domain).map((course) => course.subdomainName).filter(Boolean))].sort() as string[],
    [courses, domain],
  );
  const filteredCourses = useMemo(() => {
    const query = search.trim().toLowerCase();
    return courses.filter((course) =>
      (!domain || course.domainName === domain) &&
      (!subdomain || course.subdomainName === subdomain) &&
      (!query || course.documentName.toLowerCase().includes(query) || (course.executiveSummary ?? '').toLowerCase().includes(query)),
    );
  }, [courses, domain, search, subdomain]);

  const topicAgents = useMemo(() => {
    const seen = new Map<string, { subjectId: string; topicId: string; domainName: string; subdomainName: string; courseCount: number }>();
    for (const course of courses) {
      if (!course.subjectId || !course.topicId || !course.domainName || !course.subdomainName) continue;
      const existing = seen.get(course.topicId);
      if (existing) existing.courseCount += 1;
      else seen.set(course.topicId, { subjectId: course.subjectId, topicId: course.topicId, domainName: course.domainName, subdomainName: course.subdomainName, courseCount: 1 });
    }
    return [...seen.values()].sort((a, b) => a.subdomainName.localeCompare(b.subdomainName));
  }, [courses]);

  return (
    <div className="mx-auto max-w-6xl px-4 py-8 sm:px-8">
      <section className="relative overflow-hidden rounded-3xl border border-cyan-400/20 bg-gradient-to-br from-cyan-500/[0.16] via-slate-950 to-blue-500/[0.12] p-8 shadow-2xl sm:p-10">
        <div className="relative flex items-start gap-4">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-cyan-300/15 text-cyan-200 ring-1 ring-cyan-200/30">
            <Bot className="h-7 w-7" />
          </div>
          <div>
            <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.18em] text-cyan-300">
              <Sparkles className="h-3.5 w-3.5" /> Red de agentes
            </div>
            <h1 className="mt-2 text-3xl font-bold tracking-tight text-white">AI Agents</h1>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-slate-300">
              Agentes especializados en los cursos globales publicados por Human OS.
            </p>
          </div>
        </div>
      </section>

      {loading && <div className="mt-8 rounded-2xl border border-white/10 bg-white/[0.03] p-8 text-center text-sm text-slate-400">Cargando agentes...</div>}
      {error && <div className="mt-8 rounded-2xl border border-red-400/20 bg-red-500/[0.08] p-5 text-sm text-red-200">{error}</div>}
      {!loading && !error && courses.length === 0 && <div className="mt-8 rounded-2xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">No hay cursos globales publicados.</div>}

      {!loading && !error && topicAgents.length > 0 && (
        <>
          <h2 className="mt-8 text-lg font-bold text-white">Agentes por dominio/subdominio</h2>
          <p className="mt-1 text-sm text-slate-400">Un tutor general por subdominio: responde sobre todos sus cursos, conecta conceptos y recomienda una ruta de aprendizaje.</p>
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {topicAgents.map((topic) => (
              <Link key={topic.topicId} to={`/ai-agents/topic/${topic.subjectId}/${topic.topicId}?domain=${encodeURIComponent(topic.domainName)}&subdomain=${encodeURIComponent(topic.subdomainName)}`} className="group rounded-2xl border border-violet-300/20 bg-gradient-to-br from-violet-500/[0.08] to-transparent p-4 transition hover:border-violet-400/40 hover:bg-violet-500/[0.14]">
                <div className="flex items-center gap-2">
                  <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-violet-400/15 text-violet-200 ring-1 ring-violet-300/20"><Layers3 className="h-4.5 w-4.5" /></span>
                  <span className="min-w-0 text-xs font-semibold uppercase tracking-[0.14em] text-violet-300">{topic.domainName}</span>
                </div>
                <h3 className="mt-3 text-lg font-semibold text-white">{topic.subdomainName}</h3>
                <p className="mt-1 text-xs text-slate-400">{topic.courseCount} curso{topic.courseCount === 1 ? '' : 's'}</p>
                <div className="mt-3 inline-flex items-center gap-1.5 text-xs font-semibold text-violet-300 group-hover:text-violet-200"><MessageCircle className="h-3.5 w-3.5" /> Hablar con el Agente de {topic.subdomainName}</div>
              </Link>
            ))}
          </div>
        </>
      )}

      {!loading && !error && courses.length > 0 && (
        <div className="mt-8 grid gap-3 rounded-2xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-white/[0.03] md:grid-cols-[minmax(0,1fr)_12rem_12rem]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar agente o curso..." className="w-full rounded-xl border border-slate-200 bg-slate-50 py-2.5 pl-9 pr-3 text-sm text-slate-900 outline-none focus:border-cyan-400 dark:border-white/10 dark:bg-white/[0.04] dark:text-white" />
          </div>
          <label className="relative">
            <span className="sr-only">Dominio</span>
            <select value={domain} onChange={(event) => { setDomain(event.target.value); setSubdomain(''); }} className="w-full appearance-none rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 pr-8 text-sm text-slate-700 outline-none focus:border-cyan-400 dark:border-white/10 dark:bg-white/[0.04] dark:text-slate-200">
              <option value="">Todos los dominios</option>{domains.map((item) => <option key={item} value={item}>{item}</option>)}
            </select><ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          </label>
          <label className="relative">
            <span className="sr-only">Subdominio</span>
            <select value={subdomain} onChange={(event) => setSubdomain(event.target.value)} className="w-full appearance-none rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5 pr-8 text-sm text-slate-700 outline-none focus:border-cyan-400 dark:border-white/10 dark:bg-white/[0.04] dark:text-slate-200">
              <option value="">Todos los subdominios</option>{subdomains.map((item) => <option key={item} value={item}>{item}</option>)}
            </select><ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
          </label>
        </div>
      )}

      {!loading && !error && courses.length > 0 && filteredCourses.length > 0 && (
        <div className="mt-8 grid grid-cols-1 gap-5 md:grid-cols-2">
          {filteredCourses.map((course) => (
            <article key={course.structuredLearningDocumentId} className="rounded-2xl border border-white/10 bg-slate-950/[0.92] p-5 transition hover:border-cyan-400/30 hover:bg-white/[0.04]">
              <div className="flex items-start justify-between gap-4">
                <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-cyan-300/15 text-cyan-200 ring-1 ring-cyan-200/20"><BookOpen className="h-5 w-5" /></div>
                <span className="rounded-full border border-emerald-400/20 bg-emerald-400/10 px-2.5 py-1 text-[11px] text-emerald-300">Global · Publicado</span>
              </div>
              <h2 className="mt-5 text-xl font-semibold text-white">{course.documentName}</h2>
              <div className="mt-2 flex flex-wrap gap-2 text-xs">
                {course.domainName && <span className="rounded-full border border-cyan-400/20 bg-cyan-400/10 px-2.5 py-1 text-cyan-200">Dominio: {course.domainName}</span>}
                {course.subdomainName && <span className="rounded-full border border-violet-400/20 bg-violet-400/10 px-2.5 py-1 text-violet-200">Subdominio: {course.subdomainName}</span>}
              </div>
              <p className="mt-3 line-clamp-4 text-sm leading-6 text-slate-400">{course.executiveSummary ?? 'Agente especializado en este curso.'}</p>
              <div className="mt-5 flex flex-wrap gap-2">
                <Link to={`/ai-agents/course/${course.structuredLearningDocumentId}`} className="inline-flex items-center gap-2 rounded-xl bg-cyan-300 px-4 py-2.5 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200"><MessageCircle className="h-4 w-4" /> Hablar con Engram Agent</Link>
                <Link to={`/structured-learning/${course.structuredLearningDocumentId}`} className="inline-flex items-center gap-2 rounded-xl border border-white/10 px-4 py-2.5 text-sm text-slate-300 transition hover:border-white/25 hover:text-white"><BookOpen className="h-4 w-4" /> Ver curso</Link>
              </div>
            </article>
          ))}
        </div>
      )}
      {!loading && !error && courses.length > 0 && filteredCourses.length === 0 && <div className="mt-8 rounded-2xl border border-dashed border-white/15 p-10 text-center text-sm text-slate-400">No hay agentes que coincidan con esos filtros.</div>}
    </div>
  );
}
