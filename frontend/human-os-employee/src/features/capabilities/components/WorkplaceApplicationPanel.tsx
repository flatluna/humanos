import { useCallback, useEffect, useState, type ReactNode } from 'react';
import {
  Sparkles,
  Headphones,
  Loader2,
  MessageSquareText,
  Bot,
  Trash2,
  X,
  Send,
  ArrowRight,
} from 'lucide-react';
import {
  getStructuredLearningWorkplaceApplication,
  postStructuredLearningWorkplaceChat,
  type WorkplaceApplicationDto,
} from '../api/structuredLearningRuntimeApi';
import { getMyCqiProfile, type CqiProfileData } from '@/features/cqi-workspace/api/cqiApi';

/** Groups a long flowing paragraph into readable chunks of N sentences each, purely for
 * on-screen display — the underlying text (and what's spoken in the audio) is unchanged. */
function groupIntoParagraphs(text: string, sentencesPerParagraph = 2): string[] {
  const sentences = text.match(/[^.!?]+[.!?]+(\s+|$)/g) ?? [text];
  const paragraphs: string[] = [];
  for (let i = 0; i < sentences.length; i += sentencesPerParagraph) {
    paragraphs.push(sentences.slice(i, i + sentencesPerParagraph).join('').trim());
  }
  return paragraphs.length > 0 ? paragraphs : [text.trim()];
}

/** Splits agent output like "- point one\n- point two" into clean bullet items;
 * falls back to treating the whole string as a single item when there are no "- " markers. */
function parseBulletLines(text: string): string[] {
  const lines = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => line.replace(/^[-•]\s*/, ''));
  return lines.length > 0 ? lines : [text.trim()];
}

