import { useEffect, useRef, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Loader2, CheckCircle2, XCircle, ArrowRight, GitBranch, Boxes, ImagePlus, DollarSign } from 'lucide-react';
import { getPdfCapabilityGraphStatus, type PdfCapabilityGraphRunStatus } from '../lib/api/pdfPipelineApi';

const POLL_INTERVAL_MS = 2500;

export default function RunProgressPage() {
  const { runId } = useParams<{ runId: string }>();
  const [status, setStatus] = useState<PdfCapabilityGraphRunStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const timerRef = useRef<ReturnType<typeof setTimeout>>();

  useEffect(() => {
    if (!runId) return;
    let cancelled = false;

    async function poll() {
      try {
        const next = await getPdfCapabilityGraphStatus(runId!);
        if (cancelled) return;
        setStatus(next);
        if (next.Stage === 'Running') {
          timerRef.current = setTimeout(poll, POLL_INTERVAL_MS);
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'No se pudo consultar el estado.');
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
            <h2 className="text-xl font-semibold text-white">Generando tu capability...</h2>
            <p className="mt-2 text-slate-400">{status?.CurrentStepDescription ?? 'Iniciando pipeline...'}</p>
          </div>
        </div>
      )}

      {!error && stage === 'Failed' && (
        <div className="animate-fade-in space-y-4">
          <XCircle className="mx-auto h-14 w-14 text-red-400" />
          <h2 className="text-xl font-semibold text-white">La generación falló</h2>
          <p className="text-slate-400">{status?.ErrorMessage ?? 'Ocurrió un error inesperado.'}</p>
          <Link to="/new" className="inline-flex items-center gap-1.5 text-sm font-medium text-brand-400 hover:text-brand-300">
            Intentar de nuevo <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      )}

      {!error && stage === 'Completed' && status?.Result && (
        <div className="animate-fade-in w-full space-y-6">
          <CheckCircle2 className="mx-auto h-14 w-14 text-emerald-400" />
          <div>
            <h2 className="text-2xl font-bold text-white">¡Listo! {status.Result.GraphName}</h2>
            <p className="mt-2 text-slate-400">Tu capability fue generada exitosamente.</p>
          </div>

          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <ResultStat icon={GitBranch} label="Nodos" value={status.Result.NodeCount} />
            <ResultStat icon={Boxes} label="Con blueprint" value={status.Result.NodesWithBlueprintCount} />
            <ResultStat icon={ImagePlus} label="Ilustraciones" value={status.Result.IllustrationsGeneratedCount} />
            <ResultStat
              icon={DollarSign}
              label="Costo estimado"
              value={status.Result.EstimatedCost ? `$${status.Result.EstimatedCost.TotalCostUsd.toFixed(2)}` : '—'}
            />
          </div>

          <Link
            to={`/capabilities/${status.Result.CapabilityId}`}
            className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-3 text-sm font-semibold text-[#fff] shadow-lg shadow-brand-500/25 transition-transform hover:scale-[1.02]"
          >
            Ver capability
            <ArrowRight className="h-4 w-4" />
          </Link>
        </div>
      )}
    </div>
  );
}

function ResultStat({ icon: Icon, label, value }: { icon: typeof GitBranch; label: string; value: string | number }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
      <Icon className="mx-auto h-4 w-4 text-brand-400" />
      <div className="mt-2 text-lg font-semibold text-white">{value}</div>
      <div className="text-xs text-slate-400">{label}</div>
    </div>
  );
}
