import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { CapabilitySummary } from '../../types';

interface DeleteCapabilityModalProps {
  capability: CapabilitySummary;
  onConfirm: () => Promise<void>;
  onCancel: () => void;
}

/**
 * Big, explicit warning modal shown before permanently deleting a
 * capability. The delete itself (DELETE /capabilities/{id}) is
 * irreversible and — per CapabilityService.DeleteAsync's doc comment —
 * wipes out not just the capability's own content (levels/modules, the
 * graph, illustrations, blueprints) but also any Learning Runtime sessions
 * and learner progress (PersonCapability, Evidence, GrowthActions) tied to
 * it. This modal exists so that destructive scope is never a silent
 * surprise: the user must type the capability's exact title to enable the
 * confirm button, the standard "type to confirm" pattern for irreversible
 * admin actions.
 */
export default function DeleteCapabilityModal({ capability, onConfirm, onCancel }: DeleteCapabilityModalProps) {
  const [confirmationText, setConfirmationText] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isConfirmationValid = confirmationText.trim() === capability.title.trim();

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
      <div className="bg-white rounded-xl shadow-2xl max-w-lg w-full p-6 border-2 border-red-600">
        <div className="flex items-start gap-3 mb-4">
          <div className="flex-shrink-0 w-12 h-12 rounded-full bg-red-100 flex items-center justify-center">
            <AlertTriangle className="text-red-600" size={28} />
          </div>
          <div>
            <h2 id="delete-capability-title" className="text-xl font-bold text-red-700">
              Eliminar capability permanentemente
            </h2>
            <p className="text-sm text-gray-600 mt-1">Esta acción NO se puede deshacer.</p>
          </div>
        </div>

        <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-4 text-sm text-red-900 space-y-2">
          <p>
            Vas a eliminar <span className="font-bold">"{capability.title}"</span> y absolutamente todo su
            contenido:
          </p>
          <ul className="list-disc list-inside space-y-1">
            <li>Todos los niveles, módulos y guiones del instructor</li>
            <li>El grafo de la capability (nodos, ilustraciones, blueprints de experiencia)</li>
            <li>Cualquier sesión de Learning Runtime asociada</li>
            <li>
              El <span className="font-semibold">progreso de cualquier estudiante</span> que ya haya interactuado
              con esta capability (evidencias, mastery, evaluaciones)
            </li>
          </ul>
          <p className="font-semibold">Nada de esto se puede recuperar después de confirmar.</p>
        </div>

        <label className="block text-sm font-medium text-gray-700 mb-1">
          Escribe <span className="font-bold">{capability.title}</span> para confirmar:
        </label>
        <input
          type="text"
          value={confirmationText}
          onChange={(e) => setConfirmationText(e.target.value)}
          disabled={isDeleting}
          className="w-full px-3 py-2 border border-gray-300 rounded-lg text-gray-900 mb-2 focus:outline-none focus:ring-2 focus:ring-red-500 disabled:bg-gray-100"
          autoFocus
        />

        {error && <p className="text-sm text-red-600 mb-2">{error}</p>}

        <div className="flex justify-end gap-3 mt-4">
          <button
            onClick={onCancel}
            disabled={isDeleting}
            className="px-4 py-2 rounded-lg font-medium text-gray-700 hover:bg-gray-100 transition-all cursor-pointer disabled:opacity-50"
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
