import { useEffect, type ReactNode } from 'react';
import { useThemeStore } from '@/store/useThemeStore';

/** Syncs the persisted theme preference to the `dark` class on
 *  `<html>`, which drives Tailwind's `dark:` variant (see the
 *  `@custom-variant dark` declaration in `src/index.css`).
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const theme = useThemeStore((state) => state.theme);

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark');
  }, [theme]);

  return children;
}
