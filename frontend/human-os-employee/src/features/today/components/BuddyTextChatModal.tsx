import { useRef, useState } from 'react';
import { Bot, Loader2, Send, X } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { postBuddyChat, type BuddyChatHistoryEntry } from '../api/buddyApi';

interface ChatMessage {
  role: 'student' | 'agent';
  text: string;
}

interface BuddyTextChatModalProps {
  open: boolean;
  onClose: () => void;
  personName?: string;
  language: 'en' | 'es';
}

/**
 * Buddy's TEXT companion (2026-10-04) — same backend harness as the voice
 * Buddy (BuddyAgentService/BuddyChatFunction): dynamic per-skill tool
 * dispatch, grounded in the employee's own real profile/courses/objectives/
 * achievements/career plan/projects, nothing hardcoded. Mirrors tecBot's own
 * "text + voice" pairing for students (TecContigoAgentChat/TecContigoVoiceModal)
 * — offering text alongside Buddy's existing voice option lets employees pick
 * the cheaper, equally-capable channel for quick questions, reserving the
 * Realtime voice session for when they actually want to talk.
 */
export function BuddyTextChatModal({ open, onClose, personName, language }: BuddyTextChatModalProps) {
  const isEn = language === 'en';
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      role: 'agent',
      text: isEn
        ? `Hi${personName ? `, ${personName}` : ''}! I'm Buddy. Ask me about your role, courses, objectives, achievements, career plan, or hands-on projects.`
        : `¡Hola${personName ? `, ${personName}` : ''}! Soy Buddy. Pregúntame sobre tu rol, tus cursos, objetivos, logros, plan de carrera o proyectos prácticos.`,
    },
  ]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  if (!open) return null;

  async function sendMessage() {
    const text = input.trim();
    if (!text || sending) return;
    const history: BuddyChatHistoryEntry[] = messages;
    setInput('');
    setError(null);
    setMessages((current) => [...current, { role: 'student', text }]);
    setSending(true);
    try {
      const response = await postBuddyChat(text, history, language);
      setMessages((current) => [...current, { role: 'agent', text: response.message }]);
    } catch {
      setError(isEn ? 'Buddy could not answer right now. Try again.' : 'Buddy no pudo responder en este momento. Intenta de nuevo.');
      setInput(text);
    } finally {
      setSending(false);
      requestAnimationFrame(() => scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' }));
    }
  }

  return (
    <div className="fixed inset-0 z-[70] flex items-end justify-center bg-slate-950/40 backdrop-blur-sm sm:items-center">
      <div className="flex h-[32rem] w-full max-w-[24rem] flex-col overflow-hidden rounded-t-3xl border border-blue-200 bg-white shadow-2xl dark:border-blue-400/20 dark:bg-slate-950 sm:rounded-3xl">
        <div className="flex shrink-0 items-center justify-between gap-2 border-b border-blue-100 bg-blue-50/70 px-4 py-3 dark:border-blue-400/10 dark:bg-blue-400/[0.06]">
          <div className="flex items-center gap-2">
            <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 text-white">
              <Bot className="h-4 w-4" aria-hidden="true" />
            </span>
            <p className="text-sm font-bold text-slate-950 dark:text-white">Buddy</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={isEn ? 'Close' : 'Cerrar'}
            className="rounded-full p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div ref={scrollRef} className="min-h-0 flex-1 space-y-3 overflow-y-auto px-4 py-3">
          {messages.map((message, index) => (
            <div key={index} className={`max-w-[85%] ${message.role === 'student' ? 'ml-auto' : ''}`}>
              <div
                className={`rounded-2xl px-3.5 py-2.5 text-sm leading-6 shadow-sm ${
                  message.role === 'student'
                    ? 'rounded-br-md bg-gradient-to-br from-blue-500 to-indigo-600 text-white'
                    : 'rounded-bl-md border border-slate-200 bg-slate-50 text-slate-700 dark:border-white/10 dark:bg-white/[0.04] dark:text-white/85'
                }`}
                style={{ whiteSpace: 'pre-wrap' }}
              >
                {message.text}
              </div>
            </div>
          ))}
          {sending && (
            <div className="flex items-center gap-2 text-xs font-medium text-slate-400 dark:text-white/45">
              <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />
              {isEn ? 'Buddy is thinking…' : 'Buddy está pensando…'}
            </div>
          )}
          {error && (
            <Card className="border-rose-200 bg-rose-50 p-2.5 text-xs font-medium text-rose-700 dark:border-rose-400/25 dark:bg-rose-400/10 dark:text-rose-300">
              {error}
            </Card>
          )}
        </div>

        <div className="flex shrink-0 gap-2 border-t border-slate-200 p-3 dark:border-white/10">
          <input
            value={input}
            onChange={(event) => setInput(event.target.value)}
            onKeyDown={(event) => event.key === 'Enter' && void sendMessage()}
            disabled={sending}
            placeholder={isEn ? 'Ask Buddy...' : 'Escríbele a Buddy...'}
            className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none transition focus:border-blue-400 disabled:opacity-60 dark:border-white/10 dark:bg-white/[0.04] dark:text-white"
          />
          <button
            type="button"
            onClick={() => void sendMessage()}
            disabled={sending || !input.trim()}
            aria-label={isEn ? 'Send' : 'Enviar'}
            className="flex shrink-0 items-center justify-center rounded-xl bg-gradient-to-r from-blue-500 to-indigo-600 px-3 py-2.5 text-white shadow-sm transition disabled:opacity-50"
          >
            <Send className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  );
}
