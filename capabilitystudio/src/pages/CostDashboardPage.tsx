import { useEffect, useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import {
  ChevronDown,
  ChevronUp,
  Coins,
  ImageIcon,
  Video,
  ArrowDownToLine,
  ArrowUpFromLine,
  Layers3,
  CalendarDays,
  Clock,
  Search,
  X,
  ExternalLink,
  Sparkles,
  GraduationCap,
  BookOpen,
} from 'lucide-react';
import {
  getCapabilityCosts,
  getCapabilityCostDetail,
  getStructuredLearningCosts,
  getStructuredLearningCostDetail,
  type BackendCapabilityCostSummary,
  type BackendCapabilityCostDetail,
  type StructuredLearningCostSummary,
  type StructuredLearningCostDetail,
} from '../lib/api/costsApi';
import { getCapabilities, type BackendCapability } from '../lib/api/capabilitiesApi';
import { getStructuredLearningDocuments, type StructuredLearningDocumentSummary } from '../lib/api/structuredLearningApi';
import LoadingSpinner from '../components/LoadingSpinner';

export type UnifiedCourseType = 'structured' | 'capability';

export interface UnifiedCourseCostSummary {
  id: string;
  name: string;
  type: UnifiedCourseType;
  learningMode?: string;
  itemCount?: number;
  inputTokens: number;
  outputTokens: number;
  cachedInputTokens: number;
  totalTokens: number;
  totalElapsedMilliseconds: number;
  imagesGeneratedCount: number;
  imageCostUsd: number;
  videosGeneratedCount: number;
  videoCostUsd: number;
  estimatedCostUsd: number;
  isEstimate: boolean;
  generatedDate: string | null;
  studioUrl: string;
}

function formatTokens(n: number, locale: string): string {
  return n.toLocaleString(locale);
}

function formatUsd(n: number, locale: string): string {
  return `$${n.toLocaleString(locale, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`;
}

function formatDate(iso: string | null, locale: string): string | null {
  if (!iso) return null;
  const parsed = new Date(iso);
  if (Number.isNaN(parsed.getTime())) return null;
  return parsed.toLocaleDateString(locale, { day: '2-digit', month: 'short', year: 'numeric' });
}

function formatDuration(ms: number): string {
  if (!ms || ms <= 0) return '—';
  if (ms < 1000) return `${ms}ms`;
  const totalSeconds = Math.round(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  if (minutes === 0) return `${(ms / 1000).toFixed(1)}s`;
  return `${minutes}m ${seconds}s`;
}

function CourseCostCard({ summary }: { summary: UnifiedCourseCostSummary }) {
  const { t, i18n } = useTranslation();
  const locale = i18n.language === 'en' ? 'en-US' : 'es-MX';
  const [expanded, setExpanded] = useState(false);
  const [structuredDetail, setStructuredDetail] = useState<StructuredLearningCostDetail | null>(null);
  const [capabilityDetail, setCapabilityDetail] = useState<BackendCapabilityCostDetail | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  useEffect(() => {
    if (!expanded) return;

    if (summary.type === 'structured' && !structuredDetail && !loadingDetail) {
      setLoadingDetail(true);
      setDetailError(null);
      getStructuredLearningCostDetail(summary.id)
        .then((data) => setStructuredDetail(data))
        .catch((err: Error) => setDetailError(err.message))
        .finally(() => setLoadingDetail(false));
    } else if (summary.type === 'capability' && !capabilityDetail && !loadingDetail) {
      setLoadingDetail(true);
      setDetailError(null);
      getCapabilityCostDetail(summary.id)
        .then((data) => setCapabilityDetail(data))
        .catch((err: Error) => setDetailError(err.message))
        .finally(() => setLoadingDetail(false));
    }
  }, [expanded, summary.id, summary.type, structuredDetail, capabilityDetail, loadingDetail]);

  return (
    <div className="overflow-hidden rounded-2xl border border-white/10 bg-white/[0.03] shadow-lg transition-all hover:border-white/20">
      <div
        role="button"
        tabIndex={0}
        onClick={() => setExpanded((v) => !v)}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            setExpanded((v) => !v);
          }
        }}
        className="flex w-full cursor-pointer flex-col gap-4 p-5 text-left transition-colors hover:bg-white/[0.03] sm:flex-row sm:items-center"
      >
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2.5">
            <h3 className="truncate text-base font-semibold text-white">{summary.name}</h3>
            {summary.type === 'structured' ? (
              <span className="inline-flex items-center gap-1 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-2.5 py-0.5 text-[11px] font-medium text-emerald-300">
                <Sparkles className="h-3 w-3" />
                {t('costDashboard.typeStructured')}
              </span>
            ) : (
              <span className="inline-flex items-center gap-1 rounded-full border border-violet-500/30 bg-violet-500/10 px-2.5 py-0.5 text-[11px] font-medium text-violet-300">
                <GraduationCap className="h-3 w-3" />
                {t('costDashboard.typeCapability')}
              </span>
            )}
            {typeof summary.itemCount === 'number' && summary.itemCount > 0 && (
              <span className="inline-flex items-center gap-1 rounded-full border border-white/10 bg-white/5 px-2 py-0.5 text-[11px] text-slate-400">
                <BookOpen className="h-3 w-3" />
                {summary.itemCount} {summary.type === 'structured' ? 'Engrams' : 'Chapters'}
              </span>
            )}
          </div>

          <div className="mt-2.5 flex flex-wrap items-center gap-x-5 gap-y-1.5 text-xs text-slate-400">
            {formatDate(summary.generatedDate, locale) && (
              <span className="flex items-center gap-1.5">
                <CalendarDays className="h-3.5 w-3.5 text-indigo-400" />
                {formatDate(summary.generatedDate, locale)}
              </span>
            )}
            {summary.totalElapsedMilliseconds > 0 && (
              <span className="flex items-center gap-1.5">
                <Clock className="h-3.5 w-3.5 text-amber-400" />
                {formatDuration(summary.totalElapsedMilliseconds)}
              </span>
            )}
            <span className="flex items-center gap-1.5">
              <ArrowDownToLine className="h-3.5 w-3.5 text-sky-400" />
              {formatTokens(summary.inputTokens, locale)} in
            </span>
            <span className="flex items-center gap-1.5">
              <ArrowUpFromLine className="h-3.5 w-3.5 text-fuchsia-400" />
              {formatTokens(summary.outputTokens, locale)} out
            </span>
            <span className="flex items-center gap-1.5">
              <Layers3 className="h-3.5 w-3.5 text-slate-500" />
              {formatTokens(summary.totalTokens, locale)} {t('costDashboard.totalTokens')}
            </span>
            {summary.imagesGeneratedCount > 0 && (
              <span className="flex items-center gap-1.5">
                <ImageIcon className="h-3.5 w-3.5 text-emerald-400" />
                {summary.imagesGeneratedCount} {t('costDashboard.imagesUnit')}
              </span>
            )}
            {summary.videosGeneratedCount > 0 && (
              <span className="flex items-center gap-1.5">
                <Video className="h-3.5 w-3.5 text-pink-400" />
                {summary.videosGeneratedCount} {t('costDashboard.videosUnit')}
              </span>
            )}
          </div>
        </div>

        <div className="flex shrink-0 items-center justify-between gap-4 border-t border-white/5 pt-3 sm:border-t-0 sm:pt-0">
          <div className="text-right">
            <div className="flex items-center justify-end gap-1.5 text-lg font-bold text-white">
              <Coins className="h-4 w-4 text-amber-400" />
              {formatUsd(summary.estimatedCostUsd, locale)}
            </div>
            <div className="text-[11px] text-slate-500">
              {t('costDashboard.estimated')}
              {summary.isEstimate ? t('costDashboard.placeholderRates') : ''}
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Link
              to={summary.studioUrl}
              onClick={(e) => e.stopPropagation()}
              title={t('costDashboard.openInStudio')}
              className="flex items-center gap-1 rounded-lg border border-white/10 bg-white/5 px-2.5 py-1.5 text-xs font-medium text-slate-300 transition-colors hover:border-brand-400/40 hover:bg-brand-500/15 hover:text-white"
            >
              <ExternalLink className="h-3.5 w-3.5 text-brand-400" />
              <span className="hidden sm:inline">{t('costDashboard.openInStudio')}</span>
            </Link>

            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                setExpanded((v) => !v);
              }}
              className="rounded-lg p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
              aria-label={expanded ? 'Collapse' : 'Expand'}
            >
              {expanded ? <ChevronUp className="h-5 w-5 text-slate-400" /> : <ChevronDown className="h-5 w-5 text-slate-400" />}
            </button>
          </div>
        </div>
      </div>

      {expanded && (
        <div className="border-t border-white/10 bg-black/25 px-5 py-4">
          {loadingDetail && <LoadingSpinner label={t('costDashboard.loadingDetail')} />}

          {!loadingDetail && detailError && (
            <p className="text-sm text-red-300">{t('costDashboard.detailLoadError', { error: detailError })}</p>
          )}

          {!loadingDetail && !detailError && summary.type === 'structured' && structuredDetail && (
            <div className="overflow-x-auto">
              {structuredDetail.Steps.length === 0 ? (
                <p className="py-4 text-sm text-slate-400">{t('costDashboard.noUsageData')}</p>
              ) : (
                <table className="w-full text-left text-xs sm:text-sm">
                  <thead>
                    <tr className="text-[11px] uppercase tracking-wide text-slate-500">
                      <th className="pb-2 pr-4 font-medium">{t('costDashboard.columnStep')}</th>
                      <th className="pb-2 pr-4 font-medium">{t('costDashboard.columnAgents')}</th>
                      <th className="pb-2 pr-4 font-medium">{t('costDashboard.columnModel')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnTime')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnIn')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnOut')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnImages')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnVideos')}</th>
                      <th className="pb-2 font-medium text-right">{t('costDashboard.columnCost')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5">
                    {structuredDetail.Steps.map((step) => (
                      <tr key={step.StepName} className="text-slate-300 hover:bg-white/[0.02]">
                        <td className="py-2 pr-4 font-medium text-white">{step.StepName}</td>
                        <td className="py-2 pr-4 text-xs text-slate-400">{step.Agents || '—'}</td>
                        <td className="py-2 pr-4 text-xs text-slate-400">{step.Models || '—'}</td>
                        <td className="py-2 pr-4 text-right tabular-nums text-slate-300">
                          {formatDuration(step.TotalElapsedMilliseconds)}
                        </td>
                        <td className="py-2 pr-4 text-right tabular-nums text-slate-300">
                          {formatTokens(step.InputTokens, locale)}
                        </td>
                        <td className="py-2 pr-4 text-right tabular-nums text-slate-300">
                          {formatTokens(step.OutputTokens, locale)}
                        </td>
                        <td className="py-2 pr-4 text-right tabular-nums text-slate-300">
                          {step.ImagesGeneratedCount > 0 ? formatTokens(step.ImagesGeneratedCount, locale) : '—'}
                        </td>
                        <td className="py-2 pr-4 text-right tabular-nums text-slate-300">
                          {(step.VideosGeneratedCount ?? 0) > 0 ? formatTokens(step.VideosGeneratedCount ?? 0, locale) : '—'}
                        </td>
                        <td className="py-2 text-right font-medium tabular-nums text-amber-300">
                          {formatUsd(step.EstimatedCostUsd, locale)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t border-white/10 font-semibold text-white">
                      <td className="pt-3 pr-4" colSpan={3}>
                        {t('costDashboard.totalCourse')}
                      </td>
                      <td className="pt-3 pr-4 text-right tabular-nums">
                        {formatDuration(structuredDetail.TotalElapsedMilliseconds)}
                      </td>
                      <td className="pt-3 pr-4 text-right tabular-nums">
                        {formatTokens(structuredDetail.InputTokens, locale)}
                      </td>
                      <td className="pt-3 pr-4 text-right tabular-nums">
                        {formatTokens(structuredDetail.OutputTokens, locale)}
                      </td>
                      <td className="pt-3 pr-4 text-right tabular-nums">
                        {structuredDetail.ImagesGeneratedCount > 0 ? formatTokens(structuredDetail.ImagesGeneratedCount, locale) : '—'}
                      </td>
                      <td className="pt-3 pr-4 text-right tabular-nums">
                        {structuredDetail.VideosGeneratedCount > 0 ? formatTokens(structuredDetail.VideosGeneratedCount, locale) : '—'}
                      </td>
                      <td className="pt-3 text-right tabular-nums text-amber-300">
                        {formatUsd(structuredDetail.EstimatedCostUsd, locale)}
                      </td>
                    </tr>
                  </tfoot>
                </table>
              )}
            </div>
          )}

          {!loadingDetail && !detailError && summary.type === 'capability' && capabilityDetail && (
            <div className="overflow-x-auto">
              {capabilityDetail.Sections.length === 0 ? (
                <p className="py-4 text-sm text-slate-400">{t('costDashboard.noUsageData')}</p>
              ) : (
                <table className="w-full text-left text-xs sm:text-sm">
                  <thead>
                    <tr className="text-[11px] uppercase tracking-wide text-slate-500">
                      <th className="pb-2 pr-4 font-medium">{t('costDashboard.columnSection')}</th>
                      <th className="pb-2 pr-4 font-medium">{t('costDashboard.columnAgents')}</th>
                      <th className="pb-2 pr-4 font-medium">{t('costDashboard.columnModel')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnIn')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnOut')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnCache')}</th>
                      <th className="pb-2 pr-4 font-medium text-right">{t('costDashboard.columnTotal')}</th>
                      <th className="pb-2 font-medium text-right">{t('costDashboard.columnCost')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5">
                    {capabilityDetail.Sections.map((section) => (
                      <tr key={section.SectionLabel} className="text-slate-300 hover:bg-white/[0.02]">
                        <td className="py-2 pr-4 font-medium text-white">{section.SectionLabel}</td>
                        <td className="py-2 pr-4 text-xs text-slate-400">{section.Agents}</td>
                        <td className="py-2 pr-4 text-xs text-slate-400">{section.Models || '—'}</td>
                        <td className="py-2 pr-4 text-right tabular-nums">{formatTokens(section.InputTokens, locale)}</td>
                        <td className="py-2 pr-4 text-right tabular-nums">{formatTokens(section.OutputTokens, locale)}</td>
                        <td className="py-2 pr-4 text-right tabular-nums">{formatTokens(section.CachedInputTokens, locale)}</td>
                        <td className="py-2 pr-4 text-right font-medium tabular-nums text-white">
                          {formatTokens(section.TotalTokens, locale)}
                        </td>
                        <td className="py-2 text-right font-medium tabular-nums text-amber-300">
                          {formatUsd(section.EstimatedCostUsd, locale)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t border-white/10 font-semibold text-white">
                      <td className="pt-3 pr-4" colSpan={3}>
                        {t('costDashboard.totalCourse')}
                      </td>
                      <td className="pt-3 pr-4 text-right tabular-nums">{formatTokens(capabilityDetail.InputTokens, locale)}</td>
                      <td className="pt-3 pr-4 text-right tabular-nums">{formatTokens(capabilityDetail.OutputTokens, locale)}</td>
                      <td className="pt-3 pr-4 text-right tabular-nums">{formatTokens(capabilityDetail.CachedInputTokens, locale)}</td>
                      <td className="pt-3 pr-4 text-right tabular-nums">{formatTokens(capabilityDetail.TotalTokens, locale)}</td>
                      <td className="pt-3 text-right tabular-nums text-amber-300">{formatUsd(capabilityDetail.EstimatedCostUsd, locale)}</td>
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
  const { t, i18n } = useTranslation();
  const locale = i18n.language === 'en' ? 'en-US' : 'es-MX';
  const [courses, setCourses] = useState<UnifiedCourseCostSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dateFilter, setDateFilter] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState<'all' | 'structured' | 'capability'>('all');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    Promise.all([
      getStructuredLearningDocuments().catch(() => [] as StructuredLearningDocumentSummary[]),
      getCapabilities().catch(() => [] as BackendCapability[]),
      getStructuredLearningCosts().catch(() => [] as StructuredLearningCostSummary[]),
      getCapabilityCosts(dateFilter || undefined).catch(() => [] as BackendCapabilityCostSummary[]),
    ])
      .then(([catalogStructuredDocs, catalogCapabilities, structuredCosts, capabilityCosts]) => {
        if (cancelled) return;

        // Map cost summaries by ID for rapid lookup
        const structuredCostMap = new Map<string, StructuredLearningCostSummary>();
        structuredCosts.forEach((s) => structuredCostMap.set(s.StructuredLearningDocumentId, s));

        const capabilityCostMap = new Map<string, BackendCapabilityCostSummary>();
        capabilityCosts.forEach((c) => capabilityCostMap.set(c.CapabilityId, c));

        // 1. Structured Learning courses from Catalog (only courses belonging to this tenant)
        const structuredItems: UnifiedCourseCostSummary[] = catalogStructuredDocs.map((doc) => {
          const cost = structuredCostMap.get(doc.StructuredLearningDocumentId);
          return {
            id: doc.StructuredLearningDocumentId,
            name: doc.DocumentName,
            type: 'structured',
            itemCount: doc.SectionCount,
            inputTokens: cost?.InputTokens ?? 0,
            outputTokens: cost?.OutputTokens ?? 0,
            cachedInputTokens: cost?.CachedInputTokens ?? 0,
            totalTokens: cost?.TotalTokens ?? 0,
            totalElapsedMilliseconds: cost?.TotalElapsedMilliseconds ?? 0,
            imagesGeneratedCount: cost?.ImagesGeneratedCount ?? 0,
            imageCostUsd: cost?.ImageCostUsd ?? 0,
            videosGeneratedCount: cost?.VideosGeneratedCount ?? 0,
            videoCostUsd: cost?.VideoCostUsd ?? 0,
            estimatedCostUsd: cost?.EstimatedCostUsd ?? 0,
            isEstimate: cost?.IsEstimate ?? false,
            generatedDate: cost?.GeneratedDate ?? doc.CreatedDate,
            studioUrl: `/structured-learning/${doc.StructuredLearningDocumentId}/course?mode=edit`,
          };
        });

        // 2. Capabilities from Catalog (only courses belonging to this tenant)
        const capabilityItems: UnifiedCourseCostSummary[] = catalogCapabilities.map((cap) => {
          const cost = capabilityCostMap.get(cap.CapabilityId);
          const studioUrl =
            cap.LearningMode === 'TraditionalChapters'
              ? `/capabilities/${cap.CapabilityId}/traditional-course`
              : `/capabilities/${cap.CapabilityId}/preview`;

          return {
            id: cap.CapabilityId,
            name: cap.Name,
            type: 'capability',
            learningMode: cap.LearningMode,
            itemCount: cap.LevelCount || cap.Levels?.length || 0,
            inputTokens: cost?.InputTokens ?? 0,
            outputTokens: cost?.OutputTokens ?? 0,
            cachedInputTokens: cost?.CachedInputTokens ?? 0,
            totalTokens: cost?.TotalTokens ?? 0,
            totalElapsedMilliseconds: 0,
            imagesGeneratedCount: cost?.ImagesGeneratedCount ?? 0,
            imageCostUsd: 0,
            videosGeneratedCount: 0,
            videoCostUsd: 0,
            estimatedCostUsd: cost?.EstimatedCostUsd ?? 0,
            isEstimate: cost?.IsEstimate ?? false,
            generatedDate: cost?.GeneratedDate ?? cap.CreatedDate,
            studioUrl,
          };
        });

        const combined = [...structuredItems, ...capabilityItems].sort((a, b) => {
          const dateA = a.generatedDate ? new Date(a.generatedDate).getTime() : 0;
          const dateB = b.generatedDate ? new Date(b.generatedDate).getTime() : 0;
          return dateB - dateA;
        });

        setCourses(combined);
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

  const filteredCourses = useMemo(() => {
    return courses.filter((c) => {
      if (typeFilter !== 'all' && c.type !== typeFilter) return false;
      if (dateFilter && c.generatedDate) {
        const itemDate = c.generatedDate.slice(0, 10);
        if (itemDate !== dateFilter) return false;
      }
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase();
        if (!c.name.toLowerCase().includes(query)) return false;
      }
      return true;
    });
  }, [courses, typeFilter, dateFilter, searchQuery]);

  const totals = useMemo(() => {
    return filteredCourses.reduce(
      (acc, s) => ({
        input: acc.input + s.inputTokens,
        output: acc.output + s.outputTokens,
        images: acc.images + s.imagesGeneratedCount,
        videos: acc.videos + s.videosGeneratedCount,
        elapsed: acc.elapsed + s.totalElapsedMilliseconds,
        cost: acc.cost + s.estimatedCostUsd,
        count: acc.count + 1,
        engrams: acc.engrams + (s.type === 'structured' ? (s.itemCount ?? 0) : 0),
        chapters: acc.chapters + (s.type === 'capability' ? (s.itemCount ?? 0) : 0),
      }),
      { input: 0, output: 0, images: 0, videos: 0, elapsed: 0, cost: 0, count: 0, engrams: 0, chapters: 0 },
    );
  }, [filteredCourses]);

  return (
    <div>
      <section className="relative overflow-hidden border-b border-white/10">
        <div className="absolute inset-0 bg-mesh-gradient opacity-60" />
        <div className="relative mx-auto max-w-7xl px-6 py-14 text-center">
          <span className="inline-flex items-center gap-1.5 rounded-full border border-amber-500/30 bg-amber-500/10 px-3.5 py-1.5 text-xs font-semibold text-amber-300">
            <Coins className="h-3.5 w-3.5 text-amber-400" />
            {t('costDashboard.badge')}
          </span>
          <h1 className="mt-5 text-4xl font-extrabold tracking-tight text-white sm:text-5xl">
            {t('costDashboard.title')} <span className="shimmer-text">{t('costDashboard.titleHighlight')}</span>
          </h1>
          <p className="mx-auto mt-3 max-w-2xl text-base text-slate-400 sm:text-lg">
            {t('costDashboard.subtitle')}
          </p>

          {!loading && !error && courses.length > 0 && (
            <div className="mx-auto mt-8 grid max-w-5xl grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-8">
              <StatPill label={t('costDashboard.totalCourses')} value={String(totals.count)} />
              <StatPill label={t('costDashboard.totalEngrams')} value={formatTokens(totals.engrams, locale)} />
              <StatPill label={t('costDashboard.totalChapters')} value={formatTokens(totals.chapters, locale)} />
              <StatPill label={t('costDashboard.input')} value={formatTokens(totals.input, locale)} />
              <StatPill label={t('costDashboard.output')} value={formatTokens(totals.output, locale)} />
              <StatPill label={t('costDashboard.images')} value={String(totals.images)} />
              <StatPill label={t('costDashboard.totalTime')} value={formatDuration(totals.elapsed)} />
              <StatPill label={t('costDashboard.totalCost')} value={formatUsd(totals.cost, locale)} accent />
            </div>
          )}
        </div>
      </section>

      <section className="mx-auto max-w-5xl px-6 py-8">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-white/10 bg-white/[0.03] p-4">
          <div className="flex flex-1 min-w-[200px] items-center gap-2">
            <Search className="h-4 w-4 text-slate-500" />
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder={t('costDashboard.searchPlaceholder')}
              className="w-full bg-transparent text-sm text-white placeholder:text-slate-500 focus:outline-none"
            />
            {searchQuery && (
              <button
                type="button"
                onClick={() => setSearchQuery('')}
                className="text-slate-500 hover:text-white"
                aria-label="Clear search"
              >
                <X className="h-4 w-4" />
              </button>
            )}
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <div className="flex rounded-lg border border-white/10 bg-black/30 p-0.5 text-xs">
              <button
                type="button"
                onClick={() => setTypeFilter('all')}
                className={`rounded-md px-2.5 py-1 transition-colors ${
                  typeFilter === 'all' ? 'bg-white/15 text-white font-medium' : 'text-slate-400 hover:text-white'
                }`}
              >
                {t('costDashboard.allTypes')}
              </button>
              <button
                type="button"
                onClick={() => setTypeFilter('structured')}
                className={`rounded-md px-2.5 py-1 transition-colors ${
                  typeFilter === 'structured' ? 'bg-emerald-500/20 text-emerald-300 font-medium' : 'text-slate-400 hover:text-white'
                }`}
              >
                {t('costDashboard.typeStructured')}
              </button>
              <button
                type="button"
                onClick={() => setTypeFilter('capability')}
                className={`rounded-md px-2.5 py-1 transition-colors ${
                  typeFilter === 'capability' ? 'bg-violet-500/20 text-violet-300 font-medium' : 'text-slate-400 hover:text-white'
                }`}
              >
                {t('costDashboard.typeCapability')}
              </button>
            </div>

            <div className="flex items-center gap-2">
              <input
                id="cost-date-filter"
                type="date"
                value={dateFilter}
                onChange={(e) => setDateFilter(e.target.value)}
                className="rounded-lg border border-white/10 bg-black/30 px-2.5 py-1 text-xs text-white outline-none focus:border-indigo-400/50 [color-scheme:dark]"
              />
              {dateFilter && (
                <button
                  type="button"
                  onClick={() => setDateFilter('')}
                  title={t('costDashboard.removeFilter')}
                  className="flex items-center gap-1 rounded-lg border border-white/10 px-2 py-1 text-xs text-slate-400 transition-colors hover:bg-white/5 hover:text-white"
                >
                  <X className="h-3 w-3" />
                </button>
              )}
            </div>
          </div>
        </div>

        {loading && <LoadingSpinner label={t('costDashboard.loading')} />}

        {!loading && error && (
          <div className="rounded-2xl border border-red-500/20 bg-red-500/5 p-6 text-center text-red-300">
            {t('costDashboard.loadError', { error })}
          </div>
        )}

        {!loading && !error && courses.length === 0 && (
          <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-400">
            {dateFilter ? t('costDashboard.noResultsForDay') : t('costDashboard.noResultsAtAll')}
          </div>
        )}

        {!loading && !error && courses.length > 0 && filteredCourses.length === 0 && (
          <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-12 text-center text-slate-400">
            {t('costDashboard.noResultsFiltered')}
          </div>
        )}

        {!loading && !error && filteredCourses.length > 0 && (
          <div className="space-y-4">
            {filteredCourses.map((course) => (
              <CourseCostCard key={`${course.type}-${course.id}`} summary={course} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function StatPill({ label, value, accent }: { label: string; value: string; accent?: boolean }) {
  return (
    <div className="rounded-2xl border border-white/10 bg-white/[0.03] px-3.5 py-3 text-center">
      <div className={`text-base font-bold sm:text-lg ${accent ? 'text-amber-400' : 'text-white'}`}>{value}</div>
      <div className="mt-0.5 text-[10px] sm:text-[11px] text-slate-500">{label}</div>
    </div>
  );
}
