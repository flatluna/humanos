import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpen, Check, Compass, Heart, Lightbulb, Save, Sparkles, ShieldAlert, Mountain, Gem } from 'lucide-react';
import { useParams } from 'react-router';
import { getPersonAcademicPreferences, savePersonAcademicPreferences } from '@/api/personAcademicPreferencesApi';

const freeTextSections = [
  {
    id: 'frustrations' as const,
    title: 'Mis frustraciones',
    description: 'Lo que le genera desmotivación, bloqueos o incomodidad en su proceso de aprendizaje.',
    placeholder: 'Escribe aquí las frustraciones del estudiante...',
    icon: ShieldAlert,
    color: 'text-red-600 dark:text-red-300',
    background: 'bg-red-100 dark:bg-red-500/15',
  },
  {
    id: 'mainChallenges' as const,
    title: 'Mis principales retos',
    description: 'Los obstáculos concretos que enfrenta hoy y que quiere superar.',
    placeholder: 'Escribe aquí los principales retos del estudiante...',
    icon: Mountain,
    color: 'text-orange-600 dark:text-orange-300',
    background: 'bg-orange-100 dark:bg-orange-500/15',
  },
  {
    id: 'importantValues' as const,
    title: 'Mis valores importantes',
    description: 'Los principios y valores que guían sus decisiones académicas y personales.',
    placeholder: 'Escribe aquí los valores importantes del estudiante...',
    icon: Gem,
    color: 'text-cyan-600 dark:text-cyan-300',
    background: 'bg-cyan-100 dark:bg-cyan-500/15',
  },
];

type FreeTextId = (typeof freeTextSections)[number]['id'];
type FreeTextValues = Record<FreeTextId, string>;

const initialFreeText: FreeTextValues = {
  frustrations: '',
  mainChallenges: '',
  importantValues: '',
};

const sections = [
  {
    id: 'interests',
    title: 'Intereses académicos',
    description: 'Temas, disciplinas y áreas de conocimiento que despiertan la curiosidad del estudiante.',
    options: [
      'Ciencia y tecnología',
      'Ingeniería',
      'Inteligencia artificial y ciencia de datos',
      'Programación y desarrollo de software',
      'Negocios y emprendimiento',
      'Finanzas y economía',
      'Mercadotecnia y estrategia',
      'Liderazgo y dirección',
      'Arte, diseño y arquitectura',
      'Comunicación y medios',
      'Salud y bienestar',
      'Medicina y biotecnología',
      'Ciencias sociales',
      'Derecho y políticas públicas',
      'Educación e innovación educativa',
      'Humanidades y filosofía',
      'Sostenibilidad y cambio climático',
      'Energía y medio ambiente',
      'Relaciones internacionales',
      'Turismo, hospitalidad y gastronomía',
    ],
    icon: Compass,
    color: 'text-blue-600 dark:text-blue-300',
    background: 'bg-blue-100 dark:bg-blue-500/15',
  },
  {
    id: 'preferences',
    title: 'Gustos y preferencias de aprendizaje',
    description: 'Cómo prefiere aprender: formatos, ritmo, tipo de actividades y experiencias que le ayudan a comprender mejor.',
    options: ['Videos y demostraciones', 'Lecturas y artículos', 'Ejercicios prácticos', 'Proyectos colaborativos', 'Discusiones y debates', 'Retos y simulaciones'],
    icon: Heart,
    color: 'text-rose-600 dark:text-rose-300',
    background: 'bg-rose-100 dark:bg-rose-500/15',
  },
  {
    id: 'motivations',
    title: 'Motivaciones académicas',
    description: 'Lo que impulsa su desarrollo y las metas que quiere alcanzar durante su trayectoria educativa.',
    options: ['Prepararme para mi carrera', 'Encontrar un mejor trabajo', 'Obtener mejores calificaciones', 'Desarrollar nuevas habilidades', 'Resolver problemas reales', 'Prepararme para una certificación', 'Explorar nuevas áreas'],
    icon: Lightbulb,
    color: 'text-amber-600 dark:text-amber-300',
    background: 'bg-amber-100 dark:bg-amber-500/15',
  },
  {
    id: 'growth',
    title: 'Oportunidades de crecimiento',
    description: 'Capacidades que puede fortalecer y recomendaciones para acompañar su siguiente etapa de aprendizaje.',
    options: ['Pensamiento crítico', 'Comunicación', 'Liderazgo', 'Análisis de datos', 'Programación y tecnología', 'Organización y gestión del tiempo'],
    icon: BookOpen,
    color: 'text-emerald-600 dark:text-emerald-300',
    background: 'bg-emerald-100 dark:bg-emerald-500/15',
  },
];

