import { AppRoutes } from './routes';
import { I18nProvider } from './i18n';

function App() {
  return (
    <I18nProvider>
      <AppRoutes />
    </I18nProvider>
  );
}

export default App;
