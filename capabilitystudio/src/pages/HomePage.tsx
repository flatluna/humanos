import { useEffect, useMemo, useState } from 'react';
import { Search, Sparkles, Layers } from 'lucide-react';
import { getCapabilities, type BackendCapability } from '../lib/api/capabilitiesApi';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import CapabilityCard from '../components/CapabilityCard';
import SubjectPill from '../components/SubjectPill';
import LoadingSpinner from '../components/LoadingSpinner';
import { getSubjectIcon } from '../lib/subjectVisuals';

export default function HomePage() {
  const [capabilities, setCapabilities] = useState<BackendCapability[]>([]);
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeSubject, setActiveSubject] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([getCapabilities(), getSubjects()])
      .then(([caps, subs]) => {
        if (cancelled) return;
        setCapabilities(caps);
        setSubjects(subs);
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
  }, []);

  const subjectsInUse = useMemo(() => {
    const codes = new Set(capabilities.map((c) => c.SubjectCode).filter(Boolean));
    return subjects.filter((s) => codes.has(s.Code));
  }, [capabilities, subjects]);

  const filtered = useMemo(() => {
    return capabilities.filter((c) => {
      if (activeSubject && c.SubjectCode !== activeSubject) return false;
      if (search.trim()) {
        const q = search.trim().toLowerCase();
        if (!c.Name.toLowerCase().includes(q) && !(c.LearningSummary ?? '').toLowerCase().includes(q)) {
          return false;
        }
      }
      return true;
    });
  }, [capabilities, activeSubject, search]);

  return (
    <div>
      <section className="relative overflow-hidden border-b border-white/10">
        <div className="absolute inset-0 bg-mesh-gradient opacity-60" />
        <div className="relative mx-auto max-w-7xl px-6 py-20 text-center">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/5 px-3.5 py-1.5 text-xs font-medium text-slate-300">
            <Sparkles className="h-3.5 w-3.5 text-brand-400" />
            Motor v2 · Curador → GraphArchitect
          </span>
          <h1 className="mt-6 text-4xl sm:text-5xl font-bold tracking-tight text-white">
            Convierte cualquier PDF en un{' '}
            <span className="shimmer-text">curso vivo</span>
          </h1>
          <p className="mx-auto mt-4 max-w-2xl text-lg text-slate-400">
            Explora el catálogo de capabilities ya generadas o crea una nueva a partir de tu propio material.
          </p>

          <div className="mx-auto mt-8 max-w-xl">
            <div className="relative">
              <Search className="pointer-events-none absolute left-4 top-1/2 h-4.5 w-4.5 -translate-y-1/2 text-slate-500" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Buscar por nombre o tema..."
                className="w-full rounded-2xl border border-white/10 bg-white/[0.04] py-3.5 pl-11 pr-4 text-sm text-white placeholder:text-slate-500 backdrop-blur-xl focus:border-brand-400/50 focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              />
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-6 py-10">
        {subjectsInUse.length > 0 && (
          <div className="mb-8 flex gap-2 overflow-x-auto pb-2">
            <SubjectPill label="Todos" active={activeSubject === null} onClick={() => setActiveSubject(null)} icon={<Layers className="h-3.5 w-3.5" />} />
            {subjectsInUse.map((subject) => {
              const Icon = getSubjectIcon(subject.Code);
              return (
                <SubjectPill
                  key={subject.SubjectId}
                  label={subject.Name}
                  active={activeSubject === subject.Code}
                  onClick={() => setActiveSubject(subject.Code)}
                  icon={<Icon className="h-3.5 w-3.5" />}
                />
              );
            })}
          </div>
        )}

        {loading && <LoadingSpinner label="Cargando catálogo..." />}

        {!loading && error && (
          <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-center text-red-300">
            No se pudo cargar el catálogo: {error}
          </div>
        )}

        {!loading && !error && filtered.length === 0 && (
          <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-400">
            No hay capabilities que coincidan con tu búsqueda todavía.
          </div>
        )}

        {!loading && !error && filtered.length > 0 && (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {filtered.map((capability) => (
              <CapabilityCard key={capability.CapabilityId} capability={capability} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
