import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Award, Briefcase, Camera, CheckCircle2, GraduationCap, Languages, User, X, ZoomIn } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import {
  createMyProfile,
  getMyProfile,
  getProfilePhotoUrl,
  updateMyProfile,
  updatePersonOrgRole,
  uploadProfilePhoto,
  type PersonProfileDto,
  type UpsertPersonProfileRequest,
} from './api/personProfileApi';
import { getJobRole } from './api/jobRoleApi';
import { extractMyResume, getMyResume, saveMyResume } from './api/resumeApi';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

const ORG_ROLE_LABELS: Record<'en' | 'es', Record<string, string>> = {
  es: {
    Employee: 'Empleado',
    Manager: 'Gerente',
    HRAdmin: 'Administrador RRHH',
  },
  en: {
    Employee: 'Employee',
    Manager: 'Manager',
    HRAdmin: 'HR Administrator',
  },
};

const LANGUAGE_OPTIONS = [
  { code: 'es', label: 'Español' },
  { code: 'en', label: 'English' },
];

// Todos los países del continente americano (ISO 3166-1 alpha-2), de norte
// a sur incluyendo el Caribe — combobox en vez de texto libre (2026-08-02).
const COUNTRY_OPTIONS = [
  { code: 'CA', label: 'Canadá' },
  { code: 'US', label: 'Estados Unidos' },
  { code: 'MX', label: 'México' },
  { code: 'BZ', label: 'Belice' },
  { code: 'CR', label: 'Costa Rica' },
  { code: 'SV', label: 'El Salvador' },
  { code: 'GT', label: 'Guatemala' },
  { code: 'HN', label: 'Honduras' },
  { code: 'NI', label: 'Nicaragua' },
  { code: 'PA', label: 'Panamá' },
  { code: 'AG', label: 'Antigua y Barbuda' },
  { code: 'BS', label: 'Bahamas' },
  { code: 'BB', label: 'Barbados' },
  { code: 'CU', label: 'Cuba' },
  { code: 'DM', label: 'Dominica' },
  { code: 'DO', label: 'República Dominicana' },
  { code: 'GD', label: 'Granada' },
  { code: 'HT', label: 'Haití' },
  { code: 'JM', label: 'Jamaica' },
  { code: 'KN', label: 'San Cristóbal y Nieves' },
  { code: 'LC', label: 'Santa Lucía' },
  { code: 'VC', label: 'San Vicente y las Granadinas' },
  { code: 'TT', label: 'Trinidad y Tobago' },
  { code: 'AR', label: 'Argentina' },
  { code: 'BO', label: 'Bolivia' },
  { code: 'BR', label: 'Brasil' },
  { code: 'CL', label: 'Chile' },
  { code: 'CO', label: 'Colombia' },
  { code: 'EC', label: 'Ecuador' },
  { code: 'GY', label: 'Guyana' },
  { code: 'PY', label: 'Paraguay' },
  { code: 'PE', label: 'Perú' },
  { code: 'SR', label: 'Surinam' },
  { code: 'UY', label: 'Uruguay' },
  { code: 'VE', label: 'Venezuela' },
];

// ~20 most relevant IANA time zones for this user base (México/USA-heavy,
// per the phone area-code list above, plus the other major LatAm/EU/Asia
// hubs) — shown as a combobox instead of free text (2026-08-02).
const TIME_ZONE_OPTIONS = [
  { value: 'America/Tijuana', label: 'Tijuana (Pacífico, México)' },
  { value: 'America/Hermosillo', label: 'Hermosillo (México)' },
  { value: 'America/Chihuahua', label: 'Chihuahua (México)' },
  { value: 'America/Mexico_City', label: 'Ciudad de México' },
  { value: 'America/Cancun', label: 'Cancún (México)' },
  { value: 'America/Los_Angeles', label: 'Los Ángeles (EE. UU., PT)' },
  { value: 'America/Denver', label: 'Denver (EE. UU., MT)' },
  { value: 'America/Chicago', label: 'Chicago (EE. UU., CT)' },
  { value: 'America/New_York', label: 'Nueva York (EE. UU., ET)' },
  { value: 'America/Bogota', label: 'Bogotá' },
  { value: 'America/Lima', label: 'Lima' },
  { value: 'America/Santiago', label: 'Santiago de Chile' },
  { value: 'America/Argentina/Buenos_Aires', label: 'Buenos Aires' },
  { value: 'America/Sao_Paulo', label: 'São Paulo' },
  { value: 'Europe/Madrid', label: 'Madrid' },
  { value: 'Europe/London', label: 'Londres' },
  { value: 'Europe/Paris', label: 'París' },
  { value: 'Europe/Berlin', label: 'Berlín' },
  { value: 'Asia/Dubai', label: 'Dubái' },
  { value: 'Asia/Shanghai', label: 'Shangái' },
  { value: 'Asia/Tokyo', label: 'Tokio' },
  { value: 'Australia/Sydney', label: 'Sídney' },
];

