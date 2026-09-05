import { useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'framer-motion';
import DOMPurify from 'dompurify';
import { useTranslation } from 'react-i18next';
import {
  Brain,
  Sparkles,
  X,
  Layers,
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
  BookOpen,
  ArrowRight,
  Download,
} from 'lucide-react';
import { getNodeSummary, type NodeSummaryDto } from '@/features/capabilities/api/runtimeSessionApi';
import {
  getStructuredLearningDocument,
  getStructuredLearningSectionSummary,
  listStructuredLearningSectionVideos,
  getStructuredLearningKnowledgeFacts,
  type StructuredLearningSectionSummaryDto,
  type StructuredLearningSectionVideo,
  type StructuredLearningKnowledgeFactsResponse,
  type StructuredLearningSectionDetail,
} from '@/features/capabilities/api/structuredLearningRuntimeApi';
import { API_BASE_URL } from '@/features/capabilities/api/httpClient';
import EngramReviewAgent from '@/features/capabilities/components/EngramReviewAgent';
import VoiceTutorAgent from '@/features/capabilities/components/VoiceTutorAgent';
import { KnowledgeFactsPanel } from '@/features/capabilities/components/KnowledgeGraphSection';
import { getRetrievability } from './memoryScience';
import type { SelectedNeuron } from './types';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

/** Below this retrievability, the panel nudges the employee that this memory
 * is fading and a full 5-step redo will rebuild it more strongly. */
const FADING_THRESHOLD = 0.5;

function SummaryStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-white/5 p-3">
      <p className="text-[11px] uppercase tracking-wide text-white/50">{label}</p>
      <p className="mt-0.5 text-sm font-semibold text-white">{value}</p>
    </div>
  );
}

/** Sanitized rich-text renderer for ExampleCard.Text. */
function RichText({ html, className }: { html: string; className?: string }) {
  const sanitized = useMemo(
    () =>
      DOMPurify.sanitize(html, {
        ALLOWED_TAGS: ['p', 'br', 'strong', 'b', 'em', 'i', 'ul', 'ol', 'li', 'a'],
        ALLOWED_ATTR: ['href'],
      }),
    [html],
  );
  return <div className={className} style={{ whiteSpace: 'pre-wrap' }} dangerouslySetInnerHTML={{ __html: sanitized }} />;
}

function formatInlineText(value: string): React.ReactNode {
  const parts = value.split(/(\*\*[^*]+\*\*|__[^_]+__)/g);
  return parts.map((part, index) => {
    const bold = /^\*\*[^*]+\*\*$/.test(part) || /^__[^_]+__$/.test(part);
    return bold ? (
      <strong key={index} className="font-bold text-white">
        {part.slice(2, -2)}
      </strong>
    ) : (
      <span key={index}>{part}</span>
    );
  });
}

function splitLongBlock(block: string): string[] {
  if (!block || block.length < 420) return [block];
  const sentences = block.match(/[^.!?]+[.!?]+(?=\s|$)/g)?.map((sentence) => sentence.trim()).filter(Boolean);
  if (!sentences || sentences.length < 3) return [block];
  const groups: string[] = [];
  for (let index = 0; index < sentences.length; index += 2) {
    groups.push(sentences.slice(index, index + 2).join(' '));
  }
  return groups;
}

