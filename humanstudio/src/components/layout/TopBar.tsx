import React, { useState, useRef, useEffect } from 'react';
import { Search, LogOut, Settings, Globe } from 'lucide-react';
import { useI18n } from '../../i18n';
import type { TopBarProps } from '../../types';

const TopBar: React.FC<TopBarProps> = ({
  user,
  onSearch,
  onOpenSettings,
  onSignOut,
}) => {
  const { t, language, setLanguage } = useI18n();
  const [searchQuery, setSearchQuery] = useState('');
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);

  // Close menu when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    if (onSearch) {
      onSearch(searchQuery);
    }
  };

  const handleSignOut = () => {
    setIsUserMenuOpen(false);
    if (onSignOut) {
      onSignOut();
    }
  };

  const handleSettings = () => {
    setIsUserMenuOpen(false);
    if (onOpenSettings) {
      onOpenSettings();
    }
  };

  const toggleLanguage = () => {
    setLanguage(language === 'en' ? 'es' : 'en');
  };

  return (
    <div className="fixed top-0 left-0 right-0 h-16 bg-white border-b border-gray-200 flex items-center px-8 z-40 shadow-sm">
      {/* Brand - Left */}
      <div className="flex items-center gap-3 min-w-fit">
        <div className="w-10 h-10 bg-gradient-to-br from-blue-600 to-purple-600 rounded-lg flex items-center justify-center flex-shrink-0">
          <span className="text-white font-bold text-sm">HS</span>
        </div>
        <h1 className="text-lg font-bold text-gray-900">{t.appName}</h1>
      </div>

      {/* Search Bar - Center */}
      <form onSubmit={handleSearch} className="flex-1 flex justify-center px-8">
        <div className="relative w-full max-w-sm">
          <Search size={18} className="absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            placeholder={t.search}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2 bg-gray-100 border border-gray-300 rounded-lg text-sm text-gray-700 placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:bg-white transition-colors"
          />
        </div>
      </form>

      {/* User Menu & Language - Right */}
      <div className="flex items-center gap-4 ml-auto">
        {/* Language Toggle */}
        <button
          onClick={toggleLanguage}
          className="p-2 rounded-lg hover:bg-gray-100 transition-colors"
          title={`Switch to ${language === 'en' ? 'Spanish' : 'English'}`}
          aria-label="Toggle language"
        >
          <Globe size={20} className="text-gray-600" />
        </button>

        {/* User Menu */}
        <div className="relative" ref={userMenuRef}>
          <button
            onClick={() => setIsUserMenuOpen(!isUserMenuOpen)}
            className="flex items-center gap-3 px-3 py-2 rounded-lg hover:bg-gray-100 transition-colors"
          >
            {user.avatarUrl ? (
              <img
                src={user.avatarUrl}
                alt={user.name}
                className="w-8 h-8 rounded-full object-cover"
              />
            ) : (
              <div className="w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-purple-500 flex items-center justify-center flex-shrink-0">
                <span className="text-white text-sm font-semibold">
                  {user.name.charAt(0).toUpperCase()}
                </span>
              </div>
            )}
            <span className="text-sm font-medium text-gray-700 hidden sm:inline">
              {user.name}
            </span>
            <svg
              className={`w-4 h-4 text-gray-600 transition-transform ${
                isUserMenuOpen ? 'rotate-180' : ''
              }`}
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 14l-7 7m0 0l-7-7m7 7V3" />
            </svg>
          </button>

          {/* Dropdown Menu */}
          {isUserMenuOpen && (
            <div className="absolute right-0 mt-2 w-48 bg-white border border-gray-200 rounded-lg shadow-lg py-2 z-50">
              {/* User Info */}
              <div className="px-4 py-3 border-b border-gray-100">
                <p className="text-sm font-semibold text-gray-900">{user.name}</p>
                <p className="text-xs text-gray-500">{user.email}</p>
              </div>

              {/* Menu Items */}
              <button
                onClick={handleSettings}
                className="w-full flex items-center gap-3 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 transition-colors text-left"
              >
                <Settings size={16} />
                {t.settings}
              </button>

              <button
                onClick={handleSignOut}
                className="w-full flex items-center gap-3 px-4 py-2 text-sm text-red-600 hover:bg-red-50 transition-colors text-left border-t border-gray-100"
              >
                <LogOut size={16} />
                {t.signOut}
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default TopBar;
