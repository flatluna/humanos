import { ArrowLeft } from 'lucide-react';
import { Link } from 'react-router';
import { TecMileniumLearningExperiencePanel } from '@/features/tec-contigo/TecMileniumLearningExperiencePanel';

/** "Mi Ruta de Aprender" (2026-09-29) — standalone sidebar page reusing
 *  Tec Contigo's "2. Diseño de la experiencia de aprendizaje" panel
 *  (TecMileniumLearningExperiencePanel), so any employee can design their
 *  personalized learning path without going through the academic-gated
 *  /tec-contigo/mientras-estudias flow. */
export function MiRutaAprendizajePage() {
  return (
    <div className="mx-auto max-w-5xl space-y-6 pb-16">
      <Link to="/" className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        Volver
      </Link>
      <TecMileniumLearningExperiencePanel />
    </div>
  );
}
