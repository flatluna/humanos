import { useCallback, useEffect, useRef, useState } from 'react';
import { Mic, Bot, Sparkles } from 'lucide-react';
import { requestVoiceTutorSession } from '../api/runtimeSessionApi';

type VoiceState = 'idle' | 'connecting' | 'connected' | 'listening' | 'speaking' | 'error';

const STATE_LABELS: Record<VoiceState, string> = {
  idle: 'Toca para hablar',
  connecting: 'Conectando…',
  connected: 'Conectado — puedes hablar',
  listening: 'Escuchando…',
  speaking: 'Hablando…',
  error: 'No se pudo conectar',
};

/**
 * "Agente de voz" — a real-time WebRTC voice presence for the Instructor
 * Runtime (Azure OpenAI GPT Realtime, see VoiceTutorSessionFunction.cs and
 * /memories/repo/voice-tutor-realtime-design.md), ported from Capability
 * Studio's VoiceTutorAgent.tsx (2026-07-22) — deliberately styled as a
 * standalone AI AGENT — a glowing orb avatar with its own state — instead
 * of a plain button, per explicit product direction.
 *
 * Scope in the real student app: only rendered on Hypothesis/Teaching
 * steps (see NodeWorkflowPage.tsx). NEVER on Recall — a live conversational
 * voice agent would let the student ask it questions or fish for hints/
 * answers mid-recall, which defeats unaided retrieval (the whole point of
 * that stage). Grading/recall verification always happens through the
 * existing TutorService text-submit path regardless. Never rendered on
 * Production/Assessment either. This component owns the ENTIRE WebRTC
 * lifecycle itself: it mints the ephemeral token, negotiates the SDP
 * offer/answer directly against Azure (browser ↔ Azure, never proxied
 * through our own backend), plays the remote audio, and derives a
 * lightweight "listening"/"speaking" visual state purely from the remote
 * audio stream's volume (via Web Audio's AnalyserNode).
 */
