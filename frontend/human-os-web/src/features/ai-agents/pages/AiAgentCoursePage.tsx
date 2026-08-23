import { useEffect, useState } from 'react';
import { ArrowLeft, Bot, Eraser, Keyboard, Loader2, Mic, Send, Sparkles } from 'lucide-react';
import { Link, useParams } from 'react-router';
import { useAuth } from '@/auth/AuthContext';
import { getStructuredLearningDocuments, type StructuredLearningDocumentSummary } from '@/features/capabilities/api/structuredLearningApi';
import { getStructuredLearningDocument, type StructuredLearningStepDetail } from '@/features/capabilities/api/structuredLearningRuntimeApi';
import VoiceTutorAgent from '@/features/capabilities/components/VoiceTutorAgent';
import { apiPost } from '@/features/capabilities/api/httpClient';
import { EngramModal, MessageBody, type UsedEngram } from '@/features/ai-agents/components/ChatUi';

type AgentMode = 'text' | 'voice';

interface ChatResponse {
  message: string;
  usedEngrams?: UsedEngram[];
}

const buildGreeting = (documentName: string, userName?: string) =>
  `Hola${userName ? `, ${userName}` : ''}. ¿Cómo estás? ¿En qué puedo ayudarte? Soy tu agente experto en los Engrams de ${documentName}.`;

