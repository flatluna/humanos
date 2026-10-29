import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle } from 'lucide-react';

interface DeleteCapabilityModalProps {
  capabilityName: string;
  capabilityId: string;
  onConfirm: () => Promise<void>;
  onCancel: () => void;
}

/**
 * Destructive-action warning modal for permanently deleting a capability.
 * The user must type the exact capability name to enable the confirm button.
 * Styled to match capabilitystudio's dark theme.
 */
export default function DeleteCapabilityModal({
  capabilityName,
  // capabilityId is passed but not currently used; keeping it in the interface
  // for potential future use (e.g., for analytics or audit logging)
  capabilityId: _capabilityId,
  onConfirm,
  onCancel,
}: DeleteCapabilityModalProps) {
  const { t } = useTranslation();
  const [confirmationText, setConfirmationText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Collapses repeated whitespace before comparing — some stored names have
  // an accidental double space (e.g. from the original document title) that
  // is visually indistinguishable from a single space, making an exact-string
  // match impossible for the user to satisfy no matter how carefully they type.
  const normalize = (value: string) => value.trim().replace(/\s+/g, ' ');
  const isConfirmationValid = normalize(confirmationText) === normalize(capabilityName);

  const handleConfirm = async () => {
    if (!isConfirmationValid || isDeleting) return;

    setIsDeleting(true);
    setError(null);

    try {
      await onConfirm();
    } catch (err) {
      setError(err instanceof Error ? err.message : t('deleteCapabilityModal.deleteErrorFallback'));
      setIsDeleting(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="delete-capability-title"
    >
      <div className="rounded-2xl border-2 border-red-600 bg-slate-900 shadow-2xl max-w-lg w-full p-6">
        <div className="flex items-start gap-3 mb-4">
          <div className="flex-shrink-0 w-12 h-12 rounded-full bg-red-500/10 flex items-center justify-center border border-red-500/30">
            <AlertTriangle className="text-red-500" size={28} />
          </div>
          <div>
            <h2 id="delete-capability-title" className="text-xl font-bold text-red-500">
              {t('deleteCapabilityModal.title')}
            </h2>
            <p className="text-sm text-slate-400 mt-1">{t('deleteCapabilityModal.cannotUndo')}</p>
          </div>
        </div>

        <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-4 mb-4 text-sm text-red-200 space-y-2">
          <p>
            {t('deleteCapabilityModal.willDelete', { name: capabilityName })}
          </p>
          <ul className="list-disc list-inside space-y-1">
            <li>{t('deleteCapabilityModal.bulletLevels')}</li>
            <li>{t('deleteCapabilityModal.bulletGraph')}</li>
            <li>{t('deleteCapabilityModal.bulletSessions')}</li>
            <li>
              {t('deleteCapabilityModal.bulletProgressPrefix')}{' '}
              <span className="font-semibold">{t('deleteCapabilityModal.bulletProgressStrong')}</span>{' '}
              {t('deleteCapabilityModal.bulletProgressSuffix')}
            </li>
          </ul>
          <p className="font-semibold">{t('deleteCapabilityModal.cannotRecover')}</p>
        </div>

        <label className="block text-sm font-medium text-slate-300 mb-1">
          {t('deleteCapabilityModal.typeToConfirm')} <span className="font-bold">{capabilityName}</span> {t('deleteCapabilityModal.toConfirm')}
        </label>
        <input
          type="text"
          value={confirmationText}
          onChange={(e) => setConfirmationText(e.target.value)}
          disabled={isDeleting}
          className="w-full px-3 py-2 border border-white/10 rounded-lg bg-white/5 text-white mb-2 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-red-500/50 disabled:opacity-50"
          autoFocus
        />

        {error && <p className="text-sm text-red-400 mb-2">{error}</p>}

        <div className="flex justify-end gap-3 mt-6">
          <button
            onClick={onCancel}
            disabled={isDeleting}
            className="px-4 py-2 rounded-lg font-medium text-slate-300 hover:bg-white/5 transition-all cursor-pointer disabled:opacity-50"
          >
            {t('deleteCapabilityModal.cancel')}
          </button>
          <button
            onClick={handleConfirm}
            disabled={!isConfirmationValid || isDeleting}
            className="px-4 py-2 rounded-lg font-medium bg-red-600 text-white hover:bg-red-700 transition-all cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {isDeleting ? t('deleteCapabilityModal.deleting') : t('deleteCapabilityModal.deletePermanently')}
          </button>
        </div>
      </div>
    </div>
  );
}
