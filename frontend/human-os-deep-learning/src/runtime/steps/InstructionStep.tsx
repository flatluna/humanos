import StepCard from '@components/StepCard'

interface InstructionStepProps {
  message: string
  onContinue: () => void
  isSubmitting?: boolean
}

export default function InstructionStep({ onContinue, isSubmitting = false }: InstructionStepProps) {
  return (
    <StepCard label="Guía">
      <div className="space-y-4">
        <p className="text-sm text-gray-500">
          Lee con atención la guía de tu tutor a la derecha — a continuación se te pedirá recordarla sin volver a mirarla.
        </p>

        <button
          onClick={onContinue}
          disabled={isSubmitting}
          className="w-full px-4 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isSubmitting ? 'Procesando...' : 'Ya la leí, continuar →'}
        </button>
      </div>
    </StepCard>
  )
}
