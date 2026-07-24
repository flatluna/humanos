import { motion } from "framer-motion";
import { ArrowDown, Share2 } from "lucide-react";
import { useTranslation } from "react-i18next";

const linearStepKeys = ["chapter1", "chapter2", "chapter3", "chapter4", "chapter5"] as const;

/** Node positions (in % of the container) for the capability-graph
 *  visualization, plus which other nodes each one connects to. Mirrors the
 *  mastered / in-progress / locked legend already used in the student app's
 *  capability graph view (humanlearn/src/components/MemoryGraphView.tsx). */
interface GraphNode {
  id: string;
  labelKey: "foundations" | "coreConcept" | "relatedIdea" | "appliedSkill" | "advancedTopic" | "specialization" | "mastery";
  x: number;
  y: number;
  state: "mastered" | "active" | "locked";
}

const nodes: GraphNode[] = [
  { id: "a", labelKey: "foundations", x: 50, y: 10, state: "mastered" },
  { id: "b", labelKey: "coreConcept", x: 20, y: 38, state: "mastered" },
  { id: "c", labelKey: "relatedIdea", x: 78, y: 38, state: "mastered" },
  { id: "d", labelKey: "appliedSkill", x: 15, y: 72, state: "active" },
  { id: "e", labelKey: "advancedTopic", x: 50, y: 60, state: "active" },
  { id: "f", labelKey: "specialization", x: 82, y: 74, state: "locked" },
  { id: "g", labelKey: "mastery", x: 50, y: 94, state: "locked" },
];

const edges: [string, string][] = [
  ["a", "b"],
  ["a", "c"],
  ["b", "d"],
  ["b", "e"],
  ["c", "e"],
  ["c", "f"],
  ["d", "g"],
  ["e", "g"],
  ["f", "g"],
];

const stateStyles: Record<GraphNode["state"], string> = {
  mastered: "border-teal-300/60 bg-teal-400/20 text-teal-100",
  active: "border-blue-300/60 bg-blue-400/20 text-blue-100",
  locked: "border-white/10 bg-white/[0.04] text-white/40",
};

function nodeById(id: string): GraphNode {
  return nodes.find((n) => n.id === id)!;
}

export default function GraphLearning() {
  const { t } = useTranslation();
  const linearSteps = linearStepKeys.map((key) => t(`landing.graphLearning.linearSteps.${key}`));

  return (
    <section className="relative bg-white py-28 sm:py-36 overflow-hidden">
      <div className="max-w-6xl mx-auto px-6">
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.6 }}
          className="text-center"
        >
          <p className="text-sm font-medium uppercase tracking-widest text-blue-600">
            {t("landing.graphLearning.eyebrow")}
          </p>
          <h2 className="mt-3 text-4xl sm:text-5xl font-semibold tracking-tight text-slate-900 max-w-3xl mx-auto">
            {t("landing.graphLearning.title")}
          </h2>
          <p className="mt-6 text-lg text-slate-500 max-w-2xl mx-auto leading-relaxed">
            {t("landing.graphLearning.subtitle")}
          </p>
        </motion.div>

        <div className="mt-16 grid md:grid-cols-2 gap-6 items-stretch">
          {/* Traditional courses — rigid, linear */}
          <motion.div
            initial={{ opacity: 0, x: -24 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true, amount: 0.3 }}
            transition={{ duration: 0.6 }}
            className="rounded-3xl border border-slate-200 bg-slate-50 p-10"
          >
            <h3 className="text-xl font-semibold text-slate-900">{t("landing.graphLearning.traditionalTitle")}</h3>

            <div className="mt-8 flex flex-col items-center gap-3">
              {linearSteps.map((step, i) => (
                <div key={step} className="flex flex-col items-center gap-3">
                  <div className="rounded-xl border border-slate-200 bg-white px-6 py-3 text-slate-600">
                    {step}
                  </div>
                  {i < linearSteps.length - 1 && <ArrowDown className="h-4 w-4 text-slate-300" />}
                </div>
              ))}
            </div>

            <p className="mt-8 text-sm leading-relaxed text-slate-400">
              {t("landing.graphLearning.traditionalDescription")}
            </p>
          </motion.div>

          {/* Human OS capability graph — interconnected network */}
          <motion.div
            initial={{ opacity: 0, x: 24 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true, amount: 0.3 }}
            transition={{ duration: 0.6, delay: 0.1 }}
            className="relative rounded-3xl border border-blue-400/20 bg-gradient-to-b from-[#0a0d16] to-[#05060a] p-10 text-white overflow-hidden"
          >
            <div className="absolute right-6 top-6 flex items-center gap-1.5 rounded-full border border-white/10 bg-white/10 px-3 py-1 text-xs text-white/70">
              <Share2 className="h-3 w-3 text-blue-300" />
              {t("landing.graphLearning.badge")}
            </div>

            <h3 className="text-xl font-semibold">{t("landing.graphLearning.humanOsTitle")}</h3>

            <div className="relative mt-8 h-[340px] w-full">
              <svg className="absolute inset-0 h-full w-full" preserveAspectRatio="none">
                {edges.map(([fromId, toId]) => {
                  const from = nodeById(fromId);
                  const to = nodeById(toId);
                  return (
                    <line
                      key={`${fromId}-${toId}`}
                      x1={`${from.x}%`}
                      y1={`${from.y}%`}
                      x2={`${to.x}%`}
                      y2={`${to.y}%`}
                      stroke="rgba(255,255,255,0.14)"
                      strokeWidth={1.5}
                    />
                  );
                })}
              </svg>

              {nodes.map((node, i) => (
                <motion.div
                  key={node.id}
                  initial={{ opacity: 0, scale: 0.6 }}
                  whileInView={{ opacity: 1, scale: 1 }}
                  viewport={{ once: true, amount: 0.4 }}
                  transition={{ duration: 0.4, delay: 0.2 + i * 0.06 }}
                  className={`absolute -translate-x-1/2 -translate-y-1/2 whitespace-nowrap rounded-full border px-3 py-1.5 text-xs font-medium backdrop-blur ${stateStyles[node.state]}`}
                  style={{ left: `${node.x}%`, top: `${node.y}%` }}
                >
                  {t(`landing.graphLearning.nodes.${node.labelKey}`)}
                </motion.div>
              ))}
            </div>

            <div className="mt-2 flex flex-wrap gap-4 text-xs text-white/40">
              <span className="flex items-center gap-1.5">
                <span className="h-2 w-2 rounded-full bg-teal-300" /> {t("landing.graphLearning.legend.mastered")}
              </span>
              <span className="flex items-center gap-1.5">
                <span className="h-2 w-2 rounded-full bg-blue-300" /> {t("landing.graphLearning.legend.inProgress")}
              </span>
              <span className="flex items-center gap-1.5">
                <span className="h-2 w-2 rounded-full bg-white/30" /> {t("landing.graphLearning.legend.locked")}
              </span>
            </div>

            <p className="mt-6 text-sm leading-relaxed text-white/70">
              {t("landing.graphLearning.description")}
            </p>
          </motion.div>
        </div>

        <motion.p
          initial={{ opacity: 0, y: 16 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.6 }}
          transition={{ duration: 0.6, delay: 0.15 }}
          className="mt-16 text-center text-lg text-slate-500 max-w-2xl mx-auto"
        >
          {t("landing.graphLearning.footer")}
        </motion.p>
      </div>
    </section>
  );
}
