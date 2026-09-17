import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Bot, Eraser, Loader2, Send, Sparkles, X, Network } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { EngramDetailPanel } from './EngramDetailPanel';
import type { BridgeConceptItem } from './useKnowledgeNetwork';
import type { SelectedNeuron } from './types';
import {
  postAiAgentChat,
  type AiAgentChatHistoryEntry,
  type AiAgentUsedEngram,
} from '@/features/ai-agents/api/aiAgentsApi';
import { AgentMessageContent } from '@/features/ai-agents/components/AgentMessageContent';

export interface ConceptAgentModalProps {
  concept: BridgeConceptItem;
  onClose: () => void;
  onSelectNeuron?: (neuron: SelectedNeuron) => void;
}

interface ChatMessage {
  role: 'student' | 'agent';
  text: string;
  usedEngrams?: AiAgentUsedEngram[];
}

export function ConceptAgentModal({ concept, onClose }: ConceptAgentModalProps) {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const personId = user?.personId ?? '';
  const tenantId = user?.tenantId ?? '';

  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [openEngram, setOpenEngram] = useState<AiAgentUsedEngram | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const initialGreetingSent = useRef(false);

  // Pick first available documentId for grounding scope if exists
  const primaryDocId = concept.connectedEngrams[0]?.documentId ?? '';

  // Initial greeting tailored to this concept
  const initialGreeting = isEn
    ? `Hi${user?.name ? ` ${user.name}` : ''}! I'm your AI Knowledge Agent for the concept "${concept.name}" (${concept.entityType}). It connects ${concept.connectedEngrams.length} of your mastered engrams across your courses. Ask me anything about how this concept works or how it connects your knowledge.`
    : `¡Hola${user?.name ? ` ${user.name}` : ''}! Soy tu Agente de Conocimiento IA para el concepto "${concept.name}" (${concept.entityType}). Este concepto conecta ${concept.connectedEngrams.length} de tus engrams dominados. Pregúntame lo que quieras sobre cómo funciona o cómo se relaciona con tus cursos.`;

  useEffect(() => {
    if (initialGreetingSent.current) return;
    initialGreetingSent.current = true;
    setMessages([{ role: 'agent', text: initialGreeting }]);
  }, [initialGreeting]);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, sending]);

  function clearChat() {
    setMessages([{ role: 'agent', text: initialGreeting }]);
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

  async function sendMessage(textToSend?: string) {
    const rawText = (textToSend ?? input).trim();
    if (!rawText || sending) return;

    // Build concise context prompt prefix so backend retrieval finds relevant entity + engrams
    let promptMessage = rawText;
    if (messages.length <= 1 && !textToSend) {
      // First user query: enrich with concept name for accurate retrieval
      promptMessage = `Regarding the concept "${concept.name}" (${concept.entityType}): ${rawText}`;
    }

    const history: AiAgentChatHistoryEntry[] = messages.map((m) => ({
      role: m.role === 'student' ? 'Student' : 'Agent',
      text: m.text,
    }));

    setInput('');
    setMessages((prev) => [...prev, { role: 'student', text: rawText }]);
    setSending(true);
    setError(null);

    try {
      const response = await postAiAgentChat(tenantId, personId, primaryDocId, promptMessage, history);
      setMessages((prev) => [
        ...prev,
        {
          role: 'agent',
          text: response.message,
          usedEngrams: response.usedEngrams,
        },
      ]);
    } catch {
      setError(isEn ? 'The agent could not answer right now.' : 'El agente no pudo responder en este momento.');
      setMessages((prev) => prev.slice(0, -1));
      setInput(rawText);
    } finally {
      setSending(false);
    }
  }

  // Pre-crafted quick questions to help explore the concept
  const quickQuestions = isEn
    ? [
        `Explain what "${concept.name}" is and why it's important`,
        `How does "${concept.name}" connect with my courses?`,
        `Give me a real-world example of "${concept.name}"`,
      ]
    : [
        `Explícame qué es "${concept.name}" y por qué es importante`,
        `¿Cómo se conecta "${concept.name}" con mis cursos?`,
        `Dame un ejemplo práctico de "${concept.name}"`,
      ];

  return createPortal(
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm animate-fade-in">
      <div className="relative flex h-[88vh] w-full max-w-4xl flex-col rounded-3xl border border-indigo-500/30 bg-slate-900 shadow-2xl">
        {/* Header */}
        <div className="flex shrink-0 items-center justify-between border-b border-white/10 px-6 py-4">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-brand-500 to-accent-500 text-white shadow-lg">
              <Bot className="h-6 w-6" />
            </div>
            <div className="min-w-0">
              <div className="flex items-center gap-2">
                <span className="flex items-center gap-1 text-xs font-bold uppercase tracking-wider text-sky-400">
                  <Sparkles className="h-3.5 w-3.5" />
                  {isEn ? 'Concept Agent' : 'Agente del Concepto'}
                </span>
                <span className="rounded-full bg-sky-500/15 border border-sky-400/30 px-2 py-0.5 text-[10px] font-semibold text-sky-300">
                  {concept.entityType}
                </span>
              </div>
              <h2 className="truncate text-lg font-bold text-white">{concept.name}</h2>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={clearChat}
              title={isEn ? 'Clear chat' : 'Limpiar conversación'}
              className="flex items-center gap-1.5 rounded-full border border-white/10 bg-white/[0.05] px-3 py-1.5 text-xs font-semibold text-white/60 transition hover:border-red-300 hover:text-red-400"
            >
              <Eraser className="h-3.5 w-3.5" />
              <span>{isEn ? 'Clear' : 'Limpiar'}</span>
            </button>
            <button
              type="button"
              onClick={onClose}
              className="rounded-full p-2 text-slate-400 hover:bg-white/10 hover:text-white transition"
              aria-label={isEn ? 'Close' : 'Cerrar'}
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        </div>

        {/* Concept Context Pill / Summary */}
        <div className="shrink-0 border-b border-white/5 bg-slate-950/40 px-6 py-2.5">
          <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-300">
            <div className="flex items-center gap-1.5">
              <Network className="h-4 w-4 text-sky-400 shrink-0" />
              <span className="font-semibold text-white">{concept.connectedEngrams.length}</span>
              <span className="text-slate-400">
                {isEn ? 'connected engrams in your network:' : 'engrams conectados en tu red:'}
              </span>
              <span className="text-indigo-300 font-medium truncate max-w-md">
                {concept.connectedEngrams.map((e) => e.title).join(' · ')}
              </span>
            </div>
          </div>
          {concept.description && (
            <p className="mt-1 text-[11px] text-slate-400 line-clamp-1 italic">
              “{concept.description}”
            </p>
          )}
        </div>

        {/* Message Stream */}
        <div ref={scrollRef} className="min-h-0 flex-1 space-y-3 overflow-y-auto p-6 pr-4">
          {messages.map((message, index) => (
            <div key={index} className={`max-w-[85%] ${message.role === 'student' ? 'ml-auto' : ''}`}>
              <div
                className={`rounded-2xl px-4 py-3 text-sm leading-relaxed ${
                  message.role === 'student'
                    ? 'rounded-br-md bg-gradient-to-br from-brand-500 to-accent-500 text-white shadow-md whitespace-pre-wrap'
                    : 'rounded-bl-md border border-white/10 bg-white/[0.04] text-white/95'
                }`}
              >
                {message.role === 'student' ? (
                  message.text
                ) : (
                  <AgentMessageContent
                    text={message.text}
                    isEn={isEn}
                    onPromptClick={(prompt) => setInput(prompt)}
                  />
                )}
              </div>

              {message.role === 'agent' && message.usedEngrams && message.usedEngrams.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {message.usedEngrams.map((engram) => (
                    <button
                      key={engram.engramId}
                      type="button"
                      onClick={() => setOpenEngram(engram)}
                      title={engram.courseName}
                      className="rounded-full border border-brand-300/20 bg-brand-400/10 px-2.5 py-1 text-left text-[11px] font-medium text-brand-200 transition hover:bg-brand-400/20 hover:border-brand-300/40"
                    >
                      📖 {engram.title}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ))}

          {sending && (
            <div className="flex max-w-[85%] items-center gap-2 rounded-2xl rounded-bl-md border border-white/10 bg-white/[0.04] px-4 py-3 text-sm text-white/60">
              <Loader2 className="h-4 w-4 animate-spin text-sky-400" />
              <span>{isEn ? 'Consulting knowledge agent...' : 'Consultando agente de conocimiento...'}</span>
            </div>
          )}

          {error && (
            <div className="rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-2 text-sm text-red-300">
              {error}
            </div>
          )}
        </div>

        {/* Quick Question Chips when chat is short */}
        {messages.length <= 2 && (
          <div className="flex flex-wrap gap-2 px-6 pb-2">
            {quickQuestions.map((q, i) => (
              <button
                key={i}
                type="button"
                onClick={() => void sendMessage(q)}
                disabled={sending}
                className="flex items-center gap-1.5 rounded-full border border-sky-400/25 bg-sky-500/10 px-3 py-1 text-xs text-sky-200 hover:bg-sky-500/20 hover:border-sky-400/50 transition disabled:opacity-50"
              >
                <Sparkles className="h-3 w-3 text-sky-300" />
                <span>{q}</span>
              </button>
            ))}
          </div>
        )}

        {/* Input Bar */}
        <div className="flex shrink-0 gap-2 border-t border-white/10 p-4 sm:px-6">
          <input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && void sendMessage()}
            placeholder={
              isEn
                ? `Ask about "${concept.name}" or how it connects your engrams...`
                : `Pregunta sobre "${concept.name}" o cómo conecta tus engrams...`
            }
            disabled={sending}
            className="min-w-0 flex-1 rounded-xl border border-white/10 bg-white/[0.04] px-4 py-3 text-sm text-white placeholder:text-white/40 outline-none transition focus:border-brand-400"
          />
          <button
            type="button"
            onClick={() => void sendMessage()}
            disabled={sending || !input.trim()}
            className="flex shrink-0 items-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-white shadow-lg transition disabled:opacity-50"
          >
            <Send className="h-4 w-4" />
            <span>{isEn ? 'Send' : 'Enviar'}</span>
          </button>
        </div>

        {/* Nested detail panel if an engram chip is clicked */}
        {openEngram && (
          <EngramDetailPanel
            personId={personId}
            neuron={toSelectedNeuron(openEngram)}
            onClose={() => setOpenEngram(null)}
          />
        )}
      </div>
    </div>,
    document.body
  );
}
