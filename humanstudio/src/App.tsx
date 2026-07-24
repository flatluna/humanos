import { AppRoutes } from "./routes";
import { I18nProvider } from "./i18n";
import { ThemeProvider } from "./contexts/ThemeContext";
import "./index.css";

function App() {
  return (
    <ThemeProvider>
      <I18nProvider>
        <AppRoutes />
      </I18nProvider>
    </ThemeProvider>
  );
}

export default App;
