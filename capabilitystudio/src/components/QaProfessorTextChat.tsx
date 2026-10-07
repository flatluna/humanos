import { useRef, useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Bot, Loader2, Send, User } from 'lucide-react';
import {
  postStructuredLearningQaChat,
  type StructuredLearningQaChatHistoryEntry,
} from '../lib/api/structuredLearningApi';

interface ChatTurn {
  role: 'Student' | 'Professor';
  text: string;
}

/**
 * Text tab of "Q&A with Professor" / Deep Dive's "Speak with your agent"
 * (2026-10-06) — default interaction mode everywhere VoiceTutorAgent is
 * offered (see VoiceTextModeToggle). Stateless on the backend, so this
 * component owns the running conversation and resends it every turn.
 */
export default function QaProfessorTextChat({
  structuredLearningSectionStepId,
  personId,
  exampleCardIndex,
}: {
  structuredLearningSectionStepId: string;
  personId?: string;
  exampleCardIndex?: number;
}) {
  const { t } = useTranslation();
  const [turns, setTurns] = useState<ChatTurn[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight });
  }, [turns, sending]);

  const handleSend = async () => {
    const message = input.trim();
    if (!message || sending) return;
    setInput('');
    setError(null);
    const history: StructuredLearningQaChatHistoryEntry[] = turns.map((turn) => ({ Role: turn.role, Text: turn.text }));
    setTurns((prev) => [...prev, { role: 'Student', text: message }]);
    setSending(true);
    try {
      const result = await postStructuredLearningQaChat(
        structuredLearningSectionStepId,
        message,
        history,
        personId,
        exampleCardIndex
      );
      setTurns((prev) => [...prev, { role: 'Professor', text: result.Message }]);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="mt-2 flex flex-col gap-2">
      {turns.length > 0 && (
        <div ref={listRef} className="max-h-72 space-y-2.5 overflow-y-auto rounded-xl border border-white/10 bg-black/20 p-3">
          {turns.map((turn, i) => (
            <div key={i} className={`flex items-start gap-2 ${turn.role === 'Student' ? 'flex-row-reverse' : ''}`}>
              <div
                className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full ${
                  turn.role === 'Student' ? 'bg-brand-500/20 text-brand-300' : 'bg-indigo-500/20 text-indigo-300'
                }`}
              >
                {turn.role === 'Student' ? <User className="h-3.5 w-3.5" /> : <Bot className="h-3.5 w-3.5" />}
              </div>
              <p
                className={`max-w-[85%] whitespace-pre-wrap rounded-2xl px-3 py-2 text-xs ${
                  turn.role === 'Student' ? 'bg-brand-500/15 text-brand-50' : 'bg-indigo-500/10 text-slate-200'
                }`}
              >
                {turn.text}
              </p>
            </div>
          ))}
          {sending && (
            <div className="flex items-center gap-2 text-xs text-slate-400">
              <Loader2 className="h-3.5 w-3.5 animate-spin" /> {t('voiceTextModeToggle.professorTyping')}
            </div>
          )}
        </div>
      )}

      {error && <p className="text-xs text-red-400">{error}</p>}

      <div className="flex items-center gap-2">
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault();
              void handleSend();
            }
          }}
          placeholder={t('voiceTextModeToggle.askPlaceholder') as string}
          className="input flex-1 text-sm"
        />
        <button
          type="button"
          onClick={() => void handleSend()}
          disabled={sending || !input.trim()}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-indigo-500 px-3 py-2 text-xs font-semibold text-white disabled:opacity-50"
        >
          {sending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
        </button>
      </div>
    </div>
  );
}
