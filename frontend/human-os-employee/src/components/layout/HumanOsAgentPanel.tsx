import { useEffect, useRef, useState } from 'react';
import { Bot, Loader2, Send, X } from 'lucide-react';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { postBuddyChat, type BuddyChatHistoryEntry } from '@/features/today/api/buddyApi';
import { useHumanOsAgentPanelStore } from '@/store/useHumanOsAgentPanelStore';

interface Message {
  role: 'student' | 'agent';
  text: string;
}

export function HumanOsAgentPanel() {
  const isOpen = useHumanOsAgentPanelStore((state) => state.isOpen);
  const close = useHumanOsAgentPanelStore((state) => state.close);
  const { language } = useI18n();
  const isEn = language === 'en';
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isOpen && messages.length === 0) {
      setMessages([{ role: 'agent', text: isEn ? 'Hi! I am Buddy. How can I help you today?' : '¡Hola! Soy Buddy. ¿Cómo puedo ayudarte hoy?' }]);
    }
  }, [isOpen, isEn, messages.length]);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, sending]);

  useEffect(() => {
    if (!isOpen) return;
    const onEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') close(); };
    document.addEventListener('keydown', onEscape);
    document.body.style.overflow = 'hidden';
    return () => { document.removeEventListener('keydown', onEscape); document.body.style.overflow = ''; };
  }, [isOpen, close]);

  async function send() {
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
      setError(isEn ? 'Buddy could not answer right now.' : 'Buddy no pudo responder en este momento.');
      setInput(text);
    } finally {
      setSending(false);
    }
  }

  return (
    <>
      <div aria-hidden={!isOpen} onClick={close} className={`fixed inset-0 z-[64] bg-slate-950/50 backdrop-blur-sm transition-opacity ${isOpen ? 'opacity-100' : 'pointer-events-none opacity-0'}`} />
      <div role="dialog" aria-modal="true" aria-label="Buddy" className={`fixed inset-y-0 right-0 z-[65] flex w-full max-w-md transform flex-col bg-white shadow-2xl transition-transform dark:bg-slate-950 ${isOpen ? 'translate-x-0' : 'translate-x-full'}`}>
        <div className="flex shrink-0 items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-white/10">
          <div className="flex items-center gap-2"><Bot className="h-6 w-6 text-cyan-500" /><span className="font-semibold text-slate-900 dark:text-white">Buddy</span></div>
          <button type="button" onClick={close} aria-label={isEn ? 'Close' : 'Cerrar'}><X className="h-5 w-5" /></button>
        </div>
        <div ref={scrollRef} className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">
          {messages.map((message, index) => (
            <div key={`${message.role}-${index}`} className={`rounded-2xl px-3 py-2 text-sm ${message.role === 'student' ? 'ml-8 bg-cyan-600 text-white' : 'mr-8 bg-slate-100 text-slate-800 dark:bg-white/10 dark:text-white'}`}>{message.text}</div>
          ))}
          {sending && <Loader2 className="h-4 w-4 animate-spin text-cyan-500" />}
          {error && <p className="text-xs text-red-600">{error}</p>}
        </div>
        <form onSubmit={(event) => { event.preventDefault(); void send(); }} className="flex gap-2 border-t border-slate-200 p-3 dark:border-white/10">
          <input value={input} onChange={(event) => setInput(event.target.value)} placeholder={isEn ? 'Ask Buddy...' : 'Pregúntale a Buddy...'} className="min-w-0 flex-1 rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none dark:border-white/10 dark:bg-white/5 dark:text-white" />
          <button type="submit" disabled={sending} className="rounded-xl bg-cyan-600 p-2 text-white disabled:opacity-50"><Send className="h-4 w-4" /></button>
        </form>
      </div>
    </>
  );
}
