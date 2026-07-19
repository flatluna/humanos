import StepCard from '@components/StepCard'

interface IntroductionStepProps {
  message?: string
  onContinue: () => void
  isSubmitting?: boolean
}

/**
 * ModuleStarted stage (fixed 2026-07-16, backend companion to
 * ModuleStartedExecutor's new Introduction-Ack pause) — the Tutor's real
 * welcome to the module, shown BEFORE any Recall attempt. Reassures a
 * total beginner it's fine not to know the answer yet.
 */
export default function IntroductionStep({ onContinue, isSubmitting = false }: IntroductionStepProps) {
  return (
    <StepCard label="Introducción">
      <div className="space-y-4">
        <p className="text-sm text-gray-500">
          Lee la bienvenida de tu tutor a la derecha antes de empezar.
        </p>

        <button
          onClick={onContinue}
          disabled={isSubmitting}
          className="w-full px-4 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isSubmitting ? 'Procesando...' : 'Comenzar →'}
        </button>
      </div>
    </StepCard>
  )
}