// Phone area code — only USA and México for now, per explicit request
// (2026-08-02). `PersonProfile.PhoneNumber` is a single string on the
// backend, so the code is stored as a "+52 5512345678"-style prefix and
// parsed back out on load — no separate backend field needed for this.
const PHONE_AREA_CODES = [
  { code: '+52', label: 'México (+52)' },
  { code: '+1', label: 'EE. UU. (+1)' },
];

function parsePhoneNumber(stored: string | null): { areaCode: string; number: string } {
  const trimmed = (stored ?? '').trim();
  for (const { code } of PHONE_AREA_CODES) {
    if (trimmed.startsWith(`${code} `)) {
      return { areaCode: code, number: trimmed.slice(code.length + 1).trim() };
    }
    if (trimmed.startsWith(code)) {
      return { areaCode: code, number: trimmed.slice(code.length).trim() };
    }
  }
  return { areaCode: PHONE_AREA_CODES[0]!.code, number: trimmed };
}

const emptyForm = {
  firstName: '',
  lastName: '',
  displayName: '',
  phoneAreaCode: PHONE_AREA_CODES[0]!.code,
  phoneNumber: '',
  preferredLanguage: 'es',
  countryCode: '',
  timeZone: '',
  dateOfBirth: '',
  biography: '',
};

type ProfileFormState = typeof emptyForm;

function toFormState(profile: PersonProfileDto | null): ProfileFormState {
  if (!profile) return emptyForm;
  const { areaCode, number } = parsePhoneNumber(profile.PhoneNumber);
  return {
    firstName: profile.FirstName ?? '',
    lastName: profile.LastName ?? '',
    displayName: profile.DisplayName ?? '',
    phoneAreaCode: areaCode,
    phoneNumber: number,
    preferredLanguage: profile.PreferredLanguage || 'es',
    countryCode: profile.CountryCode ?? '',
    timeZone: profile.TimeZone ?? '',
    dateOfBirth: profile.DateOfBirth ?? '',
    biography: profile.Biography ?? '',
  };
}

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
}

const inputClass =
  'mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white';
const labelClass = 'block text-xs font-medium text-slate-500 dark:text-white/50';

/** "Mi Perfil" — the employee's own self-service profile (name, contact
 *  info, occupation/biography, etc. — the PersonProfile entity). This is
 *  deliberately separate from the HR-only org-assignment fields
 *  (Department/JobRole/Manager/OrgRole), which stay editable only from
 *  human-os-enterprise's EmployeeDetailPage — every employee can view and
 *  edit their OWN contact/personal details here, but nothing that affects
 *  the org chart or their permission tier. Uses the same GET/POST/PUT
 *  /people/{personId}/profile endpoints as EmployeeDetailPage's résumé
 *  section relies on indirectly (PersonProfileService). */
