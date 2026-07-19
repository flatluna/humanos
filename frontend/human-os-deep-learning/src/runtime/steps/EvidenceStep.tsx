import { useState } from 'react'
import StepCard from '@components/StepCard'
import MicButton from '@components/MicButton'

interface EvidenceStepProps {
  message?: string
  onSubmit: (response: string) => void
  isSubmitting?: boolean
}

export default function EvidenceStep({ onSubmit, isSubmitting = false }: EvidenceStepProps) {
  const [evidence, setEvidence] = useState('')

  const handleSubmit = () => {
    if (evidence.trim()) {
      onSubmit(evidence)
    }
  }

  return (
    <StepCard label="Práctica">
      <div className="space-y-4">
        <p className="text-sm text-gray-500">
          Describe con tus propias palabras, paso a paso, cómo lo harías tú mismo. No copies la guía — explica cómo lo aplicarías en la práctica.
        </p>

        <div className="flex justify-end">
          <MicButton
            disabled={isSubmitting}
            onTranscript={(text) => setEvidence((prev) => (prev ? `${prev} ${text}` : text))}
          />
        </div>

        <textarea
          value={evidence}
          onChange={(e) => setEvidence(e.target.value)}
          placeholder="Escribe tu respuesta aquí..."
          disabled={isSubmitting}
          className="w-full h-40 p-3 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 resize-none text-sm text-gray-800 placeholder-gray-400 disabled:bg-gray-50"
        />

        <button
          onClick={handleSubmit}
          disabled={!evidence.trim() || isSubmitting}
          className="w-full px-4 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {isSubmitting ? 'Enviando...' : 'Enviar evidencia →'}
        </button>
      </div>
    </StepCard>
  )
}
