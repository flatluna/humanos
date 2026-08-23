import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, Link } from 'react-router-dom';
import { Loader2, CheckCircle2, XCircle, ArrowRight, BookOpen, Layers, FileText, HelpCircle, ImagePlus } from 'lucide-react';
import { getTraditionalCourseStatus, type TraditionalCourseRunStatus } from '../lib/api/traditionalCourseApi';

const POLL_INTERVAL_MS = 2500;

export default function TraditionalRunProgressPage() {
  const { t } = useTranslation();
  const { runId } = useParams<{ runId: string }>();
  const [status, setStatus] = useState<TraditionalCourseRunStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  useEffect(() => {
    if (!runId) return;
    let cancelled = false;

    async function poll() {
      try {
        const next = await getTraditionalCourseStatus(runId!);
        if (cancelled) return;
        setStatus(next);
        if (next.Stage === 'Running') {
          timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : t('traditionalRunProgress.statusErrorFallback'));
      }
    }

    poll();
    return () => {
      cancelled = true;
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, [runId]);

  const stage = status?.Stage;

  return (
    <div className="mx-auto flex min-h-[70vh] max-w-2xl flex-col items-center justify-center px-6 py-16 text-center">
      {error && (
        <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-red-300">{error}</div>
      )}

      {!error && (!status || stage === 'Running') && (
        <div className="animate-fade-in space-y-6">
          <div className="relative mx-auto flex h-20 w-20 items-center justify-center">
            <div className="absolute inset-0 rounded-full bg-brand-500/20 animate-ping" />
            <div className="relative flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-brand-500 to-accent-500">
              <Loader2 className="h-7 w-7 animate-spin text-[#fff]" />
            </div>
          </div>
          <div>
            <h2 className="text-xl font-semibold text-white">{t('traditionalRunProgress.generating')}</h2>
            <p className="mt-2 text-slate-400">{status?.CurrentStepDescription ?? t('traditionalRunProgress.startingPipeline')}</p>
          </div>
        </div>
      )}

      {!error && stage === 'Failed' && (
        <div className="animate-fade-in space-y-4">
          <XCircle className="mx-auto h-14 w-14 text-red-400" />
          <h2 className="text-xl font-semibold text-white">{t('traditionalRunProgress.failed')}</h2>
          <p className="text-slate-400">{status?.ErrorMessage ?? t('traditionalRunProgress.unexpectedError')}</p>
          <Link to="/new-traditional" className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-400 hover:text-brand-300">
            {t('traditionalRunProgress.tryAgain')} <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      )}

      {!error && stage === 'Completed' && status?.Result && (
        <div className="animate-fade-in w-full space-y-6">
          <CheckCircle2 className="mx-auto h-14 w-14 text-emerald-400" />
          <div>
            <h2 className="text-2xl font-bold text-white">{t('traditionalRunProgress.done', { name: status.Result.CourseName })}</h2>
            <p className="mt-2 text-slate-400">{t('traditionalRunProgress.successMessage')}</p>
          </div>

          {status.Result.ExecutiveSummary && (
            <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 text-left">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-400">
                {t('traditionalRunProgress.executiveSummaryLabel')}
              </h3>
              <p className="mt-1.5 text-sm text-slate-200">{status.Result.ExecutiveSummary}</p>
            </div>
          )}

          <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
            <ResultStat icon={Layers} label={t('traditionalRunProgress.chapters')} value={status.Result.ChapterCount} />
            <ResultStat icon={BookOpen} label={t('traditionalRunProgress.lessons')} value={status.Result.LessonCount} />
            <ResultStat icon={FileText} label={t('traditionalRunProgress.cards')} value={status.Result.CardCount} />
            <ResultStat icon={HelpCircle} label={t('traditionalRunProgress.examQuestions')} value={status.Result.ExamQuestionCount} />
            <ResultStat icon={ImagePlus} label={t('traditionalRunProgress.illustrations')} value={status.Result.IllustrationsGeneratedCount} />
          </div>

          <Link
            to={`/capabilities/${status.Result.CapabilityId}/traditional-course`}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02]"
          >
            {t('traditionalRunProgress.viewCourse')}
            <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      )}
    </div>
  );
}

function ResultStat({ icon: Icon, label, value }: { icon: typeof Layers; label: string; value: string | number }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
      <Icon className="mx-auto h-4 w-4 text-brand-400" />
      <div className="mt-2 text-lg font-semibold text-white">{value}</div>
      <div className="text-xs text-slate-400">{label}</div>
    </div>
  );
}
