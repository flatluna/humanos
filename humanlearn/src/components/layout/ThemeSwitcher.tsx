import { useEffect, useRef, useState } from 'react';
import { Palette, Check } from 'lucide-react';
import { THEMES, applyTheme, getStoredTheme, type ThemeName } from '../../lib/theme';

// Ported from capabilitystudio/src/components/ThemeSwitcher.tsx
// (2026-07-21 design-parity pass) — identical behavior/markup.
const SWATCHES: Record<ThemeName, string> = {
  dark: 'linear-gradient(135deg, #5a75ff, #ec4899)',
  light: 'linear-gradient(135deg, #eef4ff, #35a0d6)',
  blue: 'linear-gradient(135deg, #2563eb, #22d3ee)',
  azure: 'linear-gradient(135deg, #0078d4, #50e6ff)',
};

export default function ThemeSwitcher() {
  const [theme, setTheme] = useState<ThemeName>(() => getStoredTheme());
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <div className="relative" ref={containerRef}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-label="Cambiar tema"
        className="flex h-9 w-9 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-white/5 hover:text-white"
      >
        <Palette className="h-4.5 w-4.5" />
      </button>

      {open && (
        <div className="absolute right-0 top-11 z-50 w-48 rounded-xl border border-white/10 bg-slate-950 p-1.5 shadow-2xl shadow-black/40">
          {THEMES.map((t) => (
            <button
              key={t.id}
              type="button"
              onClick={() => {
                setTheme(t.id);
                setOpen(false);
              }}
              className={`flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-sm transition-colors ${
                theme === t.id ? 'bg-white/10 text-white' : 'text-slate-300 hover:bg-white/5 hover:text-white'
              }`}
            >
              <span
                className="h-4 w-4 flex-none rounded-full border border-white/20"
                style={{ background: SWATCHES[t.id] }}
              />
              <span className="flex-1">{t.label}</span>
              {theme === t.id && <Check className="h-3.5 w-3.5 flex-none" />}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
