import { useState } from 'react';
import {
  Compass,
  Briefcase,
  Moon,
  DollarSign,
  Smile,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';

export function PersonalDevelopmentPage() {
  const { t, lang } = useLanguage();
  const [selectedTab, setSelectedTab] = useState<'career' | 'youth' | 'wellness' | 'finance'>('career');

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Header */}
      <div>
        <div className="flex items-center gap-3">
          <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
            {t('pdev.title')}
          </h1>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-rose-100 px-3 py-1 text-xs font-bold text-rose-800 dark:bg-rose-950/60 dark:text-rose-300">
            <Compass className="h-3 w-3" />
            {lang === 'es' ? 'Crecimiento Integral' : 'Holistic Growth'}
          </span>
        </div>
        <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300">
          {t('pdev.subtitle')}
        </p>
      </div>

      {/* Tabs */}
      <div className="flex flex-wrap gap-2 border-b border-warm-200 pb-3 dark:border-white/10">
        <button
          type="button"
          onClick={() => setSelectedTab('career')}
          className={`flex items-center gap-2 rounded-2xl px-4 py-2 text-xs font-bold transition ${
            selectedTab === 'career'
              ? 'bg-rose-600 text-white shadow-sm'
              : 'bg-white text-warm-600 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-300'
          }`}
        >
          <Briefcase className="h-4 w-4" />
          {t('pdev.tabParents')}
        </button>

        <button
          type="button"
          onClick={() => setSelectedTab('youth')}
          className={`flex items-center gap-2 rounded-2xl px-4 py-2 text-xs font-bold transition ${
            selectedTab === 'youth'
              ? 'bg-rose-600 text-white shadow-sm'
              : 'bg-white text-warm-600 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-300'
          }`}
        >
          <Smile className="h-4 w-4" />
          {t('pdev.tabYouth')}
        </button>

        <button
          type="button"
          onClick={() => setSelectedTab('wellness')}
          className={`flex items-center gap-2 rounded-2xl px-4 py-2 text-xs font-bold transition ${
            selectedTab === 'wellness'
              ? 'bg-rose-600 text-white shadow-sm'
              : 'bg-white text-warm-600 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-300'
          }`}
        >
          <Moon className="h-4 w-4" />
          {t('pdev.tabWellness')}
        </button>

        <button
          type="button"
          onClick={() => setSelectedTab('finance')}
          className={`flex items-center gap-2 rounded-2xl px-4 py-2 text-xs font-bold transition ${
            selectedTab === 'finance'
              ? 'bg-rose-600 text-white shadow-sm'
              : 'bg-white text-warm-600 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-300'
          }`}
        >
          <DollarSign className="h-4 w-4" />
          {t('pdev.tabFinance')}
        </button>
      </div>

      {/* Tab 1: Executive Career & Leadership (Parents) */}
      {selectedTab === 'career' && (
        <div className="space-y-6">
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            {/* Carlos */}
            <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
              <div className="flex items-center justify-between border-b border-warm-100 pb-3 dark:border-white/10">
                <div>
                  <h3 className="text-base font-bold text-warm-900 dark:text-white">
                    Carlos Mendoza · VP de Ingeniería & IA
                  </h3>
                  <p className="text-xs text-warm-500">
                    {lang === 'es' ? 'Plan de Desarrollo Ejecutivo 2026' : '2026 Executive Roadmap'}
                  </p>
                </div>
                <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-bold text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300">
                  En progreso
                </span>
              </div>

              <div className="mt-4 space-y-3">
                <div className="rounded-xl border border-warm-100 bg-warm-50/60 p-3.5 dark:border-white/5 dark:bg-white/[0.02]">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-bold text-warm-900 dark:text-white">
                      MIT Sloan Executive AI Fellow
                    </span>
                    <span className="text-xs font-bold text-emerald-600">65%</span>
                  </div>
                  <p className="text-[11px] text-warm-500 mt-1">
                    {lang === 'es'
                      ? 'Gobernanza de modelos de lenguaje a gran escala, impacto ético y gestión de equipos globales de software.'
                      : 'LLM enterprise governance, ethical AI frameworks, and global software engineering leadership.'}
                  </p>
                </div>

                <div className="rounded-xl border border-warm-100 bg-warm-50/60 p-3.5 dark:border-white/5 dark:bg-white/[0.02]">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-bold text-warm-900 dark:text-white">
                      {lang === 'es' ? 'Mentoría a Emprendedores Tech LATAM' : 'Tech Mentorship for LATAM Startups'}
                    </span>
                    <span className="text-xs font-bold text-emerald-600">80%</span>
                  </div>
                  <p className="text-[11px] text-warm-500 mt-1">
                    {lang === 'es'
                      ? '4 sesiones mensuales con fundadores de México y Colombia para potenciar el talento técnico regional.'
                      : '4 monthly advisory sessions with Mexican and Colombian tech founders to elevate regional talent.'}
                  </p>
                </div>
              </div>
            </div>

            {/* Elena */}
            <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
              <div className="flex items-center justify-between border-b border-warm-100 pb-3 dark:border-white/10">
                <div>
                  <h3 className="text-base font-bold text-warm-900 dark:text-white">
                    Dra. Elena Ríos · Neuropsicóloga & Directora
                  </h3>
                  <p className="text-xs text-warm-500">
                    {lang === 'es' ? 'Investigación Clínica & Dirección Médica' : 'Clinical Research & Medical Leadership'}
                  </p>
                </div>
                <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-xs font-bold text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300">
                  En progreso
                </span>
              </div>

              <div className="mt-4 space-y-3">
                <div className="rounded-xl border border-warm-100 bg-warm-50/60 p-3.5 dark:border-white/5 dark:bg-white/[0.02]">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-bold text-warm-900 dark:text-white">
                      Harvard Medical CME & Baylor Updates
                    </span>
                    <span className="text-xs font-bold text-emerald-600">91%</span>
                  </div>
                  <p className="text-[11px] text-warm-500 mt-1">
                    {lang === 'es'
                      ? 'Actualización en biomarcadores para neurodesarrollo temprano y protocolos de acompañamiento a familias biculturales.'
                      : 'Biomarkers in early neurodevelopment and clinical protocols for supporting bicultural families.'}
                  </p>
                </div>

                <div className="rounded-xl border border-warm-100 bg-warm-50/60 p-3.5 dark:border-white/5 dark:bg-white/[0.02]">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-bold text-warm-900 dark:text-white">
                      {lang === 'es' ? 'Libro: "Mente en Armonía para Niños"' : 'Book: "Harmonious Mind for Children"'}
                    </span>
                    <span className="text-xs font-bold text-emerald-600">50%</span>
                  </div>
                  <p className="text-[11px] text-warm-500 mt-1">
                    {lang === 'es'
                      ? 'Capítulo 4 completado sobre cómo blindar la concentración en la era de los algoritmos y redes sociales.'
                      : 'Chapter 4 completed on shielding focus and attention in the age of algorithms and social media.'}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Tab 2: Youth Emotional Intelligence */}
      {selectedTab === 'youth' && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {/* Sofía */}
          <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
            <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
              <Smile className="h-5 w-5 text-indigo-500" />
              Sofía (16) · {lang === 'es' ? 'Liderazgo & Oratoria Bilingüe' : 'Leadership & Bilingual Debate'}
            </h3>
            <p className="text-xs text-warm-500 mt-1">
              {lang === 'es' ? 'Fortalecimiento de habilidades socioemocionales para la universidad' : 'Building key socio-emotional skills for college'}
            </p>

            <div className="mt-4 space-y-3">
              <div className="rounded-2xl border border-indigo-100 bg-indigo-50/40 p-4 dark:border-white/5 dark:bg-white/[0.02]">
                <h4 className="text-xs font-bold text-indigo-950 dark:text-indigo-200">
                  {lang === 'es' ? 'Capitana de Debate Modelo ONU' : 'Model UN Debate Team Captain'}
                </h4>
                <p className="text-xs text-warm-600 dark:text-warm-300 mt-1">
                  {lang === 'es'
                    ? 'Liderazgo de equipo de 14 estudiantes, negociación diplomática en inglés y español, y resolución pacífica de controversias.'
                    : 'Leading a 14-student delegation, diplomatic negotiation in English and Spanish, and conflict resolution.'}
                </p>
              </div>

              <div className="rounded-2xl border border-warm-100 bg-warm-50/60 p-4 dark:border-white/5 dark:bg-white/[0.02]">
                <h4 className="text-xs font-bold text-warm-900 dark:text-white">
                  {lang === 'es' ? 'Gestión de Ansiedad ante Exámenes SAT' : 'Test Anxiety & Pressure Management'}
                </h4>
                <p className="text-xs text-warm-600 dark:text-warm-300 mt-1">
                  {lang === 'es'
                    ? 'Técnicas de respiración diafragmática aprendidas con su madre Elena; enfoque en crecimiento en lugar de perfección.'
                    : 'Diaphragmatic breathing techniques learned from Dr. Elena; focusing on growth over perfectionism.'}
                </p>
              </div>
            </div>
          </div>

          {/* Mateo */}
          <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
            <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
              <Smile className="h-5 w-5 text-amber-500" />
              Mateo (12) · {lang === 'es' ? 'Resiliencia & Trabajo en Equipo' : 'Resilience & Team Dynamics'}
            </h3>
            <p className="text-xs text-warm-500 mt-1">
              {lang === 'es' ? 'Desarrollo del carácter a través de la robótica y la música' : 'Character building through robotics and music'}
            </p>

            <div className="mt-4 space-y-3">
              <div className="rounded-2xl border border-amber-100 bg-amber-50/40 p-4 dark:border-white/5 dark:bg-white/[0.02]">
                <h4 className="text-xs font-bold text-amber-950 dark:text-amber-200">
                  {lang === 'es' ? 'Colaboración en Equipo FLL Robotics' : 'FLL Robotics Team Collaboration'}
                </h4>
                <p className="text-xs text-warm-600 dark:text-warm-300 mt-1">
                  {lang === 'es'
                    ? 'Aprendiendo a escuchar activamente a compañeros de equipo y aceptar el rediseño de piezas sin frustración.'
                    : 'Practicing active listening with team peers and iterating robot mechanisms without frustration.'}
                </p>
              </div>

              <div className="rounded-2xl border border-warm-100 bg-warm-50/60 p-4 dark:border-white/5 dark:bg-white/[0.02]">
                <h4 className="text-xs font-bold text-warm-900 dark:text-white">
                  {lang === 'es' ? 'Práctica de Piano & Paciencia' : 'Piano Discipline & Patience'}
                </h4>
                <p className="text-xs text-warm-600 dark:text-warm-300 mt-1">
                  {lang === 'es'
                    ? '20 minutos diarios de sonatinas de Clementi guiado por la Abuela Carmen, cultivando memoria motriz y calma mental.'
                    : '20 minutes of daily Clementi practice guided by Abuela Carmen, developing motor memory and mental serenity.'}
                </p>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Tab 3: Wellness & Sleep */}
      {selectedTab === 'wellness' && (
        <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <div className="flex items-center gap-3 border-b border-warm-100 pb-4 dark:border-white/10">
            <Moon className="h-6 w-6 text-indigo-500" />
            <div>
              <h3 className="text-base font-bold text-warm-900 dark:text-white">
                {lang === 'es' ? 'Protocolo Familiar de Desconexión Digital & Sueño' : 'Family Digital Sunset & Sleep Protocol'}
              </h3>
              <p className="text-xs text-warm-500">
                {lang === 'es' ? 'Diseñado por Dra. Elena para proteger el descanso y la salud cerebral' : 'Designed by Dr. Elena to protect restorative rest and brain health'}
              </p>
            </div>
          </div>

          <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
            <div className="rounded-2xl border border-warm-100 bg-warm-50 p-4 text-center dark:border-white/5 dark:bg-white/[0.02]">
              <span className="text-2xl font-black text-warm-900 dark:text-white">09:00 PM</span>
              <p className="text-xs font-bold text-indigo-600 dark:text-indigo-400 mt-1">Digital Sunset</p>
              <p className="text-[11px] text-warm-500 mt-1">
                {lang === 'es' ? 'Celulares y tablets en la estación central de carga del pasillo' : 'Phones and tablets placed in hallway charging dock'}
              </p>
            </div>

            <div className="rounded-2xl border border-warm-100 bg-warm-50 p-4 text-center dark:border-white/5 dark:bg-white/[0.02]">
              <span className="text-2xl font-black text-warm-900 dark:text-white">8.4 hrs</span>
              <p className="text-xs font-bold text-emerald-600 dark:text-emerald-400 mt-1">
                {lang === 'es' ? 'Promedio de Sueño' : 'Average Sleep'}
              </p>
              <p className="text-[11px] text-warm-500 mt-1">
                {lang === 'es' ? 'Medido en smartwatches familiares (+45 min vs semestre pasado)' : 'Tracked via smartwatches (+45 min vs last semester)'}
              </p>
            </div>

            <div className="rounded-2xl border border-warm-100 bg-warm-50 p-4 text-center dark:border-white/5 dark:bg-white/[0.02]">
              <span className="text-2xl font-black text-warm-900 dark:text-white">Sáb 9:00 AM</span>
              <p className="text-xs font-bold text-amber-600 dark:text-amber-400 mt-1">
                {lang === 'es' ? 'Deporte en Familia' : 'Family Sports'}
              </p>
              <p className="text-[11px] text-warm-500 mt-1">
                {lang === 'es' ? 'Tenis y ciclismo en el parque Lady Bird Lake / Viveros CDMX' : 'Tennis and trail cycling at Lady Bird Lake / Viveros'}
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Tab 4: Family Finances & College 529 */}
      {selectedTab === 'finance' && (
        <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <div className="flex items-center justify-between border-b border-warm-100 pb-4 dark:border-white/10">
            <div className="flex items-center gap-3">
              <DollarSign className="h-6 w-6 text-emerald-600" />
              <div>
                <h3 className="text-base font-bold text-warm-900 dark:text-white">
                  {lang === 'es' ? 'Fondo Universitario & Educación Financiera Familiar' : 'College 529 Plan & Family Wealth Literacy'}
                </h3>
                <p className="text-xs text-warm-500">
                  {lang === 'es' ? 'Plan de inversión educativa para Sofía y Mateo (USA / LATAM)' : 'College endowment strategy for Sofía and Mateo'}
                </p>
              </div>
            </div>

            <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-bold text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300">
              82% {lang === 'es' ? 'Fondo Completado' : 'Fund Funded'}
            </span>
          </div>

          <div className="mt-6 grid grid-cols-1 gap-6 md:grid-cols-2">
            <div className="rounded-2xl border border-warm-100 bg-warm-50/70 p-5 dark:border-white/5 dark:bg-white/[0.02]">
              <h4 className="text-xs font-bold text-warm-900 dark:text-white">
                🇺🇸 Texas 529 College Savings Plan
              </h4>
              <p className="text-xs text-warm-600 dark:text-warm-300 mt-1">
                {lang === 'es'
                  ? 'Asignado para matrícula en universidades de EE.UU. (Stanford, Johns Hopkins, UT Austin). Libre de impuestos para gastos educativos calificados.'
                  : 'Designated for US undergraduate tuition (Stanford, Johns Hopkins, UT Austin). Tax-free for qualified educational expenses.'}
              </p>
              <div className="mt-3 flex items-center justify-between text-xs font-bold text-emerald-700 dark:text-emerald-400">
                <span>{lang === 'es' ? 'Meta proyectada:' : 'Projected Target:'} $240,000 USD</span>
                <span>$198,000 USD (82%)</span>
              </div>
            </div>

            <div className="rounded-2xl border border-warm-100 bg-warm-50/70 p-5 dark:border-white/5 dark:bg-white/[0.02]">
              <h4 className="text-xs font-bold text-warm-900 dark:text-white">
                🇲🇽 Fideicomiso Educativo Monterrey / CDMX
              </h4>
              <p className="text-xs text-warm-600 dark:text-warm-300 mt-1">
                {lang === 'es'
                  ? 'Fondo de respaldo en México para el Tec de Monterrey / UNAM o programas de intercambio global en Europa y Asia.'
                  : 'Backup fund in Mexico for Tec de Monterrey / UNAM or international exchange programs in Europe and Asia.'}
              </p>
              <div className="mt-3 flex items-center justify-between text-xs font-bold text-emerald-700 dark:text-emerald-400">
                <span>{lang === 'es' ? 'Meta proyectada:' : 'Projected Target:'} $2,500,000 MXN</span>
                <span>$2,150,000 MXN (86%)</span>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
