// Ambient types for the (still non-standard) Web Speech API's
// SpeechRecognition interface — the standard TS "DOM" lib does not
// include it. Minimal surface, only what useSpeechInput.ts actually uses.

interface SpeechRecognitionErrorEvent extends Event {
  error: string
}

interface SpeechRecognitionResultItem {
  transcript: string
}

interface SpeechRecognitionResultItemList {
  [index: number]: SpeechRecognitionResultItem
  length: number
}

interface SpeechRecognitionResultEvent extends Event {
  results: {
    [index: number]: SpeechRecognitionResultItemList
    length: number
  }
}

interface SpeechRecognition extends EventTarget {
  lang: string
  continuous: boolean
  interimResults: boolean
  start(): void
  stop(): void
  abort(): void
  onresult: ((event: SpeechRecognitionResultEvent) => void) | null
  onerror: ((event: SpeechRecognitionErrorEvent) => void) | null
  onend: (() => void) | null
}

interface Window {
  SpeechRecognition?: { new (): SpeechRecognition }
  webkitSpeechRecognition?: { new (): SpeechRecognition }
}
