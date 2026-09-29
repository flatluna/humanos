import { useRef, useState } from 'react';
import { Bot, Loader2, Send, Sparkles, X } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { postTecContigoChat, type TecContigoChatHistoryEntry } from './api/tecContigoChatApi';

interface ChatMessage {
  role: 'Student' | 'Agent';
  text: string;
}

/**
 * "tecBot" — Tec Contigo's student companion chat (2026-09-28). Grounded
 * server-side in the student's own Intereses, profile, enrolled
 * courses/grades and the real course catalog (see TecContigoAgent). Plain
 * text, multi-turn — the widget just keeps and resends the running
 * history each turn, same convention as the course AI Agents chat.
 */
interface TecContigoAgentChatProps {
  /** Externally controlled open state (e.g. a "Habla con tecBot" button
   *  elsewhere on the page). When omitted, the widget manages its own
   *  open state and renders its own floating trigger button. */
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}

export function TecContigoAgentChat({ open, onOpenChange }: TecContigoAgentChatProps = {}) {
  const { user } = useAuth();
  const [internalOpen, setInternalOpen] = useState(false);
  const isControlled = open !== undefined;
  const isOpen = isControlled ? open : internalOpen;
  const setIsOpen = (next: boolean) => {
    if (isControlled) onOpenChange?.(next);
    else setInternalOpen(next);
  };
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      role: 'Agent',
      text: `Hola${user?.name ? `, ${user.name}` : ''}. Soy Buddy 👋 Puedo ayudarte a encontrar cursos según tus intereses, resolver dudas, recomendarte tu siguiente curso según tus calificaciones, y acompañarte en todas las etapas de tu vida con el Tec. ¿En qué te ayudo hoy?`,
    },
  ]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  async function sendMessage() {
    const text = input.trim();
    if (!text || sending) return;
    const history: TecContigoChatHistoryEntry[] = messages.map((m) => ({ role: m.role, text: m.text }));
    setInput('');
    setError(null);
    setMessages((current) => [...current, { role: 'Student', text }]);
    setSending(true);
    try {
      const reply = await postTecContigoChat(user?.personId, text, history);
      setMessages((current) => [...current, { role: 'Agent', text: reply }]);
    } catch {
      setError('Buddy no pudo responder en este momento. Intenta de nuevo.');
    } finally {
      setSending(false);
      requestAnimationFrame(() => scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' }));
    }
  }

  if (!isOpen) {
    // Controlled mode (open is driven by a caller's own button) renders nothing when closed.
    if (isControlled) return null;
    return (
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        className="fixed bottom-6 right-6 z-50 flex items-center gap-2 rounded-full bg-gradient-to-r from-emerald-500 to-teal-600 px-5 py-3 text-sm font-semibold text-white shadow-lg shadow-emerald-600/30 transition hover:scale-105"
      >
        <Sparkles className="h-4 w-4" aria-hidden="true" />
        Habla con Buddy
      </button>
    );
  }

  return (
    <div className="fixed bottom-6 right-6 z-50 flex h-[32rem] w-[24rem] max-w-[calc(100vw-3rem)] flex-col overflow-hidden rounded-3xl border border-emerald-200 bg-white shadow-2xl dark:border-emerald-400/20 dark:bg-slate-950">
      <div className="flex shrink-0 items-center justify-between gap-2 border-b border-emerald-100 bg-emerald-50/70 px-4 py-3 dark:border-emerald-400/10 dark:bg-emerald-400/[0.06]">
        <div className="flex items-center gap-2">
          <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br from-emerald-500 to-teal-600 text-white">
            <Bot className="h-4 w-4" aria-hidden="true" />
          </span>
          <p className="text-sm font-bold text-slate-950 dark:text-white">Buddy</p>
        </div>
        <button
          type="button"
          onClick={() => setIsOpen(false)}
          aria-label="Cerrar"
          className="rounded-full p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
        >
          <X className="h-4 w-4" />
        </button>
      </div>

      <div ref={scrollRef} className="min-h-0 flex-1 space-y-3 overflow-y-auto px-4 py-3">
        {messages.map((message, index) => (
          <div key={index} className={`max-w-[85%] ${message.role === 'Student' ? 'ml-auto' : ''}`}>
            <div
              className={`rounded-2xl px-3.5 py-2.5 text-sm leading-6 shadow-sm ${
                message.role === 'Student'
                  ? 'rounded-br-md bg-gradient-to-br from-emerald-500 to-teal-600 text-white'
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
            <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" /> Buddy está pensando…
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
          placeholder="Escríbele a Buddy..."
          className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none transition focus:border-emerald-400 disabled:opacity-60 dark:border-white/10 dark:bg-white/[0.04] dark:text-white"
        />
        <button
          type="button"
          onClick={() => void sendMessage()}
          disabled={sending || !input.trim()}
          aria-label="Enviar"
          className="flex shrink-0 items-center justify-center rounded-xl bg-gradient-to-r from-emerald-500 to-teal-600 px-3 py-2.5 text-white shadow-sm transition disabled:opacity-50"
        >
          <Send className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
