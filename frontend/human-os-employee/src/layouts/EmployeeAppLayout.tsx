import { useEffect, useRef, useState } from 'react';
import { Outlet, useBlocker, useLocation, useNavigate } from 'react-router';
import { InteractionStatus } from '@azure/msal-browser';
import { useMsal } from '@azure/msal-react';
import { LogOut } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { EmployeeHeader } from '@/components/layout/EmployeeHeader';
import { EmployeeSidebar } from '@/components/navigation/EmployeeSidebar';
import { FloatingBuddyButton } from '@/components/layout/FloatingBuddyButton';
import { HumanOsAgentPanel } from '@/components/layout/HumanOsAgentPanel';
import { GrowWithUsPromptModal } from '@/features/advance-your-knowledge/GrowWithUsPromptModal';
import { AccessDenied } from '@/components/AccessDenied';
import { useCourseFocusModeStore } from '@/store/useCourseFocusModeStore';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { closeStructuredLearningEngramSession } from '@/features/capabilities/api/runtimeSessionApi';

const REDIRECT_GUARD_KEY = 'humanos.employee.loginRedirect.startedAt';
const REDIRECT_GUARD_MAX_AGE_MS = 30_000;

function ScrollToTop() {
  const { pathname } = useLocation();

  useEffect(() => {
    window.scrollTo({ top: 0, left: 0, behavior: 'auto' });
  }, [pathname]);

  return null;
}

/** Blocks ALL navigation (Link clicks, programmatic navigate, AND browser
 * back/forward) while a course focus-mode session is active (2026-10-02) —
 * the only way out is the explicit "Exit course" button, which clears
 * focus mode first so this same blocker lets that navigation through.
 * react-router's data-router useBlocker intercepts POP (back/forward)
 * navigations too, not just in-app Link clicks. Also warns on tab close/
 * refresh via beforeunload (can't be fully prevented, only warned). */
function CourseFocusGuard() {
  const active = useCourseFocusModeStore((state) => state.active);
  // Only blocks navigation OUT of the structured-learning course tree —
  // moving between the course map and its own engrams stays unblocked, so
  // "Back to course map"/section-to-section links keep working normally.
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      active && currentLocation.pathname !== nextLocation.pathname && !nextLocation.pathname.startsWith('/structured-learning/')
  );

  useEffect(() => {
    if (blocker.state === 'blocked') {
      blocker.reset();
    }
  }, [blocker]);

  useEffect(() => {
    if (!active) return undefined;
    const handler = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', handler);
    return () => window.removeEventListener('beforeunload', handler);
  }, [active]);

  return null;
}

/** Prominent, always-reachable way to leave a course focus-mode session
 * (2026-10-02) — closes the real course session on the backend (sets
 * CompletedDate + total DurationSeconds), clears the cached session so the
 * NEXT entry counts as a real new visit (not a reused/stale one), then
 * clears focus mode (letting CourseFocusGuard's blocker stand down) and
 * navigates to the course catalog. */
