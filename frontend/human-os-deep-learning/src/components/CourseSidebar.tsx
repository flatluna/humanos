import { useEffect, useState } from 'react'
import { createPortal } from 'react-dom'

interface CapabilityModuleSummary {
  CapabilityModuleId: string
  SortOrder: number
  Title: string
}

interface CapabilityLevelSummary {
  CapabilityLevelId: string
  Layer: string
  Title: string
  Modules: CapabilityModuleSummary[]
}

interface CapabilityContentSummary {
  CapabilityId: string
  Name: string
  Levels: CapabilityLevelSummary[]
}

interface ModuleChapterSummary {
  CapabilityModuleChapterId: string
  SortOrder: number
  Title: string
  TeachingContent: string
  IsPrimaryWeight: boolean
}

interface ModuleChapterSummaryResponse {
  CapabilityModuleId: string
  ModuleTitle: string
  Chapters: ModuleChapterSummary[]
}

const LAYER_ICON: Record<string, string> = {
  Foundation: '🟦',
  Exploration: '🟦',
  Mastery: '🟦',
}

interface CourseSidebarProps {
  apiBaseUrl: string
  capabilityId?: string
  currentModuleId?: string
  chapterTitles: string[]
  currentChapterIndex?: number
  /** Icon-only collapsed mode (fixed 2026-07-16 — hamburger toggle) —
   * hides titles/chapter sub-list, keeps only level icons + a dot marking
   * the current module, so the column shrinks to a narrow rail. */
  collapsed?: boolean
}

/**
 * Course-wide sidebar (fixed 2026-07-16 — "Paso 2/3" of the course-player
 * redesign): fetches the full Levels→Modules structure ONCE per
 * capability (GET /capabilities/{id}/content) and renders it grouped by
 * level, highlighting the module currently in session. The active
 * module's own Chapters (phase-based sub-cycle) render as an indented
 * sub-list right under it, highlighting the current chapter.
 */
