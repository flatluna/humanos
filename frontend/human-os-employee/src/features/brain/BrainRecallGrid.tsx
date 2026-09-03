import { useEffect, useMemo, useState } from 'react';
import { motion } from 'framer-motion';
import { BookOpen, Layers, Sparkles, Zap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { RootGroup, SelectedNeuron } from './types';
import { getSubjectColor } from './subjectColor';
import { getMemoryColor, getMemoryTextColor, getRetrievability, isAutomatic } from './memoryScience';
import { useI18n } from '@/features/capabilities/i18n/useI18n';

/** Same 8-color palette used across the feature — one color per assigned
 *  capability root, so everything belonging to one area reads as one
 *  visual "family". Ported from human-os-web's /brain feature. */
const CLUSTER_HEX = ['#22d3ee', '#e879f9', '#34d399', '#fbbf24', '#fb7185', '#38bdf8', '#a3e635', '#f87171'];

const DAY_MS = 24 * 60 * 60 * 1000;

function relativeLabel(lastCompletedDate: string | undefined, lang: 'en' | 'es') {
  if (!lastCompletedDate) return '—';
  const days = Math.floor((Date.now() - new Date(lastCompletedDate).getTime()) / DAY_MS);
  const rtf = new Intl.RelativeTimeFormat(lang, { numeric: 'auto' });
  if (days < 1) return rtf.format(0, 'day');
  if (days < 30) return rtf.format(-days, 'day');
  if (days < 365) return rtf.format(-Math.round(days / 30), 'month');
  return rtf.format(-Math.round(days / 365), 'year');
}

export interface BrainRecallGridProps {
  rootGroups: RootGroup[];
  query: string;
  onSelectNeuron: (neuron: SelectedNeuron) => void;
}

/** A friendly, colorful "what do I already know" recall dashboard — grouped
 *  by capability (each assigned course is its own area, since this portal
 *  has no Programs concept). Every mastered node is a clickable card with a
 *  live "memory strength" ring — a real forgetting-curve (power-law
 *  retrievability, see memoryScience.ts) that decays the longer it's been
 *  since the employee last reviewed it, turning the page into a gentle
 *  "what should I refresh?" queue rather than a network diagram. Ported
 *  from human-os-web's /brain (2026-08-01). */
export function BrainRecallGrid({ rootGroups, query, onSelectNeuron }: BrainRecallGridProps) {
  const { language } = useI18n();
  const { t } = useTranslation();
  const normalizedQuery = query.trim().toLowerCase();

  // Forces a re-render every minute so the memory-strength rings keep
  // "breathing"/decaying live in the browser without needing a refetch.
  const [, tick] = useState(0);
  useEffect(() => {
    const id = window.setInterval(() => tick((n) => n + 1), 60_000);
    return () => window.clearInterval(id);
  }, []);

  const sections = useMemo(() => {
    return rootGroups
      .map((root, rootIndex) => {
        const colorHex = CLUSTER_HEX[rootIndex % CLUSTER_HEX.length];
        const clusters = root.clusters
          .map((cluster) => {
            const nodes = cluster.nodes
              .filter((node) => normalizedQuery === '' || node.name.toLowerCase().includes(normalizedQuery))
              // Weakest memory (lowest retrievability) first — turns the grid into an actionable queue.
              .slice()
              .sort((a, b) => {
                const aR = getRetrievability(a.lastCompletedDate, a.reviewCount ?? 1) ?? -1;
                const bR = getRetrievability(b.lastCompletedDate, b.reviewCount ?? 1) ?? -1;
                return aR - bR;
              });
            return { cluster, nodes };
          })
          .filter((c) => c.nodes.length > 0);
        return { root, colorHex, clusters };
      })
      .filter((s) => s.clusters.length > 0);
  }, [rootGroups, normalizedQuery]);

  if (sections.length === 0) {
    return (
      <div className="flex h-full w-full items-center justify-center p-10 text-center">
        <p className="text-sm text-white/50">
          {t('brain.noMatches')}
        </p>
      </div>
    );
  }

  return (
    <div className="h-full w-full overflow-y-auto p-4 sm:p-6">
      <div className="space-y-8">
        {sections.map(({ root, colorHex, clusters }) => (
          <section key={root.rootId}>
            <div className="mb-3 flex items-center gap-2">
              <div
                className="flex h-8 w-8 shrink-0 items-center justify-center rounded-xl text-white shadow-lg"
                style={{ background: `linear-gradient(135deg, ${colorHex}, ${colorHex}99)` }}
              >
                <BookOpen className="h-4 w-4" />
              </div>
              <h2 className="text-base font-semibold text-white">{root.rootName}</h2>
            </div>

            <div className="space-y-5 border-l-2 pl-4" style={{ borderColor: `${colorHex}40` }}>
              {clusters.map(({ cluster, nodes }) => (
                <div key={cluster.capabilityId}>
                  <div className="mb-2 flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-white/50">
                    <Layers className="h-3 w-3" />
                    {cluster.capabilityName}
                    {cluster.subjectName && (
                      <span
                        className="flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold normal-case tracking-normal text-white"
                        style={{ background: `${getSubjectColor(cluster.subjectCode ?? cluster.subjectName)}55` }}
                      >
                        <span
                          className="h-1.5 w-1.5 rounded-full"
                          style={{ background: getSubjectColor(cluster.subjectCode ?? cluster.subjectName) }}
                        />
                        {cluster.subjectName}
                      </span>
                    )}
                  </div>
                  <div className="flex flex-wrap gap-3">
                    {nodes.map((node, i) => {
                      const retrievability = getRetrievability(node.lastCompletedDate, node.reviewCount ?? 1);
                      const memoryColor = getMemoryColor(retrievability);
                      const memoryTextColor = getMemoryTextColor(retrievability);
                      const percent = retrievability === null ? null : Math.round(retrievability * 100);
                      const isFading = percent !== null && percent < 30;
                      const automatic = isAutomatic(node.reviewCount ?? 0);
                      const isDim = normalizedQuery !== '' && !node.name.toLowerCase().includes(normalizedQuery);
                      return (
                        <motion.button
                          key={node.capabilityGraphNodeId}
                          type="button"
                          initial={{ opacity: 0, y: 6 }}
                          animate={{ opacity: isDim ? 0.4 : 1, y: 0 }}
                          transition={{ duration: 0.2, delay: Math.min(i, 10) * 0.02 }}
                          whileHover={{ scale: 1.04 }}
                          whileTap={{ scale: 0.97 }}
                          onClick={() =>
                            onSelectNeuron({
                              ...node,
                              capabilityId: cluster.capabilityId,
                              capabilityName: cluster.capabilityName,
                            })
                          }
                          className="group relative flex w-40 flex-col gap-2 rounded-2xl p-3.5 text-left shadow-lg ring-1 transition-shadow hover:shadow-xl"
                          style={{
                            background: `linear-gradient(155deg, ${colorHex}33, rgba(15,23,42,0.9))`,
                            boxShadow: `0 0 0 1px ${memoryColor}55, 0 8px 24px -8px ${memoryColor}40`,
                          }}
                        >
                          <div className="flex items-center justify-between">
                            <div className="relative flex h-9 w-9 shrink-0 items-center justify-center">
                              <motion.div
                                className={`flex h-9 w-9 items-center justify-center rounded-full border border-white/20 text-[11px] font-bold ${isFading ? 'animate-pulse' : ''}`}
                                animate={{ background: memoryColor, color: memoryTextColor }}
                                transition={{ duration: 1, ease: 'easeOut' }}
                              >
                                {node.name.charAt(0).toUpperCase()}
                              </motion.div>
                            </div>
                            {percent !== null && (
                              <span
                                className={`flex items-center gap-0.5 text-xs font-bold ${automatic ? 'text-violet-300' : ''}`}
                                style={automatic ? undefined : { color: memoryColor }}
                                title={
                                  automatic
                                    ? (language === 'en'
                                        ? 'This skill became automatic (like riding a bike) after enough practice.'
                                        : 'Esta habilidad ya se volvió automática (como andar en bici) tras suficiente práctica — casi no se desvanece.')
                                    : undefined
                                }
                              >
                                {automatic && <Zap className="h-3 w-3" />}
                                {automatic ? t('brain.automatic') : `${percent}%`}
                              </span>
                            )}
                          </div>
                          <p className="line-clamp-2 text-sm font-semibold text-white">{node.name}</p>
                          <p className="text-[11px] text-white/50">{relativeLabel(node.lastCompletedDate, language)}</p>
                        </motion.button>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          </section>
        ))}
      </div>
      <div className="mt-2 flex items-center gap-1.5 pb-2 text-[11px] text-white/30">
        <Sparkles className="h-3 w-3" />
        <span>{t('brain.selectNeuronHint')}</span>
      </div>
    </div>
  );
}
