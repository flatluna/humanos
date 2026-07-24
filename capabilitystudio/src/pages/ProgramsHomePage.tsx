import { useEffect, useMemo, useState } from 'react';
import { Search, GraduationCap } from 'lucide-react';
import { getPrograms, type BackendProgram } from '../lib/api/programsApi';
import ProgramCard from '../components/ProgramCard';
import LoadingSpinner from '../components/LoadingSpinner';

export default function ProgramsHomePage() {
  const [programs, setPrograms] = useState<BackendProgram[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    getPrograms()
      .then((progs) => {
        if (!cancelled) setPrograms(progs);
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

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return programs;
    return programs.filter(
      (p) => p.Name.toLowerCase().includes(q) || (p.Description ?? '').toLowerCase().includes(q),
    );
  }, [programs, search]);

  return (
    <div>
      <section className="relative overflow-hidden border-b border-white/10">
        <div className="absolute inset-0 bg-mesh-gradient opacity-60" />
        <div className="relative mx-auto max-w-7xl px-6 py-20 text-center">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/5 px-3.5 py-1.5 text-xs font-medium text-slate-300">
            <GraduationCap className="h-3.5 w-3.5 text-brand-400" />
            Programas educativos
          </span>
          <h1 className="mt-6 text-4xl sm:text-5xl font-bold tracking-tight text-white">
            Rutas de aprendizaje <span className="shimmer-text">curadas</span>
          </h1>
          <p className="mx-auto mt-4 max-w-2xl text-lg text-slate-400">
            Agrupa capabilities existentes en una secuencia recomendada con objetivos y requisitos propios.
          </p>

          <div className="mx-auto mt-8 max-w-xl">
            <div className="relative">
              <Search className="pointer-events-none absolute left-4 top-1/2 h-4.5 w-4.5 -translate-y-1/2 text-slate-500" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Buscar programa..."
                className="w-full rounded-2xl border border-white/10 bg-white/[0.04] py-3.5 pl-11 pr-4 text-sm text-white placeholder:text-slate-500 backdrop-blur-xl focus:border-brand-400/50 focus:outline-none focus:ring-2 focus:ring-brand-500/30"
              />
            </div>
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-7xl px-6 py-10">
        {loading && <LoadingSpinner label="Cargando programas..." />}

        {!loading && error && (
          <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-center text-red-300">
            No se pudo cargar el catálogo: {error}
          </div>
        )}

        {!loading && !error && filtered.length === 0 && (
          <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-400">
            No hay programas que coincidan con tu búsqueda todavía.
          </div>
        )}

        {!loading && !error && filtered.length > 0 && (
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {filtered.map((program) => (
              <ProgramCard key={program.ProgramId} program={program} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
