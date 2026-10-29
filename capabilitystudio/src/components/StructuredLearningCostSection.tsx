import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Clock, ChevronDown, ChevronRight } from 'lucide-react';
import {
  getStructuredLearningCostDetail,
  getStructuredLearningCostPerEngram,
  type StructuredLearningCostDetail,
  type StructuredLearningEngramCost,
} from '../lib/api/costsApi';

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
  const [perEngramOpen, setPerEngramOpen] = useState(false);
  const [perEngram, setPerEngram] = useState<StructuredLearningEngramCost[] | null>(null);
  const [perEngramError, setPerEngramError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getStructuredLearningCostDetail(documentId)
      .then((result) => { if (!cancelled) setDetail(result); })
      .catch((err) => { if (!cancelled) setLoadError(err instanceof Error ? err.message : String(err)); });
    return () => { cancelled = true; };
  }, [documentId]);

  useEffect(() => {
    if (!perEngramOpen || perEngram) return;
    let cancelled = false;
    getStructuredLearningCostPerEngram(documentId)
      .then((rows) => { if (!cancelled) setPerEngram(rows); })
      .catch((err) => { if (!cancelled) setPerEngramError(err instanceof Error ? err.message : String(err)); });
    return () => { cancelled = true; };
  }, [perEngramOpen, perEngram, documentId]);

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
              <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnVideos')}</th>
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
                <td className="py-1.5 pr-3 text-right tabular-nums">{(step.VideosGeneratedCount ?? 0) > 0 ? (step.VideosGeneratedCount ?? 0).toLocaleString() : '—'}</td>
                <td className="py-1.5 text-right tabular-nums text-amber-300">${step.EstimatedCostUsd.toFixed(4)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {detail && (
        <div className="mt-4 border-t border-white/5 pt-3">
          <button
            type="button"
            onClick={() => setPerEngramOpen((v) => !v)}
            className="flex items-center gap-1.5 text-xs font-semibold text-amber-300 hover:text-amber-200"
          >
            {perEngramOpen ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
            {t('structuredLearningRunProgress.costPerEngramToggle')}
          </button>

          {perEngramOpen && (
            <>
              {perEngramError && <p className="mt-2 text-xs text-red-300">{perEngramError}</p>}
              {!perEngram && !perEngramError && <p className="mt-2 text-xs text-slate-500">{t('structuredLearningRunProgress.costLoading')}</p>}
              {perEngram && perEngram.length === 0 && <p className="mt-2 text-xs text-slate-500">{t('structuredLearningRunProgress.costPerEngramEmpty')}</p>}
              {perEngram && perEngram.length > 0 && (
                <table className="mt-2 w-full text-left text-xs">
                  <thead>
                    <tr className="text-[10px] uppercase tracking-wide text-slate-500">
                      <th className="pb-1.5 pr-3 font-medium">{t('structuredLearningRunProgress.costColumnEngram')}</th>
                      <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnCalls')}</th>
                      <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnIn')}</th>
                      <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnOut')}</th>
                      <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnImages')}</th>
                      <th className="pb-1.5 pr-3 font-medium text-right">{t('structuredLearningRunProgress.costColumnVideos')}</th>
                      <th className="pb-1.5 font-medium text-right">{t('structuredLearningRunProgress.costColumnCost')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5">
                    {perEngram.map((row) => (
                      <tr key={row.StructuredLearningDocumentSectionId} className="text-slate-300">
                        <td className="py-1.5 pr-3">
                          <p className="font-medium text-slate-200">{row.SubchapterTitle}</p>
                          <p className="text-[10px] text-slate-500">{row.ChapterTitle} · {row.Steps}</p>
                        </td>
                        <td className="py-1.5 pr-3 text-right tabular-nums">{row.CallCount}</td>
                        <td className="py-1.5 pr-3 text-right tabular-nums">{row.InputTokens.toLocaleString()}</td>
                        <td className="py-1.5 pr-3 text-right tabular-nums">{row.OutputTokens.toLocaleString()}</td>
                        <td className="py-1.5 pr-3 text-right tabular-nums">{row.ImagesGeneratedCount > 0 ? row.ImagesGeneratedCount : '—'}</td>
                        <td className="py-1.5 pr-3 text-right tabular-nums">{row.VideosGeneratedCount > 0 ? row.VideosGeneratedCount : '—'}</td>
                        <td className="py-1.5 text-right tabular-nums text-amber-300">${row.TotalCostUsd.toFixed(4)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}
