import React, { useEffect, useRef, useState } from 'react';
import { Globe, LogOut } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useI18n } from '../../i18n';
import type { StudentUser } from '../../types';

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
    <header className="sticky top-0 z-40 flex h-16 items-center border-b border-slate-200 bg-white px-6 shadow-sm">
      <Link to="/" className="flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-gradient-to-br from-blue-600 to-purple-600">
          <span className="text-sm font-bold text-white">HO</span>
        </div>
        <span className="text-lg font-semibold text-slate-900">{t.appName}</span>
      </Link>

      <div className="ml-auto flex items-center gap-3">
        <button
          onClick={toggleLanguage}
          className="flex items-center gap-1.5 rounded-lg px-2 py-2 text-sm text-slate-600 transition hover:bg-slate-100"
          title={t.language}
          aria-label={t.language}
        >
          <Globe size={18} />
          <span className="uppercase">{language}</span>
        </button>

        <div className="relative" ref={userMenuRef}>
          <button
            onClick={() => setIsUserMenuOpen((open) => !open)}
            className="flex items-center gap-2 rounded-lg px-2 py-1.5 transition hover:bg-slate-100"
          >
            {user.avatarUrl ? (
              <img src={user.avatarUrl} alt={user.name} className="h-8 w-8 rounded-full object-cover" />
            ) : (
              <div className="flex h-8 w-8 items-center justify-center rounded-full bg-gradient-to-br from-blue-500 to-purple-500">
                <span className="text-sm font-semibold text-white">{user.name.charAt(0).toUpperCase()}</span>
              </div>
            )}
            <span className="hidden text-sm font-medium text-slate-700 sm:inline">{user.name}</span>
          </button>

          {isUserMenuOpen && (
            <div className="absolute right-0 mt-2 w-56 rounded-lg border border-slate-200 bg-white py-2 shadow-lg">
              <div className="border-b border-slate-100 px-4 py-3">
                <p className="text-sm font-semibold text-slate-900">{user.name}</p>
                <p className="truncate text-xs text-slate-500">{user.email}</p>
              </div>
              <button
                onClick={onSignOut}
                className="flex w-full items-center gap-3 px-4 py-2 text-left text-sm text-slate-700 transition hover:bg-slate-50"
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
