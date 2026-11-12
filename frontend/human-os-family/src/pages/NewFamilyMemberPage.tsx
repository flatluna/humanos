import { useState } from 'react';
import { useNavigate, Link } from 'react-router';
import {
  ArrowLeft,
  User,
  Calendar,
  Phone,
  Mail,
  MapPin,
  Briefcase,
  CheckCircle2,
  AlertCircle,
  Loader2,
  Sparkles,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';
import { FamilyRole, FamilyMember } from '../types';
import { familyApi } from '../api/familyApi';
import { MIN_AGE_FOR_OWN_MSAL_ACCOUNT, requiresOwnMsalAccount } from '../lib/accountRules';

const AVATAR_COLORS = [
  { label: 'Esmeralda', value: 'from-emerald-600 to-teal-700', border: 'border-emerald-500' },
  { label: 'Índigo', value: 'from-indigo-600 to-violet-700', border: 'border-indigo-500' },
  { label: 'Ámbar', value: 'from-amber-500 to-orange-600', border: 'border-amber-500' },
  { label: 'Rosa / Coral', value: 'from-rose-500 to-pink-700', border: 'border-rose-500' },
  { label: 'Cian / Turquesa', value: 'from-teal-600 to-cyan-700', border: 'border-cyan-500' },
  { label: 'Púrpura Real', value: 'from-purple-600 to-indigo-800', border: 'border-purple-500' },
];

const ROLE_OPTIONS: { value: FamilyRole; labelEs: string; labelEn: string }[] = [
  { value: 'father', labelEs: 'Padre', labelEn: 'Father' },
  { value: 'mother', labelEs: 'Madre', labelEn: 'Mother' },
  { value: 'son', labelEs: 'Hijo', labelEn: 'Son' },
  { value: 'teen_daughter', labelEs: 'Hija', labelEn: 'Daughter / Teen Daughter' },
  { value: 'grandmother', labelEs: 'Abuela / Abuelo', labelEn: 'Grandparent' },
  { value: 'mentor', labelEs: 'Tutor / Mentor', labelEn: 'Guardian / Mentor' },
];

// Realistic professions and student options in English (matching Member Profile)
const PROFESSION_OPTIONS = [
  'Student / Child (Preschool / Kindergarten)',
  'Student / Child (Elementary School)',
  'Student (Middle School / Junior High)',
  'Student (High School)',
  'Student (College / University)',
  'Mother / Homemaker / Household Manager',
  'Software Engineer / Systems Architect',
  'Data Scientist / AI & Machine Learning Specialist',
  'Physician / Medical Doctor',
  'Pediatrician / Child Healthcare Specialist',
  'Neuropsychologist / Clinical Psychologist',
  'Registered Nurse / Healthcare Practitioner',
  'Dentist / Orthodontist',
  'Pharmacist / Pharmaceutical Scientist',
  'Corporate Executive / VP / Managing Director',
  'Chief Executive Officer (CEO) / Founder',
  'Chief Technology Officer (CTO)',
  'Entrepreneur / Business Owner',
  'Financial Analyst / Investment Manager',
  'Certified Public Accountant (CPA) / Auditor',
  'Attorney / Legal Counsel',
  'Civil / Structural Engineer',
  'Mechanical / Electrical Engineer',
  'Architect / Urban Designer',
  'Product Manager / Digital Product Lead',
  'UX/UI Designer / Creative Director',
  'Marketing Director / Brand Strategist',
  'Human Resources Director / People Operations',
  'Management Consultant / Strategy Advisor',
  'University Professor / Academic Researcher',
  'School Educator / K-12 Teacher',
  'Biomedical Researcher / Life Sciences Scientist',
  'Cybersecurity Specialist / InfoSec Lead',
  'Commercial Airline Pilot / Aviation Officer',
  'Real Estate Broker / Property Investor',
  'Supply Chain & Logistics Director',
  'Journalist / Communications & Media Specialist',
  'Non-Profit Director / Community Leader',
] as const;

const COUNTRY_DIAL_CODES = [
  { code: '+1', name: 'USA / Canadá', flag: '🇺🇸' },
  { code: '+52', name: 'México', flag: '🇲🇽' },
  { code: '+57', name: 'Colombia', flag: '🇨🇴' },
  { code: '+34', name: 'España', flag: '🇪🇸' },
  { code: '+54', name: 'Argentina', flag: '🇦🇷' },
  { code: '+56', name: 'Chile', flag: '🇨🇱' },
  { code: '+51', name: 'Perú', flag: '🇵🇪' },
  { code: '+55', name: 'Brasil', flag: '🇧🇷' },
  { code: '+593', name: 'Ecuador', flag: '🇪🇨' },
  { code: '+502', name: 'Guatemala', flag: '🇬🇹' },
  { code: '+506', name: 'Costa Rica', flag: '🇨🇷' },
  { code: '+507', name: 'Panamá', flag: '🇵🇦' },
  { code: '+44', name: 'Reino Unido', flag: '🇬🇧' },
  { code: '+33', name: 'Francia', flag: '🇫🇷' },
  { code: '+49', name: 'Alemania', flag: '🇩🇪' },
] as const;

function calculateAgeFromDob(dobString: string): number | null {
  if (!dobString) return null;
  const birth = new Date(dobString);
  if (isNaN(birth.getTime())) return null;
  const today = new Date();
  let years = today.getFullYear() - birth.getFullYear();
  const monthDiff = today.getMonth() - birth.getMonth();
  if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birth.getDate())) {
    years--;
  }
  return years >= 0 ? years : 0;
}

