import { useEffect, useState, useRef } from 'react';
import { useParams, Link, useNavigate } from 'react-router';
import {
  ArrowLeft,
  User,
  GraduationCap,
  Save,
  Loader2,
  CheckCircle2,
  AlertCircle,
  Plus,
  Edit2,
  Trash2,
  Building2,
  MapPin,
  Calendar,
  Search,
  Lock,
  Phone,
  Home,
  ShieldCheck,
} from 'lucide-react';
import { useLanguage } from '../context/LanguageContext';
import { useFamily } from '../context/FamilyContext';
import { useAuth } from '../auth/AuthContext';
import { requiresOwnMsalAccount } from '../lib/accountRules';
import {
  familyApi,
  type FamilyMemberRecord,
  type FamilyMemberEducationRecord,
  type AddressSuggestion,
} from '../api/familyApi';

const EDUCATION_LEVEL_OPTIONS = [
  { value: 'Preescolar / Kinder', labelEs: 'Preescolar / Kinder (Preschool / Kindergarten)', labelEn: 'Preschool / Kindergarten' },
  { value: 'Primaria', labelEs: 'Primaria (Elementary School)', labelEn: 'Elementary School' },
  { value: 'Secundaria', labelEs: 'Secundaria (Middle / Junior High)', labelEn: 'Middle / Junior High School' },
  { value: 'Preparatoria', labelEs: 'Preparatoria / High School (Bachillerato)', labelEn: 'High School' },
  { value: 'Universidad', labelEs: 'Universidad / Licenciatura (College / Undergraduate)', labelEn: 'Undergraduate / College' },
  { value: 'Posgrado', labelEs: 'Posgrado / Maestría / Doctorado (Graduate / Ph.D.)', labelEn: 'Graduate / Master / Ph.D.' },
  { value: 'Certificación / Técnico', labelEs: 'Educación Técnica / Asociado', labelEn: 'Vocational / Associate' },
] as const;

// Países en orden requerido (México, USA, Argentina, Colombia, Chile...)
const EDUCATION_COUNTRIES = [
  { code: 'México', label: '🇲🇽 México' },
  { code: 'USA', label: '🇺🇸 USA' },
  { code: 'Argentina', label: '🇦🇷 Argentina' },
  { code: 'Colombia', label: '🇨🇴 Colombia' },
  { code: 'Chile', label: '🇨🇱 Chile' },
  { code: 'Canadá', label: '🇨🇦 Canadá' },
  { code: 'España', label: '🇪🇸 España' },
  { code: 'Otro', label: '🌐 Otro país' },
] as const;

const COUNTRY_STATES: Record<string, string[]> = {
  'USA': [
    'Texas', 'California', 'Florida', 'New York', 'Washington', 'Illinois', 'Pennsylvania', 'Ohio',
    'Georgia', 'North Carolina', 'Michigan', 'New Jersey', 'Virginia', 'Washington D.C.', 'Massachusetts',
    'Arizona', 'Tennessee', 'Indiana', 'Missouri', 'Maryland', 'Wisconsin', 'Colorado', 'Minnesota',
    'South Carolina', 'Alabama', 'Louisiana', 'Kentucky', 'Oregon', 'Oklahoma', 'Connecticut', 'Utah',
    'Iowa', 'Nevada', 'Arkansas', 'Mississippi', 'Kansas', 'New Mexico', 'Nebraska', 'Idaho',
    'West Virginia', 'Hawaii', 'New Hampshire', 'Maine', 'Montana', 'Rhode Island', 'Delaware',
    'South Dakota', 'North Dakota', 'Alaska', 'Vermont', 'Wyoming'
  ],
  'México': [
    'Aguascalientes', 'Baja California', 'Baja California Sur', 'Campeche', 'Chiapas', 'Chihuahua',
    'Ciudad de México (CDMX)', 'Coahuila', 'Colima', 'Durango', 'Estado de México', 'Guanajuato',
    'Guerrero', 'Hidalgo', 'Jalisco', 'Michoacán', 'Morelos', 'Nayarit', 'Nuevo León', 'Oaxaca',
    'Puebla', 'Querétaro', 'Quintana Roo', 'San Luis Potosí', 'Sinaloa', 'Sonora', 'Tabasco',
    'Tamaulipas', 'Tlaxcala', 'Veracruz', 'Yucatán', 'Zacatecas'
  ],
  'Argentina': [
    'Buenos Aires', 'Ciudad Autónoma de Buenos Aires (CABA)', 'Catamarca', 'Chaco', 'Chubut', 'Córdoba',
    'Corrientes', 'Entre Ríos', 'Formosa', 'Jujuy', 'La Pampa', 'La Rioja', 'Mendoza', 'Misiones',
    'Neuquén', 'Río Negro', 'Salta', 'San Juan', 'San Luis', 'Santa Cruz', 'Santa Fe', 'Santiago del Estero',
    'Tierra del Fuego', 'Tucumán'
  ],
  'Colombia': [
    'Amazonas', 'Antioquia', 'Arauca', 'Atlántico', 'Bogotá D.C.', 'Bolívar', 'Boyacá', 'Caldas',
    'Caquetá', 'Casanare', 'Cauca', 'Cesar', 'Chocó', 'Córdoba', 'Cundinamarca', 'Guainía', 'Guaviare',
    'Huila', 'La Guajira', 'Magdalena', 'Meta', 'Nariño', 'Norte de Santander', 'Putumayo', 'Quindío',
    'Risaralda', 'San Andrés y Providencia', 'Santander', 'Sucre', 'Tolima', 'Valle del Cauca', 'Vaupés', 'Vichada'
  ],
  'Chile': [
    'Región Metropolitana de Santiago', 'Región de Valparaíso', 'Región del Biobío', 'Región de Antofagasta',
    'Región de La Araucanía', 'Región de Los Lagos', 'Región de Coquimbo', 'Región del Maule', 'Región de O\'Higgins',
    'Región de Los Ríos', 'Región de Tarapacá', 'Región de Arica y Parinacota', 'Región de Atacama', 'Región de Ñuble',
    'Región de Aysén', 'Región de Magallanes'
  ],
};

// Lista cronológica detallada para Grade or Degree: kinder primero, primaria, secundaria, etc.
const GRADE_OR_DEGREE_GROUPS = [
  {
    group: '👶 Kinder / Preescolar (Early Childhood)',
    options: [
      'Maternal / Guardería (1-2 años)',
      'Preescolar 1 (Pre-K 3)',
      'Preescolar 2 (Pre-K 4)',
      'Kindergarten / Preescolar 3 (5-6 años)',
    ],
  },
  {
    group: '🎒 Primaria (Elementary School)',
    options: [
      '1º de Primaria / 1st Grade',
      '2º de Primaria / 2nd Grade',
      '3º de Primaria / 3rd Grade',
      '4º de Primaria / 4th Grade',
      '5º de Primaria / 5th Grade',
      '6º de Primaria / 6th Grade',
      'Primaria Concluida / Elementary Graduate',
    ],
  },
  {
    group: '🏫 Secundaria (Middle / Junior High)',
    options: [
      '1º de Secundaria / 7th Grade',
      '2º de Secundaria / 8th Grade',
      '3º de Secundaria / 9th Grade',
      'Secundaria Concluida / Middle School Graduate',
    ],
  },
  {
    group: '🎓 Preparatoria / Bachillerato (High School)',
    options: [
      '1º de Preparatoria / 10th Grade (Freshman)',
      '2º de Preparatoria / 11th Grade (Junior)',
      '3º de Preparatoria / 12th Grade (Senior)',
      'Preparatoria Concluida / High School Diploma',
      'Bachillerato Internacional (IB Diploma)',
      'Advanced Placement (AP Capstone)',
    ],
  },
  {
    group: '⚙️ Educación Técnica & Grado Asociado',
    options: [
      'Carrera Técnica / Técnico Superior Universitario (TSU)',
      'Grado Asociado / Associate Degree (A.A., A.S.)',
    ],
  },
  {
    group: '🏛️ Universidad / Licenciatura / Pregrado (College / University)',
    options: [
      'Estudiante Universitario / Undergraduate (College)',
      'Licenciatura / Grado Profesional (Bachelor\'s Degree)',
      'Ingeniería / B.S. in Engineering',
      'Título Profesional Universitario Concluido',
    ],
  },
  {
    group: '📚 Posgrado / Maestría (Graduate / Master)',
    options: [
      'Estudiante de Maestría / Master\'s Candidate',
      'Maestría Concluida / Master\'s Degree (M.S., M.A., MBA, M.Ed.)',
    ],
  },
  {
    group: '🔬 Doctorado & Alta Especialidad (Ph.D. & Fellowship)',
    options: [
      'Candidato a Doctorado / Ph.D. Candidate',
      'Doctorado / Ph.D. Concluido',
      'Especialidad Médica / Residencia / Fellowship (M.D.)',
      'Posdoctorado / Postdoctoral Fellow',
    ],
  },
] as const;

// Opciones de títulos profesionales comunes para college/universidad
const COLLEGE_DEGREE_SUGGESTIONS = [
  'B.S. Computer Science / Ingeniería en Sistemas',
  'B.S. Mechanical Engineering / Ing. Mecánica',
  'B.S. Electrical Engineering / Ing. Eléctrica',
  'B.S. Industrial Engineering / Ing. Industrial',
  'B.S. Biomedical Engineering / Bioingeniería',
  'B.S. Data Science & Artificial Intelligence',
  'B.A. Economics / Licenciatura en Economía',
  'B.B.A. Business Administration / Admón. de Empresas',
  'B.A. Finance / Finanzas',
  'B.A. Marketing / Mercadotecnia',
  'Licenciatura en Derecho / Law Degree (J.D.)',
  'Médico Cirujano / Doctor of Medicine (M.D.)',
  'Licenciatura en Psicología / B.A. Psychology',
  'Arquitectura / Bachelor of Architecture (B.Arch)',
  'Licenciatura en Comunicación / Media & Journalism',
  'M.S. Master of Science',
  'M.B.A. Master of Business Administration',
  'Ph.D. Doctor of Philosophy',
] as const;