function cleanEngramContent(raw: string): string {
  if (!raw) return '';
  return raw
    .replace(/^ContractVersion:\s*[^\n]+\s*/i, '')
    .replace(/\[(?:Descripci[óo]n\s+de\s+imagen|Image\s+description)[^\]]*\]/gi, '')
    .replace(/(?:^|\n)#{0,6}\s*(?:\d+\.\s*)?D?ETAILED\s+DESCRIPTION:?[\s\S]*?(?=(?:\n#{1,6}\s+[^\n]+|\n\[(?:Descripci|Image)|$))/gi, '')
    .replace(/(?:^|\n)#{0,6}\s*(?:\d+\.\s*)?VERBATIM\s+TRANSCRIPTION:?\s*/gi, '\n')
    .replace(/#{0,6}\s*(?:\d+\.\s*)?D?ETAILED\s+DESCRIPTION:?\s*/gi, '')
    .trim();
}

function normalizeRichBlocks(content: string): string[] {
  const cleaned = cleanEngramContent(content);
  const normalized = cleaned
    .replace(/\r\n/g, '\n')
    .replace(/\s+•\s+/g, '\n• ')
    .replace(/\s+(Why this matters)\s+/gi, '\n\n$1\n')
    .replace(/\s+(Next, connect)\s+/gi, '\n\n$1 ')
    .replace(/\s+(Foundations of AI:|What is generative AI\?|AI value also comes from other capabilities:|Responsible AI means)/g, '\n\n$1');

  return normalized
    .split(/\n\s*\n|\n/)
    .map((block) => block.trim())
    .filter(Boolean)
    .flatMap((block) => splitLongBlock(block));
}

function RichEngramText({ content }: { content: string }) {
  const blocks = normalizeRichBlocks(content);

  return (
    <div className="space-y-4 text-sm leading-relaxed text-slate-300">
      {blocks.map((block, index) => {
        const lines = block.split('\n').map((line) => line.trim()).filter(Boolean);
        const isList = lines.length > 0 && lines.every((line) => /^([-*•]|\d+[.)])\s+/.test(line));
        const heading = lines.length === 1 && (/^#{1,3}\s+/.test(lines[0]) || (lines[0].length < 90 && /:$/.test(lines[0])));

        if (heading) {
          const text = lines[0].replace(/^#{1,3}\s+/, '');
          return (
            <h3 key={index} className="pt-2 text-base font-bold leading-6 text-white border-b border-white/10 pb-1">
              {text}
            </h3>
          );
        }

        if (isList) {
          const ordered = /^\d+[.)]/.test(lines[0]);
          const List = ordered ? 'ol' : 'ul';
          return (
            <List key={index} className={`space-y-1.5 pl-5 ${ordered ? 'list-decimal' : 'list-disc'}`}>
              {lines.map((line, lineIndex) => (
                <li key={lineIndex} className="pl-1 text-slate-300">
                  {formatInlineText(line.replace(/^([-*•]|\d+[.)])\s+/, ''))}
                </li>
              ))}
            </List>
          );
        }

        return (
          <p key={index} className="text-slate-300 whitespace-pre-wrap">
            {lines.map((line, lineIndex) => (
              <span key={lineIndex}>
                {lineIndex > 0 && <br />}
                {formatInlineText(line)}
              </span>
            ))}
          </p>
        );
      })}
    </div>
  );
}

async function downloadImageFile(url: string, filename = 'lesson-illustration.png') {
  try {
    const res = await fetch(url);
    if (!res.ok) throw new Error('Fetch failed');
    const blob = await res.blob();
    const blobUrl = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(blobUrl);
  } catch {
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.target = '_blank';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
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

/** Full detail of one Deep Dive card — reads again + activates the Agent. */
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
    : card.illustrationIds && card.illustrationIds.length > 0
    ? card.isStructuredLearning
      ? `${API_BASE_URL}/illustrations/structured-learning/${card.illustrationIds[0]}/image`
      : `${API_BASE_URL}/illustrations/${card.illustrationIds[0]}/image`
    : null;

  return createPortal(
    <div className="fixed inset-0 z-[60] flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm">
      <div className="relative w-full max-w-3xl rounded-3xl border border-white/10 bg-slate-900 p-6 shadow-2xl max-h-[90vh] overflow-y-auto">
        <button
          type="button"
          onClick={onClose}
          className="absolute right-4 top-4 rounded-full p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
          aria-label={language === 'en' ? 'Close' : 'Cerrar'}
        >
          <X className="h-5 w-5" />
        </button>

        {card.illustrationIds && card.illustrationIds.length > 0 ? (
          <div className="mb-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
            {card.illustrationIds.map((id, i) => (
              <div key={id} className="group/item relative space-y-1">
                <img
                  src={`${API_BASE_URL}/illustrations/${card.isStructuredLearning ? 'structured-learning/' : ''}${id}/image`}
                  alt={`${card.title} — step ${i + 1}`}
                  className="h-auto max-h-56 w-full rounded-xl object-contain bg-black/20"
                />
                <button
                  type="button"
                  onClick={() => void downloadImageFile(`${API_BASE_URL}/illustrations/${card.isStructuredLearning ? 'structured-learning/' : ''}${id}/image`, `${card.title.replace(/[^a-zA-Z0-9_-]/g, '_')}_step_${i + 1}.png`)}
                  className="absolute top-2 right-2 flex items-center gap-1 rounded-lg border border-white/20 bg-slate-900/80 px-2 py-1 text-[10px] font-medium text-white opacity-0 transition group-hover/item:opacity-100 hover:bg-slate-800"
                >
                  <Download className="h-3 w-3" />
                  <span>Save</span>
                </button>
                <p className="text-center text-[11px] font-medium text-white/50">Step {i + 1}</p>
              </div>
            ))}
          </div>
        ) : imageSrc ? (
          <div className="group relative mb-4">
            <img src={imageSrc} alt={card.title} className="h-auto max-h-[28rem] w-full rounded-2xl object-contain bg-black/20" />
            <button
              type="button"
              onClick={() => void downloadImageFile(imageSrc, `${card.title.replace(/[^a-zA-Z0-9_-]/g, '_')}.png`)}
              className="absolute top-3 right-3 flex items-center gap-1.5 rounded-lg border border-white/20 bg-slate-900/80 px-2.5 py-1 text-xs font-medium text-white opacity-0 transition group-hover:opacity-100 hover:bg-slate-800"
            >
              <Download className="h-3.5 w-3.5" />
              <span>{language === 'en' ? 'Save image' : 'Guardar imagen'}</span>
            </button>
          </div>
        ) : (
          card.emoji && <div className="mb-3 text-4xl leading-none">{card.emoji}</div>
        )}

        <h3 className="text-base font-semibold text-white">{card.title}</h3>

        {!agentActive && <RichText className="mt-2 text-sm text-white/70" html={card.text} />}

        {agentActive ? (
          card.isStructuredLearning && structuredLearningDeepDiveStepId ? (
            <div className="mt-4">
              <VoiceTutorAgent structuredLearningSectionStepId={structuredLearningDeepDiveStepId} exampleCardIndex={card.index} />
            </div>
          ) : capabilityGraphNodeId ? (
            <EngramReviewAgent personId={personId} capabilityGraphNodeId={capabilityGraphNodeId} exampleCardIndex={card.index} embedded onExit={onClose} />
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

/**
 * The Brain page's mastered-engram detail panel (2026-09-04) — extracted
 * out of BrainPage.tsx into its own reusable component so ANY page that
 * knows a mastered engram's ids can open the EXACT same tabbed panel
 * (Memory state / Rewards / Cards / Things to remember) instead of
 * reinventing a smaller modal — see AiAgentCoursePage.tsx's citation
 * cards, which now use this same component.
 */
export function EngramDetailPanel({
  personId,
  neuron: initialNeuron,
  initialEngramText,
  onClose,
  onSelectNeuron,
}: {
  personId: string;
  neuron: SelectedNeuron;
  initialEngramText?: string;
  onClose: () => void;
  onSelectNeuron?: (neuron: SelectedNeuron) => void;
}) {
  const { language } = useI18n();
  const { t } = useTranslation();

  const [activeNeuron, setActiveNeuron] = useState<SelectedNeuron>(initialNeuron);
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setActiveNeuron(initialNeuron);
  }, [initialNeuron]);

  const neuron = activeNeuron;

  const [summary, setSummary] = useState<NodeSummaryDto | null>(null);
  const [slSummary, setSlSummary] = useState<StructuredLearningSectionSummaryDto | null>(null);
  const [rewardBreakdownOpen, setRewardBreakdownOpen] = useState(false);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [slDeepDiveCards, setSlDeepDiveCards] = useState<DeepDiveCardView[]>([]);
  const [slDeepDiveStepId, setSlDeepDiveStepId] = useState<string | null>(null);
  const [slTeachingStepId, setSlTeachingStepId] = useState<string | null>(null);
  const [slTeachingIllustrationId, setSlTeachingIllustrationId] = useState<string | null>(null);
  const [slVideos, setSlVideos] = useState<StructuredLearningSectionVideo[]>([]);
  const [slKnowledgeFacts, setSlKnowledgeFacts] = useState<StructuredLearningKnowledgeFactsResponse | null>(null);
  const [expandedFactEvidence, setExpandedFactEvidence] = useState<Record<string, boolean>>({});
  const [chapterTitle, setChapterTitle] = useState<string | null>(null);
  const [chapterEngrams, setChapterEngrams] = useState<StructuredLearningSectionDetail[]>([]);
  const [slSectionText, setSlSectionText] = useState<string | null>(null);
  const [slTeachingContent, setSlTeachingContent] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'learn' | 'memory' | 'rewards' | 'cards' | 'facts'>('learn');
  const [engramOpen, setEngramOpen] = useState(false);
  const [cardModalIndex, setCardModalIndex] = useState<number | null>(null);
  const [cardAgentActive, setCardAgentActive] = useState(false);

  useEffect(() => {
    setCardModalIndex(null);
    setCardAgentActive(false);
    setRewardBreakdownOpen(false);
    setActiveTab('learn');

    if (neuron.isStructuredLearning) {
      setSummary(null);
      setSlSummary(null);
      setSlDeepDiveCards([]);
      setSlDeepDiveStepId(null);
      setSlTeachingStepId(null);
      setSlTeachingIllustrationId(null);
      setSlSectionText(null);
      setSlTeachingContent(null);
      setSlVideos([]);
      setSlKnowledgeFacts(null);
      setExpandedFactEvidence({});
      setChapterTitle(null);
      setChapterEngrams([]);
      setSummaryLoading(true);

      getStructuredLearningSectionSummary(neuron.structuredLearningSectionId, personId)
        .then((data) => setSlSummary(data))
        .catch(() => setSlSummary(null));

      getStructuredLearningDocument(neuron.structuredLearningDocumentId)
        .then((doc) => {
          const section = doc.Sections.find((s) => s.StructuredLearningDocumentSectionId === neuron.structuredLearningSectionId);
          if (section) {
            setChapterTitle(section.ChapterTitle);
            if (section.Text) {
              setSlSectionText(section.Text);
            }
            const siblings = doc.Sections
              .filter((s) => s.ChapterTitle === section.ChapterTitle)
              .sort((a, b) => a.SortOrder - b.SortOrder);
            setChapterEngrams(siblings);
          } else {
            setChapterTitle(null);
            setChapterEngrams([]);
          }
          const deepDiveStep = section?.Steps.find((s) => s.StepType === 'DeepDive');
          const teachingStep = section?.Steps.find((s) => s.StepType === 'Teaching');
          setSlTeachingStepId(teachingStep?.StructuredLearningSectionStepId ?? null);
          setSlDeepDiveStepId(deepDiveStep?.StructuredLearningSectionStepId ?? null);
          if (teachingStep?.Content) {
            setSlTeachingContent(teachingStep.Content);
          }
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
                })),
              );
            } catch {
              setSlDeepDiveCards([]);
            }
          }
        })
        .catch(() => undefined)
        .finally(() => setSummaryLoading(false));

      listStructuredLearningSectionVideos(neuron.structuredLearningSectionId)
        .then((videos) => setSlVideos(videos.filter((v) => v.IsPublished)))
        .catch(() => setSlVideos([]));

      getStructuredLearningKnowledgeFacts(neuron.structuredLearningSectionId)
        .then((facts) => setSlKnowledgeFacts(facts))
        .catch(() => setSlKnowledgeFacts(null));

      return;
    }

    setSummary(null);
    setSlSummary(null);
    setSlSectionText(null);
    setSlTeachingContent(null);
    setSummaryLoading(true);
    getNodeSummary(personId, neuron.capabilityGraphNodeId)
      .then((data) => setSummary(data))
      .catch(() => setSummary(null))
      .finally(() => setSummaryLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [neuron, personId]);

  const deepDiveCards: DeepDiveCardView[] = useMemo(() => {
    if (neuron.isStructuredLearning) return slDeepDiveCards;
    const step = summary?.Steps.find((s) => s.StepType === 'DeepDive');
    return (step?.ExampleCards ?? []).map((c, index) => ({
      index,
      title: c.Title,
      text: c.Text,
      emoji: c.Emoji,
      illustrationId: c.IllustrationId,
      isStructuredLearning: false,
    }));
  }, [summary, neuron, slDeepDiveCards]);

  function openRandomCard() {
    if (deepDiveCards.length === 0) return;
    setCardAgentActive(false);
    setCardModalIndex(Math.floor(Math.random() * deepDiveCards.length));
  }

  function handleSelectChapterEngram(section: StructuredLearningSectionDetail) {
    if (!neuron.isStructuredLearning) return;
    if (section.StructuredLearningDocumentSectionId === neuron.structuredLearningSectionId) return;
    const nextNeuron: SelectedNeuron = {
      capabilityGraphNodeId: section.StructuredLearningDocumentSectionId,
      name: section.SubchapterTitle || section.ChapterTitle,
      state: 'Mastered',
      isStructuredLearning: true,
      structuredLearningDocumentId: neuron.structuredLearningDocumentId,
      structuredLearningSectionId: section.StructuredLearningDocumentSectionId,
      capabilityId: neuron.capabilityId,
      capabilityName: neuron.capabilityName,
    };
    setActiveNeuron(nextNeuron);
    onSelectNeuron?.(nextNeuron);
    panelRef.current?.scrollTo({ top: 0, behavior: 'smooth' });
  }

  const activeMemory = neuron.isStructuredLearning ? slSummary?.Memory : summary?.Memory;
  const activeReward = neuron.isStructuredLearning ? slSummary?.Reward : summary?.Reward;
  const activeImpact = neuron.isStructuredLearning ? slSummary?.Impact : summary?.Impact;

  const formatFriendlyDate = (dateStr?: string | null) => {
    if (!dateStr) return '';
    try {
      return new Date(dateStr).toLocaleDateString(language === 'en' ? 'en-US' : 'es-MX', { day: 'numeric', month: 'short', year: 'numeric' });
    } catch {
      return dateStr;
    }
  };

  const formatDaysUntil = (targetDateStr?: string | null) => {
    if (!targetDateStr) return '';
    try {
      const target = new Date(targetDateStr).getTime();
      const diffDays = Math.max(0, Math.round((target - Date.now()) / (24 * 60 * 60 * 1000)));
      if (diffDays === 0) return t('brain.today');
      if (diffDays === 1) return t('brain.inOneDay');
      return t('brain.inDays', { count: diffDays });
    } catch {
      return '';
    }
  };

  const getTierBadgeStyle = (tier?: string | null) => {
    const norm = (tier || '').toLowerCase().trim();
    if (norm === 'critical') return 'bg-red-500/20 text-red-300 border-red-500/30';
    if (norm === 'important') return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
    return 'bg-slate-500/20 text-slate-300 border-slate-500/30';
  };

  const getMemoryStateBadgeStyle = (state?: string | null) => {
    const norm = (state || '').toLowerCase().trim();
    if (norm === 'strong') return 'text-emerald-400 bg-emerald-500/15 border-emerald-500/30';
    if (norm === 'fading') return 'text-amber-400 bg-amber-500/15 border-amber-500/30';
    return 'text-red-400 bg-red-500/15 border-red-500/30';
  };

  const selectedRetrievability = getRetrievability(neuron.lastCompletedDate, neuron.reviewCount ?? 1);
  const selectedIsFading = selectedRetrievability !== null && selectedRetrievability < FADING_THRESHOLD;

  const engramText = neuron.isStructuredLearning
    ? (slSectionText || slTeachingContent || initialEngramText || neuron.description || '')
    : (summary?.Steps.find((s) => s.StepType === 'Teaching')?.Content || initialEngramText || neuron.description || '');

  return (
    <>
      <AnimatePresence>
        <motion.div key="backdrop" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} className="fixed inset-0 z-40 bg-black/40" onClick={onClose} />
        <motion.div
          key="panel"
          ref={panelRef}
          initial={{ x: 400, opacity: 0 }}
          animate={{ x: 0, opacity: 1 }}
          exit={{ x: 400, opacity: 0 }}
          transition={{ type: 'tween', duration: 0.25 }}
          className="fixed right-0 top-0 z-50 h-full w-full overflow-y-auto bg-slate-900 p-6 shadow-2xl sm:w-[34rem]"
        >
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-fuchsia-300">
              <Layers className="h-3.5 w-3.5" />
              {neuron.capabilityName}
            </div>
            <button type="button" onClick={onClose} className="rounded-full p-1 text-white/60 hover:bg-white/10 hover:text-white" aria-label={language === 'en' ? 'Close' : 'Cerrar'}>
              <X className="h-5 w-5" />
            </button>
          </div>

          <h2 className="mt-2 text-xl font-semibold text-white">{neuron.name}</h2>
          {neuron.description && !neuron.isStructuredLearning && neuron.description !== engramText && (
            <p className="mt-2 text-sm text-white/70">{neuron.description}</p>
          )}

          {selectedIsFading && (
            <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-400/30 bg-amber-400/10 p-3">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" />
              <p className="text-xs text-amber-200">{t('brain.fadingWarning')}</p>
            </div>
          )}

          <div className="mt-5 flex gap-1 rounded-xl border border-white/10 bg-white/[0.03] p-1 overflow-x-auto">
            {(
              [
                ['learn', t('brain.tabs.learn')],
                ['memory', t('brain.tabs.memory')],
                ['rewards', t('brain.tabs.rewards')],
                ['cards', t('brain.tabs.cards')],
                ['facts', t('brain.tabs.facts')],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                type="button"
                onClick={() => setActiveTab(key)}
                className={`flex-1 min-w-fit rounded-lg px-2.5 py-1.5 text-[11px] font-semibold transition whitespace-nowrap ${
                  activeTab === key ? 'bg-gradient-to-r from-brand-500 to-accent-500 text-white shadow' : 'text-white/50 hover:bg-white/10 hover:text-white'
                }`}
              >
                {label}
              </button>
            ))}
          </div>

          {activeTab === 'learn' && (
            <div className="mt-4 space-y-4">
              {slTeachingIllustrationId && (
                <div className="group relative overflow-hidden rounded-2xl border border-white/10 bg-black/30">
                  <img
                    src={`${API_BASE_URL}/illustrations/structured-learning/${slTeachingIllustrationId}/image`}
                    alt={neuron.name}
                    className="h-auto max-h-64 w-full object-contain"
                  />
                  <button
                    type="button"
                    onClick={() =>
                      void downloadImageFile(
                        `${API_BASE_URL}/illustrations/structured-learning/${slTeachingIllustrationId}/image`,
                        `${neuron.name.replace(/[^a-zA-Z0-9_-]/g, '_')}_illustration.png`
                      )
                    }
                    className="absolute top-2 right-2 flex items-center gap-1.5 rounded-lg border border-white/20 bg-slate-900/80 px-2.5 py-1 text-xs font-medium text-white opacity-0 transition group-hover:opacity-100 hover:bg-slate-800"
                  >
                    <Download className="h-3.5 w-3.5" />
                    <span>{language === 'en' ? 'Save image' : 'Guardar imagen'}</span>
                  </button>
                </div>
              )}
              {summaryLoading && !engramText ? (
                <p className="text-sm text-white/50">{t('common.loading')}</p>
              ) : engramText ? (
                <div className="rounded-2xl border border-white/10 bg-white/[0.02] p-4">
                  <RichEngramText content={engramText} />
                </div>
              ) : (
                <p className="text-sm text-white/50">
                  {language === 'en' ? 'No text available for this engram.' : 'No hay texto disponible para este engrama.'}
                </p>
              )}
            </div>
          )}

          {activeTab === 'memory' && (
            <div className="mt-4 space-y-3">
              {activeMemory && (
                <div className="overflow-hidden rounded-2xl border border-emerald-500/25 bg-gradient-to-br from-emerald-500/10 via-emerald-500/[0.03] to-white/[0.02] p-3.5 shadow-sm">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-1.5 text-xs font-black uppercase tracking-wider text-emerald-400">
                      <Brain className="h-4 w-4 text-emerald-400" />
                      <span>{t('brain.blocks.memory')}</span>
                    </div>
                    <span className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide ${getMemoryStateBadgeStyle(activeMemory.MemoryState)}`}>
                      <Sparkles className="h-2.5 w-2.5" />
                      {activeMemory.MemoryState}
                    </span>
                  </div>

                  <div className="mt-3 flex items-center justify-between">
                    <div className="flex items-baseline gap-1.5">
                      <span className="text-2xl font-black text-white">{activeMemory.RecallPercent}%</span>
                      <span className="text-[11px] text-white/50">{t('brain.retention')}</span>
                    </div>
                    <div className="text-right">
                      {activeMemory.IsAutomatic ? (
                        <span className="inline-flex items-center gap-1 rounded-full bg-amber-500/20 px-2 py-0.5 text-[10px] font-extrabold text-amber-300">
                          <Zap className="h-3 w-3 fill-amber-300" />
                          {t('brain.automatic')}
                        </span>
                      ) : (
                        <span className="text-[11px] font-medium text-white/70">
                          {activeMemory.ReviewsUntilAutomatic} {t('brain.reviewsToAuto')}
                        </span>
                      )}
                    </div>
                  </div>

                  <div className="mt-3 flex items-center justify-between border-t border-white/5 pt-2 text-[11px] text-white/60">
                    <span>{t('brain.reviewCount', { count: activeMemory.ReviewCount })}</span>
                    {activeMemory.NextReviewDate && (
                      <span className="flex items-center gap-1 font-medium text-emerald-300/90">
                        <Calendar className="h-3 w-3" />
                        {t('brain.nextReview')}: {formatFriendlyDate(activeMemory.NextReviewDate)} ({formatDaysUntil(activeMemory.NextReviewDate)})
                      </span>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}

          {activeTab === 'rewards' && (
            <div className="mt-4 space-y-3">
              {activeReward && (
                <div className="overflow-hidden rounded-2xl border border-amber-500/25 bg-gradient-to-br from-amber-500/10 via-amber-500/[0.03] to-white/[0.02] p-3.5 shadow-sm">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-1.5 text-xs font-black uppercase tracking-wider text-amber-400">
                      <Trophy className="h-4 w-4 text-amber-400" />
                      <span>{t('brain.reward')}</span>
                    </div>
                    {activeReward.WasSurprise && activeReward.SurpriseType !== 'NONE' ? (
                      <span className="inline-flex items-center gap-1 rounded-full bg-gradient-to-r from-amber-500/30 to-purple-500/30 px-2 py-0.5 text-[10px] font-extrabold uppercase tracking-wide text-amber-200">
                        {activeReward.SurpriseType === 'JACKPOT' ? '🎉 JACKPOT' : activeReward.SurpriseType === 'DOUBLE' ? '⚡ DOUBLE' : '🎲 BONUS'}
                      </span>
                    ) : (
                      <span className="text-[10px] text-white/40 font-medium">{formatFriendlyDate(activeReward.EarnedAt)}</span>
                    )}
                  </div>

                  <div className="mt-3 flex items-baseline justify-between">
                    <div className="flex items-baseline gap-1">
                      <span className="text-2xl font-black text-amber-300">+{activeReward.PointsAwarded}</span>
                      <span className="text-xs font-medium text-amber-400/80">pts</span>
                    </div>
                    <button type="button" onClick={() => setRewardBreakdownOpen((prev) => !prev)} className="inline-flex items-center gap-1 text-[11px] font-semibold text-amber-300 hover:text-amber-200">
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

              {activeImpact && (
                <div className="overflow-hidden rounded-2xl border border-indigo-500/25 bg-gradient-to-br from-indigo-500/10 via-indigo-500/[0.03] to-white/[0.02] p-3.5 shadow-sm">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-1.5 text-xs font-black uppercase tracking-wider text-indigo-400">
                      <Target className="h-4 w-4 text-indigo-400" />
                      <span>{t('brain.roleImpact')}</span>
                    </div>
                    <span className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-extrabold uppercase tracking-wide ${getTierBadgeStyle(activeImpact.Tier)}`}>
                      {activeImpact.Tier}
                    </span>
                  </div>
                  <div className="mt-2 flex items-baseline gap-1.5">
                    <span className="text-xl font-black text-white">{activeImpact.Score}</span>
                    <span className="text-xs text-white/50">/ 100</span>
                  </div>
                  {activeImpact.Reason && <p className="mt-2 text-xs leading-relaxed text-slate-300 dark:text-white/80 bg-black/20 rounded-xl p-2.5 border border-white/5">{activeImpact.Reason}</p>}
                </div>
              )}
            </div>
          )}

          {activeTab === 'memory' && summaryLoading && !neuron.isStructuredLearning && <p className="mt-6 text-sm text-white/50">{t('common.loading')}</p>}

          {activeTab === 'memory' && !summaryLoading && summary && !neuron.isStructuredLearning && (
            <div className="mt-6 grid grid-cols-2 gap-3">
              <SummaryStat label={t('brain.attempts')} value={String(summary.AttemptCount)} />
              <SummaryStat label={t('brain.score')} value={typeof summary.FinalScore === 'number' ? `${summary.FinalScore}/100` : '—'} />
              <SummaryStat
                label={t('brain.masteredSince')}
                value={summary.FirstCompletedDate ? new Date(summary.FirstCompletedDate).toLocaleDateString(language === 'en' ? 'en-US' : 'es', { dateStyle: 'medium' }) : '—'}
              />
              <SummaryStat
                label={t('brain.lastReview')}
                value={summary.LastCompletedDate ? new Date(summary.LastCompletedDate).toLocaleDateString(language === 'en' ? 'en-US' : 'es', { dateStyle: 'medium' }) : '—'}
              />
            </div>
          )}

          {activeTab === 'memory' && neuron.isStructuredLearning && (
            <div className="mt-6 grid grid-cols-2 gap-3">
              <SummaryStat
                label={t('brain.score')}
                value={typeof slSummary?.FinalScore === 'number' ? `${slSummary.FinalScore}/100` : typeof neuron.finalScore === 'number' ? `${neuron.finalScore}/100` : '—'}
              />
              <SummaryStat
                label={t('brain.masteredOn')}
                value={
                  slSummary?.LastCompletedDate
                    ? new Date(slSummary.LastCompletedDate).toLocaleDateString(language === 'en' ? 'en-US' : 'es', { dateStyle: 'medium' })
                    : neuron.lastCompletedDate
                    ? new Date(neuron.lastCompletedDate).toLocaleDateString(language === 'en' ? 'en-US' : 'es', { dateStyle: 'medium' })
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

          {activeTab === 'facts' && neuron.isStructuredLearning && slKnowledgeFacts && slKnowledgeFacts.KnowledgeFacts.length > 0 && (
            <div className="mt-4">
              <KnowledgeFactsPanel data={slKnowledgeFacts} expandedEvidence={expandedFactEvidence} onToggleEvidence={(key) => setExpandedFactEvidence((previous) => ({ ...previous, [key]: !previous[key] }))} />
            </div>
          )}

          {activeTab === 'facts' && neuron.isStructuredLearning && (!slKnowledgeFacts || slKnowledgeFacts.KnowledgeFacts.length === 0) && (
            <p className="mt-4 text-sm text-white/50">{language === 'en' ? 'No facts extracted for this engram yet.' : 'Aún no hay datos extraídos para este engram.'}</p>
          )}

          {activeTab === 'cards' && slVideos.length > 0 && (
            <div className="mt-4 space-y-3">
              <div className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-indigo-300">
                <Video className="h-4 w-4" />
                <span>
                  {t('brain.videoLessons')} ({slVideos.length})
                </span>
              </div>
              <div className="space-y-2">
                {slVideos.map((v) => (
                  <video key={v.IllustrationId} src={`${API_BASE_URL}/illustrations/structured-learning/${v.IllustrationId}/image`} controls className="w-full rounded-xl border border-white/10 bg-black/40 shadow-md" />
                ))}
              </div>
            </div>
          )}

          {activeTab === 'cards' && deepDiveCards.length > 0 && (
            <div className="mt-4">
              <div className="flex items-center justify-between">
                <p className="text-xs font-semibold uppercase tracking-wide text-white/50">
                  {t('brain.reviewCards')} ({deepDiveCards.length})
                </p>
                <button type="button" onClick={openRandomCard} className="flex items-center gap-1 rounded-full bg-white/10 px-2.5 py-1 text-[11px] font-medium text-white/80 hover:bg-white/20">
                  <Shuffle className="h-3 w-3" />
                  {t('brain.random')}
                </button>
              </div>
              <div className="mt-2 grid grid-cols-2 gap-2">
                {deepDiveCards.map((card) => {
                  const cardImg = card.illustrationId
                    ? card.isStructuredLearning
                      ? `${API_BASE_URL}/illustrations/structured-learning/${card.illustrationId}/image`
                      : `${API_BASE_URL}/illustrations/${card.illustrationId}/image`
                    : card.illustrationIds && card.illustrationIds.length > 0
                    ? card.isStructuredLearning
                      ? `${API_BASE_URL}/illustrations/structured-learning/${card.illustrationIds[0]}/image`
                      : `${API_BASE_URL}/illustrations/${card.illustrationIds[0]}/image`
                    : null;

                  return (
                    <button
                      key={card.index}
                      type="button"
                      onClick={() => {
                        setCardAgentActive(false);
                        setCardModalIndex(card.index);
                      }}
                      className="group flex flex-col items-start gap-2 overflow-hidden rounded-xl border border-white/10 bg-white/5 p-2.5 text-left transition hover:border-white/20 hover:bg-white/10"
                    >
                      {cardImg ? (
                        <div className="h-28 w-full overflow-hidden rounded-lg bg-black/40">
                          <img
                            src={cardImg}
                            alt={card.title}
                            className="h-full w-full object-contain object-center transition group-hover:scale-105"
                          />
                        </div>
                      ) : (
                        <span className="text-2xl leading-none">{card.emoji ?? '🧠'}</span>
                      )}
                      <span className="line-clamp-2 text-xs font-semibold text-white/90 group-hover:text-white">{card.title}</span>
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {activeTab === 'cards' && slVideos.length === 0 && deepDiveCards.length === 0 && (
            <p className="mt-4 text-sm text-white/50">{language === 'en' ? 'No review cards or videos for this engram yet.' : 'Aún no hay tarjetas de repaso ni videos para este engram.'}</p>
          )}

          {neuron.isStructuredLearning ? (
            <Link
              to={`/structured-learning/${neuron.structuredLearningDocumentId}/sections/${neuron.structuredLearningSectionId}`}
              className="mt-2.5 flex w-full items-center justify-center gap-1.5 rounded-xl border border-white/15 bg-white/5 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-white/10"
            >
              <RotateCcw className="h-4 w-4" />
              {t('brain.openFullLesson')}
            </Link>
          ) : (
            <Link
              to={`/capabilities/${neuron.capabilityId}/nodes/${neuron.capabilityGraphNodeId}?from=brain`}
              className="mt-2.5 flex w-full items-center justify-center gap-1.5 rounded-xl border border-white/15 bg-white/5 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-white/10"
            >
              <RotateCcw className="h-4 w-4" />
              {t('brain.openFullLesson')}
            </Link>
          )}

          {neuron.isStructuredLearning && chapterEngrams.length > 0 && (
            <div className="mt-6 border-t border-white/10 pt-5">
              <div className="flex items-center justify-between gap-2">
                <div className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-indigo-400">
                  <BookOpen className="h-3.5 w-3.5" />
                  <span>{t('brain.chapterEngrams')}</span>
                </div>
                {chapterTitle && (
                  <span className="truncate max-w-[220px] text-[11px] font-medium text-white/50" title={chapterTitle}>
                    {chapterTitle}
                  </span>
                )}
              </div>

              <div className="mt-3 space-y-1.5">
                {chapterEngrams.map((item, idx) => {
                  const isCurrent = item.StructuredLearningDocumentSectionId === neuron.structuredLearningSectionId;
                  const engramTitle = item.SubchapterTitle || item.ChapterTitle;

                  return (
                    <button
                      key={item.StructuredLearningDocumentSectionId}
                      type="button"
                      onClick={() => handleSelectChapterEngram(item)}
                      disabled={isCurrent}
                      className={`group flex w-full items-center justify-between gap-2.5 rounded-xl border p-2.5 text-left transition ${
                        isCurrent
                          ? 'border-indigo-500/50 bg-indigo-500/15 text-white shadow-sm ring-1 ring-indigo-500/30 cursor-default'
                          : 'border-white/5 bg-white/[0.02] text-white/80 hover:border-white/15 hover:bg-white/[0.06] cursor-pointer'
                      }`}
                    >
                      <div className="flex min-w-0 flex-1 items-center gap-2.5">
                        <span
                          className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-lg text-[11px] font-bold ${
                            isCurrent
                              ? 'bg-indigo-500 text-white'
                              : 'bg-white/10 text-white/60 group-hover:bg-white/20 group-hover:text-white'
                          }`}
                        >
                          {idx + 1}
                        </span>
                        <div className="min-w-0 flex-1">
                          <p className={`truncate text-xs ${isCurrent ? 'font-semibold text-white' : 'font-medium text-white/80 group-hover:text-white'}`}>
                            {engramTitle}
                          </p>
                        </div>
                      </div>

                      {isCurrent ? (
                        <span className="shrink-0 rounded-full bg-indigo-500/30 px-2 py-0.5 text-[10px] font-bold text-indigo-200">
                          {t('brain.currentEngram')}
                        </span>
                      ) : (
                        <span className="shrink-0 rounded-lg p-1 text-white/40 group-hover:bg-white/10 group-hover:text-white transition">
                          <ArrowRight className="h-3.5 w-3.5" />
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
            </div>
          )}
        </motion.div>
      </AnimatePresence>

      {cardModalIndex !== null && deepDiveCards[cardModalIndex] && (
        <DeepDiveCardModal
          card={deepDiveCards[cardModalIndex]}
          personId={personId}
          capabilityGraphNodeId={neuron.isStructuredLearning ? undefined : neuron.capabilityGraphNodeId}
          structuredLearningDeepDiveStepId={neuron.isStructuredLearning ? slDeepDiveStepId : undefined}
          agentActive={cardAgentActive}
          onClose={() => {
            setCardModalIndex(null);
            setCardAgentActive(false);
          }}
          onAskAgent={() => setCardAgentActive(true)}
        />
      )}

      {engramOpen &&
        (neuron.isStructuredLearning ? (
          slTeachingStepId && (
            <div className="fixed inset-0 z-[70] flex items-center justify-center bg-slate-950/80 p-4 backdrop-blur-sm">
              <div className="relative w-full max-w-3xl rounded-3xl border border-white/10 bg-slate-900 p-6 shadow-2xl max-h-[90vh] overflow-y-auto">
                <button
                  type="button"
                  onClick={() => setEngramOpen(false)}
                  className="absolute right-4 top-4 rounded-full p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
                  aria-label={language === 'en' ? 'Close' : 'Cerrar'}
                >
                  <X className="h-5 w-5" />
                </button>
                <h3 className="text-base font-semibold text-white">{neuron.name}</h3>
                {slTeachingIllustrationId && (
                  <img
                    src={`${API_BASE_URL}/illustrations/structured-learning/${slTeachingIllustrationId}/image`}
                    alt={neuron.name}
                    className="mt-3 h-auto max-h-[28rem] w-full rounded-2xl object-contain bg-black/20"
                  />
                )}
                <div className="mt-4">
                  <VoiceTutorAgent structuredLearningSectionStepId={slTeachingStepId} />
                </div>
              </div>
            </div>
          )
        ) : (
          <EngramReviewAgent personId={personId} capabilityGraphNodeId={neuron.capabilityGraphNodeId} onExit={() => setEngramOpen(false)} />
        ))}
    </>
  );
}
