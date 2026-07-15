import { motion } from "framer-motion";
import { ArrowDown, Zap } from "lucide-react";

const traditional = ["Question", "Answer", "Dependency"];
const humanOs = ["Question", "Practice", "Recall", "Application", "Evidence", "Mastery"];

const neuroscience = [
  "Retrieval Practice",
  "Memory Consolidation",
  "Schema Formation",
  "Capability Development",
];

export default function MemoryParadox() {
  return (
    <section className="relative bg-[#05060a] py-28 sm:py-36 text-white overflow-hidden">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute top-0 left-1/4 h-[420px] w-[420px] rounded-full bg-blue-600/20 blur-[140px]" />
        <div className="absolute bottom-0 right-1/4 h-[420px] w-[420px] rounded-full bg-violet-600/20 blur-[140px]" />
      </div>

      <div className="relative max-w-6xl mx-auto px-6">
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.6 }}
          className="text-center"
        >
          <h2 className="text-4xl sm:text-5xl font-semibold tracking-tight">
            The Memory Paradox
          </h2>
          <p className="mt-4 text-lg text-white/50">
            The more we outsource thinking, the less we strengthen it.
          </p>
        </motion.div>

        <div className="grid md:grid-cols-2 gap-6 mt-16">
          <motion.div
            initial={{ opacity: 0, y: 24 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true, amount: 0.3 }}
            transition={{ duration: 0.6 }}
            className="rounded-3xl border border-white/10 bg-white/[0.03] p-10"
          >
            <h3 className="text-xl font-semibold text-white/90">Traditional AI</h3>

            <div className="mt-8 flex flex-col items-center gap-3">
              {traditional.map((step, i) => (
                <div key={step} className="flex flex-col items-center gap-3">
                  <div className="rounded-xl border border-white/10 bg-white/5 px-6 py-3 text-white/70">
                    {step}
                  </div>
                  {i < traditional.length - 1 && <ArrowDown className="h-4 w-4 text-white/20" />}
                </div>
              ))}
            </div>

            <p className="mt-8 text-sm leading-relaxed text-white/40">
              Designed for convenience. Little cognitive effort. Limited
              capability growth.
            </p>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, y: 24 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true, amount: 0.3 }}
            transition={{ duration: 0.6, delay: 0.1 }}
            className="relative rounded-3xl border border-blue-400/20 bg-gradient-to-b from-blue-600/20 to-violet-600/10 p-10"
          >
            <div className="absolute right-6 top-6 flex items-center gap-1.5 rounded-full border border-white/10 bg-white/10 px-3 py-1 text-xs text-white/70">
              <Zap className="h-3 w-3 text-blue-300" />
              Human OS
            </div>

            <h3 className="text-xl font-semibold text-white">Human Operative System</h3>

            <div className="mt-8 flex flex-col items-center gap-3">
              {humanOs.map((step, i) => (
                <div key={step} className="flex flex-col items-center gap-3">
                  <div className="rounded-xl border border-white/15 bg-white/10 px-6 py-3 font-medium text-white">
                    {step}
                  </div>
                  {i < humanOs.length - 1 && <ArrowDown className="h-4 w-4 text-blue-300/60" />}
                </div>
              ))}
            </div>

            <p className="mt-8 text-sm leading-relaxed text-white/70">
              Designed for human growth. Strengthens memory, thinking, and
              capability.
            </p>
          </motion.div>
        </div>

        <motion.div
          initial={{ opacity: 0, y: 16 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.6 }}
          transition={{ duration: 0.6, delay: 0.2 }}
          className="mt-14 flex flex-wrap justify-center gap-3"
        >
          {neuroscience.map((term) => (
            <span
              key={term}
              className="rounded-full border border-white/10 bg-white/5 px-4 py-1.5 text-sm text-white/60"
            >
              {term}
            </span>
          ))}
        </motion.div>
      </div>
    </section>
  );
}
