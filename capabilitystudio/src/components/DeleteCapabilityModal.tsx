import { useState } from 'react';
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
  capabilityId,
  onConfirm,
  onCancel,
}: DeleteCapabilityModalProps) {
  const [confirmationText, setConfirmationText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isConfirmationValid = confirmationText.trim() === capabilityName.trim();

  const handleConfirm = async () => {
    if (!isConfirmationValid || isDeleting) return;

    setIsDeleting(true);
    setError(null);

    try {
      await onConfirm();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'No se pudo eliminar la capability.');
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
              Eliminar capability permanentemente
            </h2>
            <p className="text-sm text-slate-400 mt-1">Esta acción NO se puede deshacer.</p>
          </div>
        </div>

        <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-4 mb-4 text-sm text-red-200 space-y-2">
          <p>
            Vas a eliminar <span className="font-bold">"{capabilityName}"</span> y absolutamente todo su contenido:
          </p>
          <ul className="list-disc list-inside space-y-1">
            <li>Todos los niveles, módulos y guiones del instructor</li>
            <li>El grafo de la capability (nodos, ilustraciones, blueprints)</li>
            <li>Cualquier sesión de Learning Runtime asociada</li>
            <li>
              El <span className="font-semibold">progreso de cualquier estudiante</span> (evidencias, mastery,
              evaluaciones)
            </li>
          </ul>
          <p className="font-semibold">Nada de esto se puede recuperar después de confirmar.</p>
        </div>

        <label className="block text-sm font-medium text-slate-300 mb-1">
          Escribe <span className="font-bold">{capabilityName}</span> para confirmar:
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
            Cancelar
          </button>
          <button
            onClick={handleConfirm}
            disabled={!isConfirmationValid || isDeleting}
            className="px-4 py-2 rounded-lg font-medium bg-red-600 text-white hover:bg-red-700 transition-all cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {isDeleting ? 'Eliminando...' : 'Eliminar permanentemente'}
          </button>
        </div>
      </div>
    </div>
  );
}
