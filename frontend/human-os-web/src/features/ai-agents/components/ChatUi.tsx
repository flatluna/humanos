import { useEffect, useState } from 'react';
import { Bot, Mic, X } from 'lucide-react';
import { getStructuredLearningDocument, type StructuredLearningStepDetail } from '@/features/capabilities/api/structuredLearningRuntimeApi';
import VoiceTutorAgent from '@/features/capabilities/components/VoiceTutorAgent';
import { API_BASE_URL } from '@/features/capabilities/api/httpClient';

/** Shared chat UI pieces for the AI Agents feature (course-level and
 * topic/domain-level agents both cite Engrams and render replies the same
 * way) — extracted so both AiAgentCoursePage and AiAgentTopicPage reuse
 * the exact same modal/formatting instead of drifting apart. */

export interface UsedEngram {
  engramId: string;
  documentId: string;
  title: string;
  courseName: string;
  text?: string;
}

/** Renders the agent's plain-text reply as separate paragraphs (split on
 * blank lines) and numbered/bulleted lines as a real list — LLM replies
 * come back with real line breaks that a single `<div>{text}</div>` was
 * collapsing into one crowded blob. No markdown lib involved, just basic
 * structure so multi-point answers read as a list instead of one wall of
 * text. */
export function MessageBody({ text }: { text: string }) {
  const paragraphs = text.trim().split(/\n\s*\n/);
  return (
    <div className="space-y-2.5">
      {paragraphs.map((paragraph, paragraphIndex) => {
        const lines = paragraph.split('\n').map((line) => line.trim()).filter(Boolean);
        const isList = lines.length > 1 && lines.every((line) => /^([-*•]|\d+[.)])\s+/.test(line));
        if (isList) {
          return (
            <ul key={paragraphIndex} className="list-disc space-y-1 pl-5">
              {lines.map((line, lineIndex) => (
                <li key={lineIndex}>{line.replace(/^([-*•]|\d+[.)])\s+/, '')}</li>
              ))}
            </ul>
          );
        }
        return <p key={paragraphIndex} className="whitespace-pre-wrap">{paragraph}</p>;
      })}
    </div>
  );
}

/** Cited-Engram modal — shows the Engram's own text, its Teaching step's
 * diagram/illustration, and a REAL real-time voice agent grounded on it
 * (resolves the Engram's own Teaching step and hands it to VoiceTutorAgent,
 * same blueprint-only path used elsewhere in this codebase), so the
 * student can ask it to explain that specific Engram out loud instead of
 * just reading it silently. */
export function EngramModal({ engram, onClose }: { engram: UsedEngram; onClose: () => void }) {
  const [teachingStep, setTeachingStep] = useState<StructuredLearningStepDetail | null>(null);
  const [zoomedImage, setZoomedImage] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setTeachingStep(null);
    getStructuredLearningDocument(engram.documentId)
      .then((doc) => {
        if (cancelled) return;
        const section = doc.Sections.find((s) => s.StructuredLearningDocumentSectionId === engram.engramId);
        setTeachingStep(section?.Steps.find((s) => s.StepType === 'Teaching') ?? null);
      })
      .catch(() => !cancelled && setTeachingStep(null));
    return () => {
      cancelled = true;
    };
  }, [engram.documentId, engram.engramId]);

  let illustrationId: string | null = null;
  if (teachingStep?.StepDataJson) {
    try {
      illustrationId = (JSON.parse(teachingStep.StepDataJson) as { IllustrationId?: string | null }).IllustrationId ?? null;
    } catch {
      illustrationId = null;
    }
  }

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/70 p-4 backdrop-blur-sm" onClick={onClose}>
      <div className="max-h-[85vh] w-full max-w-2xl overflow-y-auto rounded-2xl border border-cyan-300/20 bg-slate-900 p-6 shadow-2xl shadow-black/40" onClick={(event) => event.stopPropagation()}>
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.14em] text-cyan-300"><Bot className="h-3.5 w-3.5" /> Engram utilizado</p>
            <h2 className="mt-2 text-lg font-semibold text-white">{engram.title}</h2>
            <p className="mt-1 text-xs text-slate-400">Curso: {engram.courseName}</p>
          </div>
          <button type="button" onClick={onClose} aria-label="Cerrar Engram" className="rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white"><X className="h-5 w-5" /></button>
        </div>
        {teachingStep && (
          <div className="mt-4 flex items-center justify-between gap-3 rounded-xl border border-violet-300/20 bg-violet-500/[0.06] px-3 py-2">
            <span className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-[0.14em] text-violet-300"><Mic className="h-3.5 w-3.5" /> Agente Engram voz</span>
            <VoiceTutorAgent structuredLearningSectionStepId={teachingStep.StructuredLearningSectionStepId} compact />
          </div>
        )}
        {illustrationId && (
          <button type="button" onClick={() => setZoomedImage(true)} className="mt-4 block w-full cursor-zoom-in" aria-label="Ampliar diagrama">
            <img src={`${API_BASE_URL}/illustrations/structured-learning/${illustrationId}/image`} alt={engram.title} className="h-64 w-full rounded-xl bg-slate-950/50 object-contain object-center" />
          </button>
        )}
        <p className="mt-5 whitespace-pre-wrap text-sm leading-7 text-slate-300">{engram.text ?? 'Contenido del Engram no disponible.'}</p>
      </div>
      {zoomedImage && illustrationId && (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/90 p-4 cursor-zoom-out" onClick={(event) => { event.stopPropagation(); setZoomedImage(false); }}>
          <img src={`${API_BASE_URL}/illustrations/structured-learning/${illustrationId}/image`} alt={engram.title} className="max-h-full max-w-full rounded-xl object-contain" />
          <button type="button" onClick={(event) => { event.stopPropagation(); setZoomedImage(false); }} aria-label="Cerrar imagen" className="absolute right-4 top-4 rounded-full bg-white/10 p-2 text-white hover:bg-white/20"><X className="h-5 w-5" /></button>
        </div>
      )}
    </div>
  );
}
