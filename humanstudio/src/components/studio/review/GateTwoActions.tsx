import { Save, AlertCircle, CheckCircle2 } from 'lucide-react';

export interface GateTwoActionsProps {
  isLoading?: boolean;
  isSaving?: boolean;
  isRequesting?: boolean;
  isPublishing?: boolean;
  hasBlockingWarnings?: boolean;
  onSaveDraft?: () => void;
  onRequestChanges?: () => void;
  onApproveFinal?: () => void;
}

export function GateTwoActions({
  isLoading = false,
  isSaving = false,
  isRequesting = false,
  isPublishing = false,
  hasBlockingWarnings = false,
  onSaveDraft,
  onRequestChanges,
  onApproveFinal,
}: GateTwoActionsProps) {
  const isDisabled = isLoading || isSaving || isRequesting || isPublishing;
  const publishDisabled = isDisabled || hasBlockingWarnings;

  return (
    <div className="bg-white border border-gray-200 rounded-lg p-6 mb-6">
      <div className="flex items-start justify-between">
        <div>
          <h3 className="text-lg font-bold text-gray-900">Acciones</h3>
          {hasBlockingWarnings && (
            <div className="flex items-center gap-2 mt-2 text-red-600 text-sm font-semibold">
              <AlertCircle className="w-4 h-4" />
              Bloqueos críticos detectados. Debe regenerar el contenido para publicar.
            </div>
          )}
        </div>
      </div>

      <div className="flex flex-wrap gap-3 mt-4">
        {/* Save Draft */}
        <button
          onClick={onSaveDraft}
          disabled={isDisabled}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg font-semibold transition-colors ${
            isDisabled
              ? 'bg-gray-100 text-gray-400 cursor-not-allowed'
              : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
          }`}
        >
          <Save className="w-4 h-4" />
          {isSaving ? 'Guardando...' : 'Guardar borrador'}
        </button>

        {/* Request Changes */}
        <button
          onClick={onRequestChanges}
          disabled={isDisabled}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg font-semibold transition-colors ${
            isDisabled
              ? 'bg-amber-100 text-amber-400 cursor-not-allowed'
              : 'bg-amber-100 text-amber-700 hover:bg-amber-200'
          }`}
        >
          <AlertCircle className="w-4 h-4" />
          {isRequesting ? 'Enviando...' : 'Solicitar cambios'}
        </button>

        {/* Approve and Publish */}
        <button
          onClick={onApproveFinal}
          disabled={publishDisabled}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg font-semibold transition-colors ${
            publishDisabled
              ? 'bg-blue-100 text-blue-400 cursor-not-allowed'
              : 'bg-blue-600 text-white hover:bg-blue-700'
          }`}
        >
          <CheckCircle2 className="w-4 h-4" />
          {isPublishing ? 'Publicando...' : '✓ Aprobar y publicar →'}
        </button>
      </div>
    </div>
  );
}
