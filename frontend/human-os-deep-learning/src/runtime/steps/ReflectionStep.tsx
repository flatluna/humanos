import { useState } from 'react'
import StepCard from '@components/StepCard'
import MicButton from '@components/MicButton'

interface ReflectionStepProps {
  message?: string
  onSubmit: (response: string) => void
  isSubmitting?: boolean
}

export default function ReflectionStep({ onSubmit, isSubmitting = false }: ReflectionStepProps) {
  const [answer, setAnswer] = useState('')

  const handleSubmit = () => {
    if (answer.trim()) {
      onSubmit(answer)
    }
  }

  return (
    <StepCard label="Reflexión">
      <div className="space-y-4">
        <p className="text-sm text-gray-500">
          Responde a las preguntas de tu tutor a la derecha, con tus propias palabras.
        </p>

        <div className="flex justify-end">
          <MicButton
            disabled={isSubmitting}
            onTranscript={(text) => setAnswer((prev) => (prev ? `${prev} ${text}` : text))}
          />
        </div>

        <textarea
          value={answer}
          onChange={(e) => setAnswer(e.target.value)}
          placeholder="Escribe tu reflexión aquí..."
          disabled={isSubmitting}
          className="w-full h-40 p-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 resize-none text-sm text-gray-800 placeholder-gray-400 disabled:bg-gray-50"
        />

        <button
          onClick={handleSubmit}
          disabled={!answer.trim() || isSubmitting}
          className="w-full px-4 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isSubmitting ? 'Enviando...' : 'Completar sesión →'}
        </button>
      </div>
    </StepCard>
  )
}
