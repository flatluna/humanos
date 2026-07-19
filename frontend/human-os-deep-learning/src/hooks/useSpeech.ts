import { useCallback, useEffect, useRef, useState } from 'react'

const API_BASE_URL = 'http://localhost:7071/api'

/**
 * Voice hook — real neural Text-to-Speech via the backend's Azure AI
 * Speech endpoint (fixed 2026-07-16 — explicit user requirement: no LLM/
 * chatbot involved, direct call to Azure's neural voices; replaces the
 * earlier browser-native `speechSynthesis` Capa 1 fallback with real
 * neural voice quality) + Web Speech API for voice INPUT (STT), which
 * stays browser-native — Azure only handles narration (TTS) for now.
 */
export function useSpeech() {
  const audioRef = useRef<HTMLAudioElement | null>(null)
  const objectUrlRef = useRef<string | null>(null)
  const recognitionRef = useRef<SpeechRecognition | null>(null)
  // Fixed 2026-07-16 — real bug found via live testing: React.StrictMode
  // (active in main.tsx) double-invokes the narration useEffect in dev,
  // firing 2-3 near-simultaneous speak() calls. Each is an independent
  // async fetch; pausing audioRef.current at the TOP of speak() only
  // stops whatever was ALREADY playing at call time — it does nothing to
  // an earlier call's fetch that's still in flight, so multiple .then()
  // chains each independently created + played their own Audio object,
  // overlapping. This token is bumped on every speak() call; a fetch's
  // resolution is discarded (never played) unless it's still the LATEST
  // requested speak() — same "ignore stale async response" pattern as an
  // AbortController, without needing to cancel the actual HTTP request.
  const speakTokenRef = useRef(0)
  const [isSpeaking, setIsSpeaking] = useState(false)
  const [isListening, setIsListening] = useState(false)
  const [speechSupported] = useState(true) // backend-based — always "supported" from the browser's perspective
  const [recognitionSupported] = useState(
    () => typeof window !== 'undefined' && !!(window.SpeechRecognition || window.webkitSpeechRecognition)
  )

  const revokeObjectUrl = () => {
    if (objectUrlRef.current) {
      URL.revokeObjectURL(objectUrlRef.current)
      objectUrlRef.current = null
    }
  }

  const stopSpeaking = useCallback(() => {
    speakTokenRef.current++ // invalidate any in-flight speak() too
    audioRef.current?.pause()
    setIsSpeaking(false)
  }, [])

  const speak = useCallback((text: string, languageCode = 'es-MX') => {
    if (!text.trim()) return

    const token = ++speakTokenRef.current
    audioRef.current?.pause()
    setIsSpeaking(false)

    fetch(`${API_BASE_URL}/speech/synthesize`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ Text: text, LanguageCode: languageCode }),
    })
      .then((response) => {
        if (!response.ok) throw new Error(`Speech synthesis failed: ${response.statusText}`)
        return response.blob()
      })
      .then((blob) => {
        // A newer speak() (or stopSpeaking()) superseded this call while
        // the fetch was in flight — discard, never play.
        if (token !== speakTokenRef.current) return

        revokeObjectUrl()
        const url = URL.createObjectURL(blob)
        objectUrlRef.current = url

        const audio = new Audio(url)
        audioRef.current = audio
        audio.onplay = () => setIsSpeaking(true)
        audio.onended = () => setIsSpeaking(false)
        audio.onerror = () => setIsSpeaking(false)
        audio.play().catch(() => setIsSpeaking(false))
      })
      .catch((err) => {
        console.error('Azure Speech synthesis failed:', err)
        setIsSpeaking(false)
      })
  }, [])

  const startListening = useCallback(
    (onResult: (transcript: string) => void) => {
      if (!recognitionSupported) return

      const RecognitionCtor = window.SpeechRecognition || window.webkitSpeechRecognition
      if (!RecognitionCtor) return

      const recognition = new RecognitionCtor()
      recognition.lang = 'es-ES'
      recognition.continuous = false
      recognition.interimResults = false

      recognition.onresult = (event) => {
        const transcript = event.results[0]?.[0]?.transcript ?? ''
        if (transcript) onResult(transcript)
      }
      recognition.onerror = () => setIsListening(false)
      recognition.onend = () => setIsListening(false)

      recognitionRef.current = recognition
      setIsListening(true)
      recognition.start()
    },
    [recognitionSupported]
  )

  const stopListening = useCallback(() => {
    recognitionRef.current?.stop()
    setIsListening(false)
  }, [])

  // Stop any in-flight speech/listening when the component unmounts.
  useEffect(() => {
    return () => {
      speakTokenRef.current++ // invalidate any in-flight speak() fetch too
      audioRef.current?.pause()
      revokeObjectUrl()
      recognitionRef.current?.abort()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return {
    speak,
    stopSpeaking,
    isSpeaking,
    speechSupported,
    startListening,
    stopListening,
    isListening,
    recognitionSupported,
  }
}
