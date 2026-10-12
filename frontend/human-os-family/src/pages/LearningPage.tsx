import { useState } from 'react';
import {
  GraduationCap,
  Award,
  Clock,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';

export function LearningPage() {
  const { t, lang } = useLanguage();
  const { courses, members, selectedMemberId } = useFamily();
  const [filterCategory, setFilterCategory] = useState<'all' | 'college' | 'stem' | 'languages'>('all');

  const filteredCourses = courses.filter((c) => {
    if (selectedMemberId && c.memberId !== selectedMemberId) return false;
    if (filterCategory === 'college' && !c.titleEs.toLowerCase().includes('ap') && !c.titleEs.toLowerCase().includes('ensayos')) return false;
    if (filterCategory === 'stem' && !c.titleEs.toLowerCase().includes('python') && !c.titleEs.toLowerCase().includes('ia') && !c.titleEs.toLowerCase().includes('bio')) return false;
    if (filterCategory === 'languages' && !c.titleEs.toLowerCase().includes('español')) return false;
    return true;
  });

  const universities = [
    {
      name: 'Stanford University',
      location: 'California, USA',
      programEs: 'Biomédica & Ciencias de la Salud',
      programEn: 'Biomedical & Health Sciences',
      readiness: 78,
      statusEs: 'Objetivo Principal (Reach)',
      statusEn: 'Top Reach Target',
      satRange: '1520 - 1580',
    },
    {
      name: 'Tecnológico de Monterrey',
      location: 'Monterrey / CDMX, México',
      programEs: 'Médico Cirujano & Biotecnología',
      programEn: 'Doctor of Medicine & Biotechnology',
      readiness: 94,
      statusEs: 'Admisión Altamente Probable (Safety/Match)',
      statusEn: 'High Probability Target (Match)',
      satRange: '1420+ / PAA 1400+',
    },
    {
      name: 'Johns Hopkins University',
      location: 'Maryland, USA',
      programEs: 'Pre-Medicina & Neurociencias',
      programEn: 'Pre-Med & Neuroscience Track',
      readiness: 75,
      statusEs: 'Objetivo de Alto Rendimiento',
      statusEn: 'High Performance Target',
      satRange: '1530 - 1580',
    },
    {
      name: 'Columbia University',
      location: 'New York, USA',
      programEs: 'Biología & Humanidades Globales',
      programEn: 'Biology & Global Core Curriculum',
      readiness: 72,
      statusEs: 'Admisión Regular (Reach)',
      statusEn: 'Regular Decision (Reach)',
      satRange: '1510 - 1570',
    },
  ];

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {t('learning.title')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-100 px-3 py-1 text-xs font-bold text-amber-800 dark:bg-amber-950/60 dark:text-amber-300">
              <GraduationCap className="h-3 w-3" />
              {lang === 'es' ? 'Rutas USA & LATAM' : 'USA & LATAM Tracks'}
            </span>
          </div>
          <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300">
            {t('learning.subtitle')}
          </p>
        </div>

        {/* Filter buttons */}
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setFilterCategory('all')}
            className={`rounded-xl px-3 py-1.5 text-xs font-semibold transition ${
              filterCategory === 'all'
                ? 'bg-amber-500 text-white shadow-xs'
                : 'bg-white text-warm-700 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-200'
            }`}
          >
            {t('learning.tabAll')}
          </button>
          <button
            type="button"
            onClick={() => setFilterCategory('college')}
            className={`rounded-xl px-3 py-1.5 text-xs font-semibold transition ${
              filterCategory === 'college'
                ? 'bg-amber-500 text-white shadow-xs'
                : 'bg-white text-warm-700 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-200'
            }`}
          >
            {t('learning.tabCollege')}
          </button>
          <button
            type="button"
            onClick={() => setFilterCategory('stem')}
            className={`rounded-xl px-3 py-1.5 text-xs font-semibold transition ${
              filterCategory === 'stem'
                ? 'bg-amber-500 text-white shadow-xs'
                : 'bg-white text-warm-700 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-200'
            }`}
          >
            {t('learning.tabStem')}
          </button>
          <button
            type="button"
            onClick={() => setFilterCategory('languages')}
            className={`rounded-xl px-3 py-1.5 text-xs font-semibold transition ${
              filterCategory === 'languages'
                ? 'bg-amber-500 text-white shadow-xs'
                : 'bg-white text-warm-700 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-200'
            }`}
          >
            {t('learning.tabLanguages')}
          </button>
        </div>
      </div>

      {/* Active Learning Pathways */}
      <div className="space-y-4">
        <h2 className="text-lg font-bold text-warm-900 dark:text-white">
          {t('learning.activeTracks')}
        </h2>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {filteredCourses.map((c) => {
            const member = members.find((m) => m.id === c.memberId);
            return (
              <div
                key={c.id}
                className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs transition hover:shadow-md dark:border-white/10 dark:bg-[#0f172a]"
              >
                <div className="flex items-start justify-between gap-4">
                  <div className="flex items-center gap-3">
                    <div className={`flex h-10 w-10 items-center justify-center rounded-2xl bg-gradient-to-br ${member?.avatarBg ?? 'from-warm-500 to-warm-700'} text-xs font-bold text-white shadow-xs`}>
                      {member?.initial}
                    </div>
                    <div>
                      <h3 className="text-sm font-bold text-warm-900 dark:text-white">
                        {lang === 'es' ? c.titleEs : c.titleEn}
                      </h3>
                      <p className="text-xs text-warm-500">
                        {c.institution} · <strong>{member?.name.split(' ')[0]}</strong>
                      </p>
                    </div>
                  </div>

                  <span className="rounded-full bg-amber-100 px-2.5 py-1 text-[11px] font-bold text-amber-900 dark:bg-amber-500/20 dark:text-amber-300">
                    {lang === 'es' ? c.badgeEs : c.badgeEn}
                  </span>
                </div>

                <div className="mt-4">
                  <div className="flex items-center justify-between text-xs font-bold">
                    <span className="text-warm-600 dark:text-warm-300">{c.targetLevel}</span>
                    <span className="text-amber-600 dark:text-amber-400">{c.progressPercent}%</span>
                  </div>

                  <div className="mt-2 h-2.5 w-full overflow-hidden rounded-full bg-warm-100 dark:bg-white/10">
                    <div
                      className="h-full rounded-full bg-gradient-to-r from-amber-500 to-orange-500"
                      style={{ width: `${c.progressPercent}%` }}
                    />
                  </div>
                </div>

                <div className="mt-4 flex items-center justify-between border-t border-warm-100 pt-3 text-xs text-warm-500 dark:border-white/10 dark:text-warm-400">
                  <span className="flex items-center gap-1 font-medium">
                    <Clock className="h-3.5 w-3.5" />
                    {c.hoursPerWeek} hrs/{lang === 'es' ? 'semana' : 'week'}
                  </span>
                  <span className="rounded-md bg-warm-100 px-2 py-0.5 text-[10px] font-semibold text-warm-700 dark:bg-white/5 dark:text-warm-300">
                    {c.regionFocus}
                  </span>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* Top University Admissions Readiness Tracker (for Sofía) */}
      <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between border-b border-warm-100 pb-4 dark:border-white/10">
          <div>
            <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
              <Award className="h-5 w-5 text-amber-500" />
              {lang === 'es'
                ? 'Monitoreo de Admisiones Universitaria (Sofía · Clase 2027)'
                : 'University Admissions Readiness (Sofía · Class of 2027)'}
            </h3>
            <p className="text-xs text-warm-500">
              {lang === 'es'
                ? 'Análisis predictivo de perfil académico, puntajes SAT y actividades extracurriculares'
                : 'Predictive analysis based on GPA, SAT targets, and bicultural leadership essays'}
            </p>
          </div>

          <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-bold text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300">
            SAT Target: 1540+
          </span>
        </div>

        <div className="mt-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {universities.map((uni, idx) => (
            <div
              key={idx}
              className="rounded-2xl border border-warm-100 bg-warm-50/70 p-4 transition hover:bg-white hover:shadow-xs dark:border-white/5 dark:bg-white/[0.02]"
            >
              <span className="text-[10px] font-bold text-amber-700 dark:text-amber-400 uppercase tracking-wider">
                {lang === 'es' ? uni.statusEs : uni.statusEn}
              </span>
              <h4 className="text-sm font-bold text-warm-900 dark:text-white mt-1">
                {uni.name}
              </h4>
              <p className="text-[11px] text-warm-400">{uni.location}</p>

              <p className="text-xs text-warm-700 dark:text-warm-300 font-medium mt-2">
                {lang === 'es' ? uni.programEs : uni.programEn}
              </p>

              <div className="mt-3 pt-3 border-t border-warm-200/60 dark:border-white/5">
                <div className="flex items-center justify-between text-xs font-bold">
                  <span className="text-warm-500">{lang === 'es' ? 'Probabilidad:' : 'Match:'}</span>
                  <span className="text-emerald-600 dark:text-emerald-400">{uni.readiness}%</span>
                </div>
                <div className="mt-1.5 h-1.5 w-full rounded-full bg-warm-200 dark:bg-white/10 overflow-hidden">
                  <div
                    className="h-full bg-emerald-500 rounded-full"
                    style={{ width: `${uni.readiness}%` }}
                  />
                </div>
                <span className="mt-2 block text-[10px] text-warm-400 text-right">
                  Rango SAT: {uni.satRange}
                </span>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