const ROLE_OPTIONS = [
  { value: 'father', labelEs: 'Padre', labelEn: 'Father' },
  { value: 'mother', labelEs: 'Madre', labelEn: 'Mother' },
  { value: 'teen_daughter', labelEs: 'Hija adolescente', labelEn: 'Teen Daughter' },
  { value: 'son', labelEs: 'Hijo', labelEn: 'Son' },
  { value: 'grandmother', labelEs: 'Abuela / Abuelo', labelEn: 'Grandparent' },
  { value: 'mentor', labelEs: 'Tutor / Mentor', labelEn: 'Guardian / Mentor' },
] as const;

// Realistic professions and student options in English (including Mother / Homemaker & Students for children)
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

// Curated list of world languages for selection
const LANGUAGE_OPTIONS = [
  { value: 'Spanish', labelEs: 'Español', labelEn: 'Spanish', flag: '🇪🇸' },
  { value: 'English', labelEs: 'Inglés', labelEn: 'English', flag: '🇺🇸' },
  { value: 'French', labelEs: 'Francés', labelEn: 'French', flag: '🇫🇷' },
  { value: 'German', labelEs: 'Alemán', labelEn: 'German', flag: '🇩🇪' },
  { value: 'Italian', labelEs: 'Italiano', labelEn: 'Italian', flag: '🇮🇹' },
  { value: 'Portuguese', labelEs: 'Portugués', labelEn: 'Portuguese', flag: '🇧🇷' },
  { value: 'Mandarin Chinese', labelEs: 'Chino Mandarín', labelEn: 'Mandarin Chinese', flag: '🇨🇳' },
  { value: 'Japanese', labelEs: 'Japonés', labelEn: 'Japanese', flag: '🇯🇵' },
  { value: 'Korean', labelEs: 'Coreano', labelEn: 'Korean', flag: '🇰🇷' },
  { value: 'Russian', labelEs: 'Ruso', labelEn: 'Russian', flag: '🇷🇺' },
  { value: 'Arabic', labelEs: 'Árabe', labelEn: 'Arabic', flag: '🇸🇦' },
  { value: 'Hindi', labelEs: 'Hindi', labelEn: 'Hindi', flag: '🇮🇳' },
  { value: 'Dutch', labelEs: 'Holandés / Neerlandés', labelEn: 'Dutch', flag: '🇳🇱' },
  { value: 'Swedish', labelEs: 'Sueco', labelEn: 'Swedish', flag: '🇸🇪' },
  { value: 'Polish', labelEs: 'Polaco', labelEn: 'Polish', flag: '🇵🇱' },
  { value: 'Turkish', labelEs: 'Turco', labelEn: 'Turkish', flag: '🇹🇷' },
  { value: 'Vietnamese', labelEs: 'Vietnamita', labelEn: 'Vietnamese', flag: '🇻🇳' },
  { value: 'Hebrew', labelEs: 'Hebreo', labelEn: 'Hebrew', flag: '🇮🇱' },
  { value: 'Greek', labelEs: 'Griego', labelEn: 'Greek', flag: '🇬🇷' },
  { value: 'Latin', labelEs: 'Latín', labelEn: 'Latin', flag: '🏛️' },
  { value: 'American Sign Language (ASL)', labelEs: 'Lengua de Señas Americana (ASL)', labelEn: 'American Sign Language (ASL)', flag: '🤟' },
  { value: 'Tagalog', labelEs: 'Tagalo / Filipino', labelEn: 'Tagalog / Filipino', flag: '🇵🇭' },
  { value: 'Cantonese', labelEs: 'Cantonés', labelEn: 'Cantonese', flag: '🇭🇰' },
  { value: 'Catalan', labelEs: 'Catalán', labelEn: 'Catalan', flag: '🇪🇸' },
  { value: 'Basque', labelEs: 'Euskera (Vasco)', labelEn: 'Basque', flag: '🇪🇸' },
  { value: 'Nahuatl', labelEs: 'Náhuatl', labelEn: 'Nahuatl', flag: '🇲🇽' },
  { value: 'Quechua', labelEs: 'Quechua', labelEn: 'Quechua', flag: '🇵🇪' },
  { value: 'Mayan', labelEs: 'Maya', labelEn: 'Mayan', flag: '🇲🇽' },
] as const;

function parseFullName(fullName: string) {
  const trimmed = (fullName || '').trim();
  if (!trimmed) {
    return { firstName: '', lastName: '', motherLastName: '' };
  }
  const parts = trimmed.split(/\s+/);
  if (parts.length === 1) {
    return { firstName: parts[0], lastName: '', motherLastName: '' };
  }
  if (parts.length === 2) {
    return { firstName: parts[0], lastName: parts[1], motherLastName: '' };
  }
  if (parts.length === 3) {
    return { firstName: parts[0], lastName: parts[1], motherLastName: parts[2] };
  }
  // 4 or more parts, e.g. "Jorge Alberto Luna Pérez"
  return {
    firstName: parts.slice(0, parts.length - 2).join(' '),
    lastName: parts[parts.length - 2],
    motherLastName: parts[parts.length - 1],
  };
}

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

function parsePhoneNumber(fullPhone: string | null | undefined, defaultCode = '+1') {
  if (!fullPhone) return { countryCode: defaultCode, nationalNumber: '' };
  const trimmed = fullPhone.trim();
  for (const item of COUNTRY_DIAL_CODES) {
    if (trimmed.startsWith(item.code)) {
      const rest = trimmed.slice(item.code.length).trim();
      return { countryCode: item.code, nationalNumber: rest };
    }
  }
  return { countryCode: defaultCode, nationalNumber: trimmed };
}

