import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import {
  ArrowLeft,
  Bot,
  Eraser,
  Loader2,
  Send,
  Sparkles,
  BookOpen,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import { EngramDetailPanel } from '@/features/brain/EngramDetailPanel';
import VoiceTutorAgent from '@/features/capabilities/components/VoiceTutorAgent';
import type { SelectedNeuron } from '@/features/brain/types';
import { postAiAgentChat, type AiAgentChatHistoryEntry, type AiAgentUsedEngram } from './api/aiAgentsApi';
import { apiGet } from '@/features/capabilities/api/httpClient';
import { getCapabilities } from '@/features/capabilities/api/capabilitiesApi';
import { getStructuredLearningDocument } from '@/features/capabilities/api/structuredLearningRuntimeApi';
import { AgentMessageContent } from './components/AgentMessageContent';

interface ChatMessage {
  role: 'student' | 'agent';
  text: string;
  usedEngrams?: AiAgentUsedEngram[];
  /** The student's question this answer responds to — needed to ground the
   * "Talk about this" voice follow-up in the SAME Q&A the text chat produced. */
  question?: string;
}

interface PublishedAgentCourse {
  structuredLearningDocumentId: string;
  documentName: string;
}

interface ResolvedAgentDocument {
  DocumentName: string;
}

/** One course's chat agent — grounded ONLY in this course's own engrams
 * (StructuredLearningChatService's Knowledge Graph retrieval, semantic/
 * vector entity match, never a raw dump of every engram), and aware of
 * THIS student's own objectives/skills/progress for this course via
 * personId (BuildStudentContextAsync server-side). */
export function AiAgentCoursePage({ documentIdOverride, compact = false }: { documentIdOverride?: string; compact?: boolean } = {}) {
  const params = useParams<{ documentId: string }>();
  const documentId = documentIdOverride ?? params.documentId;
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
    Promise.all([
      getEnrolledStructuredLearningDocuments(personId),
      apiGet<PublishedAgentCourse[]>('/catalog/structured-learning'),
      getCapabilities(),
      getStructuredLearningDocument(documentId).catch(() => null),
    ])
      .then(([enrolledDocs, publishedDocs, capabilities, directDocument]) => {
        const found = enrolledDocs.find((doc) => doc.structuredLearningDocumentId === documentId)
          ?? publishedDocs.find((doc) => doc.structuredLearningDocumentId === documentId)
          ?? capabilities.find((capability) => capability.capabilityId === documentId)
          ?? (directDocument as ResolvedAgentDocument | null);
        if (!found) return setNotFound(true);
        const name = 'DocumentName' in found
          ? found.DocumentName
          : 'documentName' in found
            ? found.documentName
            : found.name;
        setDocumentName(name);
        setMessages([{ role: 'agent', text: greeting(name) }]);
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

  async function sendMessage(textOverride?: string) {
    const text = (textOverride ?? input).trim();
    if (!text || !documentId || sending) return;
    const history: AiAgentChatHistoryEntry[] = messages.map((m) => ({
      role: m.role === 'student' ? 'Student' : 'Agent',
      text: m.text,
    }));
    if (!textOverride) setInput('');
    setMessages((prev) => [...prev, { role: 'student', text }]);
    setSending(true);
    setError(null);
    try {
      const response = await postAiAgentChat(tenantId, personId, documentId, text, history);
      setMessages((prev) => [
        ...prev,
        { role: 'agent', text: response.message, usedEngrams: response.usedEngrams, question: text },
      ]);
    } catch {
      setError(isEn ? 'The agent could not answer right now.' : 'El agente no pudo responder en este momento.');
      setMessages((prev) => prev.slice(0, -1));
      if (!textOverride) setInput(text);
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
    <div className={compact ? 'flex h-full flex-col' : 'mx-auto flex h-[calc(100dvh-8.5rem)] max-w-4xl flex-col'}>
      {!compact && (
        <Link to="/ai-agents" className="inline-flex shrink-0 items-center gap-1.5 text-sm text-slate-500 hover:text-slate-900 dark:text-white/60 dark:hover:text-white">
          <ArrowLeft className="h-4 w-4" /> AI Agents
        </Link>
      )}

      <header className="mt-4 flex shrink-0 items-center gap-3 rounded-2xl border border-slate-200 bg-gradient-to-r from-brand-500/10 via-indigo-500/10 to-accent-500/10 px-4 py-3 dark:border-white/10">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-cyan-500 to-blue-600 text-white shadow-lg">
          <Bot className="h-6 w-6" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-cyan-600 dark:text-cyan-400">
            <Sparkles className="h-3.5 w-3.5" /> {isEn ? 'Course agent' : 'Agente del curso'}
          </p>
          <h1 className="truncate text-lg font-bold text-slate-900 dark:text-white">{documentName ?? '...'}</h1>
        </div>
        <button
          type="button"
          onClick={clearChat}
          title={isEn ? 'Clear chat' : 'Limpiar conversación'}
          className="flex shrink-0 items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-500 transition hover:border-red-300 hover:text-red-600 dark:border-white/10 dark:bg-white/[0.05] dark:text-white/60 dark:hover:text-red-400 cursor-pointer"
        >
          <Eraser className="h-3.5 w-3.5" /> {isEn ? 'Clear' : 'Limpiar'}
        </button>
      </header>

      <div ref={scrollRef} className="mt-4 min-h-0 flex-1 space-y-4 overflow-y-auto pr-1">
        {messages.map((message, index) => (
          <div
            key={index}
            className={`flex items-start gap-3 ${
              message.role === 'student' ? 'ml-auto max-w-[85%] justify-end' : 'w-full max-w-[96%]'
            }`}
          >
            {message.role === 'agent' && (
              <div className="mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-cyan-500 to-blue-600 text-white shadow-sm ring-2 ring-cyan-500/20">
                <Bot className="h-4 w-4" />
              </div>
            )}

            <div className="min-w-0 flex-1 space-y-2">
              <div
                className={`rounded-2xl p-4 sm:p-5 text-sm shadow-sm transition-all ${
                  message.role === 'student'
                    ? 'rounded-tr-xs bg-gradient-to-r from-cyan-600 to-blue-600 text-white shadow-md'
                    : 'rounded-tl-xs border border-slate-200/90 bg-white dark:border-white/10 dark:bg-slate-900/90 text-slate-800 dark:text-slate-200'
                }`}
              >
                {message.role === 'student' ? (
                  <p className="whitespace-pre-wrap leading-relaxed">{message.text}</p>
                ) : (
                  <AgentMessageContent
                    text={message.text}
                    isEn={isEn}
                    onPromptClick={(prompt) => void sendMessage(prompt)}
                  />
                )}
              </div>

              {message.role === 'agent' && message.usedEngrams && message.usedEngrams.length > 0 && (
                <div className="mt-2.5 rounded-xl border border-slate-200/80 bg-slate-50/70 p-3 dark:border-white/10 dark:bg-white/[0.02]">
                  <div className="mb-2 flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                    <BookOpen className="h-3.5 w-3.5 text-cyan-600 dark:text-cyan-400" />
                    <span>{isEn ? 'Referenced Engrams from Course:' : 'Engramas de referencia del curso:'}</span>
                    <span className="rounded-full bg-slate-200 px-1.5 py-0.2 text-[10px] font-semibold text-slate-700 dark:bg-white/10 dark:text-slate-300">
                      {message.usedEngrams.length}
                    </span>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {message.usedEngrams.map((engram) => (
                      <button
                        key={engram.engramId}
                        type="button"
                        onClick={() => setOpenEngram(engram)}
                        title={engram.courseName}
                        className="group inline-flex items-center gap-1.5 rounded-lg border border-cyan-200/80 bg-white px-2.5 py-1.5 text-left text-xs font-semibold text-cyan-900 shadow-2xs transition hover:border-cyan-400 hover:bg-cyan-50 dark:border-cyan-500/30 dark:bg-cyan-950/30 dark:text-cyan-200 dark:hover:bg-cyan-950/60 cursor-pointer"
                      >
                        <span className="text-cyan-600 dark:text-cyan-400">📖</span>
                        <span className="truncate max-w-[280px]">{engram.title}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {message.role === 'agent' && message.question && (
                <div className="mt-2">
                  <VoiceTutorAgent
                    key={index}
                    compact
                    autoConnect={false}
                    personName={user?.name}
                    personId={personId}
                    startLabel={isEn ? '🎙️ Talk about this answer' : '🎙️ Hablar sobre esta respuesta'}
                    aiAgentQuestion={{
                      documentId,
                      question: message.question,
                      answer: message.text,
                      engrams: (message.usedEngrams ?? []).map((e) => ({ title: e.title, text: e.text })),
                    }}
                  />
                </div>
              )}
            </div>
          </div>
        ))}

        {sending && (
          <div className="flex max-w-[85%] items-center gap-2 rounded-2xl rounded-bl-md border border-slate-200 bg-white px-4 py-2.5 text-sm text-slate-500 dark:border-white/10 dark:bg-white/[0.04] dark:text-white/60">
            <Loader2 className="h-4 w-4 animate-spin text-cyan-500" /> {isEn ? 'Consulting engrams...' : 'Consultando engramas...'}
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
          className="min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-900 outline-none transition focus:border-cyan-500 dark:border-white/10 dark:bg-white/[0.04] dark:text-white"
        />
        <button
          type="button"
          onClick={() => void sendMessage()}
          disabled={sending || !input.trim()}
          className="flex shrink-0 items-center gap-1.5 rounded-xl bg-gradient-to-r from-cyan-600 to-blue-600 px-5 py-3 text-sm font-semibold text-white shadow-md shadow-cyan-500/20 transition hover:brightness-110 disabled:opacity-50 cursor-pointer"
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
