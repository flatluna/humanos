import { useState } from 'react'
import StepCard from '@components/StepCard'
import MicButton from '@components/MicButton'

interface InitialAnswerStepProps {
  message?: string
  onSubmit: (response: string, forceAdvance?: boolean) => void
  isSubmitting?: boolean
}

export default function InitialAnswerStep({ onSubmit, isSubmitting = false }: InitialAnswerStepProps) {
  const [response, setResponse] = useState('')

  const handleSubmit = () => {
    if (response.trim()) {
      onSubmit(response)
      // Fixed 2026-07-17: ChapterPrediction can now loop back to this SAME
      // stage for a follow-up sub-question (micro-dialogue) — clear the
      // box so the previous answer doesn't linger under the new question.
      setResponse('')
    }
  }

  // Fixed 2026-07-17 (explicit user request: "necesitamos más flexibilidad,
  // un botón de continuar") — same escape hatch as RecallStep.
  const handleContinueAnyway = () => {
    onSubmit(response.trim() || '(el alumno decidió continuar sin más intentos)', true)
    setResponse('')
  }

  return (
    <StepCard label="Predicción">
      <div className="space-y-4">
        <div>
          <p className="text-sm font-medium text-gray-600 mb-2">Tu respuesta debe ser</p>
          <ul className="text-sm text-gray-500 space-y-1">
            <li className="flex items-start gap-2">
              <span className="text-indigo-500 font-bold">✓</span>
              <span>Basada en lo que ya sabes</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-indigo-500 font-bold">✓</span>
              <span>Específica y concreta</span>
            </li>
            <li className="flex items-start gap-2">
              <span className="text-indigo-500 font-bold">✓</span>
              <span>Un intento honesto (no una adivinanza al azar)</span>
            </li>
          </ul>
        </div>

        <div className="flex justify-end">
          <MicButton
            disabled={isSubmitting}
            onTranscript={(text) => setResponse((prev) => (prev ? `${prev} ${text}` : text))}
          />
        </div>

        <textarea
          value={response}
          onChange={(e) => setResponse(e.target.value)}
          placeholder="Escribe tu predicción o hipótesis..."
          disabled={isSubmitting}
          className="w-full h-32 p-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 resize-none text-sm text-gray-800 placeholder-gray-400 disabled:bg-gray-50"
        />

        <button
          onClick={handleSubmit}
          disabled={!response.trim() || isSubmitting}
          className="w-full px-4 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isSubmitting ? 'Enviando...' : 'Enviar predicción →'}
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