export default function AiAgentCoursePage() {
  const { documentId } = useParams<{ documentId: string }>();
  const { user } = useAuth();
  const [course, setCourse] = useState<StructuredLearningDocumentSummary | null>(null);
  const [voiceStep, setVoiceStep] = useState<StructuredLearningStepDetail | null>(null);
  const [messages, setMessages] = useState<{ role: 'student' | 'agent'; text: string; usedEngrams?: UsedEngram[] }[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [openEngram, setOpenEngram] = useState<UsedEngram | null>(null);
  // Voice and text are mutually exclusive (explicit user request): only one
  // agent "mode" is ever active. Voice mode uses the real-time agent's own
  // neural voice end-to-end; text mode is pure silent text — no more
  // browser `speechSynthesis` reading replies out loud.
  const [mode, setMode] = useState<AgentMode>('text');

  useEffect(() => {
    getStructuredLearningDocuments().then((courses) => {
      const selected = courses.find((item) => item.structuredLearningDocumentId === documentId);
      if (!selected) setError('Este agente no está disponible para estudiantes.');
      else {
        setCourse(selected);
        setMessages([{ role: 'agent', text: buildGreeting(selected.documentName, user?.name) }]);
        getStructuredLearningDocument(selected.structuredLearningDocumentId)
          .then((detail) => {
            const steps = detail.Sections.flatMap((section) => section.Steps);
            setVoiceStep(steps.find((step) => step.StepType === 'Teaching')
              ?? steps.find((step) => step.StepType === 'Hypothesis' || step.StepType === 'DeepDive')
              ?? null);
          })
          .catch(() => setVoiceStep(null));
      }
    }).catch(() => setError('No se pudo cargar el agente.'));
  }, [documentId]);

  async function sendMessage(messageText?: string) {
    const text = (messageText ?? input).trim();
    if (!text || !course || sending) return;
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
        documentId: course.structuredLearningDocumentId,
      });
      setMessages((current) => [...current, { role: 'agent', text: response.message, usedEngrams: response.usedEngrams }]);
    } catch {
      setError('El agente no pudo responder en este momento.');
    } finally {
      setSending(false);
    }
  }

  function clearChat() {
    if (!course) return;
    setMessages([{ role: 'agent', text: buildGreeting(course.documentName, user?.name) }]);
  }

  // Switching between Voz/Texto starts a fresh conversation (explicit user
  // request) — the two channels don't share turns mid-conversation, so
  // leaving old messages on screen after switching reads as if the OTHER
  // channel is still listening, which it isn't.
  function handleModeChange(nextMode: AgentMode) {
    setMode(nextMode);
    clearChat();
  }

  return (
    <div className="fixed inset-0 z-30 flex min-h-0 w-full flex-col overflow-hidden bg-gradient-to-br from-slate-50 via-cyan-50/30 to-indigo-50/40 pt-16 dark:bg-slate-950 dark:bg-[radial-gradient(circle_at_top_right,rgba(34,211,238,0.12),transparent_35%),radial-gradient(circle_at_bottom_left,rgba(99,102,241,0.12),transparent_40%)] lg:left-64 lg:w-[calc(100%-16rem)]">
      <div className="mx-auto flex min-h-0 w-full max-w-5xl flex-1 flex-col overflow-hidden px-3 py-3 sm:px-6 sm:py-5">
      <Link to="/ai-agents" className="inline-flex shrink-0 items-center gap-1.5 text-sm text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white"><ArrowLeft className="h-4 w-4" /> AI Agents</Link>
      {error && <div className="mt-6 rounded-xl border border-red-400/20 bg-red-500/[0.08] p-4 text-sm text-red-200">{error}</div>}
      {course && <header className="mt-4 flex shrink-0 items-center gap-3 rounded-2xl border border-cyan-200/70 bg-gradient-to-r from-cyan-500/10 via-indigo-500/10 to-violet-500/10 px-4 py-3 shadow-sm shadow-cyan-500/10 dark:border-cyan-300/15 dark:from-cyan-400/10 dark:via-indigo-400/10 dark:to-violet-400/10"><div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-cyan-300 to-indigo-400 text-slate-950 shadow-lg shadow-cyan-500/20"><Bot className="h-6 w-6" /></div><div className="min-w-0 flex-1"><p className="text-xs font-semibold uppercase tracking-[0.16em] text-cyan-600 dark:text-cyan-300">Engram Agent</p><h1 className="truncate text-lg font-bold text-slate-900 dark:text-white">{course.documentName}</h1></div>
        {voiceStep && (
          <div className="flex shrink-0 items-center gap-1 rounded-full border border-cyan-200/70 bg-white/70 p-1 dark:border-cyan-300/15 dark:bg-white/[0.05]">
            <button type="button" onClick={() => handleModeChange('text')} className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition ${mode === 'text' ? 'bg-gradient-to-r from-cyan-400 to-indigo-400 text-slate-950 shadow-sm' : 'text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white'}`}><Keyboard className="h-3.5 w-3.5" /> Texto</button>
            <button type="button" onClick={() => handleModeChange('voice')} className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition ${mode === 'voice' ? 'bg-gradient-to-r from-cyan-400 to-indigo-400 text-slate-950 shadow-sm' : 'text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white'}`}><Mic className="h-3.5 w-3.5" /> Voz</button>
          </div>
        )}
        <button type="button" onClick={clearChat} title="Limpiar conversación" className="flex shrink-0 items-center gap-1.5 rounded-full border border-cyan-200/70 bg-white/70 px-3 py-1.5 text-xs font-semibold text-slate-500 transition hover:border-red-300 hover:text-red-600 dark:border-cyan-300/15 dark:bg-white/[0.05] dark:text-slate-400 dark:hover:text-red-400"><Eraser className="h-3.5 w-3.5" /> Limpiar</button>
      </header>}
      {course && voiceStep && mode === 'voice' && (
        <div className="mt-3 shrink-0 rounded-2xl border border-violet-200/70 bg-violet-50/70 p-3 dark:border-violet-300/15 dark:bg-violet-950/20">
          <div className="mb-2 flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.14em] text-violet-700 dark:text-violet-300"><Sparkles className="h-3.5 w-3.5" /> Agente de voz</div>
          <VoiceTutorAgent
            structuredLearningSectionStepId={voiceStep.StructuredLearningSectionStepId}
            isQaMode
            onTranscript={(transcript) => setMessages((current) => [...current, { role: 'student', text: transcript }])}
            onAgentTranscript={(transcript) => setMessages((current) => [...current, { role: 'agent', text: transcript }])}
            compact={false}
          />
        </div>
      )}

      <div className="mt-3 min-h-0 flex-1 overflow-y-auto overscroll-contain pr-1 [scrollbar-gutter:stable]">
        <div className="space-y-4">
        {messages.length === 0 && course && <div className="mt-8 rounded-2xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-white/15 dark:text-slate-400">Pregúntale al agente sobre este curso.</div>}
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
      {mode === 'text' && <div className="mt-3 flex shrink-0 gap-2 border-t border-slate-200/80 pt-3 dark:border-white/10"><input value={input} onChange={(event) => setInput(event.target.value)} onKeyDown={(event) => event.key === 'Enter' && void sendMessage()} disabled={!course || sending} placeholder="Escribe tu pregunta..." className="min-w-0 flex-1 rounded-xl border border-cyan-200 bg-white/85 px-3 py-3 text-sm outline-none transition focus:border-indigo-400 focus:ring-2 focus:ring-cyan-400/20 dark:border-white/10 dark:bg-white/[0.05] dark:text-white sm:px-4" /><button type="button" onClick={() => void sendMessage()} disabled={!course || sending || !input.trim()} aria-label="Enviar pregunta" className="flex shrink-0 items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-cyan-300 to-indigo-300 px-3 py-3 text-sm font-semibold text-slate-950 shadow-lg shadow-cyan-500/20 transition hover:from-cyan-200 hover:to-indigo-200 disabled:opacity-50 sm:px-4"><Send className="h-4 w-4" /><span className="hidden sm:inline">Enviar</span></button></div>}
      </div>
      {openEngram && <EngramModal engram={openEngram} onClose={() => setOpenEngram(null)} />}
    </div>
  );
}
