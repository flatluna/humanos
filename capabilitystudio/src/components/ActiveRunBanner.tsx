import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation } from 'react-router-dom';
import { Loader2, ArrowRight } from 'lucide-react';
import { getActiveCapabilityGraphRun, type PdfCapabilityGraphRunStatus } from '../lib/api/pdfPipelineApi';
import { getActiveTraditionalCourseRun, type TraditionalCourseRunStatus } from '../lib/api/traditionalCourseApi';

const POLL_INTERVAL_MS = 4000;

/**
 * Persistent "a course is generating right now" indicator (2026-07-21),
 * shown across the whole app — not just on /runs/:runId — so the user can
 * always find their way back to a live run after navigating away or
 * reloading. Backed by GET /studio/capability-graph/active AND (2026-08-09)
 * GET /studio/traditional-course/active — these are TWO SEPARATE run slots
 * (the Graph pipeline and the Traditional pipeline don't conflict with
 * each other), so both are polled and whichever is active gets its own
 * banner row (both can show at once if, somehow, both kinds are running).
 */
export default function ActiveRunBanner() {
  const { t } = useTranslation();
  const location = useLocation();
  const [activeGraphRun, setActiveGraphRun] = useState<PdfCapabilityGraphRunStatus | null>(null);
  const [activeTraditionalRun, setActiveTraditionalRun] = useState<TraditionalCourseRunStatus | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  useEffect(() => {
    let cancelled = false;

    async function poll() {
      const [graphRun, traditionalRun] = await Promise.all([
        getActiveCapabilityGraphRun().catch(() => null),
        getActiveTraditionalCourseRun().catch(() => null),
      ]);
      if (!cancelled) {
        setActiveGraphRun(graphRun);
        setActiveTraditionalRun(traditionalRun);
        timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
      }
    }

    poll();
    return () => {
      cancelled = true;
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, []);

  const showGraphBanner = activeGraphRun && location.pathname !== `/runs/${activeGraphRun.RunId}`;
  const showTraditionalBanner =
    activeTraditionalRun && location.pathname !== `/traditional-runs/${activeTraditionalRun.RunId}`;

  if (!showGraphBanner && !showTraditionalBanner) {
    return null;
  }

  return (
    <>
      {showGraphBanner && activeGraphRun && (
        <div className="border-b border-brand-500/20 bg-brand-500/10">
          <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-6 py-2.5 text-sm">
            <div className="flex items-center gap-2.5 text-brand-100">
              <Loader2 className="h-4 w-4 flex-none animate-spin" />
              <span>
                {t('activeRunBanner.generatingPrefix')} <span className="text-slate-300">{activeGraphRun.CurrentStepDescription}</span>
              </span>
            </div>
            <Link
              to={`/runs/${activeGraphRun.RunId}`}
              className="flex flex-none items-center gap-1 font-medium text-brand-300 hover:text-brand-200"
            >
              {t('activeRunBanner.viewProgress')} <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </div>
        </div>
      )}
      {showTraditionalBanner && activeTraditionalRun && (
        <div className="border-b border-accent-500/20 bg-accent-500/10">
          <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-6 py-2.5 text-sm">
            <div className="flex items-center gap-2.5 text-brand-100">
              <Loader2 className="h-4 w-4 flex-none animate-spin" />
              <span>
                {t('activeRunBanner.generatingPrefix')} <span className="text-slate-300">{activeTraditionalRun.CurrentStepDescription}</span>
              </span>
            </div>
            <Link
              to={`/traditional-runs/${activeTraditionalRun.RunId}`}
              className="flex flex-none items-center gap-1 font-medium text-brand-300 hover:text-brand-200"
            >
              {t('activeRunBanner.viewProgress')} <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </div>
        </div>
      )}
    </>
  );
}
