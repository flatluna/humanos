import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Mic, Bot, Sparkles } from 'lucide-react';
import { requestVoiceTutorSession, requestEngramReviewSession, type ExperienceStepType } from '../lib/api/runtimeApi';

type VoiceState = 'idle' | 'connecting' | 'connected' | 'listening' | 'speaking' | 'error';

const getStateLabels = (t: (key: string) => string): Record<VoiceState, string> => ({
  idle: t('voiceTutorAgent.idle'),
  connecting: t('voiceTutorAgent.connecting'),
  connected: t('voiceTutorAgent.connected'),
  listening: t('voiceTutorAgent.listening'),
  speaking: t('voiceTutorAgent.speaking'),
  error: t('voiceTutorAgent.error'),
});

/**
 * "Agente de voz" — a real-time WebRTC voice presence for the Instructor
 * Runtime (Azure OpenAI GPT Realtime, see VoiceTutorSessionFunction.cs and
 * /memories/repo/voice-tutor-realtime-design.md), deliberately styled as a
 * standalone AI AGENT — a glowing orb avatar with its own state — instead
 * of a plain button, per explicit product direction (2026-07-21).
 *
 * Scope: Hypothesis/Teaching steps always render this component. Recall
 * renders it too, but ONLY in Studio's "Demo" preview mode (2026-07-22,
 * revised same day) — never in "Real" mode, i.e. never for an actual
 * student. Rationale: a live conversational voice agent lets the
 * student ask it questions or fish for hints/answers mid-recall, which
 * defeats unaided retrieval — the whole point of that stage. Grading/
 * recall verification always happens through the existing TutorService
 * text-submit path regardless of whether the voice agent is present.
 * "Demo" mode is a reviewer-only, session-independent-of-real-student-
 * data context (fixed seeded PREVIEW_PERSON_ID), so allowing the voice
 * agent there — to let reviewers simulate the full experience — carries
 * none of the anti-offloading risk it would for a real student. Never
 * rendered on Assessment. This component owns the ENTIRE WebRTC
 * lifecycle itself: it mints the ephemeral token, negotiates the SDP
 * offer/answer directly against Azure (browser ↔ Azure, never proxied
 * through our own backend), plays the remote audio, and derives a
 * lightweight "listening"/"speaking" visual state purely from the
 * remote audio stream's volume (via Web Audio's AnalyserNode) — this
 * works regardless of which realtime events `?webrtcfilter=on` lets
 * through, so it needs no server-event parsing at all for that part.
 */
