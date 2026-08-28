import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Clock } from 'lucide-react';
import { getStructuredLearningCostDetail, type StructuredLearningCostDetail } from '../lib/api/costsApi';

function formatMs(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

/** Read-only per-step time/token breakdown (2026-08-14) — reads
 * StructuredLearningGenerationUsage rows persisted by Extraction/
 * KnowledgeGraph/Pedagogy. Studio-only, edit mode, same graceful-skip
 * pattern as KnowledgeGraphSection when there's no usage data yet. */
export default function StructuredLearningCostSection({ documentId }: { documentId: string }) {
  const { t } = useTranslation();
  const [detail, setDetail] = useState<StructuredLearningCostDetail | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getStructuredLearningCostDetail(documentId)
      .then((result) => { if (!cancelled) setDetail(result); })
      .catch((err) => { if (!cancelled) setLoadError(err instanceof Error ? err.message : String(err)); });
    return () => { cancelled = true; };
  }, [documentId]);

  if (loadError || (detail && detail.Steps.length === 0)) {
    return null;
  }

  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-5">
      <div className="flex items-center gap-2">
        <Clock className="h-4 w-4 text-amber-400" />
        <h3 className="text-sm font-semibold text-white">{t('structuredLearningRunProgress.costTitle')}</h3>
        {detail && (
          <span className="ml-auto text-xs text-amber-300">
            {formatMs(detail.TotalElapsedMilliseconds)} · {detail.TotalTokens.toLocaleString()} Tokens
            {detail.ImagesGeneratedCount > 0 && ` · ${detail.ImagesGeneratedCount} images generated`}
            {detail.VideosGeneratedCount > 0 && ` · ${detail.VideosGeneratedCount} videos generated`} · ${detail.EstimatedCostUsd.toFixed(4)}
          </span>
        )}
      </div>

      {!detail && <p className="mt-3 text-xs text-slate-500">{t('structuredLearningRunProgress.costLoading')}</p>}

      {detail && (
        <table className="mt-3 w-full text-left text-xs">
          <thead>
            <tr className="text-[10px] uppercase tracking-wide text-slate-500">
              <th className="pb-1.5 pr-3 font-medium">{t('structuredLearningRunProgress.costColumnStep')}</th>
              <th className="pb-1.5 pr-3 font-medium">{t('structuredLearningRunProgress.costColumnModel')}</th>
              <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnTime')}</th>
              <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnIn')}</th>
              <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnOut')}</th>
              <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnImages')}</th>
              <th className="pb-1.5 font-medium text-right">{t('structuredLearningRunProgress.costColumnCost')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-white/5">
            {detail.Steps.map((step) => (
              <tr key={step.StepName} className="text-slate-300">
                <td className="py-1.5 pr-3">{step.StepName}</td>
                <td className="py-1.5 pr-3 text-slate-500">{step.Models || '—'}</td>
                <td className="py-1.5 pr-3 text-right tabular-nums">{formatMs(step.TotalElapsedMilliseconds)}</td>
                <td className="py-1.5 pr-3 text-right tabular-nums">{step.InputTokens.toLocaleString()}</td>
                <td className="py-1.5 pr-3 text-right tabular-nums">{step.OutputTokens.toLocaleString()}</td>
                <td className="py-1.5 pr-3 text-right tabular-nums">{step.ImagesGeneratedCount > 0 ? step.ImagesGeneratedCount.toLocaleString() : '—'}</td>
                <td className="py-1.5 text-right tabular-nums text-amber-300">${step.EstimatedCostUsd.toFixed(4)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
