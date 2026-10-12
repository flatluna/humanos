import { useState, useEffect } from 'react';
import { useNavigate, Link } from 'react-router';
import {
  ArrowLeft,
  User,
  GraduationCap,
  Target,
  Sparkles,
  CheckCircle2,
  Star,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';
import { FamilyRole, FamilyMember } from '../types';

const AVATAR_COLORS = [
  { label: 'Esmeralda', value: 'from-emerald-600 to-teal-700', border: 'border-emerald-500' },
  { label: 'Índigo', value: 'from-indigo-600 to-violet-700', border: 'border-indigo-500' },
  { label: 'Ámbar', value: 'from-amber-500 to-orange-600', border: 'border-amber-500' },
  { label: 'Rosa / Coral', value: 'from-rose-500 to-pink-700', border: 'border-rose-500' },
  { label: 'Cian / Turquesa', value: 'from-teal-600 to-cyan-700', border: 'border-cyan-500' },
  { label: 'Púrpura Real', value: 'from-purple-600 to-indigo-800', border: 'border-purple-500' },
];

const ELEMENTARY_MASCOTS = [
  { id: 'lion', icon: '🦁', nameEs: 'León Sabio', nameEn: 'Wise Lion', color: 'from-amber-500 to-orange-600' },
  { id: 'rocket', icon: '🚀', nameEs: 'Astronauta', nameEn: 'Space Explorer', color: 'from-indigo-600 to-violet-700' },
  { id: 'dolphin', icon: '🐬', nameEs: 'Delfín Creativo', nameEn: 'Creative Dolphin', color: 'from-teal-600 to-cyan-700' },
  { id: 'owl', icon: '🦉', nameEs: 'Búho Lector', nameEn: 'Reading Owl', color: 'from-emerald-600 to-teal-700' },
  { id: 'dragon', icon: '⚡', nameEs: 'Dragón STEM', nameEn: 'STEM Dragon', color: 'from-rose-500 to-pink-700' },
];

const AVAILABLE_LANGUAGES = [
  'Español (Nativo)',
  'English (Fluent / Native)',
  'Français',
  'Mandarín',
  'Alemán',
  'Português',
];

const HIGH_SCHOOL_FOCUS = [
  'Preparación SAT / ACT',
  'Robótica & Programación',
  'Olimpiadas de Matemáticas',
  'Debate & Oratoria',
  'Pre-Medicina & Ciencias',
  'Piano & Música',
  'Liderazgo Ejecutivo',
  'Lectura Bilingüe Avanzada',
  'Deporte de Alto Rendimiento',
  'Finanzas Personales',
];

const ELEMENTARY_FOCUS = [
  'Comprensión Lectora & Vocabulario',
  'Tablas de Multiplicar & Cálculo Mental',
  'Concurso de Deletreo (Spelling Bee)',
  'Hábito de Tareas Diarias (Homework)',
  'Ciencias Naturales & Experimentos',
  'Pensamiento Lógico & Coding (Scratch)',
  'Caligrafía & Escritura de Cuentos',
  'Arte, Creatividad & Música',
  'Lectura en Español & Raíces Latinas',
  'Natación & Habilidades Motoras',
];

const ELEMENTARY_GRADES = [
  { val: 'kindergarten', labelEs: 'Kindergarten / Preescolar', labelEn: 'Kindergarten' },
  { val: '1st', labelEs: '1° de Primaria (1st Grade)', labelEn: '1st Grade (Elementary)' },
  { val: '2nd', labelEs: '2° de Primaria (2nd Grade)', labelEn: '2nd Grade (Elementary)' },
  { val: '3rd', labelEs: '3° de Primaria (3rd Grade)', labelEn: '3rd Grade (Elementary)' },
  { val: '4th', labelEs: '4° de Primaria (4th Grade)', labelEn: '4th Grade (Elementary)' },
  { val: '5th', labelEs: '5° de Primaria (5th Grade)', labelEn: '5th Grade (Elementary)' },
];

export function NewFamilyMemberPage() {
  const { lang } = useLanguage();
  const { addMember } = useFamily();
  const navigate = useNavigate();

  // Basic Form State
  const [fullName, setFullName] = useState('');
  const [preferredName, setPreferredName] = useState('');
  const [familyRole, setFamilyRole] = useState<FamilyRole>('son');
  const [roleTitleEs, setRoleTitleEs] = useState('');
  const [roleTitleEn, setRoleTitleEn] = useState('');
  const [birthDate, setBirthDate] = useState('');
  const [age, setAge] = useState<number>(8); // Defaults to elementary age
  const [email, setEmail] = useState('');
  const [primaryLocation, setPrimaryLocation] = useState('Austin, TX');
  const [avatarBg, setAvatarBg] = useState(AVATAR_COLORS[0].value);

  // Elementary Dynamic States
  const [elementaryGrade, setElementaryGrade] = useState('3rd');
  const [selectedMascot, setSelectedMascot] = useState(ELEMENTARY_MASCOTS[0]);
  const [enableQuests, setEnableQuests] = useState(true);
  const [weeklyReward, setWeeklyReward] = useState('Tarde familiar de armado de LEGO & helado');

  // Academic States
  const [educationLevelEs, setEducationLevelEs] = useState('Primaria / Elementary School');
  const [institution, setInstitution] = useState('Barton Creek Elementary');
  const [selectedLanguages, setSelectedLanguages] = useState<string[]>([
    'Español (Nativo)',
    'English (Fluent / Native)',
  ]);

  const isElementary = age >= 5 && age <= 11;

  const [topFocusList, setTopFocusList] = useState<string[]>([
    'Comprensión Lectora & Vocabulario',
    'Tablas de Multiplicar & Cálculo Mental',
    'Hábito de Tareas Diarias (Homework)',
  ]);
  const [customFocusInput, setCustomFocusInput] = useState('');
  const [weeklyHours, setWeeklyHours] = useState<number>(4.0);
  const [targetMilestoneEs, setTargetMilestoneEs] = useState('Obtener calificación A / 10 en Lectura y Matemáticas en el reporte bimestral');
  const [targetMilestoneEn, setTargetMilestoneEn] = useState("Earn Straight A's in Reading and Math 9-week report card");

  const [errors, setErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (isElementary) {
      if (weeklyHours > 7) setWeeklyHours(4.0);
      setEducationLevelEs('Primaria / Elementary School');
      if (topFocusList.some((f) => f.includes('SAT') || f.includes('Ejecutivo'))) {
        setTopFocusList([
          'Comprensión Lectora & Vocabulario',
          'Tablas de Multiplicar & Cálculo Mental',
          'Hábito de Tareas Diarias (Homework)',
        ]);
      }
    }
  }, [isElementary]);

  const handleBirthDateChange = (val: string) => {
    setBirthDate(val);
    if (val) {
      const birthYear = new Date(val).getFullYear();
      const currentYear = new Date().getFullYear();
      const calcAge = Math.max(1, currentYear - birthYear);
      setAge(calcAge);
    }
  };

  const toggleLanguage = (l: string) => {
    setSelectedLanguages((prev) =>
      prev.includes(l) ? prev.filter((x) => x !== l) : [...prev, l],
    );
  };

  const toggleFocus = (f: string) => {
    setTopFocusList((prev) =>
      prev.includes(f) ? prev.filter((x) => x !== f) : [...prev, f],
    );
  };

  const handleAddCustomFocus = () => {
    if (customFocusInput.trim() && !topFocusList.includes(customFocusInput.trim())) {
      setTopFocusList((prev) => [...prev, customFocusInput.trim()]);
      setCustomFocusInput('');
    }
  };

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    const newErrors: Record<string, string> = {};

    if (!fullName.trim()) {
      newErrors.fullName =
        lang === 'es' ? 'El nombre completo es requerido' : 'Full name is required';
    }

    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }

    const initial = (preferredName || fullName).trim().charAt(0).toUpperCase() || 'M';
    const gradeLabel = ELEMENTARY_GRADES.find((g) => g.val === elementaryGrade);

    const defaultRoleTitleEs = isElementary
      ? `${familyRole === 'son' ? 'Hijo' : familyRole === 'teen_daughter' ? 'Hija' : 'Estudiante'} (${age} años) · ${gradeLabel?.labelEs ?? 'Primaria'} 🌟`
      : `${familyRole === 'son' ? 'Hijo' : familyRole === 'teen_daughter' ? 'Hija' : 'Integrante'} (${age} años) · Estudiante`;

    const defaultRoleTitleEn = isElementary
      ? `${familyRole === 'son' ? 'Son' : familyRole === 'teen_daughter' ? 'Daughter' : 'Student'} (${age} yrs) · ${gradeLabel?.labelEn ?? 'Elementary'} 🌟`
      : `${familyRole === 'son' ? 'Son' : familyRole === 'teen_daughter' ? 'Daughter' : 'Member'} (${age} yrs) · Student`;

    const newMember: FamilyMember = {
      id: `member-${Date.now()}`,
      name: fullName.trim(),
      preferredName: preferredName.trim() || fullName.trim(),
      role: familyRole,
      roleTitleEs: roleTitleEs.trim() || defaultRoleTitleEs,
      roleTitleEn: roleTitleEn.trim() || defaultRoleTitleEn,
      age: Number(age),
      avatarBg: isElementary ? selectedMascot.color : avatarBg,
      initial: isElementary ? selectedMascot.icon : initial,
      educationLevelEs: isElementary ? `${gradeLabel?.labelEs ?? 'Primaria'} · Elementary` : educationLevelEs.trim(),
      educationLevelEn: isElementary ? `${gradeLabel?.labelEn ?? 'Elementary'}` : educationLevelEs.trim(),
      institutionEs: institution.trim() || 'Escuela Primaria Local',
      institutionEn: institution.trim() || 'Elementary School',
      primaryLocation: primaryLocation.trim() || 'Austin, TX & CDMX',
      topFocusEs: topFocusList.length > 0 ? topFocusList : ['Lectura Comprensiva', 'Matemáticas'],
      topFocusEn: topFocusList.length > 0 ? topFocusList : ['Reading Fluency', 'Math'],
      weeklyHoursLogged: 0,
      learningStreakDays: 1,
      interests: topFocusList,
      languages: selectedLanguages,
      targetMilestoneEs: targetMilestoneEs.trim() || 'Maximizar calificaciones y hábito de tareas',
      targetMilestoneEn: targetMilestoneEn.trim() || 'Maximize report card grades & daily homework routine',
    };

    addMember(newMember);
    navigate('/family');
  };

  return (
    <div className="space-y-8 animate-fade-in pb-16">
      {/* Breadcrumb & Heading */}
      <div>
        <Link
          to="/family"
          className="inline-flex items-center gap-2 text-xs font-semibold text-emerald-600 hover:text-emerald-700 dark:text-emerald-400 mb-3 transition"
        >
          <ArrowLeft className="h-4 w-4" />
          <span>{lang === 'es' ? 'Volver a Núcleo Familiar' : 'Back to Family Core'}</span>
        </Link>

        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
            {lang === 'es' ? 'Dar de Alta Nuevo Integrante Familiar' : 'Register New Family Member'}
          </h1>
          {isElementary && (
            <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-100 px-3 py-1 text-xs font-black text-amber-900 dark:bg-amber-950/60 dark:text-amber-300 animate-pulse">
              🌟 {lang === 'es' ? 'Modo Primaria / Elementary' : 'Elementary Kid Mode'}
            </span>
          )}
        </div>
        <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300 max-w-3xl">
          {lang === 'es'
            ? 'Configura el perfil escolar. La plataforma adapta dinámicamente las herramientas para potenciar las calificaciones, la comprensión lectora bilingüe y el hábito diario de tareas.'
            : 'Configure household member profile. Tools dynamically adapt to boost school grades, daily homework habits, and bilingual reading fluency.'}
        </p>
      </div>

      {/* DYNAMIC BANNER: Elementary Optimization Notice */}
      {isElementary && (
        <div className="rounded-3xl border border-amber-300/80 bg-gradient-to-r from-amber-50 via-orange-50 to-amber-100/60 p-5 shadow-xs dark:border-amber-500/30 dark:from-amber-950/40 dark:via-warm-900 dark:to-warm-900">
          <div className="flex items-start gap-3.5">
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-amber-500 text-2xl text-white shadow-md shadow-amber-500/30">
              🎒
            </span>
            <div>
              <h2 className="text-sm font-bold text-amber-950 dark:text-amber-200 flex items-center gap-2">
                {lang === 'es'
                  ? 'Acompañamiento Estratégico para Maximizar Calificaciones en Primaria'
                  : 'Tailored Strategy to Maximize Elementary School Grades'}
                <span className="rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-bold text-amber-900 dark:text-amber-300">
                  {lang === 'es' ? '5 a 11 años' : 'Ages 5 - 11'}
                </span>
              </h2>
              <p className="mt-1 text-xs text-amber-900/80 dark:text-amber-300/80 leading-relaxed max-w-4xl">
                {lang === 'es'
                  ? 'Para niños de primaria, el éxito escolar depende de dominar la comprensión de lectura bilingüe, las tablas de multiplicar y no olvidar tareas. La plataforma activa bloques de 25 minutos, recordatorios de tareas y el tutor amigable Family Coach AI.'
                  : 'In elementary school, academic excellence hinges on bilingual reading comprehension, math automaticity, and consistent daily homework completion. The platform activates 25-minute study intervals and an encouraging kid-friendly AI tutor.'}
              </p>
            </div>
          </div>
        </div>
      )}

      <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
        {/* Main Form */}
        <form onSubmit={handleSave} className="space-y-6 lg:col-span-2">
          {/* SECTION 1: Personal Info */}
          <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a] space-y-4">
            <div className="flex items-center gap-2.5 border-b border-warm-100 pb-3 dark:border-white/10">
              <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-emerald-100 text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                <User className="h-4 w-4" />
              </span>
              <div>
                <h2 className="text-base font-bold text-warm-900 dark:text-white">
                  {lang === 'es' ? '1. Información del Estudiante' : '1. Student Information'}
                </h2>
                <p className="text-xs text-warm-500">
                  {lang === 'es' ? 'Nombre, edad y rol en la familia' : 'Name, age, and family role'}
                </p>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {lang === 'es' ? 'Nombre Completo del Niño(a) *' : 'Child Full Name *'}
                </label>
                <input
                  type="text"
                  value={fullName}
                  onChange={(e) => {
                    setFullName(e.target.value);
                    if (errors.fullName) setErrors({});
                  }}
                  placeholder="Ej: Lucas Mendoza Ríos"
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-emerald-500 focus:outline-none focus:ring-1 focus:ring-emerald-500 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                />
                {errors.fullName && (
                  <p className="mt-1 text-[11px] text-rose-500 font-semibold">{errors.fullName}</p>
                )}
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {lang === 'es' ? 'Nombre Preferido / Apodo cariñoso' : 'Preferred Name / Nickname'}
                </label>
                <input
                  type="text"
                  value={preferredName}
                  onChange={(e) => setPreferredName(e.target.value)}
                  placeholder="Ej: Luquitas"
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-emerald-500 focus:outline-none focus:ring-1 focus:ring-emerald-500 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {lang === 'es' ? 'Rol en la Familia *' : 'Family Role *'}
                </label>
                <select
                  value={familyRole}
                  onChange={(e) => setFamilyRole(e.target.value as FamilyRole)}
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-emerald-500 focus:outline-none dark:border-white/10 dark:bg-warm-900 dark:text-white"
                >
                  <option value="son">{lang === 'es' ? 'Hijo / Son' : 'Son'}</option>
                  <option value="teen_daughter">{lang === 'es' ? 'Hija / Daughter' : 'Daughter'}</option>
                  <option value="mentor">{lang === 'es' ? 'Tutor / Mentor' : 'Tutor / Mentor'}</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {lang === 'es' ? 'Edad (Cambia dinámicamente los campos)' : 'Age (Triggers dynamic grade adaptation)'}
                </label>
                <div className="flex items-center gap-2">
                  <input
                    type="date"
                    value={birthDate}
                    onChange={(e) => handleBirthDateChange(e.target.value)}
                    className="flex-1 rounded-xl border border-warm-200 bg-warm-50/60 px-3 py-2 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                  />
                  <input
                    type="number"
                    min="4"
                    max="18"
                    value={age}
                    onChange={(e) => setAge(Number(e.target.value))}
                    className="w-16 rounded-xl border border-warm-200 bg-warm-50/60 px-3 py-2 text-xs text-warm-900 text-center font-bold dark:border-white/10 dark:bg-warm-900 dark:text-white"
                  />
                  <span className="text-xs text-warm-400 font-semibold">{lang === 'es' ? 'años' : 'yrs'}</span>
                </div>
              </div>

              {/* Elementary specific: Grade Level selector */}
              {isElementary ? (
                <div className="sm:col-span-2 rounded-2xl border border-amber-200 bg-amber-50/50 p-4 dark:border-amber-500/20 dark:bg-amber-950/20">
                  <label className="block text-xs font-bold text-amber-950 dark:text-amber-200 mb-2">
                    {lang === 'es' ? 'Grado Escolar en Primaria (Elementary Grade Level):' : 'Elementary School Grade Level:'}
                  </label>
                  <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
                    {ELEMENTARY_GRADES.map((g) => (
                      <button
                        key={g.val}
                        type="button"
                        onClick={() => setElementaryGrade(g.val)}
                        className={`rounded-xl border px-3 py-2 text-left text-xs font-semibold transition ${
                          elementaryGrade === g.val
                            ? 'border-amber-500 bg-amber-500 text-white shadow-xs font-bold'
                            : 'border-warm-200 bg-white text-warm-700 hover:bg-amber-50/60 dark:border-white/10 dark:bg-warm-900 dark:text-warm-300'
                        }`}
                      >
                        {lang === 'es' ? g.labelEs : g.labelEn}
                      </button>
                    ))}
                  </div>
                </div>
              ) : (
                <>
                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      {lang === 'es' ? 'Título del Rol (Español)' : 'Role Title (Spanish)'}
                    </label>
                    <input
                      type="text"
                      value={roleTitleEs}
                      onChange={(e) => setRoleTitleEs(e.target.value)}
                      placeholder="Ej: Hijo (15) · High School & Robótica"
                      className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      {lang === 'es' ? 'Título del Rol (Inglés)' : 'Role Title (English)'}
                    </label>
                    <input
                      type="text"
                      value={roleTitleEn}
                      onChange={(e) => setRoleTitleEn(e.target.value)}
                      placeholder="Ej: Son (15) · High School & STEM"
                      className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                    />
                  </div>
                </>
              )}

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {lang === 'es' ? 'Ubicación Principal' : 'Primary Location'}
                </label>
                <input
                  type="text"
                  value={primaryLocation}
                  onChange={(e) => setPrimaryLocation(e.target.value)}
                  placeholder="Austin, TX / CDMX"
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {lang === 'es' ? 'Correo (Opcional en Primaria)' : 'Email (Optional for Minors)'}
                </label>
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder={lang === 'es' ? 'Asociado al correo de los padres' : 'Linked to parents account (COPPA Safe)'}
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                />
                <p className="text-[10px] text-warm-400 mt-1">
                  {lang === 'es'
                    ? '🔒 Por seguridad infantil (COPPA), las alertas llegarán a la cuenta del padre/madre.'
                    : '🔒 Safe for minors: All grade alerts flow directly to parents.'}
                </p>
              </div>
            </div>

            {/* DYNAMIC: Kid Mascot Selector for Elementary vs Normal Colors */}
            {isElementary ? (
              <div className="pt-2">
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-2">
                  {lang === 'es' ? 'Mascota / Avatar de Aprendizaje para el Niño:' : 'Child Learning Mascot Avatar:'}
                </label>
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
                  {ELEMENTARY_MASCOTS.map((m) => (
                    <button
                      key={m.id}
                      type="button"
                      onClick={() => setSelectedMascot(m)}
                      className={`flex flex-col items-center gap-1 rounded-2xl border p-3 transition ${
                        selectedMascot.id === m.id
                          ? 'border-amber-500 bg-amber-50 shadow-sm font-bold dark:bg-amber-950/40 text-amber-950 dark:text-amber-200 ring-2 ring-amber-400'
                          : 'border-warm-200 bg-white hover:bg-warm-50 dark:border-white/10 dark:bg-warm-900 text-warm-700 dark:text-warm-300'
                      }`}
                    >
                      <span className="text-2xl">{m.icon}</span>
                      <span className="text-[11px] text-center font-bold">{lang === 'es' ? m.nameEs : m.nameEn}</span>
                    </button>
                  ))}
                </div>
              </div>
            ) : (
              <div className="pt-2">
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-2">
                  {lang === 'es' ? 'Color Temático del Avatar' : 'Avatar Color Theme'}
                </label>
                <div className="flex flex-wrap gap-2.5">
                  {AVATAR_COLORS.map((c, i) => (
                    <button
                      key={i}
                      type="button"
                      onClick={() => setAvatarBg(c.value)}
                      className={`flex items-center gap-2 rounded-xl border px-3 py-1.5 text-xs font-medium transition ${
                        avatarBg === c.value
                          ? 'border-emerald-500 bg-emerald-50 font-bold dark:border-emerald-400 dark:bg-emerald-950/40 text-emerald-900 dark:text-emerald-200'
                          : 'border-warm-200 bg-white hover:bg-warm-50 dark:border-white/10 dark:bg-warm-900 text-warm-700 dark:text-warm-300'
                      }`}
                    >
                      <span className={`h-4 w-4 rounded-full bg-gradient-to-br ${c.value} shadow-xs`} />
                      <span>{c.label}</span>
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* SECTION 2: School & Languages */}
          <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a] space-y-4">
            <div className="flex items-center gap-2.5 border-b border-warm-100 pb-3 dark:border-white/10">
              <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-amber-100 text-amber-700 dark:bg-amber-950/60 dark:text-amber-300">
                <GraduationCap className="h-4 w-4" />
              </span>
              <div>
                <h2 className="text-base font-bold text-warm-900 dark:text-white">
                  {lang === 'es' ? '2. Escuela & Bilingüismo' : '2. School & Languages'}
                </h2>
                <p className="text-xs text-warm-500">
                  {lang === 'es' ? 'Colegio actual e idiomas de refuerzo' : 'Current school and language foundation'}
                </p>
              </div>
            </div>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {lang === 'es' ? 'Nombre de la Escuela Primaria' : 'Elementary School Name'}
                </label>
                <input
                  type="text"
                  value={institution}
                  onChange={(e) => setInstitution(e.target.value)}
                  placeholder="Ej: Barton Creek Elementary / Colegio Peterson"
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {lang === 'es' ? 'Nivel Educativo Registrado' : 'Registered Grade Level'}
                </label>
                <input
                  type="text"
                  disabled
                  value={isElementary ? `${elementaryGrade.toUpperCase()} Elementary School` : educationLevelEs}
                  className="w-full rounded-xl border border-warm-200 bg-warm-100/70 px-3.5 py-2.5 text-xs text-warm-600 dark:border-white/10 dark:bg-warm-800 dark:text-warm-300 cursor-not-allowed"
                />
              </div>
            </div>

            <div className="pt-2">
              <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-2">
                {lang === 'es' ? 'Idiomas en Casa y Escuela (Enfoque Bilingüe):' : 'Languages Spoken at Home & School (Dual Track):'}
              </label>
              <div className="flex flex-wrap gap-2">
                {AVAILABLE_LANGUAGES.map((l, i) => {
                  const isSelected = selectedLanguages.includes(l);
                  return (
                    <button
                      key={i}
                      type="button"
                      onClick={() => toggleLanguage(l)}
                      className={`flex items-center gap-1.5 rounded-xl border px-3 py-1.5 text-xs font-medium transition ${
                        isSelected
                          ? 'border-indigo-500 bg-indigo-50 font-bold text-indigo-900 dark:bg-indigo-950/40 dark:text-indigo-200'
                          : 'border-warm-200 bg-white hover:bg-warm-50 dark:border-white/10 dark:bg-warm-900 text-warm-600 dark:text-warm-300'
                      }`}
                    >
                      <CheckCircle2 className={`h-3.5 w-3.5 ${isSelected ? 'text-indigo-600' : 'opacity-30'}`} />
                      <span>{l}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          </div>

          {/* SECTION 3: Dynamic Subject Focus for Grades vs High School */}
          <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a] space-y-4">
            <div className="flex items-center gap-2.5 border-b border-warm-100 pb-3 dark:border-white/10">
              <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-indigo-100 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
                <Target className="h-4 w-4" />
              </span>
              <div>
                <h2 className="text-base font-bold text-warm-900 dark:text-white">
                  {isElementary
                    ? (lang === 'es' ? '3. Materias Clave para Elevar Calificaciones' : '3. Core Subjects to Maximize Grades')
                    : (lang === 'es' ? '3. Enfoques de Crecimiento & Hitos' : '3. Growth Focus & Key Milestones')}
                </h2>
                <p className="text-xs text-warm-500">
                  {isElementary
                    ? (lang === 'es' ? 'Selecciona los temas de mayor impacto para su boleta de calificaciones' : 'Select highest-impact areas for quarterly report cards')
                    : (lang === 'es' ? 'Objetivos estratégicos y compromiso semanal' : 'Strategic goals and weekly learning cadence')}
                </p>
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-2">
                {isElementary
                  ? (lang === 'es' ? 'Materias con Refuerzo Semanal por IA:' : 'Subjects with AI-Powered Weekly Reinforcement:')
                  : (lang === 'es' ? 'Áreas de Enfoque Principal:' : 'Top Focus Areas:')}
              </label>

              <div className="flex flex-wrap gap-1.5 mb-2.5">
                {(isElementary ? ELEMENTARY_FOCUS : HIGH_SCHOOL_FOCUS).map((f, i) => {
                  const isSelected = topFocusList.includes(f);
                  return (
                    <button
                      key={i}
                      type="button"
                      onClick={() => toggleFocus(f)}
                      className={`rounded-lg border px-2.5 py-1 text-xs font-medium transition ${
                        isSelected
                          ? 'border-emerald-500 bg-emerald-50 text-emerald-800 font-bold dark:bg-emerald-950/50 dark:text-emerald-300 ring-1 ring-emerald-400'
                          : 'border-warm-200 bg-warm-50/70 text-warm-700 hover:bg-warm-100 dark:border-white/10 dark:bg-white/5 dark:text-warm-300'
                      }`}
                    >
                      {f}
                    </button>
                  );
                })}
              </div>

              {/* Custom tag add */}
              <div className="flex items-center gap-2 mt-2">
                <input
                  type="text"
                  value={customFocusInput}
                  onChange={(e) => setCustomFocusInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault();
                      handleAddCustomFocus();
                    }
                  }}
                  placeholder={
                    isElementary
                      ? (lang === 'es' ? 'Agregar otra materia escolar (ej. Francés, Ortografía)...' : 'Add another school subject (e.g. Spelling, Science)...')
                      : (lang === 'es' ? 'Agregar otro enfoque personalizado...' : 'Add custom focus area...')
                  }
                  className="flex-1 rounded-xl border border-warm-200 bg-warm-50/60 px-3 py-1.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                />
                <button
                  type="button"
                  onClick={handleAddCustomFocus}
                  className="rounded-xl bg-warm-800 px-3 py-1.5 text-xs font-bold text-white hover:bg-warm-900 dark:bg-warm-700"
                >
                  + {lang === 'es' ? 'Agregar' : 'Add'}
                </button>
              </div>
            </div>

            {/* Elementary Specific: Cadence & Study Blocks */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isElementary
                    ? (lang === 'es' ? 'Horas Semanales Recomendadas (Bloques de 25 min)' : 'Weekly Target (25-min daily focus blocks)')
                    : (lang === 'es' ? 'Horas Objetivo Semanales de Estudio' : 'Weekly Study Target Hours')}
                </label>
                <div className="flex items-center gap-3">
                  <input
                    type="range"
                    min="1"
                    max={isElementary ? 10 : 30}
                    step="0.5"
                    value={weeklyHours}
                    onChange={(e) => setWeeklyHours(Number(e.target.value))}
                    className="flex-1 accent-emerald-600"
                  />
                  <span className="w-16 rounded-lg bg-emerald-50 px-2 py-1 text-center text-xs font-black text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300">
                    {weeklyHours} hrs
                  </span>
                </div>
                {isElementary && (
                  <p className="text-[10px] text-emerald-700 dark:text-emerald-400 font-semibold mt-1">
                    ✓ ~{Math.round((weeklyHours * 60) / 7)} min/{lang === 'es' ? 'día de tareas y lectura' : 'day homework & reading'}
                  </p>
                )}
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isElementary
                    ? (lang === 'es' ? 'Meta Escolar de Calificaciones 2026-2027:' : 'Report Card Grade Goal 2026-2027:')
                    : (lang === 'es' ? 'Hito Clave 2026-2027:' : 'Key 2026-2027 Milestone:')}
                </label>
                <input
                  type="text"
                  value={targetMilestoneEs}
                  onChange={(e) => {
                    setTargetMilestoneEs(e.target.value);
                    setTargetMilestoneEn(e.target.value);
                  }}
                  placeholder={
                    isElementary
                      ? 'Ej: Calificación A / 10 en Lectura y Matemáticas en el reporte bimestral'
                      : 'Ej: Clasificar a olimpiada estatal o SAT score 1500+'
                  }
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                />
              </div>
            </div>

            {/* SECTION 4: Elementary Gamification Quests */}
            {isElementary && (
              <div className="rounded-2xl border border-indigo-200 bg-indigo-50/40 p-4 dark:border-indigo-500/20 dark:bg-indigo-950/20 space-y-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <Star className="h-4 w-4 text-amber-500 fill-amber-500" />
                    <span className="text-xs font-bold text-indigo-950 dark:text-indigo-200">
                      {lang === 'es' ? 'Tablero de Misiones Diarias & Recompensas Familiares' : 'Daily Quests & Family Reward Board'}
                    </span>
                  </div>
                  <label className="flex items-center gap-1.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={enableQuests}
                      onChange={(e) => setEnableQuests(e.target.checked)}
                      className="rounded text-indigo-600 focus:ring-indigo-500 h-4 w-4"
                    />
                    <span className="text-[11px] font-semibold text-indigo-800 dark:text-indigo-300">
                      {lang === 'es' ? 'Activar' : 'Enable'}
                    </span>
                  </label>
                </div>

                {enableQuests && (
                  <div>
                    <label className="block text-[11px] font-bold text-warm-700 dark:text-warm-300 mb-1">
                      {lang === 'es'
                        ? 'Recompensa Semanal Pactada al Cumplir las Tareas (Acuerdo Padres-Hijo):'
                        : 'Weekly Family Reward Upon Completing Homework Goals:'}
                    </label>
                    <input
                      type="text"
                      value={weeklyReward}
                      onChange={(e) => setWeeklyReward(e.target.value)}
                      placeholder="Ej: Tarde de LEGO, tarde de cine familiar o salida al parque"
                      className="w-full rounded-xl border border-indigo-200 bg-white px-3 py-2 text-xs text-warm-900 dark:border-white/10 dark:bg-warm-900 dark:text-white"
                    />
                    <p className="text-[10px] text-indigo-700 dark:text-indigo-400 mt-1">
                      {lang === 'es'
                        ? '🎁 La neurociencia demuestra que los incentivos visuales positivos inmediatos aumentan la puntualidad de entrega de tareas en un 85%.'
                        : '🎁 Pedagogical research shows immediate positive visual reinforcement elevates on-time homework submission by 85%.'}
                    </p>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Form Actions */}
          <div className="flex items-center justify-end gap-3 pt-4">
            <Link
              to="/family"
              className="rounded-2xl border border-warm-200 bg-white px-5 py-3 text-xs font-bold text-warm-700 shadow-xs hover:bg-warm-50 dark:border-white/10 dark:bg-warm-800 dark:text-warm-200"
            >
              {lang === 'es' ? 'Cancelar' : 'Cancel'}
            </Link>

            <button
              type="submit"
              className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-emerald-600 to-teal-600 px-6 py-3 text-xs font-bold text-white shadow-md shadow-emerald-600/30 transition hover:from-emerald-700 hover:to-teal-700 hover:shadow-lg"
            >
              <CheckCircle2 className="h-4 w-4" />
              <span>
                {isElementary
                  ? (lang === 'es' ? 'Guardar Estudiante de Primaria' : 'Save Elementary Student')
                  : (lang === 'es' ? 'Guardar e Integrar a la Familia' : 'Save & Add to Household')}
              </span>
            </button>
          </div>
        </form>

        {/* Live Card Preview Column */}
        <div className="space-y-4">
          <div className="sticky top-28">
            <div className="rounded-2xl bg-warm-100 p-2 text-center text-[11px] font-bold text-warm-600 dark:bg-white/5 dark:text-warm-400 mb-3 flex items-center justify-center gap-1.5">
              <span>👀</span>
              <span>{lang === 'es' ? 'Vista Previa en Tiempo Real' : 'Live Card Preview'}</span>
            </div>

            {/* Simulated Family Card */}
            <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-md dark:border-white/10 dark:bg-[#0f172a]">
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-center gap-4">
                  <div className={`flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br ${isElementary ? selectedMascot.color : avatarBg} text-2xl font-black text-white shadow-md`}>
                    {isElementary ? selectedMascot.icon : (preferredName || fullName || 'N').charAt(0).toUpperCase()}
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-warm-900 dark:text-white">
                      {fullName || (lang === 'es' ? 'Nombre del Estudiante' : 'Student Name')}
                    </h3>
                    <p className="text-xs font-semibold text-indigo-600 dark:text-indigo-400">
                      {isElementary
                        ? (lang === 'es' ? `Hijo (${age}) · ${elementaryGrade.toUpperCase()} Primaria 🌟` : `Son (${age}) · ${elementaryGrade.toUpperCase()} Grade 🌟`)
                        : (roleTitleEs || (lang === 'es' ? 'Rol y Especialidad' : 'Role & Focus'))}
                    </p>
                    <p className="text-[11px] text-warm-500 dark:text-warm-400">
                      📍 {primaryLocation} · {age} {lang === 'es' ? 'años' : 'years old'}
                    </p>
                  </div>
                </div>

                <span className="rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-bold text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                  {weeklyHours} hrs/sem
                </span>
              </div>

              {isElementary && (
                <div className="mt-4 flex items-center justify-between rounded-xl bg-amber-50 p-2.5 dark:bg-amber-950/30">
                  <span className="text-[11px] font-bold text-amber-900 dark:text-amber-300 flex items-center gap-1">
                    <span>{selectedMascot.icon}</span>
                    <span>Mascota: {lang === 'es' ? selectedMascot.nameEs : selectedMascot.nameEn}</span>
                  </span>
                  <span className="text-[10px] font-extrabold text-amber-700 bg-amber-200/60 px-2 py-0.5 rounded-md">
                    25 min/día
                  </span>
                </div>
              )}

              <div className="mt-4 space-y-3 border-t border-warm-100 pt-4 dark:border-white/10 text-xs">
                <div>
                  <span className="font-semibold text-warm-400 uppercase tracking-wider text-[10px]">
                    {lang === 'es' ? 'Escuela & Grado:' : 'School & Grade:'}
                  </span>
                  <p className="font-bold text-warm-800 dark:text-warm-200 mt-0.5">
                    {institution || (lang === 'es' ? 'Escuela Primaria' : 'Elementary School')}
                  </p>
                  <p className="text-warm-500 text-[11px]">
                    {isElementary ? `${elementaryGrade.toUpperCase()} Grade · Bicultural Track` : educationLevelEs}
                  </p>
                </div>

                <div>
                  <span className="font-semibold text-warm-400 uppercase tracking-wider text-[10px]">
                    {isElementary
                      ? (lang === 'es' ? 'Materias para Maximizar Notas:' : 'Target Report Card Subjects:')
                      : (lang === 'es' ? 'Enfoque & Fortalezas:' : 'Key Focus:')}
                  </span>
                  <div className="mt-1 flex flex-wrap gap-1">
                    {topFocusList.map((foc, i) => (
                      <span
                        key={i}
                        className="rounded-md bg-warm-100 px-2 py-0.5 text-[10px] font-medium text-warm-800 dark:bg-white/5 dark:text-warm-200"
                      >
                        {isElementary ? '📚 ' : '✨ '}
                        {foc}
                      </span>
                    ))}
                  </div>
                </div>

                <div className="rounded-xl bg-emerald-50 p-3 dark:bg-white/[0.02] border border-emerald-100 dark:border-white/5">
                  <span className="text-[10px] font-bold text-emerald-800 dark:text-emerald-300 uppercase tracking-wider">
                    {isElementary
                      ? (lang === 'es' ? 'Objetivo de Boleta 2026-2027:' : '2026-2027 Report Card Target:')
                      : (lang === 'es' ? 'Hito 2026-2027:' : '2026-2027 Milestone:')}
                  </span>
                  <p className="font-bold text-emerald-950 dark:text-emerald-300 mt-0.5 text-xs">
                    🎯 {targetMilestoneEs}
                  </p>
                </div>
              </div>
            </div>

            {/* Smart tip */}
            <div className="mt-4 rounded-2xl border border-emerald-200 bg-emerald-50/50 p-4 dark:border-emerald-500/20 dark:bg-emerald-950/20">
              <p className="text-xs font-bold text-emerald-900 dark:text-emerald-300 flex items-center gap-1.5">
                <Sparkles className="h-4 w-4" />
                {lang === 'es' ? 'Tutor AI Kid-Friendly Listo' : 'Kid-Friendly AI Tutor Ready'}
              </p>
              <p className="mt-1 text-[11px] text-warm-600 dark:text-warm-300 leading-relaxed">
                {lang === 'es'
                  ? 'Al guardar, el Family Coach AI generará misiones diarias y repasos socráticos de ortografía y matemáticas con la mascota elegida.'
                  : 'Upon saving, Family Coach AI will generate daily homework quests and gentle socratic reviews using the chosen mascot.'}
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
