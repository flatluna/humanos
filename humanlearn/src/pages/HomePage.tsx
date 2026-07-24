import { Link } from 'react-router-dom';
import { useEffect, useState } from 'react';
import {
  Landmark,
  ChefHat,
  Users,
  PawPrint,
  FlaskConical,
  Globe2,
  Sigma,
  LayoutGrid,
  Waypoints,
  type LucideIcon,
} from 'lucide-react';
import { getSubjects, Subject } from '../lib/api/subjectsApi';
import { getCapabilities } from '../lib/api/capabilitiesApi';
import { useI18n } from '../i18n';
import MemoryGraphView from '../components/MemoryGraphView';

/** Real, professional icon set (lucide-react) mapped by Subject code —
 * replaces the earlier emoji placeholders, which read as childish. */
const SUBJECT_ICONS: Record<string, LucideIcon> = {
  finanzas: Landmark,
  cocina: ChefHat,
  'recursos-humanos': Users,
  animales: PawPrint,
  ciencia: FlaskConical,
  geografia: Globe2,
  matematicas: Sigma,
};

/** Deterministic color accent per subject (same treatment as the capability
 * cards on SubjectCapabilitiesPage.tsx) so the home grid isn't a flat wall
 * of identical white/gray cards. */
const ACCENT_GRADIENTS = [
  'from-indigo-500 to-blue-600',
  'from-emerald-500 to-teal-600',
  'from-rose-500 to-pink-600',
  'from-amber-500 to-orange-600',
  'from-violet-500 to-purple-600',
  'from-sky-500 to-cyan-600',
];

function accentFor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  return ACCENT_GRADIENTS[hash % ACCENT_GRADIENTS.length];
}

type ViewMode = 'grid' | 'graph';

/**
 * Home — Paso 2 del roadmap. Lista de Subjects (tema/dominio de
 * navegación del alumno). Stub local hasta que exista el backend real
 * (ver subjectsApi.ts).
 *
 * Redesign (2026-07-21): dropped the marketing-style hero banner in favor
 * of a compact app-dashboard header (stats inline, no full-bleed gradient
 * block) — navigation now lives permanently in the Sidebar, not here. Adds
 * a Grid/Mapa toggle: "Mapa" renders MemoryGraphView, a Neo4j-Browser-
 * style "memory graph" (Materia → sus Capabilities, node color = mastery
 * progress) built with Cytoscape.js — see MemoryGraphView.tsx for the
 * library choice rationale (NVL is Neo4j-only per its license; Cytoscape
 * is the free/open-source way to get that same look).
 */
export default function HomePage() {
  const { t, language } = useI18n();
  const [subjects, setSubjects] = useState<Subject[]>([]);
  const [courseCount, setCourseCount] = useState<number | null>(null);
  const [view, setView] = useState<ViewMode>('graph');
  const [memoryStats, setMemoryStats] = useState<{ mastered: number; total: number } | null>(null);

  useEffect(() => {
    getSubjects(language).then(setSubjects);
  }, [language]);

  useEffect(() => {
    getCapabilities()
      .then((capabilities) => setCourseCount(capabilities.length))
      .catch(() => setCourseCount(null));
  }, []);

  return (
    <div className="mx-auto max-w-5xl p-6 sm:p-8">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-semibold tracking-tight text-white">
            {view === 'graph' ? t.homeMemoryTitle : t.homeQuestion}
          </h1>
          <p className="mt-1 text-sm text-slate-400">
            {view === 'graph'
              ? memoryStats
                ? `${memoryStats.mastered} ${t.homeMemoryMasteredOf} ${memoryStats.total} ${t.homeMemoryMasteredLabel}`
                : t.homeMemorySubtitle
              : `${subjects.length} materias · ${courseCount ?? '—'} cursos disponibles`}
          </p>
        </div>

        <div className="flex rounded-xl border border-white/10 bg-white/[0.04] p-1">
          <button
            onClick={() => setView('graph')}
            className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition ${
              view === 'graph' ? 'bg-gradient-to-r from-brand-500 to-accent-500 text-[#fff] shadow-sm' : 'text-slate-400 hover:text-white'
            }`}
          >
            <Waypoints className="h-3.5 w-3.5" />
            Mapa
          </button>
          <button
            onClick={() => setView('grid')}
            className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition ${
              view === 'grid' ? 'bg-gradient-to-r from-brand-500 to-accent-500 text-[#fff] shadow-sm' : 'text-slate-400 hover:text-white'
            }`}
          >
            <LayoutGrid className="h-3.5 w-3.5" />
            Grid
          </button>
        </div>
      </div>

      {view === 'graph' ? (
        <MemoryGraphView onStats={setMemoryStats} />
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {subjects.map((subject) => {
            const Icon = SUBJECT_ICONS[subject.code] ?? Globe2;
            const accent = accentFor(subject.code);
            return (
              <Link
                key={subject.code}
                to={`/subjects/${subject.code}`}
                className="group flex flex-col gap-3 overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03] p-6 transition-all hover:-translate-y-1 hover:border-white/20 hover:bg-white/[0.06] hover:shadow-2xl hover:shadow-brand-500/10"
              >
                <div
                  className={`flex h-11 w-11 items-center justify-center rounded-lg bg-gradient-to-br text-[#fff] ${accent}`}
                >
                  <Icon size={22} strokeWidth={1.75} />
                </div>
                <div>
                  <div className="font-semibold text-white group-hover:text-brand-300">
                    {subject.name}
                  </div>
                  <p className="mt-1 text-sm leading-snug text-slate-400">{subject.description}</p>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}


