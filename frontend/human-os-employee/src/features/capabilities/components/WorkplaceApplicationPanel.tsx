import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { Sparkles, Headphones, Loader2, MessageSquareText, Trash2, X, Send } from 'lucide-react';
import {
  generateStructuredLearningWorkplaceApplication,
  getStructuredLearningWorkplaceApplication,
  postStructuredLearningWorkplaceChat,
  type WorkplaceApplicationDto,
  type WorkplaceWorkedExample,
} from '../api/structuredLearningRuntimeApi';

function ResponseText({ text }: { text: string }) {
  return (
    <div className="space-y-3 break-words text-sm leading-7">
      {text.split(/(```[\s\S]*?```)/g).filter(Boolean).map((part, index) =>
        part.startsWith('```') ? (
          <pre key={index} className="overflow-x-auto rounded-xl bg-slate-950 p-4 text-xs leading-6 text-teal-100">
            <code>{part.replace(/^```[^\n]*\n?/, '').replace(/```$/, '').trim()}</code>
          </pre>
        ) : (
          <p key={index} className="whitespace-pre-wrap">
            {part.split(/(\*\*[^*]+\*\*)/g).map((span, i) =>
              span.startsWith('**') && span.endsWith('**')
                ? <strong key={i}>{span.slice(2, -2)}</strong>
                : span
            )}
          </p>
        )
      )}
    </div>
  );
}

function WorkedExample({ example, index, isEn }: { example: WorkplaceWorkedExample; index: number; isEn: boolean }) {
  return (
    <article className="min-w-0 rounded-2xl border border-slate-200 bg-white p-5 sm:p-6 dark:border-white/10 dark:bg-slate-900/70 space-y-5">
      <div>
        <span className="text-xs font-semibold uppercase tracking-wide text-teal-700 dark:text-teal-300">
          {isEn ? `Worked example ${index + 1}` : `Ejemplo resuelto ${index + 1}`}
        </span>
        <h4 className="mt-1 text-lg font-bold">{example.Title}</h4>
        <ResponseText text={example.Situation} />
      </div>
      <section>
        <h5 className="mb-1 text-sm font-semibold">{isEn ? 'Inputs and assumptions' : 'Datos y supuestos'}</h5>
        <ResponseText text={example.InputsAndAssumptions} />
      </section>
      <section>
        <h5 className="mb-2 text-sm font-semibold">{isEn ? 'Step-by-step solution' : 'Soluci\u00f3n paso a paso'}</h5>
        <ol className="list-decimal space-y-2 pl-5 text-sm leading-7">
          {example.Steps.map((step, i) => <li key={i}><ResponseText text={step.replace(/^\d+[.)]\s*/, '')} /></li>)}
        </ol>
      </section>
      <section>
        <div className="mb-2 flex items-center justify-between gap-2">
          <h5 className="text-sm font-semibold">{isEn ? 'Practical artifact' : 'Ejemplo pr\u00e1ctico'}</h5>
          <span className="text-xs text-slate-500 dark:text-slate-400">{example.ArtifactLanguage}</span>
        </div>
        <pre className="max-w-full overflow-x-auto rounded-xl border border-teal-500/20 bg-slate-950 p-4 text-sm leading-6 text-teal-100">
          <code>{example.Artifact}</code>
        </pre>
      </section>
      <section className="rounded-xl bg-teal-50 p-4 dark:bg-teal-500/10">
        <h5 className="mb-1 text-sm font-semibold text-teal-800 dark:text-teal-200">{isEn ? 'Expected result' : 'Resultado esperado'}</h5>
        <ResponseText text={example.ExpectedResult} />
      </section>
      <section>
        <h5 className="mb-1 text-sm font-semibold">{isEn ? 'How to validate it' : 'C\u00f3mo validarlo'}</h5>
        <ResponseText text={example.Validation} />
      </section>
    </article>
  );
}

