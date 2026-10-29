import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Mic, PhoneOff, X } from 'lucide-react';
import { requestLabSimulationVoiceSession, type LabContent, type LabContentItem } from '../lib/api/labsApi';

/** Same flattening as Studio's editable moment list / LabSimulationPromptBuilder.cs: one
 * global sequence ordered by real Stage.Orden then Moment.OrdenSugerido — NOT the raw API
 * order (which sorts by STG_IdStage guid, unrelated to the actual "caso" sequence). */
function sortMomentsByGlobalSequence(moments: LabContentItem[], stages: LabContentItem[]): LabContentItem[] {
  const stageOrder = new Map(stages.map((s) => [String(s.IdStage), Number(s.Orden ?? 0)]));
  return [...moments].sort((a, b) => {
    const stageDiff = (stageOrder.get(String(a.StageId)) ?? 0) - (stageOrder.get(String(b.StageId)) ?? 0);
    return stageDiff !== 0 ? stageDiff : Number(a.OrdenSugerido ?? 0) - Number(b.OrdenSugerido ?? 0);
  });
}

type VoiceState = 'connecting' | 'connected' | 'listening' | 'speaking' | 'error';

const STATE_LABELS_ES: Record<VoiceState, string> = {
  connecting: 'Conectando…',
  connected: 'Conectado — puedes hablar',
  listening: 'Escuchando…',
  speaking: 'Hablando…',
  error: 'No se pudo conectar',
};

const STATE_LABELS_EN: Record<VoiceState, string> = {
  connecting: 'Connecting…',
  connected: 'Connected — you can talk',
  listening: 'Listening…',
  speaking: 'Speaking…',
  error: 'Could not connect',
};

/**
 * "Probar simulación" (2026-09-14) — Studio-only WebRTC voice preview of a Lab's AI actor,
 * for an ADMIN editing the Lab (never a real participant). Mints its own ephemeral Azure
 * OpenAI Realtime session (see LabSimulationVoiceSessionFunction.cs) and negotiates WebRTC
 * directly against Azure, exactly like human-os-web's VoiceTutorAgent.tsx. Deliberately does
 * NOT persist any transcript/turn — this is only an experience preview before publishing.
 */