type SectionId = (typeof sections)[number]['id'];
type Selections = Record<SectionId, string[]>;

const initialSelections: Selections = {
  interests: [],
  preferences: [],
  motivations: [],
  growth: [],
};

export function EmployeeAcademicInterestsPage() {
  const { personId } = useParams<{ personId: string }>();
  const queryClient = useQueryClient();
  const preferencesQuery = useQuery({
    queryKey: ['person-academic-preferences', personId],
    queryFn: () => getPersonAcademicPreferences(personId!),
    enabled: Boolean(personId),
  });
  const [selections, setSelections] = useState<Selections | null>(null);
  const [freeText, setFreeText] = useState<FreeTextValues | null>(null);
  const [isSaved, setIsSaved] = useState(false);

  const saveMutation = useMutation({
    mutationFn: () =>
      savePersonAcademicPreferences(personId!, {
        academicInterests: selections?.interests ?? [],
        learningPreferences: selections?.preferences ?? [],
        academicMotivations: selections?.motivations ?? [],
        growthAreas: selections?.growth ?? [],
        frustrations: storedFreeText.frustrations,
        mainChallenges: storedFreeText.mainChallenges,
        importantValues: storedFreeText.importantValues,
      }),
    onSuccess: (saved) => {
      queryClient.setQueryData(['person-academic-preferences', personId], saved);
      setIsSaved(true);
    },
  });

  const storedSelections: Selections = selections ?? {
    interests: preferencesQuery.data?.AcademicInterests ?? initialSelections.interests,
    preferences: preferencesQuery.data?.LearningPreferences ?? initialSelections.preferences,
    motivations: preferencesQuery.data?.AcademicMotivations ?? initialSelections.motivations,
    growth: preferencesQuery.data?.GrowthAreas ?? initialSelections.growth,
  };

  const storedFreeText: FreeTextValues = freeText ?? {
    frustrations: preferencesQuery.data?.Frustrations ?? initialFreeText.frustrations,
    mainChallenges: preferencesQuery.data?.MainChallenges ?? initialFreeText.mainChallenges,
    importantValues: preferencesQuery.data?.ImportantValues ?? initialFreeText.importantValues,
  };

  function updateFreeText(id: FreeTextId, value: string) {
    setIsSaved(false);
    setFreeText((current) => ({ ...(current ?? storedFreeText), [id]: value }));
  }

  function toggleOption(sectionId: SectionId, option: string) {
    setIsSaved(false);
    setSelections((current) => {
      const base = current ?? storedSelections;
      const selected = base[sectionId];
      return {
        ...base,
        [sectionId]: selected.includes(option) ? selected.filter((item) => item !== option) : [...selected, option],
      };
    });
  }

  const selectedCount = Object.values(storedSelections).reduce((total, values) => total + values.length, 0);

  return (
    <div className="mx-auto max-w-6xl space-y-6 p-6 pb-16 sm:p-8">
      <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-slate-950 via-blue-950 to-indigo-900 p-7 text-white shadow-xl shadow-blue-950/20 sm:p-10">
        <div className="flex items-start gap-4">
          <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/20">
            <Sparkles className="h-7 w-7" aria-hidden="true" />
          </span>
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-blue-200">Perfil académico</p>
            <h1 className="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">Intereses, Gustos y Preferencias</h1>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-blue-100 sm:text-base">
              Conoce al estudiante desde su perspectiva académica para acompañar mejor sus decisiones, su aprendizaje y su desarrollo.
            </p>
          </div>
        </div>
      </section>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          saveMutation.mutate();
        }}
        className="space-y-5"
      >
        <div className="grid gap-4 md:grid-cols-2">
        {sections.map(({ id, title, description, options, icon: Icon, color, background }) => (
          <fieldset key={id} className="rounded-2xl border border-slate-200/80 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
            <div className="flex items-start gap-4">
              <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${background}`}>
                <Icon className={`h-5 w-5 ${color}`} aria-hidden="true" />
              </span>
              <div>
                <h2 className="text-base font-bold text-slate-950 dark:text-white">{title}</h2>
                <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">{description}</p>
              </div>
            </div>
            <div className="mt-5 grid gap-2 sm:grid-cols-2">
              {options.map((option) => {
                const selected = storedSelections[id].includes(option);
                return (
                  <label
                    key={option}
                    className={`flex cursor-pointer items-center gap-3 rounded-xl border px-3 py-3 text-sm font-medium transition ${
                      selected
                        ? 'border-blue-500 bg-blue-50 text-blue-800 ring-1 ring-blue-500 dark:border-blue-400 dark:bg-blue-500/15 dark:text-blue-100'
                        : 'border-slate-200 text-slate-600 hover:border-blue-300 hover:bg-slate-50 dark:border-white/10 dark:text-white/70 dark:hover:bg-white/[0.05]'
                    }`}
                  >
                    <input
                      type="checkbox"
                      checked={selected}
                      onChange={() => toggleOption(id, option)}
                      className="sr-only"
                    />
                    <span className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-md border ${selected ? 'border-blue-600 bg-blue-600 text-white' : 'border-slate-300 dark:border-white/30'}`}>
                      {selected && <Check className="h-3.5 w-3.5" aria-hidden="true" />}
                    </span>
                    {option}
                  </label>
                );
              })}
            </div>
          </fieldset>
        ))}
        </div>

        <div className="grid gap-4 md:grid-cols-3">
          {freeTextSections.map(({ id, title, description, placeholder, icon: Icon, color, background }) => (
            <fieldset key={id} className="rounded-2xl border border-slate-200/80 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
              <div className="flex items-start gap-4">
                <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-xl ${background}`}>
                  <Icon className={`h-5 w-5 ${color}`} aria-hidden="true" />
                </span>
                <div>
                  <h2 className="text-base font-bold text-slate-950 dark:text-white">{title}</h2>
                  <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">{description}</p>
                </div>
              </div>
              <textarea
                value={storedFreeText[id]}
                onChange={(event) => updateFreeText(id, event.target.value)}
                placeholder={placeholder}
                rows={5}
                className="mt-4 w-full resize-y rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
              />
            </fieldset>
          ))}
        </div>

        <div className="flex flex-col items-start justify-between gap-4 rounded-2xl border border-slate-200 bg-white p-5 sm:flex-row sm:items-center dark:border-white/10 dark:bg-white/[0.04]">
          <p className="text-sm text-slate-600 dark:text-white/65">
            <span className="font-semibold text-slate-900 dark:text-white">{selectedCount}</span> opciones seleccionadas
            {isSaved && <span className="ml-2 font-semibold text-emerald-600 dark:text-emerald-300">· Perfil guardado</span>}
            {saveMutation.isError && <span className="ml-2 font-semibold text-rose-600 dark:text-rose-300">· No se pudo guardar</span>}
          </p>
          <button type="submit" disabled={saveMutation.isPending || preferencesQuery.isLoading} className="inline-flex items-center gap-2 rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white shadow-sm transition hover:bg-blue-700 disabled:cursor-wait disabled:opacity-60">
            <Save className="h-4 w-4" aria-hidden="true" />
            {saveMutation.isPending ? 'Guardando...' : 'Guardar perfil académico'}
          </button>
        </div>
      </form>

      <div className="rounded-2xl border border-dashed border-slate-300 bg-slate-50/70 p-6 dark:border-white/15 dark:bg-white/[0.03]">
        <p className="text-sm font-medium text-slate-600 dark:text-white/65">
          Captura académica para la persona <span className="font-semibold text-slate-900 dark:text-white">{personId}</span>. Puedes seleccionar una o varias opciones por categoría; la conexión con persistencia se agregará en la siguiente etapa.
        </p>
      </div>
    </div>
  );
}
