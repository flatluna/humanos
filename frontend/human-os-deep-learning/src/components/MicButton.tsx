import { useSpeech } from '@/hooks/useSpeech'

interface MicButtonProps {
  onTranscript: (text: string) => void
  disabled?: boolean
}

/**
 * Small reusable mic button (fixed 2026-07-16 — Capa 1 voice input) —
 * transcribes speech into text and hands it to the caller, which decides
 * how to merge it into its own textarea state. The transcribed text is
 * always editable afterward; nothing is auto-submitted from voice alone.
 */
export default function MicButton({ onTranscript, disabled = false }: MicButtonProps) {
  const { startListening, stopListening, isListening, recognitionSupported } = useSpeech()

  if (!recognitionSupported) return null

  const handleClick = () => {
    if (isListening) {
      stopListening()
      return
    }
    startListening((transcript) => onTranscript(transcript))
  }

  return (
    <button
      type="button"
      onClick={handleClick}
      disabled={disabled}
      title={isListening ? 'Detener grabación' : 'Hablar tu respuesta'}
      className={`inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg border transition-colors disabled:opacity-50 ${
        isListening
          ? 'bg-red-50 border-red-300 text-red-700 animate-pulse'
          : 'bg-white border-gray-300 text-gray-600 hover:bg-gray-50'
      }`}
    >
      <span>{isListening ? '🔴' : '🎤'}</span>
      {isListening ? 'Escuchando…' : 'Hablar respuesta'}
    </button>
  )
}
