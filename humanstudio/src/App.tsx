import { AppRoutes } from "./routes";
import { I18nProvider } from "./i18n";
import "./index.css";

function App() {
  return (
    <I18nProvider>
      <AppRoutes />
    </I18nProvider>
  );
}

export default App;
