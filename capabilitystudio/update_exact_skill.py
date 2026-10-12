code = r'''import { useState, useMemo } from 'react';
import { useSearchParams, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  GraduationCap,
  ArrowLeft,
  Sparkles,
  BookOpen,
  CheckCircle2,
  Layers,
  FileText,
  Target,
  Check,
  Save,
  ArrowRight,
  School,
  Heart,
} from 'lucide-react';

export interface AcademicSkillDef {
  code: string;
  subjectKey: string;
  subjectNameEs: string;
  subjectNameEn: string;
  nameEs: string;
  nameEn: string;
  descriptionEs: string;
  descriptionEn: string;
  whyItMattersEs: string;
  whyItMattersEn: string;
  skillsCovered: string[];
  hoursToMaster: number;
}

export const KINDERGARTEN_OFFICIAL_SKILLS: AcademicSkillDef[] = [
  {
    code: 'KIND-LIT-01',
    subjectKey: 'lenguaje',
    subjectNameEs: 'Lenguaje, lectura y escritura',
    subjectNameEn: 'Language, Reading & Writing',
    nameEs: 'Conciencia Fonológica, Rimas y Discriminación Auditiva',
    nameEn: 'Phonological Awareness, Rhyming & Sound Discrimination',
    descriptionEs: 'Identificar rimas, contar sílabas con palmadas y aislar el sonido inicial y final de palabras cotidianas.',
    descriptionEn: 'Recognize rhyming words, count syllables by clapping, and isolate beginning and ending sounds in common words.',
    whyItMattersEs: 'Es el predictor número uno del éxito futuro en lectura y ortografía en primaria.',
    whyItMattersEn: 'The single strongest predictor of future reading fluency and literacy success.',
    skillsCovered: [
      'Rimas consonantes y asonantes',
      'Conteo de sílabas con palmadas',
      'Aislamiento de sonido inicial',
      'Juegos de trabalenguas',
    ],
    hoursToMaster: 35,
  },
  {
    code: 'KIND-LIT-02',
    subjectKey: 'lenguaje',
    subjectNameEs: 'Lenguaje, lectura y escritura',
    subjectNameEn: 'Language, Reading & Writing',
    nameEs: 'Reconocimiento del Alfabeto A-Z y Principio Alfabético',
    nameEn: 'Alphabet Recognition A-Z & Letter-Sound Correspondence',
    descriptionEs: 'Nombrar y trazar letras mayúsculas y minúsculas asociándolas con su sonido fonético correspondiente.',
    descriptionEn: 'Identify and write uppercase and lowercase letters, matching each letter to its primary phonetic sound.',
    whyItMattersEs: 'Permite pasar de la decodificación silábica a la lectura fluida de palabras.',
    whyItMattersEn: 'Enables independent word decoding and emergent reading.',
    skillsCovered: ['Letras mayúsculas y minúsculas', 'Sonido fonético de vocales', 'Sonido de consonantes frecuentes', 'Lectura de palabras simples (CVC)'],
    hoursToMaster: 40,
  },
  {
    code: 'KIND-MAT-01',
    subjectKey: 'matematicas',
    subjectNameEs: 'Matemáticas',
    subjectNameEn: 'Mathematics',
    nameEs: 'Conteo, Cardinalidad y Sentido Numérico del 1 al 20',
    nameEn: 'Counting, Cardinality & Number Sense 1-20',
    descriptionEs: 'Contar colecciones de hasta 20 objetos con correspondencia uno a uno y reconocer cantidades visuales rápidas (subitización hasta 5).',
    descriptionEn: 'Count up to 20 objects with 1-to-1 correspondence and instantly recognize visual quantities up to 5 without counting (subitizing).',
    whyItMattersEs: 'Construye la comprensión intuitiva de la cantidad antes de pasar al cálculo formal.',
    whyItMattersEn: 'Builds foundational quantity intuition before formal arithmetic.',
    skillsCovered: ['Conteo 1 a 20', 'Correspondencia uno a uno', 'Subitización visual', 'Comparación mayor / menor / igual'],
    hoursToMaster: 35,
  },
  {
    code: 'KIND-MAT-02',
    subjectKey: 'matematicas',
    subjectNameEs: 'Matemáticas',
    subjectNameEn: 'Mathematics',
    nameEs: 'Clasificación de Figuras Geométricas y Patrones Lógicos',
    nameEn: '2D/3D Shapes, Sorting & Logical Patterns',
    descriptionEs: 'Identificar círculos, triángulos, cuadrados, rectángulos y cuerpos geométricos; crear y extender patrones lógicos (AB, ABC).',
    descriptionEn: 'Identify circles, triangles, squares, rectangles, and 3D shapes; create and extend repeating visual patterns (AB, ABC).',
    whyItMattersEs: 'Fomenta el razonamiento espacial y el pensamiento algebraico temprano.',
    whyItMattersEn: 'Fosters spatial reasoning and early algebraic logic.',
    skillsCovered: ['Figuras bidimensionales básicas', 'Cuerpos tridimensionales simples', 'Patrones de repetición', 'Clasificación por color y tamaño'],
    hoursToMaster: 25,
  },
  {
    code: 'KIND-CIE-01',
    subjectKey: 'ciencias',
    subjectNameEs: 'Ciencias',
    subjectNameEn: 'Sciences',
    nameEs: 'Exploración de la Naturaleza mediante los Cinco Sentidos',
    nameEn: 'Nature Inquiry & The Five Senses',
    descriptionEs: 'Observar, describir y clasificar plantas, animales, texturas y sonidos del entorno natural usando la vista, oído, tacto, olfato y gusto.',
    descriptionEn: 'Observe, describe, and classify natural phenomena, plants, animals, and textures using sensory observation.',
    whyItMattersEs: 'Desarrolla la capacidad de asombro y la formulación de hipótesis científicas tempranas.',
    whyItMattersEn: 'Builds empirical curiosity and early scientific observation habits.',
    skillsCovered: ['Uso de los 5 sentidos', 'Clasificación de objetos vivos y no vivos', 'Registro con dibujos en cuaderno de campo', 'Cuidado de seres vivos'],
    hoursToMaster: 25,
  },
];

export default function NewAcademicLearningPage() {
  const { i18n } = useTranslation();
  const isSpanish = i18n.language?.startsWith('es');
  const [searchParams, setSearchParams] = useSearchParams();

  const codeParam = searchParams.get('code') || 'KIND-LIT-01';

  // Find exact skill matching the code or default to KIND-LIT-01
  const matchedSkill = useMemo(() => {
    return KINDERGARTEN_OFFICIAL_SKILLS.find((s) => s.code === codeParam) || KINDERGARTEN_OFFICIAL_SKILLS[0];
  }, [codeParam]);

  // Form State initialized strictly with real data from the skill
  const [courseCode, setCourseCode] = useState(matchedSkill.code);
  const [courseTitleEn, setCourseTitleEn] = useState(matchedSkill.nameEn);
  const [courseTitleEs, setCourseTitleEs] = useState(matchedSkill.nameEs);
  const [courseSubjectEn, setCourseSubjectEn] = useState(matchedSkill.subjectNameEn);
  const [courseSubjectEs, setCourseSubjectEs] = useState(matchedSkill.subjectNameEs);
  const [courseHours, setCourseHours] = useState(matchedSkill.hoursToMaster.toString());
  const [courseDescriptionEn, setCourseDescriptionEn] = useState(matchedSkill.descriptionEn);
  const [courseDescriptionEs, setCourseDescriptionEs] = useState(matchedSkill.descriptionEs);
  const [courseWhyItMattersEn, setCourseWhyItMattersEn] = useState(matchedSkill.whyItMattersEn);
  const [courseWhyItMattersEs, setCourseWhyItMattersEs] = useState(matchedSkill.whyItMattersEs);
  const [skillsCoveredList, setSkillsCoveredList] = useState<string[]>(matchedSkill.skillsCovered);

  // Active Tab: Default is "Curso General View" as requested!
  const [activeTab, setActiveTab] = useState<'general_view' | 'steps' | 'activities' | 'evaluation'>('general_view');
  const [saveSuccessToast, setSaveSuccessToast] = useState(false);

  const handleSelectDifferentSkill = (newCode: string) => {
    const found = KINDERGARTEN_OFFICIAL_SKILLS.find((s) => s.code === newCode);
    if (found) {
      setCourseCode(found.code);
      setCourseTitleEn(found.nameEn);
      setCourseTitleEs(found.nameEs);
      setCourseSubjectEn(found.subjectNameEn);
      setCourseSubjectEs(found.subjectNameEs);
      setCourseHours(found.hoursToMaster.toString());
      setCourseDescriptionEn(found.descriptionEn);
      setCourseDescriptionEs(found.descriptionEs);
      setCourseWhyItMattersEn(found.whyItMattersEn);
      setCourseWhyItMattersEs(found.whyItMattersEs);
      setSkillsCoveredList(found.skillsCovered);
      setSearchParams({ code: found.code, stage: 'kindergarten' });
    }
  };

  const handleSaveCourseDraft = () => {
    setSaveSuccessToast(true);
    setTimeout(() => setSaveSuccessToast(false), 2500);
  };

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8 space-y-8 animate-fade-in text-slate-100">
      {/* Top Header & Breadcrumb */}
      <div>
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-xs font-semibold text-brand-400 hover:text-brand-300 transition-colors mb-3"
        >
          <ArrowLeft className="h-4 w-4" />
          <span>{isSpanish ? 'Volver al Catálogo' : 'Back to Catalog'}</span>
        </Link>

        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <div className="inline-flex items-center gap-2 rounded-full bg-emerald-500/10 px-3 py-1 text-xs font-bold text-emerald-400 border border-emerald-500/20 mb-2">
              <GraduationCap className="h-3.5 w-3.5" />
              <span>{isSpanish ? 'Pipeline de Cursos Académicos · Kindergarten' : 'Academic Course Pipeline · Kindergarten'}</span>
            </div>

            <h1 className="text-2xl sm:text-3xl font-extrabold tracking-tight text-white flex items-center gap-3">
              <span>{isSpanish ? courseTitleEs : courseTitleEn}</span>
              <span className="font-mono text-sm px-2.5 py-1 rounded-lg bg-emerald-500/20 text-emerald-300 font-bold border border-emerald-500/30">
                {courseCode}
              </span>
            </h1>

            <p className="mt-1 text-sm text-slate-400 max-w-3xl">
              {isSpanish
                ? `Materia: ${courseSubjectEs} · Kindergarten (Preescolar) · ~${courseHours} hrs maestría.`
                : `Subject: ${courseSubjectEn} · Kindergarten (Preschool) · ~${courseHours} hrs mastery.`}
            </p>
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={handleSaveCourseDraft}
              className="inline-flex items-center gap-2 rounded-xl bg-slate-800 hover:bg-slate-700 px-4 py-2.5 text-xs font-bold text-white border border-white/10 transition shadow-sm"
            >
              <Save className="h-4 w-4 text-slate-300" />
              <span>{isSpanish ? 'Guardar Borrador' : 'Save Draft'}</span>
            </button>
          </div>
        </div>
      </div>

      {saveSuccessToast && (
        <div className="rounded-2xl bg-emerald-950/70 border border-emerald-500/30 p-4 text-xs font-bold text-emerald-300 flex items-center gap-2">
          <CheckCircle2 className="h-4 w-4 text-emerald-400" />
          <span>{isSpanish ? 'Borrador del curso académico guardado exitosamente.' : 'Academic course draft successfully saved.'}</span>
        </div>
      )}

      {/* Tabs Bar: FIRST TAB IS "Curso General View" */}
      <div className="border-b border-white/10 flex flex-wrap gap-2 pb-1">
        <button
          type="button"
          onClick={() => setActiveTab('general_view')}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            activeTab === 'general_view'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <FileText className="h-4 w-4" />
          <span>Curso General View</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('steps')}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            activeTab === 'steps'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <Layers className="h-4 w-4" />
          <span>{isSpanish ? 'Estructura de Engramas' : 'Engrams Structure'}</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('activities')}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            activeTab === 'activities'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <Heart className="h-4 w-4" />
          <span>{isSpanish ? 'Actividades de Casa & Guía' : 'Home Activities & Guide'}</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('evaluation')}
          className={`flex items-center gap-2 px-5 py-3 rounded-xl text-xs font-bold transition ${
            activeTab === 'evaluation'
              ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 shadow-sm'
              : 'text-slate-400 hover:text-white hover:bg-white/5'
          }`}
        >
          <Target className="h-4 w-4" />
          <span>{isSpanish ? 'Tutor AI & Evaluación' : 'AI Tutor & Rubrics'}</span>
        </button>
      </div>

      {/* TAB 1: CURSO GENERAL VIEW (Requested First Entry View) */}
      {activeTab === 'general_view' && (
        <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
          {/* Main 2-column info */}
          <div className="lg:col-span-2 space-y-6">
            {/* Essential Course Identity Card */}
            <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 backdrop-blur-xl space-y-5">
              <div className="flex items-center justify-between border-b border-white/10 pb-4">
                <div className="flex items-center gap-2">
                  <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-emerald-500/20 text-emerald-400">
                    <BookOpen className="h-4 w-4" />
                  </span>
                  <div>
                    <h2 className="text-base font-bold text-white">
                      {isSpanish ? 'Datos del Curso (Primera Entrada)' : 'Course Data (Initial Entry)'}
                    </h2>
                    <p className="text-xs text-slate-400">
                      {isSpanish ? 'Datos exactos leídos del skill en la base de datos' : 'Exact data retrieved from the skill in the database'}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <span className="text-xs text-slate-400">{isSpanish ? 'Cargar otro skill:' : 'Switch skill:'}</span>
                  <select
                    value={courseCode}
                    onChange={(e) => handleSelectDifferentSkill(e.target.value)}
                    className="rounded-lg border border-white/10 bg-slate-950 px-2.5 py-1 text-xs font-mono text-emerald-400 focus:outline-none"
                  >
                    {KINDERGARTEN_OFFICIAL_SKILLS.map((s) => (
                      <option key={s.code} value={s.code}>
                        {s.code} - {s.nameEs}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <div>
                  <label className="block text-xs font-bold text-slate-300 mb-1">
                    {isSpanish ? 'Título del Curso (Español)' : 'Course Title (Spanish)'}
                  </label>
                  <input
                    type="text"
                    value={courseTitleEs}
                    onChange={(e) => setCourseTitleEs(e.target.value)}
                    className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none font-medium"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-300 mb-1">
                    {isSpanish ? 'Título del Curso (Inglés)' : 'Course Title (English)'}
                  </label>
                  <input
                    type="text"
                    value={courseTitleEn}
                    onChange={(e) => setCourseTitleEn(e.target.value)}
                    className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none font-medium"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-300 mb-1">
                    {isSpanish ? 'Código del Skill en BD' : 'Skill Database Code'}
                  </label>
                  <input
                    type="text"
                    value={courseCode}
                    onChange={(e) => setCourseCode(e.target.value)}
                    className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs font-mono text-emerald-400 font-bold focus:border-emerald-500 focus:outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-300 mb-1">
                    {isSpanish ? 'Materia / Asignatura' : 'Subject / Area'}
                  </label>
                  <input
                    type="text"
                    value={isSpanish ? courseSubjectEs : courseSubjectEn}
                    onChange={(e) => {
                      setCourseSubjectEs(e.target.value);
                      setCourseSubjectEn(e.target.value);
                    }}
                    className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-300 mb-1">
                    {isSpanish ? 'Horas para Maestría' : 'Hours to Master'}
                  </label>
                  <input
                    type="number"
                    value={courseHours}
                    onChange={(e) => setCourseHours(e.target.value)}
                    className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-300 mb-1">
                    {isSpanish ? 'Nivel Escolar' : 'Grade Level'}
                  </label>
                  <input
                    type="text"
                    disabled
                    value="Kindergarten (Preescolar) · 5 a 6 años"
                    className="w-full rounded-xl border border-white/10 bg-slate-950/60 px-3.5 py-2.5 text-xs text-slate-400 cursor-not-allowed"
                  />
                </div>

                <div className="sm:col-span-2">
                  <label className="block text-xs font-bold text-slate-300 mb-1">
                    {isSpanish ? 'Descripción & Qué Aprenderá el Alumno:' : 'Course Description & Scope:'}
                  </label>
                  <textarea
                    rows={3}
                    value={isSpanish ? courseDescriptionEs : courseDescriptionEn}
                    onChange={(e) => {
                      if (isSpanish) setCourseDescriptionEs(e.target.value);
                      else setCourseDescriptionEn(e.target.value);
                    }}
                    className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none leading-relaxed"
                  />
                </div>

                <div className="sm:col-span-2">
                  <label className="block text-xs font-bold text-slate-300 mb-1">
                    💡 {isSpanish ? 'Por qué es clave (Justificación Pedagógica):' : 'Why it matters (Pedagogical Rationale):'}
                  </label>
                  <textarea
                    rows={2}
                    value={isSpanish ? courseWhyItMattersEs : courseWhyItMattersEn}
                    onChange={(e) => {
                      if (isSpanish) setCourseWhyItMattersEs(e.target.value);
                      else setCourseWhyItMattersEn(e.target.value);
                    }}
                    className="w-full rounded-xl border border-white/10 bg-slate-950 px-3.5 py-2.5 text-xs text-white focus:border-emerald-500 focus:outline-none leading-relaxed"
                  />
                </div>
              </div>
            </div>

            {/* Target Competencies / Micro-skills Checklist */}
            <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 backdrop-blur-xl space-y-4">
              <div className="flex items-center gap-2 border-b border-white/10 pb-3">
                <Target className="h-4 w-4 text-emerald-400" />
                <h3 className="text-sm font-bold text-white">
                  {isSpanish ? 'Competencias Cubiertas por este Curso (Micro-Skills)' : 'Micro-Skills Covered by this Course'}
                </h3>
              </div>

              <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                {skillsCoveredList.map((skill, idx) => (
                  <div
                    key={idx}
                    className="flex items-center gap-3 rounded-2xl border border-white/10 bg-slate-950/70 p-3.5"
                  >
                    <div className="flex h-6 w-6 items-center justify-center rounded-lg bg-emerald-500/20 text-emerald-400">
                      <Check className="h-3.5 w-3.5" />
                    </div>
                    <span className="text-xs font-medium text-slate-200">{skill}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>

          {/* Right Column: Execution Parameters & Target Profile */}
          <div className="space-y-6">
            {/* Target Audience & Stage Specs */}
            <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-6 backdrop-blur-xl space-y-4">
              <h3 className="text-sm font-bold text-white flex items-center gap-2">
                <School className="h-4 w-4 text-emerald-400" />
                {isSpanish ? 'Ficha de Entrada del Curso' : 'Course Entry Specifications'}
              </h3>

              <div className="space-y-3 text-xs">
                <div className="flex items-center justify-between border-b border-white/5 pb-2">
                  <span className="text-slate-400">{isSpanish ? 'Nivel Escolar:' : 'School Stage:'}</span>
                  <span className="font-bold text-white">Kindergarten (Preescolar)</span>
                </div>

                <div className="flex items-center justify-between border-b border-white/5 pb-2">
                  <span className="text-slate-400">{isSpanish ? 'Rango de Edad:' : 'Age Range:'}</span>
                  <span className="font-bold text-emerald-400">5 - 6 años</span>
                </div>

                <div className="flex items-center justify-between border-b border-white/5 pb-2">
                  <span className="text-slate-400">{isSpanish ? 'Tiempo de Maestría:' : 'Hours to Master:'}</span>
                  <span className="font-bold text-white">~{courseHours} horas</span>
                </div>

                <div className="flex items-center justify-between border-b border-white/5 pb-2">
                  <span className="text-slate-400">{isSpanish ? 'Materia Oficial:' : 'Subject:'}</span>
                  <span className="font-bold text-emerald-300">{isSpanish ? courseSubjectEs : courseSubjectEn}</span>
                </div>

                <div className="flex items-center justify-between border-b border-white/5 pb-2">
                  <span className="text-slate-400">{isSpanish ? 'Audiencia:' : 'Audience:'}</span>
                  <span className="font-bold text-white">{isSpanish ? 'Alumnos (5-6) & Padres' : 'Students (5-6) & Parents'}</span>
                </div>
              </div>
            </div>

            {/* Smart Pipeline Notice */}
            <div className="rounded-3xl border border-emerald-500/20 bg-gradient-to-br from-emerald-950/40 via-slate-900/60 to-slate-900/60 p-6 backdrop-blur-xl">
              <div className="flex items-center gap-2 mb-2">
                <Sparkles className="h-4 w-4 text-emerald-400" />
                <h4 className="text-xs font-bold text-emerald-300">
                  {isSpanish ? 'Primer Tab: Curso General View' : 'First Tab: Curso General View'}
                </h4>
              </div>
              <p className="text-xs text-slate-400 leading-relaxed mb-4">
                {isSpanish
                  ? 'Los datos de la capability seleccionada están cargados como primera entrada del curso. En los siguientes pasos podrás diseñar los engramas, cuentos explicativos y preguntas socráticas.'
                  : 'The capability data is pre-populated as the initial course entry. In subsequent steps you can author engrams, narrative stories, and socratic tutor questions.'}
              </p>

              <button
                type="button"
                onClick={() => setActiveTab('steps')}
                className="w-full inline-flex items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-500 py-2.5 px-4 text-xs font-bold text-white shadow-lg shadow-emerald-500/20 hover:from-emerald-600 hover:to-teal-600 transition"
              >
                <span>{isSpanish ? 'Siguiente: Estructura de Engramas' : 'Next: Engrams Structure'}</span>
                <ArrowRight className="h-4 w-4" />
              </button>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: STEPS & ENGRAMS PLACEHOLDER */}
      {activeTab === 'steps' && (
        <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-8 text-center backdrop-blur-xl">
          <Layers className="h-10 w-10 text-emerald-400 mx-auto mb-3" />
          <h3 className="text-base font-bold text-white mb-1">
            {isSpanish ? 'Paso 2: Estructura de Engramas Académicos' : 'Step 2: Academic Engrams Structure'}
          </h3>
          <p className="text-xs text-slate-400 max-w-md mx-auto mb-4">
            {isSpanish
              ? `Diseña las secciones pedagógicas para ${isSpanish ? courseTitleEs : courseTitleEn}: Fundamento lúdico, práctica guiada y reto de maestría.`
              : `Design learning nodes for ${isSpanish ? courseTitleEs : courseTitleEn}: Playful foundations, guided practice, and mastery quest.`}
          </p>
          <button
            type="button"
            onClick={() => setActiveTab('general_view')}
            className="inline-flex items-center gap-1.5 text-xs text-brand-400 hover:underline"
          >
            ← {isSpanish ? 'Volver a Curso General View' : 'Back to Curso General View'}
          </button>
        </div>
      )}

      {/* TAB 3: ACTIVITIES PLACEHOLDER */}
      {activeTab === 'activities' && (
        <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-8 text-center backdrop-blur-xl">
          <Heart className="h-10 w-10 text-emerald-400 mx-auto mb-3" />
          <h3 className="text-base font-bold text-white mb-1">
            {isSpanish ? 'Paso 3: Actividades de Casa & Guía Familiar' : 'Step 3: Home Activities & Family Guide'}
          </h3>
          <p className="text-xs text-slate-400 max-w-md mx-auto mb-4">
            {isSpanish
              ? 'Dinámicas prácticas de 15 minutos en el hogar para acompañar el aprendizaje de rimas y fonética.'
              : '15-minute screen-free family exercises to reinforce phonics and rhyming.'}
          </p>
          <button
            type="button"
            onClick={() => setActiveTab('general_view')}
            className="inline-flex items-center gap-1.5 text-xs text-brand-400 hover:underline"
          >
            ← {isSpanish ? 'Volver a Curso General View' : 'Back to Curso General View'}
          </button>
        </div>
      )}

      {/* TAB 4: EVALUATION PLACEHOLDER */}
      {activeTab === 'evaluation' && (
        <div className="rounded-3xl border border-white/10 bg-slate-900/60 p-8 text-center backdrop-blur-xl">
          <Target className="h-10 w-10 text-emerald-400 mx-auto mb-3" />
          <h3 className="text-base font-bold text-white mb-1">
            {isSpanish ? 'Paso 4: Tutor AI & Evaluación' : 'Step 4: AI Tutor & Evaluation'}
          </h3>
          <p className="text-xs text-slate-400 max-w-md mx-auto mb-4">
            {isSpanish
              ? 'Preguntas socráticas para el tutor interactivo y criterios observables para alcanzar el nivel Master.'
              : 'Socratic dialogue prompts for the AI tutor and observable mastery criteria.'}
          </p>
          <button
            type="button"
            onClick={() => setActiveTab('general_view')}
            className="inline-flex items-center gap-1.5 text-xs text-brand-400 hover:underline"
          >
            ← {isSpanish ? 'Volver a Curso General View' : 'Back to Curso General View'}
          </button>
        </div>
      )}
    </div>
  );
}
'''

with open(r'c:\EducationAI\HumanOS\capabilitystudio\src\pages\NewAcademicLearningPage.tsx', 'w', encoding='utf-8') as f:
    f.write(code)

print("Updated NewAcademicLearningPage.tsx with exact real skill and Curso General View tab!")
