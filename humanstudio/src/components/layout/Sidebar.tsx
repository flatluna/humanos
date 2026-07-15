import React from 'react';
import { NavLink } from 'react-router-dom';
import { Home, BookOpen, Bookmark, Settings, X } from 'lucide-react';
import { useI18n } from '../../i18n';

interface SidebarProps {
  isOpen: boolean;
  onToggle: () => void;
}

const Sidebar: React.FC<SidebarProps> = ({ isOpen, onToggle }) => {
  const { t } = useI18n();

  // NOTE: "Progress" (student learning progress) is intentionally NOT in
  // this nav — HumanStudio is the capability-authoring app, not the student
  // app. The /progress route/page still exist but are unlinked here.
  const navItems = [
    { path: '/', label: t.home, icon: Home, emoji: '🏠' },
    { path: '/studio', label: t.studio, icon: BookOpen, emoji: '🎨' },
    { path: '/capabilities', label: t.capabilityLibrary, icon: Bookmark, emoji: '📚' },
    { path: '/settings', label: t.settings, icon: Settings, emoji: '⚙️' },
  ];

  return (
    <>
      {/* Mobile overlay */}
      {isOpen && (
        <div
          className="fixed inset-0 bg-black bg-opacity-50 md:hidden z-30"
          onClick={onToggle}
          aria-hidden="true"
        />
      )}

      {/* Sidebar */}
      <aside
        className={`fixed md:relative md:translate-x-0 left-0 top-0 h-screen w-64 bg-white border-r border-gray-200 pt-20 px-4 transition-transform duration-300 transform z-40 ${
          isOpen ? 'translate-x-0' : '-translate-x-full'
        }`}
      >
        {/* Close button for mobile */}
        <button
          onClick={onToggle}
          className="md:hidden absolute top-4 right-4 p-2 rounded-lg hover:bg-gray-100"
          aria-label="Close menu"
        >
          <X size={24} className="text-gray-600" />
        </button>

        {/* Navigation */}
        <nav className="space-y-2">
          {navItems.map((item) => {
            return (
              <NavLink
                key={item.path}
                to={item.path}
                className={({ isActive }) =>
                  `flex items-center gap-3 px-4 py-3 rounded-lg transition-colors ${
                    isActive
                      ? 'bg-blue-50 text-blue-600 border-l-4 border-blue-600'
                      : 'text-gray-700 hover:bg-gray-50'
                  }`
                }
              >
                <span className="text-lg">{item.emoji}</span>
                <span className="font-medium">{item.label}</span>
              </NavLink>
            );
          })}
        </nav>

        {/* Footer section */}
        <div className="absolute bottom-6 left-4 right-4 text-xs text-gray-500">
          <p>{t.version}</p>
        </div>
      </aside>
    </>
  );
};

export default Sidebar;