function ZoomableImageByUrl({ src, alt, className }: { src: string; alt: string; className: string }) {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [open]);

  return (
    <div className="group relative block w-full">
      <button type="button" onClick={() => setOpen(true)} className="block w-full cursor-zoom-in" aria-label="Enlarge image">
        <img src={src} alt={alt} className={className} />
        <span className="pointer-events-none absolute bottom-2 right-2 rounded-lg bg-black/65 px-2 py-1 text-[10px] font-semibold text-white opacity-0 transition group-hover:opacity-100">
          Enlarge
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
            <img src={src} alt={alt} className="max-h-[90vh] max-w-[92vw] rounded-xl object-contain shadow-2xl" />
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="absolute -right-2 -top-2 rounded-full border border-white/20 bg-slate-900 p-2 text-white shadow-lg hover:bg-slate-800 cursor-pointer"
              aria-label="Close"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function formatMentorInline(text: string): ReactNode {
  const codeParts = text.split(/(`[^`]+`)/g);
  return codeParts.map((part, i) => {
    if (part.startsWith('`') && part.endsWith('`') && part.length >= 2) {
      return (
        <code
          key={i}
          className="rounded bg-teal-950/80 border border-teal-500/30 px-1.5 py-0.5 text-teal-200 font-mono text-[11px]"
        >
          {part.slice(1, -1)}
        </code>
      );
    }
    const boldParts = part.split(/(\*\*[^*]+\*\*)/g);
    return boldParts.map((bPart, j) => {
      if (bPart.startsWith('**') && bPart.endsWith('**') && bPart.length >= 4) {
        return (
          <strong key={`${i}-${j}`} className="font-semibold text-slate-900 dark:text-white">
            {bPart.slice(2, -2)}
          </strong>
        );
      }
      if (bPart.includes(' → ') || bPart.includes(' -> ')) {
        const arrowTokens = bPart.split(/\s*(?:→|->)\s*/);
        return arrowTokens.map((tok, k) => (
          <span key={`${i}-${j}-${k}`}>
            {tok}
            {k < arrowTokens.length - 1 && (
              <span className="inline-flex items-center justify-center mx-1.5 px-1.5 py-0.5 text-[10px] rounded-md bg-teal-500/20 text-teal-700 dark:text-teal-300 font-mono border border-teal-500/30 shadow-xs">
                →
              </span>
            )}
          </span>
        ));
      }
      return bPart;
    });
  });
}

function MentorMessageContent({
  text,
  onPromptClick,
  isEn,
}: {
  text: string;
  onPromptClick?: (prompt: string) => void;
  isEn: boolean;
}) {
  const codeBlocks = text.split(/(```[\s\S]*?```)/g);

  return (
    <div className="space-y-3 text-xs text-slate-800 dark:text-slate-200 leading-relaxed">
      {codeBlocks.map((block, bIdx) => {
        if (block.startsWith('```') && block.endsWith('```')) {
          const lines = block.slice(3, -3).trim().split('\n');
          const firstLine = lines[0]?.trim();
          const isLangHeader = /^[a-z0-9_-]+$/i.test(firstLine);
          const codeLines = isLangHeader ? lines.slice(1) : lines;
          return (
            <pre
              key={bIdx}
              className="my-3 rounded-xl bg-slate-950 p-3.5 border border-teal-500/30 font-mono text-[11px] text-teal-200 overflow-x-auto shadow-inner"
            >
              <code>{codeLines.join('\n')}</code>
            </pre>
          );
        }

        const paragraphs = block.split(/\n\s*\n/).map((p) => p.trim()).filter(Boolean);

        return paragraphs.map((para, pIdx) => {
          const lines = para.split('\n').map((l) => l.trim()).filter(Boolean);
          const isListGroup = lines.length > 0 && lines.every((l) => /^[-*•]\s+|\d+[.)]\s+/.test(l));

          if (isListGroup) {
            return (
              <ul key={`${bIdx}-${pIdx}`} className="space-y-2.5 my-3">
                {lines.map((line, lIdx) => {
                  const cleaned = line.replace(/^[-*•]\s+|\d+[.)]\s+/, '');
                  const colonIndex = cleaned.indexOf(':');
                  const hasTitle = colonIndex > 0 && colonIndex < 40;
                  const title = hasTitle ? cleaned.slice(0, colonIndex + 1) : '';
                  const body = hasTitle ? cleaned.slice(colonIndex + 1) : cleaned;

                  return (
                    <li
                      key={lIdx}
                      className="flex items-start gap-2.5 rounded-xl border border-slate-200 bg-slate-50/60 p-3 dark:border-white/10 dark:bg-white/[0.025] hover:bg-slate-100 dark:hover:bg-white/[0.05] transition-colors shadow-xs"
                    >
                      <span className="h-2 w-2 rounded-full bg-teal-500 mt-1.5 shrink-0 ring-4 ring-teal-400/20" />
                      <div className="flex-1 text-slate-700 dark:text-slate-300 leading-relaxed">
                        {hasTitle && (
                          <span className="font-semibold text-teal-700 dark:text-teal-200 block sm:inline sm:mr-1.5">
                            {formatMentorInline(title)}
                          </span>
                        )}
                        {formatMentorInline(body)}
                      </div>
                    </li>
                  );
                })}
              </ul>
            );
          }

          const isEndingQuestion =
            para.endsWith('?') ||
            /^(would you like|do you want|shall we|¿te gustaría|deseas|quieres)/i.test(para);

          if (isEndingQuestion) {
            return (
              <div
                key={`${bIdx}-${pIdx}`}
                className="mt-3.5 rounded-2xl border border-teal-500/30 bg-gradient-to-r from-teal-50/80 via-white to-emerald-50/80 dark:from-teal-500/15 dark:via-slate-950 dark:to-emerald-500/15 p-3.5 shadow-md space-y-2"
              >
                <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-teal-600 dark:text-teal-300">
                  <Sparkles className="h-3.5 w-3.5 text-teal-500 dark:text-teal-400" />
                  <span>{isEn ? 'Suggested Next Step' : 'Siguiente paso sugerido'}</span>
                </div>
                <p className="text-teal-900 dark:text-teal-100 font-medium leading-relaxed">
                  {formatMentorInline(para)}
                </p>
                {onPromptClick && (
                  <button
                    type="button"
                    onClick={() => onPromptClick(para)}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-teal-400/30 bg-teal-500/10 hover:bg-teal-500/20 dark:bg-teal-500/20 dark:hover:bg-teal-500/30 px-2.5 py-1 text-[11px] font-semibold text-teal-700 dark:text-teal-200 hover:text-teal-900 dark:hover:text-white transition-colors cursor-pointer"
                  >
                    <span>{isEn ? 'Send' : 'Enviar'}</span>
                    <ArrowRight className="h-3 w-3" />
                  </button>
                )}
              </div>
            );
          }

          return (
            <p key={`${bIdx}-${pIdx}`} className="text-slate-700 dark:text-slate-200 leading-relaxed">
              {formatMentorInline(para)}
            </p>
          );
        });
      })}
    </div>
  );
}

/**
 * "In your role" step (2026-09-22) — a per-student, per-engram AI-generated
 * workplace application scenario (audio + text + CQI-aware reflection),
 * originally built inline in StructuredLearningSectionPage.tsx. Extracted
 * here so the exact same dynamically-generated content can also be shown
 * inside the Brain modal's "In your role" tab (EngramDetailPanel.tsx)
 * without duplicating this ~700-line component.
 */
export function WorkplaceApplicationStep({
  sectionId,
  personId,
  courseLanguage,
  onComplete,
  isEn,
  isStudent,
}: {
  sectionId: string;
  personId?: string;
  courseLanguage?: 'es' | 'en';
  onComplete: () => void;
  isEn: boolean;
  isStudent: boolean;
}) {
  const [data, setData] = useState<WorkplaceApplicationDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasLoaded, setHasLoaded] = useState(false);
  const [audioTimestamp, setAudioTimestamp] = useState<number>(Date.now());
  const [isChatOpen, setIsChatOpen] = useState(false);
  const [chatMessages, setChatMessages] = useState<Array<{ role: 'user' | 'assistant'; text: string }>>([]);
  const [chatInput, setChatInput] = useState('');
  const [chatLoading, setChatLoading] = useState(false);
  const [chatSessionId, setChatSessionId] = useState<string | null>(null);
  const [cqiProfile, setCqiProfile] = useState<CqiProfileData | null>(null);
  const [isCqiModalOpen, setIsCqiModalOpen] = useState(false);

  useEffect(() => {
    if (isStudent || !personId) return;
    let cancelled = false;
    getMyCqiProfile(personId)
      .then((res) => {
        if (!cancelled) setCqiProfile(res.profile);
      })
      .catch(() => {
        if (!cancelled) setCqiProfile(null);
      });
    return () => {
      cancelled = true;
    };
  }, [personId, isStudent]);

  const activeLanguage = courseLanguage || (isEn ? 'en' : 'es');

  const sendChatMessage = useCallback(async (textToSend?: string) => {
    const msg = (textToSend ?? chatInput).trim();
    if (!msg || chatLoading) return;

    setChatMessages((prev) => [...prev, { role: 'user', text: msg }]);
    if (!textToSend) setChatInput('');
    setChatLoading(true);

    try {
      const res = await postStructuredLearningWorkplaceChat(
        sectionId,
        msg,
        chatSessionId || undefined,
        personId || undefined,
        activeLanguage
      );
      if (res.SessionId) setChatSessionId(res.SessionId);
      setChatMessages((prev) => [...prev, { role: 'assistant', text: res.Reply }]);
    } catch (err) {
      setChatMessages((prev) => [
        ...prev,
        {
          role: 'assistant',
          text: err instanceof Error ? `Error: ${err.message}` : String(err),
        },
      ]);
    } finally {
      setChatLoading(false);
    }
  }, [chatInput, chatLoading, sectionId, chatSessionId, personId, activeLanguage]);

  const clearChat = useCallback(() => {
    setChatMessages([]);
    setChatInput('');
    setChatSessionId(null);
  }, []);

  const fetchApplication = useCallback(async (force = false) => {
    setLoading(true);
    setError(null);
    // Wipe any previously shown content immediately — "Regenerate" must never leave the old
    // scenario/images visible while the fresh one (which also purges the server-side cache) loads.
    setData(null);
    try {
      const res = await getStructuredLearningWorkplaceApplication(
        sectionId,
        personId || undefined,
        activeLanguage,
        force
      );
      setData(res);
      setAudioTimestamp(Date.now());
      setHasLoaded(true);
      onComplete();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [sectionId, personId, activeLanguage, onComplete]);

  return (
    <div className="space-y-6 animate-fade-in mt-4">
      {!hasLoaded && !loading && (
        <div className="rounded-3xl border border-teal-300 bg-gradient-to-br from-teal-50 via-white to-emerald-50 p-8 text-center shadow-lg dark:border-teal-500/30 dark:bg-gradient-to-br dark:from-teal-500/10 dark:via-slate-950/60 dark:to-emerald-500/10">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-teal-100 text-teal-600 dark:bg-teal-500/20 dark:text-teal-300 mb-4">
            <Sparkles className="h-7 w-7" />
          </div>
          <h3 className="text-xl font-bold text-slate-900 dark:text-white mb-2">
            {isEn ? 'Workplace Application in Your Role' : 'Caso Práctico en tu Puesto'}
          </h3>
          <p className="max-w-xl mx-auto text-sm text-slate-600 dark:text-slate-300 leading-relaxed mb-6">
            {isEn
              ? 'Generate a real workplace application tailored to your role, tools, and SMART objectives. You will listen to a pre-recorded audio explanation created specifically for you and see how to apply this engram in your daily work.'
              : 'Genera un caso de aplicación real personalizado a tu rol, tecnologías y objetivos SMART. Escucharás un audio pre-grabado creado exclusivamente para ti y verás cómo aplicar este engrama en tu trabajo diario.'}
          </p>
          {!isStudent && (
            <p className="max-w-xl mx-auto text-xs text-teal-700 dark:text-teal-300 leading-relaxed mb-6 -mt-3">
              {isEn
                ? '✨ We also draw on your own CQI assessment from HermanScience to help you maximize your learning and potential.'
                : '✨ También nos apoyamos en tu propia evaluación CQI de HermanScience para ayudarte a maximizar tu aprendizaje y potencial.'}
            </p>
          )}
          <button
            type="button"
            onClick={() => fetchApplication(false)}
            disabled={loading}
            className="inline-flex items-center gap-2.5 rounded-xl bg-gradient-to-r from-teal-600 to-emerald-600 px-6 py-3.5 text-sm font-semibold text-white shadow-lg shadow-teal-500/25 transition-all hover:scale-[1.02] active:scale-[0.98] cursor-pointer"
          >
            <Headphones className="h-4 w-4" />
            <span>{isEn ? 'Generate and listen to workplace scenario for my role' : 'Generar y escuchar caso práctico para mi puesto'}</span>
          </button>
        </div>
      )}

      {loading && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 backdrop-blur-sm p-4" role="dialog" aria-modal="true">
          <div className="w-full max-w-md rounded-3xl border border-teal-300 bg-white dark:border-teal-500/30 dark:bg-slate-900 p-8 text-center shadow-2xl">
            <Loader2 className="mx-auto h-9 w-9 animate-spin text-teal-500 dark:text-teal-400 mb-4" />
            <p className="text-sm font-bold text-slate-900 dark:text-white">
              {isEn ? 'Generating your personalized workplace application...' : 'Generando tu caso práctico personalizado...'}
            </p>
            <ul className="mt-4 space-y-2 text-left text-xs text-slate-600 dark:text-slate-300">
              <li className="flex items-start gap-2">
                <Sparkles className="h-3.5 w-3.5 shrink-0 mt-0.5 text-teal-600 dark:text-teal-300" />
                <span>{isEn ? 'Reading this engram and connecting it to your role, tools, and SMART objectives.' : 'Leyendo este engrama y conectándolo con tu rol, herramientas y objetivos SMART.'}</span>
              </li>
              {!isStudent && (
                <li className="flex items-start gap-2">
                  <Sparkles className="h-3.5 w-3.5 shrink-0 mt-0.5 text-teal-600 dark:text-teal-300" />
                  <span>{isEn ? 'Checking your HermanScience CQI profile to personalize reflection and growth guidance.' : 'Revisando tu perfil CQI de HermanScience para personalizar la reflexión y el crecimiento.'}</span>
                </li>
              )}
              <li className="flex items-start gap-2">
                <Sparkles className="h-3.5 w-3.5 shrink-0 mt-0.5 text-teal-600 dark:text-teal-300" />
                <span>{isEn ? 'Writing your scenario, practical actions, reflection questions, and growth opportunity.' : 'Escribiendo tu escenario, acciones prácticas, preguntas de reflexión y oportunidad de crecimiento.'}</span>
              </li>
              {!isStudent && (
                <li className="flex items-start gap-2">
                  <Sparkles className="h-3.5 w-3.5 shrink-0 mt-0.5 text-teal-600 dark:text-teal-300" />
                  <span>{isEn ? 'Generating two worked-example illustrations with explanations, grounded in this lesson.' : 'Generando dos ilustraciones de ejemplo con explicaciones, basadas en esta lección.'}</span>
                </li>
              )}
              <li className="flex items-start gap-2">
                <Sparkles className="h-3.5 w-3.5 shrink-0 mt-0.5 text-teal-600 dark:text-teal-300" />
                <span>{isEn ? 'Synthesizing the full narrated audio with your neural voice.' : 'Sintetizando el audio narrado completo con voz neuronal.'}</span>
              </li>
            </ul>
            <p className="mt-4 text-[11px] text-slate-400 dark:text-slate-500">
              {isEn ? 'This can take up to a minute — thanks for your patience.' : 'Esto puede tardar hasta un minuto — gracias por tu paciencia.'}
            </p>
          </div>
        </div>
      )}

      {error && (
        <div className="rounded-2xl border border-red-300 bg-red-50 p-4 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300 text-center">
          <p>{error}</p>
          <button
            type="button"
            onClick={() => fetchApplication(false)}
            className="mt-2 inline-flex items-center gap-1.5 rounded-lg bg-red-100 dark:bg-red-500/20 px-3 py-1 text-xs font-semibold text-red-800 dark:text-red-200 hover:bg-red-200 dark:hover:bg-red-500/30"
          >
            {isEn ? 'Retry' : 'Reintentar'}
          </button>
        </div>
      )}

      {data && (
        <div className="rounded-3xl border border-teal-200 bg-gradient-to-br from-teal-50/60 via-white to-emerald-50/60 p-6 sm:p-8 shadow-xl dark:border-teal-500/30 dark:bg-gradient-to-br dark:from-teal-500/10 dark:via-slate-950 dark:to-emerald-500/10 space-y-6">
          <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 border-b border-slate-200 dark:border-white/10 pb-4">
            <div>
              <span className="inline-flex items-center gap-1 rounded-full border border-teal-300 bg-teal-100/80 px-3 py-1 text-[11px] font-semibold text-teal-800 dark:border-teal-400/40 dark:bg-teal-500/15 dark:text-teal-200">
                <Sparkles className="h-3 w-3" /> {isEn ? 'Workplace Application' : 'Caso Práctico en tu Puesto'}
              </span>
              <h2 className="text-xl font-bold text-slate-900 dark:text-white mt-2">{data.ScenarioTitle}</h2>
              <p className="text-xs text-slate-500 dark:text-slate-300 mt-0.5">
                {isEn ? `Personalized for ${data.StudentName} · ${data.RoleTitle}` : `Personalizado para ${data.StudentName} · ${data.RoleTitle}`}
              </p>
            </div>
            <div className="flex items-center gap-2">
              {data.Tools.length > 0 && (
                <div className="flex flex-wrap gap-1.5 max-w-sm">
                  {data.Tools.slice(0, 3).map((tool, i) => (
                    <span key={i} className="rounded-md border border-slate-200 bg-white px-2 py-0.5 text-[10px] text-slate-600 dark:border-white/10 dark:bg-white/[0.04] dark:text-slate-300">
                      {tool}
                    </span>
                  ))}
                </div>
              )}
              <button
                type="button"
                onClick={() => fetchApplication(true)}
                disabled={loading}
                title={isEn ? 'Regenerate workplace scenario' : 'Regenerar caso práctico'}
                className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 dark:border-white/15 dark:bg-white/5 dark:hover:bg-white/10 px-2.5 py-1.5 text-[11px] font-medium text-slate-700 dark:text-slate-200 transition-colors cursor-pointer"
              >
                <Sparkles className="h-3 w-3 text-teal-600 dark:text-teal-300" />
                <span>{isEn ? 'Regenerate workplace scenario' : 'Regenerar caso práctico'}</span>
              </button>
            </div>
          </div>

          {/* Full-width Chat with Professor Button right above the Audio Player */}
          <button
            type="button"
            onClick={() => setIsChatOpen(true)}
            className="w-full inline-flex items-center justify-center gap-2 rounded-2xl border border-teal-300 bg-gradient-to-r from-teal-500/10 via-slate-50 to-emerald-500/10 hover:from-teal-500/20 hover:to-emerald-500/20 dark:border-teal-500/40 dark:bg-gradient-to-r dark:from-teal-500/20 dark:via-slate-900 dark:to-emerald-500/20 dark:hover:from-teal-500/30 dark:hover:to-emerald-500/30 px-5 py-3 text-sm font-semibold text-teal-800 dark:text-teal-200 transition-all hover:scale-[1.01] active:scale-[0.99] cursor-pointer shadow-md"
          >
            <MessageSquareText className="h-4 w-4 text-teal-600 dark:text-teal-300" />
            <span>{isEn ? 'Chat with Professor' : 'Preguntar al Profesor'}</span>
          </button>

          {cqiProfile && (
            <button
              type="button"
              onClick={() => setIsCqiModalOpen(true)}
              className="w-full inline-flex items-center justify-center gap-2 rounded-2xl border border-slate-200 bg-white hover:bg-slate-50 dark:border-white/15 dark:bg-white/5 dark:hover:bg-white/10 px-5 py-2.5 text-xs font-semibold text-slate-700 dark:text-slate-200 transition-all cursor-pointer"
            >
              <Sparkles className="h-3.5 w-3.5 text-teal-600 dark:text-teal-300" />
              <span>{isEn ? 'View my CQI Type Profile' : 'Ver mi Perfil de Tipo CQI'}</span>
            </button>
          )}

          {/* Audio Player */}
          {data.AudioUrl && (
            <div className="rounded-2xl border border-teal-200 bg-slate-50 p-4 dark:border-teal-500/20 dark:bg-slate-900/80">
              <div className="flex items-center gap-2 mb-2">
                <Headphones className="h-4 w-4 text-teal-600 dark:text-teal-400" />
                <span className="text-xs font-semibold text-teal-800 dark:text-teal-200">
                  {isEn ? '🎙️ Workplace Application Audio (Explanation for you)' : '🎙️ Audio del Caso Práctico (Explicación para ti)'}
                </span>
              </div>
              <audio
                key={audioTimestamp}
                controls
                src={`${data.AudioUrl}${data.AudioUrl.includes('?') ? '&' : '?'}t=${audioTimestamp}`}
                className="w-full"
                onEnded={onComplete}
              >
                {isEn ? 'Your browser does not support audio playback.' : 'Tu navegador no soporta el reproductor de audio.'}
              </audio>
            </div>
          )}

          {/* Text Scenario */}
          <div className="space-y-3">
            <h4 className="font-semibold text-slate-900 dark:text-white text-base">
              {isEn ? 'Scenario in your work environment' : 'Escenario en tu entorno laboral'}
            </h4>
            <div className="rounded-2xl border-l-4 border-teal-400 dark:border-teal-500/60 bg-slate-50/70 dark:bg-white/[0.03] pl-5 pr-4 py-4 space-y-3">
              {groupIntoParagraphs(data.ScenarioText).map((paragraph, i) => (
                <p key={i} className="text-sm leading-7 text-slate-700 dark:text-slate-300">
                  {paragraph}
                </p>
              ))}
            </div>
          </div>

          {data.PracticalActions.length > 0 && (
            <div className="rounded-2xl border border-slate-200 bg-slate-50/50 p-4 space-y-2 dark:border-white/10 dark:bg-white/[0.03]">
              <h4 className="font-semibold text-xs uppercase tracking-wide text-teal-700 dark:text-teal-300">
                {isEn ? 'Suggested practical actions in your projects' : 'Acciones prácticas sugeridas en tus proyectos'}
              </h4>
              <ul className="list-disc pl-5 space-y-1 text-xs text-slate-700 dark:text-slate-300">
                {data.PracticalActions.map((action, i) => (
                  <li key={i}>{action}</li>
                ))}
              </ul>
            </div>
          )}

          {data.WhyThisMatters && (
            <div className="space-y-3 pt-2">
              <h4 className="font-semibold text-slate-900 dark:text-white text-base">
                {isEn ? 'Why This Matters' : 'Por Qué Esto Importa'}
              </h4>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {isEn
                  ? 'Grounded in this lesson\u2019s content, your role/objectives, and your CQI strengths.'
                  : 'Basado en el contenido de esta lecci\u00f3n, tu rol/objetivos y tus fortalezas CQI.'}
              </p>
              {cqiProfile?.anchorTypeName && (
                <button
                  type="button"
                  onClick={() => setIsCqiModalOpen(true)}
                  className="inline-flex items-center gap-1.5 rounded-full border border-teal-300 bg-teal-50 px-3 py-1 text-xs font-medium text-teal-800 dark:border-teal-500/30 dark:bg-teal-500/10 dark:text-teal-200 hover:bg-teal-100 dark:hover:bg-teal-500/20 transition-colors cursor-pointer"
                >
                  <Sparkles className="h-3 w-3" />
                  {isEn
                    ? `See how these strengths connect to your ${cqiProfile.anchorTypeName} profile`
                    : `Ve c\u00f3mo estas fortalezas se conectan con tu perfil ${cqiProfile.anchorTypeName}`}
                </button>
              )}
              <ul className="list-disc pl-5 space-y-2 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
                {parseBulletLines(data.WhyThisMatters).map((line, i) => (
                  <li key={i}>{line}</li>
                ))}
              </ul>
              {data.WhyThisMatters.toLowerCase().includes('hermanscience') && (
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {isEn ? 'Learn more about your CQI assessment at ' : 'Conoce más sobre tu evaluación CQI en '}
                  <a
                    href="https://hermanplatform.net/"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-teal-700 dark:text-teal-300 underline hover:text-teal-800 dark:hover:text-teal-200"
                  >
                    hermanplatform.net
                  </a>
                  .
                </p>
              )}
            </div>
          )}

          {!!data.ReflectionQuestions?.length && (
            <div className="space-y-3 pt-2">
              <h4 className="font-semibold text-slate-900 dark:text-white text-base">
                {isEn ? 'Personalized Reflection' : 'Reflexión Personalizada'}
              </h4>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                {isEn
                  ? 'These questions connect this lesson to your role, tools, objectives, and CQI strengths above.'
                  : 'Estas preguntas conectan esta lecci\u00f3n con tu rol, herramientas, objetivos y las fortalezas CQI de arriba.'}
              </p>
              <ol className="space-y-3 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
                {data.ReflectionQuestions.map((question, i) => (
                  <li key={i} className="flex gap-3">
                    <span className="font-semibold text-teal-700 dark:text-teal-300">{i + 1}.</span>
                    <span>{question.replace(/^\d+[.)]\s*/, '')}</span>
                  </li>
                ))}
              </ol>
            </div>
          )}

          {data.GrowthOpportunity && (
            <div className="space-y-3 pt-2">
              <h4 className="font-semibold text-slate-900 dark:text-white text-base">
                {isEn ? 'Growth Opportunity' : 'Oportunidad de Crecimiento'}
              </h4>
              <ul className="list-disc pl-5 space-y-2 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
                {parseBulletLines(data.GrowthOpportunity).map((line, i) => (
                  <li key={i}>{line}</li>
                ))}
              </ul>
            </div>
          )}

          {!!data.KnowledgeConnections?.length && (
            <div className="space-y-3 pt-2">
              <h4 className="font-semibold text-slate-900 dark:text-white text-base">
                {isEn ? 'Knowledge Connections' : 'Conexiones de Conocimiento'}
              </h4>
              <div className="flex flex-wrap gap-2">
                {data.KnowledgeConnections.map((tag, i) => (
                  <span
                    key={i}
                    className="inline-flex items-center gap-1 rounded-full border border-teal-300 bg-teal-50 px-3 py-1.5 text-xs font-medium text-teal-800 dark:border-teal-500/30 dark:bg-teal-500/10 dark:text-teal-200"
                  >
                    ✓ {tag}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* CQI Type recognition — inline, part of the same learning artifact (no popup). */}
          {data.CqiTypeLabel && (
            <div className="space-y-3 pt-2 border-t border-slate-200 dark:border-white/10 mt-2">
              <div className="rounded-2xl border border-teal-200 bg-teal-50/70 dark:border-teal-500/20 dark:bg-teal-500/10 p-4">
                <h4 className="font-semibold text-slate-900 dark:text-white text-sm mb-1 flex items-center gap-1.5">
                  <Sparkles className="h-4 w-4 text-teal-600 dark:text-teal-300" />
                  {isEn ? 'Learning and Working Preferences' : 'Preferencias de Aprendizaje y Trabajo'}
                </h4>
                <p className="text-sm leading-relaxed text-slate-700 dark:text-slate-300">
                  {isEn
                    ? 'Learning may work best when focused reflection can alternate with discussion, examples, and practical application. '
                    : 'El aprendizaje puede funcionar mejor cuando la reflexión enfocada se alterna con discusión, ejemplos y aplicación práctica. '}
                  {isEn
                    ? `Your pattern most strongly aligns with ${data.CqiTypeLabel}.`
                    : `Tu patrón se alinea más fuertemente con ${data.CqiTypeLabel}.`}
                </p>
                {data.CqiLearningAndWorkingPreferences && (
                  <p className="mt-2 text-xs text-slate-600 dark:text-slate-400">{data.CqiLearningAndWorkingPreferences}</p>
                )}
              </div>

              {!!data.CqiExampleImageUrls?.length && (
                <div className="space-y-3">
                  <h4 className="font-semibold text-slate-900 dark:text-white text-base">
                    {isEn ? 'More examples applied to your job' : 'Más ejemplos aplicados a tu puesto'}
                  </h4>
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    {data.CqiExampleImageUrls.map((url, i) => (
                      <div key={i} className="rounded-2xl border border-slate-200 bg-slate-50/70 dark:border-white/10 dark:bg-white/[0.03] overflow-hidden">
                        <ZoomableImageByUrl
                          src={url}
                          alt={isEn ? `Workplace example ${i + 1}` : `Ejemplo laboral ${i + 1}`}
                          className="w-full object-cover"
                        />
                        {data.CqiExampleImageExplanations?.[i] && (
                          <p className="p-4 text-base leading-relaxed text-slate-700 dark:text-slate-300">
                            {data.CqiExampleImageExplanations[i]}
                          </p>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Slide-over Side Drawer for Q&A with Professor */}
      {isChatOpen && (
        <div className="fixed inset-0 z-50 overflow-hidden bg-slate-950/70 backdrop-blur-sm animate-fade-in flex justify-end">
          <div className="w-full max-w-xl sm:max-w-2xl bg-white dark:bg-slate-900 border-l border-slate-200 dark:border-teal-500/30 shadow-2xl flex flex-col h-full transform transition-transform duration-300 ease-out">
            {/* Header */}
            <div className="p-4 border-b border-slate-200 dark:border-white/10 flex items-center justify-between bg-slate-50 dark:bg-slate-950/50">
              <div className="flex items-center gap-2.5">
                <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-teal-100 text-teal-600 dark:bg-teal-500/20 dark:text-teal-300">
                  <Bot className="h-5 w-5" />
                </div>
                <div>
                  <h3 className="text-sm font-bold text-slate-900 dark:text-white leading-tight">
                    {isEn ? 'Q&A with Professor' : 'Q&A con el Profesor'}
                  </h3>
                  <p className="text-[11px] text-slate-500 dark:text-slate-400 leading-tight">
                    {data?.StudentName ? `Professor for ${data.StudentName}` : 'Personalized AI Professor'} · gpt-5-mini
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-1.5">
                {chatMessages.length > 0 && (
                  <button
                    type="button"
                    onClick={clearChat}
                    title={isEn ? 'Clear chat' : 'Limpiar chat'}
                    className="inline-flex items-center gap-1 rounded-lg border border-slate-200 bg-white hover:bg-red-50 hover:border-red-300 dark:border-white/10 dark:bg-white/5 dark:hover:bg-red-500/15 dark:hover:border-red-500/30 px-2.5 py-1 text-[11px] font-medium text-slate-600 hover:text-red-700 dark:text-slate-300 dark:hover:text-red-200 transition-colors cursor-pointer"
                  >
                    <Trash2 className="h-3 w-3" />
                    <span>{isEn ? 'Clear' : 'Limpiar'}</span>
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => setIsChatOpen(false)}
                  className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white transition-colors cursor-pointer"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>
            </div>

            {/* Message Thread */}
            <div className="flex-1 overflow-y-auto p-4 sm:p-5 space-y-4">
              {chatMessages.length === 0 ? (
                <div className="space-y-4 my-auto pt-6 text-center">
                  <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-teal-50 text-teal-600 border border-teal-200 dark:bg-teal-500/10 dark:text-teal-400 dark:border-teal-500/20">
                    <MessageSquareText className="h-6 w-6" />
                  </div>
                  <p className="text-xs text-slate-600 dark:text-slate-300 max-w-sm mx-auto leading-relaxed">
                    {isEn
                      ? 'Have questions about how to adapt this scenario to your tech stack, mitigate edge cases, or execute these actions? Ask your professor below.'
                      : '¿Tienes dudas sobre cómo adaptar este caso a tu stack, riesgos a evitar o cómo ejecutar las acciones? Pregúntale a tu profesor aquí.'}
                  </p>

                  <div className="pt-2 flex flex-col gap-2 text-left">
                    <button
                      type="button"
                      onClick={() => sendChatMessage(isEn ? 'How do I adapt this scenario to my current stack and constraints?' : '¿Cómo adapto este escenario a las restricciones de mi stack actual?')}
                      className="text-left text-xs rounded-xl border border-slate-200 bg-slate-50 hover:bg-slate-100 dark:border-white/10 dark:bg-white/[0.03] dark:hover:bg-white/[0.08] p-3 text-teal-800 dark:text-teal-200 transition-colors cursor-pointer"
                    >
                      💡 {isEn ? 'How do I adapt this scenario to my current stack and constraints?' : '¿Cómo adapto este escenario a las restricciones de mi stack actual?'}
                    </button>
                    <button
                      type="button"
                      onClick={() => sendChatMessage(isEn ? 'What potential edge cases or risks should I watch out for?' : '¿Qué casos extremos o riesgos de seguridad debo prever?')}
                      className="text-left text-xs rounded-xl border border-slate-200 bg-slate-50 hover:bg-slate-100 dark:border-white/10 dark:bg-white/[0.03] dark:hover:bg-white/[0.08] p-3 text-teal-800 dark:text-teal-200 transition-colors cursor-pointer"
                    >
                      🛡️ {isEn ? 'What potential edge cases or risks should I watch out for?' : '¿Qué casos extremos o riesgos de seguridad debo prever?'}
                    </button>
                    <button
                      type="button"
                      onClick={() => sendChatMessage(isEn ? 'Can you give me a step-by-step checklist to start this week?' : '¿Puedes darme una lista de pasos concretos para arrancar esta semana?')}
                      className="text-left text-xs rounded-xl border border-slate-200 bg-slate-50 hover:bg-slate-100 dark:border-white/10 dark:bg-white/[0.03] dark:hover:bg-white/[0.08] p-3 text-teal-800 dark:text-teal-200 transition-colors cursor-pointer"
                    >
                      📋 {isEn ? 'Can you give me a step-by-step checklist to start this week?' : '¿Puedes darme una lista de pasos concretos para arrancar esta semana?'}
                    </button>
                  </div>
                </div>
              ) : (
                chatMessages.map((m, idx) => (
                  <div
                    key={idx}
                    className={`flex flex-col ${m.role === 'user' ? 'items-end' : 'items-start'}`}
                  >
                    {m.role === 'user' ? (
                      <div className="max-w-[85%] rounded-2xl px-4 py-2.5 text-xs leading-relaxed bg-teal-600 text-white rounded-br-none shadow-md">
                        {m.text}
                      </div>
                    ) : (
                      <div className="w-full max-w-[96%] rounded-2xl p-4 text-xs leading-relaxed bg-slate-50 border border-slate-200 dark:bg-white/[0.04] dark:border-white/10 text-slate-800 dark:text-slate-200 rounded-bl-none shadow-md">
                        <MentorMessageContent text={m.text} onPromptClick={(opt) => sendChatMessage(opt)} isEn={isEn} />
                      </div>
                    )}
                  </div>
                ))
              )}

              {chatLoading && (
                <div className="flex items-center gap-2 text-xs text-teal-600 dark:text-teal-300 py-2">
                  <Loader2 className="h-4 w-4 animate-spin text-teal-500 dark:text-teal-400" />
                  <span>Thinking...</span>
                </div>
              )}
            </div>

            {/* Input Bar */}
            <div className="p-3 border-t border-slate-200 dark:border-white/10 bg-slate-50 dark:bg-slate-950/70">
              <form
                onSubmit={(e) => {
                  e.preventDefault();
                  sendChatMessage();
                }}
                className="flex items-center gap-2"
              >
                <input
                  type="text"
                  value={chatInput}
                  onChange={(e) => setChatInput(e.target.value)}
                  placeholder={isEn ? 'Ask a question about this scenario or implementation...' : 'Pregunta sobre la implementación, riesgos o pasos concretos...'}
                  disabled={chatLoading}
                  className="flex-1 rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-xs text-slate-900 placeholder:text-slate-400 focus:border-teal-500 focus:outline-none dark:border-white/10 dark:bg-white/5 dark:text-white dark:placeholder:text-slate-400 dark:focus:border-teal-400"
                />
                <button
                  type="submit"
                  disabled={chatLoading || !chatInput.trim()}
                  className="inline-flex items-center justify-center rounded-xl bg-teal-600 hover:bg-teal-700 disabled:opacity-40 p-2.5 text-white shadow-md transition-colors cursor-pointer"
                >
                  <Send className="h-4 w-4" />
                </button>
              </form>
            </div>
          </div>
        </div>
      )}

      {/* CQI Type Profile modal ("Anchor Foundation") */}
      {isCqiModalOpen && cqiProfile && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-slate-950/70 backdrop-blur-sm animate-fade-in flex items-start justify-center p-4 sm:p-8">
          <div className="w-full max-w-2xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-teal-500/30 rounded-3xl shadow-2xl my-8">
            <div className="p-5 border-b border-slate-200 dark:border-white/10 flex items-center justify-between bg-slate-50 dark:bg-slate-950/50 rounded-t-3xl">
              <div>
                <h3 className="text-base font-bold text-slate-900 dark:text-white">
                  {isEn ? 'Anchor Foundation' : 'Fundamento Ancla'}
                </h3>
                <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">{cqiProfile.anchorTypeName}</p>
              </div>
              <button
                type="button"
                onClick={() => setIsCqiModalOpen(false)}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white transition-colors cursor-pointer"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="p-6 space-y-5 text-sm leading-relaxed text-slate-700 dark:text-slate-300">
              {data?.WhyThisMatters && (
                <p className="text-xs italic text-teal-700 dark:text-teal-300 border-b border-slate-200 dark:border-white/10 pb-4">
                  {isEn
                    ? "This foundation is what shapes the strengths called out in \u201cWhy This Matters\u201d for this lesson."
                    : 'Este fundamento es lo que da forma a las fortalezas mencionadas en "Por Qu\u00e9 Esto Importa" para esta lecci\u00f3n.'}
                </p>
              )}
              {cqiProfile.anchorDescription && <p>{cqiProfile.anchorDescription}</p>}

              {cqiProfile.whatMayDrawAttention && (
                <div className="space-y-1.5">
                  <h4 className="font-semibold text-slate-900 dark:text-white text-sm">
                    {isEn ? 'What may draw attention' : 'Qué puede llamar la atención'}
                  </h4>
                  <p>{cqiProfile.whatMayDrawAttention}</p>
                </div>
              )}

              {cqiProfile.howChoicesMayTakeShape && (
                <div className="space-y-1.5">
                  <h4 className="font-semibold text-slate-900 dark:text-white text-sm">
                    {isEn ? 'How choices may take shape' : 'Cómo pueden tomar forma las decisiones'}
                  </h4>
                  <p>{cqiProfile.howChoicesMayTakeShape}</p>
                </div>
              )}

              {cqiProfile.workAndCommunication && (
                <div className="space-y-1.5">
                  <h4 className="font-semibold text-slate-900 dark:text-white text-sm">
                    {isEn ? 'Work and communication' : 'Trabajo y comunicación'}
                  </h4>
                  <p>{cqiProfile.workAndCommunication}</p>
                </div>
              )}

              {cqiProfile.whatMaySupportBalance && (
                <div className="space-y-1.5">
                  <h4 className="font-semibold text-slate-900 dark:text-white text-sm">
                    {isEn ? 'What may support balance' : 'Qué puede apoyar el equilibrio'}
                  </h4>
                  <p>{cqiProfile.whatMaySupportBalance}</p>
                </div>
              )}

              {cqiProfile.potentialStrengths.length > 0 && (
                <div className="space-y-1.5">
                  <h4 className="font-semibold text-slate-900 dark:text-white text-sm">
                    {isEn ? 'Potential strengths' : 'Fortalezas potenciales'}
                  </h4>
                  <ul className="list-disc pl-5 space-y-1">
                    {cqiProfile.potentialStrengths.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </div>
              )}

              {cqiProfile.patternsToWatchUnderPressure.length > 0 && (
                <div className="space-y-1.5">
                  <h4 className="font-semibold text-slate-900 dark:text-white text-sm">
                    {isEn ? 'Patterns to watch under pressure' : 'Patrones a observar bajo presión'}
                  </h4>
                  <ul className="list-disc pl-5 space-y-1">
                    {cqiProfile.patternsToWatchUnderPressure.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ul>
                </div>
              )}

              <a
                href="/cqi-workspace"
                className="inline-flex items-center gap-1 text-xs text-teal-700 dark:text-teal-300 underline hover:text-teal-800 dark:hover:text-teal-200"
              >
                {isEn ? 'Open full CQI Workspace' : 'Abrir Espacio de Trabajo CQI completo'}
              </a>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