export function MemberMainPage() {
  const { memberId } = useParams<{ memberId: string }>();
  const navigate = useNavigate();
  const { lang } = useLanguage();
  const { family, refreshFamily } = useFamily();
  const { account } = useAuth();
  const isSpanish = lang === 'es';

  // Link MSAL account (Azure OID + real login email) state
  const [isLinkingAccount, setIsLinkingAccount] = useState(false);
  const [linkAccountMsg, setLinkAccountMsg] = useState<string | null>(null);

  // Delete member state
  const [isDeletingMember, setIsDeletingMember] = useState(false);
  const [deleteMemberError, setDeleteMemberError] = useState<string | null>(null);

  async function handleDeleteMember() {
    if (!member) return;
    const confirmed = window.confirm(
      isSpanish
        ? `¿Seguro que quieres eliminar a "${member.fullName}" de la familia? Esta acción no se puede deshacer desde la app.`
        : `Are you sure you want to remove "${member.fullName}" from the family? This cannot be undone from the app.`
    );
    if (!confirmed) return;

    setDeleteMemberError(null);
    setIsDeletingMember(true);
    try {
      await familyApi.deleteMember(member.memberId);
      await refreshFamily();
      navigate('/family');
    } catch (err) {
      setDeleteMemberError(err instanceof Error ? err.message : String(err));
      setIsDeletingMember(false);
    }
  }

  // Active Tab: 1 = Datos del Miembro, 2 = Situación Educativa
  const [activeTab, setActiveTab] = useState<'profile' | 'education'>('profile');

  // Loading & error states
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Member data (Tab 1)
  const [member, setMember] = useState<FamilyMemberRecord | null>(null);
  const [memberForm, setMemberForm] = useState<Partial<FamilyMemberRecord>>({});
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [motherLastName, setMotherLastName] = useState('');
  const [phoneCountryCode, setPhoneCountryCode] = useState('+1');
  const [phoneNationalNumber, setPhoneNationalNumber] = useState('');
  const [newLanguageInput, setNewLanguageInput] = useState('');
  const [newFocusInput, setNewFocusInput] = useState('');
  const [isSavingMember, setIsSavingMember] = useState(false);
  const [memberSuccessMsg, setMemberSuccessMsg] = useState<string | null>(null);
  const [memberErrorMsg, setMemberErrorMsg] = useState<string | null>(null);

  // Bing Maps address autocomplete & filter state
  const [addressFilterQuery, setAddressFilterQuery] = useState('');
  const [addressSuggestions, setAddressSuggestions] = useState<AddressSuggestion[]>([]);
  const [isSearchingAddress, setIsSearchingAddress] = useState(false);
  const addressSearchTimerRef = useRef<number | null>(null);

  // Education data (Tab 2)
  const [educations, setEducations] = useState<FamilyMemberEducationRecord[]>([]);
  const [isEditingEdu, setIsEditingEdu] = useState(false);
  const [selectedEduId, setSelectedEduId] = useState<string | null>(null);
  const [eduForm, setEduForm] = useState<{
    educationLevel: string;
    schoolName: string;
    gradeOrDegree: string;
    fieldOfStudy: string;
    degree: string;
    gpa: string;
    startYear: string;
    endYear: string;
    isCurrent: boolean;
    status: string;
    country: string;
    state: string;
    city: string;
    notes: string;
  }>({
    educationLevel: 'Secundaria',
    schoolName: '',
    gradeOrDegree: '',
    fieldOfStudy: '',
    degree: '',
    gpa: '',
    startYear: '',
    endYear: '',
    isCurrent: true,
    status: 'Current',
    country: 'México',
    state: '',
    city: '',
    notes: '',
  });
  const [isSavingEdu, setIsSavingEdu] = useState(false);
  const [eduSuccessMsg, setEduSuccessMsg] = useState<string | null>(null);
  const [eduErrorMsg, setEduErrorMsg] = useState<string | null>(null);

  function calculateAgeFromDob(dobString: string): number {
    const birth = new Date(dobString);
    if (isNaN(birth.getTime())) return 0;
    const today = new Date();
    let age = today.getFullYear() - birth.getFullYear();
    const m = today.getMonth() - birth.getMonth();
    if (m < 0 || (m === 0 && today.getDate() < birth.getDate())) {
      age--;
    }
    return age >= 0 ? age : 0;
  }

  function handleAddressFilterChange(query: string) {
    setAddressFilterQuery(query);

    if (addressSearchTimerRef.current) {
      window.clearTimeout(addressSearchTimerRef.current);
    }

    if (!query || query.trim().length < 3) {
      setAddressSuggestions([]);
      setIsSearchingAddress(false);
      return;
    }

    setIsSearchingAddress(true);
    addressSearchTimerRef.current = window.setTimeout(() => {
      void familyApi
        .searchAddresses(query)
        .then((suggs) => {
          setAddressSuggestions(suggs);
        })
        .finally(() => {
          setIsSearchingAddress(false);
        });
    }, 300);
  }

  function handleSelectSuggestion(sugg: AddressSuggestion) {
    const street = [sugg.streetNumber, sugg.streetName].filter(Boolean).join(' ') || sugg.streetName || null;
    const country =
      sugg.countryCode === 'US' ? 'USA' : sugg.countryCode === 'MX' ? 'MEX' : sugg.countryCode || 'USA';

    setMemberForm((prev) => ({
      ...prev,
      primaryLocation: sugg.address,
      primaryFullAddress: sugg.address,
      primaryStreet: street,
      primaryCity: sugg.city ?? null,
      primaryState: sugg.region ?? null,
      primaryZipCode: sugg.postalCode ?? null,
      primaryCountry: country,
      latitude: sugg.latitude != null ? Number(sugg.latitude) : null,
      longitude: sugg.longitude != null ? Number(sugg.longitude) : null,
    }));

    setAddressFilterQuery(sugg.address);
    setAddressSuggestions([]);
  }

  const handleCopyFamilyAddress = () => {
    if (!family) return;
    const fullAddress =
      family.primaryFullAddress ||
      [family.primaryStreet, family.primaryCity, family.primaryState, family.primaryZipCode, family.primaryCountry]
        .filter(Boolean)
        .join(', ') ||
      family.primaryCity ||
      '';

    setAddressFilterQuery(fullAddress);
    setMemberForm((prev) => ({
      ...prev,
      primaryLocation: fullAddress,
      primaryFullAddress: fullAddress,
      primaryStreet: family.primaryStreet || '',
      primaryCity: family.primaryCity || '',
      primaryState: family.primaryState || '',
      primaryZipCode: family.primaryZipCode || '',
      primaryCountry: family.primaryCountry || 'USA',
      latitude: family.latitude != null ? Number(family.latitude) : null,
      longitude: family.longitude != null ? Number(family.longitude) : null,
    }));
  };

  useEffect(() => {
    return () => {
      if (addressSearchTimerRef.current) {
        window.clearTimeout(addressSearchTimerRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (!memberId) return;

    let isMounted = true;
    setIsLoading(true);
    setError(null);

    familyApi
      .getMember(memberId)
      .then((data) => {
        if (!isMounted) return;
        setMember(data.member);
        const parsedNames = parseFullName(data.member.fullName || '');
        setFirstName(parsedNames.firstName);
        setLastName(parsedNames.lastName);
        setMotherLastName(parsedNames.motherLastName);

        const parsedPhone = parsePhoneNumber(data.member.phoneNumber, data.member.phoneCountryCode || '+1');
        setPhoneCountryCode(parsedPhone.countryCode);
        setPhoneNationalNumber(parsedPhone.nationalNumber);

        const fam = family;
        const initialStreet = data.member.primaryStreet || (fam && !data.member.primaryStreet ? fam.primaryStreet : '') || '';
        const initialCity = data.member.primaryCity || (fam && !data.member.primaryCity ? fam.primaryCity : '') || '';
        const initialState = data.member.primaryState || (fam && !data.member.primaryState ? fam.primaryState : '') || '';
        const initialZip = data.member.primaryZipCode || (fam && !data.member.primaryZipCode ? fam.primaryZipCode : '') || '';
        const initialCountry = data.member.primaryCountry || fam?.primaryCountry || 'USA';
        const initialFull =
          data.member.primaryFullAddress || data.member.primaryLocation || fam?.primaryFullAddress || '';
        const initialLat = data.member.latitude ?? (fam?.latitude != null ? Number(fam.latitude) : null);
        const initialLng = data.member.longitude ?? (fam?.longitude != null ? Number(fam.longitude) : null);

        setMemberForm({
          fullName: data.member.fullName,
          preferredName: data.member.preferredName,
          familyRole: data.member.familyRole,
          roleTitleEs: data.member.roleTitleEs,
          roleTitleEn: data.member.roleTitleEn,
          dateOfBirth: data.member.dateOfBirth,
          age: data.member.age,
          primaryLocation: initialFull,
          primaryStreet: initialStreet,
          primaryCity: initialCity,
          primaryState: initialState,
          primaryZipCode: initialZip,
          primaryCountry: initialCountry,
          primaryFullAddress: initialFull,
          latitude: initialLat,
          longitude: initialLng,
          email: data.member.email,
          phoneNumber: data.member.phoneNumber,
          phoneCountryCode: data.member.phoneCountryCode || parsedPhone.countryCode,
          weeklyHoursTarget: data.member.weeklyHoursTarget,
          languages: data.member.languages || [],
          topFocus: data.member.topFocus || [],
        });
        setAddressFilterQuery(initialFull);
        setEducations(data.educations || []);
      })
      .catch((err) => {
        if (!isMounted) return;
        console.error(err);
        setError(err.message || 'No se pudo cargar el perfil del miembro.');
      })
      .finally(() => {
        if (isMounted) setIsLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [memberId]);

  // Si los datos de la familia cargan y el integrante aún no tiene calle/ciudad fijada, sincronizar con la familia
  useEffect(() => {
    if (!family) return;
    setMemberForm((prev) => {
      if (prev.primaryStreet || prev.primaryCity) return prev;
      const full =
        prev.primaryFullAddress ||
        prev.primaryLocation ||
        family.primaryFullAddress ||
        '';
      return {
        ...prev,
        primaryLocation: full,
        primaryFullAddress: full,
        primaryStreet: family.primaryStreet || '',
        primaryCity: family.primaryCity || '',
        primaryState: family.primaryState || '',
        primaryZipCode: family.primaryZipCode || '',
        primaryCountry: family.primaryCountry || 'USA',
        latitude: family.latitude != null ? Number(family.latitude) : null,
        longitude: family.longitude != null ? Number(family.longitude) : null,
      };
    });
    setAddressFilterQuery((prev) => prev || family.primaryFullAddress || '');
  }, [family]);

  // Name field change handlers - keeps fullName in sync
  const handleFirstNameChange = (val: string) => {
    setFirstName(val);
    const combined = [val.trim(), lastName.trim(), motherLastName.trim()].filter(Boolean).join(' ');
    setMemberForm((prev) => ({ ...prev, fullName: combined }));
  };

  const handleLastNameChange = (val: string) => {
    setLastName(val);
    const combined = [firstName.trim(), val.trim(), motherLastName.trim()].filter(Boolean).join(' ');
    setMemberForm((prev) => ({ ...prev, fullName: combined }));
  };

  const handleMotherLastNameChange = (val: string) => {
    setMotherLastName(val);
    const combined = [firstName.trim(), lastName.trim(), val.trim()].filter(Boolean).join(' ');
    setMemberForm((prev) => ({ ...prev, fullName: combined }));
  };

  const handlePhoneCountryCodeChange = (code: string) => {
    setPhoneCountryCode(code);
    const full = phoneNationalNumber.trim() ? `${code} ${phoneNationalNumber.trim()}` : '';
    setMemberForm((prev) => ({
      ...prev,
      phoneCountryCode: code,
      phoneNumber: full || null,
    }));
  };

  const handlePhoneNationalNumberChange = (national: string) => {
    setPhoneNationalNumber(national);
    const full = national.trim() ? `${phoneCountryCode} ${national.trim()}` : '';
    setMemberForm((prev) => ({
      ...prev,
      phoneCountryCode,
      phoneNumber: full || null,
    }));
  };

  // Tab 1: Guardar datos del miembro
  const handleSaveMember = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!memberId) return;

    setIsSavingMember(true);
    setMemberSuccessMsg(null);
    setMemberErrorMsg(null);

    try {
      const combinedFullName = [firstName.trim(), lastName.trim(), motherLastName.trim()].filter(Boolean).join(' ');
      const combinedPhone = phoneNationalNumber.trim()
        ? `${phoneCountryCode} ${phoneNationalNumber.trim()}`
        : null;

      const formToSave = {
        ...memberForm,
        fullName: combinedFullName || memberForm.fullName || '',
        phoneNumber: combinedPhone,
        phoneCountryCode,
      };
      const updated = await familyApi.updateMember(memberId, formToSave);
      setMember(updated);
      setMemberSuccessMsg(
        isSpanish
          ? 'Datos del integrante actualizados correctamente en la base de datos SQL.'
          : 'Member profile updated successfully in SQL database.'
      );
      await refreshFamily();
      setTimeout(() => setMemberSuccessMsg(null), 4000);
    } catch (err: any) {
      console.error(err);
      setMemberErrorMsg(err.message || 'Error al actualizar los datos del miembro.');
    } finally {
      setIsSavingMember(false);
    }
  };

  /**
   * Links the currently signed-in MSAL account (from this app's own login,
   * see src/auth) to this family member: saves AzureOid + the real login
   * email (account.username), which may differ from the member's household
   * Email on file (e.g. a demo account reused across business use cases).
   * Backend enforces the 12+ age policy.
   */
  const handleLinkMsalAccount = async () => {
    if (!memberId || !account) return;
    const claims = account.idTokenClaims as Record<string, unknown> | undefined;
    const oid = (claims?.oid as string) || (claims?.sub as string) || account.homeAccountId.split('.')[0];

    setIsLinkingAccount(true);
    setLinkAccountMsg(null);
    try {
      await familyApi.linkMemberAccount(memberId, {
        azureOid: oid,
        azureLoginEmail: account.username,
      });
      const refreshed = await familyApi.getMember(memberId);
      setMember(refreshed.member);
      setLinkAccountMsg(
        isSpanish
          ? `✅ Cuenta Microsoft (${account.username}) vinculada a este integrante.`
          : `✅ Microsoft account (${account.username}) linked to this member.`
      );
      await refreshFamily();
    } catch (err: any) {
      console.error(err);
      setLinkAccountMsg(err.message || (isSpanish ? 'No se pudo vincular la cuenta.' : 'Could not link the account.'));
    } finally {
      setIsLinkingAccount(false);
    }
  };

  // Tab 1: Idiomas
  const handleAddLanguageValue = (langVal: string) => {
    const trimmed = langVal.trim();
    if (!trimmed) return;
    const current = memberForm.languages || [];
    if (!current.some((l) => l.toLowerCase() === trimmed.toLowerCase())) {
      setMemberForm({ ...memberForm, languages: [...current, trimmed] });
    }
  };

  const handleAddLanguage = () => {
    if (!newLanguageInput.trim()) return;
    handleAddLanguageValue(newLanguageInput);
    setNewLanguageInput('');
  };

  const handleRemoveLanguage = (indexToRemove: number) => {
    const current = memberForm.languages || [];
    setMemberForm({ ...memberForm, languages: current.filter((_, i) => i !== indexToRemove) });
  };

  // Tab 1: Enfoques
  const handleAddFocus = () => {
    if (!newFocusInput.trim()) return;
    const current = memberForm.topFocus || [];
    if (!current.includes(newFocusInput.trim())) {
      setMemberForm({ ...memberForm, topFocus: [...current, newFocusInput.trim()] });
    }
    setNewFocusInput('');
  };

  const handleRemoveFocus = (indexToRemove: number) => {
    const current = memberForm.topFocus || [];
    setMemberForm({ ...memberForm, topFocus: current.filter((_, i) => i !== indexToRemove) });
  };

  // Tab 2: Guardar escuela / nivel
  const handleSaveEducation = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!memberId) return;

    if (!eduForm.schoolName.trim()) {
      setEduErrorMsg(isSpanish ? 'El nombre de la escuela es obligatorio.' : 'School name is required.');
      return;
    }

    setIsSavingEdu(true);
    setEduSuccessMsg(null);
    setEduErrorMsg(null);

    const payload: Partial<FamilyMemberEducationRecord> = {
      educationLevel: eduForm.educationLevel,
      schoolName: eduForm.schoolName.trim(),
      gradeOrDegree: eduForm.gradeOrDegree.trim() || null,
      fieldOfStudy: eduForm.fieldOfStudy.trim() || null,
      degree: eduForm.degree.trim() || null,
      gpa: eduForm.gpa.trim() || null,
      startYear: eduForm.startYear ? parseInt(eduForm.startYear, 10) : null,
      endYear: eduForm.endYear ? parseInt(eduForm.endYear, 10) : null,
      isCurrent: eduForm.isCurrent,
      status: eduForm.isCurrent ? 'Current' : 'Completed',
      country: eduForm.country.trim() || null,
      state: eduForm.state.trim() || null,
      city: eduForm.city.trim() || null,
      notes: eduForm.notes.trim() || null,
    };

    try {
      if (selectedEduId) {
        // Update existing
        const updated = await familyApi.updateMemberEducation(memberId, selectedEduId, payload);
        setEducations((prev) =>
          prev.map((item) => (item.educationId === selectedEduId ? updated : payload.isCurrent ? { ...item, isCurrent: false } : item))
        );
        setEduSuccessMsg(
          isSpanish ? 'Registro educativo actualizado en SQL correctamente.' : 'Education record updated in SQL successfully.'
        );
      } else {
        // Create new
        const created = await familyApi.createMemberEducation(memberId, payload);
        setEducations((prev) => [created, ...(payload.isCurrent ? prev.map((item) => ({ ...item, isCurrent: false })) : prev)]);
        setEduSuccessMsg(
          isSpanish ? 'Nuevo nivel educativo registrado en la base de datos SQL.' : 'New education record saved to SQL.'
        );
      }

      // Reset form
      setIsEditingEdu(false);
      setSelectedEduId(null);
      setEduForm({
        educationLevel: 'Secundaria',
        schoolName: '',
        gradeOrDegree: '',
        fieldOfStudy: '',
        degree: '',
        gpa: '',
        startYear: '',
        endYear: '',
        isCurrent: true,
        status: 'Current',
        country: 'México',
        state: '',
        city: '',
        notes: '',
      });

      await refreshFamily();
      setTimeout(() => setEduSuccessMsg(null), 4000);
    } catch (err: any) {
      console.error(err);
      setEduErrorMsg(err.message || 'Error al guardar la información educativa.');
    } finally {
      setIsSavingEdu(false);
    }
  };

  // Tab 2: Abrir formulario para editar
  const handleStartEditEdu = (edu: FamilyMemberEducationRecord) => {
    setSelectedEduId(edu.educationId);
    setEduForm({
      educationLevel: edu.educationLevel,
      schoolName: edu.schoolName,
      gradeOrDegree: edu.gradeOrDegree || '',
      fieldOfStudy: edu.fieldOfStudy || '',
      degree: edu.degree || '',
      gpa: edu.gpa || '',
      startYear: edu.startYear ? edu.startYear.toString() : '',
      endYear: edu.endYear ? edu.endYear.toString() : '',
      isCurrent: edu.isCurrent,
      status: edu.status || (edu.isCurrent ? 'Current' : 'Completed'),
      country: edu.country || 'México',
      state: edu.state || '',
      city: edu.city || '',
      notes: edu.notes || '',
    });
    setIsEditingEdu(true);
    setEduErrorMsg(null);
  };

  // Tab 2: Eliminar escuela
  const handleDeleteEdu = async (educationId: string) => {
    if (!memberId) return;
    if (
      !window.confirm(
        isSpanish
          ? '¿Estás seguro de eliminar este registro educativo?'
          : 'Are you sure you want to delete this education record?'
      )
    ) {
      return;
    }

    try {
      await familyApi.deleteMemberEducation(memberId, educationId);
      setEducations((prev) => prev.filter((e) => e.educationId !== educationId));
      setEduSuccessMsg(isSpanish ? 'Registro eliminado correctamente.' : 'Record deleted successfully.');
      await refreshFamily();
      setTimeout(() => setEduSuccessMsg(null), 3000);
    } catch (err: any) {
      console.error(err);
      setEduErrorMsg(err.message || 'Error al eliminar el registro educativo.');
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center py-24 text-warm-600 dark:text-warm-300">
        <Loader2 className="h-8 w-8 animate-spin text-indigo-600 mb-3" />
        <p className="text-sm font-semibold">{isSpanish ? 'Cargando datos del integrante desde SQL...' : 'Loading member data from SQL...'}</p>
      </div>
    );
  }

  if (error || !member) {
    return (
      <div className="space-y-4 max-w-xl mx-auto py-12">
        <div className="rounded-2xl border border-rose-200 bg-rose-50 p-6 text-rose-900 dark:border-rose-800 dark:bg-rose-950/30 dark:text-rose-200">
          <AlertCircle className="h-6 w-6 mb-2 text-rose-600" />
          <h2 className="text-base font-bold">{isSpanish ? 'Error al cargar integrante' : 'Error loading member'}</h2>
          <p className="text-xs mt-1">{error || (isSpanish ? 'No se encontró el integrante solicitado.' : 'Member not found.')}</p>
        </div>
        <Link
          to="/family"
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-indigo-600 hover:underline"
        >
          <ArrowLeft className="h-4 w-4" />
          {isSpanish ? 'Volver a Integrantes' : 'Back to Members'}
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-8 animate-fade-in pb-16 max-w-5xl mx-auto">
      {/* Top Breadcrumb & Header */}
      <div>
        <Link
          to="/family"
          className="inline-flex items-center gap-1.5 text-xs font-semibold text-indigo-600 hover:text-indigo-700 dark:text-indigo-400 mb-3 transition"
        >
          <ArrowLeft className="h-4 w-4" />
          <span>{isSpanish ? 'Volver a Integrantes de la Familia' : 'Back to Family Members'}</span>
        </Link>

        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
          <div className="flex items-center gap-4">
            <div
              className={`flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br ${member.avatarBg} text-2xl font-black text-white shadow-md`}
            >
              {(member.preferredName || member.fullName).charAt(0).toUpperCase()}
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-2xl font-black text-warm-900 dark:text-white">
                  {member.fullName}
                </h1>
                <span className="rounded-full bg-emerald-50 px-2.5 py-0.5 text-[11px] font-bold text-emerald-700 dark:bg-emerald-950/60 dark:text-emerald-300">
                  {isSpanish ? 'Activo' : 'Active'}
                </span>
              </div>
              <p className="text-xs font-semibold text-indigo-600 dark:text-indigo-400 mt-0.5">
                {isSpanish ? member.roleTitleEs : member.roleTitleEn} · {member.age ?? 0} {isSpanish ? 'años' : 'years old'}
              </p>
              <p className="text-[11px] text-warm-500 dark:text-warm-400 mt-0.5">
                📍 {member.primaryLocation || (isSpanish ? 'Sin ubicación registrada' : 'No location specified')} · ✉️ {member.email || (isSpanish ? 'Sin correo' : 'No email')}
              </p>
            </div>
          </div>

          {/* MSAL account link status / action (age-based policy: 12+ own login) */}
          <div className="flex flex-col items-start sm:items-end gap-1.5">
            {requiresOwnMsalAccount(member.age) ? (
              member.azureOid ? (
                <div className="flex flex-col items-start sm:items-end gap-0.5">
                  <span className="inline-flex items-center gap-1.5 rounded-full bg-indigo-50 px-3 py-1.5 text-xs font-bold text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
                    <ShieldCheck className="h-3.5 w-3.5" />
                    {isSpanish ? 'Cuenta vinculada' : 'Account linked'}: {member.azureLoginEmail}
                  </span>
                  <span className="text-[10px] font-mono text-warm-400 dark:text-warm-500">
                    OID: {member.azureOid}
                  </span>
                </div>
              ) : account ? (
                <button
                  type="button"
                  onClick={() => void handleLinkMsalAccount()}
                  disabled={isLinkingAccount}
                  className="inline-flex items-center gap-1.5 rounded-full bg-gradient-to-r from-indigo-600 to-violet-600 px-3 py-1.5 text-xs font-bold text-white shadow-sm transition hover:opacity-95 disabled:opacity-60"
                >
                  <ShieldCheck className="h-3.5 w-3.5" />
                  {isLinkingAccount
                    ? (isSpanish ? 'Vinculando…' : 'Linking…')
                    : (isSpanish ? `Vincular mi cuenta (${account.username})` : `Link my account (${account.username})`)}
                </button>
              ) : null
            ) : (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-warm-100 px-3 py-1.5 text-xs font-semibold text-warm-600 dark:bg-white/5 dark:text-warm-300">
                👨‍👩‍👧 {isSpanish ? 'Gestionado por el administrador(a)' : 'Managed by admin'}
              </span>
            )}
            {linkAccountMsg && (
              <p className="max-w-xs text-right text-[11px] font-medium text-warm-500 dark:text-warm-400">{linkAccountMsg}</p>
            )}
            <button
              type="button"
              onClick={() => void handleDeleteMember()}
              disabled={isDeletingMember}
              className="mt-1 inline-flex items-center gap-1.5 rounded-full bg-rose-50 px-3 py-1.5 text-xs font-bold text-rose-700 hover:bg-rose-100 dark:bg-rose-950/40 dark:text-rose-300 dark:hover:bg-rose-900/40 transition disabled:opacity-60"
            >
              <Trash2 className="h-3.5 w-3.5" />
              {isDeletingMember
                ? (isSpanish ? 'Eliminando…' : 'Deleting…')
                : (isSpanish ? 'Eliminar integrante' : 'Delete member')}
            </button>
            {deleteMemberError && (
              <p className="max-w-xs text-right text-[11px] font-medium text-rose-600 dark:text-rose-400">{deleteMemberError}</p>
            )}
          </div>
        </div>
      </div>

      {/* Tabs Selector (EXACTLY TWO TABS) */}
      <div className="flex border-b border-warm-200 dark:border-white/10">
        <button
          type="button"
          onClick={() => setActiveTab('profile')}
          className={`flex items-center gap-2 border-b-2 px-6 py-3.5 text-sm font-bold transition ${
            activeTab === 'profile'
              ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-300'
              : 'border-transparent text-warm-500 hover:text-warm-900 dark:text-warm-400 dark:hover:text-white'
          }`}
        >
          <User className="h-4 w-4" />
          <span>{isSpanish ? '1) Datos del Integrante' : '1) Member Profile Data'}</span>
        </button>

        <button
          type="button"
          onClick={() => setActiveTab('education')}
          className={`flex items-center gap-2 border-b-2 px-6 py-3.5 text-sm font-bold transition ${
            activeTab === 'education'
              ? 'border-indigo-600 text-indigo-600 dark:border-indigo-400 dark:text-indigo-300'
              : 'border-transparent text-warm-500 hover:text-warm-900 dark:text-warm-400 dark:hover:text-white'
          }`}
        >
          <GraduationCap className="h-4 w-4" />
          <span>{isSpanish ? '2) Situación Actual Educativa' : '2) Current Education & Schools'}</span>
          {educations.length > 0 && (
            <span className="rounded-full bg-indigo-100 px-2 py-0.5 text-[10px] font-bold text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
              {educations.length}
            </span>
          )}
        </button>
      </div>

      {/* TAB 1: DATOS DEL MIEMBRO */}
      {activeTab === 'profile' && (
        <form onSubmit={handleSaveMember} className="space-y-6">
          {memberSuccessMsg && (
            <div className="flex items-center gap-2 rounded-2xl border border-emerald-200 bg-emerald-50/80 p-4 text-xs font-semibold text-emerald-800 dark:border-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300">
              <CheckCircle2 className="h-4 w-4 text-emerald-600 shrink-0" />
              <span>{memberSuccessMsg}</span>
            </div>
          )}

          {memberErrorMsg && (
            <div className="flex items-center gap-2 rounded-2xl border border-rose-200 bg-rose-50/80 p-4 text-xs font-semibold text-rose-800 dark:border-rose-800 dark:bg-rose-950/40 dark:text-rose-300">
              <AlertCircle className="h-4 w-4 text-rose-600 shrink-0" />
              <span>{memberErrorMsg}</span>
            </div>
          )}

          <div className="rounded-3xl border border-warm-200/80 bg-white p-6 shadow-xs dark:border-white/10 dark:bg-[#0f172a] space-y-5">
            <h3 className="text-base font-bold text-warm-900 dark:text-white border-b border-warm-100 pb-3 dark:border-white/10">
              {isSpanish ? 'Información General del Integrante' : 'General Member Information'}
            </h3>

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {isSpanish ? (
                <>
                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      Nombre(s) *
                    </label>
                    <input
                      type="text"
                      required
                      value={firstName}
                      onChange={(e) => handleFirstNameChange(e.target.value)}
                      placeholder="ej. Jorge"
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      Primer Apellido (Last Name) *
                    </label>
                    <input
                      type="text"
                      required
                      value={lastName}
                      onChange={(e) => handleLastNameChange(e.target.value)}
                      placeholder="ej. Luna"
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      Segundo Apellido / Materno (Mother's Name)
                    </label>
                    <input
                      type="text"
                      value={motherLastName}
                      onChange={(e) => handleMotherLastNameChange(e.target.value)}
                      placeholder="ej. Pérez"
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  </div>
                </>
              ) : (
                <>
                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      First Name (Name) *
                    </label>
                    <input
                      type="text"
                      required
                      value={firstName}
                      onChange={(e) => handleFirstNameChange(e.target.value)}
                      placeholder="e.g. Jorge"
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      Last Name *
                    </label>
                    <input
                      type="text"
                      required
                      value={lastName}
                      onChange={(e) => handleLastNameChange(e.target.value)}
                      placeholder="e.g. Luna"
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  </div>
                </>
              )}

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Nombre Preferido / Apodo' : 'Preferred Name / Nickname'}
                </label>
                <input
                  type="text"
                  value={memberForm.preferredName || ''}
                  onChange={(e) => setMemberForm({ ...memberForm, preferredName: e.target.value })}
                  placeholder={isSpanish ? 'ej. Jorge' : 'e.g. Jorge'}
                  className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Rol en la Familia' : 'Family Role'}
                </label>
                <select
                  value={memberForm.familyRole || 'father'}
                  onChange={(e) => setMemberForm({ ...memberForm, familyRole: e.target.value })}
                  className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                >
                  {ROLE_OPTIONS.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {isSpanish ? opt.labelEs : opt.labelEn}
                    </option>
                  ))}
                </select>
              </div>

              {/* Profesión / Título de Rol - Combo box con 34 opciones en inglés */}
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Profesión' : 'Profession'}
                </label>
                <select
                  value={
                    PROFESSION_OPTIONS.find((p) => p === memberForm.roleTitleEn) ||
                    PROFESSION_OPTIONS.find((p) => p === memberForm.roleTitleEs) ||
                    memberForm.roleTitleEn ||
                    memberForm.roleTitleEs ||
                    ''
                  }
                  onChange={(e) => {
                    const val = e.target.value;
                    setMemberForm({
                      ...memberForm,
                      roleTitleEs: val,
                      roleTitleEn: val,
                    });
                  }}
                  className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                >
                  <option value="">
                    {isSpanish
                      ? '-- Selecciona una profesión (34 opciones en inglés) --'
                      : '-- Select a profession (34 options in English) --'}
                  </option>
                  {PROFESSION_OPTIONS.map((prof) => (
                    <option key={prof} value={prof}>
                      {prof}
                    </option>
                  ))}
                  {memberForm.roleTitleEn && !PROFESSION_OPTIONS.includes(memberForm.roleTitleEn as any) && (
                    <option value={memberForm.roleTitleEn}>
                      {memberForm.roleTitleEn}
                    </option>
                  )}
                  {memberForm.roleTitleEs && memberForm.roleTitleEs !== memberForm.roleTitleEn && !PROFESSION_OPTIONS.includes(memberForm.roleTitleEs as any) && (
                    <option value={memberForm.roleTitleEs}>
                      {memberForm.roleTitleEs}
                    </option>
                  )}
                </select>
              </div>

              {/* Fecha de Nacimiento con calendario */}
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  <span className="flex items-center gap-1.5">
                    <Calendar className="h-3.5 w-3.5 text-indigo-600" />
                    <span>{isSpanish ? 'Fecha de Nacimiento' : 'Date of Birth'}</span>
                  </span>
                </label>
                <input
                  type="date"
                  value={
                    memberForm.dateOfBirth
                      ? memberForm.dateOfBirth.split('T')[0]
                      : ''
                  }
                  onChange={(e) => {
                    const dob = e.target.value || null;
                    const newAge = dob ? calculateAgeFromDob(dob) : memberForm.age;
                    setMemberForm({
                      ...memberForm,
                      dateOfBirth: dob,
                      age: newAge ?? memberForm.age,
                    });
                  }}
                  className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Edad' : 'Age'}
                </label>
                <input
                  type="number"
                  min="0"
                  max="120"
                  value={memberForm.age ?? ''}
                  onChange={(e) =>
                    setMemberForm({ ...memberForm, age: e.target.value ? parseInt(e.target.value, 10) : null })
                  }
                  className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Correo Electrónico' : 'Email Address'}
                </label>
                <input
                  type="email"
                  value={memberForm.email || ''}
                  onChange={(e) => setMemberForm({ ...memberForm, email: e.target.value })}
                  className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
              </div>

              {/* Teléfono móvil con selector de código de país (USA +1, México +52, etc.) */}
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
                    onChange={(e) => handlePhoneCountryCodeChange(e.target.value)}
                    className="w-40 rounded-xl border border-warm-200 bg-white px-2.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white shrink-0"
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
                    onChange={(e) => handlePhoneNationalNumberChange(e.target.value)}
                    placeholder={phoneCountryCode === '+52' ? 'ej. 55 1234 5678' : 'e.g. (512) 555-0199'}
                    className="flex-1 rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Meta de Horas Semanales de Aprendizaje' : 'Weekly Study Hours Target'}
                </label>
                <input
                  type="number"
                  step="0.5"
                  min="0"
                  max="80"
                  value={memberForm.weeklyHoursTarget ?? 8}
                  onChange={(e) =>
                    setMemberForm({ ...memberForm, weeklyHoursTarget: parseFloat(e.target.value) || 0 })
                  }
                  className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2.5 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
              </div>
            </div>

            {/* UBICACIÓN PRINCIPAL: BING MAPS AUTOCOMPLETE & LOCKED FIELDS */}
            <div className="rounded-2xl border border-indigo-100 bg-indigo-50/40 p-5 dark:border-indigo-500/20 dark:bg-slate-900/60">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2 text-indigo-700 dark:text-indigo-400">
                  <MapPin className="h-5 w-5" />
                  <h3 className="text-sm font-extrabold uppercase tracking-wider">
                    {isSpanish ? 'Ubicación principal (Bing Maps)' : 'Primary location (Bing Maps)'}
                  </h3>
                </div>
                {family && (
                  <button
                    type="button"
                    onClick={handleCopyFamilyAddress}
                    className="inline-flex items-center gap-1.5 rounded-xl border border-indigo-300 bg-white px-3 py-1.5 text-xs font-bold text-indigo-700 shadow-xs hover:bg-indigo-50 hover:border-indigo-400 dark:border-indigo-500/40 dark:bg-slate-800 dark:text-indigo-300 dark:hover:bg-indigo-950/50 transition cursor-pointer"
                    title={
                      isSpanish
                        ? 'Copiar la dirección del hogar familiar registrada en el sistema'
                        : 'Copy household address from family master data'
                    }
                  >
                    <Home className="h-3.5 w-3.5 text-indigo-600 dark:text-indigo-400" />
                    <span>{isSpanish ? 'Copiar dirección de la familia' : 'Copy family household address'}</span>
                  </button>
                )}
              </div>
              <p className="mt-1 text-xs text-warm-600 dark:text-warm-400">
                {isSpanish
                  ? 'Usa el botón para heredar la dirección del hogar familiar o el buscador para filtrar mediante Bing Maps. Los datos resultantes quedan fijados y protegidos.'
                  : 'Use the button to inherit family household address or search via Bing Maps. The resulting values are securely locked.'}
              </p>

              {/* Bing Maps Filter / Adivina dirección Input */}
              <div className="relative mt-4">
                <label className="block text-xs font-bold uppercase tracking-wider text-indigo-900 dark:text-indigo-300">
                  {isSpanish ? 'Filtrar o adivinar dirección con Bing Maps' : 'Filter or predict address with Bing Maps'}
                </label>
                <div className="relative mt-1">
                  <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-indigo-500" />
                  <input
                    type="text"
                    value={addressFilterQuery}
                    onChange={(e) => handleAddressFilterChange(e.target.value)}
                    placeholder={
                      isSpanish
                        ? 'Escribe dirección, calle o ciudad (ej. 100 Congress Ave Austin, o Reforma 222 CDMX)...'
                        : 'Type address, street or city to search via Bing Maps...'
                    }
                    className="w-full rounded-xl border border-indigo-300 bg-white pl-10 pr-10 py-2.5 text-sm font-medium text-warm-900 shadow-sm focus:border-indigo-600 focus:ring-1 focus:ring-indigo-600 focus:outline-none dark:border-indigo-500/40 dark:bg-slate-900 dark:text-white"
                  />
                  {isSearchingAddress && (
                    <Loader2 className="absolute right-3.5 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-indigo-600 dark:text-indigo-400" />
                  )}
                </div>

                {/* Suggestions Dropdown */}
                {addressSuggestions.length > 0 && (
                  <div className="absolute z-20 mt-1 max-h-60 w-full overflow-auto rounded-xl border border-indigo-200 bg-white py-1 shadow-xl dark:border-indigo-500/30 dark:bg-slate-900">
                    <div className="px-3 py-1 text-[11px] font-bold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
                      {isSpanish ? 'Sugerencias de Bing Maps (clic para seleccionar)' : 'Bing Maps suggestions (click to select)'}
                    </div>
                    {addressSuggestions.map((sugg, idx) => (
                      <button
                        key={sugg.entityId || idx}
                        type="button"
                        onClick={() => handleSelectSuggestion(sugg)}
                        className="flex w-full flex-col items-start px-3.5 py-2 text-left hover:bg-indigo-50 transition dark:hover:bg-indigo-950/50"
                      >
                        <span className="text-sm font-semibold text-warm-900 dark:text-white">{sugg.address}</span>
                        <div className="mt-0.5 flex flex-wrap gap-1 text-[11px] text-warm-500 dark:text-warm-400">
                          {sugg.city && <span className="font-medium text-indigo-600 dark:text-indigo-400">{sugg.city}</span>}
                          {sugg.region && <span>· {sugg.region}</span>}
                          {sugg.countryCode && <span>· {sugg.countryCode}</span>}
                          {sugg.latitude != null && sugg.longitude != null && (
                            <span className="font-mono text-[10px] text-emerald-600 dark:text-emerald-400">
                              · ({sugg.latitude.toFixed(4)}, {sugg.longitude.toFixed(4)})
                            </span>
                          )}
                        </div>
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* LOCKED READONLY FIELDS (No editables directamente) */}
              <div className="mt-5 rounded-xl border border-warm-200/90 bg-white/80 p-4 dark:border-white/10 dark:bg-slate-900/80">
                <div className="mb-3 flex items-center justify-between">
                  <div className="flex items-center gap-1.5 text-xs font-bold text-warm-700 dark:text-warm-300">
                    <Lock className="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" />
                    <span>
                      {isSpanish
                        ? 'Campos autocompletados por Bing Maps (no editables directamente):'
                        : 'Fields populated by Bing Maps (read-only):'}
                    </span>
                  </div>
                  <span className="rounded bg-warm-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-warm-600 dark:bg-white/10 dark:text-warm-300">
                    {isSpanish ? 'Solo lectura' : 'Locked'}
                  </span>
                </div>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                  {/* País */}
                  <div>
                    <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                      {isSpanish ? 'País' : 'Country'}
                    </label>
                    <input
                      type="text"
                      value={memberForm.primaryCountry || ''}
                      readOnly
                      disabled
                      className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                    />
                  </div>

                  {/* Estado */}
                  <div>
                    <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                      {isSpanish ? 'Estado / Región' : 'State / Region'}
                    </label>
                    <input
                      type="text"
                      value={memberForm.primaryState || ''}
                      readOnly
                      disabled
                      className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                    />
                  </div>

                  {/* Ciudad */}
                  <div>
                    <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                      {isSpanish ? 'Ciudad' : 'City'}
                    </label>
                    <input
                      type="text"
                      value={memberForm.primaryCity || ''}
                      readOnly
                      disabled
                      className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                    />
                  </div>

                  {/* Calle / Número */}
                  <div>
                    <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                      {isSpanish ? 'Calle / Número' : 'Street'}
                    </label>
                    <input
                      type="text"
                      value={memberForm.primaryStreet || ''}
                      readOnly
                      disabled
                      className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                    />
                  </div>

                  {/* Código Postal */}
                  <div>
                    <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                      {isSpanish ? 'Código postal' : 'Postal code'}
                    </label>
                    <input
                      type="text"
                      value={memberForm.primaryZipCode || ''}
                      readOnly
                      disabled
                      className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200"
                    />
                  </div>

                  {/* Coordenadas Lat/Long */}
                  <div>
                    <label className="block text-[11px] font-bold uppercase tracking-wider text-warm-500 dark:text-warm-400">
                      {isSpanish ? 'Coordenadas (Lat, Long)' : 'Coordinates (Lat, Long)'}
                    </label>
                    <input
                      type="text"
                      value={
                        memberForm.latitude != null && memberForm.longitude != null
                          ? `${memberForm.latitude.toFixed(4)}, ${memberForm.longitude.toFixed(4)}`
                          : ''
                      }
                      readOnly
                      disabled
                      className="mt-1 w-full cursor-not-allowed rounded-lg border border-warm-200 bg-warm-100/70 px-3 py-1.5 text-xs font-semibold text-warm-800 dark:border-white/10 dark:bg-slate-800/80 dark:text-warm-200 font-mono"
                    />
                  </div>
                </div>
              </div>
            </div>

            {/* Idiomas */}
            <div className="pt-4 border-t border-warm-100 dark:border-white/10">
              <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1.5">
                {isSpanish ? 'Idiomas de Dominio' : 'Languages Spoken'}
              </label>

              <div className="grid grid-cols-1 gap-2.5 sm:grid-cols-2">
                {/* Selector de lista de idiomas */}
                <div>
                  <label className="block text-[11px] font-semibold text-warm-500 dark:text-warm-400 mb-1">
                    {isSpanish ? 'Seleccionar de lista de idiomas:' : 'Select from language list:'}
                  </label>
                  <select
                    value=""
                    onChange={(e) => {
                      const val = e.target.value;
                      if (val) {
                        handleAddLanguageValue(val);
                      }
                    }}
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  >
                    <option value="">
                      {isSpanish
                        ? '-- Escoge un idioma de la lista --'
                        : '-- Choose a language from list --'}
                    </option>
                    {LANGUAGE_OPTIONS.map((langOpt) => {
                      const label = isSpanish ? langOpt.labelEs : langOpt.labelEn;
                      const isAlreadySelected = (memberForm.languages || []).some(
                        (l) => l.toLowerCase() === label.toLowerCase() || l.toLowerCase() === langOpt.value.toLowerCase()
                      );
                      return (
                        <option
                          key={langOpt.value}
                          value={label}
                          disabled={isAlreadySelected}
                        >
                          {langOpt.flag} {label} {isAlreadySelected ? `(${isSpanish ? 'Ya añadido' : 'Added'})` : ''}
                        </option>
                      );
                    })}
                  </select>
                </div>

                {/* Opción de añadir otro idioma manual */}
                <div>
                  <label className="block text-[11px] font-semibold text-warm-500 dark:text-warm-400 mb-1">
                    {isSpanish ? 'O añadir otro idioma personalizado:' : 'Or add custom / other language:'}
                  </label>
                  <div className="flex gap-2">
                    <input
                      type="text"
                      value={newLanguageInput}
                      onChange={(e) => setNewLanguageInput(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                          e.preventDefault();
                          handleAddLanguage();
                        }
                      }}
                      placeholder={isSpanish ? 'Escribe idioma y presiona "+"..."' : 'Type language and press "+"'}
                      className="flex-1 rounded-xl border border-warm-200 bg-white px-3 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                    <button
                      type="button"
                      onClick={handleAddLanguage}
                      className="rounded-xl bg-indigo-100 px-3 py-2 text-xs font-bold text-indigo-700 hover:bg-indigo-200 dark:bg-indigo-950/60 dark:text-indigo-300"
                    >
                      + {isSpanish ? 'Añadir' : 'Add'}
                    </button>
                  </div>
                </div>
              </div>

              {/* Etiquetas / Badges de idiomas seleccionados */}
              <div className="mt-3 flex flex-wrap gap-2">
                {(memberForm.languages || []).map((langItem, idx) => (
                  <span
                    key={idx}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-indigo-50 px-2.5 py-1 text-xs font-semibold text-indigo-700 dark:bg-indigo-950/50 dark:text-indigo-300"
                  >
                    🗣️ {langItem}
                    <button
                      type="button"
                      onClick={() => handleRemoveLanguage(idx)}
                      className="ml-1 text-indigo-400 hover:text-indigo-700 font-bold"
                    >
                      ×
                    </button>
                  </span>
                ))}
              </div>
            </div>

            {/* Enfoques y Fortalezas */}
            <div className="pt-4 border-t border-warm-100 dark:border-white/10">
              <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1.5">
                {isSpanish ? 'Enfoques Clave & Fortalezas' : 'Key Focus Areas & Strengths'}
              </label>
              <div className="flex gap-2">
                <input
                  type="text"
                  value={newFocusInput}
                  onChange={(e) => setNewFocusInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') {
                      e.preventDefault();
                      handleAddFocus();
                    }
                  }}
                  placeholder={isSpanish ? 'Escribe un enfoque y presiona "+"...' : 'Add a focus area and click "+"'}
                  className="flex-1 rounded-xl border border-warm-200 bg-white px-3 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
                <button
                  type="button"
                  onClick={handleAddFocus}
                  className="rounded-xl bg-warm-200 px-3 py-2 text-xs font-bold text-warm-800 hover:bg-warm-300 dark:bg-white/10 dark:text-warm-200"
                >
                  + {isSpanish ? 'Añadir' : 'Add'}
                </button>
              </div>

              <div className="mt-2.5 flex flex-wrap gap-2">
                {(memberForm.topFocus || []).map((focusItem, idx) => (
                  <span
                    key={idx}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-warm-100 px-2.5 py-1 text-xs font-semibold text-warm-800 dark:bg-white/10 dark:text-warm-200"
                  >
                    ✨ {focusItem}
                    <button
                      type="button"
                      onClick={() => handleRemoveFocus(idx)}
                      className="ml-1 text-warm-400 hover:text-warm-700 font-bold"
                    >
                      ×
                    </button>
                  </span>
                ))}
              </div>
            </div>

            {/* Save Button */}
            <div className="pt-4 border-t border-warm-100 dark:border-white/10 flex justify-end">
              <button
                type="submit"
                disabled={isSavingMember}
                className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 px-6 py-3 text-xs font-bold text-white shadow-md shadow-indigo-600/30 transition hover:from-indigo-700 hover:to-violet-700 disabled:opacity-50"
              >
                {isSavingMember ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                <span>{isSpanish ? 'Guardar Datos en Base de Datos' : 'Save Profile to Database'}</span>
              </button>
            </div>
          </div>
        </form>
      )}

      {/* TAB 2: SITUACIÓN ACTUAL EDUCATIVA */}
      {activeTab === 'education' && (
        <div className="space-y-6">
          {eduSuccessMsg && (
            <div className="flex items-center gap-2 rounded-2xl border border-emerald-200 bg-emerald-50/80 p-4 text-xs font-semibold text-emerald-800 dark:border-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-300">
              <CheckCircle2 className="h-4 w-4 text-emerald-600 shrink-0" />
              <span>{eduSuccessMsg}</span>
            </div>
          )}

          {eduErrorMsg && (
            <div className="flex items-center gap-2 rounded-2xl border border-rose-200 bg-rose-50/80 p-4 text-xs font-semibold text-rose-800 dark:border-rose-800 dark:bg-rose-950/40 dark:text-rose-300">
              <AlertCircle className="h-4 w-4 text-rose-600 shrink-0" />
              <span>{eduErrorMsg}</span>
            </div>
          )}

          {/* Header of Tab 2 */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h3 className="text-base font-bold text-warm-900 dark:text-white">
                {isSpanish ? 'Historial & Situación Actual Educativa' : 'Education History & Current Schooling'}
              </h3>
              <p className="text-xs text-warm-500 dark:text-warm-400 mt-0.5">
                {isSpanish
                  ? 'Registros persistidos en la tabla SQL dbo.FamilyMemberEducation para cada etapa (Primaria, Secundaria, Preparatoria, Universidad).'
                  : 'Persisted in SQL table dbo.FamilyMemberEducation for each stage (Elementary, Middle, High School, University).'}
              </p>
            </div>

            {!isEditingEdu && (
              <button
                type="button"
                onClick={() => {
                  setSelectedEduId(null);
                  setEduForm({
                    educationLevel: 'Secundaria',
                    schoolName: '',
                    gradeOrDegree: '',
                    fieldOfStudy: '',
                    degree: '',
                    gpa: '',
                    startYear: '',
                    endYear: '',
                    isCurrent: true,
                    status: 'Current',
                    country: 'México',
                    state: '',
                    city: '',
                    notes: '',
                  });
                  setIsEditingEdu(true);
                  setEduErrorMsg(null);
                }}
                className="inline-flex items-center gap-2 rounded-2xl bg-gradient-to-r from-indigo-600 to-violet-600 px-4 py-2.5 text-xs font-bold text-white shadow-md shadow-indigo-600/30 transition hover:from-indigo-700 hover:to-violet-700"
              >
                <Plus className="h-4 w-4" />
                <span>{isSpanish ? '+ Registrar Escuela / Nivel' : '+ Add School / Level'}</span>
              </button>
            )}
          </div>

          {/* FORMULARIO DE AÑADIR / EDITAR ESCUELA */}
          {isEditingEdu && (
            <form onSubmit={handleSaveEducation} className="rounded-3xl border-2 border-indigo-500/40 bg-indigo-50/20 p-6 shadow-sm dark:border-indigo-500/30 dark:bg-slate-900/60 space-y-4">
              <div className="flex items-center justify-between border-b border-indigo-200 pb-3 dark:border-indigo-500/20">
                <h4 className="text-sm font-bold text-indigo-950 dark:text-indigo-200">
                  {selectedEduId
                    ? (isSpanish ? 'Editar Registro Educativo' : 'Edit Education Record')
                    : (isSpanish ? 'Nuevo Registro Educativo' : 'New Education Record')}
                </h4>
                <button
                  type="button"
                  onClick={() => {
                    setIsEditingEdu(false);
                    setSelectedEduId(null);
                  }}
                  className="text-xs font-semibold text-warm-500 hover:text-warm-800 dark:hover:text-white"
                >
                  {isSpanish ? 'Cancelar' : 'Cancel'}
                </button>
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                {/* 1. PAÍS PRIMERO (México, USA, Argentina, Colombia, Chile...) */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'País' : 'Country'} *
                  </label>
                  <select
                    value={eduForm.country}
                    onChange={(e) => {
                      const newCountry = e.target.value;
                      setEduForm({
                        ...eduForm,
                        country: newCountry,
                        state: COUNTRY_STATES[newCountry] ? '' : eduForm.state,
                      });
                    }}
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  >
                    {EDUCATION_COUNTRIES.map((c) => (
                      <option key={c.code} value={c.code}>
                        {c.label}
                      </option>
                    ))}
                  </select>
                </div>

                {/* 2. ESTADO / PROVINCIA ASOCIADO AL PAÍS */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'Estado / Provincia' : 'State / Province'} *
                  </label>
                  {COUNTRY_STATES[eduForm.country] ? (
                    <select
                      value={eduForm.state}
                      onChange={(e) => setEduForm({ ...eduForm, state: e.target.value })}
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    >
                      <option value="">
                        {isSpanish ? '-- Selecciona un Estado --' : '-- Select a State --'}
                      </option>
                      {COUNTRY_STATES[eduForm.country].map((st) => (
                        <option key={st} value={st}>
                          {st}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <input
                      type="text"
                      value={eduForm.state}
                      onChange={(e) => setEduForm({ ...eduForm, state: e.target.value })}
                      placeholder={isSpanish ? 'ej. Estado, Provincia o Región' : 'e.g. State or Province'}
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  )}
                </div>

                {/* 3. CIUDAD */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'Ciudad' : 'City'}
                  </label>
                  <input
                    type="text"
                    value={eduForm.city}
                    onChange={(e) => setEduForm({ ...eduForm, city: e.target.value })}
                    placeholder={
                      eduForm.country === 'USA'
                        ? 'e.g. Austin, Houston, Seattle'
                        : eduForm.country === 'México'
                        ? 'ej. Monterrey, CDMX, Guadalajara'
                        : 'ej. Ciudad / Localidad'
                    }
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                {/* 4. COMBOBOX SI CURRENT O TERMINADO */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'Estatus Académico' : 'Academic Status'} *
                  </label>
                  <select
                    value={eduForm.isCurrent ? 'current' : 'completed'}
                    onChange={(e) => {
                      const isCurr = e.target.value === 'current';
                      setEduForm({
                        ...eduForm,
                        isCurrent: isCurr,
                        status: isCurr ? 'Current' : 'Completed',
                      });
                    }}
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  >
                    <option value="current">
                      🟢 {isSpanish ? 'Cursando actualmente (In Progress)' : 'Currently Enrolled (In Progress)'}
                    </option>
                    <option value="completed">
                      🎓 {isSpanish ? 'Terminado / Graduado (Completed)' : 'Completed / Graduated'}
                    </option>
                  </select>
                </div>

                {/* 5. NIVEL EDUCATIVO */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'Nivel Educativo General' : 'Education Stage Level'} *
                  </label>
                  <select
                    value={eduForm.educationLevel}
                    onChange={(e) => setEduForm({ ...eduForm, educationLevel: e.target.value })}
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  >
                    {EDUCATION_LEVEL_OPTIONS.map((opt) => (
                      <option key={opt.value} value={opt.value}>
                        {isSpanish ? opt.labelEs : opt.labelEn}
                      </option>
                    ))}
                  </select>
                </div>

                {/* 6. NOMBRE DE LA ESCUELA / INSTITUCIÓN */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'Nombre de la Escuela / Institución' : 'School / Institution Name'} *
                  </label>
                  <input
                    type="text"
                    required
                    value={eduForm.schoolName}
                    onChange={(e) => setEduForm({ ...eduForm, schoolName: e.target.value })}
                    placeholder={isSpanish ? 'ej. Westlake High School, Tec de Monterrey, UNAM' : 'e.g. Westlake High School'}
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  />
                </div>

                {/* 7. GRADE OR DEGREE COMBOBOX (LISTA LARGA: KINDER PRIMERO, PRIMARIA, SECUNDARIA...) */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'Grado o Nivel Cursado' : 'Grade or Degree Level'} *
                  </label>
                  <select
                    value={eduForm.gradeOrDegree}
                    onChange={(e) => setEduForm({ ...eduForm, gradeOrDegree: e.target.value })}
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  >
                    <option value="">
                      {isSpanish ? '-- Selecciona Grado o Nivel (Kinder, Primaria, etc.) --' : '-- Select Grade or Degree Level --'}
                    </option>
                    {GRADE_OR_DEGREE_GROUPS.map((grp) => (
                      <optgroup key={grp.group} label={grp.group}>
                        {grp.options.map((opt) => (
                          <option key={opt} value={opt}>
                            {opt}
                          </option>
                        ))}
                      </optgroup>
                    ))}
                    {eduForm.gradeOrDegree &&
                      !GRADE_OR_DEGREE_GROUPS.some((g) => (g.options as readonly string[]).includes(eduForm.gradeOrDegree)) && (
                        <option value={eduForm.gradeOrDegree}>{eduForm.gradeOrDegree}</option>
                      )}
                  </select>
                </div>

                {/* 8. PARA PROFESIONAL (COLLEGE): QUÉ DEGREE */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish
                      ? 'Título Profesional / Grado Universitario (College Degree)'
                      : 'Professional / College Degree'}
                  </label>
                  <input
                    type="text"
                    list="collegeDegreesList"
                    value={eduForm.degree}
                    onChange={(e) => setEduForm({ ...eduForm, degree: e.target.value })}
                    placeholder={
                      isSpanish
                        ? 'ej. B.S. Computer Science, Licenciatura en Derecho, M.D.'
                        : 'e.g. B.S. in Computer Science, B.A. Economics, M.D.'
                    }
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  />
                  <datalist id="collegeDegreesList">
                    {COLLEGE_DEGREE_SUGGESTIONS.map((deg) => (
                      <option key={deg} value={deg} />
                    ))}
                  </datalist>
                </div>

                {/* 9. GPA */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'Promedio Académico (GPA)' : 'Academic GPA'}
                  </label>
                  <input
                    type="text"
                    value={eduForm.gpa}
                    onChange={(e) => setEduForm({ ...eduForm, gpa: e.target.value })}
                    placeholder={
                      eduForm.country === 'USA'
                        ? 'e.g. 3.85 (scale 4.0)'
                        : 'ej. 3.85 / 4.0 o 9.5 / 10'
                    }
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  />
                </div>

                {/* 10. ÁREA DE ESTUDIO / ESPECIALIDAD */}
                <div>
                  <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                    {isSpanish ? 'Área de Estudio / Especialidad' : 'Field of Study / Major'}
                  </label>
                  <input
                    type="text"
                    value={eduForm.fieldOfStudy}
                    onChange={(e) => setEduForm({ ...eduForm, fieldOfStudy: e.target.value })}
                    placeholder={isSpanish ? 'ej. STEM, Ciencias Computacionales, Pre-Med' : 'e.g. STEM, Computer Science, Pre-Med'}
                    className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                  />
                </div>

                {/* 11. AÑO DE INICIO Y AÑO DE FIN */}
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      {isSpanish ? 'Año Inicio' : 'Start Year'}
                    </label>
                    <input
                      type="number"
                      min="1950"
                      max="2035"
                      value={eduForm.startYear}
                      onChange={(e) => setEduForm({ ...eduForm, startYear: e.target.value })}
                      placeholder="2020"
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                      {isSpanish ? 'Año Fin / Previsto' : 'End Year'}
                    </label>
                    <input
                      type="number"
                      min="1950"
                      max="2035"
                      value={eduForm.endYear}
                      onChange={(e) => setEduForm({ ...eduForm, endYear: e.target.value })}
                      placeholder="2024"
                      className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                    />
                  </div>
                </div>
              </div>

              {/* Notas */}
              <div>
                <label className="block text-xs font-bold text-warm-700 dark:text-warm-300 mb-1">
                  {isSpanish ? 'Notas u Observaciones' : 'Notes'}
                </label>
                <textarea
                  rows={2}
                  value={eduForm.notes}
                  onChange={(e) => setEduForm({ ...eduForm, notes: e.target.value })}
                  placeholder={isSpanish ? 'Detalles del programa, bachillerato bilingüe, actividades...' : 'Program details, extracurriculars...'}
                  className="w-full rounded-xl border border-warm-200 bg-white px-3.5 py-2 text-xs font-medium text-warm-900 focus:border-indigo-600 focus:outline-none dark:border-white/10 dark:bg-slate-900 dark:text-white"
                />
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => {
                    setIsEditingEdu(false);
                    setSelectedEduId(null);
                  }}
                  className="rounded-xl px-4 py-2 text-xs font-semibold text-warm-600 hover:bg-warm-100 dark:text-warm-300 dark:hover:bg-white/10"
                >
                  {isSpanish ? 'Cancelar' : 'Cancel'}
                </button>
                <button
                  type="submit"
                  disabled={isSavingEdu}
                  className="inline-flex items-center gap-2 rounded-xl bg-indigo-600 px-5 py-2 text-xs font-bold text-white shadow-sm hover:bg-indigo-700 disabled:opacity-50"
                >
                  {isSavingEdu ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
                  <span>{isSpanish ? 'Guardar en SQL' : 'Save to SQL'}</span>
                </button>
              </div>
            </form>
          )}

          {/* LISTADO DE ESCUELAS / NIVELES */}
          {educations.length === 0 ? (
            <div className="rounded-3xl border border-warm-200/80 bg-white p-12 text-center shadow-xs dark:border-white/10 dark:bg-[#0f172a]">
              <GraduationCap className="mx-auto h-12 w-12 text-warm-400 mb-3" />
              <h4 className="text-base font-bold text-warm-900 dark:text-white">
                {isSpanish ? 'No hay escuelas ni niveles registrados aún' : 'No education records found yet'}
              </h4>
              <p className="text-xs text-warm-500 dark:text-warm-400 mt-1">
                {isSpanish
                  ? 'Registra la escuela de Primaria, Secundaria, Preparatoria o Universidad del miembro.'
                  : 'Add elementary, middle, high school, or university records for this member.'}
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {educations.map((edu) => (
                <div
                  key={edu.educationId}
                  className={`rounded-3xl border p-5 shadow-xs transition ${
                    edu.isCurrent
                      ? 'border-indigo-400/80 bg-indigo-50/30 dark:border-indigo-500/30 dark:bg-indigo-950/20'
                      : 'border-warm-200/80 bg-white dark:border-white/10 dark:bg-[#0f172a]'
                  }`}
                >
                  <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-3">
                    <div className="flex items-start gap-3.5">
                      <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-indigo-100 text-indigo-700 dark:bg-indigo-950/60 dark:text-indigo-300">
                        <Building2 className="h-5 w-5" />
                      </div>
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <h4 className="text-base font-bold text-warm-900 dark:text-white">
                            {edu.schoolName}
                          </h4>
                          <span className="rounded-full bg-indigo-100 px-2.5 py-0.5 text-[11px] font-bold text-indigo-800 dark:bg-indigo-950/80 dark:text-indigo-300">
                            {edu.educationLevel}
                          </span>
                          {edu.isCurrent && (
                            <span className="rounded-full bg-emerald-100 px-2.5 py-0.5 text-[10px] font-black uppercase tracking-wider text-emerald-800 dark:bg-emerald-950/80 dark:text-emerald-300">
                              {isSpanish ? 'Nivel Actual' : 'Current'}
                            </span>
                          )}
                        </div>

                        {edu.gradeOrDegree && (
                          <p className="text-xs font-semibold text-warm-700 dark:text-warm-300 mt-1">
                            🎓 {edu.gradeOrDegree} {edu.fieldOfStudy ? `· ${edu.fieldOfStudy}` : ''}
                          </p>
                        )}

                        {edu.degree && (
                          <p className="text-xs font-bold text-indigo-700 dark:text-indigo-300 mt-0.5">
                            📜 {isSpanish ? 'Título / Grado' : 'Degree'}: {edu.degree}
                          </p>
                        )}

                        <div className="mt-1 flex flex-wrap gap-3 text-[11px] text-warm-500 dark:text-warm-400">
                          {(edu.startYear || edu.endYear) && (
                            <span className="flex items-center gap-1">
                              <Calendar className="h-3 w-3" />
                              {edu.startYear || '—'} - {edu.isCurrent ? (isSpanish ? 'Presente' : 'Present') : (edu.endYear || '—')}
                            </span>
                          )}
                          {(edu.city || edu.state || edu.country) && (
                            <span className="flex items-center gap-1">
                              <MapPin className="h-3 w-3" />
                              {[edu.city, edu.state, edu.country].filter(Boolean).join(', ')}
                            </span>
                          )}
                          {edu.gpa && (
                            <span className="flex items-center gap-1 font-semibold text-amber-700 dark:text-amber-300">
                              ⭐ GPA: {edu.gpa}
                            </span>
                          )}
                        </div>

                        {edu.notes && (
                          <p className="mt-2 text-xs text-warm-600 dark:text-warm-300 bg-white/60 dark:bg-white/5 rounded-xl p-2.5">
                            📝 {edu.notes}
                          </p>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center gap-2 self-end sm:self-start">
                      <button
                        type="button"
                        onClick={() => handleStartEditEdu(edu)}
                        className="inline-flex items-center gap-1 rounded-xl border border-warm-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-warm-700 hover:bg-warm-100 dark:border-white/10 dark:bg-slate-800 dark:text-warm-200"
                      >
                        <Edit2 className="h-3.5 w-3.5 text-indigo-600" />
                        <span>{isSpanish ? 'Editar' : 'Edit'}</span>
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDeleteEdu(edu.educationId)}
                        className="inline-flex items-center gap-1 rounded-xl border border-rose-200 bg-white px-2.5 py-1.5 text-xs font-semibold text-rose-600 hover:bg-rose-50 dark:border-rose-900/40 dark:bg-slate-800 dark:text-rose-400"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                        <span>{isSpanish ? 'Eliminar' : 'Delete'}</span>
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
