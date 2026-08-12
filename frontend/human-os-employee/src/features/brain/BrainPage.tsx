import { useMemo, useState } from 'react';
import { Link } from 'react-router';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'framer-motion';
import DOMPurify from 'dompurify';
import { Brain, Search, Sparkles, X, Bot, Layers, LayoutGrid, Share2, RotateCcw, AlertTriangle, Shuffle } from 'lucide-react';
import { useAuth } from '@/auth/AuthContext';
import { Card } from '@/components/ui/Card';
import { LoadingBackdrop } from '@/components/ui/LoadingBackdrop';
import { getNodeSummary, type NodeSummaryDto } from '@/features/capabilities/api/runtimeSessionApi';
import { API_BASE_URL } from '@/features/capabilities/api/httpClient';
import EngramReviewAgent from '@/features/capabilities/components/EngramReviewAgent';
import { BrainGraph } from './BrainGraph';
import { BrainRecallGrid } from './BrainRecallGrid';
import { MemoryScoreGauge } from './MemoryScoreGauge';
import { MemoryScienceExplainer } from './MemoryScienceExplainer';
import { getSubjectColor } from './subjectColor';
import { computeGlobalMemoryScore, getRetrievability, isAutomatic } from './memoryScience';
import { useBrainGraph } from './useBrainGraph';
import type { SelectedNeuron } from './types';

/** Below this retrievability, the panel nudges the employee that this memory
 * is fading and a full 5-step redo (not just a chat with the agent) will
 * rebuild it more strongly — mirrors the reviewCount-driven stability model
 * in memoryScience.ts. */
const FADING_THRESHOLD = 0.5;

/** Spanish labels for Capability.Category, matching the employee sidebar's
 * own 4 career-plan track labels (EmployeeSidebar.tsx's COURSE_CATEGORY_LINKS)
 * so "tipo de curso" reads the same everywhere in the app. */
