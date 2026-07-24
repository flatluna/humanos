import React, { useEffect, useRef, useState } from 'react';
import { Globe, LogOut, Sparkles } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useI18n } from '../../i18n';
import type { StudentUser } from '../../types';
import ThemeSwitcher from './ThemeSwitcher';

interface TopBarProps {
  user: StudentUser;
  onSignOut?: () => void;
}

const TopBar: React.FC<TopBarProps> = ({ user, onSignOut }) => {
  const { t, language, setLanguage } = useI18n();
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const toggleLanguage = () => setLanguage(language === 'en' ? 'es' : 'en');

  return (
    <header className="sticky top-0 z-40 flex h-16 flex-none items-center border-b border-white/10 bg-slate-950/80 px-6 backdrop-blur-xl">
      <Link to="/" className="group flex items-center gap-2.5">
        <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-brand-500 to-accent-500 shadow-lg shadow-brand-500/30 transition-transform group-hover:scale-105">
          <Sparkles className="h-5 w-5 text-[#fff]" strokeWidth={2.25} />
        </div>
        <span className="text-lg font-semibold tracking-tight text-white">{t.appName}</span>
      </Link>

      <div className="ml-auto flex items-center gap-1">
        <button
          onClick={toggleLanguage}
          className="flex items-center gap-1.5 rounded-lg px-2.5 py-2 text-sm text-slate-400 transition hover:bg-white/5 hover:text-white"
          title={t.language}
          aria-label={t.language}
        >
          <Globe size={18} />
          <span className="uppercase">{language}</span>
        </button>

        <ThemeSwitcher />

        <div className="relative ml-1" ref={userMenuRef}>
          <button
            onClick={() => setIsUserMenuOpen((open) => !open)}
            className="flex items-center gap-2 rounded-lg px-2 py-1.5 transition hover:bg-white/5"
          >
            {user.avatarUrl ? (
              <img src={user.avatarUrl} alt={user.name} className="h-8 w-8 rounded-full object-cover" />
            ) : (
              <div className="flex h-8 w-8 items-center justify-center rounded-full bg-gradient-to-br from-brand-500 to-accent-500">
                <span className="text-sm font-semibold text-[#fff]">{user.name.charAt(0).toUpperCase()}</span>
              </div>
            )}
            <span className="hidden text-sm font-medium text-slate-300 sm:inline">{user.name}</span>
          </button>

          {isUserMenuOpen && (
            <div className="absolute right-0 mt-2 w-56 rounded-xl border border-white/10 bg-slate-950 py-2 shadow-2xl shadow-black/40">
              <div className="border-b border-white/10 px-4 py-3">
                <p className="text-sm font-semibold text-white">{user.name}</p>
                <p className="truncate text-xs text-slate-400">{user.email}</p>
              </div>
              <button
                onClick={onSignOut}
                className="flex w-full items-center gap-3 px-4 py-2 text-left text-sm text-slate-300 transition hover:bg-white/5 hover:text-white"
              >
                <LogOut size={16} />
                {t.signOut}
              </button>
            </div>
          )}
        </div>
      </div>
    </header>
  );
};

export default TopBar;
