import { useEffect, useState } from 'react';
import { ChevronDown, ChevronUp, Coins, ImageIcon, ArrowDownToLine, ArrowUpFromLine, Layers3, CalendarDays, X } from 'lucide-react';
import {
  getCapabilityCosts,
  getCapabilityCostDetail,
  type BackendCapabilityCostSummary,
  type BackendCapabilityCostDetail,
} from '../lib/api/costsApi';
import LoadingSpinner from '../components/LoadingSpinner';

function formatTokens(n: number): string {
  return n.toLocaleString('es-MX');
}

function formatUsd(n: number): string {
  return `$${n.toLocaleString('es-MX', { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`;
}

function formatDate(iso: string | null): string | null {
  if (!iso) return null;
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return null;
  return parsed.toLocaleDateString('es-MX', { day: '2-digit', month: 'short', year: 'numeric' });
}

function CapabilityCostCard({ summary }: { summary: BackendCapabilityCostSummary }) {
  const [expanded, setExpanded] = useState(false);
  const [detail, setDetail] = useState<BackendCapabilityCostDetail | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  useEffect(() => {
    if (!expanded || detail || loadingDetail) return;
    setLoadingDetail(true);
    setDetailError(null);
    getCapabilityCostDetail(summary.CapabilityId)
      .then(setDetail)
      .catch((err: Error) => setDetailError(err.message))
      .finally(() => setLoadingDetail(false));
  }, [expanded, detail, loadingDetail, summary.CapabilityId]);

  return (
    <div className="overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03]">
      <button
        type="button"
        onClick={() => setExpanded((v) => !v)}
        className="flex w-full items-center gap-4 p-5 text-left transition-colors hover:bg-white/[0.03]"
      >
        <div className="min-w-0 flex-1">
          <h3 className="truncate font-semibold text-white">{summary.CapabilityName}</h3>
          <div className="mt-2 flex flex-wrap items-center gap-x-5 gap-y-1.5 text-xs text-slate-400">
            {formatDate(summary.GeneratedDate) && (
              <span className="flex items-center gap-1.5">
                <CalendarDays className="h-3.5 w-3.5 text-indigo-400" />
                {formatDate(summary.GeneratedDate)}
              </span>
            )}
            <span className="flex items-center gap-1.5">
              <ArrowDownToLine className="h-3.5 w-3.5 text-sky-400" />
              {formatTokens(summary.InputTokens)} in
            </span>
            <span className="flex items-center gap-1.5">
              <ArrowUpFromLine className="h-3.5 w-3.5 text-fuchsia-400" />
              {formatTokens(summary.OutputTokens)} out
            </span>
            <span className="flex items-center gap-1.5">
              <Layers3 className="h-3.5 w-3.5 text-slate-500" />
              {formatTokens(summary.TotalTokens)} tokens totales
            </span>
            <span className="flex items-center gap-1.5">
              <ImageIcon className="h-3.5 w-3.5 text-emerald-400" />
              {summary.ImagesGeneratedCount} imágenes
            </span>
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-4">
          <div className="text-right">
            <div className="flex items-center gap-1.5 text-lg font-semibold text-white">
              <Coins className="h-4 w-4 text-amber-400" />
              {formatUsd(summary.EstimatedCostUsd)}
            </div>
            <div className="text-[11px] text-slate-500">estimado{summary.IsEstimate ? ' · tarifas placeholder' : ''}</div>
          </div>
          {expanded ? <ChevronUp className="h-5 w-5 text-slate-400" /> : <ChevronDown className="h-5 w-5 text-slate-400" />}
        </div>
      </button>

      {expanded && (
        <div className="border-t border-white/10 bg-black/20 px-5 py-4">
          {loadingDetail && <LoadingSpinner label="Cargando detalle..." />}

          {!loadingDetail && detailError && (
            <p className="text-sm text-red-300">No se pudo cargar el detalle: {detailError}</p>
          )}

          {!loadingDetail && !detailError && detail && (
            <div className="overflow-x-auto">
              {detail.Sections.length === 0 ? (
                <p className="py-4 text-sm text-slate-400">Sin datos de uso registrados para esta capability.</p>
              ) : (
                <table className="w-full text-left text-sm">
                  <thead>
                    <tr className="text-xs uppercase tracking-wide text-slate-500">
                      <th className="pb-2 pr-4 font-medium">Sección</th>
                      <th className="pb-2 pr-4 font-medium">Agentes</th>
                      <th className="pb-2 pr-4 font-medium">Modelo</th>
                      <th className="pb-2 pr-4 font-medium text-right">In</th>
                      <th className="pb-2 pr-4 font-medium text-right">Out</th>
                      <th className="pb-2 pr-4 font-medium text-right">Cache</th>
                      <th className="pb-2 pr-4 font-medium text-right">Total</th>
                      <th className="pb-2 font-medium text-right">Costo</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5">
                    {detail.Sections.map((section) => (
                      <tr key={section.SectionLabel} className="text-slate-300">
                        <td className="py-2 pr-4">{section.SectionLabel}</td>
                        <td className="py-2 pr-4 text-slate-500">{section.Agents}</td>
                        <td className="py-2 pr-4 text-slate-500">{section.Models || '—'}</td>
                        <td className="py-2 pr-4 text-right tabular-nums">{formatTokens(section.InputTokens)}</td>
                        <td className="py-2 pr-4 text-right tabular-nums">{formatTokens(section.OutputTokens)}</td>
                        <td className="py-2 pr-4 text-right tabular-nums">{formatTokens(section.CachedInputTokens)}</td>
                        <td className="py-2 pr-4 text-right font-medium tabular-nums text-white">
                          {formatTokens(section.TotalTokens)}
                        </td>
                        <td className="py-2 text-right font-medium tabular-nums text-amber-300">
                          {formatUsd(section.EstimatedCostUsd)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t border-white/10 font-semibold text-white">
                      <td className="pt-3 pr-4" colSpan={3}>
                        Total del curso
                      </td>
                      <td className="pt-3 pr-4 text-right tabular-nums">{formatTokens(detail.InputTokens)}</td>
                      <td className="pt-3 pr-4 text-right tabular-nums">{formatTokens(detail.OutputTokens)}</td>
                      <td className="pt-3 pr-4 text-right tabular-nums">{formatTokens(detail.CachedInputTokens)}</td>
                      <td className="pt-3 pr-4 text-right tabular-nums">{formatTokens(detail.TotalTokens)}</td>
                      <td className="pt-3 text-right tabular-nums text-amber-300">{formatUsd(detail.EstimatedCostUsd)}</td>
                    </tr>
                  </tfoot>
                </table>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default function CostDashboardPage() {
  const [summaries, setSummaries] = useState<BackendCapabilityCostSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dateFilter, setDateFilter] = useState('');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    getCapabilityCosts(dateFilter || undefined)
      .then((data) => {
        if (!cancelled) setSummaries(data);
      })
      .catch((err: Error) => {
        if (!cancelled) setError(err.message);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [dateFilter]);

  const totals = summaries.reduce(
    (acc, s) => ({
      input: acc.input + s.InputTokens,
      output: acc.output + s.OutputTokens,
      images: acc.images + s.ImagesGeneratedCount,
      cost: acc.cost + s.EstimatedCostUsd,
    }),
    { input: 0, output: 0, images: 0, cost: 0 },
  );

  return (
    <div>
      <section className="relative overflow-hidden border-b border-white/10">
        <div className="absolute inset-0 bg-mesh-gradient opacity-60" />
        <div className="relative mx-auto max-w-7xl px-6 py-16 text-center">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-white/5 px-3.5 py-1.5 text-xs font-medium text-slate-300">
            <Coins className="h-3.5 w-3.5 text-amber-400" />
            Costos de generación
          </span>
          <h1 className="mt-6 text-4xl font-bold tracking-tight text-white sm:text-5xl">
            Costo por <span className="shimmer-text">capability</span>
          </h1>
          <p className="mx-auto mt-4 max-w-2xl text-lg text-slate-400">
            Tokens de entrada/salida, imágenes generadas y costo estimado por cada capability generada con IA.
          </p>

          {!loading && !error && summaries.length > 0 && (
            <div className="mx-auto mt-8 grid max-w-2xl grid-cols-2 gap-4 sm:grid-cols-4">
              <StatPill label="Input" value={formatTokens(totals.input)} />
              <StatPill label="Output" value={formatTokens(totals.output)} />
              <StatPill label="Imágenes" value={String(totals.images)} />
              <StatPill label="Costo total" value={formatUsd(totals.cost)} accent />
            </div>
          )}
        </div>
      </section>

      <section className="mx-auto max-w-4xl px-6 py-10">
        <div className="mb-6 flex flex-wrap items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.03] p-4">
          <label htmlFor="cost-date-filter" className="flex items-center gap-2 text-sm text-slate-300">
            <CalendarDays className="h-4 w-4 text-indigo-400" />
            Filtrar por día
          </label>
          <input
            id="cost-date-filter"
            type="date"
            value={dateFilter}
            onChange={(e) => setDateFilter(e.target.value)}
            className="rounded-lg border border-white/10 bg-black/30 px-3 py-1.5 text-sm text-white outline-none focus:border-indigo-400/50 [color-scheme:dark]"
          />
          {dateFilter && (
            <button
              type="button"
              onClick={() => setDateFilter('')}
              className="flex items-center gap-1 rounded-lg border border-white/10 px-2.5 py-1.5 text-xs text-slate-400 transition-colors hover:bg-white/5 hover:text-white"
            >
              <X className="h-3.5 w-3.5" />
              Quitar filtro
            </button>
          )}
        </div>

        {loading && <LoadingSpinner label="Cargando costos..." />}

        {!loading && error && (
          <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-center text-red-300">
            No se pudo cargar el dashboard de costos: {error}
          </div>
        )}

        {!loading && !error && summaries.length === 0 && (
          <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-400">
            {dateFilter
              ? 'No hay capabilities generadas ese día.'
              : 'Todavía no hay capabilities generadas con datos de costo registrados.'}
          </div>
        )}

        {!loading && !error && summaries.length > 0 && (
          <div className="space-y-4">
            {summaries.map((summary) => (
              <CapabilityCostCard key={summary.CapabilityId} summary={summary} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function StatPill({ label, value, accent }: { label: string; value: string; accent?: boolean }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] px-4 py-3">
      <div className={`text-lg font-semibold ${accent ? 'text-amber-400' : 'text-white'}`}>{value}</div>
      <div className="text-[11px] text-slate-500">{label}</div>
    </div>
  );
}
