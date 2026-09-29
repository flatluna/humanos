import { ArrowLeft, Award, BookOpen, Building2, CheckCircle2, ExternalLink, FileText, Newspaper, RefreshCw, Sparkles, UserCheck } from 'lucide-react';
import { Link } from 'react-router';
import { Card } from '@/components/ui/Card';
import { useI18n } from '@/features/capabilities/i18n/useI18n';
import { useGrowWithUsPlanStore } from '@/store/useGrowWithUsPlanStore';
import { GrowWithUsSavedSearchesPanel } from './GrowWithUsSavedSearchesPanel';
import { GrowWithUsSavedSearchesToolbar } from './GrowWithUsSavedSearchesToolbar';

/**
 * "Advance Your Knowledge" (2026-09-27 rewrite) — shows the AI-generated
 * "Grow With Us" growth plan (GrowWithUsPlanAgent) produced from the
 * learner's own free-text prompt via GrowWithUsPromptModal (opened from
 * the main side menu's "Grow With Us" item). Replaces the previous static
 * "coming soon" skills catalog + Stay Fresh stale-engram grid entirely —
 * see useGrowWithUsPlanStore for the in-memory plan state.
 */
export function AdvanceYourKnowledgePage() {
  const { language } = useI18n();
  const isEn = language === 'en';
  const { plan, openModal } = useGrowWithUsPlanStore();

  return (
    <div className="min-h-screen bg-[#f5f7fb] pb-16 dark:bg-[#05060a]">
      <main className="mx-auto max-w-7xl px-6 pt-8 sm:pt-12">
        <Link to="/" className="inline-flex items-center gap-2 text-sm font-semibold text-slate-500 transition hover:text-blue-600 dark:text-white/60 dark:hover:text-blue-300">
          <ArrowLeft className="h-4 w-4" aria-hidden="true" />
          {isEn ? 'Back to your journey' : 'Volver a tu recorrido'}
        </Link>

        <div className="mt-6">
          <GrowWithUsSavedSearchesToolbar />
        </div>

        <div className="mt-2 grid grid-cols-1 gap-6 lg:grid-cols-[280px_1fr]">
          <div className="lg:sticky lg:top-8 lg:self-start">
            <GrowWithUsSavedSearchesPanel />
          </div>

          <div>
        <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-amber-500 via-orange-600 to-rose-600 p-7 text-white shadow-xl shadow-amber-900/20 sm:p-10">
          <div className="flex flex-wrap items-start justify-between gap-6">
            <div className="max-w-3xl">
              <div className="flex items-center gap-2 text-sm font-semibold text-amber-100">
                <Sparkles className="h-4 w-4" aria-hidden="true" />
                {isEn ? 'Always' : 'Siempre'}
              </div>
              <h1 className="mt-4 text-3xl font-bold tracking-tight sm:text-5xl">{isEn ? 'Advance Your Knowledge' : 'Avanza tu Conocimiento'}</h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-amber-50 sm:text-lg">
                {isEn
                  ? 'Tell us what you want to grow into — our agent searches the web and builds you a full plan grounded in your role, résumé, and courses.'
                  : 'Cuéntanos en qué quieres crecer — nuestro agente busca en internet y te construye un plan completo basado en tu puesto, tu currículum y tus cursos.'}
              </p>
            </div>
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
              <RefreshCw className="h-8 w-8" aria-hidden="true" />
            </div>
          </div>
          <button
            type="button"
            onClick={openModal}
            className="mt-6 inline-flex items-center gap-2 rounded-xl bg-white px-5 py-3 text-sm font-semibold text-amber-700 shadow-lg shadow-black/10 transition hover:bg-amber-50"
          >
            <Sparkles className="h-4 w-4" aria-hidden="true" />
            {plan ? (isEn ? 'Generate a new plan' : 'Generar un nuevo plan') : (isEn ? 'Generate my plan' : 'Generar mi plan')}
          </button>
        </section>

        {!plan ? (
          <Card className="mt-6 border-slate-200/80 bg-white p-8 text-center shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
            <p className="text-sm font-medium text-slate-500 dark:text-white/60">
              {isEn
                ? 'You have not generated a growth plan yet — click "Generate my plan" and tell us what you want to focus on.'
                : 'Aún no has generado un plan de crecimiento — haz clic en "Generar mi plan" y cuéntanos en qué quieres enfocarte.'}
            </p>
          </Card>
        ) : (
          <div className="mt-6 space-y-6">
            {plan.title && <h2 className="text-xl font-bold text-slate-950 dark:text-white">{plan.title}</h2>}
            <Card className="border-amber-200/70 bg-white p-6 shadow-sm dark:border-amber-400/20 dark:bg-white/[0.04]">
              <h3 className="mb-2 text-xs font-bold uppercase tracking-wide text-amber-700 dark:text-amber-400">
                {isEn ? 'Executive summary' : 'Resumen ejecutivo'}
              </h3>
              <p className="text-sm leading-6 text-slate-700 dark:text-white/80">{plan.overallSummary}</p>
            </Card>

            {/* Best Practices Section */}
            {plan.bestPractices && plan.bestPractices.length > 0 && (
              <section aria-labelledby="best-practices-title">
                <div className="mb-3 flex items-center gap-2.5">
                  <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-emerald-600 text-white shadow-sm">
                    <Award className="h-4.5 w-4.5" aria-hidden="true" />
                  </span>
                  <div>
                    <h2 id="best-practices-title" className="text-lg font-bold text-slate-950 dark:text-white">
                      {isEn ? 'Recommended Industry & Role Best Practices' : 'Mejores Prácticas Recomendadas para el Puesto y la Empresa'}
                    </h2>
                    <p className="text-xs text-slate-500 dark:text-white/60">
                      {isEn
                        ? 'Grounding in your role expectations, company description, and industry standards.'
                        : 'Alineadas con los objetivos de tu puesto, la descripción de tu empresa y los estándares de la industria.'}
                    </p>
                  </div>
                </div>
                <div className="grid gap-4 md:grid-cols-2">
                  {plan.bestPractices.map((bp, index) => (
                    <Card
                      key={index}
                      className="flex flex-col justify-between border-slate-200/80 bg-white p-5 shadow-sm transition dark:border-white/10 dark:bg-white/[0.04]"
                    >
                      <div>
                        <div className="mb-2 flex items-center justify-between gap-2">
                          <span
                            className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
                              bp.scope === 'Company'
                                ? 'bg-purple-100 text-purple-700 dark:bg-purple-500/15 dark:text-purple-300'
                                : 'bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300'
                            }`}
                          >
                            {bp.scope === 'Company' ? (
                              <>
                                <Building2 className="h-3 w-3" />
                                {isEn ? 'Company & Industry' : 'Empresa e Industria'}
                              </>
                            ) : (
                              <>
                                <UserCheck className="h-3 w-3" />
                                {isEn ? 'For Your Role' : 'Para Tu Puesto'}
                              </>
                            )}
                          </span>
                        </div>
                        <h3 className="text-sm font-bold text-slate-950 dark:text-white">{bp.title}</h3>
                        <p className="mt-2 text-sm leading-6 text-slate-600 dark:text-white/70">{bp.description}</p>
                      </div>
                      {bp.impact && (
                        <div className="mt-3.5 flex items-start gap-2 rounded-xl bg-slate-50 p-3 text-xs leading-5 text-slate-600 dark:bg-white/[0.03] dark:text-white/60">
                          <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-600 dark:text-emerald-400" />
                          <span>
                            <strong className="font-semibold text-slate-800 dark:text-white/80">{isEn ? 'Impact: ' : 'Impacto: '}</strong>
                            {bp.impact}
                          </span>
                        </div>
                      )}
                    </Card>
                  ))}
                </div>
              </section>
            )}

            <section aria-labelledby="suggested-courses-title">
              <div className="mb-3 flex items-center gap-2.5">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-amber-600 text-white shadow-sm">
                  <BookOpen className="h-4.5 w-4.5" aria-hidden="true" />
                </span>
                <h2 id="suggested-courses-title" className="text-lg font-bold text-slate-950 dark:text-white">
                  {isEn ? 'Suggested courses' : 'Cursos sugeridos'}
                </h2>
              </div>
              {plan.suggestedCourses.length === 0 ? (
                <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn ? 'No courses were suggested this time.' : 'No se sugirieron cursos esta vez.'}
                </p>
              ) : (
                <div className="grid gap-4 md:grid-cols-2">
                  {plan.suggestedCourses.map((course) => (
                    <Card key={course.name} className="flex flex-col border-slate-200/80 bg-white p-5 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
                      <h3 className="text-sm font-bold text-slate-950 dark:text-white">{course.name}</h3>
                      <p className="mt-1.5 text-sm leading-6 text-slate-600 dark:text-white/70">{course.objectives}</p>
                      {course.suggestedIndex.length > 0 && (
                        <ol className="mt-3 list-decimal space-y-1 pl-4 text-xs font-medium text-slate-500 dark:text-white/55">
                          {course.suggestedIndex.map((item, index) => (
                            <li key={index}>{item}</li>
                          ))}
                        </ol>
                      )}
                    </Card>
                  ))}
                </div>
              )}
            </section>

            <section aria-labelledby="papers-title">
              <div className="mb-3 flex items-center gap-2.5">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-indigo-600 text-white shadow-sm">
                  <FileText className="h-4.5 w-4.5" aria-hidden="true" />
                </span>
                <h2 id="papers-title" className="text-lg font-bold text-slate-950 dark:text-white">
                  {isEn ? 'Papers to read' : 'Papers para leer'}
                </h2>
              </div>
              {plan.papers.length === 0 ? (
                <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn ? 'No papers were suggested this time.' : 'No se sugirieron papers esta vez.'}
                </p>
              ) : (
                <div className="space-y-3">
                  {plan.papers.map((paper) => (
                    <Card key={paper.title} className="border-slate-200/80 bg-white p-4 shadow-sm dark:border-white/10 dark:bg-white/[0.04]">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="text-sm font-bold text-slate-950 dark:text-white">{paper.title}</p>
                          {paper.author && <p className="text-xs font-medium text-slate-400 dark:text-white/45">{paper.author}</p>}
                          <p className="mt-1 text-sm leading-6 text-slate-600 dark:text-white/70">{paper.summary}</p>
                        </div>
                        {paper.url && (
                          <a
                            href={paper.url}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex shrink-0 items-center gap-1 rounded-full border border-indigo-200 bg-indigo-50 px-2.5 py-1 text-xs font-semibold text-indigo-700 transition hover:bg-indigo-100 dark:border-indigo-400/25 dark:bg-indigo-400/10 dark:text-indigo-200"
                          >
                            {isEn ? 'Read' : 'Leer'}
                            <ExternalLink className="h-3 w-3" aria-hidden="true" />
                          </a>
                        )}
                      </div>
                    </Card>
                  ))}
                </div>
              )}
            </section>

            <section aria-labelledby="news-title">
              <div className="mb-3 flex items-center gap-2.5">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-sky-600 text-white shadow-sm">
                  <Newspaper className="h-4.5 w-4.5" aria-hidden="true" />
                </span>
                <h2 id="news-title" className="text-lg font-bold text-slate-950 dark:text-white">
                  {isEn ? 'Relevant news for your role' : 'Noticias relevantes para tu puesto'}
                </h2>
              </div>
              {plan.newsLinks.length === 0 ? (
                <p className="text-sm font-medium text-slate-500 dark:text-white/60">
                  {isEn ? 'No relevant news was found this time.' : 'No se encontraron noticias relevantes esta vez.'}
                </p>
              ) : (
                <div className="space-y-3">
                  {plan.newsLinks.map((news) => (
                    <a
                      key={news.url}
                      href={news.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="block rounded-2xl border border-sky-200/70 bg-white p-4 shadow-sm transition hover:-translate-y-0.5 hover:border-sky-300 hover:shadow-md dark:border-sky-400/20 dark:bg-white/[0.04] dark:hover:border-sky-400/40"
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="text-sm font-bold text-slate-950 dark:text-white">{news.title}</p>
                          <p className="mt-1 text-sm leading-6 text-slate-600 dark:text-white/70">{news.summary}</p>
                        </div>
                        <ExternalLink className="h-4 w-4 shrink-0 text-sky-500" aria-hidden="true" />
                      </div>
                    </a>
                  ))}
                </div>
              )}
            </section>
          </div>
        )}
          </div>
        </div>
      </main>
    </div>
  );
}
