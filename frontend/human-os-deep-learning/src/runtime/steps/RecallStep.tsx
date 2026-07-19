import { useState } from 'react'
import StepCard from '@components/StepCard'
import MicButton from '@components/MicButton'

interface RecallStepProps {
  message?: string
  onSubmit: (response: string, forceAdvance?: boolean) => void
  isSubmitting?: boolean
  /** 1-based attempt number for this Recall retrieval-practice turn
   * (fixed 2026-07-17 — explicit user request: "no veo la suma de veces
   * iterando... necesitamos mostrar en que pregunta estamos"). */
  attemptNumber?: number
  totalAttempts?: number
  /** The PREVIOUS attempt's estimated accuracy 0-100 (fixed 2026-07-17) —
   * undefined on the first attempt or after a genuine clarifying
   * question. */
  lastAccuracyPercentage?: number
}

export default function RecallStep({
  onSubmit,
  isSubmitting = false,
  attemptNumber,
  totalAttempts,
  lastAccuracyPercentage,
}: RecallStepProps) {
  const [response, setResponse] = useState('')

  const handleSubmit = () => {
    if (response.trim()) {
      onSubmit(response)
      // Fixed 2026-07-17: ChapterRecall can now loop back to this SAME
      // stage for a follow-up sub-question (micro-dialogue) — clear the
      // box so the previous answer doesn't linger under the new question.
      setResponse('')
    }
  }

  // Fixed 2026-07-17 (explicit user request: "necesitamos más flexibilidad,
  // un botón de continuar") — lets the learner explicitly skip further
  // retries instead of being stuck iterating until the retry budget runs
  // out. Sends whatever is currently typed (may be empty) tagged with
  // forceAdvance so the backend bypasses the Tutor's completeness check.
  const handleContinueAnyway = () => {
    onSubmit(response.trim() || '(el alumno decidió continuar sin más intentos)', true)
    setResponse('')
  }

  return (
    <StepCard label="Recordar">
      <div className="space-y-4">
        {/* Fixed 2026-07-17: visible iteration/progress indicator — was
            getting lost in the conversation, explicit user complaint
            ("no veo la suma de veces iterando... se pierde"). */}
        {attemptNumber !== undefined && totalAttempts !== undefined && (
          <div className="flex items-center justify-between text-xs font-medium text-indigo-700 bg-indigo-50 rounded-lg px-3 py-2">
            <span>Intento {attemptNumber} de {totalAttempts}</span>
            {lastAccuracyPercentage !== undefined && (
              <span>Precisión del intento anterior: ~{lastAccuracyPercentage}%</span>
            )}
          </div>
        )}

        <p className="text-sm text-gray-500">
          Sin volver a mirar la guía anterior, escribe de memoria lo que recuerdas. Está bien si no recuerdas todo — inténtalo igual.
        </p>

        <div className="flex justify-end">
          <MicButton
            disabled={isSubmitting}
            onTranscript={(text) => setResponse((prev) => (prev ? `${prev} ${text}` : text))}
          />
        </div>

        <textarea
          value={response}
          onChange={(e) => setResponse(e.target.value)}
          placeholder="Escribe lo que recuerdas..."
          disabled={isSubmitting}
          className="w-full h-32 p-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 resize-none text-sm text-gray-800 placeholder-gray-400 disabled:bg-gray-50"
        />

        <button
          onClick={handleSubmit}
          disabled={!response.trim() || isSubmitting}
          className="w-full px-4 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isSubmitting ? 'Enviando...' : 'Continuar →'}
        </button>

        <button
          onClick={handleContinueAnyway}
          disabled={isSubmitting}
          className="w-full px-4 py-2 text-xs font-medium text-gray-500 hover:text-gray-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          Prefiero continuar de todas formas →
        </button>
      </div>
    </StepCard>
  )
}
