import { useState } from 'react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { BuddyVoiceModal } from '@/features/today/components/BuddyVoiceModal';
import { TecContigoVoiceModal } from '@/features/tec-contigo/TecContigoVoiceModal';

/**
 * Global floating voice-companion launcher (2026-09-22, split 2026-09-28) —
 * mounted once in EmployeeAppLayout so it floats above every page (not just
 * Today). STRICTLY split by user type, never mixed: students (Academic
 * tenant) get tecBot (TecContigoVoiceModal, grounded in their own
 * interests/grades/Engrams); every other user (employee, HR admin, manager,
 * etc.) gets Buddy (BuddyVoiceModal) instead.
 */
export function FloatingBuddyButton() {
  const [isOpen, setIsOpen] = useState(false);
  const { user } = useAuth();
  const { language } = useI18n();
  const displayName = user?.name?.split(' ')[0] ?? '';
  const isStudent = Boolean(user?.isAcademicTenant);
  const label = isStudent
    ? (language === 'en' ? 'Talk to tecBot' : 'Habla con tecBot')
    : (language === 'en' ? 'Talk to Buddy' : 'Habla con Buddy');

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        aria-label={label}
        className="group fixed right-4 top-20 z-[60] flex h-14 w-14 animate-buddyFloat items-center justify-center rounded-full shadow-lg shadow-emerald-600/30 ring-2 ring-white transition-transform hover:scale-110 dark:ring-slate-900"
      >
        <img
          src={isStudent ? '/TecBot.png' : '/buddy.png'}
          alt={isStudent ? 'tecBot' : 'Buddy'}
          className="h-16 w-16 object-contain"
        />
        <span className="absolute -right-1 -top-1 h-3.5 w-3.5 rounded-full border-2 border-white bg-emerald-500 dark:border-slate-900" />
      </button>

      {isStudent ? (
        <TecContigoVoiceModal
          open={isOpen}
          onClose={() => setIsOpen(false)}
          personId={user?.personId}
          personName={displayName}
          language={language === 'en' ? 'en' : 'es'}
        />
      ) : (
        <BuddyVoiceModal
          open={isOpen}
          onClose={() => setIsOpen(false)}
          personName={displayName}
          language={language === 'en' ? 'en' : 'es'}
        />
      )}
    </>
  );
}

