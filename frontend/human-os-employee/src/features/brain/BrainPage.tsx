import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { useTranslation } from 'react-i18next';
import {
  Brain,
  Search,
  Bot,
  LayoutGrid,
  Share2,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { BrainGraph } from './BrainGraph';
import { BrainRecallGrid } from './BrainRecallGrid';
import { EngramDetailPanel } from './EngramDetailPanel';
import { MemoryScoreGauge } from './MemoryScoreGauge';
import { MemoryScienceExplainer } from './MemoryScienceExplainer';
import { ReviewQueueSection } from './ReviewQueueSection';
import { getSubjectColor } from './subjectColor';
import { computeGlobalMemoryScore, getRetrievability, isAutomatic } from './memoryScience';
import { useBrainGraph } from './useBrainGraph';
import type { SelectedNeuron } from './types';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

/** Below this retrievability, the panel nudges the employee that this memory
 * is fading and a full 5-step redo (not just a chat with the agent) will
 * rebuild it more strongly — mirrors the reviewCount-driven stability model
 * in memoryScience.ts. */
const FADING_THRESHOLD = 0.5;

/** Spanish labels for Capability.Category, matching the employee sidebar's
 * own 4 career-plan track labels (EmployeeSidebar.tsx's COURSE_CATEGORY_LINKS)
 * so "tipo de curso" reads the same everywhere in the app. */
const CATEGORY_LABELS: Record<'en' | 'es', Record<string, string>> = {
  es: {
    'job-skills': 'Mi Puesto',
    'soft-skills': 'Soft Skills',
    'department-processes': 'Procesos de Depto.',
    'hr-processes': 'Procesos de RRHH',
  },
  en: {
    'job-skills': 'My Role',
    'soft-skills': 'Soft Skills',
    'department-processes': 'Department Processes',
    'hr-processes': 'HR Processes',
  },
};

type StatusFilter = 'all' | 'mastered' | 'needs-review';

/** One row of "pill" filter chips (Todas + one per option) — shared by the
 * área/curso/tipo de curso/estado filters (2026-08-02) to avoid repeating
 * the same markup four times. */
function FilterChipRow({
  label,
  allLabel,
  options,
  value,
  onChange,
}: {
  label: string;
  allLabel: string;
  options: { value: string; label: string; color?: string }[];
  value: string | null;
  onChange: (value: string | null) => void;
}) {
  return (
    <div className="mt-3 flex flex-wrap items-center gap-2">
      <span className="text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-slate-500">
        {label}
      </span>
      <button
        type="button"
        onClick={() => onChange(null)}
        className={`rounded-full px-3 py-1 text-xs font-medium transition ${
          value === null
            ? 'bg-gradient-to-r from-brand-500 to-accent-500 text-white shadow'
            : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-white/[0.06] dark:text-slate-300 dark:hover:bg-white/[0.12]'
        }`}
      >
        {allLabel}
      </button>
      {options.map((opt) => (
        <button
          key={opt.value}
          type="button"
          onClick={() => onChange(value === opt.value ? null : opt.value)}
          className={`flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium transition ${
            value === opt.value
              ? 'text-white shadow'
              : 'bg-slate-100 text-slate-600 hover:bg-slate-200 dark:bg-white/[0.06] dark:text-slate-300 dark:hover:bg-white/[0.12]'
          }`}
          style={value === opt.value ? { background: opt.color ?? 'linear-gradient(to right, #6366f1, #a855f7)' } : undefined}
        >
          {opt.color && <span className="h-2 w-2 rounded-full" style={{ background: opt.color }} />}
          {opt.label}
        </button>
      ))}
    </div>
  );
}

/** "Mi Cerebro" — employee portal's mastered-engrams dashboard, ported from
 * human-os-web's /brain (2026-08-01). Every mastered CapabilityGraphNode
 * across the employee's assigned capabilities is shown as a card with a
 * live "memory strength" ring driven by a real forgetting-curve algorithm
 * (memoryScience.ts) — the fading-memory feature the employee portal was
 * missing. Trimmed vs. the original: no force-directed graph view toggle
 * (grid view only, which is the default there too) and no Programs
 * grouping (this portal has no Programs concept — every root is one
 * assigned capability). */
export function BrainPage() {
  const { language } = useI18n();
  const { t } = useTranslation();
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const { data: rootGroups = [], isLoading: loading } = useBrainGraph(personId);
  const [query, setQuery] = useState('');

  const [selectedNode, setSelectedNode] = useState<SelectedNeuron | null>(null);
  const [view, setView] = useState<'grid' | 'graph'>('grid');
  const [subjectFilter, setSubjectFilter] = useState<string | null>(null);
  const [courseFilter, setCourseFilter] = useState<string | null>(null);
  const [categoryFilter, setCategoryFilter] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  const totalNeurons = useMemo(
    () => rootGroups.reduce((sum, r) => sum + r.clusters.reduce((s, c) => s + c.nodes.length, 0), 0),
    [rootGroups],
  );
  const totalAreas = rootGroups.length;
  const memoryScore = useMemo(() => computeGlobalMemoryScore(rootGroups), [rootGroups]);
  const subjectOptions = useMemo(() => {
    const bySubject = new Map<string, string>();
    rootGroups.forEach((r) =>
      r.clusters.forEach((c) => {
        if (c.subjectCode && c.subjectName) {
          bySubject.set(c.subjectCode, c.subjectName);
        }
      }),
    );
    return Array.from(bySubject.entries())
      .map(([code, name]) => ({ code, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [rootGroups]);

  // "Por curso" (2026-08-02) — every distinct capability that has at least
  // one mastered node, regardless of subject/category grouping.
  const courseOptions = useMemo(() => {
    const byCourse = new Map<string, string>();
    rootGroups.forEach((r) =>
      r.clusters.forEach((c) => {
        byCourse.set(c.capabilityId, c.capabilityName);
      }),
    );
    return Array.from(byCourse.entries())
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [rootGroups]);

  // "Por tipo de curso" (2026-08-02) — the employer career-plan track
  // (Capability.Category), only shown when at least one mastered node's
  // capability actually has one set (personal/consumer courses have none).
  const categoryOptions = useMemo(() => {
    const categories = new Set<string>();
    rootGroups.forEach((r) =>
      r.clusters.forEach((c) => {
        if (c.category) categories.add(c.category);
      }),
    );
    return Array.from(categories)
      .map((code) => ({ code, name: CATEGORY_LABELS[language]?.[code] ?? code }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [rootGroups, language]);

  const filteredRootGroups = useMemo(() => {
    const hasDateFilter = dateFrom !== '' || dateTo !== '';
    return rootGroups
      .map((r) => ({
        ...r,
        clusters: r.clusters
          .filter((c) => !subjectFilter || c.subjectCode === subjectFilter)
          .filter((c) => !courseFilter || c.capabilityId === courseFilter)
          .filter((c) => !categoryFilter || c.category === categoryFilter)
          .map((c) => ({
            ...c,
            nodes:
              statusFilter === 'all' && !hasDateFilter
                ? c.nodes
                : c.nodes.filter((n) => {
                    if (statusFilter !== 'all') {
                      const automatic = isAutomatic(n.reviewCount ?? 0);
                      const retrievability = getRetrievability(n.lastCompletedDate, n.reviewCount ?? 1);
                      const needsReview = !automatic && (retrievability === null || retrievability < FADING_THRESHOLD);
                      if (statusFilter === 'mastered' && needsReview) return false;
                      if (statusFilter === 'needs-review' && !needsReview) return false;
                    }
                    if (hasDateFilter) {
                      const reviewDate = n.lastCompletedDate?.slice(0, 10);
                      if (!reviewDate) return false;
                      if (dateFrom && reviewDate < dateFrom) return false;
                      if (dateTo && reviewDate > dateTo) return false;
                    }
                    return true;
                  }),
          }))
          .filter((c) => c.nodes.length > 0),
      }))
      .filter((r) => r.clusters.length > 0);
  }, [rootGroups, subjectFilter, courseFilter, categoryFilter, statusFilter, dateFrom, dateTo]);

  const totalNeedsReview = useMemo(() => {
    return rootGroups.reduce(
      (sum, r) =>
        sum +
        r.clusters.reduce((s, c) => {
          return (
            s +
            c.nodes.filter((n) => {
              if (isAutomatic(n.reviewCount ?? 0)) return false; // near-permanent, never "needs review"
              const retrievability = getRetrievability(n.lastCompletedDate, n.reviewCount ?? 1);
              return retrievability === null || retrievability < FADING_THRESHOLD;
            }).length
          );
        }, 0),
      0,
    );
  }, [rootGroups]);

  function handleSelectNode(node: SelectedNeuron) {
    setSelectedNode(node);
  }

  const isEmpty = !loading && totalNeurons === 0;

  return (
    <div className="mx-auto max-w-6xl p-4 sm:p-8">
      <div className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-accent-500 to-brand-600 text-white shadow-lg shadow-accent-500/30">
          <Brain className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">{t('brain.title')}</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {t('brain.subtitle')}
          </p>
        </div>
      </div>

      <MemoryScienceExplainer />

      {!isEmpty && (
        <div className="mt-6 flex flex-wrap items-stretch gap-3">
          <div className="grid grid-cols-3 gap-3 sm:max-w-lg">
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">{t('brain.activeNeurons')}</p>
              <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">{totalNeurons}</p>
            </Card>
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">{t('brain.knowledgeAreas')}</p>
              <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">{totalAreas}</p>
            </Card>
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">{t('brain.dueForReview')}</p>
              <p className="mt-1 text-2xl font-bold text-amber-500 dark:text-amber-400">{totalNeedsReview}</p>
            </Card>
          </div>
          <MemoryScoreGauge
            score={memoryScore}
            label={t('brain.memoryStrength')}
            caption={t('brain.memoryStrengthCaption')}
          />
        </div>
      )}

      {!isEmpty && <ReviewQueueSection personId={personId} />}

      {!isEmpty && (
        <Card className="mt-4 flex items-start gap-3 border-dashed p-4">
          <Bot className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" />
          <p className="text-sm text-slate-500 dark:text-slate-400">
            {t('brain.askComingSoon')}
          </p>
        </Card>
      )}

      {loading && (
        <LoadingBackdrop
          title={t('brain.loadingTitle')}
          description={t('brain.loadingDescription')}
        />
      )}

      {isEmpty && (
        <Card className="mt-8 flex flex-col items-center gap-3 p-10 text-center">
          <Brain className="h-10 w-10 text-slate-300 dark:text-slate-600" />
          <p className="text-base font-semibold text-slate-900 dark:text-white">{t('brain.emptyTitle')}</p>
          <p className="max-w-sm text-sm text-slate-500 dark:text-slate-400">
            {t('brain.emptyDescription')}
          </p>
          <Link
            to="/capabilities"
            className="mt-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-accent-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
          >
            {t('brain.exploreButton')}
          </Link>
        </Card>
      )}

      {!loading && !isEmpty && (
        <>
          <div className="mt-6 flex flex-wrap items-center gap-3">
            <div className="relative max-w-md flex-1">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
              <input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={t('brain.searchPlaceholder')}
                className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-9 pr-3 text-sm text-slate-900 shadow-sm outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
              />
            </div>
            <div className="flex items-center gap-1 rounded-xl border border-slate-200 bg-white p-1 dark:border-white/10 dark:bg-white/[0.03]">
              <button
                type="button"
                onClick={() => setView('grid')}
                className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition ${
                  view === 'grid'
                    ? 'bg-gradient-to-r from-brand-500 to-accent-500 text-white shadow'
                    : 'text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white'
                }`}
              >
                <LayoutGrid className="h-3.5 w-3.5" />
                {t('brain.grid')}
              </button>
              <button
                type="button"
                onClick={() => setView('graph')}
                className={`flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-medium transition ${
                  view === 'graph'
                    ? 'bg-gradient-to-r from-brand-500 to-accent-500 text-white shadow'
                    : 'text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white'
                }`}
              >
                <Share2 className="h-3.5 w-3.5" />
                {t('brain.graph')}
              </button>
            </div>
          </div>

          {subjectOptions.length > 1 && (
            <FilterChipRow
              label={`${t('brain.subject')}:`}
              allLabel={t('brain.allSubjects')}
              value={subjectFilter}
              onChange={setSubjectFilter}
              options={subjectOptions.map((s) => ({ value: s.code, label: s.name, color: getSubjectColor(s.code) }))}
            />
          )}

          {courseOptions.length > 0 && (
            <FilterChipRow
              label={`${t('brain.course')}:`}
              allLabel={t('brain.all')}
              value={courseFilter}
              onChange={setCourseFilter}
              options={courseOptions.map((c) => ({ value: c.id, label: c.name }))}
            />
          )}

          {categoryOptions.length > 0 && (
            <FilterChipRow
              label={`${t('brain.courseType')}:`}
              allLabel={t('brain.all')}
              value={categoryFilter}
              onChange={setCategoryFilter}
              options={categoryOptions.map((c) => ({ value: c.code, label: c.name }))}
            />
          )}

          <FilterChipRow
            label={`${t('brain.status')}:`}
            allLabel={t('brain.all')}
            value={statusFilter === 'all' ? null : statusFilter}
            onChange={(v) => setStatusFilter((v as StatusFilter) ?? 'all')}
            options={[
              { value: 'mastered', label: t('brain.mastered') },
              { value: 'needs-review', label: t('brain.needsReview') },
            ]}
          />

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <span className="text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-slate-500">
              {t('brain.lastReview')}:
            </span>
            <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
              {t('brain.from')}
              <input
                type="date"
                value={dateFrom}
                onChange={(e) => setDateFrom(e.target.value)}
                max={dateTo || undefined}
                className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-xs text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
              />
            </label>
            <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
              {t('brain.to')}
              <input
                type="date"
                value={dateTo}
                onChange={(e) => setDateTo(e.target.value)}
                min={dateFrom || undefined}
                className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-xs text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
              />
            </label>
            {(dateFrom || dateTo) && (
              <button
                type="button"
                onClick={() => {
                  setDateFrom('');
                  setDateTo('');
                }}
                className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-500 hover:bg-slate-200 dark:bg-white/[0.06] dark:text-slate-300 dark:hover:bg-white/[0.12]"
              >
                {t('brain.clearDates')}
              </button>
            )}
          </div>

          <div className="relative mt-4 h-[560px] w-full overflow-hidden rounded-3xl border border-white/10 bg-[radial-gradient(ellipse_at_center,_#1e1b4b_0%,_#0f172a_55%,_#020617_100%)] shadow-2xl sm:h-[680px]">
            {view === 'grid' ? (
              <BrainRecallGrid rootGroups={filteredRootGroups} query={query} onSelectNeuron={handleSelectNode} />
            ) : (
              <BrainGraph
                personId={personId}
                courseId={courseFilter}
                query={query}
                onSelectNeuron={handleSelectNode}
                onSelectCourse={setCourseFilter}
              />
            )}
          </div>
        </>
      )}

      {selectedNode && <EngramDetailPanel personId={personId} neuron={selectedNode} onClose={() => setSelectedNode(null)} />}
    </div>
  );
}