function ExitCourseBar() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const navigate = useNavigate();
  const location = useLocation();
  const exit = useCourseFocusModeStore((state) => state.exit);
  const [confirming, setConfirming] = useState(false);

  const confirmExit = () => {
    setConfirming(false);
    const match = location.pathname.match(/^\/structured-learning\/([^/]+)/);
    const documentId = match?.[1];
    if (documentId) {
      const storageKey = `structuredLearningCourseSession:${documentId}`;
      const cached = sessionStorage.getItem(storageKey);
      if (cached) {
        const { EngramSessionId } = JSON.parse(cached) as { EngramSessionId: string };
        closeStructuredLearningEngramSession(EngramSessionId).catch(() => undefined);
      }
      sessionStorage.removeItem(storageKey);
    }

    exit();
    // Deferred one tick (2026-10-02 fix): calling navigate() in the SAME
    // synchronous handler as exit() races CourseFocusGuard's blocker,
    // which still closes over the pre-update `active=true` until this
    // component re-renders — the navigation attempt was getting blocked
    // by the very state change meant to release it.
    setTimeout(() => navigate('/available-courses'), 0);
  };

  return (
    <div className="flex items-center justify-between border-b border-amber-300 bg-amber-50 px-4 py-2.5 sm:px-6 dark:border-amber-500/30 dark:bg-amber-500/10">
      <div className="flex items-center gap-2">
        <span className="font-black text-base tracking-tight text-amber-950 dark:text-amber-100">
          Engrams<span className="text-amber-600 dark:text-amber-300">IQ</span>
        </span>
        <span className="text-xs font-semibold text-amber-800 dark:text-amber-200">
          {isEn ? 'Focused course session — navigation is locked' : 'Sesión de curso enfocada — la navegación está bloqueada'}
        </span>
      </div>
      <button
        type="button"
        onClick={() => setConfirming(true)}
        className="inline-flex items-center gap-1.5 rounded-full bg-rose-600 px-4 py-2 text-sm font-bold text-white shadow-md shadow-rose-600/30 transition hover:bg-rose-700"
      >
        <LogOut className="h-4 w-4" />
        {isEn ? 'Exit course' : 'Salir del curso'}
      </button>

      {confirming && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm">
          <div className="w-full max-w-sm rounded-2xl border border-rose-200 bg-white p-6 text-center shadow-2xl dark:border-rose-500/20 dark:bg-slate-900">
            <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-full bg-rose-100 text-rose-600 dark:bg-rose-500/15 dark:text-rose-300">
              <LogOut className="h-5 w-5" />
            </div>
            <h2 className="mt-3 text-base font-bold text-slate-900 dark:text-white">
              {isEn ? 'Are you sure you want to exit?' : '¿Seguro que quieres salir?'}
            </h2>
            <p className="mt-1.5 text-sm text-slate-600 dark:text-slate-400">
              {isEn
                ? 'This will end your session now — the total time and progress for this visit will be saved.'
                : 'Esto terminará tu sesión ahora — se guardará el tiempo total y el progreso de esta visita.'}
            </p>
            <div className="mt-5 flex gap-2">
              <button
                type="button"
                onClick={() => setConfirming(false)}
                className="flex-1 rounded-xl border border-slate-200 px-4 py-2.5 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 dark:border-white/10 dark:text-slate-200 dark:hover:bg-white/5"
              >
                {isEn ? 'Cancel' : 'Cancelar'}
              </button>
              <button
                type="button"
                onClick={confirmExit}
                className="flex-1 rounded-xl bg-rose-600 px-4 py-2.5 text-sm font-bold text-white shadow-md shadow-rose-600/30 transition hover:bg-rose-700"
              >
                {isEn ? 'Yes, exit' : 'Sí, salir'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/** Shell for the whole Employee Portal: real MSAL session gate, then a
 *  "belongs to a company tenant" gate (same as human-os-enterprise), but
 *  deliberately NO OrgRole gate — every employee, manager, and HRAdmin can
 *  use this app to take their own assigned courses. This is intentionally
 *  a separate app/shell from both human-os-web (consumer/independent
 *  students) and human-os-enterprise (HR admin console). */
export function EmployeeAppLayout() {
  const { isAuthenticated, isLoading, backendError, notEnterpriseAccount, user, login, refreshUser } = useAuth();
  const { inProgress } = useMsal();
  const focusModeActive = useCourseFocusModeStore((state) => state.active);
  const redirectStarted = useRef(false);

  useEffect(() => {
    if (isAuthenticated) {
      sessionStorage.removeItem(REDIRECT_GUARD_KEY);
      redirectStarted.current = false;
      return;
    }

    if (isLoading || inProgress !== InteractionStatus.None || redirectStarted.current) {
      return;
    }

    const now = Date.now();
    const startedAtRaw = sessionStorage.getItem(REDIRECT_GUARD_KEY);
    const startedAt = startedAtRaw ? Number.parseInt(startedAtRaw, 10) : 0;
    const guardActive = Number.isFinite(startedAt) && now - startedAt < REDIRECT_GUARD_MAX_AGE_MS;

    if (guardActive) {
      return;
    }

    sessionStorage.setItem(REDIRECT_GUARD_KEY, now.toString());
    redirectStarted.current = true;
    void login().catch(() => {
      sessionStorage.removeItem(REDIRECT_GUARD_KEY);
      redirectStarted.current = false;
    });
  }, [inProgress, isAuthenticated, isLoading, login]);

  if (!isAuthenticated || isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-50 dark:bg-[#05060a]">
        <div className="h-8 w-8 animate-spin rounded-full border-2 border-slate-300 border-t-brand-600 dark:border-white/20 dark:border-t-brand-400" />
      </div>
    );
  }

  if (backendError) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-50 px-4 text-center dark:bg-[#05060a]">
        <p className="text-lg font-semibold text-slate-900 dark:text-white">No pudimos conectar con el servidor</p>
        <p className="max-w-md text-sm text-slate-500 dark:text-white/50">
          Tu sesión es válida, pero el backend no respondió correctamente. Verifica tu conexión e inténtalo de nuevo.
        </p>
        <button
          type="button"
          onClick={() => void refreshUser()}
          className="inline-flex min-h-11 items-center justify-center rounded-full bg-slate-900 px-6 py-2.5 text-sm font-medium text-white transition hover:bg-slate-700 dark:bg-white dark:text-slate-900 dark:hover:bg-white/90"
        >
          Reintentar
        </button>
      </div>
    );
  }

  if (notEnterpriseAccount || !user) {
    return <AccessDenied reason="no-enterprise-account" />;
  }

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-[#05060a]">
      <ScrollToTop />
      <CourseFocusGuard />

      {/* <main><Outlet/></main> MUST stay at the same position in the tree
          across the focusModeActive flip (2026-10-02 bugfix) — previously
          this branched into two entirely different JSX subtrees, which
          made React unmount/remount <Outlet/> (and therefore the course
          page itself) the instant focus mode turned off. That remount
          re-ran the page's own "enter focus mode" effect, flipping it
          straight back on — Exit course looked like it did nothing. Only
          the chrome AROUND <main> (header/sidebar vs exit bar) differs now. */}
      {focusModeActive ? <ExitCourseBar /> : <EmployeeHeader />}

      <div className="flex">
        {!focusModeActive && <EmployeeSidebar />}

        <main className="min-w-0 flex-1 px-6 py-8 lg:px-10">
          <Outlet />
        </main>
      </div>

      {!focusModeActive && (
        <>
          <FloatingBuddyButton />
          <HumanOsAgentPanel />
          <GrowWithUsPromptModal />
        </>
      )}
    </div>
  );
}
