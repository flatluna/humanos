import { useEffect, useRef, useState } from 'react';
import { ArrowLeft, BookOpen, Bot, Send, Sparkles, X } from 'lucide-react';
import { useParams, useSearchParams, Link } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { getSubjects, type Subject } from '../lib/api/subjectsApi';
import { getStructuredLearningTopics, type StructuredLearningTopic } from '../lib/api/structuredLearningTopicsApi';
import { getStructuredLearningDocument, listStructuredLearningSectionVideos, type StructuredLearningSectionVideo } from '../lib/api/structuredLearningApi';
import { apiImageUrl } from '../lib/api/httpClient';
import VoiceTutorAgent from '../components/VoiceTutorAgent';
import {
  postStructuredLearningChat,
  type EngramReference,
  type StructuredLearningChatHistoryEntry,
} from '../lib/api/structuredLearningChatApi';
import { AgentMessageContent } from '../components/AgentMessageContent';

const DEFAULT_STUDIO_TENANT_ID = '00000000-0000-0000-0000-000000000000';

/**
 * AI Agents chat window (2026-08-18) — full chat page for both agent
 * levels of the "AI Agents" network:
 * - Topic-level (e.g. Matemáticas/Álgebra): no `?documentId=`, sees every
 *   Engram across every course of that Topic.
 * - Course-level (e.g. one specific course's `documentId`): passed via
 *   query string from AiAgentsTopicPage's per-course "Talk to course
 *   agent" button, sees only that course's own Engrams.
 * Grounded via StructuredLearningKnowledgeRetrievalService's Knowledge
 * Graph retrieval package (exact/alias/semantic entity match -> linked
 * Engrams/Schemas), scoped server-side by Subject/Topic/Document.
 */