export function ProfilePage() {
  const { user, refreshUser } = useAuth();
  const { t, language } = useI18n();
  const personId = user?.personId ?? '';
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [form, setForm] = useState<ProfileFormState>(emptyForm);
  const [selectedOrgRole, setSelectedOrgRole] = useState<string>(user?.orgRole ?? 'Employee');
  const [savedMessage, setSavedMessage] = useState(false);

  useEffect(() => {
    if (user?.orgRole) {
      setSelectedOrgRole(user.orgRole);
    }
  }, [user?.orgRole]);

  const profileQuery = useQuery({
    queryKey: ['my-profile', personId],
    queryFn: () => getMyProfile(personId),
    enabled: !!personId,
  });

  // Prefer the real profile name over the MSAL account's raw "name" claim,
  // which can be a bare username/test-account string instead of the
  // person's actual first and last name.
  const profileFullName = profileQuery.data?.DisplayName
    || [profileQuery.data?.FirstName, profileQuery.data?.LastName].filter(Boolean).join(' ');
  const emailFullName = user?.email?.split('@')[0]
    ?.split(/[._-]/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
    .join(' ');
  const safeName = profileFullName || emailFullName || user?.name;

  // Full job description (title/purpose/skills/etc.) for "Mi Puesto y
  // Desarrollo" below — read-only here, HR-managed data (2026-08-02).
  const jobRoleQuery = useQuery({
    queryKey: ['my-job-role', user?.jobRoleId],
    queryFn: () => getJobRole(user!.jobRoleId!),
    enabled: !!user?.jobRoleId,
  });

  const resumeQuery = useQuery({
    queryKey: ['my-resume', personId],
    queryFn: () => getMyResume(personId),
    enabled: !!personId,
  });
  const resumeSourceText = resumeQuery.data && (resumeQuery.data.rawResumeText?.trim() || [
    resumeQuery.data.professionalObjective,
    resumeQuery.data.professionalSummary,
    ...resumeQuery.data.education,
    ...resumeQuery.data.skills,
    ...resumeQuery.data.workExperience,
  ].filter(Boolean).join('\n'));
  const isStudentResume = Boolean(resumeSourceText && /estudiante|student|semestre|graduaci[oó]n|expected|esperado|ingenier[ií]a/i.test(resumeSourceText));

  // Seed the form from the loaded profile exactly once per fetch — after
  // that the form is fully user-controlled so an in-flight edit never
  // gets clobbered by a background refetch.
  useEffect(() => {
    if (profileQuery.isSuccess) {
      setForm(toFormState(profileQuery.data));
    }
  }, [profileQuery.isSuccess, profileQuery.data]);

  const saveMutation = useMutation({
    mutationFn: (body: UpsertPersonProfileRequest) =>
      profileQuery.data ? updateMyProfile(personId, body) : createMyProfile(personId, body),
    onSuccess: (profile) => {
      queryClient.setQueryData(['my-profile', personId], profile);
      setSavedMessage(true);
      setTimeout(() => setSavedMessage(false), 3000);
    },
  });

  const extractStudentMutation = useMutation({
    mutationFn: async () => {
      const resume = resumeQuery.data;
      if (!resumeSourceText) throw new Error('Resume text is required');
      const extracted = await extractMyResume(personId, resumeSourceText);
      return saveMyResume(personId, { ...resume, ...extracted });
    },
    onSuccess: (resume) => {
      queryClient.setQueryData(['my-resume', personId], resume);
    },
  });

  const uploadPhotoMutation = useMutation({
    mutationFn: (file: File) => uploadProfilePhoto(personId, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-profile', personId] });
    },
  });

  function handlePhotoChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (file) uploadPhotoMutation.mutate(file);
  }

  function handleChange(key: keyof ProfileFormState, value: string) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  const [isPhotoZoomOpen, setIsPhotoZoomOpen] = useState(false);

  const orgRoleMutation = useMutation({
    mutationFn: (newRole: string) => updatePersonOrgRole(personId, newRole),
    onSuccess: async () => {
      await refreshUser();
      queryClient.invalidateQueries({ queryKey: ['my-profile', personId] });
    },
  });

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmedPhone = form.phoneNumber.trim();
    if (selectedOrgRole !== user?.orgRole) {
      orgRoleMutation.mutate(selectedOrgRole);
    }
    saveMutation.mutate({
      firstName: form.firstName || null,
      lastName: form.lastName || null,
      displayName: form.displayName || null,
      phoneNumber: trimmedPhone ? `${form.phoneAreaCode} ${trimmedPhone}` : null,
      preferredLanguage: form.preferredLanguage,
      countryCode: form.countryCode || null,
      timeZone: form.timeZone || null,
      dateOfBirth: form.dateOfBirth || null,
      // Occupation is never manually typed here either — it always
      // mirrors the real Job Role title HR assigned (see the read-only
      // field below and "Mi Puesto y Desarrollo"), same rationale as
      // Company mirroring the tenant.
      occupation: user?.jobRoleTitle ?? null,
      // Company is never manually typed here — this app only serves
      // tenant-linked employees, so it always mirrors the real company
      // they logged in under (see EmployeeAppLayout's notEnterpriseAccount
      // gate), instead of letting it drift out of sync via free text.
      company: user?.tenantName ?? null,
      biography: form.biography || null,
    });
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6 pb-16">
      {profileQuery.isLoading && (
        <LoadingBackdrop title={t.profileLoading} description={t.profileLoadingDesc} icon={User} />
      )}

      {/* Gradient header banner — same visual family as CareerPlanPage's. */}
      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-indigo-600 via-indigo-500 to-violet-600 p-6 text-white shadow-lg shadow-indigo-500/20 dark:border-white/10 sm:p-8">
        <div className="flex items-center gap-4">
          <div className="group relative h-16 w-16 shrink-0">
            {profileQuery.data?.ProfilePhotoUrl ? (
              <button
                type="button"
                onClick={() => setIsPhotoZoomOpen(true)}
                aria-label="Ampliar foto de perfil"
                title="Ampliar foto de perfil"
                className="group/zoom relative block h-16 w-16 overflow-hidden rounded-2xl ring-1 ring-white/25"
              >
                <img
                  src={getProfilePhotoUrl(personId, profileQuery.data.UpdatedDate)}
                  alt={safeName ?? 'Empleado'}
                  className="h-16 w-16 object-cover"
                />
                <span className="absolute inset-0 flex items-center justify-center bg-black/40 opacity-0 transition group-hover/zoom:opacity-100">
                  <ZoomIn className="h-5 w-5 text-white" />
                </span>
              </button>
            ) : (
              <span className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 text-xl font-bold ring-1 ring-white/25 backdrop-blur">
                {initialsOf(safeName ?? 'Empleado')}
              </span>
            )}
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              disabled={uploadPhotoMutation.isPending}
              aria-label="Cambiar foto de perfil"
              title="Cambiar foto de perfil"
              className="absolute -bottom-1.5 -right-1.5 flex h-7 w-7 items-center justify-center rounded-full border-2 border-white bg-indigo-600 text-white shadow-md transition hover:bg-indigo-700 disabled:opacity-60 dark:border-slate-900"
            >
              <Camera className="h-3.5 w-3.5" />
            </button>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/png,image/jpeg,image/webp"
              className="hidden"
              onChange={handlePhotoChange}
            />
          </div>
          <div>
            <p className="text-lg font-semibold">{safeName}</p>
            <p className="text-sm text-white/80">{user?.email}</p>
            <div className="mt-2 flex flex-wrap gap-2 text-xs">
              {user?.tenantName && (
                <span className="rounded-full bg-white/15 px-2.5 py-1 font-medium">{user.tenantName}</span>
              )}
              {user?.tenantId && (
                <span className="rounded-full bg-white/15 px-2.5 py-1 font-mono">Tenant ID: {user.tenantId}</span>
              )}
              {user?.orgRole && (
                <span className="rounded-full bg-white/15 px-2.5 py-1 font-medium">
                  {ORG_ROLE_LABELS[language][user.orgRole] ?? user.orgRole}
                </span>
              )}
              {user?.jobRoleTitle && (
                <span className="rounded-full bg-white/15 px-2.5 py-1 font-medium">{user.jobRoleTitle}</span>
              )}
              {user?.departmentName && (
                <span className="rounded-full bg-white/15 px-2.5 py-1 font-medium">{user.departmentName}</span>
              )}
            </div>
          </div>
        </div>
      </div>

      <Card className="p-6 sm:p-8">
        <div className="mb-6 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{t.profileTitle}</h2>
          {savedMessage && (
            <span className="flex items-center gap-1.5 text-sm font-medium text-emerald-600 dark:text-emerald-400">
              <CheckCircle2 className="h-4 w-4" />
              {t.profileSaved}
            </span>
          )}
        </div>

        <form onSubmit={handleSubmit} className="space-y-6">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <label>
              <span className={labelClass}>{t.profileFirstName}</span>
              <input
                className={inputClass}
                value={form.firstName}
                onChange={(e) => handleChange('firstName', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{t.profileLastName}</span>
              <input
                className={inputClass}
                value={form.lastName}
                onChange={(e) => handleChange('lastName', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{t.profileDisplayName}</span>
              <input
                className={inputClass}
                value={form.displayName}
                onChange={(e) => handleChange('displayName', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{t.profilePhone}</span>
              <div className="mt-1 flex gap-2">
                <select
                  className="w-32 rounded-lg border border-slate-200 bg-white px-2 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                  value={form.phoneAreaCode}
                  onChange={(e) => handleChange('phoneAreaCode', e.target.value)}
                >
                  {PHONE_AREA_CODES.map((area) => (
                    <option key={area.code} value={area.code}>
                      {area.label}
                    </option>
                  ))}
                </select>
                <input
                  className="w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                  placeholder="5512345678"
                  inputMode="tel"
                  value={form.phoneNumber}
                  onChange={(e) => handleChange('phoneNumber', e.target.value)}
                />
              </div>
            </label>
            <label>
              <span className={labelClass}>{t.profilePreferredLanguage}</span>
              <select
                className={inputClass}
                value={form.preferredLanguage}
                onChange={(e) => handleChange('preferredLanguage', e.target.value)}
              >
                {LANGUAGE_OPTIONS.map((lang) => (
                  <option key={lang.code} value={lang.code}>
                    {lang.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span className={labelClass}>{t.profileCountry}</span>
              <select
                className={inputClass}
                value={form.countryCode}
                onChange={(e) => handleChange('countryCode', e.target.value)}
              >
                <option value="">{t.profileSelectCountry}</option>
                {/* Si el valor guardado no está en la lista (un registro
                    previo fuera del continente americano), lo mantenemos
                    seleccionable en vez de descartarlo silenciosamente. */}
                {form.countryCode && !COUNTRY_OPTIONS.some((c) => c.code === form.countryCode) && (
                  <option value={form.countryCode}>{form.countryCode}</option>
                )}
                {COUNTRY_OPTIONS.map((country) => (
                  <option key={country.code} value={country.code}>
                    {country.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span className={labelClass}>{t.profileTimeZone}</span>
              <select
                className={inputClass}
                value={form.timeZone}
                onChange={(e) => handleChange('timeZone', e.target.value)}
              >
                <option value="">{t.profileSelectTimeZone}</option>
                {/* If the stored value doesn't match any option below (an
                    older/custom entry), keep it selectable instead of
                    silently discarding it on next save. */}
                {form.timeZone && !TIME_ZONE_OPTIONS.some((tz) => tz.value === form.timeZone) && (
                  <option value={form.timeZone}>{form.timeZone}</option>
                )}
                {TIME_ZONE_OPTIONS.map((tz) => (
                  <option key={tz.value} value={tz.value}>
                    {tz.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span className={labelClass}>{t.profileDateOfBirth}</span>
              <input
                type="date"
                className={inputClass}
                value={form.dateOfBirth}
                onChange={(e) => handleChange('dateOfBirth', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{language === 'es' ? 'Tipo de empleado / Rol' : 'Employee Type / Role'}</span>
              <select
                className={`${inputClass} font-medium text-brand-700 dark:text-brand-300`}
                value={selectedOrgRole}
                onChange={(e) => setSelectedOrgRole(e.target.value)}
              >
                <option value="Employee">{ORG_ROLE_LABELS[language].Employee ?? 'Empleado'}</option>
                <option value="Manager">{ORG_ROLE_LABELS[language].Manager ?? 'Gerente'}</option>
                <option value="HRAdmin">{ORG_ROLE_LABELS[language].HRAdmin ?? 'Administrador RRHH'}</option>
              </select>
            </label>
            <label>
              <span className={labelClass}>{t.profileOccupation}</span>
              <input
                className={`${inputClass} cursor-not-allowed bg-slate-50 text-slate-500 dark:bg-white/[0.02] dark:text-white/40`}
                value={user?.jobRoleTitle ?? t.profileUnassignedJob}
                readOnly
                disabled
                title={t.profileOccupationTitle}
              />
            </label>
            <label>
              <span className={labelClass}>{t.profileCompany}</span>
              <input
                className={`${inputClass} cursor-not-allowed bg-slate-50 text-slate-500 dark:bg-white/[0.02] dark:text-white/40`}
                value={user?.tenantName ?? t.profileUnassignedCompany}
                readOnly
                disabled
                title={t.profileCompanyTitle}
              />
            </label>
            <label>
              <span className={labelClass}>{t.profileDepartment}</span>
              <input
                className={`${inputClass} cursor-not-allowed bg-slate-50 text-slate-500 dark:bg-white/[0.02] dark:text-white/40`}
                value={user?.departmentName ?? t.profileUnassignedDepartment}
                readOnly
                disabled
                title={t.profileDepartmentTitle}
              />
            </label>
            <label>
              <span className={labelClass}>{t.profileManager}</span>
              <input
                className={`${inputClass} cursor-not-allowed bg-slate-50 text-slate-500 dark:bg-white/[0.02] dark:text-white/40`}
                value={user?.managerDisplayName ?? t.profileUnassignedManager}
                readOnly
                disabled
                title={t.profileManagerTitle}
              />
            </label>
          </div>

          <label className="block">
            <span className={labelClass}>{t.profileBiography}</span>
            <textarea
              className={`${inputClass} min-h-[100px] resize-y`}
              value={form.biography}
              onChange={(e) => handleChange('biography', e.target.value)}
            />
          </label>

          {saveMutation.isError && (
            <p className="text-sm text-red-600 dark:text-red-400">
              {t.profileSaveError}
            </p>
          )}

          <div className="flex justify-end">
            <button
              type="submit"
              disabled={saveMutation.isPending}
              className="rounded-full bg-brand-600 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:opacity-60"
            >
              {saveMutation.isPending ? t.profileSaving : t.profileSaveChanges}
            </button>
          </div>
        </form>
      </Card>

      {/* "Mi Puesto y Desarrollo" (2026-08-02) — the employee's real job
          description/required skills, already captured by HR in
          human-os-enterprise (Department/JobRole), surfaced here read-only
          so they can see exactly what's expected of them and identify
          where to grow — this is the whole point of "Mi Perfil": a single
          place to see everything relevant to their training and personal
          development. Nothing here is editable from this app (HR-only,
          same as Department/JobRole/OrgRole assignment). */}
      {user?.jobRoleId && (
        <Card className="p-6 sm:p-8">
          <div className="mb-6 flex items-center gap-2">
            <Briefcase className="h-5 w-5 text-brand-600 dark:text-brand-400" />
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{t.profileRoleDevTitle}</h2>
          </div>

          {jobRoleQuery.isLoading && (
            <p className="text-sm text-slate-500 dark:text-white/50">{t.profileJobRoleLoading}</p>
          )}

          {jobRoleQuery.isError && (
            <p className="text-sm text-red-600 dark:text-red-400">
              {t.profileJobRoleError}
            </p>
          )}

          {jobRoleQuery.data && (
            <div className="space-y-6">
              <div>
                <p className="text-base font-semibold text-slate-900 dark:text-white">
                  {jobRoleQuery.data.title}
                  {jobRoleQuery.data.levelLabel && (
                    <span className="ml-2 text-sm font-normal text-slate-500 dark:text-white/50">
                      ({jobRoleQuery.data.levelLabel})
                    </span>
                  )}
                </p>
                <p className="text-sm text-slate-500 dark:text-white/50">{jobRoleQuery.data.departmentName}</p>
              </div>

              {jobRoleQuery.data.purpose && (
                <JobRoleField label={t.profileJobPurpose} value={jobRoleQuery.data.purpose} />
              )}
              {jobRoleQuery.data.summary && <JobRoleField label={t.profileJobSummary} value={jobRoleQuery.data.summary} />}
              <JobRoleListField label={t.profileJobResponsibilities} items={jobRoleQuery.data.responsibilities} />

              <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                <JobRoleListField
                  label={t.profileJobTechnicalSkills}
                  items={jobRoleQuery.data.technicalSkills}
                  pills
                />
                <JobRoleListField label={t.profileJobSoftSkills} items={jobRoleQuery.data.softSkills} pills />
              </div>

              {jobRoleQuery.data.requiredExperience && (
                <JobRoleField label={t.profileJobExperience} value={jobRoleQuery.data.requiredExperience} />
              )}
              {jobRoleQuery.data.requiredEducation && (
                <JobRoleField label={t.profileJobEducation} value={jobRoleQuery.data.requiredEducation} />
              )}
              <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
                <JobRoleListField
                  label={t.profileJobCertifications}
                  items={jobRoleQuery.data.requiredCertifications}
                  pills
                />
                <JobRoleListField label={t.profileJobLanguages} items={jobRoleQuery.data.requiredLanguages} pills />
              </div>
              <JobRoleListField label={t.profileJobTools} items={jobRoleQuery.data.tools} pills />
            </div>
          )}
        </Card>
      )}

      <Card className="p-6 sm:p-8">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
          <GraduationCap className="h-5 w-5 text-brand-600 dark:text-brand-400" />
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{t.profileResumeTitle}</h2>
          </div>
          {isStudentResume && resumeSourceText && (
            <button
              type="button"
              onClick={() => extractStudentMutation.mutate()}
              disabled={extractStudentMutation.isPending}
              className="rounded-full bg-brand-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-brand-700 disabled:cursor-wait disabled:opacity-60"
            >
              {extractStudentMutation.isPending
                ? (language === 'es' ? 'Extrayendo...' : 'Extracting...')
                : (language === 'es' ? 'Extraer perfil académico' : 'Extract academic profile')}
            </button>
          )}
        </div>
        {extractStudentMutation.isError && <p className="mb-4 text-sm text-red-600 dark:text-red-400">{language === 'es' ? 'No se pudo extraer la información académica.' : 'Academic information could not be extracted.'}</p>}

        {resumeQuery.isLoading && <p className="text-sm text-slate-500 dark:text-white/50">{t.profileResumeLoading}</p>}
        {resumeQuery.isError && <p className="text-sm text-red-600 dark:text-red-400">{t.profileResumeError}</p>}
        {resumeQuery.data && (
          <div className="grid gap-4 md:grid-cols-2">
            {resumeQuery.data.studentAcademicProfile?.semesters?.length ? (
              <div className="rounded-2xl border border-emerald-200 bg-emerald-50/60 p-5 md:col-span-2 dark:border-emerald-500/20 dark:bg-emerald-500/10">
                <h3 className="text-sm font-semibold text-emerald-900 dark:text-emerald-200">Perfil académico</h3>
                <p className="mt-1 text-xs text-emerald-800/70 dark:text-emerald-200/70">{resumeQuery.data.studentAcademicProfile.institution} · {resumeQuery.data.studentAcademicProfile.degree} · Promedio: {resumeQuery.data.studentAcademicProfile.cumulativeAverage ?? 'N/D'} / {resumeQuery.data.studentAcademicProfile.averageScale ?? '100'}</p>
                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                  {resumeQuery.data.studentAcademicProfile.semesters.map((semester) => (
                    <div key={semester.name} className="rounded-xl border border-emerald-200 bg-white p-3 dark:border-emerald-500/20 dark:bg-white/5">
                      <div className="flex justify-between text-sm font-semibold text-slate-800 dark:text-white"><span>{semester.name}</span><span>{semester.average ?? 'NA'}</span></div>
                      <div className="mt-2 space-y-1 text-xs text-slate-600 dark:text-white/70">{semester.subjects.map((subject) => <div key={subject.name} className="flex justify-between gap-2"><span>{subject.name}</span><span>{subject.grade ?? 'NA'}</span></div>)}</div>
                    </div>
                  ))}
                </div>
              </div>
            ) : null}
            {resumeQuery.data.professionalObjective && (
              <div className="rounded-2xl border border-blue-100 bg-blue-50/60 p-5 dark:border-blue-400/20 dark:bg-blue-400/[0.08]">
                <h3 className="text-sm font-semibold text-blue-700 dark:text-blue-200">{t.profileResumeObjective}</h3>
                <p className="mt-3 text-sm leading-6 text-slate-700 dark:text-white/75">{resumeQuery.data.professionalObjective}</p>
              </div>
            )}
            {resumeQuery.data.professionalSummary && (
              <div className="rounded-2xl border border-slate-200 bg-slate-50 p-5 dark:border-white/10 dark:bg-white/[0.04]">
                <h3 className="text-sm font-semibold text-slate-900 dark:text-white">{t.profileResumeSummary}</h3>
                <p className="mt-3 text-sm leading-6 text-slate-700 dark:text-white/75">{resumeQuery.data.professionalSummary}</p>
              </div>
            )}
            {([
              ['profileResumeSkills', resumeQuery.data.skills, Briefcase, true],
              ['profileResumeWorkExperience', resumeQuery.data.workExperience, Briefcase, false],
              ['profileResumeEducation', resumeQuery.data.education, GraduationCap, false],
              ['profileResumeCertifications', resumeQuery.data.certifications, Award, false],
              ['profileResumeLanguages', resumeQuery.data.languages, Languages, true],
            ] as const).map(([key, values, Icon]) => values.length > 0 && (
              <div key={key} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm shadow-slate-200/40 dark:border-white/10 dark:bg-white/[0.03] dark:shadow-none">
                <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-900 dark:text-white">
                  <Icon className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                  {t[key]}
                </h3>
                <ul className="mt-4 space-y-3 text-sm text-slate-700 dark:text-white/70">
                  {values.map((value, index) => <li key={`${key}-${index}`} className={key === 'profileResumeSkills' || key === 'profileResumeLanguages' ? 'inline-flex mr-2 mb-2 rounded-full border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-medium dark:border-white/10 dark:bg-white/[0.06]' : 'border-l-2 border-brand-300 pl-3 leading-6 dark:border-brand-400/50'}>{value}</li>)}
                </ul>
              </div>
            ))}
            {!resumeQuery.data.professionalObjective && !resumeQuery.data.professionalSummary &&
              !resumeQuery.data.skills.length && !resumeQuery.data.workExperience.length &&
              !resumeQuery.data.education.length && !resumeQuery.data.certifications.length &&
              !resumeQuery.data.languages.length && (
                <p className="text-sm text-slate-500 dark:text-white/50">{t.profileResumeEmpty}</p>
              )}
          </div>
        )}
      </Card>

      {isPhotoZoomOpen && profileQuery.data?.ProfilePhotoUrl && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-6"
          onClick={() => setIsPhotoZoomOpen(false)}
        >
          <button
            type="button"
            onClick={() => setIsPhotoZoomOpen(false)}
            aria-label="Cerrar"
            className="absolute right-6 top-6 flex h-9 w-9 items-center justify-center rounded-full bg-white/10 text-white transition hover:bg-white/20"
          >
            <X className="h-5 w-5" />
          </button>
          <img
            src={getProfilePhotoUrl(personId, profileQuery.data.UpdatedDate)}
            alt={user?.name ?? 'Empleado'}
            className="max-h-[85vh] max-w-[90vw] rounded-2xl object-contain shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          />
        </div>
      )}
    </div>
  );
}

function JobRoleField({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <span className={labelClass}>{label}</span>
      <p className="mt-1 whitespace-pre-line text-sm text-slate-700 dark:text-white/70">{value}</p>
    </div>
  );
}

function JobRoleListField({ label, items, pills }: { label: string; items: string[]; pills?: boolean }) {
  if (items.length === 0) return null;
  return (
    <div>
      <span className={labelClass}>{label}</span>
      {pills ? (
        <div className="mt-1.5 flex flex-wrap gap-1.5">
          {items.map((item) => (
            <span
              key={item}
              className="rounded-full bg-brand-500/10 px-2.5 py-1 text-xs font-medium text-brand-700 dark:bg-brand-500/15 dark:text-brand-300"
            >
              {item}
            </span>
          ))}
        </div>
      ) : (
        <ul className="mt-1 list-inside list-disc space-y-1 text-sm text-slate-700 dark:text-white/70">
          {items.map((item) => (
            <li key={item}>{item}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
