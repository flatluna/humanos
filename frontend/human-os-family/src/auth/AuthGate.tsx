import { Heart, LogIn } from 'lucide-react';
import { useAuth } from './AuthContext';

export function AuthGate({ children }: { children: React.ReactNode }) {
  const { account, isLoading, login, error } = useAuth();

  if (isLoading) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-emerald-50 via-white to-teal-50 px-6 dark:from-[#0b0f17] dark:via-[#0b0f17] dark:to-[#0b0f17]">
        <section className="w-full max-w-sm rounded-2xl border border-warm-200 bg-white/90 p-8 text-center shadow-xl dark:border-white/10 dark:bg-[#111827]" aria-live="polite">
          <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-emerald-600 to-teal-700 text-lg font-bold text-white shadow-md shadow-emerald-600/30">
            <Heart className="h-5 w-5 fill-white" />
          </div>
          <p className="text-xs font-semibold uppercase tracking-wide text-emerald-600 dark:text-emerald-400">EngramsIQ Family</p>
          <h1 className="mt-1 text-lg font-bold text-warm-900 dark:text-white">Conectando con Microsoft…</h1>
          <p className="mt-2 text-sm text-warm-500 dark:text-warm-400">Estamos verificando tu sesión familiar.</p>
        </section>
      </main>
    );
  }

  if (!account) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-gradient-to-br from-emerald-50 via-white to-teal-50 px-6 dark:from-[#0b0f17] dark:via-[#0b0f17] dark:to-[#0b0f17]">
        <section className="w-full max-w-sm rounded-2xl border border-warm-200 bg-white/90 p-8 text-center shadow-xl dark:border-white/10 dark:bg-[#111827]">
          <div className="mx-auto mb-4 flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-emerald-600 to-teal-700 text-lg font-bold text-white shadow-md shadow-emerald-600/30">
            <Heart className="h-5 w-5 fill-white" />
          </div>
          <p className="text-xs font-semibold uppercase tracking-wide text-emerald-600 dark:text-emerald-400">EngramsIQ Family</p>
          <h1 className="mt-1 text-lg font-bold text-warm-900 dark:text-white">Inicia sesión</h1>
          <p className="mt-2 text-sm text-warm-500 dark:text-warm-400">
            Accede a Armonía &amp; Crecimiento Familiar con tu cuenta de EngramsIQ.
          </p>
          <button
            type="button"
            onClick={() => void login()}
            className="mt-6 inline-flex w-full items-center justify-center gap-2 rounded-full bg-gradient-to-r from-emerald-600 via-teal-600 to-indigo-600 px-4 py-2.5 text-sm font-semibold text-white shadow-md shadow-emerald-600/20 transition hover:opacity-95 hover:shadow-lg"
          >
            <LogIn size={17} />
            Continuar con Microsoft
          </button>
          {error && (
            <p className="mt-3 text-xs text-rose-600 dark:text-rose-400" role="alert">
              {error}
            </p>
          )}
        </section>
      </main>
    );
  }

  return children;
}