export default function LabSimulationVoiceModal({
  labId,
  content,
  onClose,
}: {
  labId: string;
  /** Already-loaded Lab content — reused to show the dialogue script/questions on the
   * right side, so the admin can follow along and check the AI actor is hitting them. */
  content: LabContent;
  onClose: () => void;
}) {
  const { i18n } = useTranslation();
  const isEs = i18n.language.startsWith('es');
  const STATE_LABELS = isEs ? STATE_LABELS_ES : STATE_LABELS_EN;
  const [state, setState] = useState<VoiceState>('connecting');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actorLabel, setActorLabel] = useState<string | null>(null);

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
      setState((current) => {
        if (current === 'connecting' || current === 'error') return current;
        return isAgentSpeaking ? 'speaking' : 'listening';
      });
      animationFrameRef.current = requestAnimationFrame(tick);
    };
    tick();
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function connect() {
      try {
        const session = await requestLabSimulationVoiceSession(labId);
        if (cancelled) return;
        if (session.ActorNombre) setActorLabel(`${session.ActorNombre}${session.ActorRol ? ` — ${session.ActorRol}` : ''}`);

        const micStream = await navigator.mediaDevices.getUserMedia({
          audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
        });
        if (cancelled) {
          micStream.getTracks().forEach((track) => track.stop());
          return;
        }
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
        };
        // Real voice barge-in: the server's VAD (turn_detection: server_vad,
        // interrupt_response: true — set server-side in LabRealtimeVoiceSessionService)
        // detects the admin talking over the actor and cancels/truncates its response.
        // Without this handler the local <audio> element keeps playing whatever was
        // already buffered, so the interruption is never actually heard — pausing
        // playback immediately on this event is what makes it feel instant.
        dataChannel.onmessage = (event) => {
          let payload: Record<string, unknown>;
          try {
            payload = JSON.parse(event.data as string);
          } catch {
            return;
          }
          if (payload.type === 'input_audio_buffer.speech_started') {
            audioElementRef.current?.pause();
          } else if (payload.type === 'response.audio_transcript.delta' || payload.type === 'response.created') {
            // A new response has started (either fresh or after an interruption) — make
            // sure local playback isn't left paused from a previous barge-in.
            if (audioElementRef.current?.paused) audioElementRef.current.play().catch(() => undefined);
          }
        };

        const offer = await peerConnection.createOffer();
        await peerConnection.setLocalDescription(offer);

        const sdpResponse = await fetch(session.RealtimeCallsUrl, {
          method: 'POST',
          headers: {
            Authorization: `Bearer ${session.ClientSecret}`,
            'Content-Type': 'application/sdp',
          },
          body: offer.sdp,
        });
        if (!sdpResponse.ok) throw new Error(`No se pudo negociar la sesión de voz (${sdpResponse.status}).`);
        const answerSdp = await sdpResponse.text();
        await peerConnection.setRemoteDescription({ type: 'answer', sdp: answerSdp });

        if (!cancelled) setState('connected');
      } catch (err) {
        if (!cancelled) {
          setErrorMessage(err instanceof Error ? err.message : 'Error desconocido al conectar.');
          setState('error');
        }
      }
    }

    connect();
    return () => {
      cancelled = true;
      teardown();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [labId]);

  const handleClose = useCallback(() => {
    teardown();
    onClose();
  }, [teardown, onClose]);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 px-4">
      <div className="grid w-full max-w-4xl grid-cols-1 overflow-hidden rounded-2xl border border-white/10 bg-slate-900 shadow-2xl sm:grid-cols-[minmax(0,320px)_1fr]">
        <div className="relative p-6 text-center">
          <button
            type="button"
            onClick={handleClose}
            className="absolute right-3 top-3 rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white sm:hidden"
            aria-label="Cerrar"
          >
            <X className="h-5 w-5" />
          </button>

          <div
            className={`mx-auto flex h-24 w-24 items-center justify-center rounded-full border-2 transition-colors ${
              state === 'speaking'
                ? 'border-teal-400 bg-teal-400/20 shadow-[0_0_30px_rgba(45,212,191,0.5)]'
                : state === 'listening'
                ? 'border-amber-400 bg-amber-400/10'
                : state === 'error'
                ? 'border-red-500 bg-red-500/10'
                : 'border-slate-500 bg-slate-500/10'
            }`}
          >
            <Mic className={`h-10 w-10 ${state === 'error' ? 'text-red-400' : 'text-teal-200'}`} />
          </div>

          <h2 className="mt-5 text-lg font-semibold text-white">{isEs ? 'Probar simulación' : 'Test simulation'}</h2>
          {actorLabel && <p className="mt-1 text-sm text-teal-300">{actorLabel}</p>}
          <p className="mt-2 text-sm text-slate-400">{errorMessage ?? STATE_LABELS[state]}</p>
          <p className="mt-4 text-xs text-slate-500">
            {isEs
              ? 'Vista previa de administrador — esta conversación no se guarda ni cuenta como intento real.'
              : 'Administrator preview — this conversation is not saved and does not count as a real attempt.'}
          </p>

          <button
            type="button"
            onClick={handleClose}
            className="mt-6 inline-flex items-center gap-2 rounded-lg bg-red-500/90 px-4 py-2 text-sm font-medium text-white hover:bg-red-500"
          >
            <PhoneOff className="h-4 w-4" /> {isEs ? 'Terminar prueba' : 'End test'}
          </button>

          <audio ref={audioElementRef} autoPlay className="hidden" />
        </div>

        <div className="relative max-h-[80vh] overflow-y-auto border-t border-white/10 bg-slate-950/40 p-5 sm:max-h-[85vh] sm:border-l sm:border-t-0">
          <button
            type="button"
            onClick={handleClose}
            className="absolute right-3 top-3 hidden rounded-full p-1 text-slate-400 hover:bg-white/10 hover:text-white sm:block"
            aria-label={isEs ? 'Cerrar' : 'Close'}
          >
            <X className="h-5 w-5" />
          </button>
          <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-400">
            {isEs ? 'Guion de la conversación' : 'Conversation script'} ({content.Moments.length})
          </h3>
          <p className="mt-1 text-xs text-slate-500">
            {isEs
              ? 'Sigue estas jugadas mientras hablas para ver si el actor de IA las está introduciendo bien.'
              : 'Follow these beats while you talk to see if the AI actor is bringing them up correctly.'}
          </p>
          {content.Scenarios[0] && (
            <div className="mt-4 rounded-lg border border-white/10 bg-slate-900/60 p-3">
              <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {isEs ? 'Situación de la reunión' : 'Meeting situation'}
              </p>
              <p className="mt-1 text-sm font-medium text-white">{String(content.Scenarios[0].Nombre ?? '')}</p>
              {Boolean(content.Scenarios[0].Descripcion) && (
                <p className="mt-1 whitespace-pre-wrap text-xs leading-5 text-slate-300">
                  {String(content.Scenarios[0].Descripcion)}
                </p>
              )}
              {Boolean(content.Scenarios[0].ContextoParticipante) && (
                <p className="mt-2 whitespace-pre-wrap text-xs leading-5 text-slate-400">
                  <span className="font-semibold text-slate-300">{isEs ? 'Tu contexto: ' : 'Your context: '}</span>
                  {String(content.Scenarios[0].ContextoParticipante)}
                </p>
              )}
            </div>
          )}
          <div className="mt-4 space-y-3">
            {content.Moments.length === 0 ? (
              <p className="text-sm text-slate-500">{isEs ? 'Este Lab no tiene preguntas/intervenciones registradas.' : 'This Lab has no registered questions/interventions.'}</p>
            ) : (
              sortMomentsByGlobalSequence(content.Moments, content.Stages).map((item, index) => {
                const nombre = String(item.Nombre ?? '').trim();
                const skill = content.SkillsByName[nombre.toLowerCase()];
                return (
                  <article
                    key={String(item.IdExpectedMoment ?? index)}
                    className="rounded-lg border border-teal-400/20 bg-teal-400/5 p-3"
                  >
                    <div className="flex items-center justify-between gap-2">
                      <p className="text-sm font-semibold text-white">
                        {index + 1}. {String(item.Nombre ?? item.Codigo ?? (isEs ? 'Pregunta' : 'Question'))}
                      </p>
                      {Boolean(item.EsCritico) && <span className="text-xs text-amber-300">{isEs ? 'Crítica' : 'Critical'}</span>}
                    </div>
                    {skill && (
                      <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                        <span className="rounded-full bg-purple-500/15 px-2 py-0.5 text-[11px] font-medium text-purple-200">
                          {skill.SkillName}
                        </span>
                      </div>
                    )}
                    <p className="mt-2 text-[11px] font-semibold uppercase tracking-wide text-teal-300">
                      {isEs ? 'Intervención del actor' : "Actor's line"}
                    </p>
                    <p className="mt-0.5 whitespace-pre-wrap text-xs leading-5 text-slate-200">
                      {String(item.Trigger ?? (isEs ? 'Sin pregunta registrada.' : 'No question registered.'))}
                    </p>
                    <p className="mt-2 text-[11px] font-semibold uppercase tracking-wide text-teal-300">
                      {isEs ? 'Qué debe lograr el participante' : 'What the participant must achieve'}
                    </p>
                    <p className="mt-0.5 whitespace-pre-wrap text-xs leading-5 text-slate-300">
                      {String(item.IntencionEsperada ?? (isEs ? 'Sin intención registrada.' : 'No intent registered.'))}
                    </p>
                    {Boolean(item.RespuestaEjemplar) && (
                      <>
                        <p className="mt-2 text-[11px] font-semibold uppercase tracking-wide text-teal-300">
                          {isEs ? 'Respuesta ejemplar' : 'Exemplary response'}
                        </p>
                        <p className="mt-0.5 whitespace-pre-wrap text-xs leading-5 text-slate-300">
                          {String(item.RespuestaEjemplar)}
                        </p>
                      </>
                    )}
                  </article>
                );
              })
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
