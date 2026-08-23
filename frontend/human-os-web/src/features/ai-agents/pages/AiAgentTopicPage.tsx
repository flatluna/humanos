import { useEffect, useState } from 'react';
import { ArrowLeft, Bot, Eraser, Loader2, Send, Sparkles } from 'lucide-react';
import { Link, useParams, useSearchParams } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { apiPost } from '@/features/capabilities/api/httpClient';
import { EngramModal, MessageBody, type UsedEngram } from '@/features/ai-agents/components/ChatUi';

interface ChatResponse {
  message: string;
  usedEngrams?: UsedEngram[];
}

const buildGreeting = (topicName: string, userName?: string) =>
  `Hola${userName ? `, ${userName}` : ''}. ¿Cómo estás? ¿En qué puedo ayudarte? Soy tu agente experto en ${topicName}. Puedo responder preguntas de cualquiera de sus cursos, conectar conceptos entre ellos y recomendarte una ruta de aprendizaje.`;

/**
 * Domain/Subdomain-level "AI Agents" chat (2026-08-18) — a general tutor
 * scoped to a whole Subdomain (e.g. Matemáticas/Álgebra), not a single
 * course, mirroring capabilitystudio's AiAgentsChatPage.tsx topic-level
 * path. Grounded server-side via the SAME `/studio/structured-learning/
 * chat` endpoint AiAgentCoursePage uses, just passing subjectId/topicId
 * instead of documentId — StructuredLearningChatService retrieves across
 * every course of that Subdomain instead of one.
 */
