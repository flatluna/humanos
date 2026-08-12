import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle } from 'lucide-react';

interface DeleteStructuredLearningSectionModalProps {
  sectionName: string;
  onConfirm: () => Promise<void>;
  onCancel: () => void;
}

/**
 * Destructive-action warning modal for permanently deleting ONE section
 * from a Structured Learning document (2026-08-11) — mirrors
 * DeleteGraphNodeModal (Graph's equivalent) exactly, including the
 * type-the-exact-name-to-confirm pattern.
 */
export default function DeleteStructuredLearningSectionModal({
  sectionName,
  onConfirm,
  onCancel,
}: DeleteStructuredLearningSectionModalProps) {
  const { t } = useTranslation();
  const [confirmationText, setConfirmationText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isConfirmationValid = confirmationText.trim() === sectionName.trim();

  const handleConfirm = async () => {
    if (!isConfirmationValid || isDeleting) return;

    setIsDeleting(true);
    setError(null);

    try {
      await onConfirm();
    } catch (err) {
      setError(err instanceof Error ? err.message : t('deleteStructuredLearningSectionModal.deleteErrorFallback'));
      setIsDeleting(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="delete-structured-learning-section-title"
    >
      <div className="rounded-2xl border-2 border-red-600 bg-slate-900 shadow-2xl max-w-lg w-full p-6">
        <div className="flex items-start gap-3 mb-4">
          <div className="flex-shrink-0 w-12 h-12 rounded-full bg-red-500/10 flex items-center justify-center border border-red-500/30">
            <AlertTriangle className="text-red-500" size={28} />
          </div>
          <div>
            <h2 id="delete-structured-learning-section-title" className="text-xl font-bold text-red-500">
              {t('deleteStructuredLearningSectionModal.title')}
            </h2>
            <p className="text-sm text-slate-400 mt-1">{t('deleteStructuredLearningSectionModal.cannotUndo')}</p>
          </div>
        </div>

        <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-4 mb-4 text-sm text-red-200 space-y-2">
          <p>{t('deleteStructuredLearningSectionModal.willDelete', { name: sectionName })}</p>
          <ul className="list-disc list-inside space-y-1">
            <li>{t('deleteStructuredLearningSectionModal.bulletSteps')}</li>
            <li>{t('deleteStructuredLearningSectionModal.bulletIllustrations')}</li>
            <li>{t('deleteStructuredLearningSectionModal.bulletProgress')}</li>
            <li>{t('deleteStructuredLearningSectionModal.bulletNumbering')}</li>
          </ul>
          <p className="font-semibold">{t('deleteStructuredLearningSectionModal.cannotRecover')}</p>
        </div>

        <label className="block text-sm font-medium text-slate-300 mb-1">
          {t('deleteStructuredLearningSectionModal.typeToConfirm')} <span className="font-bold">{sectionName}</span>{' '}
          {t('deleteStructuredLearningSectionModal.toConfirm')}
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
            {t('deleteStructuredLearningSectionModal.cancel')}
          </button>
          <button
            onClick={handleConfirm}
            disabled={!isConfirmationValid || isDeleting}
            className="px-4 py-2 rounded-lg font-medium bg-red-600 text-white hover:bg-red-700 transition-all cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {isDeleting
              ? t('deleteStructuredLearningSectionModal.deleting')
              : t('deleteStructuredLearningSectionModal.deletePermanently')}
          </button>
        </div>
      </div>
    </div>
  );
}
