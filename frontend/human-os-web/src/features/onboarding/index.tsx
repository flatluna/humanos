import { useState } from 'react';
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
 *  re-entered or invented.
 */
export function OnboardingPage() {
  const { user, refreshUser } = useAuth();
  const [mode, setMode] = useState<OnboardingMode>('individual');
  const [form, setForm] = useState<OnboardingFormState>(EMPTY_FORM);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState(false);

  function update<K extends keyof OnboardingFormState>(key: K, value: OnboardingFormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
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
              }
            : {
                email: user.email,
                adminFirstName: form.adminFirstName.trim(),
                adminLastName: form.adminLastName.trim(),
                companyName: form.companyName.trim(),
                companyAddress: form.companyAddress.trim() || null,
                companyEmail: form.companyEmail.trim(),
                companyPhone: form.companyPhone.trim() || null,
              },
        ),
      });

      // 409 means a Person already exists for this Azure identity (e.g. a
      // prior submit succeeded but the app didn't move on yet) — treat it
      // as success rather than an error, and just reload the real user.
      if (!response.ok && response.status !== 409) throw new Error('onboarding failed');

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
        <p className="text-xs font-semibold uppercase tracking-widest text-blue-600 dark:text-blue-300">
          Bienvenido a Engram Academy
        </p>
        <h1 className="mt-1 text-2xl font-semibold text-slate-900 dark:text-white">
          {mode === 'individual' ? 'Creemos tu cuenta' : 'Creemos la cuenta de tu organización'}
        </h1>
        <p className="mt-2 text-sm text-slate-500 dark:text-white/50">
          {mode === 'individual'
            ? 'Solo tu nombre — tu cuenta personal en Engram Academy queda lista al instante.'
            : 'Solo unos datos para crear tu empresa en Engram Academy y tu perfil de administrador.'}
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
            Soy un individuo
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
            Soy una organización
          </button>
        </div>

        <div className="mt-6 space-y-5">
          <section>
            <h2 className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
              Tu información
            </h2>
            <div className="mt-3 grid grid-cols-1 gap-4 sm:grid-cols-2">
              <label className="block">
                <span className="text-sm font-medium text-slate-700 dark:text-white/80">Nombre</span>
                <input
                  type="text"
                  value={form.adminFirstName}
                  onChange={(event) => update('adminFirstName', event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </label>
              <label className="block">
                <span className="text-sm font-medium text-slate-700 dark:text-white/80">Apellido</span>
                <input
                  type="text"
                  value={form.adminLastName}
                  onChange={(event) => update('adminLastName', event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </label>
            </div>
            <p className="mt-2 text-xs text-slate-400 dark:text-white/40">Correo: {user?.email}</p>
          </section>

          {mode === 'organization' && (
          <section>
            <h2 className="text-xs font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
              Tu empresa
            </h2>
            <div className="mt-3 space-y-4">
              <label className="block">
                <span className="text-sm font-medium text-slate-700 dark:text-white/80">Nombre de la empresa</span>
                <input
                  type="text"
                  value={form.companyName}
                  onChange={(event) => update('companyName', event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </label>
              <label className="block">
                <span className="text-sm font-medium text-slate-700 dark:text-white/80">Dirección</span>
                <input
                  type="text"
                  value={form.companyAddress}
                  onChange={(event) => update('companyAddress', event.target.value)}
                  className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                />
              </label>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <label className="block">
                  <span className="text-sm font-medium text-slate-700 dark:text-white/80">Correo de la empresa</span>
                  <input
                    type="email"
                    value={form.companyEmail}
                    onChange={(event) => update('companyEmail', event.target.value)}
                    className="mt-1.5 w-full rounded-xl border border-slate-200 px-3 py-2.5 text-sm text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                  />
                </label>
                <label className="block">
                  <span className="text-sm font-medium text-slate-700 dark:text-white/80">Teléfono</span>
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
              No pudimos crear tu cuenta. Inténtalo de nuevo.
            </p>
          )}

          <button
            type="button"
            onClick={handleSubmit}
            disabled={!isValid || isSaving}
            className="inline-flex min-h-11 w-full items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
          >
            {isSaving ? 'Creando...' : mode === 'individual' ? 'Crear mi cuenta' : 'Crear mi organización'}
          </button>
        </div>
      </Card>
    </div>
  );
}

