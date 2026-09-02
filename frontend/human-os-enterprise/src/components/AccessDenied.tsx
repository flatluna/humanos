import { useTranslation } from 'react-i18next';
import { ShieldAlert } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';

/** Shown when a real Human OS account exists but Person.OrgRole is
 *  'Employee' (or the account has no Tenant at all) — this console is
 *  scoped to Manager/HRAdmin only. */
export function AccessDenied({ reason }: { reason: 'role' | 'no-enterprise-account' }) {
  const { t } = useTranslation();
  const { logout, signedInEmail } = useAuth();

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-gradient-to-br from-slate-50 via-white to-amber-50/40 px-4 text-center dark:from-[#05060a] dark:via-[#05060a] dark:to-amber-500/5">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-amber-500 to-amber-400 text-white shadow-lg shadow-amber-500/30">
        <ShieldAlert className="h-7 w-7" />
      </div>
      <p className="text-lg font-semibold text-slate-900 dark:text-white">
        {reason === 'role' ? t('accessDenied.titleRole') : t('accessDenied.titleNoAccount')}
      </p>
      <p className="max-w-md text-sm text-slate-500 dark:text-white/50">
        {reason === 'role'
          ? t('accessDenied.messageRole')
          : t('accessDenied.messageNoAccount')}
      </p>
      {signedInEmail && (
        <p className="text-xs text-slate-400 dark:text-white/30">
          {t('accessDenied.signedInAs')}{' '}
          <span className="font-medium text-slate-500 dark:text-white/50">{signedInEmail}</span>
        </p>
      )}
      <button
        type="button"
        onClick={() => void logout()}
        className="inline-flex min-h-11 items-center justify-center rounded-full bg-gradient-to-r from-slate-900 to-slate-700 px-6 py-2.5 text-sm font-medium text-white shadow-md transition hover:brightness-110 dark:from-white dark:to-white dark:text-slate-900 dark:hover:bg-white/90"
      >
        {t('accessDenied.signOut')}
      </button>
    </div>
  );
}
