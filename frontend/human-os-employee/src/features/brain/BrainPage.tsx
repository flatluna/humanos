import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'framer-motion';
import DOMPurify from 'dompurify';
import { useTranslation } from 'react-i18next';
import {
  Brain,
  Search,
  Sparkles,
  X,
  Bot,
  Layers,
  LayoutGrid,
  Share2,
  RotateCcw,
  AlertTriangle,
  Shuffle,
  Video,
  ChevronDown,
  ChevronUp,
  Zap,
  Target,
  Trophy,
  Calendar,
} from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { getNodeSummary, type NodeSummaryDto } from '@/features/capabilities/api/runtimeSessionApi';
import {
  getStructuredLearningDocument,
  getStructuredLearningSectionSummary,
  listStructuredLearningSectionVideos,
  type StructuredLearningSectionSummaryDto,
  type StructuredLearningSectionVideo,
} from '@/features/capabilities/api/structuredLearningRuntimeApi';
import { API_BASE_URL } from '@/features/capabilities/api/httpClient';
import EngramReviewAgent from '@/features/capabilities/components/EngramReviewAgent';
import VoiceTutorAgent from '@/features/capabilities/components/VoiceTutorAgent';
import { BrainGraph } from './BrainGraph';
import { BrainRecallGrid } from './BrainRecallGrid';
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

function SummaryStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-white/5 p-3">
      <p className="text-[11px] uppercase tracking-wide text-white/50">{label}</p>
      <p className="mt-0.5 text-sm font-semibold text-white">{value}</p>
    </div>
  );
}

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

/** Sanitized rich-text renderer for ExampleCard.Text — same restricted tag
 * allowlist used elsewhere in the app (NodeWorkflowPage's RichContent). */
function RichText({ html, className }: { html: string; className?: string }) {
  const sanitized = useMemo(
    () =>
      DOMPurify.sanitize(html, {
        ALLOWED_TAGS: ['p', 'br', 'strong', 'b', 'em', 'i', 'ul', 'ol', 'li', 'a'],
        ALLOWED_ATTR: ['href'],
      }),
    [html],
  );
  return (
    <div
      className={className}
      style={{ whiteSpace: 'pre-wrap' }}
      dangerouslySetInnerHTML={{ __html: sanitized }}
    />
  );
}

type DeepDiveCardView = {
  index: number;
  title: string;
  text: string;
  emoji?: string;
  illustrationId?: string | null;
  illustrationIds?: string[] | null;
  isStructuredLearning?: boolean;
};

/** Full detail of one Deep Dive card, launched from the "Tarjetas de
 * repaso" grid — lets the student read the card again and, right in this
 * SAME window (2026-08-02 fix — previously this closed and opened a whole
 * second full-screen modal, which felt like being sent back to a generic
 * lesson review instead of staying on this exact card), activate the
 * Agent Engrama to explain THAT specific card. The illustration/title
 * never change when the agent activates — only the body switches from the
 * card text to the embedded agent controls. */
