// Ported verbatim from capabilitystudio (2026-07-21 design-parity pass) —
// see capabilitystudio/src/lib/theme.ts. Only the localStorage key changed
// (own namespace, not shared with the Studio app).
export type ThemeName = 'dark' | 'light' | 'blue' | 'azure';

export const THEMES: { id: ThemeName; label: string }[] = [
  { id: 'dark', label: 'Oscuro' },
  { id: 'light', label: 'Claro' },
  { id: 'blue', label: 'Azul' },
  { id: 'azure', label: 'Azure' },
];

const STORAGE_KEY = 'human-learn-theme';
const DEFAULT_THEME: ThemeName = 'dark';

function isThemeName(value: string | null): value is ThemeName {
  return value === 'dark' || value === 'light' || value === 'blue' || value === 'azure';
}

export function getStoredTheme(): ThemeName {
  if (typeof window === 'undefined') return DEFAULT_THEME;
  const stored = window.localStorage.getItem(STORAGE_KEY);
  return isThemeName(stored) ? stored : DEFAULT_THEME;
}

export function applyTheme(theme: ThemeName) {
  document.documentElement.setAttribute('data-theme', theme);
  window.localStorage.setItem(STORAGE_KEY, theme);
}
