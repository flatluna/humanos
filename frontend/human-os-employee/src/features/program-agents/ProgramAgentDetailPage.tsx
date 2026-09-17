import { useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router';
import {
  ArrowLeft,
  Bot,
  Eraser,
  Loader2,
  Send,
  Sparkles,
  GraduationCap,
  Layers,
  BookOpen,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { EngramDetailPanel } from '@/features/brain/EngramDetailPanel';
import type { SelectedNeuron } from '@/features/brain/types';
import { postAiProgramAgentChat, type AiAgentChatHistoryEntry, type AiAgentUsedEngram } from '@/features/ai-agents/api/aiAgentsApi';
import { AgentMessageContent } from '@/features/ai-agents/components/AgentMessageContent';
import { getProgramById, type ProgramCatalogDetail } from '@/features/programs/api/programsApi';

interface ChatMessage {
  role: 'student' | 'agent';
  text: string;
  usedEngrams?: AiAgentUsedEngram[];
  question?: string;
}

export function ProgramAgentDetailPage() {
  const { programId } = useParams<{ programId: string }>();
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const tenantId = user?.tenantId ?? '';

  const [programDetail, setProgramDetail] = useState<ProgramCatalogDetail | null>(null);
  const [loadingProgram, setLoadingProgram] = useState(true);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [openEngram, setOpenEngram] = useState<AiAgentUsedEngram | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  const totalEngrams = (programDetail?.capabilities ?? []).reduce(
    (sum, c) => sum + (c.nodeCount || c.levelCount || 0),
    0
  );

  function greeting(programName: string, coursesCount: number, engramsCount: number): string {
    return isEn
      ? `Hello${user?.name ? `, ${user.name}` : ''}! I am your Program AI Agent for "${programName}". I have access to all ${coursesCount} courses and ${engramsCount} engrams across this entire program. What would you like to explore or clarify?`
      : `¡Hola${user?.name ? `, ${user.name}` : ''}! Soy tu Agente Experto de IA para el programa "${programName}". Cuento con todo el conocimiento de los ${coursesCount} cursos y ${engramsCount} engramas que integran este programa. ¿Qué dudas o temas deseas consultar?`;
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
    if (!programId) return;
    setLoadingProgram(true);
    getProgramById(programId)
      .then((detail) => {
        setProgramDetail(detail);
        const cCount = detail.capabilities?.length ?? detail.capabilityCount ?? 0;
        const eCount = (detail.capabilities ?? []).reduce((sum, c) => sum + (c.nodeCount || c.levelCount || 0), 0);
        setMessages([{ role: 'agent', text: greeting(detail.name, cCount, eCount) }]);
      })
      .catch((err) => {
        console.error('Failed to load program:', err);
        setError(isEn ? 'Failed to load program' : 'No se pudo cargar el programa');
      })
      .finally(() => setLoadingProgram(false));
  }, [programId, isEn]);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, sending]);

  function clearChat() {
    if (!programDetail) return;
    const cCount = programDetail.capabilities?.length ?? programDetail.capabilityCount ?? 0;
    setMessages([{ role: 'agent', text: greeting(programDetail.name, cCount, totalEngrams) }]);
  }

  async function sendMessage(textOverride?: string) {
    const text = (textOverride ?? input).trim();
    if (!text || !programId || sending) return;

    // Only the last few turns are sent — older ones stay on screen for the
    // student but re-sending them every turn would keep growing the prompt
    // (and its token cost) for no benefit, since the retrieval step already
    // re-grounds each answer from the course material, not from old chat text.
    const MAX_HISTORY_MESSAGES = 6;
    const history: AiAgentChatHistoryEntry[] = messages
      .slice(-MAX_HISTORY_MESSAGES)
      .map((m) => ({
        role: m.role === 'student' ? 'Student' : 'Agent',
        text: m.text,
      }));

    if (!textOverride) setInput('');
    setMessages((prev) => [...prev, { role: 'student', text }]);
    setSending(true);
    setError(null);

    try {
      const response = await postAiProgramAgentChat(tenantId, personId, programId, text, history);
      setMessages((prev) => [
        ...prev,
        { role: 'agent', text: response.message, usedEngrams: response.usedEngrams, question: text },
      ]);
    } catch {
      setError(
        isEn
          ? 'The program agent could not answer right now. Please try again.'
          : 'El agente del programa no pudo responder en este momento. Por favor reintenta.'
      );
      setMessages((prev) => prev.slice(0, -1));
      if (!textOverride) setInput(text);
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="mx-auto flex h-[calc(100vh-4rem)] max-w-5xl flex-col p-4 sm:p-6">
      {/* Top Header */}
      <div className="flex flex-none items-center justify-between border-b border-slate-200 pb-4 dark:border-white/10">
        <div className="flex items-center gap-3">
          <Link
            to="/program-agents"
            className="flex h-9 w-9 items-center justify-center rounded-xl border border-slate-200 text-slate-500 hover:bg-slate-100 dark:border-white/10 dark:text-slate-400 dark:hover:bg-white/5"
            title={isEn ? 'Back to Program Agents' : 'Volver a Agentes de Programa'}
          >
            <ArrowLeft className="h-4 w-4" />
          </Link>

          <div>
            <div className="flex items-center gap-2">
              <span className="flex items-center gap-1 rounded-md bg-purple-100 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-purple-700 dark:bg-purple-500/20 dark:text-purple-300">
                <GraduationCap className="h-3 w-3" />
                {isEn ? 'Program Agent' : 'Agente del Programa'}
              </span>
              <span className="text-xs text-slate-400 dark:text-white/40">
                • {programDetail?.capabilities?.length ?? 0} {isEn ? 'courses' : 'cursos'}
                {totalEngrams > 0 ? ` • ${totalEngrams} ${isEn ? 'engrams' : 'engramas'}` : ''}
              </span>
            </div>
            <h1 className="text-base sm:text-lg font-bold text-slate-900 dark:text-white truncate max-w-lg">
              {loadingProgram ? (isEn ? 'Loading program...' : 'Cargando programa...') : programDetail?.name}
            </h1>
          </div>
        </div>

        <button
          type="button"
          onClick={clearChat}
          className="flex items-center gap-1.5 rounded-xl border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600 hover:bg-slate-100 dark:border-white/10 dark:text-slate-300 dark:hover:bg-white/5"
        >
          <Eraser className="h-3.5 w-3.5" />
          <span className="hidden sm:inline">{isEn ? 'Clear' : 'Limpiar'}</span>
        </button>
      </div>

      {/* Program Courses Pill Bar */}
      {programDetail?.capabilities && programDetail.capabilities.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5 pt-3 pb-1 border-b border-slate-100 dark:border-white/5">
          <span className="text-[11px] font-semibold text-slate-400 dark:text-slate-500 mr-1 flex items-center gap-1">
            <Layers className="h-3 w-3" />
            {isEn ? 'Courses in scope:' : 'Cursos incluidos:'}
          </span>
          {programDetail.capabilities.map((c) => (
            <span
              key={c.capabilityId}
              className="rounded-full bg-slate-100 px-2.5 py-0.5 text-[10px] font-medium text-slate-700 dark:bg-white/5 dark:text-slate-300"
            >
              {c.capabilityName}
            </span>
          ))}
        </div>
      )}

      {/* Messages Scroll Area */}
      <div ref={scrollRef} className="flex-1 space-y-4 overflow-y-auto py-4">
        {messages.map((message, i) => (
          <div
            key={i}
            className={`flex gap-3 ${message.role === 'student' ? 'justify-end' : 'justify-start'}`}
          >
            {message.role === 'agent' && (
              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl bg-purple-600 text-white shadow-xs">
                <Bot className="h-4 w-4" />
              </span>
            )}
            <div
              className={`max-w-[85%] sm:max-w-[75%] rounded-2xl p-4 text-sm leading-relaxed shadow-xs ${
                message.role === 'student'
                  ? 'bg-gradient-to-r from-purple-600 to-indigo-600 text-white'
                  : 'border border-slate-200 bg-white text-slate-900 dark:border-white/10 dark:bg-slate-900 dark:text-white'
              }`}
            >
              <AgentMessageContent text={message.text} />

              {/* Citations from multiple courses */}
              {message.usedEngrams && message.usedEngrams.length > 0 && (
                <div className="mt-4 pt-3 border-t border-slate-100 dark:border-white/10 space-y-2">
                  <p className="flex items-center gap-1 text-[11px] font-bold uppercase tracking-wider text-purple-600 dark:text-purple-400">
                    <Sparkles className="h-3 w-3" />
                    {isEn ? 'Cited Engrams across Program:' : 'Engramas consultados en el programa:'}
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {message.usedEngrams.map((engram, idx) => (
                      <button
                        key={idx}
                        type="button"
                        onClick={() => setOpenEngram(engram)}
                        className="group flex items-center gap-1.5 rounded-xl border border-purple-200/80 bg-purple-50/70 px-2.5 py-1 text-left text-xs transition hover:border-purple-300 hover:bg-purple-100 dark:border-purple-500/20 dark:bg-purple-500/10 dark:hover:bg-purple-500/20"
                      >
                        <BookOpen className="h-3.5 w-3.5 text-purple-600 dark:text-purple-400 shrink-0" />
                        <div>
                          <div className="font-semibold text-slate-800 dark:text-slate-200 text-[11px] group-hover:text-purple-700 dark:group-hover:text-purple-300">
                            {engram.title}
                          </div>
                          <div className="text-[10px] text-purple-600 dark:text-purple-400">
                            {engram.courseName}
                          </div>
                        </div>
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        ))}

        {sending && (
          <div className="flex items-center gap-3 text-slate-400 dark:text-white/40 text-xs">
            <span className="flex h-8 w-8 items-center justify-center rounded-xl bg-purple-600/10 text-purple-600 dark:bg-purple-500/10 dark:text-purple-300">
              <Loader2 className="h-4 w-4 animate-spin" />
            </span>
            <span>
              {isEn
                ? 'Synthesizing knowledge across all program courses and engrams...'
                : 'Buscando y sintetizando en todos los cursos y engramas del programa...'}
            </span>
          </div>
        )}

        {error && (
          <div className="rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-600 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
            {error}
          </div>
        )}
      </div>

      {/* Input Composer */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          sendMessage();
        }}
        className="flex-none pt-2"
      >
        <div className="relative flex items-center">
          <input
            type="text"
            value={input}
            disabled={sending}
            onChange={(e) => setInput(e.target.value)}
            placeholder={
              isEn
                ? 'Ask a cross-course question about this program...'
                : 'Haz una pregunta sobre cualquier tema de este programa...'
            }
            className="w-full rounded-2xl border border-slate-200 bg-white py-3.5 pl-4 pr-12 text-sm text-slate-900 placeholder:text-slate-400 focus:border-purple-500 focus:outline-none focus:ring-2 focus:ring-purple-500/20 dark:border-white/10 dark:bg-slate-900 dark:text-white"
          />
          <button
            type="submit"
            disabled={!input.trim() || sending}
            className="absolute right-2 flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-r from-purple-600 to-indigo-600 text-white transition hover:brightness-110 disabled:opacity-40"
          >
            <Send className="h-4 w-4" />
          </button>
        </div>
      </form>

      {/* Engram Slide-Over / Modal */}
      {openEngram && (
        <EngramDetailPanel
          neuron={toSelectedNeuron(openEngram)}
          personId={personId}
          onClose={() => setOpenEngram(null)}
        />
      )}
    </div>
  );
}