const CATEGORY_LABELS: Record<string, string> = {
  'job-skills': 'Mi Puesto',
  'soft-skills': 'Soft Skills',
  'department-processes': 'Procesos de Depto.',
  'hr-processes': 'Procesos de RRHH',
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
  agentActive,
  onClose,
  onAskAgent,
}: {
  card: DeepDiveCardView;
  personId: string;
  capabilityGraphNodeId: string;
  agentActive: boolean;
  onClose: () => void;
  onAskAgent: () => void;
}) {
  return createPortal(
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm">
      <div className="relative w-full max-w-md rounded-3xl border border-white/10 bg-slate-900 p-6 shadow-2xl">
        <button
          type="button"
          onClick={onClose}
          className="absolute right-4 top-4 rounded-full p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
          aria-label="Cerrar"
        >
          <X className="h-5 w-5" />
        </button>

        {card.illustrationId ? (
          <img
            src={`${API_BASE_URL}/illustrations/${card.illustrationId}/image`}
            alt={card.title}
            className="mb-3 h-auto max-h-56 w-full rounded-xl object-contain bg-black/20"
          />
        ) : (
          card.emoji && <div className="mb-3 text-4xl leading-none">{card.emoji}</div>
        )}

        <h3 className="text-base font-semibold text-white">{card.title}</h3>

        {!agentActive && <RichText className="mt-2 text-sm text-white/70" html={card.text} />}

        {agentActive ? (
          <EngramReviewAgent
            personId={personId}
            capabilityGraphNodeId={capabilityGraphNodeId}
            exampleCardIndex={card.index}
            embedded
            onExit={onClose}
          />
        ) : (
          <button
            type="button"
            onClick={onAskAgent}
            className="mt-5 flex w-full items-center justify-center gap-1.5 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-accent-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
          >
            <Sparkles className="h-4 w-4" />
            Que el agente me lo explique
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
  const { user } = useAuth();
  const personId = user?.personId ?? '';

  const { data: rootGroups = [], isLoading: loading } = useBrainGraph(personId);
  const [query, setQuery] = useState('');

  const [selectedNode, setSelectedNode] = useState<SelectedNeuron | null>(null);
  const [summary, setSummary] = useState<NodeSummaryDto | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
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
      .map((code) => ({ code, name: CATEGORY_LABELS[code] ?? code }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [rootGroups]);

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
    setSummary(null);
    setSummaryLoading(true);
    setCardModalIndex(null);
    setCardAgentActive(false);
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
    const step = summary?.Steps.find((s) => s.StepType === 'DeepDive');
    return (step?.ExampleCards ?? []).map((c, index) => ({
      index,
      title: c.Title,
      text: c.Text,
      emoji: c.Emoji,
      illustrationId: c.IllustrationId,
    }));
  }, [summary]);

  function openRandomCard() {
    if (deepDiveCards.length === 0) return;
    setCardAgentActive(false);
    setCardModalIndex(Math.floor(Math.random() * deepDiveCards.length));
  }

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
          <h1 className="text-2xl font-semibold tracking-tight text-slate-900 dark:text-white">Mi Cerebro</h1>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Cada neurona brillante de abajo es algo que ya dominaste — un mapa real de tu propio conocimiento.
          </p>
        </div>
      </div>

      <MemoryScienceExplainer />

      {!isEmpty && (
        <div className="mt-6 flex flex-wrap items-stretch gap-3">
          <div className="grid grid-cols-3 gap-3 sm:max-w-lg">
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Neuronas activas</p>
              <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">{totalNeurons}</p>
            </Card>
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Áreas de conocimiento</p>
              <p className="mt-1 text-2xl font-bold text-slate-900 dark:text-white">{totalAreas}</p>
            </Card>
            <Card className="p-4">
              <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Para repasar pronto</p>
              <p className="mt-1 text-2xl font-bold text-amber-500 dark:text-amber-400">{totalNeedsReview}</p>
            </Card>
          </div>
          <MemoryScoreGauge
            score={memoryScore}
            label="Fuerza de memoria global"
            caption="Qué tan fresco está tu conocimiento dominado ahora mismo, según la curva real del olvido."
          />
        </div>
      )}

      {!isEmpty && (
        <Card className="mt-4 flex items-start gap-3 border-dashed p-4">
          <Bot className="mt-0.5 h-5 w-5 shrink-0 text-slate-400" />
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Preguntar sobre todo lo que has aprendido a la vez llegará pronto. Por ahora, elige una neurona específica
            para hablar sobre ella.
          </p>
        </Card>
      )}

      {loading && (
        <LoadingBackdrop
          title="Construyendo tu red de conocimiento"
          description="Conectando tu aprendizaje bilingüe en todos tus Engramas..."
        />
      )}

      {isEmpty && (
        <Card className="mt-8 flex flex-col items-center gap-3 p-10 text-center">
          <Brain className="h-10 w-10 text-slate-300 dark:text-slate-600" />
          <p className="text-base font-semibold text-slate-900 dark:text-white">Todavía no has dominado ningún nodo.</p>
          <p className="max-w-sm text-sm text-slate-500 dark:text-slate-400">
            Completa tu primer nodo en un Engrama para verlo brillar aquí.
          </p>
          <Link
            to="/capabilities"
            className="mt-2 rounded-xl bg-gradient-to-r from-brand-500 to-accent-500 px-5 py-2.5 text-sm font-semibold text-white shadow-lg shadow-accent-500/25 transition-transform hover:scale-[1.02] active:scale-[0.98]"
          >
            Explorar Engramas
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
                placeholder="Busca un tema que ya aprendiste..."
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
                Grilla
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
                Grafo
              </button>
            </div>
          </div>

          <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">
            Selecciona una neurona para ver qué aprendiste y hablar con tu Agente sobre ese tema.
          </p>

          {subjectOptions.length > 1 && (
            <FilterChipRow
              label="Materia:"
              allLabel="Todas"
              value={subjectFilter}
              onChange={setSubjectFilter}
              options={subjectOptions.map((s) => ({ value: s.code, label: s.name, color: getSubjectColor(s.code) }))}
            />
          )}

          {courseOptions.length > 1 && (
            <FilterChipRow
              label="Curso:"
              allLabel="Todos"
              value={courseFilter}
              onChange={setCourseFilter}
              options={courseOptions.map((c) => ({ value: c.id, label: c.name }))}
            />
          )}

          {categoryOptions.length > 0 && (
            <FilterChipRow
              label="Tipo de curso:"
              allLabel="Todos"
              value={categoryFilter}
              onChange={setCategoryFilter}
              options={categoryOptions.map((c) => ({ value: c.code, label: c.name }))}
            />
          )}

          <FilterChipRow
            label="Estado:"
            allLabel="Todas"
            value={statusFilter === 'all' ? null : statusFilter}
            onChange={(v) => setStatusFilter((v as StatusFilter) ?? 'all')}
            options={[
              { value: 'mastered', label: 'Dominado' },
              { value: 'needs-review', label: 'Necesita repaso' },
            ]}
          />

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <span className="text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-slate-500">
              Último repaso:
            </span>
            <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
              Desde
              <input
                type="date"
                value={dateFrom}
                onChange={(e) => setDateFrom(e.target.value)}
                max={dateTo || undefined}
                className="rounded-lg border border-slate-200 bg-white px-2 py-1 text-xs text-slate-900 outline-none focus:border-brand-500 dark:border-white/10 dark:bg-white/[0.03] dark:text-white"
              />
            </label>
            <label className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
              Hasta
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
                Limpiar fechas
              </button>
            )}
          </div>

          <div className="relative mt-4 h-[560px] w-full overflow-hidden rounded-3xl border border-white/10 bg-[radial-gradient(ellipse_at_center,_#1e1b4b_0%,_#0f172a_55%,_#020617_100%)] shadow-2xl sm:h-[680px]">
            {view === 'grid' ? (
              <BrainRecallGrid rootGroups={filteredRootGroups} query={query} onSelectNeuron={handleSelectNode} />
            ) : (
              <BrainGraph rootGroups={filteredRootGroups} query={query} onSelectNeuron={handleSelectNode} />
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
                  aria-label="Cerrar"
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
                    Este recuerdo se está desvaneciendo. Platicar con el agente te ayuda a repasarlo, pero rehacer el
                    ciclo completo de 5 pasos lo reconstruye mucho más fuerte.
                  </p>
                </div>
              )}

              {summaryLoading && <p className="mt-6 text-sm text-white/50">Cargando...</p>}

              {!summaryLoading && summary && (
                <div className="mt-6 grid grid-cols-2 gap-3">
                  <SummaryStat label="Intentos" value={String(summary.AttemptCount)} />
                  <SummaryStat
                    label="Puntaje"
                    value={typeof summary.FinalScore === 'number' ? `${summary.FinalScore}/100` : '—'}
                  />
                  <SummaryStat
                    label="Dominado desde"
                    value={
                      summary.FirstCompletedDate
                        ? new Date(summary.FirstCompletedDate).toLocaleDateString('es', { dateStyle: 'medium' })
                        : '—'
                    }
                  />
                  <SummaryStat
                    label="Último repaso"
                    value={
                      summary.LastCompletedDate
                        ? new Date(summary.LastCompletedDate).toLocaleDateString('es', { dateStyle: 'medium' })
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
                Habla con tu Agente sobre esto
              </button>

              {deepDiveCards.length > 0 && (
                <div className="mt-6">
                  <div className="flex items-center justify-between">
                    <p className="text-xs font-semibold uppercase tracking-wide text-white/50">
                      Tarjetas de repaso ({deepDiveCards.length})
                    </p>
                    <button
                      type="button"
                      onClick={openRandomCard}
                      className="flex items-center gap-1 rounded-full bg-white/10 px-2.5 py-1 text-[11px] font-medium text-white/80 hover:bg-white/20"
                    >
                      <Shuffle className="h-3 w-3" />
                      Aleatoria
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

              <Link
                to={`/capabilities/${selectedNode.capabilityId}/nodes/${selectedNode.capabilityGraphNodeId}?from=brain`}
                className="mt-2.5 flex w-full items-center justify-center gap-1.5 rounded-xl border border-white/15 bg-white/5 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-white/10"
              >
                <RotateCcw className="h-4 w-4" />
                Rehacer los 5 pasos (sube el score)
              </Link>
            </motion.div>
          </>
        )}
      </AnimatePresence>

      {cardModalIndex !== null && deepDiveCards[cardModalIndex] && selectedNode && (
        <DeepDiveCardModal
          card={deepDiveCards[cardModalIndex]}
          personId={personId}
          capabilityGraphNodeId={selectedNode.capabilityGraphNodeId}
          agentActive={cardAgentActive}
          onClose={() => {
            setCardModalIndex(null);
            setCardAgentActive(false);
          }}
          onAskAgent={() => setCardAgentActive(true)}
        />
      )}

      {engramOpen && selectedNode && (
        <EngramReviewAgent
          personId={personId}
          capabilityGraphNodeId={selectedNode.capabilityGraphNodeId}
          onExit={() => setEngramOpen(false)}
        />
      )}
    </div>
  );
}