export default function AiAgentsChatPage() {
  const { subjectName, topicName } = useParams<{ subjectName: string; topicName: string }>();
  const [searchParams] = useSearchParams();
  const documentId = searchParams.get('documentId') ?? undefined;
  const courseName = searchParams.get('courseName') ?? undefined;
  const { user } = useAuth();
  const tenantId = user?.tenantId ?? DEFAULT_STUDIO_TENANT_ID;

  const [subject, setSubject] = useState<Subject | null>(null);
  const [topic, setTopic] = useState<StructuredLearningTopic | null>(null);
  const [loading, setLoading] = useState(true);

  const [messages, setMessages] = useState<
    { author: 'student' | 'agent'; text: string; usedEngrams?: EngramReference[] }[]
  >([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [openEngram, setOpenEngram] = useState<EngramReference | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let cancelled = false;

    async function loadContext() {
      setLoading(true);
      try {
        const subjects = await getSubjects();
        const selectedSubject = subjects.find((item) => item.Name.toLowerCase() === decodeURIComponent(subjectName ?? '').toLowerCase());
        if (cancelled || !selectedSubject) return;
        setSubject(selectedSubject);
        const topics = await getStructuredLearningTopics(tenantId, selectedSubject.SubjectId);
        if (cancelled) return;
        setTopic(topics.find((item) => item.Name.toLowerCase() === decodeURIComponent(topicName ?? '').toLowerCase()) ?? null);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    void loadContext();
    return () => {
      cancelled = true;
    };
  }, [subjectName, topicName, tenantId]);

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' });
  }, [messages, sending]);

  const agentName = documentId ? (courseName ?? 'Course Agent') : topic?.Name ? `${topic.Name} Agent` : 'Domain Agent';

  async function handleSend() {
    const text = input.trim();
    if (!text || sending) return;
    setError(null);
    setInput('');
    setMessages((prev) => [...prev, { author: 'student', text }]);
    setSending(true);
    try {
      const history: StructuredLearningChatHistoryEntry[] = messages.map((m) => ({
        Role: m.author === 'student' ? 'Student' : 'Agent',
        Text: m.text,
      }));
      const response = await postStructuredLearningChat(tenantId, text, history, {
        subjectId: documentId ? undefined : subject?.SubjectId,
        topicId: documentId ? undefined : topic?.StructuredLearningTopicId,
        documentId,
      });
      setMessages((prev) => [...prev, { author: 'agent', text: response.Message, usedEngrams: response.UsedEngrams }]);
    } catch {
      setError('The agent could not answer right now. Please try again.');
      setMessages((prev) => prev.slice(0, -1));
      setInput(text);
    } finally {
      setSending(false);
    }
  }

  const backLink = `/ai-agents/${encodeURIComponent(subjectName ?? '')}/${encodeURIComponent(topicName ?? '')}`;

  return (
    <div className="mx-auto flex h-[calc(100vh-2rem)] max-w-4xl flex-col px-6 py-6">
      <Link to={backLink} className="inline-flex items-center gap-1.5 text-sm text-slate-400 transition hover:text-white">
        <ArrowLeft className="h-4 w-4" /> {topic?.Name ?? 'Back'}
      </Link>

      <header className="mt-4 flex items-center gap-3 border-b border-white/10 pb-4">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-cyan-300/15 text-cyan-200 ring-1 ring-cyan-200/30">
          <Bot className="h-6 w-6" />
        </div>
        <div>
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-[0.18em] text-cyan-300">
            <Sparkles className="h-3 w-3" /> {loading ? 'Loading...' : documentId ? 'Course agent' : 'Topic agent'}
          </div>
          <h1 className="text-lg font-bold text-white">{agentName}</h1>
        </div>
      </header>

      <div ref={scrollRef} className="mt-4 flex-1 space-y-3 overflow-y-auto pr-1">
        {messages.length === 0 && (
          <div className="mt-6 rounded-2xl border border-dashed border-white/15 p-8 text-center text-sm text-slate-400">
            Ask {agentName} anything about {documentId ? 'this course' : `${topic?.Name ?? 'this domain'}'s courses`}.
          </div>
        )}
        {messages.map((message, index) => (
          <div key={index} className={`max-w-[85%] ${message.author === 'student' ? 'ml-auto' : ''}`}>
            {message.author === 'student' ? (
              <div
                className="rounded-2xl px-4 py-2.5 text-sm leading-relaxed bg-cyan-500/20 text-white whitespace-pre-wrap"
              >
                {message.text}
              </div>
            ) : (
              <div className="rounded-2xl px-4 py-3 text-sm leading-relaxed bg-white/[0.05] text-slate-200">
                <AgentMessageContent text={message.text} onPromptClick={(prompt) => setInput(prompt)} />
              </div>
            )}
            {message.usedEngrams && message.usedEngrams.length > 0 && (
              <div className="mt-1.5 flex flex-wrap gap-1.5">
                {message.usedEngrams.map((engram) => (
                  <button
                    key={engram.EngramId}
                    type="button"
                    onClick={() => setOpenEngram(engram)}
                    className="rounded-full border border-cyan-400/20 bg-cyan-400/[0.06] px-2 py-0.5 text-[10px] text-cyan-300 transition hover:border-cyan-400/40 hover:bg-cyan-400/[0.12]"
                  >
                    📖 {engram.Title}
                  </button>
                ))}
              </div>
            )}
          </div>
        ))}
        {sending && <div className="max-w-[85%] rounded-2xl bg-white/[0.05] px-4 py-2.5 text-sm text-slate-400">Thinking...</div>}
        {error && <div className="rounded-xl border border-red-400/20 bg-red-500/[0.08] px-4 py-2 text-sm text-red-200">{error}</div>}
      </div>

      <div className="mt-4 flex gap-2 border-t border-white/10 pt-4">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSend()}
          placeholder={`Ask ${agentName}...`}
          disabled={sending}
          className="flex-1 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-3 text-sm text-white placeholder:text-slate-500 focus:border-cyan-400 focus:outline-none"
        />
        <button
          type="button"
          onClick={handleSend}
          disabled={sending || !input.trim()}
          className="flex items-center gap-1.5 rounded-xl bg-cyan-300 px-4 py-3 text-sm font-semibold text-slate-950 disabled:opacity-50"
        >
          <Send className="h-4 w-4" /> Send
        </button>
      </div>

      {openEngram && (
        <EngramModal
          engram={openEngram}
          subjectName={subject?.Name}
          topicName={topic?.Name}
          onClose={() => setOpenEngram(null)}
        />
      )}
    </div>
  );
}

/** Cited-Engram modal (2026-08-18, simplified per explicit request): just
 * the Engram's own content plus the realtime Voice Tutor Agent — no
 * score/last-reviewed stats (that's per-student runtime state this Studio
 * chat has no business showing) and no flashcards. NEVER navigates
 * anywhere — stays on THIS chat page so the open conversation is never
 * lost. Voice agent is wired to the Engram's Teaching step (same
 * blueprint-only path BlueprintStepPeek uses in PreviewNodePage.tsx —
 * conversational Q&A, not graded). */
