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
