import { ShieldAlert } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';

/** Shown when the signed-in identity has no Human OS Person yet, or that
 *  Person has no Tenant (individual/consumer account) — this portal is
 *  only for employees of a company already registered in Human OS. Unlike
 *  human-os-enterprise, there is no role-based reason here: every OrgRole
 *  (Employee/Manager/HRAdmin) is allowed to take their own courses. */
export function AccessDenied({ reason }: { reason: 'no-enterprise-account' }) {
  const { logout, signedInEmail } = useAuth();
  void reason;

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gradient-to-br from-slate-50 via-white to-amber-50/40 px-4 text-center dark:from-[#05060a] dark:via-[#05060a] dark:to-amber-500/5">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-amber-500 to-amber-400 text-white shadow-lg shadow-amber-500/30">
        <ShieldAlert className="h-7 w-7" />
      </div>
      <p className="text-lg font-semibold text-slate-900 dark:text-white">Esta cuenta no pertenece a una empresa</p>
      <p className="max-w-md text-sm text-slate-500 dark:text-white/50">
        Este portal es para empleados de empresas ya registradas en Human OS. Si acabas de crear tu cuenta, completa
        el onboarding en la app principal (localhost:5178).
      </p>
      {signedInEmail && (
        <p className="text-xs text-slate-400 dark:text-white/30">
          Conectado como <span className="font-medium text-slate-500 dark:text-white/50">{signedInEmail}</span>
        </p>
      )}
      <button
        type="button"
        onClick={() => void logout()}
        className="inline-flex min-h-11 items-center justify-center rounded-full bg-gradient-to-r from-slate-900 to-slate-700 px-6 py-2.5 text-sm font-medium text-white shadow-md transition hover:brightness-110 dark:from-white dark:to-white dark:text-slate-900 dark:hover:bg-white/90"
      >
        Cerrar sesión
      </button>
    </div>
  );
}
