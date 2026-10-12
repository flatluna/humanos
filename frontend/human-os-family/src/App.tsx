import { RouterProvider } from 'react-router';
import { router } from './routes';
import { LanguageProvider } from './context/LanguageContext';
import { ThemeProvider } from './context/ThemeContext';
import { FamilyProvider } from './context/FamilyContext';

export function App() {
  return (
    <ThemeProvider>
      <LanguageProvider>
        <FamilyProvider>
          <RouterProvider router={router} />
        </FamilyProvider>
      </LanguageProvider>
    </ThemeProvider>
  );
}

export default App;
