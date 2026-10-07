import { useEffect, useRef, useState } from 'react';
import { Bot, Eraser, Loader2, Send, Sparkles, X } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { AgentMessageContent } from '@/features/ai-agents/components/AgentMessageContent';
import {
  postAiAgentChat,
  type AiAgentChatHistoryEntry,
} from '@/features/ai-agents/api/aiAgentsApi';

interface ChatMessage {
  role: 'student' | 'agent';
  text: string;
}

export function EngramTextAgent({
  documentId,
  engramId,
  title,
  onClose,
  engramSessionId,
}: {
  documentId: string;
  engramId: string;
  title: string;
  onClose: () => void;
  engramSessionId?: string;
}) {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      role: 'agent',
      text: isEn
        ? `I'm your text agent for "${title}". Ask me about this Engram.`
        : `Soy tu agente de texto para "${title}". Pregúntame sobre este Engram.`,
    },
  ]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, sending]);

  function clearChat() {
    setMessages([{ role: 'agent', text: isEn ? `Ask me about "${title}".` : `Pregúntame sobre "${title}".` }]);
  }

  async function sendMessage() {
    const text = input.trim();
    if (!text || sending) return;
    const history: AiAgentChatHistoryEntry[] = messages.slice(-6).map((message) => ({
      role: message.role === 'student' ? 'Student' : 'Agent',
      text: message.text,
    }));
    setInput('');
    setMessages((previous) => [...previous, { role: 'student', text }]);
    setSending(true);
    setError(null);
    try {
      const response = await postAiAgentChat(
        user?.tenantId ?? '',
        user?.personId ?? '',
        documentId,
        text,
        history,
        engramId,
        engramSessionId,
      );
      setMessages((previous) => [...previous, { role: 'agent', text: response.message }]);
    } catch {
      setError(isEn ? 'The agent could not answer right now.' : 'El agente no pudo responder en este momento.');
      setMessages((previous) => previous.slice(0, -1));
      setInput(text);
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm">
      <div className="flex h-[min(700px,90vh)] w-full max-w-2xl flex-col rounded-3xl border border-indigo-500/30 bg-slate-900 shadow-2xl">
        <header className="flex shrink-0 items-center gap-3 border-b border-white/10 px-5 py-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-gradient-to-br from-cyan-500 to-blue-600 text-white">
            <Bot className="h-5 w-5" />
          </div>
          <div className="min-w-0 flex-1">
            <p className="flex items-center gap-1 text-xs font-semibold uppercase tracking-wide text-cyan-400">
              <Sparkles className="h-3.5 w-3.5" /> {isEn ? 'Engram text agent' : 'Agente de texto del Engram'}
            </p>
            <h2 className="truncate font-bold text-white">{title}</h2>
          </div>
          <button type="button" onClick={clearChat} className="rounded-full p-2 text-white/60 hover:bg-white/10 hover:text-white" title={isEn ? 'Clear' : 'Limpiar'}>
            <Eraser className="h-4 w-4" />
          </button>
          <button type="button" onClick={onClose} className="rounded-full p-2 text-white/60 hover:bg-white/10 hover:text-white" aria-label={isEn ? 'Close' : 'Cerrar'}>
            <X className="h-5 w-5" />
          </button>
        </header>
        <div ref={scrollRef} className="min-h-0 flex-1 space-y-3 overflow-y-auto p-5">
          {messages.map((message, index) => (
            <div key={`${message.role}-${index}`} className={message.role === 'student' ? 'ml-auto max-w-[85%] rounded-2xl rounded-br-md bg-indigo-600 px-4 py-3 text-sm text-white' : 'max-w-[90%] rounded-2xl rounded-bl-md bg-white/10 px-4 py-3 text-sm text-slate-200'}>
              {message.role === 'agent' ? <AgentMessageContent text={message.text} /> : message.text}
            </div>
          ))}
          {sending && <Loader2 className="h-5 w-5 animate-spin text-cyan-400" />}
          {error && <p className="text-xs text-red-300">{error}</p>}
        </div>
        <form onSubmit={(event) => { event.preventDefault(); void sendMessage(); }} className="flex shrink-0 gap-2 border-t border-white/10 p-4">
          <input value={input} onChange={(event) => setInput(event.target.value)} placeholder={isEn ? 'Ask about this Engram...' : 'Pregunta sobre este Engram...'} className="min-w-0 flex-1 rounded-xl border border-white/10 bg-white/5 px-4 py-3 text-sm text-white outline-none placeholder:text-white/40 focus:border-cyan-400" disabled={sending} />
          <button type="submit" disabled={sending || !input.trim()} className="rounded-xl bg-cyan-600 px-4 text-white disabled:opacity-40" aria-label={isEn ? 'Send' : 'Enviar'}>
            <Send className="h-4 w-4" />
          </button>
        </form>
      </div>
    </div>
  );
}