function DeepDiveCardModal({
  card,
  personId,
  capabilityGraphNodeId,
  structuredLearningDeepDiveStepId,
  agentActive,
  onClose,
  onAskAgent,
}: {
  card: DeepDiveCardView;
  personId: string;
  capabilityGraphNodeId?: string;
  structuredLearningDeepDiveStepId?: string | null;
  agentActive: boolean;
  onClose: () => void;
  onAskAgent: () => void;
}) {
  const { language } = useI18n();
  const imageSrc = card.illustrationId
    ? card.isStructuredLearning
      ? `${API_BASE_URL}/illustrations/structured-learning/${card.illustrationId}/image`
      : `${API_BASE_URL}/illustrations/${card.illustrationId}/image`
    : null;

  return createPortal(
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm">
      <div className="relative w-full max-w-md rounded-3xl border border-white/10 bg-slate-900 p-6 shadow-2xl max-h-[90vh] overflow-y-auto">
        <button
          type="button"
          onClick={onClose}
          className="absolute right-4 top-4 rounded-full p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
          aria-label={language === 'en' ? 'Close' : 'Cerrar'}
        >
          <X className="h-5 w-5" />
        </button>

        {imageSrc ? (
          <img
            src={imageSrc}
            alt={card.title}
            className="mb-3 h-auto max-h-56 w-full rounded-xl object-contain bg-black/20"
          />
        ) : (
          card.emoji && <div className="mb-3 text-4xl leading-none">{card.emoji}</div>
        )}

        <h3 className="text-base font-semibold text-white">{card.title}</h3>

        {!agentActive && <RichText className="mt-2 text-sm text-white/70" html={card.text} />}

        {agentActive ? (
          card.isStructuredLearning && structuredLearningDeepDiveStepId ? (
            <div className="mt-4">
              <VoiceTutorAgent
                structuredLearningSectionStepId={structuredLearningDeepDiveStepId}
                exampleCardIndex={card.index}
              />
            </div>
          ) : capabilityGraphNodeId ? (
            <EngramReviewAgent
              personId={personId}
              capabilityGraphNodeId={capabilityGraphNodeId}
              exampleCardIndex={card.index}
              embedded
              onExit={onClose}
            />
          ) : null
        ) : (
          <button
            type="button"
            onClick={onAskAgent}
            className="mt-5 flex w-full items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-accent-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
          >
            <Sparkles className="h-4 w-4" />
            {language === 'en' ? 'Ask the Agent to explain it' : 'Que el agente me lo explique'}
          </button>
        )}
      </div>
    </div>,
    document.body,
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
  const [summary, setSummary] = useState<NodeSummaryDto | null>(null);
  const [slSummary, setSlSummary] = useState<StructuredLearningSectionSummaryDto | null>(null);
  const [rewardBreakdownOpen, setRewardBreakdownOpen] = useState(false);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [slDeepDiveCards, setSlDeepDiveCards] = useState<DeepDiveCardView[]>([]);
  const [slDeepDiveStepId, setSlDeepDiveStepId] = useState<string | null>(null);
  const [slTeachingStepId, setSlTeachingStepId] = useState<string | null>(null);
  const [slTeachingIllustrationId, setSlTeachingIllustrationId] = useState<string | null>(null);
  const [slVideos, setSlVideos] = useState<StructuredLearningSectionVideo[]>([]);
  const [engramOpen, setEngramOpen] = useState(false);
  const [cardModalIndex, setCardModalIndex] = useState<number | null>(null);
  const [cardAgentActive, setCardAgentActive] = useState(false);
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
    setCardModalIndex(null);
    setCardAgentActive(false);
    setRewardBreakdownOpen(false);

    if (node.isStructuredLearning) {
      setSummary(null);
      setSlSummary(null);
      setSlDeepDiveCards([]);
      setSlDeepDiveStepId(null);
      setSlTeachingStepId(null);
      setSlTeachingIllustrationId(null);
      setSlVideos([]);
      setSummaryLoading(true);

      getStructuredLearningSectionSummary(node.structuredLearningSectionId, personId)
        .then((data) => setSlSummary(data))
        .catch(() => setSlSummary(null));

      getStructuredLearningDocument(node.structuredLearningDocumentId)
        .then((doc) => {
          const section = doc.Sections.find((s) => s.StructuredLearningDocumentSectionId === node.structuredLearningSectionId);
          const deepDiveStep = section?.Steps.find((s) => s.StepType === 'DeepDive');
          const teachingStep = section?.Steps.find((s) => s.StepType === 'Teaching');
          setSlTeachingStepId(teachingStep?.StructuredLearningSectionStepId ?? null);
          setSlDeepDiveStepId(deepDiveStep?.StructuredLearningSectionStepId ?? null);
          if (teachingStep?.StepDataJson) {
            try {
              setSlTeachingIllustrationId((JSON.parse(teachingStep.StepDataJson) as { IllustrationId?: string | null }).IllustrationId ?? null);
            } catch {
              setSlTeachingIllustrationId(null);
            }
          }
          if (deepDiveStep?.StepDataJson) {
            try {
              const cards = JSON.parse(deepDiveStep.StepDataJson) as { Title: string; Text: string; Emoji?: string | null; IllustrationId?: string | null; IllustrationIds?: string[] | null }[];
              setSlDeepDiveCards(
                cards.map((c, index) => ({
                  index,
                  title: c.Title,
                  text: c.Text,
                  emoji: c.Emoji ?? undefined,
                  illustrationId: c.IllustrationId,
                  illustrationIds: c.IllustrationIds,
                  isStructuredLearning: true,
                }))
              );
            } catch {
              setSlDeepDiveCards([]);
            }
          }
        })
        .catch(() => undefined)
        .finally(() => setSummaryLoading(false));

      listStructuredLearningSectionVideos(node.structuredLearningSectionId)
        .then((videos) => {
          setSlVideos(videos.filter((v) => v.IsPublished));
        })
        .catch(() => setSlVideos([]));

      return;
    }

    setSummary(null);
    setSlSummary(null);
    setSummaryLoading(true);
    getNodeSummary(personId, node.capabilityGraphNodeId)
      .then((data) => setSummary(data))
      .catch(() => setSummary(null))
      .finally(() => setSummaryLoading(false));
  }

  // "Ver todas las tarjetas" (2026-08-02) — the Deep Dive step's already-
  // generated ExampleCards, already fetched via getNodeSummary above, so
  // browsing them needs no extra request. Only the DeepDive step ever has
  // cards; every other step type's ExampleCards field is undefined.
  const deepDiveCards: DeepDiveCardView[] = useMemo(() => {
    if (selectedNode?.isStructuredLearning) return slDeepDiveCards;
    const step = summary?.Steps.find((s) => s.StepType === 'DeepDive');
    return (step?.ExampleCards ?? []).map((c, index) => ({
      index,
      title: c.Title,
      text: c.Text,
      emoji: c.Emoji,
      illustrationId: c.IllustrationId,
      isStructuredLearning: false,
    }));
  }, [summary, selectedNode, slDeepDiveCards]);

  function openRandomCard() {
    if (deepDiveCards.length === 0) return;
    setCardAgentActive(false);
    setCardModalIndex(Math.floor(Math.random() * deepDiveCards.length));
  }

  const activeMemory = selectedNode?.isStructuredLearning ? slSummary?.Memory : summary?.Memory;
  const activeReward = selectedNode?.isStructuredLearning ? slSummary?.Reward : summary?.Reward;
  const activeImpact = selectedNode?.isStructuredLearning ? slSummary?.Impact : summary?.Impact;

  const formatFriendlyDate = (dateStr?: string | null) => {
    if (!dateStr) return '';
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString(language === 'en' ? 'en-US' : 'es-MX', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
      });
    } catch {
      return dateStr;
    }
  };

  const formatDaysUntil = (targetDateStr?: string | null) => {
    if (!targetDateStr) return '';
    try {
      const target = new Date(targetDateStr).getTime();
      const diffDays = Math.max(0, Math.round((target - Date.now()) / (24 * 60 * 60 * 1000)));
      if (diffDays === 0) {
        return t('brain.today');
      }
      if (diffDays === 1) {
        return t('brain.inOneDay');
      }
      return t('brain.inDays', { count: diffDays });
    } catch {
      return '';
    }
  };

  const getTierBadgeStyle = (tier?: string | null) => {
    const norm = (tier || '').toLowerCase().trim();
    if (norm === 'critical') {
      return 'bg-red-500/20 text-red-300 border-red-500/30';
    }
    if (norm === 'important') {
      return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
    }
    return 'bg-slate-500/20 text-slate-300 border-slate-500/30';
  };

  const getMemoryStateBadgeStyle = (state?: string | null) => {
    const norm = (state || '').toLowerCase().trim();
    if (norm === 'strong') {
      return 'text-emerald-400 bg-emerald-500/15 border-emerald-500/30';
    }
    if (norm === 'fading') {
      return 'text-amber-400 bg-amber-500/15 border-amber-500/30';
    }
    return 'text-red-400 bg-red-500/15 border-red-500/30';
  };

  const selectedRetrievability = selectedNode
    ? getRetrievability(selectedNode.lastCompletedDate, selectedNode.reviewCount ?? 1)
    : null;
  const selectedIsFading = selectedRetrievability !== null && selectedRetrievability < FADING_THRESHOLD;

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

      <AnimatePresence>
        {selectedNode && (
          <>
            <motion.div
              key="backdrop"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="fixed inset-0 z-40 bg-black/40"
              onClick={() => setSelectedNode(null)}
            />
            <motion.div
              key="panel"
              initial={{ x: 400, opacity: 0 }}
              animate={{ x: 0, opacity: 1 }}
              exit={{ x: 400, opacity: 0 }}
              transition={{ type: 'tween', duration: 0.25 }}
              className="fixed right-0 top-0 z-50 h-full w-full overflow-y-auto bg-slate-900 p-6 shadow-2xl sm:w-96"
            >
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-fuchsia-300">
                  <Layers className="h-3.5 w-3.5" />
                  {selectedNode.capabilityName}
                </div>
                <button
                  type="button"
                  onClick={() => setSelectedNode(null)}
                  className="rounded-full p-1 text-white/60 hover:bg-white/10 hover:text-white"
                  aria-label={language === 'en' ? 'Close' : 'Cerrar'}
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              <h2 className="mt-2 text-xl font-semibold text-white">{selectedNode.name}</h2>
              {selectedNode.description && <p className="mt-2 text-sm text-white/70">{selectedNode.description}</p>}

              {selectedIsFading && (
                <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-400/30 bg-amber-400/10 p-3">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" />
                  <p className="text-xs text-amber-200">
                    {t('brain.fadingWarning')}
                  </p>
                </div>
              )}

              {/* ENRICHED SECTIONS: MEMORY + REWARD + IMPACT */}
              <div className="mt-5 space-y-3">
                {/* 1. MEMORY SECTION */}
                {activeMemory && (
                  <div className="overflow-hidden rounded-2xl border border-emerald-500/25 bg-gradient-to-br from-emerald-500/10 via-emerald-500/[0.03] to-white/[0.02] p-3.5 shadow-sm">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-1.5 text-xs font-black uppercase tracking-wider text-emerald-400">
                        <Brain className="h-4 w-4 text-emerald-400" />
                        <span>{t('brain.blocks.memory')}</span>
                      </div>
                      <span
                        className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide ${getMemoryStateBadgeStyle(
                          activeMemory.MemoryState
                        )}`}
                      >
                        <Sparkles className="h-2.5 w-2.5" />
                        {activeMemory.MemoryState}
                      </span>
                    </div>

                    <div className="mt-3 flex items-center justify-between">
                      <div className="flex items-baseline gap-1.5">
                        <span className="text-2xl font-black text-white">{activeMemory.RecallPercent}%</span>
                        <span className="text-[11px] text-white/50">
                          {t('brain.retention')}
                        </span>
                      </div>

                      <div className="text-right">
                        {activeMemory.IsAutomatic ? (
                          <span className="inline-flex items-center gap-1 rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-extrabold text-amber-300">
                            <Zap className="h-3 w-3 fill-amber-300" />
                            {t('brain.automatic')}
                          </span>
                        ) : (
                          <span className="text-[11px] font-medium text-white/70">
                            {activeMemory.ReviewsUntilAutomatic}{' '}
                            {t('brain.reviewsToAuto')}
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="mt-3 flex items-center justify-between border-t border-white/5 pt-2 text-[11px] text-white/60">
                      <span>
                        {t('brain.reviewCount', { count: activeMemory.ReviewCount })}
                      </span>
                      {activeMemory.NextReviewDate && (
                        <span className="flex items-center gap-1 font-medium text-emerald-300/90">
                          <Calendar className="h-3 w-3" />
                          {t('brain.nextReview')}:{' '}
                          {formatFriendlyDate(activeMemory.NextReviewDate)} ({formatDaysUntil(activeMemory.NextReviewDate)})
                        </span>
                      )}
                    </div>
                  </div>
                )}

                {/* 2. REWARD SECTION */}
                {activeReward && (
                  <div className="overflow-hidden rounded-2xl border border-amber-500/25 bg-gradient-to-br from-amber-500/10 via-amber-500/[0.03] to-white/[0.02] p-3.5 shadow-sm">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-1.5 text-xs font-black uppercase tracking-wider text-amber-400">
                        <Trophy className="h-4 w-4 text-amber-400" />
                        <span>{t('brain.reward')}</span>
                      </div>

                      {activeReward.WasSurprise && activeReward.SurpriseType !== 'NONE' ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-gradient-to-r from-amber-500/30 to-purple-500/30 px-2 py-0.5 text-[10px] font-extrabold uppercase tracking-wide text-amber-200">
                          {activeReward.SurpriseType === 'JACKPOT'
                            ? '🎉 JACKPOT'
                            : activeReward.SurpriseType === 'DOUBLE'
                            ? '⚡ DOUBLE'
                            : '🎲 BONUS'}
                        </span>
                      ) : (
                        <span className="text-[10px] text-white/40 font-medium">
                          {formatFriendlyDate(activeReward.EarnedAt)}
                        </span>
                      )}
                    </div>

                    <div className="mt-3 flex items-baseline justify-between">
                      <div className="flex items-baseline gap-1">
                        <span className="text-2xl font-black text-amber-300">+{activeReward.PointsAwarded}</span>
                        <span className="text-xs font-medium text-amber-400/80">pts</span>
                      </div>

                      <button
                        type="button"
                        onClick={() => setRewardBreakdownOpen((prev) => !prev)}
                        className="inline-flex items-center gap-1 text-[11px] font-semibold text-amber-300 hover:text-amber-200"
                      >
                        <span>{t('brain.viewBreakdown')}</span>
                        {rewardBreakdownOpen ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
                      </button>
                    </div>

                    {rewardBreakdownOpen && (
                      <motion.div
                        initial={{ opacity: 0, height: 0 }}
                        animate={{ opacity: 1, height: 'auto' }}
                        exit={{ opacity: 0, height: 0 }}
                        className="mt-3 rounded-xl bg-black/30 p-2.5 text-[11px] font-mono text-white/80 border border-white/5 space-y-1"
                      >
                        <div className="flex justify-between text-white/60">
                          <span>{t('brain.basePoints')}:</span>
                          <span className="font-bold text-white">{activeReward.PointsBase}</span>
                        </div>
                        <div className="flex justify-between text-white/60">
                          <span>{t('brain.difficulty')}:</span>
                          <span className="font-bold text-white">×{activeReward.DifficultyMultiplier}</span>
                        </div>
                        <div className="flex justify-between text-white/60">
                          <span>{t('brain.memoryEffort')}:</span>
                          <span className="font-bold text-white">×{activeReward.MemoryEffortMultiplier}</span>
                        </div>
                        <div className="flex justify-between text-white/60">
                          <span>{t('brain.surpriseBonus')}:</span>
                          <span className="font-bold text-white">×{activeReward.SurpriseMultiplier}</span>
                        </div>
                        <div className="mt-1 flex justify-between border-t border-white/10 pt-1 font-bold text-amber-300">
                          <span>{t('brain.total')}:</span>
                          <span>{activeReward.PointsAwarded} pts</span>
                        </div>
                      </motion.div>
                    )}
                  </div>
                )}

                {/* 3. IMPACT SECTION */}
                {activeImpact && (
                  <div className="overflow-hidden rounded-2xl border border-indigo-500/25 bg-gradient-to-br from-indigo-500/10 via-indigo-500/[0.03] to-white/[0.02] p-3.5 shadow-sm">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-1.5 text-xs font-black uppercase tracking-wider text-indigo-400">
                        <Target className="h-4 w-4 text-indigo-400" />
                        <span>{t('brain.roleImpact')}</span>
                      </div>
                      <span
                        className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-extrabold uppercase tracking-wide ${getTierBadgeStyle(
                          activeImpact.Tier
                        )}`}
                      >
                        {activeImpact.Tier}
                      </span>
                    </div>

                    <div className="mt-2 flex items-baseline gap-1.5">
                      <span className="text-xl font-black text-white">{activeImpact.Score}</span>
                      <span className="text-xs text-white/50">/ 100</span>
                    </div>

                    {activeImpact.Reason && (
                      <p className="mt-2 text-xs leading-relaxed text-slate-300 dark:text-white/80 bg-black/20 rounded-xl p-2.5 border border-white/5">
                        {activeImpact.Reason}
                      </p>
                    )}
                  </div>
                )}
              </div>

              {summaryLoading && !selectedNode.isStructuredLearning && <p className="mt-6 text-sm text-white/50">{t('common.loading')}</p>}

              {!summaryLoading && summary && !selectedNode.isStructuredLearning && (
                <div className="mt-6 grid grid-cols-2 gap-3">
                  <SummaryStat label={t('brain.attempts')} value={String(summary.AttemptCount)} />
                  <SummaryStat
                    label={t('brain.score')}
                    value={typeof summary.FinalScore === 'number' ? `${summary.FinalScore}/100` : '—'}
                  />
                  <SummaryStat
                    label={t('brain.masteredSince')}
                    value={
                      summary.FirstCompletedDate
                        ? new Date(summary.FirstCompletedDate).toLocaleDateString(language === 'en' ? 'en-US' : 'es', { dateStyle: 'medium' })
                        : '—'
                    }
                  />
                  <SummaryStat
                    label={t('brain.lastReview')}
                    value={
                      summary.LastCompletedDate
                        ? new Date(summary.LastCompletedDate).toLocaleDateString(language === 'en' ? 'en-US' : 'es', { dateStyle: 'medium' })
                        : '—'
                    }
                  />
                </div>
              )}

              {selectedNode.isStructuredLearning && (
                <div className="mt-6 grid grid-cols-2 gap-3">
                  <SummaryStat
                    label={t('brain.score')}
                    value={
                      typeof slSummary?.FinalScore === 'number'
                        ? `${slSummary.FinalScore}/100`
                        : typeof selectedNode.finalScore === 'number'
                        ? `${selectedNode.finalScore}/100`
                        : '—'
                    }
                  />
                  <SummaryStat
                    label={t('brain.masteredOn')}
                    value={
                      slSummary?.LastCompletedDate
                        ? new Date(slSummary.LastCompletedDate).toLocaleDateString(language === 'en' ? 'en-US' : 'es', { dateStyle: 'medium' })
                        : selectedNode.lastCompletedDate
                        ? new Date(selectedNode.lastCompletedDate).toLocaleDateString(language === 'en' ? 'en-US' : 'es', { dateStyle: 'medium' })
                        : '—'
                    }
                  />
                </div>
              )}

              <button
                type="button"
                onClick={() => setEngramOpen(true)}
                className="mt-6 flex w-full items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-accent-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
              >
                <Sparkles className="h-4 w-4" />
                {t('brain.talkWithAgent')}
              </button>

              {slVideos.length > 0 && (
                <div className="mt-6 space-y-3">
                  <div className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-indigo-300">
                    <Video className="h-4 w-4" />
                    <span>{t('brain.videoLessons')} ({slVideos.length})</span>
                  </div>
                  <div className="space-y-2">
                    {slVideos.map((v) => (
                      <video
                        key={v.IllustrationId}
                        src={`${API_BASE_URL}/illustrations/structured-learning/${v.IllustrationId}/image`}
                        controls
                        className="w-full rounded-xl border border-white/10 bg-black/40 shadow-md"
                      />
                    ))}
                  </div>
                </div>
              )}

              {deepDiveCards.length > 0 && (
                <div className="mt-6">
                  <div className="flex items-center justify-between">
                    <p className="text-xs font-semibold uppercase tracking-wide text-white/50">
                      {t('brain.reviewCards')} ({deepDiveCards.length})
                    </p>
                    <button
                      type="button"
                      onClick={openRandomCard}
                      className="flex items-center gap-1 rounded-full bg-white/10 px-2.5 py-1 text-[11px] font-medium text-white/80 hover:bg-white/20"
                    >
                      <Shuffle className="h-3 w-3" />
                      {t('brain.random')}
                    </button>
                  </div>
                  <div className="mt-2 grid grid-cols-2 gap-2">
                    {deepDiveCards.map((card) => (
                      <button
                        key={card.index}
                        type="button"
                        onClick={() => {
                          setCardAgentActive(false);
                          setCardModalIndex(card.index);
                        }}
                        className="flex flex-col items-start gap-1 rounded-xl border border-white/10 bg-white/5 p-2.5 text-left hover:bg-white/10"
                      >
                        <span className="text-lg leading-none">{card.emoji ?? '🧠'}</span>
                        <span className="line-clamp-2 text-xs font-medium text-white/85">{card.title}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {selectedNode.isStructuredLearning ? (
                <Link
                  to={`/structured-learning/${selectedNode.structuredLearningDocumentId}/sections/${selectedNode.structuredLearningSectionId}`}
                  className="mt-2.5 flex w-full items-center justify-center gap-1.5 rounded-xl border border-white/15 bg-white/5 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-white/10"
                >
                  <RotateCcw className="h-4 w-4" />
                  {t('brain.openFullLesson')}
                </Link>
              ) : (
                <Link
                  to={`/capabilities/${selectedNode.capabilityId}/nodes/${selectedNode.capabilityGraphNodeId}?from=brain`}
                  className="mt-2.5 flex w-full items-center justify-center gap-1.5 rounded-xl border border-white/15 bg-white/5 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-white/10"
                >
                  <RotateCcw className="h-4 w-4" />
                  {t('brain.openFullLesson')}
                </Link>
              )}
            </motion.div>
          </>
        )}
      </AnimatePresence>

      {cardModalIndex !== null && deepDiveCards[cardModalIndex] && selectedNode && (
        <DeepDiveCardModal
          card={deepDiveCards[cardModalIndex]}
          personId={personId}
          capabilityGraphNodeId={selectedNode.isStructuredLearning ? undefined : selectedNode.capabilityGraphNodeId}
          structuredLearningDeepDiveStepId={selectedNode.isStructuredLearning ? slDeepDiveStepId : undefined}
          agentActive={cardAgentActive}
          onClose={() => {
            setCardModalIndex(null);
            setCardAgentActive(false);
          }}
          onAskAgent={() => setCardAgentActive(true)}
        />
      )}

      {engramOpen && selectedNode && (
        selectedNode.isStructuredLearning ? (
          slTeachingStepId && (
            <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm">
              <div className="relative w-full max-w-md rounded-3xl border border-white/10 bg-slate-900 p-6 shadow-2xl max-h-[90vh] overflow-y-auto">
                <button
                  type="button"
                  onClick={() => setEngramOpen(false)}
                  className="absolute right-4 top-4 rounded-full p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
                  aria-label={language === 'en' ? 'Close' : 'Cerrar'}
                >
                  <X className="h-5 w-5" />
                </button>
                <h3 className="text-base font-semibold text-white">{selectedNode.name}</h3>
                {slTeachingIllustrationId && (
                  <img
                    src={`${API_BASE_URL}/illustrations/structured-learning/${slTeachingIllustrationId}/image`}
                    alt={selectedNode.name}
                    className="mt-3 h-auto max-h-56 w-full rounded-xl object-contain bg-black/20"
                  />
                )}
                <div className="mt-4">
                  <VoiceTutorAgent structuredLearningSectionStepId={slTeachingStepId} />
                </div>
              </div>
            </div>
          )
        ) : (
          <EngramReviewAgent
            personId={personId}
            capabilityGraphNodeId={selectedNode.capabilityGraphNodeId}
            onExit={() => setEngramOpen(false)}
          />
        )
      )}
    </div>
  );
}