export default function VoiceTutorAgent({
  learningSessionStepId,
  promptText,
  onTranscript,
  onExplanationAccepted,
  exampleCardIndex,
  autoConnect = true,
  compact = false,
}: {
  learningSessionStepId: string;
  promptText?: string;
  onTranscript?: (transcript: string) => void;
  /** Deep Dive only (2026-07-30, ported from Capability Studio): fired
   * when the voice Agent itself calls the `mark_explanation_understood`
   * Realtime tool — i.e. the exact moment IT decides (per its own
   * hard-capped instructions) the student's spoken explanation is good
   * enough. The caller should forward this straight to the same
   * deterministic grading endpoint used for a typed answer
   * (submitDeepDiveAttempt) — this callback only ever signals WHEN to
   * grade, never the grade itself. */
  onExplanationAccepted?: (explanation: string) => void;
  /** Only for the Deep Dive step (2026-07-29) — which ExampleCard's "habla
   * con tu maestro" button was pressed, so the voice Agent explains THAT
   * specific example (with its result) instead of the step's own generic
   * content. */
  exampleCardIndex?: number;
  /** Set to false for a per-ExampleCard button (2026-07-29) — the Agent
   * must only start talking when the student explicitly presses that
   * card's button, never auto-connect on a timer like Hypothesis/Teaching. */
  autoConnect?: boolean;
  /** Renders a small inline button instead of the full glowing-orb card —
   * used for the per-ExampleCard "habla con tu maestro" button. */
  compact?: boolean;
}) {
  // Stable identity for "which step is this widget currently about" — used
  // to reset/re-auto-connect when the parent page swaps in a different
  // step without remounting this component instance.
  const targetKey = exampleCardIndex !== undefined ? learningSessionStepId + ':' + exampleCardIndex : learningSessionStepId;

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
  // Grace-period timer that ramps the mic gain back up a short beat after
  // the Agent's audio goes quiet — see monitorRemoteAudioLevel below.
  const micGraceTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Real barge-in support (2026-07-24, ported from EngramReviewAgent.tsx):
  // instead of hard-muting the mic track while the Agent talks (which
  // fully blocks genuine interruption, not just echo), the raw mic is
  // routed through a GainNode before being sent over the peer connection.
  // While the Agent is speaking the gain is ducked way down (not to zero)
  // so ordinary speaker-bleed echo stays below the server VAD's
  // `threshold`, but a real, clearly louder interruption from the student
  // still crosses it and triggers `interrupt_response: true` server-side
  // like a normal conversation.
  const micGainNodeRef = useRef<GainNode | null>(null);
  const micProcessingContextRef = useRef<AudioContext | null>(null);
  const micProcessedStreamRef = useRef<MediaStream | null>(null);
  // Dedup guard for the Deep Dive `mark_explanation_understood` tool call —
  // Azure can legitimately report the SAME completed function call via both
  // `response.output_item.done` and `response.done`; only ever act on a
  // given call_id once, and only ever send its function_call_output once
  // (double-answering re-triggers the model into generating extra
  // responses).
  const handledToolCallIdsRef = useRef<Set<string>>(new Set());

  const teardown = useCallback(() => {
    if (animationFrameRef.current !== null) {
      cancelAnimationFrame(animationFrameRef.current);
      animationFrameRef.current = null;
    }
    if (micGraceTimeoutRef.current !== null) {
      clearTimeout(micGraceTimeoutRef.current);
      micGraceTimeoutRef.current = null;
    }
    dataChannelRef.current = null;
    peerConnectionRef.current?.close();
    peerConnectionRef.current = null;
    localStreamRef.current?.getTracks().forEach((track) => track.stop());
    localStreamRef.current = null;
    micProcessedStreamRef.current?.getTracks().forEach((track) => track.stop());
    micProcessedStreamRef.current = null;
    micGainNodeRef.current = null;
    micProcessingContextRef.current?.close().catch(() => undefined);
    micProcessingContextRef.current = null;
    audioContextRef.current?.close().catch(() => undefined);
    audioContextRef.current = null;
    setState('idle');
  }, []);

  useEffect(() => teardown, [teardown]);

  // The parent page reuses this same component instance across steps
  // (Hipótesis → Enseñanza) without remounting it. If a voice session is
  // active/connecting for the PREVIOUS step when the student advances, it
  // must be torn down here — otherwise the student keeps talking to an
  // Agent still holding the old step's context.
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

      // FIXED 2026-07-24 — replaced the earlier hard mic-mute (see below)
      // with GainNode-based "ducking": confirmed real-world setup is
      // laptop/desktop speakers + mic (no headphones), which leaks enough
      // acoustic echo of the Agent's OWN voice back into the mic that
      // browser echoCancellation alone doesn't fully cancel it. But fully
      // muting the mic while the Agent spoke also blocked genuine
      // barge-in entirely (the server's VAD never saw ANY mic audio to
      // judge). Ducking to a low (not zero) gain lets a real, clearly
      // louder interruption still cross the server's VAD `threshold` while
      // ordinary quieter speaker-bleed echo mostly stays below it. Ramped
      // smoothly (setTargetAtTime) to avoid audible clicks/zipper noise.
      const micGain = micGainNodeRef.current;
      if (micGain) {
        const ctx = micProcessingContextRef.current;
        const now = ctx?.currentTime ?? 0;
        if (isAgentSpeaking) {
          micGain.gain.setTargetAtTime(0.12, now, 0.05);
          if (micGraceTimeoutRef.current !== null) {
            clearTimeout(micGraceTimeoutRef.current);
            micGraceTimeoutRef.current = null;
          }
        } else if (micGain.gain.value < 0.9 && micGraceTimeoutRef.current === null) {
          micGraceTimeoutRef.current = setTimeout(() => {
            const ctx2 = micProcessingContextRef.current;
            micGainNodeRef.current?.gain.setTargetAtTime(1, ctx2?.currentTime ?? 0, 0.05);
            micGraceTimeoutRef.current = null;
          }, 500);
        }
      }

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
      const session = await requestVoiceTutorSession({ learningSessionStepId, promptText, exampleCardIndex });

      // Explicit (not just `true`) so the browser can't skip echo
      // cancellation — without it, the mic picks up the Agent's own voice
      // from the speakers and the model ends up "talking to itself".
      // Headphones are still strongly recommended for best results,
      // especially on laptops with the mic close to the speakers.
      const micStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      });
      localStreamRef.current = micStream;

      // Route the raw mic through a GainNode so it can be ducked (not hard
      // muted) while the Agent is speaking — see monitorRemoteAudioLevel.
      // The peer connection sends the PROCESSED stream's track, never the
      // raw hardware mic track directly.
      const micProcessingContext = new AudioContext();
      micProcessingContextRef.current = micProcessingContext;
      const micSource = micProcessingContext.createMediaStreamSource(micStream);
      const micGainNode = micProcessingContext.createGain();
      micGainNode.gain.value = 1;
      micGainNodeRef.current = micGainNode;
      const micDestination = micProcessingContext.createMediaStreamDestination();
      micSource.connect(micGainNode).connect(micDestination);
      micProcessedStreamRef.current = micDestination.stream;

      const peerConnection = new RTCPeerConnection();
      peerConnectionRef.current = peerConnection;

      micDestination.stream.getTracks().forEach((track) => peerConnection.addTrack(track, micDestination.stream));

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

        // Deep Dive's `mark_explanation_understood` tool call — the Agent
        // decides itself (per its own instructions) when the student's
        // spoken explanation is good enough; this only signals WHEN to
        // grade, never the grade itself (grading always goes through the
        // same deterministic submitDeepDiveAttempt endpoint as a typed
        // answer).
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
        // Real barge-in: keep the mic live and let the server's own VAD
        // decide when the student is genuinely interrupting (as opposed to
        // acoustic echo of the Agent's own voice leaking into the mic).
        // `interrupt_response: true` (the API default, set explicitly here)
        // makes the server auto-cancel the in-progress response and
        // auto-truncate its unplayed audio the moment real speech is
        // detected — WebRTC connections need no manual truncate bookkeeping
        // client-side for this. The `threshold` stays high enough that
        // ordinary speaker-bleed echo (with browser echoCancellation on)
        // usually doesn't cross it, while a real, clearly louder
        // interruption does.
        dataChannel.send(
          JSON.stringify({
            type: 'session.update',
            session: {
              turn_detection: {
                type: 'server_vad',
                threshold: 0.75,
                prefix_padding_ms: 300,
                silence_duration_ms: 700,
                create_response: true,
                interrupt_response: true,
              },
            },
          })
        );

        // The Realtime model otherwise waits silently for the student to
        // speak first. Trigger its first turn WITHOUT overriding
        // `instructions` here — a `response.create.response.instructions`
        // override REPLACES the session's grounded system prompt for that
        // turn, which is what caused the Agent to open with a generic
        // "¿en qué puedo ayudarte?" instead of diving into the actual
        // lesson content. The real "start immediately, no generic
        // greeting" instruction lives in the session's own instructions
        // (see VoiceTutorSessionFunction.BuildInstructions), so it stays in
        // effect for the whole session, not just this call.
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
        throw new Error(`No se pudo negociar la sesión de voz (status ${sdpResponse.status}).`);
      }

      const answerSdp = await sdpResponse.text();
      await peerConnection.setRemoteDescription({ type: 'answer', sdp: answerSdp });

      setState('connected');
    } catch (err) {
      console.error('VoiceTutorAgent: connection failed', err);
      setErrorMessage('No se pudo conectar con el Agente de voz. Verifica el micrófono e intenta de nuevo.');
      teardown();
      setState('error');
    }
  }, [learningSessionStepId, promptText, onTranscript, onExplanationAccepted, monitorRemoteAudioLevel, teardown]);

  const isActive = state === 'connected' || state === 'listening' || state === 'speaking';
  const isBusy = state === 'connecting';

  // Full stop: tapping the orb while it's connected/listening/speaking
  // always ends the conversation entirely (closes the RTCPeerConnection,
  // stops the mic, loses the session context) — a single, predictable
  // "stop" action instead of a two-tier interrupt-vs-stop distinction the
  // student had no reliable way to control. Automatic barge-in (the
  // server's own VAD cancelling/truncating an in-progress response the
  // moment the student starts talking, via `interrupt_response: true`
  // below) still happens on its own and needs no button press.
  const stopConversation = useCallback(() => {
    const dataChannel = dataChannelRef.current;
    if (dataChannel?.readyState === 'open') {
      dataChannel.send(JSON.stringify({ type: 'response.cancel' }));
    }
    teardown();
  }, [teardown]);

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
          onClick={isActive ? stopConversation : connect}
          disabled={isBusy}
          className="flex items-center gap-1.5 rounded-full border border-accent-300 bg-accent-50 px-3 py-1.5 text-xs font-semibold text-accent-700 transition hover:bg-accent-100 disabled:opacity-50 dark:border-accent-400/30 dark:bg-accent-500/10 dark:text-accent-300 dark:hover:bg-accent-500/20"
        >
          {isBusy ? (
            <Sparkles className="h-3.5 w-3.5 animate-spin" />
          ) : isActive ? (
            <Mic className="h-3.5 w-3.5" />
          ) : (
            <Bot className="h-3.5 w-3.5" />
          )}
          {isActive ? STATE_LABELS[state] : 'Habla con tu maestro sobre este ejemplo'}
        </button>
        {errorMessage && <p className="text-[11px] text-red-600 dark:text-red-400">{errorMessage}</p>}
        {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
        <audio ref={audioElementRef} autoPlay />
      </div>
    );
  }

  return (
    <div className="mt-6 flex flex-col items-center gap-3 rounded-2xl border border-accent-200 bg-gradient-to-b from-accent-50 to-white p-6 dark:border-accent-400/20 dark:from-accent-500/[0.08] dark:to-transparent">
      <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-transparent bg-clip-text bg-gradient-to-r from-brand-600 to-accent-600 dark:from-brand-400 dark:to-accent-400">
        <Sparkles className="h-3.5 w-3.5 text-accent-600 dark:text-accent-400" />
        Agente Engram de IA
      </p>

      <button
        type="button"
        onClick={isActive ? stopConversation : connect}
        disabled={isBusy}
        className="group relative flex h-20 w-20 items-center justify-center rounded-full outline-none"
        aria-label={isActive ? 'Detener al Agente de voz' : 'Hablar con el Agente de voz'}
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

      <p className="text-xs font-semibold uppercase tracking-wide text-accent-600 dark:text-accent-300">{STATE_LABELS[state]}</p>
      {errorMessage && <p className="text-xs text-red-600 dark:text-red-400">{errorMessage}</p>}

      {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
      <audio ref={audioElementRef} autoPlay />
    </div>
  );
}
