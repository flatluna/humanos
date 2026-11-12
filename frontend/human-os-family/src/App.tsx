import { RouterProvider } from 'react-router';
import { router } from './routes';
import { LanguageProvider } from './context/LanguageContext';
import { ThemeProvider } from './context/ThemeContext';
import { FamilyProvider } from './context/FamilyContext';
import { AuthGate } from './auth/AuthGate';

export function App() {
  return (
    <AuthGate>
      <ThemeProvider>
        <LanguageProvider>
          <FamilyProvider>
            <RouterProvider router={router} />
          </FamilyProvider>
        </LanguageProvider>
      </ThemeProvider>
    </AuthGate>
  );
}

export default App;
