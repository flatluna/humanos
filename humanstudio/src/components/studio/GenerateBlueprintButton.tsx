import React from 'react';
import { ArrowRight } from 'lucide-react';

interface GenerateBlueprintButtonProps {
  isValid: boolean;
  isLoading: boolean;
  onClick: () => void;
  hasUploadingFile?: boolean;
  hasErrorFile?: boolean;
}

const GenerateBlueprintButton: React.FC<GenerateBlueprintButtonProps> = ({
  isValid,
  isLoading,
  onClick,
  hasUploadingFile = false,
  hasErrorFile = false,
}) => {
  const isDisabled =
    !isValid || isLoading || hasUploadingFile || hasErrorFile;

  return (
    <div className="flex justify-end">
      <button
        onClick={onClick}
        disabled={isDisabled}
        className={`inline-flex items-center gap-2 px-6 py-3 rounded-lg font-medium transition-all ${
          isDisabled
            ? 'bg-gray-300 text-gray-600 cursor-not-allowed'
            : 'bg-purple-600 text-white hover:bg-purple-700 active:bg-purple-800 cursor-pointer'
        }`}
      >
        {isLoading ? (
          <>
            <span className="animate-spin">⟳</span>
            Generando blueprint...
          </>
        ) : (
          <>
            Generar blueprint
            <ArrowRight size={18} />
          </>
        )}
      </button>
    </div>
  );
};

export default GenerateBlueprintButton;
