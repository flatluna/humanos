import { useEffect, useRef, useState } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Loader2, ArrowRight } from 'lucide-react';
import { getActiveCapabilityGraphRun, type PdfCapabilityGraphRunStatus } from '../lib/api/pdfPipelineApi';

const POLL_INTERVAL_MS = 4000;

/**
 * Persistent "a capability is generating right now" indicator (2026-07-21),
 * shown across the whole app — not just on /runs/:runId — so the user can
 * always find their way back to a live run after navigating away or
 * reloading. Backed by GET /studio/capability-graph/active, which the
 * backend keeps authoritative (only one run is ever allowed at a time),
 * so no client-side storage of the RunId is needed for this to work.
 */
export default function ActiveRunBanner() {
  const location = useLocation();
  const [activeRun, setActiveRun] = useState<PdfCapabilityGraphRunStatus | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  useEffect(() => {
    let cancelled = false;

    async function poll() {
      try {
        const run = await getActiveCapabilityGraphRun();
        if (!cancelled) setActiveRun(run);
      } catch {
        // Best-effort indicator only — a failed poll just tries again.
      } finally {
        if (!cancelled) timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
      }
    }

    poll();
    return () => {
      cancelled = true;
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, []);

  if (!activeRun || location.pathname === `/runs/${activeRun.RunId}`) {
    return null;
  }

  return (
    <div className="border-b border-brand-500/20 bg-brand-500/10">
      <div className="mx-auto flex max-w-7xl items-center justify-between gap-4 px-6 py-2.5 text-sm">
        <div className="flex items-center gap-2.5 text-brand-100">
          <Loader2 className="h-4 w-4 flex-none animate-spin" />
          <span>
            Generando una capability ahora mismo — <span className="text-slate-300">{activeRun.CurrentStepDescription}</span>
          </span>
        </div>
        <Link
          to={`/runs/${activeRun.RunId}`}
          className="flex flex-none items-center gap-1 font-medium text-brand-300 hover:text-brand-200"
        >
          Ver progreso <ArrowRight className="h-3.5 w-3.5" />
        </Link>
      </div>
    </div>
  );
}
