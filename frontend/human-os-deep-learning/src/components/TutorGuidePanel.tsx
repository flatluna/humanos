interface TutorGuidePanelProps {
  stageLabel: string
  message: string
  /** Fixed 2026-07-16 — reflects the voice narration's live state so the
   * "agent" column visibly reacts while talking (pulsing avatar + a small
   * speaking badge), not just a static text panel. */
  isSpeaking?: boolean
}

/**
 * Persistent "Agente interactuando" panel (redesign 2026-07-16 — now its
 * own middle column between the course menu and the main step content,
 * per explicit layout request). Sticky on scroll so the learner always
 * has the current prompt/guidance visible while they work in the main
 * column.
 */
export default function TutorGuidePanel({ stageLabel, message, isSpeaking = false }: TutorGuidePanelProps) {
  return (
    <aside className="lg:sticky lg:top-6 h-fit">
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        <div className="flex items-center gap-3 px-5 py-4 border-b border-gray-100 bg-slate-900">
          <div
            className={`w-8 h-8 rounded-full bg-indigo-500 flex items-center justify-center text-white text-sm font-semibold shrink-0 ${
              isSpeaking ? 'animate-pulse ring-2 ring-indigo-300' : ''
            }`}
          >
            T
          </div>
          <div>
            <p className="text-white text-sm font-semibold leading-tight">Tu Tutor</p>
            <p className="text-slate-400 text-xs leading-tight">
              {stageLabel}
              {isSpeaking && ' · 🔊 hablando…'}
            </p>
          </div>
        </div>
        <div className="px-5 py-5">
          <p className="text-sm text-gray-700 leading-relaxed whitespace-pre-line">
            {message}
          </p>
        </div>
      </div>
    </aside>
  )
}