export default function AiAgentTopicPage() {
  const { subjectId, topicId } = useParams<{ subjectId: string; topicId: string }>();
  const [searchParams] = useSearchParams();
  const domainName = searchParams.get('domain') ?? 'este dominio';
  const subdomainName = searchParams.get('subdomain') ?? 'este subdominio';
  const { user } = useAuth();

  const [messages, setMessages] = useState<{ role: 'student' | 'agent'; text: string; usedEngrams?: UsedEngram[] }[]>(
    [{ role: 'agent', text: buildGreeting(subdomainName, user?.name) }]
  );
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [openEngram, setOpenEngram] = useState<UsedEngram | null>(null);

  useEffect(() => {
    setMessages([{ role: 'agent', text: buildGreeting(subdomainName, user?.name) }]);
  }, [subjectId, topicId, subdomainName, user?.name]);

  async function sendMessage() {
    const text = input.trim();
    if (!text || !subjectId || !topicId || sending) return;
    const history = messages.map((message) => ({ Role: message.role === 'student' ? 'Student' : 'Agent', Text: message.text }));
    setInput('');
    setMessages((current) => [...current, { role: 'student', text }]);
    setSending(true);
    try {
      const response = await apiPost<ChatResponse>('/studio/structured-learning/chat', {
        tenantId: user?.tenantId ?? '00000000-0000-0000-0000-000000000000',
        personId: user?.personId,
        message: text,
        history,
        subjectId,
        topicId,
      });
      setMessages((current) => [...current, { role: 'agent', text: response.message, usedEngrams: response.usedEngrams }]);
    } catch {
      setError('El agente no pudo responder en este momento.');
    } finally {
      setSending(false);
    }
  }

  function clearChat() {
    setMessages([{ role: 'agent', text: buildGreeting(subdomainName, user?.name) }]);
  }

  return (
    <div className="fixed inset-0 z-30 flex min-h-0 w-full flex-col overflow-hidden bg-gradient-to-br from-slate-50 via-cyan-50/30 to-indigo-50/40 pt-16 dark:bg-slate-950 dark:bg-[radial-gradient(circle_at_top_right,rgba(34,211,238,0.12),transparent_35%),radial-gradient(circle_at_bottom_left,rgba(99,102,241,0.12),transparent_40%)] lg:left-64 lg:w-[calc(100%-16rem)]">
      <div className="mx-auto flex min-h-0 w-full max-w-5xl flex-1 flex-col overflow-hidden px-3 py-3 sm:px-6 sm:py-5">
        <Link to="/ai-agents" className="inline-flex shrink-0 items-center gap-1.5 text-sm text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> AI Agents</Link>
        {error && <div className="mt-6 rounded-xl border border-red-400/20 bg-red-500/[0.08] p-4 text-sm text-red-200">{error}</div>}
        <header className="mt-4 flex shrink-0 items-center gap-3 rounded-2xl border border-cyan-200/70 bg-gradient-to-r from-cyan-500/10 via-indigo-500/10 to-violet-500/10 px-4 py-3 shadow-sm shadow-cyan-500/10 dark:border-cyan-300/15 dark:from-cyan-400/10 dark:via-indigo-400/10 dark:to-violet-400/10">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-cyan-300 to-indigo-400 text-slate-950 shadow-lg shadow-cyan-500/20"><Bot className="h-6 w-6" /></div>
          <div className="min-w-0 flex-1">
            <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-cyan-600 dark:text-cyan-300"><Sparkles className="h-3.5 w-3.5" /> Agente de {domainName}</p>
            <h1 className="truncate text-lg font-bold text-slate-900 dark:text-white">{subdomainName}</h1>
          </div>
          <button type="button" onClick={clearChat} title="Limpiar conversación" className="flex shrink-0 items-center gap-1.5 rounded-full border border-cyan-200/70 bg-white/70 px-3 py-1.5 text-xs font-semibold text-slate-500 transition hover:border-red-300 hover:text-red-600 dark:border-cyan-300/15 dark:bg-white/[0.05] dark:text-slate-400 dark:hover:text-red-400"><Eraser className="h-3.5 w-3.5" /> Limpiar</button>
        </header>

        <div className="mt-3 min-h-0 flex-1 overflow-y-auto overscroll-contain pr-1 [scrollbar-gutter:stable]">
          <div className="space-y-4">
            {messages.map((message, index) => (
              <div key={index} className={`max-w-[85%] sm:max-w-[75%] ${message.role === 'student' ? 'ml-auto' : ''}`}>
                <div className={`rounded-2xl px-4 py-3 text-sm leading-6 shadow-sm ${message.role === 'student' ? 'rounded-br-md bg-gradient-to-br from-cyan-400 to-indigo-400 text-slate-950 shadow-cyan-500/15' : 'rounded-bl-md border border-indigo-100 bg-white/80 text-slate-700 dark:border-indigo-300/10 dark:bg-indigo-950/35 dark:text-slate-200'}`}><MessageBody text={message.text} /></div>
                {message.role === 'agent' && message.usedEngrams && message.usedEngrams.length > 0 && (
                  <div className="mt-2 rounded-xl border border-cyan-200/60 bg-cyan-50/70 p-3 dark:border-cyan-300/10 dark:bg-cyan-950/25">
                    <p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-cyan-700 dark:text-cyan-300">Engrams utilizados</p>
                    <div className="mt-2 flex flex-wrap gap-1.5">
                      {message.usedEngrams.map((engram) => <button key={engram.engramId} type="button" onClick={() => setOpenEngram(engram)} title={engram.courseName} className="rounded-full border border-cyan-300/50 bg-white/80 px-2.5 py-1 text-left text-[11px] font-medium text-cyan-800 transition hover:border-cyan-500 hover:bg-cyan-100 dark:border-cyan-300/20 dark:bg-cyan-400/10 dark:text-cyan-200 dark:hover:bg-cyan-400/20">📖 {engram.title}</button>)}
                    </div>
                  </div>
                )}
              </div>
            ))}
            {sending && (
              <div className="flex max-w-[85%] items-center gap-3 rounded-2xl rounded-bl-md border border-cyan-200/70 bg-white/80 px-4 py-3 text-sm text-slate-500 shadow-sm dark:border-cyan-300/15 dark:bg-indigo-950/35 dark:text-slate-300">
                <span className="relative flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-cyan-300 to-indigo-400 text-slate-950 shadow-md shadow-cyan-400/30">
                  <Bot className="relative z-10 h-4 w-4" />
                  <Loader2 className="absolute -inset-1 h-9 w-9 animate-spin text-cyan-300/70" strokeWidth={1.5} />
                  <Sparkles className="absolute -right-2 -top-2 h-3.5 w-3.5 animate-pulse text-violet-400" />
                </span>
                <span className="font-medium text-slate-600 dark:text-slate-200">Pensando</span>
                <span className="flex gap-1" aria-label="El agente está pensando">
                  <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-cyan-400 [animation-delay:-0.3s]" />
                  <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-indigo-400 [animation-delay:-0.15s]" />
                  <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-violet-400" />
                </span>
              </div>
            )}
          </div>
        </div>
        <div className="mt-3 flex shrink-0 gap-2 border-t border-slate-200/80 pt-3 dark:border-white/10">
          <input value={input} onChange={(event) => setInput(event.target.value)} onKeyDown={(event) => event.key === 'Enter' && void sendMessage()} disabled={sending} placeholder="Escribe tu pregunta..." className="min-w-0 flex-1 rounded-xl border border-cyan-200 bg-white/85 px-3 py-3 text-sm outline-none transition focus:border-indigo-400 focus:ring-2 focus:ring-cyan-400/20 dark:border-white/10 dark:bg-white/[0.05] dark:text-white sm:px-4" />
          <button type="button" onClick={() => void sendMessage()} disabled={sending || !input.trim()} aria-label="Enviar pregunta" className="flex shrink-0 items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-cyan-300 to-indigo-300 px-3 py-3 text-sm font-semibold text-slate-950 shadow-lg shadow-cyan-500/20 transition hover:from-cyan-200 hover:to-indigo-200 disabled:opacity-50 sm:px-4"><Send className="h-4 w-4" /><span className="hidden sm:inline">Enviar</span></button>
        </div>
      </div>
      {openEngram && <EngramModal engram={openEngram} onClose={() => setOpenEngram(null)} />}
    </div>
  );
}
