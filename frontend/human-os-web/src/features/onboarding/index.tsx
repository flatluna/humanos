import { useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';

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

/** Company + admin onboarding, shown once after a real MSAL sign-in when
 *  no Human OS Tenant/Person exists yet for this Azure identity (see
 *  AuthContext). The admin's real name/company details are the only
 *  thing this form collects — email and oid always come from the
 *  already-authenticated MSAL session, never re-entered or invented.
 */
export function OnboardingPage() {
  const { user, refreshUser } = useAuth();
  const [form, setForm] = useState<OnboardingFormState>(EMPTY_FORM);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState(false);

  function update<K extends keyof OnboardingFormState>(key: K, value: OnboardingFormState[K]) {
    setForm((current) => ({ ...current, [key]: value }));
  }

  const isValid =
    form.adminFirstName.trim() && form.adminLastName.trim() && form.companyName.trim() && form.companyEmail.trim();

  async function handleSubmit() {
    if (!user?.oid || !isValid) return;

    setIsSaving(true);
    setError(false);

    try {
      const response = await fetch('/api/onboarding', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Azure-OID': user.oid,
          'X-Azure-TID': user.tid,
        },
        body: JSON.stringify({
          email: user.email,
          adminFirstName: form.adminFirstName.trim(),
          adminLastName: form.adminLastName.trim(),
          companyName: form.companyName.trim(),
          companyAddress: form.companyAddress.trim() || null,
          companyEmail: form.companyEmail.trim(),
          companyPhone: form.companyPhone.trim() || null,
        }),
      });

      if (!response.ok) throw new Error('onboarding failed');

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
          Bienvenido a Human OS
        </p>
        <h1 className="mt-1 text-2xl font-semibold text-slate-900 dark:text-white">
          Creemos la cuenta de tu organización
        </h1>
        <p className="mt-2 text-sm text-slate-500 dark:text-white/50">
          Solo unos datos para crear tu empresa en Human OS y tu perfil de administrador.
        </p>

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
            {isSaving ? 'Creando...' : 'Crear mi organización'}
          </button>
        </div>
      </Card>
    </div>
  );
}