/** Shared by the section page and the Brain modal's In Your Role tab. */
export function WorkplaceApplicationStep({
  sectionId, personId, courseLanguage, onComplete, isEn, isStudent, engramSessionId,
}: {
  sectionId: string;
  personId?: string;
  courseLanguage?: 'es' | 'en';
  onComplete: () => void;
  isEn: boolean;
  isStudent: boolean;
  engramSessionId?: string;
}) {
  const [data, setData] = useState<WorkplaceApplicationDto | null>(null);
  const [instructions, setInstructions] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [audioTimestamp, setAudioTimestamp] = useState(Date.now());
  const [isChatOpen, setIsChatOpen] = useState(false);
  const [chatMessages, setChatMessages] = useState<Array<{ role: 'user' | 'assistant'; text: string }>>([]);
  const [chatInput, setChatInput] = useState('');
  const [chatLoading, setChatLoading] = useState(false);
  const [chatError, setChatError] = useState<string | null>(null);
  const [chatSessionId, setChatSessionId] = useState<string | undefined>();
  const requestVersion = useRef(0);
  const completeRef = useRef(onComplete);
  const sessionRef = useRef(engramSessionId);
  const instructionsEdited = useRef(false);
  const inputId = useId();
  const activeLanguage = courseLanguage || (isEn ? 'en' : 'es');

  useEffect(() => { completeRef.current = onComplete; }, [onComplete]);
  useEffect(() => { sessionRef.current = engramSessionId; }, [engramSessionId]);

  useEffect(() => {
    const version = ++requestVersion.current;
    setData(null);
    setInstructions('');
    instructionsEdited.current = false;
    setError(null);
    setLoading(true);
    setIsChatOpen(false);
    setChatMessages([]);
    setChatSessionId(undefined);
    setChatInput('');
    setChatError(null);
    setChatLoading(false);
    getStructuredLearningWorkplaceApplication(sectionId, personId, activeLanguage, false, sessionRef.current)
      .then((result) => {
        if (version !== requestVersion.current) return;
        setData(result);
        if (result) {
          if (!instructionsEdited.current) setInstructions(result.UserInstructions || '');
          setAudioTimestamp(Date.now());
          completeRef.current();
        }
      })
      .catch((err: unknown) => {
        if (version === requestVersion.current) setError(err instanceof Error ? err.message : String(err));
      })
      .finally(() => {
        if (version === requestVersion.current) setLoading(false);
      });
    return () => { ++requestVersion.current; };
  }, [sectionId, personId, activeLanguage, engramSessionId]);

  useEffect(() => {
    if (!isChatOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setIsChatOpen(false);
    };
    window.addEventListener('keydown', closeOnEscape);
    return () => window.removeEventListener('keydown', closeOnEscape);
  }, [isChatOpen]);

  async function generate(event: FormEvent) {
    event.preventDefault();
    const prompt = instructions.trim();
    if (!prompt || loading) return;
    const version = ++requestVersion.current;
    setLoading(true);
    setError(null);
    try {
      const result = await generateStructuredLearningWorkplaceApplication(
        sectionId, prompt, personId, activeLanguage, engramSessionId
      );
      if (version !== requestVersion.current) return;
      setData(result);
      setInstructions((draft) => draft.trim() === prompt ? result.UserInstructions : draft);
      setAudioTimestamp(Date.now());
      setChatMessages([]);
      setChatSessionId(undefined);
      setChatError(null);
      setChatLoading(false);
      completeRef.current();
    } catch (err) {
      if (version === requestVersion.current) setError(err instanceof Error ? err.message : String(err));
    } finally {
      if (version === requestVersion.current) setLoading(false);
    }
  }

  async function sendChat(event: FormEvent) {
    event.preventDefault();
    const message = chatInput.trim();
    if (!message || chatLoading) return;
    const version = requestVersion.current;
    setChatLoading(true);
    setChatError(null);
    setChatMessages((previous) => [...previous, { role: 'user', text: message }]);
    setChatInput('');
    try {
      const result = await postStructuredLearningWorkplaceChat(sectionId, message, chatSessionId, personId, activeLanguage);
      if (version !== requestVersion.current) return;
      setChatSessionId(result.SessionId);
      setChatMessages((previous) => [...previous, { role: 'assistant', text: result.Reply }]);
    } catch (err) {
      if (version === requestVersion.current) setChatError(err instanceof Error ? err.message : String(err));
    } finally {
      if (version === requestVersion.current) setChatLoading(false);
    }
  }

  return (
    <div className="mt-4 space-y-6 text-slate-800 dark:text-slate-200">
      <form onSubmit={generate} className="rounded-3xl border border-teal-200 bg-gradient-to-br from-teal-50 via-white to-emerald-50 p-6 sm:p-8 dark:border-teal-500/30 dark:from-teal-500/10 dark:via-slate-950 dark:to-emerald-500/10">
        <div className="mb-4 flex items-start gap-3">
          <Sparkles className="mt-1 h-6 w-6 shrink-0 text-teal-600 dark:text-teal-300" />
          <div>
            <h3 className="text-xl font-bold">{isEn ? 'Apply this lesson your way' : 'Aplica esta lecci\u00f3n a tu caso'}</h3>
            <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-slate-300">
              {isEn
                ? `Describe the problem you want to solve. Get a worked application for your ${isStudent ? 'studies and career preparation' : 'role'}, with code, calculations, formulas or ready-to-use text when appropriate.`
                : `Describe el problema que quieres resolver. Recibe una aplicaci\u00f3n para ${isStudent ? 'tus estudios y preparaci\u00f3n profesional' : 'tu puesto'}, con c\u00f3digo, c\u00e1lculos, f\u00f3rmulas o textos listos para usar cuando corresponda.`}
            </p>
          </div>
        </div>
        <label htmlFor={inputId} className="mb-2 block text-sm font-semibold">{isEn ? 'Your instructions to the agent' : 'Tus instrucciones para el agente'}</label>
        <textarea
          id={inputId}
          value={instructions}
          onChange={(event) => { instructionsEdited.current = true; setInstructions(event.target.value); }}
          maxLength={4000}
          rows={5}
          required
          aria-describedby={`${inputId}-help`}
          placeholder={isEn
            ? 'How can I apply this knowledge to a finance problem? Show a realistic example with sample data, formulas and the expected result, explaining my contribution in my role.'
            : '\u00bfC\u00f3mo aplico este conocimiento a un problema del \u00e1rea de finanzas? Muestra un ejemplo realista con datos de muestra, f\u00f3rmulas y el resultado esperado, explicando mi aportaci\u00f3n desde mi puesto.'}
          className="w-full resize-y rounded-xl border border-slate-300 bg-white p-4 text-sm leading-6 focus:border-teal-500 focus:outline-none focus:ring-2 focus:ring-teal-500/20 dark:border-white/20 dark:bg-slate-900"
        />
        <div className="mt-3 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p id={`${inputId}-help`} className="text-xs text-slate-500 dark:text-slate-400">
            {isEn ? 'The latest result is saved for you and this lesson.' : 'Se guarda tu \u00faltimo resultado para esta lecci\u00f3n.'} {instructions.length}/4000
          </p>
          <button type="submit" disabled={loading || !instructions.trim()} className="inline-flex items-center justify-center gap-2 rounded-xl bg-teal-600 px-5 py-3 text-sm font-semibold text-white hover:bg-teal-700 disabled:cursor-not-allowed disabled:opacity-50">
            {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
            {isEn ? 'Generate application' : 'Generar aplicaci\u00f3n'}
          </button>
        </div>
      </form>

      {loading && <p role="status" className="flex items-center gap-2 text-sm text-teal-700 dark:text-teal-300"><Loader2 className="h-4 w-4 animate-spin" />{isEn ? 'Preparing your application and worked examples...' : 'Preparando tu aplicaci\u00f3n y ejemplos resueltos...'}</p>}
      {error && <div role="alert" className="rounded-xl border border-red-300 bg-red-50 p-4 text-sm text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">{error}<p className="mt-2">{isEn ? 'The request could not be completed. Submit your instructions to try again.' : 'No se pudo completar la petici\u00f3n. Env\u00eda tus instrucciones para reintentar.'}</p></div>}

      {data && !loading && (
        <div className="space-y-6">
          <header className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
            <div>
              <h2 className="text-2xl font-bold">{data.ScenarioTitle}</h2>
              <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{data.StudentName} &middot; {data.RoleTitle}</p>
            </div>
            <button type="button" onClick={() => setIsChatOpen(true)} className="inline-flex items-center justify-center gap-2 rounded-xl border border-teal-300 px-4 py-2 text-sm font-semibold text-teal-800 dark:border-teal-500/40 dark:text-teal-200"><MessageSquareText className="h-4 w-4" />{isEn ? 'Ask the Professor' : 'Preguntar al Profesor'}</button>
          </header>
          {data.UserInstructions && <section className="rounded-xl border border-slate-200 p-4 dark:border-white/10"><h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-teal-700 dark:text-teal-300">{isEn ? 'Request used for this result' : 'Petici\u00f3n utilizada para este resultado'}</h3><p className="whitespace-pre-wrap text-sm">{data.UserInstructions}</p></section>}
          <section className="rounded-2xl bg-slate-50 p-5 dark:bg-white/[0.03] space-y-4">
            <h3 className="text-lg font-semibold">{isEn ? 'How to apply it' : 'C\u00f3mo aplicarlo'}</h3>
            <ResponseText text={data.ScenarioText} />
            {data.AudioUrl && <div className="border-t border-slate-200 pt-4 dark:border-white/10"><p className="mb-2 flex items-center gap-2 text-xs font-semibold text-teal-700 dark:text-teal-300"><Headphones className="h-4 w-4" />{isEn ? 'Listen to the application overview' : 'Escucha el resumen de la aplicaci\u00f3n'}</p><audio key={audioTimestamp} controls src={`${data.AudioUrl}${data.AudioUrl.includes('?') ? '&' : '?'}t=${audioTimestamp}`} className="w-full" onEnded={onComplete} /></div>}
          </section>
          {data.WhyThisMatters && <section><h3 className="mb-3 text-lg font-semibold">{isEn ? 'Why This Matters' : 'Por qu\u00e9 esto importa'}</h3><ul className="list-disc space-y-2 pl-5">{data.WhyThisMatters.split(/\r?\n/).map((line) => line.trim().replace(/^[-*\u2022]\s*/, '')).filter(Boolean).map((line, i) => <li key={i}><ResponseText text={line} /></li>)}</ul></section>}
          <section className="min-w-0 space-y-4"><h3 className="text-lg font-semibold">{isEn ? 'Real-world worked examples' : 'Ejemplos reales resueltos'}</h3>{data.WorkedExamples.map((example, i) => <WorkedExample key={i} example={example} index={i} isEn={isEn} />)}</section>
          {data.PracticalActions.length > 0 && <section className="rounded-2xl border border-slate-200 p-5 dark:border-white/10"><h3 className="mb-3 font-semibold">{isEn ? 'Put it into practice' : 'Ll\u00e9valo a la pr\u00e1ctica'}</h3><ul className="list-disc space-y-2 pl-5">{data.PracticalActions.map((action, i) => <li key={i}><ResponseText text={action} /></li>)}</ul></section>}
          {!!data.KnowledgeConnections?.length && <div className="flex flex-wrap gap-2">{data.KnowledgeConnections.map((tag, i) => <span key={i} className="rounded-full bg-teal-50 px-3 py-1 text-xs text-teal-800 dark:bg-teal-500/10 dark:text-teal-200">{tag}</span>)}</div>}
        </div>
      )}

      {isChatOpen && <div className="fixed inset-0 z-50 flex justify-end bg-slate-950/70 backdrop-blur-sm" role="dialog" aria-modal="true" aria-label={isEn ? 'Ask the Professor' : 'Preguntar al Profesor'}>
        <div className="flex h-full w-full max-w-2xl flex-col bg-white shadow-2xl dark:bg-slate-900">
          <header className="flex items-center justify-between border-b border-slate-200 p-4 dark:border-white/10">
            <h3 className="font-bold">{isEn ? 'Q&A with Professor' : 'Q&A con el Profesor'}</h3>
            <div className="flex gap-2">
              <button type="button" disabled={chatLoading} onClick={() => { setChatMessages([]); setChatSessionId(undefined); setChatError(null); }} aria-label={isEn ? 'Clear chat' : 'Limpiar chat'} className="rounded-lg p-2 hover:bg-slate-100 dark:hover:bg-white/10"><Trash2 className="h-4 w-4" /></button>
              <button type="button" onClick={() => setIsChatOpen(false)} aria-label={isEn ? 'Close' : 'Cerrar'} className="rounded-lg p-2 hover:bg-slate-100 dark:hover:bg-white/10"><X className="h-5 w-5" /></button>
            </div>
          </header>
          <div className="flex-1 space-y-4 overflow-y-auto p-5" aria-live="polite">
            {chatMessages.length === 0 && <p className="text-sm text-slate-500">{isEn ? 'Ask about the example, formulas, code or how to adapt the solution.' : 'Pregunta sobre el ejemplo, las f\u00f3rmulas, el c\u00f3digo o c\u00f3mo adaptar la soluci\u00f3n.'}</p>}
            {chatMessages.map((message, i) => <div key={i} className={`rounded-xl p-4 ${message.role === 'user' ? 'ml-8 bg-teal-50 dark:bg-teal-500/10' : 'mr-4 bg-slate-50 dark:bg-white/5'}`}><p className="mb-1 text-xs font-semibold">{message.role === 'user' ? (isEn ? 'You' : 'T\u00fa') : (isEn ? 'Professor' : 'Profesor')}</p><ResponseText text={message.text} /></div>)}
            {chatLoading && <Loader2 className="h-5 w-5 animate-spin" />}
            {chatError && <p role="alert" className="text-sm text-red-600 dark:text-red-300">{chatError}</p>}
          </div>
          <form onSubmit={sendChat} className="flex items-end gap-2 border-t border-slate-200 p-4 dark:border-white/10">
            <textarea value={chatInput} onChange={(event) => setChatInput(event.target.value)} rows={2} aria-label={isEn ? 'Your question' : 'Tu pregunta'} placeholder={isEn ? 'Ask a follow-up question...' : 'Escribe tu pregunta...'} className="flex-1 resize-none rounded-xl border border-slate-300 bg-transparent p-3 text-sm dark:border-white/20" />
            <button type="submit" disabled={chatLoading || !chatInput.trim()} aria-label={isEn ? 'Send' : 'Enviar'} className="rounded-xl bg-teal-600 p-3 text-white disabled:opacity-50"><Send className="h-5 w-5" /></button>
          </form>
        </div>
      </div>}
    </div>
  );
}
