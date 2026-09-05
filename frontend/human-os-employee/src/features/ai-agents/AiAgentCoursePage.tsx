import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import { ArrowLeft, Bot, Eraser, Loader2, Send, Sparkles } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import { EngramDetailPanel } from '@/features/brain/EngramDetailPanel';
import VoiceTutorAgent from '@/features/capabilities/components/VoiceTutorAgent';
import type { SelectedNeuron } from '@/features/brain/types';
import { postAiAgentChat, type AiAgentChatHistoryEntry, type AiAgentUsedEngram } from './api/aiAgentsApi';

interface ChatMessage {
  role: 'student' | 'agent';
  text: string;
  usedEngrams?: AiAgentUsedEngram[];
  /** The student's question this answer responds to — needed to ground the
   * "Talk about this" voice follow-up in the SAME Q&A the text chat produced. */
  question?: string;
}

/** One course's chat agent — grounded ONLY in this course's own engrams
 * (StructuredLearningChatService's Knowledge Graph retrieval, semantic/
 * vector entity match, never a raw dump of every engram), and aware of
 * THIS student's own objectives/skills/progress for this course via
 * personId (BuildStudentContextAsync server-side). */
export function AiAgentCoursePage() {
  const { documentId } = useParams<{ documentId: string }>();
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const tenantId = user?.tenantId ?? '';

  const [documentName, setDocumentName] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [openEngram, setOpenEngram] = useState<AiAgentUsedEngram | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  function greeting(name: string): string {
    return isEn
      ? `Hi${user?.name ? `, ${user.name}` : ''}. I'm your expert agent for "${name}". Ask me anything about this course.`
      : `Hola${user?.name ? `, ${user.name}` : ''}. Soy tu agente experto en "${name}". Pregúntame lo que quieras sobre este curso.`;
  }

  /** Reuses the EXACT same tabbed detail panel the Brain page opens for a
   * mastered engram (Learn / Memory state / Rewards / Cards / Things to remember)
   * — built from the cited AiAgentUsedEngram's ids, no new modal. */
  function toSelectedNeuron(engram: AiAgentUsedEngram): SelectedNeuron {
    return {
      capabilityGraphNodeId: engram.engramId,
      name: engram.title,
      state: 'Mastered',
      isStructuredLearning: true,
      structuredLearningDocumentId: engram.documentId,
      structuredLearningSectionId: engram.engramId,
      capabilityId: engram.documentId,
      capabilityName: engram.courseName,
    };
  }

  useEffect(() => {
    if (!personId || !documentId) return;
    getEnrolledStructuredLearningDocuments(personId)
      .then((docs) => {
        const found = docs.find((d) => d.structuredLearningDocumentId === documentId);
        if (!found) {
          setNotFound(true);
          return;
        }
        setDocumentName(found.documentName);
        setMessages([{ role: 'agent', text: greeting(found.documentName) }]);
      })
      .catch(() => setNotFound(true));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [personId, documentId]);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, sending]);

  function clearChat() {
    if (!documentName) return;
    setMessages([{ role: 'agent', text: greeting(documentName) }]);
  }

  async function sendMessage() {
    const text = input.trim();
    if (!text || !documentId || sending) return;
    const history: AiAgentChatHistoryEntry[] = messages.map((m) => ({ role: m.role === 'student' ? 'Student' : 'Agent', text: m.text }));
    setInput('');
    setMessages((prev) => [...prev, { role: 'student', text }]);
    setSending(true);
    setError(null);
    try {
      const response = await postAiAgentChat(tenantId, personId, documentId, text, history);
      setMessages((prev) => [...prev, { role: 'agent', text: response.message, usedEngrams: response.usedEngrams, question: text }]);
    } catch {
      setError(isEn ? 'The agent could not answer right now.' : 'El agente no pudo responder en este momento.');
      setMessages((prev) => prev.slice(0, -1));
      setInput(text);
    } finally {
      setSending(false);
    }
  }

  if (notFound) {
    return (
      <div className="mx-auto max-w-2xl p-8 text-center">
        <p className="text-sm text-slate-500 dark:text-white/60">
          {isEn ? 'This agent is not available for you.' : 'Este agente no está disponible para ti.'}
        </p>
        <Link to="/ai-agents" className="mt-4 inline-flex items-center gap-1.5 text-sm font-semibold text-brand-600 dark:text-brand-300">
          <ArrowLeft className="h-4 w-4" /> AI Agents
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto flex h-[calc(100dvh-8.5rem)] max-w-4xl flex-col">
      <Link to="/ai-agents" className="inline-flex shrink-0 items-center gap-1.5 text-sm text-slate-500 hover:text-slate-900 dark:text-white/60 dark:hover:text-white">
        <ArrowLeft className="h-4 w-4" /> AI Agents
      </Link>

      <header className="mt-4 flex shrink-0 items-center gap-3 rounded-2xl border border-slate-200 bg-gradient-to-r from-brand-500/10 via-indigo-500/10 to-accent-500/10 px-4 py-3 dark:border-white/10">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-brand-500 to-accent-500 text-white shadow-lg">
          <Bot className="h-6 w-6" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-brand-600 dark:text-brand-300">
            <Sparkles className="h-3.5 w-3.5" /> {isEn ? 'Course agent' : 'Agente del curso'}
          </p>
          <h1 className="truncate text-lg font-bold text-slate-900 dark:text-white">{documentName ?? '...'}</h1>
        </div>
        <button
          type="button"
          onClick={clearChat}
          title={isEn ? 'Clear chat' : 'Limpiar conversación'}
          className="flex shrink-0 items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-500 transition hover:border-red-300 hover:text-red-600 dark:border-white/10 dark:bg-white/[0.05] dark:text-white/60 dark:hover:text-red-400"
        >
          <Eraser className="h-3.5 w-3.5" /> {isEn ? 'Clear' : 'Limpiar'}
        </button>
      </header>

      <div ref={scrollRef} className="mt-4 min-h-0 flex-1 space-y-3 overflow-y-auto pr-1">
        {messages.map((message, index) => (
          <div key={index} className={`max-w-[85%] ${message.role === 'student' ? 'ml-auto' : ''}`}>
            <div
              className={`whitespace-pre-wrap rounded-2xl px-4 py-2.5 text-sm leading-relaxed ${
                message.role === 'student'
                  ? 'rounded-br-md bg-gradient-to-br from-brand-500 to-accent-500 text-white'
                  : 'rounded-bl-md border border-slate-200 bg-white text-slate-700 dark:border-white/10 dark:bg-white/[0.04] dark:text-white/90'
              }`}
            >
              {message.text}
            </div>
            {message.role === 'agent' && message.usedEngrams && message.usedEngrams.length > 0 && (
              <div className="mt-2 flex flex-wrap gap-1.5">
                {message.usedEngrams.map((engram) => (
                  <button
                    key={engram.engramId}
                    type="button"
                    onClick={() => setOpenEngram(engram)}
                    title={engram.courseName}
                    className="rounded-full border border-brand-200 bg-brand-50 px-2.5 py-1 text-left text-[11px] font-medium text-brand-700 transition hover:border-brand-400 hover:bg-brand-100 dark:border-brand-300/20 dark:bg-brand-400/10 dark:text-brand-200 dark:hover:bg-brand-400/20"
                  >
                    📖 {engram.title}
                  </button>
                ))}
              </div>
            )}
            {message.role === 'agent' && message.question && (
              <VoiceTutorAgent
                key={index}
                compact
                autoConnect={false}
                personName={user?.name}
                startLabel={isEn ? 'Talk about this' : 'Habla sobre esto'}
                aiAgentQuestion={{
                  documentId,
                  question: message.question,
                  answer: message.text,
                  engrams: (message.usedEngrams ?? []).map((e) => ({ title: e.title, text: e.text })),
                }}
              />
            )}
          </div>
        ))}
        {sending && (
          <div className="flex max-w-[85%] items-center gap-2 rounded-2xl rounded-bl-md border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-500 dark:border-white/10 dark:bg-white/[0.04] dark:text-white/60">
            <Loader2 className="h-4 w-4 animate-spin" /> {isEn ? 'Thinking...' : 'Pensando...'}
          </div>
        )}
        {error && <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">{error}</div>}
      </div>

      <div className="mt-4 flex shrink-0 gap-2 border-t border-slate-200 pt-4 dark:border-white/10">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && void sendMessage()}
          placeholder={isEn ? 'Ask about this course...' : 'Pregunta sobre este curso...'}
          disabled={sending}
          className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-brand-400 dark:border-white/10 dark:bg-white/[0.04] dark:text-white"
        />
        <button
          type="button"
          onClick={() => void sendMessage()}
          disabled={sending || !input.trim()}
          className="flex shrink-0 items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-4 py-3 text-sm font-semibold text-white disabled:opacity-50"
        >
          <Send className="h-4 w-4" /> {isEn ? 'Send' : 'Enviar'}
        </button>
      </div>

      {openEngram && (
        <EngramDetailPanel
          personId={personId}
          neuron={toSelectedNeuron(openEngram)}
          initialEngramText={openEngram.text}
          onClose={() => setOpenEngram(null)}
        />
      )}
    </div>
  );
}
