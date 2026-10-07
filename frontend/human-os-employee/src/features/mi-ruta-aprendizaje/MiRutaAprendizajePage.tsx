import { ArrowLeft } from 'lucide-react';
import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import { TecMileniumLearningExperiencePanel } from '@/features/tec-contigo/TecMileniumLearningExperiencePanel';

/** "Mi Ruta de Desarrollo" — standalone sidebar page enabling any employee
 *  to design their personalized professional growth path. */
export function MiRutaAprendizajePage() {
  const { i18n } = useTranslation();
  const isEn = i18n.language?.startsWith('en');

  return (
    <div className="mx-auto max-w-5xl space-y-6 pb-16">
      <Link to="/" className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300">
        <ArrowLeft className="h-4 w-4" aria-hidden="true" />
        {isEn ? 'Back' : 'Volver'}
      </Link>
      <TecMileniumLearningExperiencePanel />
    </div>
  );
}
