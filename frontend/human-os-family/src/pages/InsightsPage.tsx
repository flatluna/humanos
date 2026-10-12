import {
  LineChart,
  AlertTriangle,
  Sparkles,
  Clock,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';

export function InsightsPage() {
  const { t, lang } = useLanguage();
  const { setIsAiCoachOpen } = useFamily();

  const dimensions = [
    {
      nameEs: 'Bilingüismo & Cultura Dual',
      nameEn: 'Bilingual & Cultural Fluency',
      score: 96,
      color: 'bg-emerald-500',
      text: 'text-emerald-600',
    },
    {
      nameEs: 'Conexión & Armonía Familiar',
      nameEn: 'Family Bonding & Harmony',
      score: 94,
      color: 'bg-teal-500',
      text: 'text-teal-600',
    },
    {
      nameEs: 'Rigor Académico & Carrera',
      nameEn: 'Academic & Career Rigor',
      score: 91,
      color: 'bg-indigo-500',
      text: 'text-indigo-600',
    },
    {
      nameEs: 'Desarrollo Socioemocional',
      nameEn: 'Socio-Emotional Flourishing',
      score: 88,
      color: 'bg-rose-500',
      text: 'text-rose-600',
    },
    {
      nameEs: 'Salud, Sueño & Deporte',
      nameEn: 'Health, Sleep & Sports',
      score: 85,
      color: 'bg-amber-500',
      text: 'text-amber-600',
    },
  ];

  const gapAlerts = [
    {
      member: 'Sofía',
      role: 'Hija (16)',
      titleEs: 'Ensayo Personal en Español (Nivel C2 Académico)',
      titleEn: 'Academic Spanish Personal Statement (C2 Level)',
      descEs: 'Aunque su inglés para el SAT es sobresaliente (percentil 98), para las convocatorias del Tec de Monterrey y becas de liderazgo se recomienda afinar la riqueza léxica en español formal.',
      descEn: 'While her SAT verbal score is at 98th percentile, leadership scholarship applications in Mexico require refining formal academic rhetoric in Spanish.',
      actionEs: 'Asignar 2 horas semanales de redacción con Abuela Carmen.',
      actionEn: 'Schedule 2 weekly essay reviews with Abuela Carmen.',
      severity: 'medium',
    },
    {
      member: 'Mateo',
      role: 'Hijo (12)',
      titleEs: 'Gestión de Tiempo en Exámenes de Competencia AMC 8',
      titleEn: 'Time Pacing in AMC 8 Math Competitions',
      descEs: 'En las últimas pruebas simuladas resolvió con precisión los problemas difíciles, pero agotó el tiempo en los primeros 10 ejercicios por exceso de verificación.',
      descEn: 'In recent mocks he solved complex questions accurately, but spent too much time double-checking early problems.',
      actionEs: 'Práctica de agilidad cronometrada de 15 min los martes.',
      actionEn: '15-min speed drilling on Tuesday afternoons.',
      severity: 'low',
    },
    {
      member: 'Carlos & Elena',
      role: 'Padres',
      titleEs: 'Sobrecarga de Videollamadas Transfronterizas',
      titleEn: 'Cross-Border Conference Call Burnout',
      descEs: 'La coordinación entre equipos de Austin (CST) y América Latina provocó que 2 noches esta semana se extendieran juntas hasta las 9:30 PM.',
      descEn: 'Syncing teams across Austin and LATAM caused work calls to run past 9:30 PM on two occasions this week.',
      actionEs: 'Activar bloqueo estricto de agenda después de las 6:30 PM.',
      actionEn: 'Enable strict calendar block past 6:30 PM.',
      severity: 'high',
    },
  ];

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {t('insights.title')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-teal-100 px-3 py-1 text-xs font-bold text-teal-800 dark:bg-teal-950/60 dark:text-teal-300">
              <LineChart className="h-3 w-3" />
              {lang === 'es' ? 'Analítica Predictiva' : 'Predictive Analytics'}
            </span>
          </div>
          <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300">
            {t('insights.subtitle')}
          </p>
        </div>

        <button
          type="button"
          onClick={() => setIsAiCoachOpen(true)}
          className="inline-flex items-center gap-2 rounded-2xl bg-teal-600 px-4 py-2.5 text-xs font-bold text-white shadow-md shadow-teal-600/20 transition hover:bg-teal-700"
        >
          <Sparkles className="h-4 w-4" />
          <span>{lang === 'es' ? 'Generar Reporte Integral' : 'Generate Full Report'}</span>
        </button>
      </div>

      {/* Holistic Growth Radar / Progress Bars */}
      <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
        <div className="flex items-center justify-between border-b border-warm-100 pb-4 dark:border-white/10">
          <div>
            <h3 className="text-base font-bold text-warm-900 dark:text-white">
              {t('insights.radarTitle')}
            </h3>
            <p className="text-xs text-warm-500">
              {lang === 'es' ? 'Evaluación continua del bienestar, aprendizaje y armonía en el hogar' : 'Ongoing evaluation of wellness, intellect, and household harmony'}
            </p>
          </div>

          <span className="text-xs font-black text-emerald-600 dark:text-emerald-400">
            90.8 / 100 Global
          </span>
        </div>

        <div className="mt-6 space-y-4">
          {dimensions.map((dim, idx) => (
            <div key={idx} className="space-y-1.5">
              <div className="flex items-center justify-between text-xs font-bold">
                <span className="text-warm-800 dark:text-warm-200">
                  {lang === 'es' ? dim.nameEs : dim.nameEn}
                </span>
                <span className={dim.text}>{dim.score}%</span>
              </div>
              <div className="h-3 w-full rounded-full bg-warm-100 dark:bg-white/10 overflow-hidden">
                <div
                  className={`h-full ${dim.color} rounded-full transition-all duration-700`}
                  style={{ width: `${dim.score}%` }}
                />
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Screentime vs Deep Focus Analytics */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
            <Clock className="h-5 w-5 text-indigo-500" />
            {t('insights.screenVsStudy')}
          </h3>
          <p className="text-xs text-warm-500 mt-1">
            {lang === 'es' ? 'Medición semanal integrada de dispositivos familiares' : 'Weekly integrated telemetry from family devices'}
          </p>

          <div className="mt-6 grid grid-cols-2 gap-4">
            <div className="rounded-2xl border border-emerald-100 bg-emerald-50/50 p-4 text-center dark:border-white/5 dark:bg-white/[0.02]">
              <span className="text-3xl font-black text-emerald-600 dark:text-emerald-400">34.5 hrs</span>
              <p className="text-xs font-bold text-warm-800 dark:text-warm-200 mt-1">
                {lang === 'es' ? 'Estudio & Lectura' : 'Study & Reading'}
              </p>
              <p className="text-[10px] text-warm-400 mt-0.5">
                {lang === 'es' ? '76% del tiempo en pantalla' : '76% of total device time'}
              </p>
            </div>

            <div className="rounded-2xl border border-rose-100 bg-rose-50/50 p-4 text-center dark:border-white/5 dark:bg-white/[0.02]">
              <span className="text-3xl font-black text-rose-600 dark:text-rose-400">10.2 hrs</span>
              <p className="text-xs font-bold text-warm-800 dark:text-warm-200 mt-1">
                {lang === 'es' ? 'Entretenimiento / Juegos' : 'Entertainment / Gaming'}
              </p>
              <p className="text-[10px] text-warm-400 mt-0.5">
                {lang === 'es' ? 'Dentro del umbral saludable (<14 hrs)' : 'Within healthy benchmark (<14 hrs)'}
              </p>
            </div>
          </div>

          <p className="mt-4 text-xs text-warm-600 dark:text-warm-300 leading-relaxed">
            {lang === 'es'
              ? '✅ Excelente ratio de 3.4 a 1 a favor del desarrollo intelectual. El protocolo "Digital Sunset" a las 9 PM redujo el insomnio en un 80%.'
              : '✅ Exceptional 3.4 to 1 ratio favoring intellectual growth. The 9 PM Digital Sunset protocol reduced insomnia by 80%.'}
          </p>
        </div>

        {/* Learning Gaps & Early Alerts */}
        <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-amber-500" />
            {t('insights.gapsTitle')}
          </h3>
          <p className="text-xs text-warm-500 mt-1">
            {lang === 'es' ? 'Detección proactiva para evitar estrés y asegurar metas' : 'Proactive detection to prevent stress and secure milestones'}
          </p>

          <div className="mt-4 space-y-3">
            {gapAlerts.map((gap, i) => (
              <div
                key={i}
                className="rounded-2xl border border-warm-100 bg-warm-50/60 p-3.5 dark:border-white/5 dark:bg-white/[0.02]"
              >
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-warm-900 dark:text-white">
                    {gap.member} ({gap.role})
                  </span>
                  <span className={`rounded-md px-2 py-0.5 text-[10px] font-bold ${
                    gap.severity === 'high'
                      ? 'bg-rose-100 text-rose-800 dark:bg-rose-950/60 dark:text-rose-300'
                      : 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300'
                  }`}>
                    {lang === 'es' ? gap.titleEs : gap.titleEn}
                  </span>
                </div>

                <p className="text-[11px] text-warm-600 dark:text-warm-300 mt-1.5 leading-relaxed">
                  {lang === 'es' ? gap.descEs : gap.descEn}
                </p>

                <p className="mt-2 text-[11px] font-bold text-emerald-700 dark:text-emerald-400">
                  💡 {lang === 'es' ? gap.actionEs : gap.actionEn}
                </p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
