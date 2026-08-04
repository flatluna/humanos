import { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Mic, Bot, Sparkles, X, Image as ImageIcon } from 'lucide-react';
import { requestEngramReviewSession, requestGeneratedVisualAidImage } from '../api/runtimeSessionApi';
import { API_BASE_URL } from '../api/httpClient';

type VoiceState = 'idle' | 'connecting' | 'connected' | 'listening' | 'speaking' | 'error';

/** This node's real, already-generated illustrations — fetched straight
 * from the ephemeral-session HTTP response (VoiceTutorSessionDto.Illustrations)
 * and shown immediately, independent of whether the model ever calls the
 * Realtime tool (added 2026-07-24 after live testing showed the tool-call
 * round trip alone is unreliable — the model sometimes claims a visual was
 * shown when nothing actually rendered). Also gains one entry per
 * on-demand `generated_image` tool call during the conversation (2026-07-24
 * — added to this SAME browsable list, `isGenerated: true`, rather than a
 * temporary overlay that disappears, so a student can freely flip back to
 * any picture the Agent drew earlier in the conversation instead of losing
 * it after a timeout). `url` is a real HTTP URL for course illustrations,
 * or a `data:` URI for generated ones (never uploaded/persisted anywhere). */
type NodeIllustration = { id: string; url: string; caption?: string; isGenerated?: boolean };

/** The four OPTIONAL, model-composed (not pre-existing) visual examples the
 * Agent can trigger mid-conversation via the `show_visual_aid` Realtime
 * tool — shown as a temporary overlay on top of the illustration gallery,
 * then reverts back automatically. See EngramReviewSessionFunction.BuildVisualAidTools.
 * `text` (added 2026-07-24) is a small "whiteboard" for free-form written
 * text — used whenever the student explicitly asks the Agent to WRITE/spell
 * something out (e.g. "how do you spell good morning"), not just say it.
 * `generated_image` (added 2026-07-24) only ever appears here in its
 * `loading` state (a brief spinner while the real AI image is being drawn)
 * — once ready, it's pushed into `nodeIllustrations` instead (see that
 * type's doc comment), never rendered as an EphemeralAid itself. */
type EphemeralAid =
  | { kind: 'emoji'; emoji: string; caption?: string }
  | { kind: 'chart'; items: { label: string; value: number }[]; caption?: string }
  | { kind: 'text'; text: string; caption?: string }
  | { kind: 'generated_image'; loading: true };

const STATE_LABELS: Record<VoiceState, string> = {
  idle: 'Preparando el repaso…',
  connecting: 'Conectando…',
  connected: 'Conectado — puedes hablar',
  listening: 'Escuchando…',
  speaking: 'Hablando…',
  error: 'No se pudo conectar',
};

/**
 * "Reactivar Engrama" — a full-screen Agent presence for reviewing an
 * already-Mastered node via spoken active recall (NOT an exam, never
 * grades anything). Sibling of VoiceTutorAgent.tsx, same WebRTC/Azure
 * OpenAI Realtime plumbing (own ephemeral token, direct browser↔Azure SDP
 * negotiation, volume-derived listening/speaking state), but:
 * - Grounded in the WHOLE node (all 5 steps + the student's own past
 *   answers via EngramReviewSessionFunction/GetNodeSummaryAsync), not one
 *   step's content.
 * - Rendered as a dedicated modal overlay launched explicitly from the
 *   node-summary recap screen's "Reactivar Engrama" button, rather than an
 *   inline widget auto-appearing on a step.
 * - No `onTranscript` — this is a free-flowing study conversation, not
 *   feeding an answer textbox.
 */
