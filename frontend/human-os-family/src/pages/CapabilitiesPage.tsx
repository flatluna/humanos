import { useState } from 'react';
import {
  Layers,
  Search,
  Sparkles,
  Clock,
  ArrowRight,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';
import { HUMAN_STAGES, CAPABILITIES_CATALOG, HumanCapability } from '../data/mockCapabilitiesData';

const DOMAIN_STYLES: Record<string, { chip: string; text: string; bg: string }> = {
  'STEM & Math': {
    chip: 'bg-amber-100 text-amber-900 dark:bg-amber-500/20 dark:text-amber-300',
    text: 'text-amber-700 dark:text-amber-300',
    bg: 'from-amber-500 to-orange-500',
  },
  'Language & Literacy': {
    chip: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-500/20 dark:text-emerald-300',
    text: 'text-emerald-700 dark:text-emerald-300',
    bg: 'from-emerald-500 to-teal-600',
  },
  'Socio-Emotional': {
    chip: 'bg-rose-100 text-rose-900 dark:bg-rose-500/20 dark:text-rose-300',
    text: 'text-rose-700 dark:text-rose-300',
    bg: 'from-rose-500 to-pink-600',
  },
  'Critical Thinking': {
    chip: 'bg-indigo-100 text-indigo-900 dark:bg-indigo-500/20 dark:text-indigo-300',
    text: 'text-indigo-700 dark:text-indigo-300',
    bg: 'from-indigo-600 to-violet-600',
  },
  'Practical Life': {
    chip: 'bg-teal-100 text-teal-900 dark:bg-teal-500/20 dark:text-teal-300',
    text: 'text-teal-700 dark:text-teal-300',
    bg: 'from-teal-600 to-cyan-600',
  },
};

const MASTERY_BADGES: Record<string, { labelEs: string; labelEn: string; style: string }> = {
  Foundation: {
    labelEs: 'Nivel Inicial / Foundation',
    labelEn: 'Foundation Level',
    style: 'bg-slate-100 text-slate-700 dark:bg-white/10 dark:text-slate-300',
  },
  Proficient: {
    labelEs: 'Competente / Proficient',
    labelEn: 'Proficient',
    style: 'bg-blue-100 text-blue-800 dark:bg-blue-500/20 dark:text-blue-300',
  },
  Advanced: {
    labelEs: 'Avanzado / Advanced',
    labelEn: 'Advanced',
    style: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-500/20 dark:text-indigo-300',
  },
  Master: {
    labelEs: 'Maestría / Master 🌟',
    labelEn: 'Mastery 🌟',
    style: 'bg-emerald-100 text-emerald-900 dark:bg-emerald-500/20 dark:text-emerald-300 font-bold',
  },
};

export function CapabilitiesPage() {
  const { lang } = useLanguage();
  const { members, setIsAiCoachOpen } = useFamily();

  const [selectedStageKey, setSelectedStageKey] = useState<string>('elementary_mid'); // Default to 2nd & 3rd elementary
  const [selectedDomain, setSelectedDomain] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [selectedCapabilityModal, setSelectedCapabilityModal] = useState<HumanCapability | null>(null);

  const currentStage = HUMAN_STAGES.find((s) => s.key === selectedStageKey) || HUMAN_STAGES[0];

  // Filter capabilities by stage, domain, search
  const filteredCapabilities = CAPABILITIES_CATALOG.filter((c) => {
    if (selectedStageKey !== 'all_stages' && c.stageKey !== selectedStageKey) return false;
    if (selectedDomain !== 'all' && c.domain !== selectedDomain) return false;
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const matchName = c.nameEs.toLowerCase().includes(q) || c.nameEn.toLowerCase().includes(q);
      const matchCode = c.code.toLowerCase().includes(q);
      const matchDesc = c.descriptionEs.toLowerCase().includes(q) || c.descriptionEn.toLowerCase().includes(q);
      const matchSkills = c.skillsCovered.some((s) => s.toLowerCase().includes(q));
      if (!matchName && !matchCode && !matchDesc && !matchSkills) return false;
    }
    return true;
  });

  return (
    <div className="space-y-8 animate-fade-in pb-16">
      {/* Header */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
              {lang === 'es' ? 'Capabilities & Skills por Nivel Humano' : 'Capabilities & Skills by Life Stage'}
            </h1>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-violet-100 px-3 py-1 text-xs font-bold text-violet-800 dark:bg-violet-950/60 dark:text-violet-300">
              <Layers className="h-3 w-3" />
              HumanOS Architecture
            </span>
          </div>
          <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300 max-w-4xl">
            {lang === 'es'
              ? 'Catálogo estructurado de capabilities requeridas para maestría en cada etapa del desarrollo, desde Elementary School hasta la madurez profesional y ejecutiva.'
              : 'Structured catalog of core capabilities required for mastery across every human development milestone, from early elementary to executive adulthood.'}
          </p>
        </div>

        <button
          type="button"
          onClick={() => setIsAiCoachOpen(true)}
          className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-violet-600 to-indigo-600 px-4 py-2.5 text-xs font-bold text-white shadow-md shadow-violet-600/20 transition hover:from-violet-700 hover:to-indigo-700"
        >
          <Sparkles className="h-4 w-4" />
          <span>{lang === 'es' ? 'Mapear Skills con AI Coach' : 'Map Skills with AI Coach'}</span>
        </button>
      </div>

      {/* STAGE SELECTOR (Timeline of Human Development) */}
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <p className="text-xs font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
            {lang === 'es' ? 'Línea de Tiempo del Desarrollo Humano & Grados Escolares:' : 'Human Development Timeline & Grade Levels:'}
          </p>
          <button
            type="button"
            onClick={() => setSelectedStageKey('all_stages')}
            className={`text-xs font-bold transition ${
              selectedStageKey === 'all_stages'
                ? 'text-violet-600 underline dark:text-violet-400'
                : 'text-warm-500 hover:text-warm-800 dark:text-warm-400'
            }`}
          >
            {lang === 'es' ? 'Ver todos los niveles juntos' : 'View all stages combined'}
          </button>
        </div>

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
          {HUMAN_STAGES.map((stage) => {
            const isSelected = selectedStageKey === stage.key;
            return (
              <button
                key={stage.id}
                type="button"
                onClick={() => setSelectedStageKey(stage.key)}
                className={`relative flex flex-col justify-between rounded-2xl border p-3.5 text-left transition-all ${
                  isSelected
                    ? 'border-violet-500 bg-violet-50/80 shadow-md ring-2 ring-violet-500/30 dark:border-violet-400 dark:bg-violet-950/40'
                    : 'border-warm-200/80 bg-white hover:border-warm-300 hover:bg-warm-50/70 dark:border-white/10 dark:bg-[#0f172a] dark:hover:bg-white/5'
                }`}
              >
                <div>
                  <span className="text-2xl">{stage.icon}</span>
                  <h3 className={`mt-2 text-xs font-bold leading-snug ${isSelected ? 'text-violet-950 dark:text-violet-200' : 'text-warm-900 dark:text-white'}`}>
                    {lang === 'es' ? stage.nameEs : stage.nameEn}
                  </h3>
                </div>

                <div className="mt-3 pt-2 border-t border-warm-100 dark:border-white/5">
                  <span className="block text-[10px] font-semibold text-warm-500 dark:text-warm-400">
                    {stage.ageRange}
                  </span>
                  <span className="block text-[9px] uppercase font-bold text-violet-600 dark:text-violet-400">
                    {stage.categoryGroup}
                  </span>
                </div>
              </button>
            );
          })}
        </div>
      </div>

      {/* CURRENT STAGE HIGHLIGHT BANNER */}
      {selectedStageKey !== 'all_stages' && (
        <div className="rounded-3xl border border-warm-200/80 bg-gradient-to-r from-violet-50 via-indigo-50 to-white p-5 shadow-xs dark:border-white/10 dark:from-violet-950/30 dark:via-warm-900 dark:to-warm-900">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3.5">
              <span className="text-3xl">{currentStage.icon}</span>
              <div>
                <div className="flex items-center gap-2">
                  <h2 className="text-base font-bold text-warm-900 dark:text-white">
                    {lang === 'es' ? currentStage.nameEs : currentStage.nameEn}
                  </h2>
                  <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${currentStage.badgeBg}`}>
                    {currentStage.ageRange}
                  </span>
                </div>
                <p className="mt-1 text-xs text-warm-600 dark:text-warm-300 max-w-3xl">
                  {lang === 'es' ? currentStage.descriptionEs : currentStage.descriptionEn}
                </p>
              </div>
            </div>

            <span className="text-xs font-black text-violet-700 dark:text-violet-300 bg-white dark:bg-warm-800 px-3 py-1.5 rounded-xl border border-violet-200 dark:border-white/10 shrink-0">
              {filteredCapabilities.length} {lang === 'es' ? 'Capabilities Requeridas' : 'Required Capabilities'}
            </span>
          </div>
        </div>
      )}

      {/* FILTER & SEARCH BAR */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        {/* Search */}
        <div className="relative flex-1 max-w-md">
          <Search className="absolute left-3.5 top-2.5 h-4 w-4 text-warm-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={
              lang === 'es'
                ? 'Buscar capability por nombre, código (ej: ELEM-MATH) o skill...'
                : 'Search capability by name, code (e.g. ELEM-MATH), or skill...'
            }
            className="w-full rounded-2xl border border-warm-200 bg-white py-2 pl-9 pr-4 text-xs text-warm-900 focus:border-violet-500 focus:outline-none dark:border-white/10 dark:bg-warm-900 dark:text-white"
          />
        </div>

        {/* Domain Filters */}
        <div className="flex flex-wrap items-center gap-1.5">
          {['all', 'STEM & Math', 'Language & Literacy', 'Socio-Emotional', 'Critical Thinking', 'Practical Life'].map((dom) => (
            <button
              key={dom}
              type="button"
              onClick={() => setSelectedDomain(dom)}
              className={`rounded-xl px-3 py-1.5 text-xs font-semibold transition ${
                selectedDomain === dom
                  ? 'bg-violet-600 text-white shadow-xs font-bold'
                  : 'bg-white text-warm-700 hover:bg-warm-100 dark:bg-warm-800 dark:text-warm-300 dark:hover:bg-warm-700'
              }`}
            >
              {dom === 'all' ? (lang === 'es' ? 'Todos los Dominios' : 'All Domains') : dom}
            </button>
          ))}
        </div>
      </div>

      {/* CAPABILITIES GRID */}
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
        {filteredCapabilities.map((cap) => {
          const domainStyle = DOMAIN_STYLES[cap.domain] ?? DOMAIN_STYLES['STEM & Math'];
          const mastery = MASTERY_BADGES[cap.targetMasteryLevel] ?? MASTERY_BADGES.Proficient;

          return (
            <div
              key={cap.id}
              className="group relative flex flex-col justify-between rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs transition hover:-translate-y-0.5 hover:shadow-md dark:border-white/10 dark:bg-[#0f172a]"
            >
              <div>
                {/* Header: Code & Domain & Mastery */}
                <div className="flex items-center justify-between gap-2 flex-wrap">
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-xs font-bold text-warm-600 dark:text-warm-400 bg-warm-100 dark:bg-white/10 px-2 py-0.5 rounded-md">
                      {cap.code}
                    </span>
                    <span className={`rounded-md px-2 py-0.5 text-[11px] font-bold ${domainStyle.chip}`}>
                      {cap.domain}
                    </span>
                  </div>

                  <span className={`rounded-full px-2.5 py-0.5 text-[11px] ${mastery.style}`}>
                    {lang === 'es' ? mastery.labelEs : mastery.labelEn}
                  </span>
                </div>

                {/* Capability Name & Description */}
                <h3 className="mt-3 text-base font-bold text-warm-900 dark:text-white group-hover:text-violet-600 dark:group-hover:text-violet-400 transition">
                  {lang === 'es' ? cap.nameEs : cap.nameEn}
                </h3>

                <p className="mt-1.5 text-xs text-warm-600 dark:text-warm-300 leading-relaxed">
                  {lang === 'es' ? cap.descriptionEs : cap.descriptionEn}
                </p>

                {/* Why it Matters (Pedagogical insight like HumanOS) */}
                <div className="mt-3 rounded-2xl bg-warm-50 p-3.5 dark:bg-white/[0.02] border border-warm-100 dark:border-white/5">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-warm-400">
                    💡 {lang === 'es' ? 'Por qué es clave para el futuro:' : 'Why it matters for future success:'}
                  </span>
                  <p className="text-xs font-medium text-warm-800 dark:text-warm-200 mt-0.5">
                    {lang === 'es' ? cap.whyItMattersEs : cap.whyItMattersEn}
                  </p>
                </div>

                {/* Micro-Skills Covered */}
                <div className="mt-3 pt-2">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-warm-400">
                    {lang === 'es' ? 'Competencias Específicas / Micro-Skills:' : 'Specific Micro-Skills Covered:'}
                  </span>
                  <div className="mt-1.5 flex flex-wrap gap-1.5">
                    {cap.skillsCovered.map((s, idx) => (
                      <span
                        key={idx}
                        className="rounded-lg bg-warm-100/80 px-2 py-0.5 text-[10px] font-semibold text-warm-700 dark:bg-white/5 dark:text-warm-300"
                      >
                        ✓ {s}
                      </span>
                    ))}
                  </div>
                </div>
              </div>

              {/* Card Footer: Hours & Action */}
              <div className="mt-5 flex items-center justify-between border-t border-warm-100 pt-3 text-xs text-warm-500 dark:border-white/10 dark:text-warm-400">
                <span className="flex items-center gap-1 font-medium">
                  <Clock className="h-3.5 w-3.5 text-warm-400" />
                  ~{cap.recommendedHoursToMaster} hrs {lang === 'es' ? 'para maestría' : 'to mastery'}
                </span>

                <button
                  type="button"
                  onClick={() => setSelectedCapabilityModal(cap)}
                  className="inline-flex items-center gap-1 text-xs font-bold text-violet-600 hover:text-violet-700 dark:text-violet-400"
                >
                  <span>{lang === 'es' ? 'Ver Detalles & Asignar' : 'View Details & Assign'}</span>
                  <ArrowRight className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          );
        })}
      </div>

      {/* DETAIL MODAL / DRAWER */}
      {selectedCapabilityModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 backdrop-blur-xs animate-fade-in">
          <div className="w-full max-w-xl rounded-3xl border border-warm-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-[#0f172a] space-y-5">
            <div className="flex items-start justify-between">
              <div>
                <span className="font-mono text-xs font-bold bg-violet-100 text-violet-800 dark:bg-violet-950/60 dark:text-violet-300 px-2 py-0.5 rounded-md">
                  {selectedCapabilityModal.code}
                </span>
                <h3 className="mt-2 text-lg font-bold text-warm-900 dark:text-white">
                  {lang === 'es' ? selectedCapabilityModal.nameEs : selectedCapabilityModal.nameEn}
                </h3>
                <p className="text-xs text-warm-500">
                  {selectedCapabilityModal.domain} · {selectedCapabilityModal.recommendedHoursToMaster} hrs recomendadas
                </p>
              </div>

              <button
                type="button"
                onClick={() => setSelectedCapabilityModal(null)}
                className="rounded-full p-1.5 text-warm-400 hover:bg-warm-100 dark:hover:bg-white/10"
              >
                ✕
              </button>
            </div>

            <p className="text-xs text-warm-700 dark:text-warm-300 leading-relaxed">
              {lang === 'es' ? selectedCapabilityModal.descriptionEs : selectedCapabilityModal.descriptionEn}
            </p>

            <div className="rounded-2xl bg-warm-50 p-4 dark:bg-white/[0.02]">
              <span className="text-[11px] font-bold text-warm-500 uppercase tracking-wider">
                {lang === 'es' ? 'Asignar a un Integrante de la Familia:' : 'Assign to Family Member:'}
              </span>
              <div className="mt-2 flex flex-wrap gap-2">
                {members.map((m) => (
                  <button
                    key={m.id}
                    type="button"
                    onClick={() => {
                      alert(
                        lang === 'es'
                          ? `Capability ${selectedCapabilityModal.code} asignada exitosamente a ${m.name}`
                          : `Capability ${selectedCapabilityModal.code} successfully assigned to ${m.name}`
                      );
                      setSelectedCapabilityModal(null);
                    }}
                    className="flex items-center gap-1.5 rounded-xl border border-warm-200 bg-white px-3 py-1.5 text-xs font-semibold text-warm-700 hover:border-violet-500 hover:bg-violet-50 dark:border-white/10 dark:bg-warm-900 dark:text-warm-300"
                  >
                    <span className={`flex h-4 w-4 items-center justify-center rounded-full bg-gradient-to-br ${m.avatarBg} text-[9px] font-bold text-white`}>
                      {m.initial}
                    </span>
                    <span>{m.name.split(' ')[0]}</span>
                  </button>
                ))}
              </div>
            </div>

            <div className="flex justify-end pt-2">
              <button
                type="button"
                onClick={() => setSelectedCapabilityModal(null)}
                className="rounded-2xl bg-warm-900 px-5 py-2 text-xs font-bold text-white hover:bg-warm-800 dark:bg-white dark:text-warm-900"
              >
                {lang === 'es' ? 'Cerrar' : 'Close'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
