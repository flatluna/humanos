import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { MsalProvider } from '@azure/msal-react';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { msalInstance } from '@/auth';
import { AuthProvider } from '@/auth/AuthContext';
import { queryClient } from '@/api';
import { ThemeProvider } from '@/components/theme/ThemeProvider';
import App from '@/app/App';
import '@/index.css';
import './localization';

// The HumanOS for Companies landing page (5181) sends admins here with
// ?mode=organization&companyName=...&companyEmail=... so the signup form
// starts pre-filled. MSAL's loginRedirect bounces the browser away and
// back to this exact origin/path with no query string, so this must be
// captured into sessionStorage BEFORE any MSAL redirect can fire (see
// AuthenticatedAppLayout's auto-login effect) — OnboardingPage reads it
// back from sessionStorage instead of the (by-then-gone) URL query.
const pendingOnboardingParams = new URLSearchParams(window.location.search);
if (pendingOnboardingParams.get('mode') === 'organization') {
  sessionStorage.setItem(
    'humanos.pendingOnboarding',
    JSON.stringify({
      mode: 'organization',
      companyName: pendingOnboardingParams.get('companyName') ?? '',
      companyEmail: pendingOnboardingParams.get('companyEmail') ?? '',
    }),
  );
  window.history.replaceState(null, '', window.location.pathname);
}

const root = document.getElementById('root');

if (!root) {
  throw new Error('Root element not found');
}

createRoot(root).render(
  <StrictMode>
    <ThemeProvider>
      <MsalProvider instance={msalInstance}>
        <AuthProvider>
          <QueryClientProvider client={queryClient}>
            <App />
            <ReactQueryDevtools initialIsOpen={false} />
          </QueryClientProvider>
        </AuthProvider>
      </MsalProvider>
    </ThemeProvider>
  </StrictMode>,
);
