import { useCallback, useEffect, useRef, useState } from 'react';
import { Sparkles, X } from 'lucide-react';
import { requestTecContigoVoiceSession, searchTecContigoWeb } from './api/tecContigoVoiceApi';

type VoiceState = 'idle' | 'connecting' | 'connected' | 'listening' | 'speaking' | 'error';

/**
 * tecBot's real-time VOICE companion (2026-09-28) — the second way to talk to
 * the SAME "Tec Contigo" student agent as TecContigoAgentChat (text). Grounded
 * server-side in the exact same CONTEXT block (interests, official TEC grades,
 * Human OS/Engrams progress, assessment evidence) via
 * TecContigoVoiceSessionFunction/TecContigoContextBuilder — NOT Buddy
 * (employee onboarding voice) and NOT the old HermanaScience tutor. WebRTC
 * lifecycle modeled after BuddyVoiceModal: connects directly browser-to-Azure
 * via an ephemeral token minted server-side; the real Azure OpenAI API key
 * never reaches the browser.
 */
export function TecContigoVoiceModal({
  open,
  onClose,
  personId,
  personName,
  language,
}: {
  open: boolean;
  onClose: () => void;
  personId?: string;
  personName?: string;
  language: 'es' | 'en';
}) {
  const isEn = language === 'en';
  const [state, setState] = useState<VoiceState>('idle');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const peerConnectionRef = useRef<RTCPeerConnection | null>(null);
  const localStreamRef = useRef<MediaStream | null>(null);
  const audioElementRef = useRef<HTMLAudioElement | null>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const dataChannelRef = useRef<RTCDataChannel | null>(null);
  // Guards against the same Realtime tool call being reported twice (it can arrive via
  // both `response.output_item.done` and `response.done`) — only ever answer it once.
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

  // Answers a Realtime tool call and lets the Agent's turn continue naturally.
  const sendToolResult = useCallback((callId: string, output: string) => {
    const dataChannel = dataChannelRef.current;
    if (dataChannel?.readyState === 'open') {
      dataChannel.send(
        JSON.stringify({
          type: 'conversation.item.create',
          item: { type: 'function_call_output', call_id: callId, output },
        }),
      );
      dataChannel.send(JSON.stringify({ type: 'response.create' }));
    }
  }, []);

  // Realtime "search_the_web" tool: tecBot asked for current/external information it
  // doesn't already have grounded in the student's record — looked up server-side via
  // Perplexity Sonar, never invented.
  const handleSearchTheWeb = useCallback(
    async (callId: string, query: string) => {
      try {
        const text = await searchTecContigoWeb(query);
        sendToolResult(callId, text);
      } catch (err) {
        console.error('TecContigoVoiceModal: search_the_web failed', err);
        sendToolResult(
          callId,
          isEn
            ? 'The web search failed temporarily. Tell the student briefly you could not look that up right now and continue the conversation normally.'
            : 'La búsqueda en la web falló temporalmente. Dile brevemente al estudiante que no pudiste consultarlo ahora mismo y continúa la conversación con normalidad.',
        );
      }
    },
    [isEn, sendToolResult],
  );

  const connect = useCallback(async () => {
    setErrorMessage(null);
    setState('connecting');
    handledToolCallIdsRef.current.clear();
    try {
      const session = await requestTecContigoVoiceSession(personId, personName, language);

      const micStream = await navigator.mediaDevices.getUserMedia({
        audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
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
          return;
        }
        if (payload.type === 'input_audio_buffer.speech_started') {
          audioElementRef.current?.pause();
        }

        // Realtime function-calling: the completed tool-call item can arrive via either
        // `response.output_item.done` or as part of `response.done`'s `response.output`.
        const items =
          payload.type === 'response.output_item.done' && payload.item
            ? [payload.item as Record<string, unknown>]
            : payload.type === 'response.done'
              ? (((payload.response as Record<string, unknown> | undefined)?.output as Record<string, unknown>[] | undefined) ?? [])
              : [];
        for (const item of items) {
          if (item.type !== 'function_call' || item.name !== 'search_the_web') continue;
          const callId = item.call_id as string;
          if (handledToolCallIdsRef.current.has(callId)) continue;
          let args: { query?: string };
          try {
            args = JSON.parse((item.arguments as string) ?? '{}');
          } catch {
            continue;
          }
          if (!args.query) continue;
          handledToolCallIdsRef.current.add(callId);
          void handleSearchTheWeb(callId, args.query);
        }
      };
      dataChannel.onopen = () => {
        dataChannel.send(JSON.stringify({ type: 'response.create' }));
      };

      const offer = await peerConnection.createOffer();
      await peerConnection.setLocalDescription(offer);

      const sdpResponse = await fetch(session.RealtimeCallsUrl, {
        method: 'POST',
        body: offer.sdp,
        headers: { Authorization: `Bearer ${session.ClientSecret}`, 'Content-Type': 'application/sdp' },
      });

      if (!sdpResponse.ok) {
        throw new Error(`Voice session negotiation failed (status ${sdpResponse.status}).`);
      }

      const answerSdp = await sdpResponse.text();
      await peerConnection.setRemoteDescription({ type: 'answer', sdp: answerSdp });

      setState('connected');
    } catch (err) {
      console.error('TecContigoVoiceModal: connection failed', err);
      setErrorMessage(
        isEn
          ? 'Could not connect to tecBot. Check your microphone and try again.'
          : 'No se pudo conectar con tecBot. Verifica tu micrófono e intenta de nuevo.',
      );
      teardown();
      setState('error');
    }
  }, [personId, personName, language, isEn, monitorRemoteAudioLevel, teardown]);

  useEffect(() => {
    if (open) {
      void connect();
    } else {
      teardown();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => teardown, [teardown]);

  if (!open) return null;

  const isActive = state === 'connected' || state === 'listening' || state === 'speaking';
  const isBusy = state === 'connecting';

  const stateLabels: Record<VoiceState, string> = {
    idle: isEn ? 'Idle' : 'Inactivo',
    connecting: isEn ? 'Connecting…' : 'Conectando…',
    connected: isEn ? 'Connected' : 'Conectado',
    listening: isEn ? 'Listening…' : 'Escuchando…',
    speaking: isEn ? 'tecBot is speaking…' : 'tecBot está hablando…',
    error: isEn ? 'Connection error' : 'Error de conexión',
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4" onClick={onClose}>
      <div
        className="w-full max-w-md rounded-3xl border border-emerald-200/80 bg-white p-7 text-center shadow-2xl dark:border-emerald-400/20 dark:bg-slate-950"
        onClick={(e) => e.stopPropagation()}
      >
        <button
          type="button"
          onClick={onClose}
          aria-label={isEn ? 'Close' : 'Cerrar'}
          className="absolute right-5 top-5 flex h-8 w-8 items-center justify-center rounded-full text-slate-400 transition hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-white/10 dark:hover:text-white"
        >
          <X className="h-4 w-4" />
        </button>

        <div className="mx-auto flex h-28 w-28 items-center justify-center">
          {isBusy ? (
            <span className="h-8 w-8 animate-spin rounded-full border-2 border-emerald-500/40 border-t-emerald-500" />
          ) : (
            <img
              src="/TecBot.png"
              alt="tecBot"
              className={`h-36 w-36 object-contain transition-transform ${isActive ? 'animate-pulse' : ''} ${
                state === 'speaking' ? 'scale-110' : state === 'listening' ? 'scale-105' : 'scale-100'
              }`}
            />
          )}
        </div>

        <h3 className="mt-5 text-xl font-bold text-slate-950 dark:text-white">tecBot</h3>
        <p className="mt-1 text-sm font-semibold text-emerald-600 dark:text-emerald-300">Tu Lifelong Learning Partner</p>
        <p className="mt-1 inline-flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-emerald-600 dark:text-emerald-300">
          <Sparkles className="h-3.5 w-3.5" /> {stateLabels[state]}
        </p>

        {errorMessage && <p className="mt-3 text-sm text-red-600 dark:text-red-400">{errorMessage}</p>}

        <p className="mt-4 text-sm leading-6 text-slate-500 dark:text-white/60">
          {isEn
            ? 'Ask tecBot about your interests, your official TEC grades, your Human OS progress, or what to study next.'
            : 'Pregúntale a tecBot sobre tus intereses, tus calificaciones oficiales del Tec, tu progreso en Human OS, o qué estudiar después.'}
        </p>

        <audio ref={audioElementRef} autoPlay className="hidden" />

        <button
          type="button"
          onClick={onClose}
          className="mt-6 inline-flex items-center justify-center rounded-full border border-slate-200 px-5 py-2 text-sm font-semibold text-slate-600 transition hover:bg-slate-50 dark:border-white/15 dark:text-white/70 dark:hover:bg-white/5"
        >
          {isEn ? 'End conversation' : 'Terminar conversación'}
        </button>
      </div>
    </div>
  );
}