function EngramModal({
  engram,
  subjectName,
  topicName,
  onClose,
}: {
  engram: EngramReference;
  subjectName?: string;
  topicName?: string;
  onClose: () => void;
}) {
  const [teachingStepId, setTeachingStepId] = useState<string | null>(null);
  const [illustrationId, setIllustrationId] = useState<string | null>(null);
  const [videos, setVideos] = useState<StructuredLearningSectionVideo[]>([]);

  useEffect(() => {
    let cancelled = false;
    getStructuredLearningDocument(engram.DocumentId)
      .then((doc) => {
        if (cancelled) return;
        const section = doc.Sections.find((s) => s.StructuredLearningDocumentSectionId === engram.EngramId);
        const teachingStep = section?.Steps.find((s) => s.StepType === 'Teaching') ?? null;
        setTeachingStepId(teachingStep?.StructuredLearningSectionStepId ?? null);
        try {
          const parsed = teachingStep?.StepDataJson ? (JSON.parse(teachingStep.StepDataJson) as { IllustrationId?: string | null }) : null;
          setIllustrationId(parsed?.IllustrationId ?? null);
        } catch {
          setIllustrationId(null);
        }
      })
      .catch(() => !cancelled && setTeachingStepId(null));
    listStructuredLearningSectionVideos(engram.EngramId)
      .then((list) => !cancelled && setVideos(list))
      .catch(() => !cancelled && setVideos([]));
    return () => {
      cancelled = true;
    };
  }, [engram.DocumentId, engram.EngramId]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm" onClick={onClose}>
      <div
        className="max-h-[85vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-white/10 bg-slate-900 p-6 shadow-2xl shadow-black/40"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.14em] text-cyan-300">
            <BookOpen className="h-3.5 w-3.5" /> {engram.CourseName}
          </div>
          <button type="button" onClick={onClose} className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>

        {videos.length > 0 ? (
          <div className="mt-3 space-y-3">
            {videos.map((video) => (
              <ZoomableEngramMedia key={video.IllustrationId} illustrationId={video.IllustrationId} alt={engram.Title} kind="video" />
            ))}
          </div>
        ) : illustrationId ? (
          <div className="mt-3">
            <ZoomableEngramMedia illustrationId={illustrationId} alt={engram.Title} kind="image" />
          </div>
        ) : null}

        <h2 className="mt-3 text-lg font-semibold text-white">{engram.Title}</h2>

        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-slate-500">
          <span>Engram ID: <span className="font-mono text-slate-400">{engram.EngramId}</span></span>
          <span>·</span>
          <span>Course: <span className="text-slate-400">{engram.CourseName}</span> <span className="font-mono">({engram.DocumentId})</span></span>
          {(subjectName || topicName) && (
            <>
              <span>·</span>
              <span>Domain / Topic: <span className="text-slate-400">{[subjectName, topicName].filter(Boolean).join(' / ')}</span></span>
            </>
          )}
        </div>

        <p className="mt-3 whitespace-pre-wrap text-sm leading-relaxed text-slate-300">{engram.Text}</p>

        {teachingStepId && (
          <div className="mt-5 border-t border-white/10 pt-4">
            <VoiceTutorAgent structuredLearningSectionStepId={teachingStepId} />
          </div>
        )}
      </div>
    </div>
  );
}

/** Engram citation media (2026-08-25) — the Teaching illustration or any
 * generated video, shown at the top of the modal and enlargeable in a
 * fullscreen lightbox on click, same pattern as StructuredLearningSectionPage's
 * ZoomableIllustration. */
function ZoomableEngramMedia({ illustrationId, alt, kind }: { illustrationId: string; alt: string; kind: 'image' | 'video' }) {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open]);

  const src = apiImageUrl(`/illustrations/structured-learning/${illustrationId}/image`);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="group relative block w-full overflow-hidden rounded-xl border border-white/10 bg-white/[0.02] cursor-zoom-in"
        aria-label="Ampliar"
      >
        {kind === 'video' ? (
          <video src={src} className="h-56 w-full bg-black object-contain" muted playsInline preload="metadata" />
        ) : (
          <img src={src} alt={alt} className="h-56 w-full object-contain object-center" />
        )}
        {kind === 'video' && (
          <span className="pointer-events-none absolute inset-0 flex items-center justify-center">
            <span className="flex h-12 w-12 items-center justify-center rounded-full bg-black/60 text-white transition group-hover:bg-black/75">▶</span>
          </span>
        )}
        <span className="pointer-events-none absolute bottom-2 right-2 rounded-lg bg-black/65 px-2 py-1 text-[10px] font-semibold text-white opacity-0 transition group-hover:opacity-100">
          Ampliar
        </span>
      </button>
      {open && (
        <div
          className="fixed inset-0 z-[70] flex items-center justify-center bg-black/85 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          onClick={() => setOpen(false)}
        >
          <div className="relative flex max-h-[95vh] max-w-[95vw] items-center justify-center" onClick={(event) => event.stopPropagation()}>
            {kind === 'video' ? (
              <video src={src} controls autoPlay muted playsInline className="max-h-[90vh] max-w-[92vw] rounded-xl object-contain shadow-2xl" />
            ) : (
              <img src={src} alt={alt} className="max-h-[90vh] max-w-[92vw] rounded-xl object-contain shadow-2xl" />
            )}
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="absolute -right-2 -top-2 rounded-full border border-white/20 bg-slate-900 p-2 text-white shadow-lg hover:bg-slate-800"
              aria-label="Cerrar"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}
    </>
  );
}
