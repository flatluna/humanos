import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  Landmark,
  ChefHat,
  Users,
  PawPrint,
  FlaskConical,
  Globe2,
  Sigma,
  Home,
  Sparkles,
  type LucideIcon,
} from 'lucide-react';
import { getSubjects, type Subject } from '../../lib/api/subjectsApi';
import { useI18n } from '../../i18n';

/** URL of the separate `humanstudio` app (course-creation/authoring tool). */
const STUDIO_URL = 'http://localhost:3000';

const SUBJECT_ICONS: Record<string, LucideIcon> = {
  finanzas: Landmark,
  cocina: ChefHat,
  'recursos-humanos': Users,
  animales: PawPrint,
  ciencia: FlaskConical,
  geografia: Globe2,
  matematicas: Sigma,
};

/**
 * Persistent left navigation (2026-07-21 app-shell redesign) — replaces
 * the earlier "landing page" feel (giant hero + no persistent nav) with a
 * always-visible Materias list, matching a real app (Notion/Linear-style
 * sidebar) rather than a marketing page. Subjects are fetched once here
 * (same GET /subjects as HomePage.tsx) so the list is available on every
 * route, not just "/".
 */
export default function Sidebar() {
  const { language } = useI18n();
  const { subjectCode } = useParams();
  const [subjects, setSubjects] = useState<Subject[]>([]);

  useEffect(() => {
    getSubjects(language).then(setSubjects);
  }, [language]);

  return (
    <aside className="hidden w-60 flex-none flex-col border-r border-white/10 bg-white/[0.02] sm:flex">
      <nav className="flex flex-1 flex-col gap-6 overflow-y-auto p-4">
        <Link
          to="/"
          className={`flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition ${
            !subjectCode ? 'bg-white/10 text-white' : 'text-slate-400 hover:bg-white/5 hover:text-white'
          }`}
        >
          <Home className="h-4 w-4" />
          Inicio
        </Link>

        <div>
          <p className="mb-1.5 px-3 text-xs font-semibold uppercase tracking-wider text-slate-500">
            Materias
          </p>
          <div className="flex flex-col gap-0.5">
            {subjects.map((subject) => {
              const Icon = SUBJECT_ICONS[subject.code] ?? Globe2;
              const isActive = subjectCode === subject.code;
              return (
                <Link
                  key={subject.code}
                  to={`/subjects/${subject.code}`}
                  className={`flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition ${
                    isActive ? 'bg-white/10 text-white' : 'text-slate-400 hover:bg-white/5 hover:text-white'
                  }`}
                >
                  <Icon className="h-4 w-4 flex-none" strokeWidth={1.75} />
                  <span className="truncate">{subject.name}</span>
                </Link>
              );
            })}
          </div>
        </div>
      </nav>

      <div className="border-t border-white/10 p-4">
        <a
          href={STUDIO_URL}
          target="_blank"
          rel="noreferrer"
          className="flex items-center gap-2.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-3 py-2.5 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
        >
          <Sparkles className="h-4 w-4" />
          Human OS Studio
        </a>
      </div>
    </aside>
  );
}
