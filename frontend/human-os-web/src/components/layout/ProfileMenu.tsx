import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'framer-motion';
import { User, SlidersHorizontal, ShieldCheck, HelpCircle, LogOut, type LucideIcon } from 'lucide-react';
import { clsx } from 'clsx';
import { msalInstance } from '@/auth';

interface MenuLink {
  labelKey: 'nav.profile' | 'nav.preferences' | 'nav.privacyAndData' | 'nav.help';
  to: string;
  icon: LucideIcon;
}

const MENU_LINKS: MenuLink[] = [
  { labelKey: 'nav.profile', to: '/profile', icon: User },
  { labelKey: 'nav.preferences', to: '/settings', icon: SlidersHorizontal },
  { labelKey: 'nav.privacyAndData', to: '/privacy', icon: ShieldCheck },
  { labelKey: 'nav.help', to: '/help', icon: HelpCircle },
];

interface ProfileMenuProps {
  userName: string;
  roleName?: string;
  organizationName?: string;
  /** `compact` (header, avatar only on small widths) or `full` (sidebar footer, always shows name). */
  variant?: 'compact' | 'full';
  /** Whether the parent sidebar is manually collapsed to icon-only.
   *  Ignored for the `compact` (header) variant. */
  collapsed?: boolean;
}

export function ProfileMenu({
  userName,
  roleName,
  organizationName,
  variant = 'compact',
  collapsed = false,
}: ProfileMenuProps) {
  const { t } = useTranslation();
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);
  const initials = userName
    .split(' ')
    .map((part) => part[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();

  useEffect(() => {
    function handlePointerDown(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    }

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, []);

  function handleSignOut() {
    setIsOpen(false);
    const accounts = msalInstance.getAllAccounts();
    // TODO: Once Microsoft Entra ID is configured, this will always have
    // an active account. For now, guard against calling logout with no
    // signed-in account.
    if (accounts.length > 0) {
      void msalInstance.logoutRedirect();
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setIsOpen((open) => !open)}
        aria-haspopup="menu"
        aria-expanded={isOpen}
        className={clsx(
          'flex items-center gap-2.5 rounded-xl transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500',
          variant === 'compact'
            ? 'p-1 hover:bg-slate-100 dark:hover:bg-white/5'
            : 'w-full p-2 hover:bg-slate-100 dark:hover:bg-white/5',
        )}
      >
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-slate-900 text-sm font-semibold text-white dark:bg-white dark:text-slate-900">
          {initials}
        </span>
        <span className={clsx('text-left', collapsed ? 'hidden' : 'hidden lg:block')}>
          <span className="block text-sm font-semibold text-slate-900 dark:text-white">{userName}</span>
          {roleName && <span className="block text-xs text-slate-400 dark:text-white/40">{roleName}</span>}
        </span>
      </button>

      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0, y: -6, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -6, scale: 0.98 }}
            transition={{ duration: 0.15 }}
            role="menu"
            className={clsx(
              'absolute z-40 w-64 overflow-hidden rounded-2xl border border-slate-200 bg-white p-2 shadow-lg shadow-slate-900/10 dark:border-white/10 dark:bg-[#0b0c10]',
              variant === 'compact' ? 'right-0 top-full mt-2' : 'bottom-full left-0 mb-2',
            )}
          >
            <div className="px-3 py-2">
              <p className="text-sm font-semibold text-slate-900 dark:text-white">{userName}</p>
              {roleName && <p className="text-xs text-slate-400 dark:text-white/40">{roleName}</p>}
              {organizationName && <p className="text-xs text-slate-400 dark:text-white/40">{organizationName}</p>}
            </div>

            <div className="my-1 h-px bg-slate-100 dark:bg-white/10" />

            <ul>
              {MENU_LINKS.map(({ labelKey, to, icon: Icon }) => (
                <li key={to}>
                  <a
                    href={to}
                    role="menuitem"
                    onClick={() => setIsOpen(false)}
                    className="flex items-center gap-3 rounded-xl px-3 py-2 text-sm font-medium text-slate-600 transition hover:bg-slate-100 hover:text-slate-900 dark:text-white/60 dark:hover:bg-white/5 dark:hover:text-white"
                  >
                    <Icon className="h-4 w-4" />
                    {t(labelKey)}
                  </a>
                </li>
              ))}
            </ul>

            <div className="my-1 h-px bg-slate-100 dark:bg-white/10" />

            <button
              type="button"
              role="menuitem"
              onClick={handleSignOut}
              className="flex w-full items-center gap-3 rounded-xl px-3 py-2 text-sm font-medium text-red-600 transition hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-500/10"
            >
              <LogOut className="h-4 w-4" />
              {t('nav.signOut')}
            </button>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
