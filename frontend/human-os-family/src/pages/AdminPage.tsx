import { useState } from 'react';
import {
  Settings,
  DollarSign,
  Shield,
  Globe2,
  CheckCircle2,
  Sliders,
  Save,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';

export function AdminPage() {
  const { t, lang } = useLanguage();
  const { activeCurrency, setActiveCurrency } = useFamily();
  const [digitalSunsetTime, setDigitalSunsetTime] = useState('21:00');
  const [savedNotification, setSavedNotification] = useState(false);

  const budgetItems = [
    {
      categoryEs: 'Matrículas Escolares & Programas IB / AP',
      categoryEn: 'School Tuition & IB / AP Programs',
      costUsd: 14000,
      costMxn: 280000,
      allocatedTo: 'Sofía & Mateo',
      status: '68% ejercido',
    },
    {
      categoryEs: 'Tutores Privados & Preparación SAT Digital',
      categoryEn: 'Private Tutoring & Digital SAT Coaching',
      costUsd: 4500,
      costMxn: 90000,
      allocatedTo: 'Sofía',
      status: '80% ejercido',
    },
    {
      categoryEs: 'Campamentos de Verano & Viaje Bicultural Oaxaca',
      categoryEn: 'Summer Camps & Oaxaca Bicultural Immersion',
      costUsd: 5000,
      costMxn: 100000,
      allocatedTo: 'Toda la Familia',
      status: '40% ejercido',
    },
    {
      categoryEs: 'Equipamiento STEM, Robótica FLL & Piano',
      categoryEn: 'STEM Lab Supplies, FLL Robotics & Music',
      costUsd: 2000,
      costMxn: 40000,
      allocatedTo: 'Mateo & Sofía',
      status: '90% ejercido',
    },
    {
      categoryEs: 'Suscripciones de IA, EngramsIQ & Plataformas',
      categoryEn: 'AI Software, EngramsIQ & Educational Tools',
      costUsd: 1500,
      costMxn: 30000,
      allocatedTo: 'Toda la Familia',
      status: '100% activo',
    },
  ];

  const integrations = [
    {
      name: 'Google Classroom & Canvas LMS',
      service: 'Westlake High & Sycamore Middle',
      status: 'Conectado',
      active: true,
    },
    {
      name: 'CollegeBoard & Khan Academy SAT',
      service: 'Puntajes y simulacros en tiempo real',
      status: 'Conectado',
      active: true,
    },
    {
      name: 'Apple Health / Garmin Family Telemetry',
      service: 'Monitoreo de horas de sueño y descanso',
      status: 'Conectado',
      active: true,
    },
    {
      name: 'Texas 529 & Bank Portal',
      service: 'Sincronización de aportaciones de ahorro',
      status: 'Conectado',
      active: true,
    },
  ];

  const handleSave = () => {
    setSavedNotification(true);
    setTimeout(() => setSavedNotification(false), 2500);
  };

  return (
    <div className="space-y-8 animate-fade-in">
      {/* Header */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {t('admin.title')}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-slate-100 px-3 py-1 text-xs font-bold text-slate-800 dark:bg-white/10 dark:text-white">
              <Settings className="h-3 w-3" />
              {lang === 'es' ? 'Panel de Control' : 'Control Panel'}
            </span>
          </div>
          <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300">
            {t('admin.subtitle')}
          </p>
        </div>

        <button
          type="button"
          onClick={handleSave}
          className="inline-flex items-center gap-2 rounded-2xl bg-warm-900 px-4 py-2.5 text-xs font-bold text-white shadow-md transition hover:bg-warm-800 dark:bg-white dark:text-warm-900 dark:hover:bg-warm-100"
        >
          <Save className="h-4 w-4" />
          <span>{lang === 'es' ? 'Guardar Cambios' : 'Save Preferences'}</span>
        </button>
      </div>

      {savedNotification && (
        <div className="rounded-2xl bg-emerald-50 border border-emerald-200 p-4 text-xs font-bold text-emerald-800 dark:bg-emerald-950/60 dark:border-emerald-500/30 dark:text-emerald-300">
          ✓ {lang === 'es' ? 'Preferencias familiares actualizadas exitosamente.' : 'Family household settings successfully updated.'}
        </div>
      )}

      {/* Educational Budget Section */}
      <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between border-b border-warm-100 pb-4 dark:border-white/10">
          <div className="flex items-center gap-3">
            <DollarSign className="h-6 w-6 text-emerald-600" />
            <div>
              <h3 className="text-base font-bold text-warm-900 dark:text-white">
                {t('admin.budgetSection')}
              </h3>
              <p className="text-xs text-warm-500">
                {lang === 'es'
                  ? 'Presupuesto total proyectado 2026: $27,000 USD (~$540,000 MXN)'
                  : 'Total 2026 projected education budget: $27,000 USD (~$540,000 MXN)'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold text-warm-500">{lang === 'es' ? 'Moneda:' : 'Currency:'}</span>
            <button
              type="button"
              onClick={() => setActiveCurrency(activeCurrency === 'USD' ? 'MXN' : 'USD')}
              className="rounded-xl border border-warm-200 bg-warm-50 px-3 py-1 text-xs font-bold text-warm-900 dark:border-white/10 dark:bg-warm-800 dark:text-white"
            >
              {activeCurrency === 'USD' ? '$ USD (Dólares)' : '$ MXN (Pesos)'}
            </button>
          </div>
        </div>

        <div className="mt-6 overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-warm-100 text-warm-400 dark:border-white/10">
                <th className="pb-3 font-bold uppercase tracking-wider">{lang === 'es' ? 'Categoría' : 'Category'}</th>
                <th className="pb-3 font-bold uppercase tracking-wider">{lang === 'es' ? 'Asignado a' : 'Beneficiary'}</th>
                <th className="pb-3 font-bold uppercase tracking-wider">{lang === 'es' ? 'Presupuesto Anual' : 'Annual Budget'}</th>
                <th className="pb-3 font-bold uppercase tracking-wider">{lang === 'es' ? 'Estado' : 'Status'}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-warm-100 dark:divide-white/5">
              {budgetItems.map((item, idx) => (
                <tr key={idx} className="hover:bg-warm-50/50 dark:hover:bg-white/[0.02]">
                  <td className="py-3.5 font-semibold text-warm-900 dark:text-white">
                    {lang === 'es' ? item.categoryEs : item.categoryEn}
                  </td>
                  <td className="py-3.5 text-warm-600 dark:text-warm-300">
                    {item.allocatedTo}
                  </td>
                  <td className="py-3.5 font-bold text-emerald-600 dark:text-emerald-400">
                    {activeCurrency === 'USD'
                      ? `$${item.costUsd.toLocaleString()} USD`
                      : `$${item.costMxn.toLocaleString()} MXN`}
                  </td>
                  <td className="py-3.5">
                    <span className="rounded-md bg-warm-100 px-2 py-0.5 text-[10px] font-semibold text-warm-700 dark:bg-white/10 dark:text-warm-300">
                      {item.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Permissions & Bicultural Settings */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Members Permissions */}
        <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
            <Shield className="h-5 w-5 text-indigo-500" />
            {t('admin.membersPermissions')}
          </h3>
          <p className="text-xs text-warm-500 mt-1">
            {lang === 'es' ? 'Control de acceso parental y protección de privacidad para menores' : 'Parental access control and minor data protection'}
          </p>

          <div className="mt-4 space-y-3">
            <div className="flex items-center justify-between rounded-2xl border border-warm-100 bg-warm-50/60 p-3.5 dark:border-white/5 dark:bg-white/[0.02]">
              <div>
                <p className="text-xs font-bold text-warm-900 dark:text-white">
                  Carlos & Elena (Padres / Co-Admins)
                </p>
                <p className="text-[11px] text-warm-500">
                  {lang === 'es' ? 'Acceso administrativo total: finanzas, telemetría y suscripción' : 'Full admin access: finances, telemetry, subscriptions'}
                </p>
              </div>
              <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-[10px] font-bold text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300">
                Super Admin
              </span>
            </div>

            <div className="flex items-center justify-between rounded-2xl border border-warm-100 bg-warm-50/60 p-3.5 dark:border-white/5 dark:bg-white/[0.02]">
              <div>
                <p className="text-xs font-bold text-warm-900 dark:text-white">
                  Sofía & Mateo (Hijos)
                </p>
                <p className="text-[11px] text-warm-500">
                  {lang === 'es' ? 'Perfil estudiantil: Metas, Family Coach y rutas de aprendizaje' : 'Student profile: Goals, Family Coach, and learning tracks'}
                </p>
              </div>
              <span className="rounded-full bg-indigo-100 px-2.5 py-0.5 text-[10px] font-bold text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300">
                Estudiante Pro
              </span>
            </div>

            <div className="flex items-center justify-between rounded-2xl border border-warm-100 bg-warm-50/60 p-3.5 dark:border-white/5 dark:bg-white/[0.02]">
              <div>
                <p className="text-xs font-bold text-warm-900 dark:text-white">
                  Abuela Carmen (Mentora Cultural)
                </p>
                <p className="text-[11px] text-warm-500">
                  {lang === 'es' ? 'Acceso a proyectos de historia familiar y lecturas' : 'Access to family heritage projects and reading clubs'}
                </p>
              </div>
              <span className="rounded-full bg-rose-100 px-2.5 py-0.5 text-[10px] font-bold text-rose-800 dark:bg-rose-950/60 dark:text-rose-300">
                Mentor
              </span>
            </div>
          </div>
        </div>

        {/* Regional & Timezone settings */}
        <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
            <Globe2 className="h-5 w-5 text-emerald-600" />
            {t('admin.biculturalSettings')}
          </h3>
          <p className="text-xs text-warm-500 mt-1">
            {lang === 'es' ? 'Sincronización horaria entre Texas y México' : 'Timezone sync between Texas and Mexico'}
          </p>

          <div className="mt-4 space-y-4 text-xs">
            <div>
              <label className="font-bold text-warm-700 dark:text-warm-300 block mb-1">
                {lang === 'es' ? 'Zona Horaria Primaria:' : 'Primary Household Timezone:'}
              </label>
              <select className="w-full rounded-xl border border-warm-200 bg-warm-50 p-2.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white">
                <option>Central Time (US/Canada & CDMX) - UTC-5 / UTC-6</option>
                <option>Eastern Time (Miami / Bogotá) - UTC-5</option>
                <option>Pacific Time (Silicon Valley / Tijuana) - UTC-7 / UTC-8</option>
              </select>
            </div>

            <div>
              <label className="font-bold text-warm-700 dark:text-warm-300 block mb-1">
                {lang === 'es' ? 'Horario de "Digital Sunset" (Desconexión de Pantallas):' : 'Digital Sunset Screen-Off Time:'}
              </label>
              <input
                type="time"
                value={digitalSunsetTime}
                onChange={(e) => setDigitalSunsetTime(e.target.value)}
                className="w-full rounded-xl border border-warm-200 bg-warm-50 p-2.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
              />
            </div>

            <div className="rounded-2xl border border-emerald-100 bg-emerald-50/50 p-4 dark:border-white/5 dark:bg-white/[0.02]">
              <p className="font-bold text-emerald-900 dark:text-emerald-300">
                {lang === 'es' ? 'Política Bilingüe Activa' : 'Active Dual Language Policy'}
              </p>
              <p className="mt-1 text-[11px] text-warm-600 dark:text-warm-300 leading-relaxed">
                {lang === 'es'
                  ? 'Todas las notificaciones y recomendaciones de Family Coach se emiten en formato bilingüe para reforzar el español formal en los jóvenes.'
                  : 'All notifications and AI Coach summaries are formatted bilingually to reinforce academic Spanish fluency.'}
              </p>
            </div>
          </div>
        </div>
      </div>

      {/* Integrations Section */}
      <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
        <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
          <Sliders className="h-5 w-5 text-indigo-500" />
          {t('admin.integrations')}
        </h3>
        <p className="text-xs text-warm-500 mt-1">
          {lang === 'es' ? 'Conexión con portales escolares de Austin, CollegeBoard y dispositivos inteligentes' : 'Connected school portals, CollegeBoard, and IoT health trackers'}
        </p>

        <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
          {integrations.map((item, idx) => (
            <div
              key={idx}
              className="flex items-center justify-between rounded-2xl border border-warm-100 bg-warm-50/60 p-4 dark:border-white/5 dark:bg-white/[0.02]"
            >
              <div>
                <h4 className="text-xs font-bold text-warm-900 dark:text-white">
                  {item.name}
                </h4>
                <p className="text-[11px] text-warm-500 mt-0.5">{item.service}</p>
              </div>

              <span className="flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-1 text-[10px] font-bold text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300">
                <CheckCircle2 className="h-3 w-3 text-emerald-600" />
                {item.status}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
