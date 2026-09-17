import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import {
  Mic,
  PhoneOff,
  Loader2,
  Award,
  ThumbsUp,
  TrendingUp,
  Sparkles,
  Check,
  ArrowLeft,
  MessagesSquare,
  ClipboardCheck,
  ListChecks,
  ChevronDown,
  Pause,
  Play,
  RotateCcw,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { getJobRole } from '@/features/profile/api/jobRoleApi';
import { getMyResume } from '@/features/profile/api/resumeApi';
import {
  getRunnableScenario,
  startAttempt,
  getAttempt,
  getAttemptVoiceSession,
  createConversationTurn,
  completeAttempt,
  pauseAttempt,
  resumeAttempt,
  evaluateAttempt,
  getAttemptEvaluation,
  listConversationTurns,
  type AttemptItem,
  type AttemptEvaluationResponse,
  type ConversationTurnItem,
} from '../api/learningLabsApi';

type Stage = 'conversation' | 'evaluation' | 'results';
type VoiceState = 'connecting' | 'connected' | 'listening' | 'speaking' | 'error' | 'paused';

const STAGES: { key: Stage; icon: typeof MessagesSquare }[] = [
  { key: 'conversation', icon: MessagesSquare },
  { key: 'evaluation', icon: ClipboardCheck },
  { key: 'results', icon: Award },
];

// Strips punctuation/case/whitespace differences so two STT/TTS passes of the same utterance
// (e.g. differing only by a comma) are still recognized as the same turn for dedupe purposes.
function normalizeForDedupe(text: string): string {
  return text.toLowerCase().replace(/[^\p{L}\p{N}]+/gu, ' ').trim();
}

/**
 * Attempt run page (2026-09-15) — every real Lab attempt gets its own bookmarkable page
 * (`/learning-labs/:idLab/attempts/:idAttempt`), NOT a modal, split into three explicit stages:
 * (1) the live realtime conversation, (2) an explicit "Run Evaluation" step that sends the full
 * transcript + the Lab's own objectives/rubric + the employee's real required job-role skills to
 * the AttemptEvaluationAgent, and (3) the resulting score/feedback/recommendations. If the URL has
 * no attempt id yet, one is created and the URL is replaced so refreshing never loses the attempt.
 */
export function AttemptPage() {
  const { idLab, idAttempt: idAttemptParam } = useParams<{ idLab?: string; idAttempt?: string }>();
  const navigate = useNavigate();
  const { language } = useI18n();
  const isEn = language === 'en';
  const { user } = useAuth();
  const orgTenantId = user?.tenantId ?? '';

  const [stage, setStage] = useState<Stage>('conversation');
  const [voiceState, setVoiceState] = useState<VoiceState>('connecting');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actorLabel, setActorLabel] = useState<string | null>(null);
  const [ending, setEnding] = useState(false);
  const [pausing, setPausing] = useState(false);
  const [restarting, setRestarting] = useState(false);
  const [evaluating, setEvaluating] = useState(false);
  const [evaluation, setEvaluation] = useState<AttemptEvaluationResponse | null>(null);
  const [bootError, setBootError] = useState<string | null>(null);
  const [transcript, setTranscript] = useState<ConversationTurnItem[]>([]);
  const [transcriptError, setTranscriptError] = useState<string | null>(null);
  const [showTranscript, setShowTranscript] = useState(true);

  const attemptRef = useRef<AttemptItem | null>(null);
  const startIdempotencyKeyRef = useRef<string | null>(null);
  const actorIdRef = useRef<string | null>(null);
  const lastParticipantTextRef = useRef<string | null>(null);
  const lastActorTextRef = useRef<string | null>(null);
  const gotResponseRef = useRef(false);
  const peerConnectionRef = useRef<RTCPeerConnection | null>(null);
  const localStreamRef = useRef<MediaStream | null>(null);
  const audioElementRef = useRef<HTMLAudioElement | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const animationFrameRef = useRef<number | null>(null);

  const teardown = useCallback(() => {
    if (animationFrameRef.current !== null) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    peerConnectionRef.current?.close();
    peerConnectionRef.current = null;
    localStreamRef.current?.getTracks().forEach((track) => track.stop());
    localStreamRef.current = null;
    audioContextRef.current?.close().catch(() => undefined);
    audioContextRef.current = null;
  }, []);

  const monitorRemoteAudioLevel = useCallback((stream: MediaStream) => {
    const audioContext = new AudioContext();
    audioContextRef.current = audioContext;
    const source = audioContext.createMediaStreamSource(stream);
    const analyser = audioContext.createAnalyser();
    analyser.fftSize = 512;
    source.connect(analyser);

    const buffer = new Uint8Array(analyser.frequencyBinCount);
    const tick = () => {
      analyser.getByteFrequencyData(buffer);
      let sum = 0;
      for (let i = 0; i < buffer.length; i += 1) sum += buffer[i];
      const average = sum / buffer.length;
      const isAgentSpeaking = average > 12;
      // The mic stays live the ENTIRE time (matches VoiceTutorAgent.tsx/EngramReviewAgent.tsx/
      // LabSimulationVoiceModal.tsx) so the server's own VAD (turn_detection: server_vad,
      // interrupt_response: true) can detect the student talking over the actor and auto-cancel
      // its response — real barge-in. Previously this disabled the mic track entirely while the
      // actor spoke ("half-duplex"), which fed the server zero audio during that window and made
      // interrupting impossible. `input_audio_buffer.speech_started` (below) still pauses local
      // playback immediately for a snappy felt interruption.
      setVoiceState((current) => {
        if (current === 'connecting' || current === 'error') return current;
        return isAgentSpeaking ? 'speaking' : 'listening';
      });
      animationFrameRef.current = requestAnimationFrame(tick);
    };
    tick();
  }, []);

  const connectVoice = useCallback(
    async (attempt: AttemptItem) => {
      try {
        // Competitive-advantage grounding: the live agent knows who it's actually talking to —
        // their real job role and résumé — so it plays its character with the participant's real
        // background in mind (e.g. an interviewer references their résumé, a client knows their role).
        const [jobRole, resume] = await Promise.all([
          user?.jobRoleId ? getJobRole(user.jobRoleId).catch(() => null) : Promise.resolve(null),
          user?.personId ? getMyResume(user.personId).catch(() => null) : Promise.resolve(null),
        ]);
        const resumeParts: string[] = [];
        if (resume?.professionalSummary) resumeParts.push(resume.professionalSummary);
        if (resume?.workExperience?.length) resumeParts.push(`Experience: ${resume.workExperience.join('; ')}`);
        if (resume?.education?.length) resumeParts.push(`Education: ${resume.education.join('; ')}`);
        if (resume?.skills?.length) resumeParts.push(`Skills: ${resume.skills.join(', ')}`);
        const session = await getAttemptVoiceSession(attempt.IdAttempt, orgTenantId, {
          EmployeeName: user?.name,
          CompanyName: user?.tenantName ?? undefined,
          JobRoleTitle: jobRole?.title,
          JobRoleSummary: jobRole?.summary ?? undefined,
          RequiredTechnicalSkills: jobRole?.technicalSkills ?? [],
          RequiredSoftSkills: jobRole?.softSkills ?? [],
          ResumeSummary: resumeParts.length > 0 ? resumeParts.join(' | ') : undefined,
        });
        actorIdRef.current = session.ActorId;
        if (session.ActorNombre) setActorLabel(`${session.ActorNombre}${session.ActorRol ? ` — ${session.ActorRol}` : ''}`);

        const micStream = await navigator.mediaDevices.getUserMedia({
          audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
        });
        localStreamRef.current = micStream;

        const peerConnection = new RTCPeerConnection();
        peerConnectionRef.current = peerConnection;
        micStream.getTracks().forEach((track) => peerConnection.addTrack(track, micStream));

        peerConnection.ontrack = (event) => {
          const [remoteStream] = event.streams;
          if (audioElementRef.current) audioElementRef.current.srcObject = remoteStream;
          monitorRemoteAudioLevel(remoteStream);
        };

        const dataChannel = peerConnection.createDataChannel('oai-events');
        dataChannel.onopen = () => {
          dataChannel.send(JSON.stringify({ type: 'response.create' }));
          // Safety net: the actor is instructed to speak first, but if nothing starts within a
          // while (e.g. the very first response.create raced the session finishing setup), nudge it
          // again once rather than leaving the student staring at silence. Window is generous (8s)
          // because a too-short window can race the real response.created event and cause the model
          // to generate two independent greetings back to back.
          window.setTimeout(() => {
            if (!gotResponseRef.current && dataChannel.readyState === 'open') {
              dataChannel.send(JSON.stringify({ type: 'response.create' }));
            }
          }, 8000);
        };
        dataChannel.onmessage = (event) => {
          let payload: Record<string, unknown>;
          try {
            payload = JSON.parse(event.data as string);
          } catch {
            return;
          }

          const attemptId = attemptRef.current?.IdAttempt;
          if (import.meta.env.DEV) console.debug('[realtime]', payload.type);
          if (payload.type === 'response.created') {
            gotResponseRef.current = true;
          }
          if (payload.type === 'input_audio_buffer.speech_started') {
            audioElementRef.current?.pause();
          } else if (
            payload.type === 'response.audio_transcript.delta' ||
            payload.type === 'response.output_audio_transcript.delta' ||
            payload.type === 'response.created'
          ) {
            if (audioElementRef.current?.paused) audioElementRef.current.play().catch(() => undefined);
          } else if (payload.type === 'conversation.item.input_audio_transcription.completed' && attemptId) {
            const transcript = (payload.transcript as string | undefined)?.trim();
            if (transcript && normalizeForDedupe(transcript) !== lastParticipantTextRef.current) {
              lastParticipantTextRef.current = normalizeForDedupe(transcript);
              createConversationTurn(attemptId, orgTenantId, 'PARTICIPANT', transcript).catch(() => undefined);
            }
          } else if (
            (payload.type === 'response.audio_transcript.done' || payload.type === 'response.output_audio_transcript.done') &&
            attemptId
          ) {
            const transcript = (payload.transcript as string | undefined)?.trim();
            if (transcript && normalizeForDedupe(transcript) !== lastActorTextRef.current) {
              lastActorTextRef.current = normalizeForDedupe(transcript);
              createConversationTurn(attemptId, orgTenantId, 'SIMULATED_ACTOR', transcript, actorIdRef.current ?? undefined).catch(
                () => undefined,
              );
            }
          }
        };

        const offer = await peerConnection.createOffer();
        await peerConnection.setLocalDescription(offer);

        const sdpResponse = await fetch(session.RealtimeCallsUrl, {
          method: 'POST',
          headers: { Authorization: `Bearer ${session.ClientSecret}`, 'Content-Type': 'application/sdp' },
          body: offer.sdp,
        });
        if (!sdpResponse.ok) throw new Error(`No se pudo negociar la sesión de voz (${sdpResponse.status}).`);
        const answerSdp = await sdpResponse.text();
        await peerConnection.setRemoteDescription({ type: 'answer', sdp: answerSdp });

        setVoiceState('connected');
      } catch (err) {
        setErrorMessage(err instanceof Error ? err.message : String(err));
        setVoiceState('error');
      }
    },
    [orgTenantId, monitorRemoteAudioLevel, user],
  );

  useEffect(() => {
    if (!orgTenantId) return;
    if (!idAttemptParam && !idLab) return;
    let cancelled = false;

    async function boot() {
      try {
        let attempt: AttemptItem;
        if (idAttemptParam) {
          attempt = await getAttempt(idAttemptParam, orgTenantId);
        } else {
          const scenario = await getRunnableScenario(idLab!, orgTenantId);
          if (!scenario) throw new Error(isEn ? 'This Lab has no runnable scenario yet.' : 'Este Lab todavía no tiene un escenario para correr.');
          // Stable per-mount key so React StrictMode's dev-only double-invoke of this effect (or a
          // fast double-navigation) replays the SAME cached attempt server-side instead of racing a
          // second insert into a unique (tenant, scenario, participant, attempt#) index.
          if (!startIdempotencyKeyRef.current) startIdempotencyKeyRef.current = crypto.randomUUID();
          attempt = await startAttempt(scenario.idScenario, orgTenantId, isEn ? 'en-US' : 'es-MX', startIdempotencyKeyRef.current);
          if (cancelled) return;
          navigate(`/learning-labs/${idLab}/attempts/${attempt.IdAttempt}`, { replace: true });
        }
        if (cancelled) return;
        attemptRef.current = attempt;

        if (attempt.Estatus === 'COMPLETED') {
          const existingEvaluation = await getAttemptEvaluation(attempt.IdAttempt, orgTenantId).catch(() => null);
          if (cancelled) return;
          listConversationTurns(attempt.IdAttempt, orgTenantId)
            .then((turns) => { if (!cancelled) setTranscript(turns); })
            .catch((err) => { if (!cancelled) setTranscriptError(err instanceof Error ? err.message : String(err)); });
          if (existingEvaluation) {
            setEvaluation(existingEvaluation);
            setStage('results');
          } else {
            setStage('evaluation');
          }
          return;
        }

        await connectVoice(attempt);
      } catch (err) {
        if (!cancelled) setBootError(err instanceof Error ? err.message : String(err));
      }
    }

    boot();
    return () => {
      cancelled = true;
      teardown();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [idLab, idAttemptParam, orgTenantId]);

  const handleEndConversation = useCallback(async () => {
    if (ending) return;
    setEnding(true);
    teardown();
    const attempt = attemptRef.current;
    if (attempt) {
      try {
        const completed = await completeAttempt(attempt.IdAttempt, orgTenantId, attempt.RowVersion);
        attemptRef.current = completed;
      } catch {
        // Best-effort — the attempt stays IN_PROGRESS server-side if this fails, not fatal here.
      }
      listConversationTurns(attempt.IdAttempt, orgTenantId)
        .then(setTranscript)
        .catch((err) => setTranscriptError(err instanceof Error ? err.message : String(err)));
    }
    setStage('evaluation');
  }, [ending, teardown, orgTenantId]);

  // Freezes the live conversation (mic/audio torn down, Attempt -> PAUSED server-side) without
  // completing/evaluating it, so the student can stop mid-way without being forced into "End"
  // (which immediately locks in whatever happened so far for grading).
  const handlePause = useCallback(async () => {
    const attempt = attemptRef.current;
    if (!attempt || pausing) return;
    setPausing(true);
    teardown();
    try {
      const paused = await pauseAttempt(attempt.IdAttempt, orgTenantId, attempt.RowVersion);
      attemptRef.current = paused;
      setVoiceState('paused');
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : String(err));
    } finally {
      setPausing(false);
    }
  }, [pausing, teardown, orgTenantId]);

  // PAUSED -> IN_PROGRESS, then mints a fresh voice session and reconnects.
  const handleResume = useCallback(async () => {
    const attempt = attemptRef.current;
    if (!attempt || pausing) return;
    setPausing(true);
    setErrorMessage(null);
    setVoiceState('connecting');
    try {
      const resumed = await resumeAttempt(attempt.IdAttempt, orgTenantId, attempt.RowVersion);
      attemptRef.current = resumed;
      await connectVoice(resumed);
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : String(err));
      setVoiceState('error');
    } finally {
      setPausing(false);
    }
  }, [pausing, orgTenantId, connectVoice]);

  // Drops the current realtime session and mints a brand new one on the SAME attempt (the actor
  // greets again from scratch) — for when the conversation went badly and the student wants a
  // clean restart instead of ending early and getting graded on a bad take. Previously logged
  // turns stay in the transcript; this only resets the LIVE call, never the Attempt's status.
  const handleRestart = useCallback(async () => {
    const attempt = attemptRef.current;
    if (!attempt || restarting) return;
    setRestarting(true);
    setErrorMessage(null);
    teardown();
    gotResponseRef.current = false;
    lastParticipantTextRef.current = null;
    lastActorTextRef.current = null;
    setVoiceState('connecting');
    try {
      await connectVoice(attempt);
    } finally {
      setRestarting(false);
    }
  }, [restarting, teardown, connectVoice]);

  const handleRunEvaluation = useCallback(async () => {
    const attempt = attemptRef.current;
    if (!attempt || evaluating) return;
    setEvaluating(true);
    setErrorMessage(null);
    try {
      const jobRole = user?.jobRoleId ? await getJobRole(user.jobRoleId).catch(() => null) : null;
      const result = await evaluateAttempt(attempt.IdAttempt, orgTenantId, {
        EmployeeName: user?.name ?? undefined,
        JobRoleTitle: jobRole?.title,
        RequiredTechnicalSkills: jobRole?.technicalSkills ?? [],
        RequiredSoftSkills: jobRole?.softSkills ?? [],
        RequiredDepartmentProcesses: jobRole?.departmentProcesses ?? [],
        RequiredHrProcesses: jobRole?.hrProcesses ?? [],
      });
      setEvaluation(result);
      setStage('results');
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : String(err));
    } finally {
      setEvaluating(false);
    }
  }, [evaluating, orgTenantId, user]);

  const stageIndex = useMemo(() => STAGES.findIndex((s) => s.key === stage), [stage]);

  const stageTitle: Record<Stage, string> = {
    conversation: isEn ? 'Conversation' : 'Conversación',
    evaluation: isEn ? 'Evaluation' : 'Evaluación',
    results: isEn ? 'Results' : 'Resultados',
  };

  const voiceStateLabel: Record<VoiceState, string> = {
    connecting: isEn ? 'Connecting…' : 'Conectando…',
    connected: isEn ? 'Connected — you can talk' : 'Conectado — puedes hablar',
    listening: isEn ? 'Listening…' : 'Escuchando…',
    speaking: isEn ? 'Speaking…' : 'Hablando…',
    error: isEn ? 'Could not connect' : 'No se pudo conectar',
    paused: isEn ? 'Paused — resume when ready' : 'Pausado — reanuda cuando quieras',
  };

  if (bootError) {
    return (
      <div className="mx-auto max-w-2xl p-8 text-center">
        <p className="text-sm text-red-500">{bootError}</p>
        <button
          type="button"
          onClick={() => navigate('/learning-labs')}
          className="mt-4 inline-flex items-center gap-1.5 rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white dark:bg-white dark:text-slate-900"
        >
          <ArrowLeft className="h-4 w-4" /> {isEn ? 'Back to Learning Labs' : 'Volver a Labs de Aprendizaje'}
        </button>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl space-y-8 p-4 sm:p-8">
      <button
        type="button"
        onClick={() => navigate('/learning-labs')}
        className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {isEn ? 'Learning Labs' : 'Labs de Aprendizaje'}
      </button>

      {/* Stepper */}
      <div className="flex items-center justify-center gap-2">
        {STAGES.map(({ key, icon: Icon }, i) => {
          const isDone = i < stageIndex;
          const isActive = i === stageIndex;
          return (
            <div key={key} className="flex items-center gap-2">
              <div
                className={`flex items-center gap-2 rounded-full px-3.5 py-1.5 text-xs font-semibold uppercase tracking-wide transition-colors ${
                  isActive
                    ? 'bg-teal-600 text-white'
                    : isDone
                    ? 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-300'
                    : 'bg-slate-100 text-slate-400 dark:bg-white/[0.04] dark:text-slate-500'
                }`}
              >
                {isDone ? <Check className="h-3.5 w-3.5" /> : <Icon className="h-3.5 w-3.5" />}
                {stageTitle[key]}
              </div>
              {i < STAGES.length - 1 && <div className={`h-px w-8 ${isDone ? 'bg-emerald-400' : 'bg-slate-200 dark:bg-white/10'}`} />}
            </div>
          );
        })}
      </div>

      {/* Stage 1 — Conversation */}
      {stage === 'conversation' && (
        <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm dark:border-white/10 dark:bg-white/[0.03]">
          <div
            className={`mx-auto flex h-24 w-24 items-center justify-center rounded-full border-2 transition-colors ${
              voiceState === 'speaking'
                ? 'border-teal-400 bg-teal-400/20 shadow-[0_0_30px_rgba(45,212,191,0.35)]'
                : voiceState === 'listening'
                ? 'border-amber-400 bg-amber-400/10'
                : voiceState === 'error'
                ? 'border-red-500 bg-red-500/10'
                : 'border-slate-300 bg-slate-100 dark:border-slate-500 dark:bg-slate-500/10'
            }`}
          >
            <Mic className={`h-10 w-10 ${voiceState === 'error' ? 'text-red-400' : 'text-teal-500 dark:text-teal-200'}`} />
          </div>

          <h2 className="mt-5 text-lg font-semibold text-slate-900 dark:text-white">{isEn ? 'Live Interview' : 'Entrevista en vivo'}</h2>
          {actorLabel && <p className="mt-1 text-sm text-teal-600 dark:text-teal-300">{actorLabel}</p>}
          <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">{errorMessage ?? voiceStateLabel[voiceState]}</p>

          <div className="mt-6 flex flex-wrap items-center justify-center gap-3">
            {voiceState === 'paused' ? (
              <button
                type="button"
                onClick={handleResume}
                disabled={pausing}
                className="inline-flex items-center gap-2 rounded-lg bg-teal-600 px-4 py-2 text-sm font-medium text-white hover:bg-teal-500 disabled:opacity-60"
              >
                {pausing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Play className="h-4 w-4" />}
                {isEn ? 'Resume' : 'Reanudar'}
              </button>
            ) : (
              <button
                type="button"
                onClick={handlePause}
                disabled={pausing || voiceState === 'connecting' || voiceState === 'error'}
                className="inline-flex items-center gap-2 rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60 dark:border-white/15 dark:text-slate-200 dark:hover:bg-white/5"
              >
                {pausing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Pause className="h-4 w-4" />}
                {isEn ? 'Pause' : 'Pausar'}
              </button>
            )}

            <button
              type="button"
              onClick={handleRestart}
              disabled={restarting || voiceState === 'connecting'}
              className="inline-flex items-center gap-2 rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60 dark:border-white/15 dark:text-slate-200 dark:hover:bg-white/5"
            >
              {restarting ? <Loader2 className="h-4 w-4 animate-spin" /> : <RotateCcw className="h-4 w-4" />}
              {isEn ? 'Restart conversation' : 'Reiniciar conversación'}
            </button>

            <button
              type="button"
              onClick={handleEndConversation}
              disabled={ending}
              className="inline-flex items-center gap-2 rounded-lg bg-red-500/90 px-4 py-2 text-sm font-medium text-white hover:bg-red-500 disabled:opacity-60"
            >
              {ending ? <Loader2 className="h-4 w-4 animate-spin" /> : <PhoneOff className="h-4 w-4" />}
              {isEn ? 'End interview' : 'Terminar entrevista'}
            </button>
          </div>

          <audio ref={audioElementRef} autoPlay className="hidden" />
        </div>
      )}

      {/* Stage 2 — Evaluation */}
      {stage === 'evaluation' && (
        <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm dark:border-white/10 dark:bg-white/[0.03]">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-teal-500/15 text-teal-600 dark:text-teal-300">
            <ClipboardCheck className="h-8 w-8" />
          </div>
          <h2 className="mt-4 text-lg font-semibold text-slate-900 dark:text-white">
            {isEn ? 'Conversation complete' : 'Conversación completada'}
          </h2>
          <p className="mx-auto mt-2 max-w-md text-sm text-slate-500 dark:text-slate-400">
            {isEn
              ? "Ready to send your full conversation, the Lab's objectives and rubric, and your required job skills to the AI evaluator."
              : 'Listo para enviar tu conversación completa, los objetivos y la rúbrica del Lab, y tus habilidades requeridas al evaluador de IA.'}
          </p>
          {errorMessage && <p className="mt-3 text-sm text-red-500">{errorMessage}</p>}
          <button
            type="button"
            onClick={handleRunEvaluation}
            disabled={evaluating}
            className="mt-6 inline-flex items-center gap-2 rounded-lg bg-teal-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-teal-500 disabled:opacity-60"
          >
            {evaluating ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
            {evaluating ? (isEn ? 'Evaluating…' : 'Evaluando…') : isEn ? 'Run Evaluation' : 'Ejecutar Evaluación'}
          </button>
        </div>
      )}

      {(stage === 'evaluation' || stage === 'results') && (
        <TranscriptPanel
          isEn={isEn}
          transcript={transcript}
          error={transcriptError}
          show={showTranscript}
          onToggle={() => setShowTranscript((v) => !v)}
        />
      )}

      {/* Stage 3 — Results */}
      {stage === 'results' && evaluation && (
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm dark:border-white/10 dark:bg-white/[0.03]">
          <div className="flex items-center gap-3">
            <div className="flex h-12 w-12 flex-none items-center justify-center rounded-full bg-teal-500/15 text-teal-600 dark:text-teal-300">
              <Award className="h-6 w-6" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
                {isEn ? 'Evaluation results' : 'Resultados de la evaluación'}
              </h2>
              <p className="text-sm text-slate-500 dark:text-slate-400">
                {isEn ? 'Score' : 'Puntaje'}: <span className="font-semibold text-teal-600 dark:text-teal-300">{evaluation.ScoreFinal}/10</span>{' '}
                · {evaluation.Resultado}
              </p>
            </div>
          </div>

          <p className="mt-4 text-sm leading-relaxed text-slate-700 dark:text-slate-300">{evaluation.Feedback}</p>

          {evaluation.Strengths.length > 0 && (
            <div className="mt-5">
              <h3 className="flex items-center gap-1.5 text-sm font-semibold text-emerald-600 dark:text-emerald-300">
                <ThumbsUp className="h-4 w-4" /> {isEn ? 'Strengths' : 'Fortalezas'}
              </h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-700 dark:text-slate-300">
                {evaluation.Strengths.map((s, i) => (
                  <li key={i} className="list-disc pl-4 marker:text-emerald-500">
                    {s}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {evaluation.Gaps.length > 0 && (
            <div className="mt-5">
              <h3 className="flex items-center gap-1.5 text-sm font-semibold text-amber-600 dark:text-amber-300">
                <TrendingUp className="h-4 w-4" /> {isEn ? 'Weaknesses' : 'Debilidades'}
              </h3>
              <ul className="mt-2 space-y-1 text-sm text-slate-700 dark:text-slate-300">
                {evaluation.Gaps.map((g, i) => (
                  <li key={i} className="list-disc pl-4 marker:text-amber-500">
                    {g}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {evaluation.CriteriaScores.length > 0 && (
            <div className="mt-5">
              <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
                {isEn ? 'Score by skill area (only areas actually tested)' : 'Puntaje por área (solo lo realmente evaluado)'}
              </h3>
              <ul className="mt-2 space-y-2">
                {evaluation.CriteriaScores.map((c, i) => (
                  <li key={i} className="rounded-lg border border-slate-200 p-3 dark:border-white/10">
                    <div className="flex items-center justify-between">
                      <span className="text-sm font-medium text-slate-900 dark:text-white">{c.CriterionName}</span>
                      <span className="text-sm font-semibold text-teal-600 dark:text-teal-300">{c.Score}/10</span>
                    </div>
                    <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{c.Evidence}</p>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {evaluation.TurnEvaluations.length > 0 && (
            <div className="mt-5">
              <h3 className="flex items-center gap-1.5 text-sm font-semibold text-indigo-600 dark:text-indigo-300">
                <ListChecks className="h-4 w-4" /> {isEn ? 'Question-by-question review' : 'Revisión pregunta por pregunta'}
              </h3>
              <ul className="mt-2 space-y-3">
                {evaluation.TurnEvaluations.map((t, i) => (
                  <li key={i} className="rounded-lg border border-slate-200 bg-slate-50 p-4 dark:border-white/10 dark:bg-white/[0.03]">
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex items-start gap-2">
                        <span className="mt-0.5 flex-none rounded-full bg-indigo-500/15 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-indigo-600 dark:text-indigo-300">
                          {t.SkillArea}
                        </span>
                        <p className="text-sm font-medium text-slate-900 dark:text-white">{t.Question}</p>
                      </div>
                      <span
                        className={`flex-none rounded-full px-2 py-0.5 text-xs font-semibold ${
                          t.Score >= 7
                            ? 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-300'
                            : t.Score >= 4
                            ? 'bg-amber-500/15 text-amber-600 dark:text-amber-300'
                            : 'bg-red-500/15 text-red-600 dark:text-red-300'
                        }`}
                      >
                        {t.Score}/10
                      </span>
                    </div>
                    <p className="mt-2 text-sm italic text-slate-600 dark:text-slate-300">"{t.Answer}"</p>
                    <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">{t.Assessment}</p>
                    <div className="mt-3 rounded-md border border-teal-200 bg-teal-50 p-2.5 dark:border-teal-500/20 dark:bg-teal-500/10">
                      <p className="text-[11px] font-semibold uppercase tracking-wide text-teal-700 dark:text-teal-300">
                        {isEn ? 'Recommended answer' : 'Respuesta recomendada'}
                      </p>
                      <p className="mt-1 text-xs text-teal-900 dark:text-teal-100">{t.RecommendedAnswer}</p>
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {evaluation.RecommendedSkills.length > 0 && (
            <div className="mt-5">
              <h3 className="flex items-center gap-1.5 text-sm font-semibold text-teal-600 dark:text-teal-300">
                <Sparkles className="h-4 w-4" /> {isEn ? 'Recommended training' : 'Capacitación recomendada'}
              </h3>
              <ul className="mt-2 space-y-2">
                {evaluation.RecommendedSkills.map((rec, i) => (
                  <li key={i} className="rounded-lg border border-slate-200 bg-slate-50 p-3 dark:border-white/10 dark:bg-white/[0.03]">
                    <div className="flex items-center justify-between">
                      <span className="text-sm font-medium text-slate-900 dark:text-white">{rec.SkillName}</span>
                      <span className="rounded-full bg-teal-500/15 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-teal-600 dark:text-teal-300">
                        {rec.SkillCategory}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{rec.Reason}</p>
                  </li>
                ))}
              </ul>
            </div>
          )}

          <div className="mt-6 flex gap-2">
            <button
              type="button"
              onClick={handleRunEvaluation}
              disabled={evaluating}
              className="inline-flex flex-1 items-center justify-center gap-2 rounded-lg border border-teal-600 px-4 py-2 text-sm font-medium text-teal-600 hover:bg-teal-50 disabled:opacity-60 dark:text-teal-300 dark:hover:bg-teal-500/10"
            >
              {evaluating ? <Loader2 className="h-4 w-4 animate-spin" /> : <Sparkles className="h-4 w-4" />}
              {evaluating ? (isEn ? 'Re-evaluating…' : 'Reevaluando…') : isEn ? 'Re-run evaluation' : 'Volver a evaluar'}
            </button>
            <button
              type="button"
              onClick={() => navigate('/learning-labs')}
              className="flex-1 rounded-lg bg-teal-600 px-4 py-2 text-sm font-medium text-white hover:bg-teal-500"
            >
              {isEn ? 'Back to Learning Labs' : 'Volver a Labs de Aprendizaje'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

/** Full ordered transcript, collapsed by default — lets the employee verify the conversation
 * was captured correctly (speaker + exact text) alongside the AI evaluator's feedback. */
function TranscriptPanel({
  isEn,
  transcript,
  error,
  show,
  onToggle,
}: {
  isEn: boolean;
  transcript: ConversationTurnItem[];
  error: string | null;
  show: boolean;
  onToggle: () => void;
}) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white shadow-sm dark:border-white/10 dark:bg-white/[0.03]">
      <button
        type="button"
        onClick={onToggle}
        className="flex w-full items-center justify-between p-4 text-left"
      >
        <span className="flex items-center gap-2 text-sm font-semibold text-slate-800 dark:text-slate-200">
          <MessagesSquare className="h-4 w-4 text-teal-600 dark:text-teal-300" />
          {isEn ? 'Full conversation transcript' : 'Transcripción completa de la conversación'}
          <span className="text-xs font-normal text-slate-400">({transcript.length})</span>
        </span>
        <ChevronDown className={`h-4 w-4 text-slate-400 transition-transform ${show ? 'rotate-180' : ''}`} />
      </button>
      {show && (
        <div className="max-h-96 space-y-2 overflow-y-auto border-t border-slate-200 p-4 dark:border-white/10">
          {error && <p className="text-sm text-red-500">{error}</p>}
          {!error && transcript.length === 0 && (
            <p className="text-sm text-slate-400">{isEn ? 'No turns recorded yet.' : 'Aún no hay turnos registrados.'}</p>
          )}
          {transcript.map((t) => (
            <div
              key={t.IdTurn}
              className={`flex gap-2 rounded-lg p-2.5 text-sm ${
                t.SpeakerType === 'PARTICIPANT'
                  ? 'bg-teal-50 dark:bg-teal-500/10'
                  : 'bg-slate-50 dark:bg-white/[0.04]'
              }`}
            >
              <span className="w-8 flex-none text-xs text-slate-400">#{t.NumeroTurno}</span>
              <div>
                <span className="text-[11px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">
                  {t.SpeakerType === 'PARTICIPANT' ? (isEn ? 'You' : 'Tú') : isEn ? 'Actor' : 'Actor'}
                </span>
                <p className="text-slate-700 dark:text-slate-300">{t.Texto}</p>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
