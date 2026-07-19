import { useState, useEffect, useRef } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import StepIndicator from '@components/StepIndicator'
import TutorGuidePanel from '@components/TutorGuidePanel'
import CourseSidebar from '@components/CourseSidebar'
import { useSpeech } from '@/hooks/useSpeech'
import IntroductionStep from './steps/IntroductionStep'
import RecallStep from './steps/RecallStep'
import InitialAnswerStep from './steps/InitialAnswerStep'
import InstructionStep from './steps/InstructionStep'
import EvidenceStep from './steps/EvidenceStep'
import AssessmentStep from './steps/AssessmentStep'
import ReflectionStep from './steps/ReflectionStep'
import CompletedScreen from './components/CompletedScreen'
import {
  RuntimeStage,
  type RuntimeTurnResponse,
  StudentEvidenceKind,
  EvidenceAssistanceLevel,
  type SubmitRuntimeEvidenceRequest,
  type SubmitRuntimeEvidencePartRequest,
  parseRuntimeTurnResponse,
} from '@/types/runtime'

// Ordered labels for the top stepper AND the sidebar Tutor panel (redesign
// 2026-07-16 — bare numbered circles with no titles were confusing; every
// stage now has a short, real label instead). REORDERED 2026-07-16: teach
// (Instruction) now comes BEFORE Recall/Prediction, not after — explicit
// correction: "primero necesitamos enseñar paso a paso, preguntar que
// aprendio recall". Must match the backend's RuntimeSessionWorkflowFactory
// graph order exactly.
const TOTAL_VISIBLE_STEPS = 6
const STAGE_TO_STEP_NUMBER: Record<RuntimeStage, number> = {
  [RuntimeStage.ModuleStarted]: 1,
  [RuntimeStage.Instruction]: 2,
  [RuntimeStage.ChapterTeaching]: 2,
  [RuntimeStage.ChapterRecall]: 3,
  [RuntimeStage.ChapterPrediction]: 4,
  [RuntimeStage.ChapterMiniPractice]: 4,
  [RuntimeStage.RecallRequired]: 3,
  [RuntimeStage.PredictionRequired]: 4,
  [RuntimeStage.LearnerProduction]: 5,
  [RuntimeStage.Assessment]: 5,
  [RuntimeStage.Reflection]: 6,
  [RuntimeStage.Completed]: 6,
  [RuntimeStage.RequiresRevision]: 6,
}
const STEP_LABELS = ['Introducción', 'Guía', 'Recordar', 'Predicción', 'Práctica', 'Reflexión']
const STAGE_LABELS: Record<RuntimeStage, string> = {
  [RuntimeStage.ModuleStarted]: 'Introducción',
  [RuntimeStage.Instruction]: 'Guía',
  [RuntimeStage.ChapterTeaching]: 'Guía',
  [RuntimeStage.ChapterRecall]: 'Recordar',
  [RuntimeStage.ChapterPrediction]: 'Predicción',
  [RuntimeStage.ChapterMiniPractice]: 'Mini-práctica',
  [RuntimeStage.RecallRequired]: 'Recordar',
  [RuntimeStage.PredictionRequired]: 'Predicción',
  [RuntimeStage.LearnerProduction]: 'Práctica',
  [RuntimeStage.Assessment]: 'Evaluación',
  [RuntimeStage.Reflection]: 'Reflexión',
  [RuntimeStage.Completed]: 'Completado',
  [RuntimeStage.RequiresRevision]: 'Requiere revisión',
}

// Real capability data — "Preparar café en prensa francesa correctamente"
// (fetched from GET /api/capabilities and /api/capabilities/{id}/content).
// Used only to start a brand-new real Runtime session (via
// POST /people/{personId}/modules/{moduleId}/sessions) when the URL points
// to a sessionId that doesn't exist yet. Once started, ALL content comes
// from the real backend/TutorAgent — no client-side mock text.
const DEMO_PERSON_ID = '9e2a95d7-4d34-4a14-a7ee-0e4d5eb72b84'
const DEMO_CAPABILITY_MODULE_ID = '9378a534-7db3-4201-9780-106de74df78e' // Foundation, módulo 1: "Modelo mental: etapas de la prensa francesa"
// RuntimeTurnResponse doesn't currently return capability metadata, so we
// attach the real title/code (from the same capability the module above
// belongs to) client-side purely for header display — never used as
// pedagogical content.
const DEMO_CAPABILITY_TITLE = 'Preparar café en prensa francesa correctamente'
const DEMO_CAPABILITY_CODE = 'preparar-café-en-prensa-francesa-correctamente-0b26cddd'

