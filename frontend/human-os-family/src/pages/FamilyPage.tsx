import { Link } from 'react-router';
import {
  Users,
  Heart,
  Globe2,
  Plus,
  Compass,
  CheckCircle2,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';

export function FamilyPage() {
  const { t, lang } = useLanguage();
  const { members, householdName } = useFamily();

  const activeProjects = [
    {
      titleEs: 'Viaje de Inmersión Cultural Oaxaca - Austin 2027',
      titleEn: 'Oaxaca - Austin Cultural Immersion & Volunteering 2027',
      descEs: 'Proyecto bicultural donde Sofía y Mateo liderarán un taller de robótica para niños en una escuela comunitaria en Oaxaca, reconectando con la familia extendida.',
      descEn: 'Bicultural initiative where Sofía and Mateo lead a robotics workshop for youth in a Oaxaca community school, reconnecting with extended family.',
      progress: 60,
      lead: 'Carmen & Carlos',
      tag: 'Cultura & Servicio',
    },
    {
      titleEs: 'Libro de Historia Oral "Raíces Mendoza-Ríos"',
      titleEn: '"Raíces Mendoza-Ríos" Family Oral History Book',
      descEs: 'Compilación de historias de vida, cartas de los abuelos, recetas tradicionales y lecciones de superación grabadas en video y transcritas con IA.',
      descEn: 'Compilation of life stories, ancestral letters, heritage recipes, and resilience lessons recorded and transcribed with AI.',
      progress: 85,
      lead: 'Abuela Carmen',
      tag: 'Legado Familiar',
    },
    {
      titleEs: 'Laboratorio STEM & Makerspace del Hogar',
      titleEn: 'Household Makerspace & STEM Discovery Lab',
      descEs: 'Estación de trabajo equipada con impresora 3D, sensores Arduino y microscopio digital para los experimentos escolares de Mateo y Sofía.',
      descEn: 'Dedicated workspace equipped with 3D printer, Arduino sensors, and digital microscope for Sofía and Mateo independent science projects.',
      progress: 92,
      lead: 'Carlos & Mateo',
      tag: 'Ciencia & Innovación',
    },
  ];

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Page Header */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {t('family.title')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-100 px-3 py-1 text-xs font-bold text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300">
              <Users className="h-3 w-3" />
              {members.length} {lang === 'es' ? 'Integrantes' : 'Members'}
            </span>
          </div>
          <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300">
            {t('family.subtitle')}
          </p>
        </div>

        <Link
          to="/family/new"
          className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 px-5 py-3 text-xs font-bold text-white shadow-md shadow-indigo-600/30 transition hover:from-indigo-700 hover:to-violet-700 hover:shadow-lg"
        >
          <Plus className="h-4 w-4" />
          <span>{lang === 'es' ? '+ Nuevo Integrante Familiar' : '+ New Family Member'}</span>
        </Link>
      </div>

      {/* Household Identity Banner */}
      <div className="rounded-3xl border border-warm-200/80 bg-gradient-to-br from-indigo-900 via-indigo-950 to-warm-950 p-6 text-white shadow-lg">
        <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
          <div className="space-y-2">
            <span className="inline-flex items-center gap-2 rounded-full bg-white/10 px-3 py-1 text-xs font-semibold backdrop-blur-sm text-indigo-200">
              <Globe2 className="h-3.5 w-3.5 text-indigo-300" />
              {lang === 'es' ? 'Familia Global Bicultural' : 'Global Bicultural Household'}
            </span>
            <h2 className="text-2xl font-black">{householdName}</h2>
            <p className="text-xs text-indigo-200 max-w-2xl leading-relaxed">
              {lang === 'es'
                ? 'Conectando raíces mexicanas en CDMX y proyección profesional en Austin, Texas. Nuestro compromiso es el aprendizaje continuo, la salud mental y la excelencia con propósito.'
                : 'Connecting Mexican roots in Mexico City with global innovation in Austin, Texas. Dedicated to lifelong learning, holistic wellness, and purpose-driven excellence.'}
            </p>
          </div>

          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            <div className="rounded-2xl bg-white/10 p-3 backdrop-blur-xs text-center">
              <p className="text-lg font-bold text-white">5</p>
              <p className="text-[10px] text-indigo-200 font-medium">{lang === 'es' ? 'Generaciones unidas' : 'Family Generations'}</p>
            </div>
            <div className="rounded-2xl bg-white/10 p-3 backdrop-blur-xs text-center">
              <p className="text-lg font-bold text-white">100%</p>
              <p className="text-[10px] text-indigo-200 font-medium">{lang === 'es' ? 'Hogar Bilingüe' : 'Dual Language'}</p>
            </div>
            <div className="col-span-2 sm:col-span-1 rounded-2xl bg-white/10 p-3 backdrop-blur-xs text-center">
              <p className="text-lg font-bold text-white">Austin / CDMX</p>
              <p className="text-[10px] text-indigo-200 font-medium">{lang === 'es' ? 'Hubs Familiares' : 'Family Hubs'}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Members Cards */}
      <div className="space-y-4">
        <h2 className="text-lg font-bold text-warm-900 dark:text-white">
          {t('family.membersList')}
        </h2>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {members.map((member) => (
            <div
              key={member.id}
              className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-center gap-4">
                  <div className={`flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br ${member.avatarBg} text-xl font-black text-white shadow-md`}>
                    {member.initial}
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-warm-900 dark:text-white">
                      {member.name}
                    </h3>
                    <p className="text-xs font-semibold text-indigo-600 dark:text-indigo-400">
                      {lang === 'es' ? member.roleTitleEs : member.roleTitleEn}
                    </p>
                    <p className="text-[11px] text-warm-500 dark:text-warm-400">
                      📍 {member.primaryLocation} · {member.age} {lang === 'es' ? 'años' : 'years old'}
                    </p>
                  </div>
                </div>

                <span className="rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                  {member.weeklyHoursLogged} hrs/sem
                </span>
              </div>

              {/* Education / Role details */}
              <div className="mt-5 space-y-3 border-t border-warm-100 pt-4 dark:border-white/10 text-xs">
                <div>
                  <span className="font-semibold text-warm-400 uppercase tracking-wider text-[10px]">
                    {lang === 'es' ? 'Institución & Nivel:' : 'Institution & Level:'}
                  </span>
                  <p className="font-medium text-warm-800 dark:text-warm-200 mt-0.5">
                    {lang === 'es' ? member.educationLevelEs : member.educationLevelEn}
                  </p>
                  <p className="text-warm-500 text-[11px]">
                    {lang === 'es' ? member.institutionEs : member.institutionEn}
                  </p>
                </div>

                <div>
                  <span className="font-semibold text-warm-400 uppercase tracking-wider text-[10px]">
                    {lang === 'es' ? 'Idiomas de Dominio:' : 'Languages Spoken:'}
                  </span>
                  <div className="mt-1 flex flex-wrap gap-1.5">
                    {member.languages.map((lng, i) => (
                      <span
                        key={i}
                        className="rounded-md bg-indigo-50 px-2 py-0.5 text-[11px] font-medium text-indigo-700 dark:bg-indigo-950/50 dark:text-indigo-300"
                      >
                        🗣️ {lng}
                      </span>
                    ))}
                  </div>
                </div>

                <div>
                  <span className="font-semibold text-warm-400 uppercase tracking-wider text-[10px]">
                    {lang === 'es' ? 'Enfoque & Fortalezas:' : 'Key Focus & Strengths:'}
                  </span>
                  <div className="mt-1 flex flex-wrap gap-1.5">
                    {(lang === 'es' ? member.topFocusEs : member.topFocusEn).map((foc, i) => (
                      <span
                        key={i}
                        className="rounded-md bg-warm-100 px-2 py-0.5 text-[11px] font-medium text-warm-800 dark:bg-white/5 dark:text-warm-200"
                      >
                        ✨ {foc}
                      </span>
                    ))}
                  </div>
                </div>

                <div className="rounded-xl bg-warm-50 p-3 dark:bg-white/[0.02]">
                  <span className="text-[10px] font-bold text-warm-500 uppercase tracking-wider">
                    {lang === 'es' ? 'Hito 2026-2027:' : '2026-2027 Milestone:'}
                  </span>
                  <p className="font-bold text-emerald-800 dark:text-emerald-400 mt-0.5">
                    🎯 {lang === 'es' ? member.targetMilestoneEs : member.targetMilestoneEn}
                  </p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Household Values & Collective Projects */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Household Agreements */}
        <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <div className="flex items-center gap-2 border-b border-warm-100 pb-4 dark:border-white/10">
            <Heart className="h-5 w-5 text-rose-500" />
            <h3 className="text-base font-bold text-warm-900 dark:text-white">
              {t('family.valuesTitle')}
            </h3>
          </div>

          <div className="mt-4 space-y-4">
            <div className="rounded-2xl border border-rose-100 bg-rose-50/40 p-4 dark:border-white/5 dark:bg-white/[0.02]">
              <h4 className="text-xs font-bold text-rose-900 dark:text-rose-300 flex items-center gap-1.5">
                <CheckCircle2 className="h-4 w-4 text-rose-600" />
                {t('family.value1.title')}
              </h4>
              <p className="mt-1 text-xs text-warm-600 dark:text-warm-300 leading-relaxed">
                {t('family.value1.desc')}
              </p>
            </div>

            <div className="rounded-2xl border border-emerald-100 bg-emerald-50/40 p-4 dark:border-white/5 dark:bg-white/[0.02]">
              <h4 className="text-xs font-bold text-emerald-900 dark:text-emerald-300 flex items-center gap-1.5">
                <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                {t('family.value2.title')}
              </h4>
              <p className="mt-1 text-xs text-warm-600 dark:text-warm-300 leading-relaxed">
                {t('family.value2.desc')}
              </p>
            </div>

            <div className="rounded-2xl border border-indigo-100 bg-indigo-50/40 p-4 dark:border-white/5 dark:bg-white/[0.02]">
              <h4 className="text-xs font-bold text-indigo-900 dark:text-indigo-300 flex items-center gap-1.5">
                <CheckCircle2 className="h-4 w-4 text-indigo-600" />
                {t('family.value3.title')}
              </h4>
              <p className="mt-1 text-xs text-warm-600 dark:text-warm-300 leading-relaxed">
                {t('family.value3.desc')}
              </p>
            </div>
          </div>
        </div>

        {/* Collective Projects */}
        <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <div className="flex items-center gap-2 border-b border-warm-100 pb-4 dark:border-white/10">
            <Compass className="h-5 w-5 text-indigo-600" />
            <h3 className="text-base font-bold text-warm-900 dark:text-white">
              {t('family.projectsTitle')}
            </h3>
          </div>

          <div className="mt-4 space-y-4">
            {activeProjects.map((p, idx) => (
              <div
                key={idx}
                className="rounded-2xl border border-warm-100 bg-warm-50/60 p-4 dark:border-white/5 dark:bg-white/[0.02]"
              >
                <div className="flex items-center justify-between">
                  <span className="rounded-md bg-indigo-100 px-2 py-0.5 text-[10px] font-bold text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300">
                    {p.tag}
                  </span>
                  <span className="text-xs font-bold text-indigo-600 dark:text-indigo-400">
                    {p.progress}% {lang === 'es' ? 'Completado' : 'Done'}
                  </span>
                </div>

                <h4 className="mt-2 text-xs font-bold text-warm-900 dark:text-white">
                  {lang === 'es' ? p.titleEs : p.titleEn}
                </h4>
                <p className="mt-1 text-xs text-warm-600 dark:text-warm-300 leading-relaxed">
                  {lang === 'es' ? p.descEs : p.descEn}
                </p>

                <div className="mt-3 flex items-center justify-between text-[11px] text-warm-400 border-t border-warm-200/60 pt-2 dark:border-white/5">
                  <span>{lang === 'es' ? 'Líder del proyecto:' : 'Project Lead:'} <strong>{p.lead}</strong></span>
                  <div className="h-1.5 w-24 rounded-full bg-warm-200 dark:bg-white/10 overflow-hidden">
                    <div
                      className="h-full bg-indigo-600 rounded-full"
                      style={{ width: `${p.progress}%` }}
                    />
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
