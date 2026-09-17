import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { Award, BriefcaseBusiness, Camera, CheckCircle2, GraduationCap, Languages, User } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import {
  createMyProfile,
  getMyProfile,
  getProfilePhotoUrl,
  updateMyProfile,
  uploadProfilePhoto,
  getMyResume,
  type PersonProfileDto,
  type UpsertPersonProfileRequest,
} from '../api/personProfileApi';

const LANGUAGE_OPTIONS = [
  { code: 'en', label: 'English' },
  { code: 'es', label: 'Español' },
];

// Every country in the Americas (ISO 3166-1 alpha-2), north to south
// including the Caribbean — combobox instead of free text.
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

// ~20 most relevant IANA time zones for this user base.
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

// USA and México only, per the same phone-area-code convention already
// used by human-os-employee's ProfilePage. `PersonProfile.PhoneNumber` is
// a single string on the backend, so the code is stored as a
// "+52 5512345678"-style prefix and parsed back out on load.
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
  preferredLanguage: 'en',
  countryCode: '',
  timeZone: '',
  dateOfBirth: '',
  occupation: '',
  company: '',
  biography: '',
};

type ProfileFormState = typeof emptyForm;

function toFormState(profile: PersonProfileDto | null, fallbackLanguage: string): ProfileFormState {
  if (!profile) return { ...emptyForm, preferredLanguage: fallbackLanguage };
  const { areaCode, number } = parsePhoneNumber(profile.PhoneNumber);
  return {
    firstName: profile.FirstName ?? '',
    lastName: profile.LastName ?? '',
    displayName: profile.DisplayName ?? '',
    phoneAreaCode: areaCode,
    phoneNumber: number,
    preferredLanguage: profile.PreferredLanguage || fallbackLanguage,
    countryCode: profile.CountryCode ?? '',
    timeZone: profile.TimeZone ?? '',
    dateOfBirth: profile.DateOfBirth ?? '',
    occupation: profile.Occupation ?? '',
    company: profile.Company ?? '',
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

/** "Mi Perfil" — the account's own self-service profile (name, contact
 *  info, occupation/biography, photo — the PersonProfile entity). Uses
 *  the same GET/POST/PUT /people/{personId}/profile endpoints as
 *  human-os-employee's ProfilePage, ported here for individual/consumer
 *  accounts. Deliberately no "Job Role" read-only section — human-os-web
 *  serves individual learners as well as org employees, and org-assigned
 *  job/department data isn't yet exposed by this app's AuthContext. */
export function ProfilePage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [form, setForm] = useState<ProfileFormState>(emptyForm);
  const [savedMessage, setSavedMessage] = useState(false);

  const profileQuery = useQuery({
    queryKey: ['my-profile', personId],
    queryFn: () => getMyProfile(personId),
    enabled: !!personId,
  });

  const resumeQuery = useQuery({
    queryKey: ['my-resume', personId],
    queryFn: () => getMyResume(personId),
    enabled: !!personId,
  });

  // Seed the form from the loaded profile exactly once per fetch — after
  // that the form is fully user-controlled so an in-flight edit never
  // gets clobbered by a background refetch.
  useEffect(() => {
    if (profileQuery.isSuccess) {
      setForm(toFormState(profileQuery.data, user?.preferredLanguage ?? 'en'));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

  const photoMutation = useMutation({
    mutationFn: (file: File) => uploadProfilePhoto(personId, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['my-profile', personId] });
    },
  });

  function handleChange(key: keyof ProfileFormState, value: string) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  function handlePhotoClick() {
    fileInputRef.current?.click();
  }

  function handlePhotoChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (file) photoMutation.mutate(file);
    e.target.value = '';
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmedPhone = form.phoneNumber.trim();
    saveMutation.mutate({
      firstName: form.firstName || null,
      lastName: form.lastName || null,
      displayName: form.displayName || null,
      phoneNumber: trimmedPhone ? `${form.phoneAreaCode} ${trimmedPhone}` : null,
      preferredLanguage: form.preferredLanguage,
      countryCode: form.countryCode || null,
      timeZone: form.timeZone || null,
      dateOfBirth: form.dateOfBirth || null,
      occupation: form.occupation || null,
      company: user?.tenantName || form.company || null,
      biography: form.biography || null,
    });
  }

  const hasPhoto = !!profileQuery.data?.ProfilePhotoUrl;
  const photoUrl = hasPhoto ? getProfilePhotoUrl(personId, profileQuery.data?.UpdatedDate) : null;

  return (
    <div className="mx-auto max-w-4xl space-y-6 pb-16">
      {profileQuery.isLoading && (
        <LoadingBackdrop title={t('profile.loadingTitle')} description={t('profile.loadingDescription')} icon={User} />
      )}

      <div className="overflow-hidden rounded-3xl border border-slate-200 bg-gradient-to-br from-indigo-600 via-indigo-500 to-violet-600 p-6 text-white shadow-lg shadow-indigo-500/20 dark:border-white/10 sm:p-8">
        <div className="flex items-center gap-4">
          <button
            type="button"
            onClick={handlePhotoClick}
            className="group relative flex h-16 w-16 shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-white/15 text-xl font-bold ring-1 ring-white/25 backdrop-blur"
            title={t('profile.changePhoto')}
          >
            {photoUrl ? (
              <img src={photoUrl} alt="" className="h-full w-full object-cover" />
            ) : (
              initialsOf(user?.name ?? 'Engram Academy')
            )}
            <span className="absolute inset-0 flex items-center justify-center bg-black/40 opacity-0 transition group-hover:opacity-100">
              <Camera className="h-5 w-5" />
            </span>
          </button>
          <input ref={fileInputRef} type="file" accept="image/*" className="hidden" onChange={handlePhotoChange} />
          <div>
            <p className="text-lg font-semibold">{user?.name}</p>
            <p className="text-sm text-white/80">{user?.email}</p>
            {user?.tenantName && (
              <div className="mt-2 flex flex-wrap gap-2 text-xs">
                <span className="rounded-full bg-white/15 px-2.5 py-1 font-medium">{user.tenantName}</span>
              </div>
            )}
          </div>
        </div>
        {photoMutation.isError && <p className="mt-3 text-sm text-red-100">{t('profile.photoError')}</p>}
      </div>

      {profileQuery.isError && (
        <Card className="p-6">
          <p className="text-sm text-red-600 dark:text-red-400">{t('profile.loadError')}</p>
        </Card>
      )}

      <Card className="p-6 sm:p-8">
        <div className="mb-6 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{t('profile.personalInfoTitle')}</h2>
          {savedMessage && (
            <span className="flex items-center gap-1.5 text-sm font-medium text-emerald-600 dark:text-emerald-400">
              <CheckCircle2 className="h-4 w-4" />
              {t('profile.savedLabel')}
            </span>
          )}
        </div>

        <form onSubmit={handleSubmit} className="space-y-6">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <label>
              <span className={labelClass}>{t('profile.firstName')}</span>
              <input
                className={inputClass}
                value={form.firstName}
                onChange={(e) => handleChange('firstName', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{t('profile.lastName')}</span>
              <input
                className={inputClass}
                value={form.lastName}
                onChange={(e) => handleChange('lastName', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{t('profile.displayName')}</span>
              <input
                className={inputClass}
                value={form.displayName}
                onChange={(e) => handleChange('displayName', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{t('profile.phone')}</span>
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
              <span className={labelClass}>{t('profile.preferredLanguage')}</span>
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
              <span className={labelClass}>{t('profile.country')}</span>
              <select
                className={inputClass}
                value={form.countryCode}
                onChange={(e) => handleChange('countryCode', e.target.value)}
              >
                <option value="">{t('profile.countryPlaceholder')}</option>
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
              <span className={labelClass}>{t('profile.timeZone')}</span>
              <select
                className={inputClass}
                value={form.timeZone}
                onChange={(e) => handleChange('timeZone', e.target.value)}
              >
                <option value="">{t('profile.timeZonePlaceholder')}</option>
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
              <span className={labelClass}>{t('profile.dateOfBirth')}</span>
              <input
                type="date"
                className={inputClass}
                value={form.dateOfBirth}
                onChange={(e) => handleChange('dateOfBirth', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{t('profile.occupation')}</span>
              <input
                className={inputClass}
                placeholder={t('profile.occupationPlaceholder')}
                value={form.occupation}
                onChange={(e) => handleChange('occupation', e.target.value)}
              />
            </label>
            <label>
              <span className={labelClass}>{t('profile.company')}</span>
              {user?.tenantName ? (
                <input
                  className={`${inputClass} cursor-not-allowed bg-slate-50 text-slate-500 dark:bg-white/[0.02] dark:text-white/40`}
                  value={user.tenantName}
                  readOnly
                  disabled
                  title={t('profile.companyReadOnlyHint')}
                />
              ) : (
                <input
                  className={inputClass}
                  placeholder={t('profile.companyPlaceholder')}
                  value={form.company}
                  onChange={(e) => handleChange('company', e.target.value)}
                />
              )}
            </label>
          </div>

          <label className="block">
            <span className={labelClass}>{t('profile.biography')}</span>
            <textarea
              className={`${inputClass} min-h-[100px] resize-y`}
              placeholder={t('profile.biographyPlaceholder')}
              value={form.biography}
              onChange={(e) => handleChange('biography', e.target.value)}
            />
          </label>

          {saveMutation.isError && <p className="text-sm text-red-600 dark:text-red-400">{t('profile.saveError')}</p>}

          <div className="flex justify-end">
            <button
              type="submit"
              disabled={saveMutation.isPending}
              className="rounded-full bg-brand-600 px-6 py-2.5 text-sm font-semibold text-white transition hover:bg-brand-700 disabled:opacity-60"
            >
              {saveMutation.isPending ? t('profile.saving') : t('profile.saveChanges')}
            </button>
          </div>
        </form>
      </Card>

      <Card className="p-6 sm:p-8">
        <div className="mb-6 flex items-center gap-3">
          <BriefcaseBusiness className="h-5 w-5 text-brand-600 dark:text-brand-400" />
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">{t('profile.resumeTitle')}</h2>
        </div>

        {resumeQuery.isLoading && <p className="text-sm text-slate-500 dark:text-white/50">{t('profile.resumeLoading')}</p>}
        {resumeQuery.isError && <p className="text-sm text-red-600 dark:text-red-400">{t('profile.resumeError')}</p>}
        {resumeQuery.isSuccess && (
          <div className="space-y-7">
            {resumeQuery.data.professionalObjective && (
              <div>
                <h3 className="text-sm font-semibold text-slate-900 dark:text-white">{t('profile.professionalObjective')}</h3>
                <p className="mt-2 whitespace-pre-line text-sm leading-6 text-slate-600 dark:text-white/70">{resumeQuery.data.professionalObjective}</p>
              </div>
            )}
            {resumeQuery.data.professionalSummary && (
              <div>
                <h3 className="text-sm font-semibold text-slate-900 dark:text-white">{t('profile.professionalSummary')}</h3>
                <p className="mt-2 whitespace-pre-line text-sm leading-6 text-slate-600 dark:text-white/70">{resumeQuery.data.professionalSummary}</p>
              </div>
            )}
            {([
              ['skills', resumeQuery.data.skills, BriefcaseBusiness],
              ['workExperience', resumeQuery.data.workExperience, BriefcaseBusiness],
              ['education', resumeQuery.data.education, GraduationCap],
              ['certifications', resumeQuery.data.certifications, Award],
              ['languages', resumeQuery.data.languages, Languages],
            ] as const).map(([key, values, Icon]) => values.length > 0 && (
              <div key={key}>
                <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-900 dark:text-white">
                  <Icon className="h-4 w-4 text-brand-600 dark:text-brand-400" />
                  {t(`profile.${key}`)}
                </h3>
                <ul className="mt-2 space-y-2 text-sm text-slate-600 dark:text-white/70">
                  {values.map((value, index) => <li key={`${key}-${index}`} className="whitespace-pre-line">{value}</li>)}
                </ul>
              </div>
            ))}
            {!resumeQuery.data.professionalObjective && !resumeQuery.data.professionalSummary &&
              !resumeQuery.data.skills.length && !resumeQuery.data.workExperience.length &&
              !resumeQuery.data.education.length && !resumeQuery.data.certifications.length &&
              !resumeQuery.data.languages.length && (
                <p className="text-sm text-slate-500 dark:text-white/50">{t('profile.resumeEmpty')}</p>
              )}
          </div>
        )}
      </Card>
    </div>
  );
}