export default function EngramReviewAgent({
  personId,
  capabilityGraphNodeId,
  exampleCardIndex,
  focusCardTitle,
  embedded = false,
  onExit,
}: {
  personId: string;
  capabilityGraphNodeId: string;
  /** Optional (2026-08-02) — 0-based index of one Deep Dive ExampleCard to
   * focus this conversation on, from the Brain page's card browser, instead
   * of the whole lesson. */
  exampleCardIndex?: number;
  /** Optional label shown in the modal header when exampleCardIndex is set,
   * so the student sees which card they're about to have explained. Unused
   * when `embedded` is true (the parent card view already shows the title). */
  focusCardTitle?: string;
  /** When true (2026-08-02), renders WITHOUT its own full-screen portal/
   * backdrop/title chrome or illustration gallery — just the mic
   * orb/state/exit controls plus any on-demand ephemeral visual aid, meant
   * to be dropped directly inside an already-open card modal (e.g.
   * DeepDiveCardModal) so activating the agent never opens a second modal
   * window on top of the first. */
  embedded?: boolean;
  onExit: () => void;
}) {
  const [state, setState] = useState<VoiceState>('idle');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [nodeIllustrations, setNodeIllustrations] = useState<NodeIllustration[]>([]);
  const [illustrationIndex, setIllustrationIndex] = useState(0);
  const [ephemeralAid, setEphemeralAid] = useState<EphemeralAid | null>(null);
  const [ephemeralAidVisible, setEphemeralAidVisible] = useState(false);

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
  const micGraceTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const ephemeralAidHideTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Real barge-in support (2026-07-24): instead of hard-muting the mic
  // track while the Agent talks (which fully blocks genuine interruption,
  // not just echo), the raw mic is routed through a GainNode before being
  // sent over the peer connection. While the Agent is speaking the gain is
  // ducked way down (not to zero) so ordinary speaker-bleed echo stays
  // below the server VAD's `threshold`, but a real, clearly louder
  // interruption from the student still crosses it and triggers
  // `interrupt_response: true` server-side like a normal conversation.
  const micGainNodeRef = useRef<GainNode | null>(null);
  const micProcessingContextRef = useRef<AudioContext | null>(null);
  const micProcessedStreamRef = useRef<MediaStream | null>(null);
  // Kept in a ref (mirrors nodeIllustrations state) so handleShowIllustration's
  // functional setIllustrationIndex updater can read the latest fetched list
  // without needing to depend on/re-create the callback whenever it changes.
  const nodeIllustrationsRef = useRef<NodeIllustration[]>([]);
  // A completed show_visual_aid tool call can legitimately be reported by
  // BOTH `response.output_item.done` (as soon as that item finishes) AND
  // again inside `response.done`'s `response.output` array (the final
  // summary of the whole turn) — this is normal Realtime API behavior, not
  // a bug. Without dedup, each occurrence answers the tool call again and
  // sends another `response.create`, which snowballs into the model
  // generating endless extra responses (the Agent never stops talking, and
  // the mic never gets a chance to re-enable). Track handled call_ids here.
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
    if (ephemeralAidHideTimeoutRef.current !== null) {
      clearTimeout(ephemeralAidHideTimeoutRef.current);
      ephemeralAidHideTimeoutRef.current = null;
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
    setNodeIllustrations([]);
    setIllustrationIndex(0);
    setEphemeralAid(null);
    setEphemeralAidVisible(false);
    setState('idle');
  }, []);


  useEffect(() => teardown, [teardown]);

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

      // Duck (not mute) the outgoing mic level while the Agent's own audio
      // is audible — see micGainNodeRef's doc comment above. A real,
      // louder interruption still reaches the server's VAD and triggers
      // `interrupt_response: true`; ordinary speaker-bleed echo mostly
      // doesn't. Ramped smoothly (setTargetAtTime) to avoid audible
      // clicks/zipper noise.
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

  // Handles the Realtime "show_visual_aid" tool call: reveals an OPTIONAL
  // visual (a real pre-generated illustration, an emoji example, or a
  // simple bar chart — all zero-latency, nothing is ever generated on
  // demand here) and answers the tool call so the Agent's own turn can
  // continue naturally.
  const sendToolResult = useCallback((callId: string, output: string) => {
    const dataChannel = dataChannelRef.current;
    if (dataChannel?.readyState === 'open') {
      dataChannel.send(
        JSON.stringify({
          type: 'conversation.item.create',
          item: { type: 'function_call_output', call_id: callId, output },
        })
      );
      dataChannel.send(JSON.stringify({ type: 'response.create' }));
    }
  }, []);

  // kind="illustration": the requested image is ALREADY in nodeIllustrations
  // (fetched upfront from the HTTP response) — just jump the gallery to it,
  // no timeout/revert since it's a real part of the lesson's material.
  const handleShowIllustration = useCallback(
    (callId: string, illustrationId: string) => {
      setIllustrationIndex((currentIndex) => {
        const idx = nodeIllustrationsRef.current.findIndex((i) => i.id === illustrationId);
        return idx >= 0 ? idx : currentIndex;
      });
      sendToolResult(callId, 'Ilustración mostrada al estudiante en pantalla.');
    },
    [sendToolResult]
  );

  // kind="emoji"/"chart"/"text": a lightweight example (or written text)
  // the model composes itself — shown as a temporary overlay above the
  // illustration gallery, then reverts back automatically.
  const handleShowEphemeralAid = useCallback(
    (callId: string, aid: EphemeralAid) => {
      setEphemeralAid(aid);
      setEphemeralAidVisible(false);
      requestAnimationFrame(() => setEphemeralAidVisible(true));

      if (ephemeralAidHideTimeoutRef.current !== null) {
        clearTimeout(ephemeralAidHideTimeoutRef.current);
      }
      // Written text stays up longer (40s) than emoji/chart (25s) — it's
      // meant to be read/copied, not just glanced at.
      ephemeralAidHideTimeoutRef.current = setTimeout(
        () => {
          setEphemeralAidVisible(false);
          setTimeout(() => setEphemeralAid(null), 300);
          ephemeralAidHideTimeoutRef.current = null;
        },
        aid.kind === 'text' ? 40000 : 25000
      );

      sendToolResult(callId, 'Ayuda visual mostrada al estudiante en pantalla.');
    },
    [sendToolResult]
  );

  // kind="generated_image": no existing illustration fits, so an actual new
  // image is generated on demand (Azure OpenAI gpt-image-1.5) from the
  // model's own descriptive prompt. Shows a brief loading overlay first
  // since generation takes a few seconds; once ready, the image joins the
  // SAME persistent, browsable gallery as the lesson's own illustrations
  // (never lost/auto-hidden — the student can flip back to it any time via
  // the ‹ › gallery controls, exactly like a real course illustration).
  // Answers the tool call only once the real outcome (success or failure)
  // is known, so the Agent can react appropriately either way instead of
  // the turn stalling.
  const handleGenerateImage = useCallback(
    async (callId: string, imagePrompt: string, caption?: string) => {
      setEphemeralAid({ kind: 'generated_image', loading: true });
      setEphemeralAidVisible(false);
      requestAnimationFrame(() => setEphemeralAidVisible(true));
      if (ephemeralAidHideTimeoutRef.current !== null) {
        clearTimeout(ephemeralAidHideTimeoutRef.current);
        ephemeralAidHideTimeoutRef.current = null;
      }
      try {
        const result = await requestGeneratedVisualAidImage(imagePrompt);
        const newIllustration: NodeIllustration = {
          id: `generated-${Date.now()}`,
          url: `data:${result.ContentType};base64,${result.ImageBase64}`,
          caption: caption ?? 'Imagen generada por el Agente',
          isGenerated: true,
        };
        const updated = [...nodeIllustrationsRef.current, newIllustration];
        nodeIllustrationsRef.current = updated;
        setNodeIllustrations(updated);
        setIllustrationIndex(updated.length - 1);
        setEphemeralAidVisible(false);
        setTimeout(() => setEphemeralAid(null), 300);
        sendToolResult(callId, 'Imagen generada y agregada a la galeria en pantalla.');
      } catch (err) {
        console.error('EngramReviewAgent: generated_image failed', err);
        setEphemeralAidVisible(false);
        setTimeout(() => setEphemeralAid(null), 300);
        sendToolResult(
          callId,
          'No se pudo generar la imagen esta vez (falla temporal). Dile brevemente al estudiante que no se pudo dibujar y continua la conversacion con normalidad.'
        );
      }
    },
    [sendToolResult]
  );

  const connect = useCallback(async () => {
    setErrorMessage(null);
    setState('connecting');
    handledToolCallIdsRef.current.clear();
    try {
      const session = await requestEngramReviewSession({ personId, capabilityGraphNodeId, exampleCardIndex });

      const illustrations: NodeIllustration[] = (session.Illustrations ?? []).map((i) => ({
        id: i.Id,
        url: `${API_BASE_URL}/illustrations/${i.Id}/image`,
        caption: i.Caption ?? undefined,
      }));
      nodeIllustrationsRef.current = illustrations;
      setNodeIllustrations(illustrations);
      setIllustrationIndex(0);

      const micStream = await navigator.mediaDevices.getUserMedia({
        audio: {
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        },
      });
      localStreamRef.current = micStream;

      // Route the raw mic through a GainNode so it can be ducked (not fully
      // muted) while the Agent talks — see micGainNodeRef's doc comment.
      // The peer connection sends the PROCESSED stream's track, not the
      // hardware track directly.
      const micProcessingContext = new AudioContext();
      micProcessingContextRef.current = micProcessingContext;
      const micSource = micProcessingContext.createMediaStreamSource(micStream);
      const micGain = micProcessingContext.createGain();
      micGain.gain.value = 1;
      micSource.connect(micGain);
      const micDestination = micProcessingContext.createMediaStreamDestination();
      micGain.connect(micDestination);
      micGainNodeRef.current = micGain;
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
          return;
        }
        if (payload.type === 'input_audio_buffer.speech_started') {
          audioElementRef.current?.pause();
        }
        // Realtime function-calling: fired once a tool call the model made
        // (show_visual_aid) is fully assembled. Works whether the completed
        // item arrives via `response.output_item.done` or as part of
        // `response.done`'s `response.output` array — the exact event Azure
        // uses in `webrtcfilter=on` mode wasn't confirmed ahead of time, so
        // both are handled defensively. Also logged to the console (both the
        // raw event type and matched tool calls) so a live test can confirm
        // exactly what Azure sends in this mode.
        console.log('[EngramReviewAgent] data channel event:', payload.type, payload);
        const items =
          payload.type === 'response.output_item.done' && payload.item
            ? [payload.item as Record<string, unknown>]
            : payload.type === 'response.done'
              ? (((payload.response as Record<string, unknown> | undefined)?.output as Record<string, unknown>[] | undefined) ?? [])
              : [];
        for (const item of items) {
          if (item.type !== 'function_call' || item.name !== 'show_visual_aid') continue;
          let args: {
            kind?: string;
            illustration_id?: string;
            emoji?: string;
            text?: string;
            image_prompt?: string;
            caption?: string;
            chart_items?: { label: string; value: number }[];
          };
          try {
            args = JSON.parse((item.arguments as string) ?? '{}');
          } catch {
            continue; // Malformed tool arguments — ignore, don't crash the session.
          }
          console.log('[EngramReviewAgent] show_visual_aid called with', args);
          const callId = item.call_id as string;
          // Guard against the same tool call being reported twice (see
          // handledToolCallIdsRef's comment) — only ever answer it once.
          if (handledToolCallIdsRef.current.has(callId)) continue;
          handledToolCallIdsRef.current.add(callId);
          if (args.kind === 'illustration' && args.illustration_id) {
            handleShowIllustration(callId, args.illustration_id);
          } else if (args.kind === 'emoji' && args.emoji) {
            handleShowEphemeralAid(callId, { kind: 'emoji', emoji: args.emoji, caption: args.caption });
          } else if (args.kind === 'chart' && args.chart_items?.length) {
            handleShowEphemeralAid(callId, { kind: 'chart', items: args.chart_items, caption: args.caption });
          } else if (args.kind === 'text' && args.text) {
            handleShowEphemeralAid(callId, { kind: 'text', text: args.text, caption: args.caption });
          } else if (args.kind === 'generated_image' && args.image_prompt) {
            handleGenerateImage(callId, args.image_prompt, args.caption);
          }
        }
      };
      dataChannel.onopen = () => {
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
      console.error('EngramReviewAgent: connection failed', err);
      setErrorMessage('No se pudo conectar con el Agente Engrama. Verifica el micrófono e intenta de nuevo.');
      teardown();
      setState('error');
    }
  }, [personId, capabilityGraphNodeId, exampleCardIndex, monitorRemoteAudioLevel, teardown, handleShowIllustration, handleShowEphemeralAid, handleGenerateImage]);

  const isActive = state === 'connected' || state === 'listening' || state === 'speaking';
  const isBusy = state === 'connecting';

  // Full stop: tapping the orb while it's connected/listening/speaking
  // always ends the conversation entirely — same reliable, predictable
  // "stop" action as VoiceTutorAgent.tsx. Automatic barge-in (the server's
  // own VAD cancelling/truncating an in-progress response) still happens
  // on its own via `interrupt_response: true` and needs no button press.
  const stopConversation = useCallback(() => {
    const dataChannel = dataChannelRef.current;
    if (dataChannel?.readyState === 'open') {
      dataChannel.send(JSON.stringify({ type: 'response.cancel' }));
    }
    teardown();
  }, [teardown]);

  const handleExit = useCallback(() => {
    stopConversation();
    onExit();
  }, [stopConversation, onExit]);

  // Auto-connect shortly after the modal opens — the student already
  // expressed intent by clicking "Reactivar Engrama", so there's no need
  // for a separate manual "start" tap.
  const connectRef = useRef(connect);
  useEffect(() => {
    connectRef.current = connect;
  }, [connect]);

  useEffect(() => {
    const timer = setTimeout(() => {
      if (stateRef.current === 'idle') {
        connectRef.current();
      }
    }, 600);
    return () => clearTimeout(timer);
  }, []);

  const ephemeralAidContent = ephemeralAid ? (
    <>
      {ephemeralAid.kind === 'emoji' && (
        <div className="flex flex-col items-center gap-2">
          <p className="text-5xl leading-none">{ephemeralAid.emoji}</p>
          {ephemeralAid.caption && <p className="text-sm text-slate-300">{ephemeralAid.caption}</p>}
        </div>
      )}
      {ephemeralAid.kind === 'chart' && (
        <div className="flex flex-col gap-2">
          {ephemeralAid.caption && <p className="mb-1 text-xs text-slate-400">{ephemeralAid.caption}</p>}
          {(() => {
            const max = Math.max(...ephemeralAid.items.map((it) => it.value), 1);
            return ephemeralAid.items.map((item, idx) => (
              <div key={idx} className="flex items-center gap-2 text-left">
                <span className="w-20 shrink-0 truncate text-xs text-slate-400">{item.label}</span>
                <div className="h-3 flex-1 overflow-hidden rounded-full bg-white/10">
                  <div
                    className="h-full rounded-full bg-gradient-to-r from-brand-400 to-accent-400"
                    style={{ width: `${Math.max((item.value / max) * 100, 4)}%` }}
                  />
                </div>
                <span className="w-10 shrink-0 text-right text-xs text-slate-300">{item.value}</span>
              </div>
            ));
          })()}
        </div>
      )}
      {ephemeralAid.kind === 'text' && (
        <div className="flex flex-col items-center gap-2 rounded-xl border border-white/10 bg-white p-4 text-slate-900">
          <p
            className={`whitespace-pre-wrap break-words text-lg font-semibold ${
              ephemeralAid.text.includes('\n') ? 'text-left' : 'text-center'
            }`}
          >
            {ephemeralAid.text}
          </p>
          {ephemeralAid.caption && <p className="text-xs text-slate-500">{ephemeralAid.caption}</p>}
        </div>
      )}
      {ephemeralAid.kind === 'generated_image' && ephemeralAid.loading && (
        <div className="flex flex-col items-center gap-2 py-6">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-accent-400/30 border-t-accent-400" />
          <p className="text-xs text-slate-400">Dibujando…</p>
        </div>
      )}
    </>
  ) : null;

  const controls = (
    <>
      <button
        type="button"
        onClick={isActive ? stopConversation : connect}
        disabled={isBusy}
        className="group relative mt-5 flex h-24 w-24 items-center justify-center rounded-full outline-none"
        aria-label={isActive ? 'Detener al Agente Engrama' : 'Hablar con el Agente Engrama'}
      >
        {(state === 'listening' || state === 'speaking' || state === 'connecting') && (
          <span className="absolute inset-0 animate-ping rounded-full bg-accent-400/30" />
        )}
        <span
          className={`absolute inset-0 rounded-full bg-gradient-to-br from-brand-400 via-accent-400 to-brand-600 shadow-lg shadow-accent-500/30 transition-transform duration-300 ${
            state === 'speaking' ? 'scale-110' : state === 'listening' ? 'scale-105' : 'scale-100'
          }`}
        />
        <span className="relative flex h-[88px] w-[88px] items-center justify-center rounded-full border border-white/20 bg-slate-950/50">
          {isBusy ? (
            <Sparkles className="h-8 w-8 animate-spin text-white" />
          ) : isActive ? (
            <Mic className="h-8 w-8 text-white" />
          ) : (
            <Bot className="h-8 w-8 text-white/80" />
          )}
        </span>
      </button>

      <p className="text-xs font-semibold uppercase tracking-wide text-accent-300">{STATE_LABELS[state]}</p>
      {errorMessage && <p className="text-xs text-red-400">{errorMessage}</p>}

      <button
        type="button"
        onClick={handleExit}
        className="mt-2 rounded-xl border border-white/10 px-4 py-2 text-sm font-medium text-slate-300 hover:bg-white/5"
      >
        Terminar repaso
      </button>

      {/* eslint-disable-next-line jsx-a11y/media-has-caption */}
      <audio ref={audioElementRef} autoPlay />
    </>
  );

  // Embedded mode (2026-08-02): no portal, no backdrop, no title chrome, no
  // illustration gallery — this is dropped directly inside an already-open
  // card modal (DeepDiveCardModal) so the illustration/title the student is
  // already looking at never changes and no second modal window appears.
  // Only the on-demand ephemeral visual aid (emoji/chart/text/generated
  // image the Agent triggers mid-conversation) is still shown here, since
  // it's genuinely new content, not a duplicate of the card's own image.
  if (embedded) {
    return (
      <div className="mt-2 flex w-full flex-col items-center gap-3">
        {ephemeralAid && (
          <div
            className={`w-full overflow-hidden rounded-2xl border border-accent-400/30 bg-slate-950/60 p-4 shadow-lg shadow-black/40 transition-all duration-300 ease-out ${
              ephemeralAidVisible ? 'opacity-100 scale-100' : 'opacity-0 scale-95'
            }`}
          >
            {ephemeralAidContent}
          </div>
        )}
        {controls}
      </div>
    );
  }

  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm">
      <div className="relative flex w-full max-w-md flex-col items-center gap-4 rounded-3xl border border-accent-400/30 bg-gradient-to-b from-slate-900 via-slate-950 to-slate-950 p-8 text-center shadow-2xl shadow-black/50">
        <button
          type="button"
          onClick={handleExit}
          className="absolute right-4 top-4 rounded-full p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
          aria-label="Cerrar"
        >
          <X className="h-5 w-5" />
        </button>

        <p className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wider text-transparent bg-clip-text bg-gradient-to-r from-brand-400 to-accent-400">
          <Sparkles className="h-3.5 w-3.5 text-accent-400" />
          Agente Engrama
        </p>
        <h2 className="text-lg font-semibold text-white">Reactivar Engrama</h2>
        {focusCardTitle && (
          <p className="-mt-2 rounded-full bg-white/5 px-3 py-1 text-xs font-medium text-accent-300">
            Tarjeta: {focusCardTitle}
          </p>
        )}
        <p className="text-sm text-slate-400">
          Repasemos juntos lo que ya aprendiste — sin examen, sin puntaje. Solo tú, tu memoria y una
          buena conversación.
        </p>

        {/* Persistent visual-aid area: ALWAYS present in the modal. Shows
            this node's REAL illustrations (fetched upfront via HTTP, not
            dependent on the model calling any tool) as a small browsable
            gallery when available; a model-triggered emoji/chart/text
            example temporarily overlays on top of it (like a small
            whiteboard for "text"), then reverts back. */}
        <div
          className={`w-full overflow-hidden rounded-2xl border transition-all duration-300 ease-out ${
            ephemeralAid
              ? `border-accent-400/30 bg-slate-950/60 p-4 shadow-lg shadow-black/40 ${
                  ephemeralAidVisible ? 'opacity-100 scale-100' : 'opacity-0 scale-95'
                }`
              : nodeIllustrations.length > 0
                ? 'border-accent-400/30 bg-slate-950/60'
                : 'border-dashed border-white/10 bg-white/[0.02] p-5'
          }`}
        >
          {ephemeralAid ? (
            ephemeralAidContent
          ) : nodeIllustrations.length > 0 ? (
            <div>
              {/* eslint-disable-next-line jsx-a11y/alt-text */}
              <img
                src={nodeIllustrations[illustrationIndex].url}
                alt={nodeIllustrations[illustrationIndex].caption ?? 'Ilustración de la lección'}
                className="max-h-56 w-full object-contain bg-slate-900"
              />
              {nodeIllustrations[illustrationIndex].caption && (
                <p className="px-3 pt-1.5 text-xs text-slate-400">
                  {nodeIllustrations[illustrationIndex].caption}
                  {nodeIllustrations[illustrationIndex].isGenerated && (
                    <span className="ml-1.5 rounded-full bg-accent-400/15 px-1.5 py-0.5 text-[9px] font-semibold uppercase tracking-wide text-accent-300">
                      Generada por IA
                    </span>
                  )}
                </p>
              )}
              {nodeIllustrations.length > 1 && (
                <div className="flex items-center justify-center gap-4 pb-2.5 pt-1">
                  <button
                    type="button"
                    onClick={() =>
                      setIllustrationIndex((i) => (i - 1 + nodeIllustrations.length) % nodeIllustrations.length)
                    }
                    className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-lg text-slate-300 hover:bg-white/10 hover:text-white active:bg-white/20"
                    aria-label="Ilustración anterior"
                  >
                    ‹
                  </button>
                  <span className="text-[10px] text-slate-500">
                    {illustrationIndex + 1} / {nodeIllustrations.length}
                  </span>
                  <button
                    type="button"
                    onClick={() => setIllustrationIndex((i) => (i + 1) % nodeIllustrations.length)}
                    className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-lg text-slate-300 hover:bg-white/10 hover:text-white active:bg-white/20"
                    aria-label="Siguiente ilustración"
                  >
                    ›
                  </button>
                </div>
              )}
            </div>
          ) : (
            <div className="flex flex-col items-center gap-1.5 text-slate-500">
              <ImageIcon className="h-5 w-5" />
              <p className="text-xs">Aquí puede aparecer una imagen, emoji o gráfica de ejemplo</p>
            </div>
          )}
        </div>

        {controls}
      </div>
    </div>,
    document.body
  );
}
