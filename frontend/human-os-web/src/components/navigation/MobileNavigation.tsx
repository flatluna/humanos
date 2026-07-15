import { useState } from 'react';
import { NavLink } from 'react-router';
import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'framer-motion';
import { Menu, X, User, Settings } from 'lucide-react';
import { clsx } from 'clsx';
import { MOBILE_BAR_ITEMS, ALL_NAV_ITEMS } from './navItems';

const DRAWER_ONLY_ITEMS = ALL_NAV_ITEMS.filter((item) => !MOBILE_BAR_ITEMS.includes(item));

export function MobileNavigation() {
  const { t } = useTranslation();
  const [isDrawerOpen, setDrawerOpen] = useState(false);

  return (
    <>
      <nav
        aria-label={t('nav.mobileNavigation')}
        className="fixed inset-x-0 bottom-0 z-30 flex items-center justify-around border-t border-slate-200 bg-white/95 px-2 py-1.5 backdrop-blur-md md:hidden dark:border-white/10 dark:bg-[#05060a]/95"
      >
        {MOBILE_BAR_ITEMS.map(({ to, icon: Icon, labelKey }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              clsx(
                'flex min-h-11 min-w-11 flex-col items-center justify-center gap-0.5 rounded-xl px-3 py-1.5 text-[11px] font-medium transition',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500',
                isActive ? 'text-blue-600 dark:text-blue-300' : 'text-slate-400 dark:text-white/40',
              )
            }
          >
            <Icon className="h-5 w-5" />
            {t(labelKey)}
          </NavLink>
        ))}

        <button
          type="button"
          onClick={() => setDrawerOpen(true)}
          aria-label={t('nav.more')}
          aria-haspopup="dialog"
          aria-expanded={isDrawerOpen}
          className="flex min-h-11 min-w-11 flex-col items-center justify-center gap-0.5 rounded-xl px-3 py-1.5 text-[11px] font-medium text-slate-400 transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-blue-500 dark:text-white/40"
        >
          <Menu className="h-5 w-5" />
          {t('nav.more')}
        </button>
      </nav>

      <AnimatePresence>
        {isDrawerOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-40 bg-slate-900/40 md:hidden"
            onClick={() => setDrawerOpen(false)}
          >
            <motion.div
              role="dialog"
              aria-modal="true"
              aria-label={t('nav.more')}
              initial={{ y: '100%' }}
              animate={{ y: 0 }}
              exit={{ y: '100%' }}
              transition={{ duration: 0.25, ease: 'easeOut' }}
              onClick={(event) => event.stopPropagation()}
              className="absolute inset-x-0 bottom-0 rounded-t-3xl bg-white p-6 dark:bg-[#0b0c10]"
            >
              <div className="flex items-center justify-between">
                <p className="text-sm font-semibold text-slate-900 dark:text-white">{t('nav.more')}</p>
                <button
                  type="button"
                  onClick={() => setDrawerOpen(false)}
                  aria-label={t('common.close')}
                  className="flex h-9 w-9 items-center justify-center rounded-full border border-slate-200 text-slate-500 dark:border-white/10 dark:text-white/60"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>

              <ul className="mt-4 grid grid-cols-3 gap-3">
                {DRAWER_ONLY_ITEMS.map(({ to, icon: Icon, labelKey }) => (
                  <li key={to}>
                    <NavLink
                      to={to}
                      onClick={() => setDrawerOpen(false)}
                      className="flex min-h-[64px] flex-col items-center justify-center gap-1.5 rounded-2xl border border-slate-200 p-3 text-xs font-medium text-slate-600 dark:border-white/10 dark:text-white/60"
                    >
                      <Icon className="h-5 w-5" />
                      {t(labelKey)}
                    </NavLink>
                  </li>
                ))}
                <li>
                  <NavLink
                    to="/profile"
                    onClick={() => setDrawerOpen(false)}
                    className="flex min-h-[64px] flex-col items-center justify-center gap-1.5 rounded-2xl border border-slate-200 p-3 text-xs font-medium text-slate-600 dark:border-white/10 dark:text-white/60"
                  >
                    <User className="h-5 w-5" />
                    {t('nav.profile')}
                  </NavLink>
                </li>
                <li>
                  <NavLink
                    to="/settings"
                    onClick={() => setDrawerOpen(false)}
                    className="flex min-h-[64px] flex-col items-center justify-center gap-1.5 rounded-2xl border border-slate-200 p-3 text-xs font-medium text-slate-600 dark:border-white/10 dark:text-white/60"
                  >
                    <Settings className="h-5 w-5" />
                    {t('nav.settings')}
                  </NavLink>
                </li>
              </ul>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </>
  );
}