export function NewFamilyMemberPage() {
  const { lang } = useLanguage();
  const { family, householdCities, addMember, refreshFamily } = useFamily();
  const navigate = useNavigate();
  const isSpanish = lang === 'es';

  // Naming convention toggle: USA (First, Last) vs LATAM (First, Last, Mother's Last Name)
  const [namingConvention, setNamingConvention] = useState<'usa' | 'latam'>(
    isSpanish ? 'latam' : 'usa'
  );

  // Name fields
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [motherLastName, setMotherLastName] = useState('');
  const [preferredName, setPreferredName] = useState('');

  // Role and Profession
  const [familyRole, setFamilyRole] = useState<FamilyRole>('son');
  const [profession, setProfession] = useState('');

  // Birth date and age
  const [birthDate, setBirthDate] = useState('');
  const [age, setAge] = useState<number | ''>('');

  // Contact info
  const [email, setEmail] = useState('');
  const [phoneCountryCode, setPhoneCountryCode] = useState('+1');
  const [phoneNationalNumber, setPhoneNationalNumber] = useState('');

  // Avatar theme
  const [avatarBg, setAvatarBg] = useState(AVATAR_COLORS[0].value);

  // Submission state
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});

  // Primary location is inherited directly from the family household master record
  const familyAddress =
    family?.primaryFullAddress ||
    [family?.primaryStreet, family?.primaryCity, family?.primaryState, family?.primaryZipCode, family?.primaryCountry]
      .filter(Boolean)
      .join(', ') ||
    householdCities ||
    'Austin, TX, USA';

  // Compute full name according to selected convention
  const computedFullName =
    namingConvention === 'latam'
      ? [firstName.trim(), lastName.trim(), motherLastName.trim()].filter(Boolean).join(' ')
      : [firstName.trim(), lastName.trim()].filter(Boolean).join(' ');

  const initial =
    (preferredName || firstName || 'N').trim().charAt(0).toUpperCase() || 'N';

  const handleBirthDateChange = (val: string) => {
    setBirthDate(val);
    if (val) {
      const calculatedAge = calculateAgeFromDob(val);
      if (calculatedAge !== null) {
        setAge(calculatedAge);
      }
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    const newErrors: Record<string, string> = {};

    if (!firstName.trim()) {
      newErrors.firstName = isSpanish ? 'El nombre es requerido' : 'First name is required';
    }
    if (!lastName.trim()) {
      newErrors.lastName = isSpanish ? 'El apellido es requerido' : 'Last name is required';
    }

    if (Object.keys(newErrors).length > 0) {
      setErrors(newErrors);
      return;
    }

    setIsSubmitting(true);
    setErrorMsg(null);

    const fullPhoneNumber = phoneNationalNumber.trim()
      ? `${phoneCountryCode} ${phoneNationalNumber.trim()}`
      : null;

    const roleOption = ROLE_OPTIONS.find((r) => r.value === familyRole);
    const defaultRoleTitleEs = roleOption?.labelEs || 'Integrante';
    const defaultRoleTitleEn = roleOption?.labelEn || 'Member';
    const chosenRoleTitleEs = profession.trim() || defaultRoleTitleEs;
    const chosenRoleTitleEn = profession.trim() || defaultRoleTitleEn;

    try {
      await familyApi.createMember({
        fullName: computedFullName,
        preferredName: preferredName.trim() || firstName.trim(),
        familyRole: familyRole,
        roleTitleEs: chosenRoleTitleEs,
        roleTitleEn: chosenRoleTitleEn,
        dateOfBirth: birthDate ? `${birthDate}T00:00:00Z` : null,
        age: typeof age === 'number' ? age : null,
        primaryLocation: familyAddress,
        email: email.trim() || null,
        phoneNumber: fullPhoneNumber,
        phoneCountryCode: phoneCountryCode,
        avatarBg: avatarBg,
        weeklyHoursTarget: 8,
      });

      await refreshFamily();
      navigate('/family');
    } catch (err: any) {
      console.error('Failed to create member via API, fallback to context state:', err);
      // Fallback to local state if offline or endpoint error
      const newMember: FamilyMember = {
        id: `member-${Date.now()}`,
        name: computedFullName,
        preferredName: preferredName.trim() || firstName.trim(),
        role: familyRole,
        roleTitleEs: chosenRoleTitleEs,
        roleTitleEn: chosenRoleTitleEn,
        age: typeof age === 'number' ? age : 0,
        avatarBg: avatarBg,
        initial: initial,
        educationLevelEs: '',
        educationLevelEn: '',
        institutionEs: '',
        institutionEn: '',
        primaryLocation: familyAddress,
        topFocusEs: [],
        topFocusEn: [],
        weeklyHoursLogged: 0,
        learningStreakDays: 0,
        interests: [],
        languages: [],
        targetMilestoneEs: '',
        targetMilestoneEn: '',
      };
      addMember(newMember);
      navigate('/family');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-8 animate-fade-in pb-16">
      {/* Breadcrumb & Heading */}
      <div>
        <Link
          to="/family"
          className="inline-flex items-center gap-2 text-xs font-semibold text-indigo-600 hover:text-indigo-700 dark:text-indigo-400 mb-3 transition"
        >
          <ArrowLeft className="h-4 w-4" />
          <span>{isSpanish ? 'Volver a Núcleo Familiar' : 'Back to Family Core'}</span>
        </Link>

        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-3xl font-extrabold tracking-tight text-warm-900 dark:text-white">
            {isSpanish ? 'Dar de Alta Nuevo Integrante Familiar' : 'Register New Family Member'}
          </h1>
          <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-100 px-3 py-1 text-xs font-bold text-indigo-800 dark:bg-indigo-950/60 dark:text-indigo-300">
            <User className="h-3 w-3" />
            {isSpanish ? 'Expediente Maestro (Master File)' : 'Master File Record'}
          </span>
        </div>
        <p className="mt-1.5 text-sm text-warm-600 dark:text-warm-300 max-w-3xl">
          {isSpanish
            ? 'Registra los datos generales del integrante del núcleo familiar como expediente maestro. Posteriormente podrás configurar su trayectoria educativa e historial escolar en su perfil.'
            : 'Register household member master details. Academic history and schooling can be subsequently configured in their member profile.'}
        </p>
      </div>

      {errorMsg && (
        <div className="flex items-center gap-2 rounded-2xl border border-rose-200 bg-rose-50/80 p-4 text-xs font-semibold text-rose-800 dark:border-rose-800 dark:bg-rose-950/40 dark:text-rose-300">
          <AlertCircle className="h-4 w-4 text-rose-600 shrink-0" />
          <span>{errorMsg}</span>
        </div>
      )}

      <div className="grid grid-cols-1 gap-8 lg:grid-cols-3">
        {/* Main Master Form */}
        <form onSubmit={handleSave} className="space-y-6 lg:col-span-2">
          {/* SECTION 1: Personal Master Info */}
          <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a] space-y-5">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-warm-100 pb-3 dark:border-white/10">
              <div className="flex items-center gap-2.5">
                <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-indigo-100 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
                  <User className="h-4 w-4" />
                </span>
                <div>
                  <h2 className="text-base font-bold text-warm-900 dark:text-white">
                    {isSpanish ? '1. Información del Integrante' : '1. Member Information'}
                  </h2>
                  <p className="text-xs text-warm-500">
                    {isSpanish ? 'Datos generales y filiación familiar' : 'Master personal details and family role'}
                  </p>
                </div>
              </div>

              {/* USA vs LATAM Naming Convention Toggle */}
              <div className="flex items-center gap-1 rounded-xl bg-warm-100 p-1 dark:bg-slate-800 text-xs self-start sm:self-auto">
                <button
                  type="button"
                  onClick={() => setNamingConvention('usa')}
                  className={`rounded-lg px-3 py-1 font-bold transition ${
                    namingConvention === 'usa'
                      ? 'bg-white text-indigo-700 shadow-xs dark:bg-slate-900 dark:text-indigo-400'
                      : 'text-warm-600 hover:text-warm-900 dark:text-warm-400 dark:hover:text-white'
                  }`}
                >
                  🇺🇸 USA (First & Last)
                </button>
                <button
                  type="button"
                  onClick={() => setNamingConvention('latam')}
                  className={`rounded-lg px-3 py-1 font-bold transition ${
                    namingConvention === 'latam'
                      ? 'bg-white text-indigo-700 shadow-xs dark:bg-slate-900 dark:text-indigo-400'
                      : 'text-warm-600 hover:text-warm-900 dark:text-warm-400 dark:hover:text-white'
                  }`}
                >
                  🌎 LATAM (Nombre + 2 Apellidos)
                </button>
              </div>
            </div>

            {/* Dynamic Name Input Fields */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {namingConvention === 'latam' ? (
                <>
                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      Nombre(s) *
                    </label>
                    <input
                      type="text"
                      value={firstName}
                      onChange={(e) => {
                        setFirstName(e.target.value);
                        if (errors.firstName) setErrors((prev) => ({ ...prev, firstName: '' }));
                      }}
                      placeholder="ej. Mateo Alejandro"
                      className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                    {errors.firstName && (
                      <p className="mt-1 text-[11px] text-rose-500 font-semibold">{errors.firstName}</p>
                    )}
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      Primer Apellido (Paterno) *
                    </label>
                    <input
                      type="text"
                      value={lastName}
                      onChange={(e) => {
                        setLastName(e.target.value);
                        if (errors.lastName) setErrors((prev) => ({ ...prev, lastName: '' }));
                      }}
                      placeholder="ej. Mendoza"
                      className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                    {errors.lastName && (
                      <p className="mt-1 text-[11px] text-rose-500 font-semibold">{errors.lastName}</p>
                    )}
                  </div>

                  <div className="sm:col-span-2">
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      Segundo Apellido / Materno (Mother's Last Name)
                    </label>
                    <input
                      type="text"
                      value={motherLastName}
                      onChange={(e) => setMotherLastName(e.target.value)}
                      placeholder="ej. Ríos"
                      className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  </div>
                </>
              ) : (
                <>
                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      First Name *
                    </label>
                    <input
                      type="text"
                      value={firstName}
                      onChange={(e) => {
                        setFirstName(e.target.value);
                        if (errors.firstName) setErrors((prev) => ({ ...prev, firstName: '' }));
                      }}
                      placeholder="e.g. Matthew"
                      className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                    {errors.firstName && (
                      <p className="mt-1 text-[11px] text-rose-500 font-semibold">{errors.firstName}</p>
                    )}
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      Last Name *
                    </label>
                    <input
                      type="text"
                      value={lastName}
                      onChange={(e) => {
                        setLastName(e.target.value);
                        if (errors.lastName) setErrors((prev) => ({ ...prev, lastName: '' }));
                      }}
                      placeholder="e.g. Miller"
                      className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                    {errors.lastName && (
                      <p className="mt-1 text-[11px] text-rose-500 font-semibold">{errors.lastName}</p>
                    )}
                  </div>
                </>
              )}

              {/* Preferred Name / Nickname */}
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Nombre Preferido / Apodo' : 'Preferred Name / Nickname'}
                </label>
                <input
                  type="text"
                  value={preferredName}
                  onChange={(e) => setPreferredName(e.target.value)}
                  placeholder={isSpanish ? 'ej. Matt / Mateo' : 'e.g. Matt'}
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
              </div>

              {/* Family Role */}
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Rol en la Familia *' : 'Family Role *'}
                </label>
                <select
                  value={familyRole}
                  onChange={(e) => setFamilyRole(e.target.value as FamilyRole)}
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                >
                  {ROLE_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {isSpanish ? opt.labelEs : opt.labelEn}
                    </option>
                  ))}
                </select>
              </div>

              {/* Profession / Role Title */}
              <div className="sm:col-span-2">
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  <span className="flex items-center gap-1.5">
                    <Briefcase className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                    <span>{isSpanish ? 'Profesión / Ocupación (incluye Student para niños)' : 'Profession / Occupation (includes Student for children)'}</span>
                  </span>
                </label>
                <select
                  value={profession}
                  onChange={(e) => setProfession(e.target.value)}
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                >
                  <option value="">
                    {isSpanish
                      ? '-- Selecciona una profesión u opción de Student (en inglés) --'
                      : '-- Select a profession or Student option (in English) --'}
                  </option>
                  {PROFESSION_OPTIONS.map((prof) => (
                    <option key={prof} value={prof}>
                      {prof}
                    </option>
                  ))}
                </select>
              </div>

              {/* Date of Birth & Age */}
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  <span className="flex items-center gap-1.5">
                    <Calendar className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                    <span>{isSpanish ? 'Fecha de Nacimiento (Calendario)' : 'Date of Birth (Calendar)'}</span>
                  </span>
                </label>
                <input
                  type="date"
                  value={birthDate}
                  onChange={(e) => handleBirthDateChange(e.target.value)}
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Edad (Calculada automáticamente)' : 'Age (Calculated automatically)'}
                </label>
                <div className="flex items-center gap-2">
                  <input
                    type="number"
                    min="0"
                    max="120"
                    value={age}
                    onChange={(e) => setAge(e.target.value ? Number(e.target.value) : '')}
                    placeholder="ej. 10"
                    className="flex-1 rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs font-bold text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  />
                  <span className="text-xs font-semibold text-warm-400">
                    {isSpanish ? 'años' : 'yrs'}
                  </span>
                </div>
              </div>

              {/* Email */}
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  <span className="flex items-center gap-1.5">
                    <Mail className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                    <span>{isSpanish ? 'Correo Electrónico (Email)' : 'Email Address'}</span>
                  </span>
                </label>
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="ej. integrante@familia.com"
                  className="w-full rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
                {/* Age-based MSAL account rule: <12 managed by parent, 12+ gets own login */}
                {age !== '' && (
                  requiresOwnMsalAccount(age) ? (
                    <p className="mt-1.5 text-[11px] font-medium text-emerald-700 dark:text-emerald-400">
                      {isSpanish
                        ? `✅ Por ser mayor de ${MIN_AGE_FOR_OWN_MSAL_ACCOUNT} años, este integrante podrá tener su propio usuario y contraseña (MSAL) usando este correo.`
                        : `✅ Being ${MIN_AGE_FOR_OWN_MSAL_ACCOUNT}+ years old, this member can have their own MSAL username and password with this email.`}
                    </p>
                  ) : (
                    <p className="mt-1.5 text-[11px] font-medium text-warm-500 dark:text-warm-400">
                      {isSpanish
                        ? `ℹ️ Por ser menor de ${MIN_AGE_FOR_OWN_MSAL_ACCOUNT} años, este integrante NO recibirá usuario/contraseña propios. Su actividad será gestionada desde la cuenta del padre/madre administrador(a).`
                        : `ℹ️ Being under ${MIN_AGE_FOR_OWN_MSAL_ACCOUNT} years old, this member will NOT get their own username/password. Their activity is managed from the parent/admin account.`}
                    </p>
                  )
                )}
              </div>

              {/* Phone with Country Dial Code */}
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  <span className="flex items-center gap-1.5">
                    <Phone className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                    <span>{isSpanish ? 'Número Telefónico Móvil' : 'Mobile Phone Number'}</span>
                  </span>
                </label>
                <div className="flex gap-2">
                  <select
                    value={phoneCountryCode}
                    onChange={(e) => setPhoneCountryCode(e.target.value)}
                    className="w-36 rounded-xl border border-warm-200 bg-warm-50/60 px-2 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white shrink-0"
                  >
                    {COUNTRY_DIAL_CODES.map((c) => (
                      <option key={c.code} value={c.code}>
                        {c.flag} {c.code} ({c.name})
                      </option>
                    ))}
                  </select>
                  <input
                    type="tel"
                    value={phoneNationalNumber}
                    onChange={(e) => setPhoneNationalNumber(e.target.value)}
                    placeholder={phoneCountryCode === '+52' ? '55 1234 5678' : '(512) 555-0199'}
                    className="flex-1 rounded-xl border border-warm-200 bg-warm-50/60 px-3.5 py-2.5 text-xs text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  />
                </div>
              </div>

              {/* Primary Location (Family Household Address) */}
              <div className="sm:col-span-2 rounded-2xl border border-indigo-100 bg-indigo-50/50 p-4 dark:border-indigo-500/20 dark:bg-slate-900/60">
                <div className="flex items-center gap-2 text-indigo-700 dark:text-indigo-400 mb-2">
                  <MapPin className="h-4 w-4" />
                  <span className="text-xs font-bold uppercase tracking-wider">
                    {isSpanish
                      ? 'Ubicación Principal (Heredada del Hogar Familiar)'
                      : 'Primary Location (Family Household Address)'}
                  </span>
                </div>
                <div className="flex items-start gap-2.5">
                  <span className="text-lg">🏠</span>
                  <div>
                    <p className="text-xs font-bold text-warm-900 dark:text-white">
                      {familyAddress}
                    </p>
                    <p className="text-[11px] text-warm-500 dark:text-warm-400 mt-0.5 leading-relaxed">
                      {isSpanish
                        ? 'El integrante residirá en la dirección registrada del núcleo familiar. No es necesario ingresar una dirección nueva.'
                        : 'The member resides at the registered family household address. No separate address required.'}
                    </p>
                  </div>
                </div>
              </div>
            </div>

            {/* Avatar Color Palette */}
            <div className="pt-2 border-t border-warm-100 dark:border-white/10">
              <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-2">
                {isSpanish ? 'Color Temático del Avatar' : 'Avatar Color Theme'}
              </label>
              <div className="flex flex-wrap gap-2.5">
                {AVATAR_COLORS.map((c, i) => (
                  <button
                    key={i}
                    type="button"
                    onClick={() => setAvatarBg(c.value)}
                    className={`flex items-center gap-2 rounded-xl border px-3 py-1.5 text-xs font-medium transition ${
                      avatarBg === c.value
                        ? 'border-indigo-600 bg-indigo-50 font-bold dark:border-indigo-400 dark:bg-indigo-950/40 text-indigo-900 dark:text-indigo-200 ring-2 ring-indigo-400'
                        : 'border-warm-200 bg-white hover:bg-warm-50 dark:border-white/10 dark:bg-slate-900 text-warm-700 dark:text-warm-300'
                    }`}
                  >
                    <span className={`h-4 w-4 rounded-full bg-gradient-to-br ${c.value} shadow-xs`} />
                    <span>{c.label}</span>
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Form Actions */}
          <div className="flex items-center justify-end gap-3 pt-2">
            <Link
              to="/family"
              className="rounded-2xl border border-warm-200 bg-white px-5 py-3 text-xs font-bold text-warm-700 shadow-xs hover:bg-warm-50 dark:border-white/10 dark:bg-warm-800 dark:text-warm-200"
            >
              {isSpanish ? 'Cancelar' : 'Cancel'}
            </Link>

            <button
              type="submit"
              disabled={isSubmitting}
              className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 px-6 py-3 text-xs font-bold text-white shadow-md shadow-indigo-600/30 transition hover:from-indigo-700 hover:to-violet-700 hover:shadow-lg disabled:opacity-60"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  <span>{isSpanish ? 'Guardando...' : 'Saving...'}</span>
                </>
              ) : (
                <>
                  <CheckCircle2 className="h-4 w-4" />
                  <span>
                    {isSpanish
                      ? 'Guardar e Integrar a la Familia'
                      : 'Save & Add to Household'}
                  </span>
                </>
              )}
            </button>
          </div>
        </form>

        {/* Live Card Preview Column */}
        <div className="space-y-4">
          <div className="sticky top-28">
            <div className="rounded-2xl bg-warm-100 p-2 text-center text-[11px] font-bold text-warm-600 dark:bg-white/5 dark:text-warm-400 mb-3 flex items-center justify-center gap-1.5">
              <span>👀</span>
              <span>{isSpanish ? 'Vista Previa en Tiempo Real' : 'Live Card Preview'}</span>
            </div>

            {/* Member Master Card Preview */}
            <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-md dark:border-white/10 dark:bg-[#0f172a] space-y-4">
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-center gap-4">
                  <div
                    className={`flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br ${avatarBg} text-2xl font-black text-white shadow-md`}
                  >
                    {initial}
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-warm-900 dark:text-white">
                      {computedFullName || (isSpanish ? 'Nombre del Integrante' : 'Member Full Name')}
                    </h3>
                    <p className="text-xs font-semibold text-indigo-600 dark:text-indigo-400">
                      {profession ||
                        (ROLE_OPTIONS.find((r) => r.value === familyRole)?.[isSpanish ? 'labelEs' : 'labelEn'] ??
                          'Integrante')}
                    </p>
                    <p className="text-[11px] text-warm-500 dark:text-warm-400">
                      {age !== '' ? `${age} ${isSpanish ? 'años' : 'years old'}` : (isSpanish ? 'Edad no especificada' : 'Age not specified')}
                    </p>
                  </div>
                </div>

                <span className="rounded-full bg-indigo-50 px-2.5 py-1 text-xs font-bold text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
                  {ROLE_OPTIONS.find((r) => r.value === familyRole)?.[isSpanish ? 'labelEs' : 'labelEn']}
                </span>
              </div>

              <div className="border-t border-warm-100 pt-4 dark:border-white/10 space-y-2 text-xs">
                <div className="flex items-center gap-2 text-warm-600 dark:text-warm-300">
                  <MapPin className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400 shrink-0" />
                  <span className="truncate">{familyAddress}</span>
                </div>

                {email && (
                  <div className="flex items-center gap-2 text-warm-600 dark:text-warm-300">
                    <Mail className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400 shrink-0" />
                    <span className="truncate">{email}</span>
                  </div>
                )}

                {phoneNationalNumber && (
                  <div className="flex items-center gap-2 text-warm-600 dark:text-warm-300">
                    <Phone className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400 shrink-0" />
                    <span>{phoneCountryCode} {phoneNationalNumber}</span>
                  </div>
                )}

                {birthDate && (
                  <div className="flex items-center gap-2 text-warm-600 dark:text-warm-300">
                    <Calendar className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400 shrink-0" />
                    <span>{birthDate}</span>
                  </div>
                )}
              </div>
            </div>

            {/* Informational tip */}
            <div className="mt-4 rounded-2xl border border-indigo-200 bg-indigo-50/50 p-4 dark:border-indigo-500/20 dark:bg-indigo-950/20">
              <p className="text-xs font-bold text-indigo-900 dark:text-indigo-300 flex items-center gap-1.5">
                <Sparkles className="h-4 w-4" />
                {isSpanish ? 'Expediente Maestro' : 'Master File Ready'}
              </p>
              <p className="mt-1 text-[11px] text-warm-600 dark:text-warm-300 leading-relaxed">
                {isSpanish
                  ? 'Una vez dado de alta, podrás registrar su trayectoria escolar, grados académicos, calificaciones y metas pedagógicas en su pestaña de educación.'
                  : 'Once registered, academic history, grade levels, GPA, and educational goals can be managed in their member education tab.'}
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
