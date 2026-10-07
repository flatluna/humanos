import { useEffect, useRef, useState } from 'react';
import { Send, Bot, Loader2 } from 'lucide-react';
import { requestStructuredLearningQaChat, type QaProfessorChatHistoryEntry } from '../api/runtimeSessionApi';

interface ChatMessage {
  role: 'Student' | 'Professor';
  text: string;
}

/**
 * Text tab of "Q&A with Professor" (2026-10-01) — the cheap/text sibling
 * of VoiceTutorAgent's Q&A mode. Same grounding as the voice tab (this
 * section's content + the student's full LEARNER CONTEXT, built
 * server-side by StructuredLearningQaProfessorChatService), just a plain
 * chat box instead of a live WebRTC call, so students who don't need audio
 * can ask questions without the voice session cost.
 */
export default function ProfessorTextChat({
  structuredLearningSectionStepId,
  personId,
  isEn,
  exampleCardIndex,
  compact,
  engramSessionId,
}: {
  structuredLearningSectionStepId: string;
  personId: string;
  isEn: boolean;
  /** DeepDive cards only (2026-10-02) — grounds the Professor in ONE
   * ExampleCard instead of the whole section, mirroring the Voice tab's
   * reviewOnly card path. */
  exampleCardIndex?: number;
  /** Shrinks the chat area — used when embedded inside a small DeepDive card. */
  compact?: boolean;
  /** The real engram visit (StructuredLearningEngramSession) this chat is
   * happening during (2026-10-02) — attaches reported token usage to that
   * session instead of a lifetime-per-person bucket. */
  engramSessionId?: string;
}) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  // Switching DeepDive cards starts a fresh conversation — the Professor
  // shouldn't carry over the previous card's chat history.
  useEffect(() => {
    setMessages([]);
    setInput('');
    setError(null);
  }, [structuredLearningSectionStepId, exampleCardIndex]);

  const handleSend = async () => {
    const trimmed = input.trim();
    if (!trimmed || sending) return;
    setError(null);
    setInput('');
    const nextMessages: ChatMessage[] = [...messages, { role: 'Student', text: trimmed }];
    setMessages(nextMessages);
    setSending(true);
    try {
      const history: QaProfessorChatHistoryEntry[] = messages.map((m) => ({ Role: m.role, Text: m.text }));
      const response = await requestStructuredLearningQaChat(structuredLearningSectionStepId, trimmed, history, personId, exampleCardIndex, engramSessionId);
      setMessages([...nextMessages, { role: 'Professor', text: response.Message }]);
      requestAnimationFrame(() => scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' }));
    } catch (err) {
      setError(err instanceof Error ? err.message : (isEn ? 'Could not reach the Professor.' : 'No se pudo contactar al Profesor.'));
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="flex flex-col rounded-2xl border border-indigo-200 bg-white/70 dark:border-indigo-500/20 dark:bg-slate-950/40">
      <div ref={scrollRef} className={`${compact ? 'h-48 min-h-[8rem] p-3' : 'h-[32rem] min-h-[20rem] p-6'} space-y-4 overflow-y-auto`}>
        {messages.length === 0 && (
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {isEn ? 'Type a question about this lecture below.' : 'Escribe abajo una pregunta sobre esta clase.'}
          </p>
        )}
        {messages.map((m, i) => (
          <div key={i} className={`max-w-[90%] ${m.role === 'Student' ? 'ml-auto' : ''}`}>
            <div
              className={`rounded-2xl px-5 py-3 text-sm leading-relaxed whitespace-pre-wrap ${
                m.role === 'Student'
                  ? 'bg-gradient-to-r from-indigo-600 to-purple-600 text-white'
                  : 'bg-slate-100 text-slate-800 dark:bg-white/[0.06] dark:text-slate-200'
              }`}
            >
              {m.text}
            </div>
          </div>
        ))}
        {sending && (
          <div className="flex items-center gap-1.5 text-sm text-slate-500 dark:text-slate-400">
            <Loader2 className="h-4 w-4 animate-spin" />
            {isEn ? 'The Professor is answering…' : 'El Profesor está respondiendo…'}
          </div>
        )}
        {error && <div className="rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-600 dark:border-red-400/20 dark:bg-red-500/[0.08] dark:text-red-200">{error}</div>}
      </div>
      <div className="flex items-center gap-2 border-t border-indigo-100 p-4 dark:border-indigo-500/10">
        <Bot className="h-4 w-4 shrink-0 text-indigo-500" />
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSend()}
          placeholder={isEn ? 'Ask the Professor…' : 'Pregúntale al Profesor…'}
          className="flex-1 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-800 outline-none focus:border-indigo-400 dark:border-white/10 dark:bg-slate-900 dark:text-slate-100"
        />
        <button
          type="button"
          onClick={handleSend}
          disabled={sending || !input.trim()}
          className="inline-flex shrink-0 items-center justify-center rounded-xl bg-gradient-to-r from-indigo-600 to-purple-600 p-3 text-white shadow-md shadow-indigo-500/20 transition-all hover:scale-[1.03] active:scale-[0.98] disabled:opacity-40 disabled:hover:scale-100"
        >
          <Send className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
