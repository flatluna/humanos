import { ShieldAlert } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';

/** Shown when a real Human OS account exists but isn't allowed to author
 *  courses (an org Employee/Manager — only HRAdmin or an individual
 *  Consumer account can use Studio, 2026-08-05 ownership/visibility
 *  model), or when there's no Human OS account at all yet. */
export function AccessDenied({ reason }: { reason: 'role' | 'no-account' }) {
  const { logout, signedInEmail } = useAuth();

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-950 px-4 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-amber-500 to-amber-400 text-white shadow-lg shadow-amber-500/30">
        <ShieldAlert className="h-7 w-7" />
      </div>
      <p className="text-lg font-semibold text-white">
        {reason === 'role' ? 'No tienes acceso a Capability Studio' : 'Aún no tienes una cuenta de Human OS'}
      </p>
      <p className="max-w-md text-sm text-slate-400">
        {reason === 'role'
          ? 'Capability Studio es solo para cuentas individuales o para Administradores de RRHH (HRAdmin) de una empresa. Si eres empleado de una organización, pide a tu HRAdmin que cree el curso.'
          : 'Crea tu cuenta primero en la app principal de Human OS (localhost:5178) y vuelve a intentarlo.'}
      </p>
      {signedInEmail && (
        <p className="text-xs text-slate-500">
          Conectado como <span className="font-medium text-slate-300">{signedInEmail}</span>
        </p>
      )}
      <button
        type="button"
        onClick={() => void logout()}
        className="inline-flex min-h-11 items-center justify-center rounded-full bg-gradient-to-r from-brand-500 to-accent-500 px-6 py-2.5 text-sm font-medium text-white shadow-md transition hover:brightness-110"
      >
        Cerrar sesión
      </button>
    </div>
  );
}
