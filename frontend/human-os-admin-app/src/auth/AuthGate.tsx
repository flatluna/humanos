import { LogIn } from 'lucide-react';
import { useAuth } from './AuthContext';

export function AuthGate({ children }: { children: React.ReactNode }) {
  const { account, isLoading, login, error } = useAuth();

  if (isLoading) {
    return (
      <main className="auth-screen">
        <section className="auth-card" aria-live="polite">
          <div className="brand-mark">H</div>
          <p className="eyebrow">HumanOS Admin</p>
          <h1>Conectando con Microsoft</h1>
          <p>Estamos verificando tu sesión.</p>
        </section>
      </main>
    );
  }

  if (!account) {
    return (
      <main className="auth-screen">
        <section className="auth-card">
          <div className="brand-mark">H</div>
          <p className="eyebrow">HumanOS Admin</p>
          <h1>Inicia sesión</h1>
          <p>Accede a la consola administrativa con tu cuenta de HumanOS.</p>
          <button className="auth-login-button" type="button" onClick={() => void login()}>
            <LogIn size={17} />
            Continuar con Microsoft
          </button>
          {error && <p className="auth-error" role="alert">{error}</p>}
        </section>
      </main>
    );
  }

  return children;
}