export default function SessionRuntime() {
  const { sessionId } = useParams<{ sessionId: string }>()
  const navigate = useNavigate()
  
  // Session state from backend
  const [session, setSession] = useState<RuntimeTurnResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [voiceEnabled, setVoiceEnabled] = useState(true)
  const { speak, stopSpeaking, isSpeaking, speechSupported } = useSpeech()

  // API config
  const apiBaseUrl = 'http://localhost:7071/api'
  const sessionIdRef = useRef(sessionId || '')

  /**
   * Start a brand-new REAL Runtime session against the backend (Studio-
   * published CapabilityModule + real TutorAgent), then replace the URL
   * with the real runtimeSessionId so refreshes rehydrate correctly.
   */
  const startNewRealSession = async () => {
    const response = await fetch(
      `${apiBaseUrl}/people/${DEMO_PERSON_ID}/modules/${DEMO_CAPABILITY_MODULE_ID}/sessions`,
      { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }
    )

    if (!response.ok) {
      throw new Error(`Failed to start a new session: ${response.statusText}`)
    }

    const data = parseRuntimeTurnResponse(await response.json())
    data.capabilityTitle = data.capabilityTitle ?? DEMO_CAPABILITY_TITLE
    data.capabilityCode = data.capabilityCode ?? DEMO_CAPABILITY_CODE
    sessionIdRef.current = data.runtimeSessionId
    setSession(data)
    setError(null)
    navigate(`/session/${data.runtimeSessionId}`, { replace: true })
  }

  /**
   * Starts a fresh session for the SAME module the learner just finished
   * (fixed 2026-07-17 — explicit user request after seeing a "Requires
   * Revision" outcome: "forzar a comenzar la lección sería buenísimo").
   * Unlike startNewRealSession (which always uses the hardcoded demo
   * module), this restarts whatever module the CURRENT terminal session
   * belongs to.
   */
  const handleRestartModule = async () => {
    if (!session?.capabilityModuleId) return

    try {
      setIsSubmitting(true)
      const response = await fetch(
        `${apiBaseUrl}/people/${DEMO_PERSON_ID}/modules/${session.capabilityModuleId}/sessions`,
        { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }
      )

      if (!response.ok) {
        throw new Error(`Failed to restart the module: ${response.statusText}`)
      }

      const data = parseRuntimeTurnResponse(await response.json())
      data.capabilityTitle = data.capabilityTitle ?? session.capabilityTitle
      data.capabilityCode = data.capabilityCode ?? session.capabilityCode
      sessionIdRef.current = data.runtimeSessionId
      setSession(data)
      setError(null)
      navigate(`/session/${data.runtimeSessionId}`, { replace: true })
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      console.error('Failed to restart module:', errorMessage)
      setError(errorMessage)
    } finally {
      setIsSubmitting(false)
    }
  }

  /**
   * Load session state from the real backend. If the sessionId in the URL
   * doesn't exist yet (404), starts a brand-new real session instead of
   * fabricating any mock content.
   */
  const loadSession = async () => {
    if (!sessionId) {
      setError('No session ID provided')
      setIsLoading(false)
      return
    }

    try {
      setIsLoading(true)
      const response = await fetch(`${apiBaseUrl}/sessions/${sessionId}`)

      if (response.status === 404) {
        await startNewRealSession()
        return
      }

      if (!response.ok) {
        throw new Error(`Failed to load session: ${response.statusText}`)
      }

      const data = parseRuntimeTurnResponse(await response.json())
      data.capabilityTitle = data.capabilityTitle ?? DEMO_CAPABILITY_TITLE
      data.capabilityCode = data.capabilityCode ?? DEMO_CAPABILITY_CODE
      setSession(data)
      sessionIdRef.current = data.runtimeSessionId
      setError(null)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      console.error('Failed to load session from backend:', errorMessage)
      setError(errorMessage)
    } finally {
      setIsLoading(false)
    }
  }

  // Load session on mount
  useEffect(() => {
    loadSession()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId])

  // Auto-narrate the Tutor's message for the current turn (fixed
  // 2026-07-16 — Capa 1 voice: browser-native Web Speech API, no backend
  // changes). Re-runs whenever the stage/message actually changes (not on
  // every render), and is skipped for terminal screens (CompletedScreen
  // reads message itself, no turn-by-turn narration needed there).
  useEffect(() => {
    if (!voiceEnabled || !session || session.isTerminal) return
    speak(session.message)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.stage, session?.message, voiceEnabled])

  /**
   * POST /sessions/{id}/introduction-ack → transitions ModuleStarted → RecallRequired
   */
  const handleIntroductionAck = async () => {
    if (!sessionIdRef.current) return

    try {
      setIsSubmitting(true)
      const response = await fetch(
        `${apiBaseUrl}/sessions/${sessionIdRef.current}/introduction-ack`,
        { method: 'POST' }
      )

      if (!response.ok) {
        throw new Error(`Failed to acknowledge introduction: ${response.statusText}`)
      }

      const nextStage = parseRuntimeTurnResponse(await response.json())
      nextStage.capabilityTitle = nextStage.capabilityTitle ?? session?.capabilityTitle
      nextStage.capabilityCode = nextStage.capabilityCode ?? session?.capabilityCode
      setSession(nextStage)
      setError(null)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      console.error('Failed to acknowledge introduction:', errorMessage)
      setError(errorMessage)
    } finally {
      setIsSubmitting(false)
    }
  }

  /**
   * POST /sessions/{id}/instruction-ack → transitions Instruction → LearnerProduction
   */
  const handleInstructionAck = async () => {
    if (!sessionIdRef.current) return

    try {
      setIsSubmitting(true)
      const response = await fetch(
        `${apiBaseUrl}/sessions/${sessionIdRef.current}/instruction-ack`,
        { method: 'POST' }
      )

      if (!response.ok) {
        throw new Error(`Failed to acknowledge instruction: ${response.statusText}`)
      }

      const nextStage = parseRuntimeTurnResponse(await response.json())
      nextStage.capabilityTitle = nextStage.capabilityTitle ?? session?.capabilityTitle
      nextStage.capabilityCode = nextStage.capabilityCode ?? session?.capabilityCode
      setSession(nextStage)
      setError(null)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      console.error('Failed to acknowledge instruction:', errorMessage)
      setError(errorMessage)
    } finally {
      setIsSubmitting(false)
    }
  }

  /**
   * POST /sessions/{id}/chapter-ack → transitions ChapterTeaching →
   * ChapterPrediction (primary-weight chapter) or ChapterRecall (fixed
   * 2026-07-16 — phase-based Chapters loop).
   */
  const handleChapterAck = async () => {
    if (!sessionIdRef.current) return

    try {
      setIsSubmitting(true)
      const response = await fetch(
        `${apiBaseUrl}/sessions/${sessionIdRef.current}/chapter-ack`,
        { method: 'POST' }
      )

      if (!response.ok) {
        throw new Error(`Failed to acknowledge chapter: ${response.statusText}`)
      }

      const nextStage = parseRuntimeTurnResponse(await response.json())
      nextStage.capabilityTitle = nextStage.capabilityTitle ?? session?.capabilityTitle
      nextStage.capabilityCode = nextStage.capabilityCode ?? session?.capabilityCode
      setSession(nextStage)
      setError(null)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      console.error('Failed to acknowledge chapter:', errorMessage)
      setError(errorMessage)
    } finally {
      setIsSubmitting(false)
    }
  }

  /**
   * POST /sessions/{id}/chapter-mini-practice-ack → transitions
   * ChapterMiniPractice → ChapterRecall (fixed 2026-07-16).
   */
  const handleChapterMiniPracticeAck = async () => {
    if (!sessionIdRef.current) return

    try {
      setIsSubmitting(true)
      const response = await fetch(
        `${apiBaseUrl}/sessions/${sessionIdRef.current}/chapter-mini-practice-ack`,
        { method: 'POST' }
      )

      if (!response.ok) {
        throw new Error(`Failed to acknowledge mini-practice: ${response.statusText}`)
      }

      const nextStage = parseRuntimeTurnResponse(await response.json())
      nextStage.capabilityTitle = nextStage.capabilityTitle ?? session?.capabilityTitle
      nextStage.capabilityCode = nextStage.capabilityCode ?? session?.capabilityCode
      setSession(nextStage)
      setError(null)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      console.error('Failed to acknowledge mini-practice:', errorMessage)
      setError(errorMessage)
    } finally {
      setIsSubmitting(false)
    }
  }

  /**
   * POST /sessions/{id}/evidence → submits whatever the CURRENT pending
   * stage requires (Recall/Prediction/LearnerProduction/Reflection) and
   * advances to the next real stage returned by the TutorAgent/Assessment.
   */
  const handleEvidenceSubmit = async (evidenceText: string, forceAdvance?: boolean) => {
    if (!sessionIdRef.current) return

    try {
      setIsSubmitting(true)

      const request: SubmitRuntimeEvidenceRequest = {
        parts: [
          {
            kind: StudentEvidenceKind.Text,
            text: evidenceText,
          } as SubmitRuntimeEvidencePartRequest,
        ],
        assistanceLevel: EvidenceAssistanceLevel.Unaided,
        capturedBeforeAssistance: true,
        forceAdvance,
      }

      const response = await fetch(
        `${apiBaseUrl}/sessions/${sessionIdRef.current}/evidence`,
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request),
        }
      )

      if (!response.ok) {
        throw new Error(`Failed to submit evidence: ${response.statusText}`)
      }

      const nextStage = parseRuntimeTurnResponse(await response.json())
      nextStage.capabilityTitle = nextStage.capabilityTitle ?? session?.capabilityTitle
      nextStage.capabilityCode = nextStage.capabilityCode ?? session?.capabilityCode
      setSession(nextStage)
      setError(null)
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error'
      console.error('Failed to submit evidence:', errorMessage)
      setError(errorMessage)
    } finally {
      setIsSubmitting(false)
    }
  }

  // ============================================================================
  // RENDER
  // ============================================================================

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-slate-50">
        <div className="text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-indigo-600 mb-4 mx-auto"></div>
          <p className="text-gray-500 text-sm">Cargando sesión...</p>
        </div>
      </div>
    )
  }

  if (error || !session) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-slate-50">
        <div className="max-w-md text-center">
          <h2 className="text-xl font-semibold text-red-600 mb-2">Error</h2>
          <p className="text-gray-600 text-sm mb-4">{error || 'Sesión no encontrada'}</p>
          <button
            onClick={loadSession}
            className="px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 transition-colors"
          >
            Reintentar
          </button>
        </div>
      </div>
    )
  }

  const stepNumber = STAGE_TO_STEP_NUMBER[session.stage]
  const isTerminal = session.isTerminal

  // Render completed/terminal screen
  if (isTerminal && (session.stage === RuntimeStage.Completed || session.stage === RuntimeStage.RequiresRevision)) {
    return (
      <CompletedScreen
        assessment={session.finalAssessment}
        message={session.message}
        onRestartModule={handleRestartModule}
        isRestarting={isSubmitting}
      />
    )
  }

  return (
    <div className="min-h-screen bg-slate-50">
      {/* Header */}
      <header className="bg-slate-900">
        <div className="max-w-7xl mx-auto px-6 py-6 flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-semibold text-white mb-1">
              {session.capabilityTitle || 'Sesión de aprendizaje'}
            </h1>
            {session.capabilityCode && (
              <p className="text-slate-400 text-xs">Código: {session.capabilityCode}</p>
            )}
          </div>
          <div className="flex items-center gap-2 shrink-0">
            {speechSupported && (
              <button
                onClick={() => {
                  if (voiceEnabled) stopSpeaking()
                  setVoiceEnabled((v) => !v)
                }}
                title={voiceEnabled ? 'Silenciar narración' : 'Activar narración de voz'}
                className={`px-3 py-1.5 text-xs font-medium border rounded-lg transition-colors ${
                  voiceEnabled
                    ? 'text-slate-300 border-slate-700 hover:bg-slate-800'
                    : 'text-slate-500 border-slate-800 bg-slate-800/50'
                }`}
              >
                {voiceEnabled ? (isSpeaking ? '🔊 Narrando…' : '🔊 Voz activa') : '🔇 Voz apagada'}
              </button>
            )}
            <button
              onClick={loadSession}
              className="px-3 py-1.5 text-xs font-medium text-slate-300 border border-slate-700 rounded-lg hover:bg-slate-800 transition-colors"
            >
              Actualizar
            </button>
          </div>
        </div>
      </header>

      <div className="max-w-7xl mx-auto px-6 py-8">
        {/* Top-down progress stepper */}
        <div className="mb-8">
          <StepIndicator currentStep={stepNumber} totalSteps={TOTAL_VISIBLE_STEPS} labels={STEP_LABELS} />
        </div>

        {/* 3 columns (fixed 2026-07-16): 1) course menu, 2) Agente
            interactuando (TutorGuidePanel), 3) el tutor texto (main step
            content). Stacks vertically below lg for responsive layouts. */}
        <div className="flex flex-col lg:flex-row gap-6 items-start">
          <CourseSidebar
            apiBaseUrl={apiBaseUrl}
            capabilityId={session.capabilityId}
            currentModuleId={session.capabilityModuleId}
            chapterTitles={session.allChapterTitles ?? []}
            currentChapterIndex={session.chapterIndex}
          />

          <div className="w-full lg:w-80 shrink-0">
            <TutorGuidePanel
              stageLabel={STAGE_LABELS[session.stage]}
              message={session.message}
              isSpeaking={isSpeaking}
            />
          </div>

          <div className="flex-1 min-w-0">
            {/* Chapter progress banner (fixed 2026-07-16) — shown for every
                Chapter* stage, tells the learner where they are in the
                phase-based cycle (e.g. "Capítulo 2 de 5: Sustitución"). */}
            {session.totalChapters !== undefined && session.chapterIndex !== undefined && (
              <div className="mb-4 flex items-center justify-between rounded-lg bg-indigo-50 border border-indigo-200 px-4 py-2">
                <span className="text-sm font-medium text-indigo-900">
                  Capítulo {session.chapterIndex + 1} de {session.totalChapters}
                  {session.chapterTitle ? `: ${session.chapterTitle}` : ''}
                </span>
              </div>
            )}

            {/* ModuleStarted → IntroductionStep */}
            {session.stage === RuntimeStage.ModuleStarted && (
              <IntroductionStep
                onContinue={handleIntroductionAck}
                isSubmitting={isSubmitting}
              />
            )}

            {/* RecallRequired → RecallStep (module-wide, cumulative, runs
                once at the end of every chapter) */}
            {session.stage === RuntimeStage.RecallRequired && (
              <RecallStep
                onSubmit={handleEvidenceSubmit}
                isSubmitting={isSubmitting}
                attemptNumber={session.attemptNumber}
                totalAttempts={session.totalAttempts}
                lastAccuracyPercentage={session.lastAccuracyPercentage}
              />
            )}

            {/* ChapterRecall → RecallStep (lighter, per-chapter) */}
            {session.stage === RuntimeStage.ChapterRecall && (
              <RecallStep
                onSubmit={handleEvidenceSubmit}
                isSubmitting={isSubmitting}
                attemptNumber={session.attemptNumber}
                totalAttempts={session.totalAttempts}
                lastAccuracyPercentage={session.lastAccuracyPercentage}
              />
            )}

            {/* PredictionRequired → InitialAnswerStep */}
            {session.stage === RuntimeStage.PredictionRequired && (
              <InitialAnswerStep
                onSubmit={handleEvidenceSubmit}
                isSubmitting={isSubmitting}
              />
            )}

            {/* ChapterPrediction → InitialAnswerStep (only the module's
                one primary-weight chapter reaches this) */}
            {session.stage === RuntimeStage.ChapterPrediction && (
              <InitialAnswerStep
                onSubmit={handleEvidenceSubmit}
                isSubmitting={isSubmitting}
              />
            )}

            {/* Instruction → InstructionStep (legacy whole-script path) */}
            {session.stage === RuntimeStage.Instruction && (
              <InstructionStep
                message={session.message}
                onContinue={handleInstructionAck}
                isSubmitting={isSubmitting}
              />
            )}

            {/* ChapterTeaching → InstructionStep (phase-based path) */}
            {session.stage === RuntimeStage.ChapterTeaching && (
              <InstructionStep
                message={session.message}
                onContinue={handleChapterAck}
                isSubmitting={isSubmitting}
              />
            )}

            {/* ChapterMiniPractice → InstructionStep (presentation-only,
                the learner works the exercise off-app and acknowledges) */}
            {session.stage === RuntimeStage.ChapterMiniPractice && (
              <InstructionStep
                message={session.message}
                onContinue={handleChapterMiniPracticeAck}
                isSubmitting={isSubmitting}
              />
            )}

            {/* LearnerProduction → EvidenceStep */}
            {session.stage === RuntimeStage.LearnerProduction && (
              <EvidenceStep
                onSubmit={handleEvidenceSubmit}
                isSubmitting={isSubmitting}
              />
            )}

            {/* Assessment → AssessmentStep (transient in practice — the
                workflow computes this internally and normally routes straight
                to Reflection/RequiresRevision, but handled defensively here) */}
            {session.stage === RuntimeStage.Assessment && (
              <AssessmentStep
                assessment={session.finalAssessment}
                onContinue={loadSession}
                isSubmitting={isSubmitting}
              />
            )}

            {/* Reflection → ReflectionStep */}
            {session.stage === RuntimeStage.Reflection && (
              <ReflectionStep
                onSubmit={handleEvidenceSubmit}
                isSubmitting={isSubmitting}
              />
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

