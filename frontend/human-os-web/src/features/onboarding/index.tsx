import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';

type OnboardingMode = 'individual' | 'organization';

interface OnboardingFormState {
  adminFirstName: string;
  adminLastName: string;
  companyName: string;
  companyAddress: string;
  companyEmail: string;
  companyPhone: string;
}

const EMPTY_FORM: OnboardingFormState = {
  adminFirstName: '',
  adminLastName: '',
  companyName: '',
  companyAddress: '',
  companyEmail: '',
  companyPhone: '',
};

/** Onboarding shown once after a real MSAL sign-in when no Human OS
 *  Person exists yet for this Azure identity (see AuthContext). Two
 *  paths: an individual learner (Person only, no Tenant) or an
 *  organización admin (Tenant + Person, existing flow). Email and oid
 *  always come from the already-authenticated MSAL session, never
 *  re-entered or invented. The student/admin also picks their preferred
 *  UI language here (defaulting to whatever the app is currently
 *  showing, i.e. browser/localStorage-detected) — that choice is saved
 *  to PersonProfile.PreferredLanguage and becomes the account's default
 *  language on every future login (see AuthContext.loadUser).
 */
export function OnboardingPage() {
  const { user, refreshUser } = useAuth();
  const { t, i18n } = useTranslation();
  // The HumanOS for Companies landing page (5181) sends admins here via
  // ?mode=organization&companyName=...&companyEmail=..., captured into
  // sessionStorage in main.tsx before MSAL's login redirect could wipe
  // the URL query string — read it once here to pre-fill the form.
  const pendingOnboarding = (() => {
    try {
      const raw = sessionStorage.getItem('humanos.pendingOnboarding');
      return raw ? (JSON.parse(raw) as { mode?: string; companyName?: string; companyEmail?: string }) : null;
    } catch {
      return null;
    }
  })();
  const [mode, setMode] = useState<OnboardingMode>(pendingOnboarding?.mode === 'organization' ? 'organization' : 'individual');
  const [form, setForm] = useState<OnboardingFormState>({
    ...EMPTY_FORM,
    companyName: pendingOnboarding?.companyName ?? '',
    companyEmail: pendingOnboarding?.companyEmail ?? '',
  });
  const [preferredLanguage, setPreferredLanguage] = useState<'en' | 'es'>(
    i18n.language.startsWith('es') ? 'es' : 'en',
  );
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState(false);

  // Consume the pending-onboarding hint once so it doesn't leak into a
  // later, unrelated individual signup in the same browser session.
  useEffect(() => {
    sessionStorage.removeItem('humanos.pendingOnboarding');
  }, []);

  function update<K extends keyof OnboardingFormState>(key: K, value: OnboardingFormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  function selectLanguage(lang: 'en' | 'es') {
    setPreferredLanguage(lang);
    void i18n.changeLanguage(lang);
  }

  const isValid =
    mode === 'individual'
      ? Boolean(form.adminFirstName.trim() && form.adminLastName.trim())
      : Boolean(form.adminFirstName.trim() && form.adminLastName.trim() && form.companyName.trim() && form.companyEmail.trim());

  async function handleSubmit() {
    if (!user?.oid || !isValid) return;

    setIsSaving(true);
    setError(false);

    try {
      const response = await fetch(mode === 'individual' ? '/api/onboarding/individual' : '/api/onboarding', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Azure-OID': user.oid,
          'X-Azure-TID': user.tid,
        },
        body: JSON.stringify(
          mode === 'individual'
            ? {
                email: user.email,
                firstName: form.adminFirstName.trim(),
                lastName: form.adminLastName.trim(),
                preferredLanguage,
              }
            : {
                email: user.email,
                adminFirstName: form.adminFirstName.trim(),
                adminLastName: form.adminLastName.trim(),
                companyName: form.companyName.trim(),
                companyAddress: form.companyAddress.trim() || null,
                companyEmail: form.companyEmail.trim(),
                companyPhone: form.companyPhone.trim() || null,
                preferredLanguage,
              },
        ),
      });

      // 409 means a Person already exists for this Azure identity (e.g. a
      // prior submit succeeded but the app didn't move on yet) — treat it
      // as success rather than an error, and just reload the real user.
      if (!response.ok && response.status !== 409) throw new Error('onboarding failed');

      // A company admin belongs in Human OS Enterprise, not this
      // individual-learner app — send them there instead of staying here.
      if (mode === 'organization') {
        window.location.assign(import.meta.env.VITE_ENTERPRISE_URL ?? 'http://localhost:5179');
        return;
      }

      await refreshUser();
    } catch {
      setError(true);
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-10 dark:bg-[#05060a]">
      <Card className="w-full max-w-lg p-6 sm:p-8">
        <div className="flex items-center justify-between gap-3">
          <p className="text-xs font-semibold uppercase tracking-widest text-blue-600 dark:text-blue-300">
            {t('onboarding.eyebrow')}
          </p>
          <div className="flex items-center rounded-full border border-slate-200 p-0.5 text-xs font-semibold dark:border-white/10">
            <button
              type="button"
              onClick={() => selectLanguage('en')}
              aria-pressed={preferredLanguage === 'en'}
              className={
                preferredLanguage === 'en'
                  ? 'rounded-full bg-slate-900 px-2.5 py-1 text-white dark:bg-white dark:text-slate-900'
                  : 'rounded-full px-2.5 py-1 text-slate-400 hover:text-slate-700 dark:text-white/40 dark:hover:text-white'
              }
            >
              EN
            </button>
            <button
              type="button"
              onClick={() => selectLanguage('es')}
              aria-pressed={preferredLanguage === 'es'}
              className={
                preferredLanguage === 'es'
                  ? 'rounded-full bg-slate-900 px-2.5 py-1 text-white dark:bg-white dark:text-slate-900'
                  : 'rounded-full px-2.5 py-1 text-slate-400 hover:text-slate-700 dark:text-white/40 dark:hover:text-white'
              }
            >
              ES
            </button>
          </div>
        </div>
        <h1 className="mt-1 text-2xl font-semibold text-slate-900 dark:text-white">
          {mode === 'individual' ? t('onboarding.titleIndividual') : t('onboarding.titleOrganization')}
        </h1>
        <p className="mt-2 text-sm text-slate-500 dark:text-white/50">
          {mode === 'individual' ? t('onboarding.subtitleIndividual') : t('onboarding.subtitleOrganization')}
        </p>

        <div className="mt-5 grid grid-cols-2 gap-2 rounded-full bg-slate-100 p-1 dark:bg-white/5">
          <button
            type="button"
            onClick={() => setMode('individual')}
            className={`rounded-full px-4 py-2 text-sm font-medium transition ${
              mode === 'individual'
                ? 'bg-white text-slate-900 shadow-sm dark:bg-white/10 dark:text-white'
                : 'text-slate-500 dark:text-white/50'
            }`}
          >
            {t('onboarding.modeIndividual')}
          </button>
          <button
            type="button"
            onClick={() => setMode('organization')}
            className={`rounded-full px-4 py-2 text-sm font-medium transition ${
              mode === 'organization'
                ? 'bg-white text-slate-900 shadow-sm dark:bg-white/10 dark:text-white'
                : 'text-slate-500 dark:text-white/50'
            }`}
          >
            {t('onboarding.modeOrganization')}
          </button>
        </div>

        <div className="mt-6 space-y-5">
          <section>
            <h2 className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
              {t('onboarding.yourInfo')}
            </h2>
            <div className="mt-3 grid grid-cols-1 gap-4 sm:grid-cols-2">
              <label className="block">
                <span className="text-sm font-medium text-slate-700 dark:text-white/80">{t('onboarding.firstName')}</span>
                <input
                  type="text"
                  value={form.adminFirstName}
                  onChange={(event) => update('adminFirstName', event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </label>
              <label className="block">
                <span className="text-sm font-medium text-slate-700 dark:text-white/80">{t('onboarding.lastName')}</span>
                <input
                  type="text"
                  value={form.adminLastName}
                  onChange={(event) => update('adminLastName', event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </label>
            </div>
            <p className="mt-2 text-xs text-slate-400 dark:text-white/40">
              {t('onboarding.emailLabel')}: {user?.email}
            </p>
          </section>

          {mode === 'organization' && (
          <section>
            <h2 className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
              {t('onboarding.yourCompany')}
            </h2>
            <div className="mt-3 space-y-4">
              <label className="block">
                <span className="text-sm font-medium text-slate-700 dark:text-white/80">{t('onboarding.companyName')}</span>
                <input
                  type="text"
                  value={form.companyName}
                  onChange={(event) => update('companyName', event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </label>
              <label className="block">
                <span className="text-sm font-medium text-slate-700 dark:text-white/80">{t('onboarding.companyAddress')}</span>
                <input
                  type="text"
                  value={form.companyAddress}
                  onChange={(event) => update('companyAddress', event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </label>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <label className="block">
                  <span className="text-sm font-medium text-slate-700 dark:text-white/80">{t('onboarding.companyEmail')}</span>
                  <input
                    type="email"
                    value={form.companyEmail}
                    onChange={(event) => update('companyEmail', event.target.value)}
                    className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                  />
                </label>
                <label className="block">
                  <span className="text-sm font-medium text-slate-700 dark:text-white/80">{t('onboarding.companyPhone')}</span>
                  <input
                    type="tel"
                    value={form.companyPhone}
                    onChange={(event) => update('companyPhone', event.target.value)}
                    className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                  />
                </label>
              </div>
            </div>
          </section>
          )}

          {error && (
            <p className="text-sm text-amber-600 dark:text-amber-400">
              {t('onboarding.errorMessage')}
            </p>
          )}

          <button
            type="button"
            onClick={handleSubmit}
            disabled={!isValid || isSaving}
            className="inline-flex min-h-11 w-full items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            {isSaving ? t('onboarding.creating') : mode === 'individual' ? t('onboarding.createAccount') : t('onboarding.createOrganization')}
          </button>
        </div>
      </Card>
    </div>
  );
}

