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
  type LucideIcon,
} from 'lucide-react';
import { getSubjects, Subject } from '../lib/api/subjectsApi';
import { useI18n } from '../i18n';

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

/**
 * Home — Paso 2 del roadmap. Lista de Subjects (tema/dominio de
 * navegación del alumno). Stub local hasta que exista el backend real
 * (ver subjectsApi.ts).
 */
export default function HomePage() {
  const { t, language } = useI18n();
  const [subjects, setSubjects] = useState<Subject[]>([]);

  useEffect(() => {
    getSubjects(language).then(setSubjects);
  }, [language]);

  return (
    <div className="mx-auto max-w-4xl p-8">
      <p className="mb-3 text-sm font-semibold uppercase tracking-wider text-blue-600">
        {t.appName}
      </p>
      <h1 className="mb-10 bg-gradient-to-r from-blue-600 via-indigo-600 to-purple-600 bg-clip-text text-4xl font-extrabold leading-tight text-transparent sm:text-5xl">
        {t.homeQuestion}
      </h1>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {subjects.map((subject) => {
          const Icon = SUBJECT_ICONS[subject.code] ?? Globe2;
          return (
            <Link
              key={subject.code}
              to={`/subjects/${subject.code}`}
              className="group flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-6 transition hover:-translate-y-0.5 hover:border-slate-300 hover:shadow-md"
            >
              <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-slate-100 text-slate-700 transition group-hover:bg-blue-50 group-hover:text-blue-600">
                <Icon size={22} strokeWidth={1.75} />
              </div>
              <div>
                <div className="font-semibold text-slate-900">{subject.name}</div>
                <p className="mt-1 text-sm leading-snug text-slate-500">{subject.description}</p>
              </div>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
