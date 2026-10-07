import { Mic, MessageSquareText } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export type QaInteractionMode = 'text' | 'voice';

/**
 * "Voz en tiempo real vs. Texto" switch (2026-10-06) — lets the STUDENT
 * (not the course designer) choose, per visit, whether QA Professor /
 * Deep Dive's "speak with your agent" uses the realtime voice Agent or a
 * plain text chat. Text is always available and is the default in every
 * place this toggle is used; real-time voice is always an explicit opt-in
 * (never auto-started) — shared across the whole app so this choice reads
 * the same everywhere (Learn's QA Professor, Deep Dive's agent chat, etc).
 */
export default function VoiceTextModeToggle({
  mode,
  onChange,
}: {
  mode: QaInteractionMode;
  onChange: (mode: QaInteractionMode) => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="inline-flex items-center gap-1 rounded-xl border border-white/10 bg-white/[0.03] p-1">
      <button
        type="button"
        onClick={() => onChange('text')}
        className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold transition-all ${
          mode === 'text'
            ? 'bg-gradient-to-r from-brand-500 to-accent-500 text-white shadow-md shadow-brand-500/20'
            : 'text-slate-400 hover:text-white hover:bg-white/[0.05]'
        }`}
      >
        <MessageSquareText className="h-3.5 w-3.5" />
        {t('voiceTextModeToggle.text')}
      </button>
      <button
        type="button"
        onClick={() => onChange('voice')}
        className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold transition-all ${
          mode === 'voice'
            ? 'bg-gradient-to-r from-accent-500 to-fuchsia-500 text-white shadow-md shadow-accent-500/20'
            : 'text-slate-400 hover:text-white hover:bg-white/[0.05]'
        }`}
      >
        <Mic className="h-3.5 w-3.5" />
        {t('voiceTextModeToggle.voice')}
      </button>
    </div>
  );
}