export default function CourseSidebar({
  apiBaseUrl,
  capabilityId,
  currentModuleId,
  chapterTitles,
  currentChapterIndex,
  collapsed = false,
}: CourseSidebarProps) {
  const [content, setContent] = useState<CapabilityContentSummary | null>(null)
  // Read-only chapter "review" (added 2026-07-16 — "regresar a capítulo
  // anterior"): clicking any chapter in the current module's sub-list
  // fetches that chapter's raw TeachingContent from the DB and shows it
  // in a modal. Does NOT touch the live Runtime session/progress.
  const [reviewChapter, setReviewChapter] = useState<ModuleChapterSummary | null>(null)
  const [reviewLoading, setReviewLoading] = useState(false)
  const [reviewError, setReviewError] = useState(false)

  useEffect(() => {
    if (!capabilityId) return

    let cancelled = false
    fetch(`${apiBaseUrl}/capabilities/${capabilityId}/content`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (!cancelled) setContent(data)
      })
      .catch(() => {
        if (!cancelled) setContent(null)
      })

    return () => {
      cancelled = true
    }
  }, [apiBaseUrl, capabilityId])

  const handleReviewChapter = (index: number) => {
    if (!currentModuleId) return
    setReviewLoading(true)
    setReviewError(false)
    setReviewChapter(null)
    fetch(`${apiBaseUrl}/modules/${currentModuleId}/chapters`)
      .then((res) => (res.ok ? res.json() : null))
      .then((data: ModuleChapterSummaryResponse | null) => {
        const chapter = data?.Chapters.find((c) => c.SortOrder === index) ?? null
        if (!chapter) {
          setReviewError(true)
          return
        }
        setReviewChapter(chapter)
      })
      .catch(() => setReviewError(true))
      .finally(() => setReviewLoading(false))
  }

  if (!capabilityId) return null

  const totalModules = content?.Levels.reduce((acc, l) => acc + l.Modules.length, 0) ?? 0

  if (collapsed) {
    return (
      <aside className="w-14 shrink-0 bg-white border border-gray-200 rounded-lg p-2 lg:sticky lg:top-6 flex flex-col items-center gap-3 py-4">
        {content?.Levels.map((level) => (
          <div key={level.CapabilityLevelId} className="flex flex-col items-center gap-1.5" title={level.Layer}>
            <span className="text-lg">{LAYER_ICON[level.Layer] ?? '🟦'}</span>
            {level.Modules.map((module) => (
              <span
                key={module.CapabilityModuleId}
                title={module.Title}
                className={`w-2 h-2 rounded-full ${
                  module.CapabilityModuleId === currentModuleId ? 'bg-indigo-600' : 'bg-gray-300'
                }`}
              />
            ))}
          </div>
        ))}
      </aside>
    )
  }

  return (
    <aside className="w-full lg:w-72 shrink-0 bg-white border border-gray-200 rounded-lg p-4 lg:sticky lg:top-6 lg:max-h-[calc(100vh-3rem)] lg:overflow-y-auto">
      {content ? (
        <>
          <p className="text-xs text-gray-400 mb-3">
            {content.Levels.length} niveles · {totalModules} módulos
          </p>
          <nav className="space-y-4">
            {content.Levels.map((level) => (
              <div key={level.CapabilityLevelId}>
                <p className="text-xs font-semibold text-blue-800 mb-1.5">
                  {LAYER_ICON[level.Layer] ?? '🟦'} {level.Layer}
                </p>
                <ul className="space-y-0.5">
                  {level.Modules.map((module) => {
                    const isCurrent = module.CapabilityModuleId === currentModuleId
                    return (
                      <li key={module.CapabilityModuleId}>
                        <div
                          className={`text-sm px-2 py-1.5 rounded-md ${
                            isCurrent ? 'bg-indigo-50 text-indigo-900 font-medium' : 'text-gray-600'
                          }`}
                        >
                          {module.SortOrder + 1}. {module.Title}
                        </div>

                        {/* Chapter sub-list — only under the CURRENT module.
                            Clickable (fixed 2026-07-16 — "regresar a
                            capítulo anterior"): opens a read-only review
                            modal with that chapter's teaching content,
                            without touching live session progress. */}
                        {isCurrent && chapterTitles.length > 0 && (
                          <ul className="ml-3 mt-1 mb-1 space-y-0.5 border-l-2 border-indigo-200 pl-2">
                            {chapterTitles.map((title, index) => (
                              <li key={`${title}-${index}`}>
                                <button
                                  type="button"
                                  onClick={() => handleReviewChapter(index)}
                                  title="Revisar este capítulo"
                                  className={`w-full text-left text-xs px-2 py-1 rounded hover:bg-indigo-50 hover:text-indigo-800 transition-colors ${
                                    index === currentChapterIndex
                                      ? 'bg-indigo-100 text-indigo-900 font-medium'
                                      : 'text-gray-500'
                                  }`}
                                >
                                  {index !== currentChapterIndex && '↩ '}
                                  {title}
                                </button>
                              </li>
                            ))}
                          </ul>
                        )}
                      </li>
                    )
                  })}
                </ul>
              </div>
            ))}
          </nav>
        </>
      ) : (
        <p className="text-sm text-gray-400">Cargando estructura del curso…</p>
      )}

      {/* Review modal (fixed 2026-07-16) — read-only, closes without
          affecting the live Runtime session. Rendered via a portal into
          document.body (fixed again same day — nested inside this
          <aside>'s lg:overflow-y-auto/lg:sticky scroll container, a plain
          `position:fixed` child was being clipped/hidden behind other
          content instead of overlaying the whole viewport). */}
      {(reviewLoading || reviewError || reviewChapter) &&
        createPortal(
          <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 px-4"
            onClick={() => {
              setReviewChapter(null)
              setReviewError(false)
            }}
          >
            <div
              className="bg-white rounded-xl shadow-xl max-w-xl w-full max-h-[80vh] overflow-y-auto"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
                <p className="text-sm font-semibold text-gray-900">
                  {reviewLoading ? 'Cargando…' : reviewChapter ? `📖 ${reviewChapter.Title}` : 'No se pudo cargar'}
                </p>
                <button
                  type="button"
                  onClick={() => {
                    setReviewChapter(null)
                    setReviewError(false)
                  }}
                  className="text-gray-400 hover:text-gray-700 text-lg leading-none"
                >
                  ✕
                </button>
              </div>
              <div className="px-5 py-5">
                {reviewLoading && <p className="text-sm text-gray-400">Cargando capítulo…</p>}
                {reviewError && !reviewLoading && (
                  <p className="text-sm text-red-500">No se pudo cargar el contenido de este capítulo.</p>
                )}
                {reviewChapter && !reviewLoading && (
                  <p className="text-sm text-gray-700 leading-relaxed whitespace-pre-line">
                    {reviewChapter.TeachingContent}
                  </p>
                )}
              </div>
            </div>
          </div>,
          document.body
        )}
    </aside>
  )
}