export default function VoiceTutorAgent({
  learningSessionStepId,
  capabilityGraphNodeId,
  stepType,
  promptText,
  personName,
  onTranscript,
  onExplanationAccepted,
  exampleCardIndex,
  reviewOnly,
  reviewSession,
  startLabel,
  autoConnect = true,
  compact = false,
}: {
  /** Live-session path: the student is actually on (or already completed) this step. */
  learningSessionStepId?: string;
  /** Blueprint-only path (2026-07-22) — for Studio demo's read-only "peek"
   * at a step the live session hasn't reached yet (no LearningSessionStep
   * row exists there). Provide this together with `stepType` INSTEAD of
   * `learningSessionStepId`; 'Hypothesis'/'Teaching'/'DeepDive' are supported. */
  capabilityGraphNodeId?: string;
  stepType?: ExperienceStepType;
  promptText?: string;
  /** Optional (2026-07-25) — student's display name, used only for the
   * Hypothesis/Teaching opening greeting server-side. Omit if unknown. */
  personName?: string;
  onTranscript?: (transcript: string) => void;
  /** Deep Dive only (2026-07-30): fired when the voice Agent itself calls
   * the `mark_explanation_understood` Realtime tool — i.e. the exact
   * moment IT decides (per its own hard-capped instructions) the
   * student's spoken explanation is good enough. The caller should
   * forward this straight to the same deterministic grading endpoint used
   * for a typed answer (submitDeepDiveAttempt) — this callback only ever
   * signals WHEN to grade, never the grade itself. */
  onExplanationAccepted?: (explanation: string) => void;
  /** Only for the Deep Dive step (2026-07-29) — which ExampleCard's "habla
   * con tu maestro" button was pressed. */
  exampleCardIndex?: number;
  /** DeepDive blueprint-only path only (2026-07-30) — revisiting an
   * already-mastered card via the read-only card index. Switches the
   * Agent to a plain explain-and-invite-questions conversation with no
   * grading tool offered at all — never a quiz. Ignored for every other
   * path/step type. */
  reviewOnly?: boolean;
  /** "Reactivar Engrama" path (2026-07-30, ported from human-os-web's
   * EngramReviewAgent.tsx): a full-lesson, NEVER-graded spoken review of
   * an already-Mastered node — the Agent reads/explains the whole lesson
   * back (grounded in the blueprint + the student's own past answers) and
   * invites questions. Provide this INSTEAD of learningSessionStepId/
   * capabilityGraphNodeId+stepType; takes priority over both if given. */
  reviewSession?: { personId: string; capabilityGraphNodeId: string };
  /** Overrides the default idle-button label (e.g. "Talk to the agent")
   * with custom already-localized text — used by the review flow to say
   * something like "Listen to this lesson" instead. */
  startLabel?: string;
  /** Set to false for a per-ExampleCard button — the Agent must only
   * start talking when the student explicitly presses that card's button. */
  autoConnect?: boolean;
  /** Renders a small inline button instead of the full glowing-orb card. */
  compact?: boolean;
}) {
  const { t } = useTranslation();
  // Stable identity for "which step is this widget currently about" —
  // used to reset/re-auto-connect when the parent page swaps in a
  // different step (either a live step or a different peeked step)
  // without remounting this component instance.
  const targetKey = `${learningSessionStepId ?? `${capabilityGraphNodeId}:${stepType}`}:${exampleCardIndex ?? ''}`;

  const [state, setState] = useState<VoiceState>('idle');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const stateRef = useRef(state);
  useEffect(() => {
    stateRef.current = state;
  }, [state]);

  const peerConnectionRef = useRef<RTCPeerConnection | null>(null);
  const localStreamRef = useRef<MediaStream | null>(null);
  const audioElementRef = useRef<HTMLAudioElement | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const dataChannelRef = useRef<RTCDataChannel | null>(null);
  // Dedup guard for the Deep Dive `mark_explanation_understood` tool call —
  // Azure can legitimately report the SAME completed function call via both
  // `response.output_item.done` and `response.done` (see EngramReviewAgent's
  // identical note); only ever act on a given call_id once, and only ever
  // send its function_call_output once (double-answering re-triggers the
  // model into generating extra responses, per that earlier bug).
  const handledToolCallIdsRef = useRef<Set<string>>(new Set());

  const teardown = useCallback(() => {
    if (animationFrameRef.current !== null) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    dataChannelRef.current = null;
    peerConnectionRef.current?.close();
    peerConnectionRef.current = null;
    localStreamRef.current?.getTracks().forEach((track) => track.stop());
    localStreamRef.current = null;
    audioContextRef.current?.close().catch(() => undefined);
    audioContextRef.current = null;
    setState('idle');
  }, []);

  useEffect(() => teardown, [teardown]);

  // The parent page reuses this same component instance across steps
  // (Hipótesis → Enseñanza, etc.) without remounting it. If a voice
  // session is active/connecting for the PREVIOUS step when the student
  // advances, it must be torn down here — otherwise the student keeps
  // talking to an Agent still holding the old step's context.
  const previousTargetKeyRef = useRef(targetKey);
  useEffect(() => {
    if (previousTargetKeyRef.current !== targetKey) {
      previousTargetKeyRef.current = targetKey;
      teardown();
    }
  }, [targetKey, teardown]);

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

      // CHANGED 2026-07-25 (explicit user request: "quiero que cuando el
      // usuario hable el agente se calle, que permita yo interrumpa") —
      // the mic now stays live the ENTIRE time, including while the Agent
      // is speaking, so the server's own VAD (`turn_detection: server_vad`,
      // `interrupt_response: true`, configured in the data channel's
      // `session.update` below) can detect real speech and auto-cancel/
      // truncate the Agent's in-progress response — true voice barge-in.
      // `input_audio_buffer.speech_started` (handled in the data channel's
      // onmessage below) pauses local playback immediately for a snappy
      // felt interruption as soon as the server detects it.
      //
      // Trade-off accepted (previously avoided by a 2026-07-23 fix that
      // muted the mic while the Agent spoke): on speaker+mic setups
      // without headphones, acoustic echo of the Agent's own voice can
      // occasionally leak into the mic and false-trigger the VAD, causing
      // the Agent to self-interrupt mid-sentence. Headphones avoid this
      // entirely. This trade-off is intentional per explicit request —
      // real barge-in is now prioritized over that echo edge case.

      // If the agent's audio resumes (a new response), make sure local
      // playback isn't left paused from a previous manual/auto interrupt.
      if (isAgentSpeaking && audioElementRef.current?.paused) {
        audioElementRef.current.play().catch(() => undefined);
      }

      setState((current) => {
        if (current === 'idle' || current === 'connecting' || current === 'error') return current;
        return isAgentSpeaking ? 'speaking' : 'listening';
      });
      animationFrameRef.current = requestAnimationFrame(tick);
    };
    tick();
  }, []);

  const connect = useCallback(async () => {
    setErrorMessage(null);
    setState('connecting');
    handledToolCallIdsRef.current.clear();
    try {
      const session = reviewSession
        ? await requestEngramReviewSession(reviewSession.personId, reviewSession.capabilityGraphNodeId)
        : learningSessionStepId
        ? await requestVoiceTutorSession({ learningSessionStepId, promptText, personName, exampleCardIndex })
        : await requestVoiceTutorSession({
            capabilityGraphNodeId: capabilityGraphNodeId!,
            stepType: stepType!,
            personName,
            exampleCardIndex,
            isReviewOnly: reviewOnly,
          });

      // Explicit (not just `true`) so the browser can't skip echo
      // cancellation — without it, the mic picks up the Agent's own
      // voice from the speakers and the model ends up "talking to
      // itself". Headphones are still strongly recommended for best
      // results, especially on laptops with the mic close to the speakers.
      const micStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      });
      localStreamRef.current = micStream;

      const peerConnection = new RTCPeerConnection();
      peerConnectionRef.current = peerConnection;

      micStream.getTracks().forEach((track) => peerConnection.addTrack(track, micStream));

      peerConnection.ontrack = (event) => {
        const [remoteStream] = event.streams;
        if (audioElementRef.current) {
          audioElementRef.current.srcObject = remoteStream;
        }
        monitorRemoteAudioLevel(remoteStream);
      };

      const dataChannel = peerConnection.createDataChannel('oai-events');
      dataChannelRef.current = dataChannel;
      dataChannel.onmessage = (event) => {
        let payload: Record<string, unknown>;
        try {
          payload = JSON.parse(event.data as string);
        } catch {
          return; // Not JSON — ignore.
        }

        // Real voice barge-in: the server detected the student talking over
        // the Agent and is cancelling/truncating its response server-side.
        // Pause local playback immediately for a snappy felt interruption
        // instead of waiting for the audio track itself to go quiet.
        if (payload.type === 'input_audio_buffer.speech_started') {
          audioElementRef.current?.pause();
        }

        if (
          onTranscript &&
          payload.type === 'conversation.item.input_audio_transcription.completed' &&
          typeof payload.transcript === 'string' &&
          payload.transcript.trim()
        ) {
          onTranscript(payload.transcript.trim());
        }

        // Deep Dive's `mark_explanation_understood` tool call — same
        // "check both possible completed-item events" defensiveness as
        // EngramReviewAgent's show_visual_aid handling, since it's unclear
        // ahead of time which one Azure will use for a given session.
        if (onExplanationAccepted) {
          const items =
            payload.type === 'response.output_item.done' && payload.item
              ? [payload.item as Record<string, unknown>]
              : payload.type === 'response.done'
                ? (((payload.response as Record<string, unknown> | undefined)?.output as
                    | Record<string, unknown>[]
                    | undefined) ?? [])
                : [];
          for (const item of items) {
            if (item.type !== 'function_call' || item.name !== 'mark_explanation_understood') continue;
            const callId = item.call_id as string;
            if (handledToolCallIdsRef.current.has(callId)) continue;
            handledToolCallIdsRef.current.add(callId);
            let explanation = '';
            try {
              const args = JSON.parse((item.arguments as string) ?? '{}') as { explanation?: string };
              explanation = args.explanation?.trim() ?? '';
            } catch {
              explanation = '';
            }
            if (explanation) onExplanationAccepted(explanation);
            if (dataChannelRef.current?.readyState === 'open') {
              dataChannelRef.current.send(
                JSON.stringify({
                  type: 'conversation.item.create',
                  item: { type: 'function_call_output', call_id: callId, output: 'Explicación registrada para calificación real.' },
                })
              );
              dataChannelRef.current.send(JSON.stringify({ type: 'response.create' }));
            }
          }
        }
      };
      dataChannel.onopen = () => {
        // FIXED 2026-07-25: this GA Realtime session schema nests
        // turn_detection under `session.audio.input.turn_detection` (same
        // place as transcription), NOT at the top level like the legacy
        // Realtime API. The previous top-level `session: { turn_detection }`
        // sent here was silently ignored by the server, so the connection
        // actually ran on the server's DEFAULT (much more trigger-happy) VAD
        // sensitivity the whole time — the real cause of the Agent
        // self-interrupting on its own voice bleeding back into an open mic
        // (no headphones) and answering as if the student had said
        // something when they hadn't. The backend now ALSO sets this
        // correctly (plus `noise_reduction: near_field`) right at session
        // creation (see RealtimeVoiceSessionService), so this client-side
        // update is now redundant/defensive rather than the only source of
        // truth — kept for clarity and in case a future per-session
        // override is needed.
        dataChannel.send(
          JSON.stringify({
            type: 'session.update',
            session: {
              audio: {
                input: {
                  turn_detection: {
                    type: 'server_vad',
                    threshold: 0.85,
                    prefix_padding_ms: 300,
                    silence_duration_ms: 700,
                    create_response: true,
                    interrupt_response: true,
                  },
                  noise_reduction: { type: 'near_field' },
                },
              },
            },
          }),
        );


        // The Realtime model otherwise waits silently for the student to
        // speak first. Trigger its first turn WITHOUT overriding
        // `instructions` here — a `response.create.response.instructions`
        // override REPLACES the session's grounded system prompt for that
        // turn, which is what caused the Agent to open with a generic
        // "¿en qué puedo ayudarte?" instead of diving into the actual
        // lesson content. The real "start immediately, no generic
        // greeting" instruction now lives in the session's own
        // instructions (see VoiceTutorSessionFunction.BuildInstructions),
        // so it stays in effect for the whole session, not just this call.
        dataChannel.send(JSON.stringify({ type: 'response.create' }));
      };

      const offer = await peerConnection.createOffer();
      await peerConnection.setLocalDescription(offer);

      const sdpResponse = await fetch(session.RealtimeCallsUrl, {
        method: 'POST',
        body: offer.sdp,
        headers: {
          Authorization: `Bearer ${session.ClientSecret}`,
          'Content-Type': 'application/sdp',
        },
      });

      if (!sdpResponse.ok) {
        throw new Error(t('voiceTutorAgent.negotiationError', { status: sdpResponse.status }));
      }

      const answerSdp = await sdpResponse.text();
      await peerConnection.setRemoteDescription({ type: 'answer', sdp: answerSdp });

      setState('connected');
    } catch (err) {
      console.error('VoiceTutorAgent: connection failed', err);
      
      // Distinguish between "step type not supported" vs other connection errors
      let errorMsg = t('voiceTutorAgent.connectionErrorFallback');
      const errorText = String(err);
      if (errorText.includes('only available for')) {
        errorMsg = t('voiceTutorAgent.stepNotSupported');
      }
      
      setErrorMessage(errorMsg);
      teardown();
      setState('error');
    }
  }, [
    learningSessionStepId,
    capabilityGraphNodeId,
    stepType,
    promptText,
    personName,
    onTranscript,
    onExplanationAccepted,
    reviewOnly,
    reviewSession,
    monitorRemoteAudioLevel,
    teardown,
  ]);

  const isActive = state === 'connected' || state === 'listening' || state === 'speaking';
  const isBusy = state === 'connecting';

  // Auto-start: rather than making the student click the orb, connect the
  // Agent on its own shortly after landing on a step so it starts reading
  // the instructions right away — this reads as more intuitive than a
  // silent, inert button. A short delay (instead of instant) avoids firing
  // while the step's own content/audio setup is still settling in, and
  // gives the student a beat to see the orb appear before it starts
  // talking. Only fires if nothing else has already put the agent into a
  // non-idle state (e.g. the student clicked it manually before the timer,
  // or a previous attempt is already connecting/connected/errored).
  const connectRef = useRef(connect);
  useEffect(() => {
    connectRef.current = connect;
  }, [connect]);

  useEffect(() => {
    if (!autoConnect) return undefined;
    const timer = setTimeout(() => {
      if (stateRef.current === 'idle') {
        connectRef.current();
      }
    }, 3000);
    return () => clearTimeout(timer);
  }, [targetKey, autoConnect]);

  if (compact) {
    return (
      <div className="mt-2 flex flex-col items-start gap-1">
        <button
          type="button"
          onClick={isActive ? teardown : connect}
          disabled={isBusy}
          className="flex items-center gap-1.5 rounded-full border border-accent-400/30 bg-accent-500/10 px-3 py-1.5 text-xs font-semibold text-accent-300 transition hover:bg-accent-500/20 disabled:opacity-50"
        >
          {isBusy ? (
            <Sparkles className="h-3.5 w-3.5 animate-spin" />
          ) : isActive ? (
            <Mic className="h-3.5 w-3.5" />
          ) : (
            <Bot className="h-3.5 w-3.5" />
          )}
          {isActive ? getStateLabels(t)[state] : startLabel ?? t('voiceTutorAgent.talkAboutExample')}
        </button>
        {errorMessage && <p className="text-[11px] text-red-400">{errorMessage}</p>}
        {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
        <audio ref={audioElementRef} autoPlay />
      </div>
    );
  }

  return (
    <div className="mt-6 flex flex-col items-center gap-3 rounded-2xl border border-accent-400/20 bg-gradient-to-b from-accent-500/[0.08] to-transparent p-6">
      <button
        type="button"
        onClick={isActive ? teardown : connect}
        disabled={isBusy}
        className="group relative flex h-20 w-20 items-center justify-center rounded-full outline-none"
        aria-label={isActive ? t('voiceTutorAgent.stopAgent') : startLabel ?? t('voiceTutorAgent.talkToAgent')}
      >
        {(state === 'listening' || state === 'speaking' || state === 'connecting') && (
          <span className="absolute inset-0 animate-ping rounded-full bg-accent-400/30" />
        )}
        <span
          className={`absolute inset-0 rounded-full bg-gradient-to-br from-brand-400 via-accent-400 to-brand-600 shadow-lg shadow-accent-500/30 transition-transform duration-300 ${
            state === 'speaking' ? 'scale-110' : state === 'listening' ? 'scale-105' : 'scale-100'
          }`}
        />
        <span className="relative flex h-[72px] w-[72px] items-center justify-center rounded-full border border-white/20 bg-slate-950/50">
          {isBusy ? (
            <Sparkles className="h-7 w-7 animate-spin text-white" />
          ) : isActive ? (
            <Mic className="h-7 w-7 text-white" />
          ) : (
            <Bot className="h-7 w-7 text-white/80" />
          )}
        </span>
      </button>

      <p className="text-xs font-semibold uppercase tracking-wide text-accent-300">
        {state === 'idle' && startLabel ? startLabel : getStateLabels(t)[state]}
      </p>
      {errorMessage && <p className="text-xs text-red-400">{errorMessage}</p>}

      {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
      <audio ref={audioElementRef} autoPlay />
    </div>
  );
}
