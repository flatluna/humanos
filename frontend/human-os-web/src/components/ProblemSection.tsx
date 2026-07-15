import { motion } from "framer-motion";
import { Search, Video, BookOpen, Bot, Files, Brain, Wrench, Target, Puzzle, Gem } from "lucide-react";

const information = [
  { icon: Search, label: "Search Engines" },
  { icon: Bot, label: "AI Tools" },
  { icon: Video, label: "Videos" },
  { icon: BookOpen, label: "Courses" },
  { icon: Files, label: "Content" },
];

const capability = [
  { icon: Brain, label: "Memory" },
  { icon: Wrench, label: "Thinking" },
  { icon: Target, label: "Application" },
  { icon: Puzzle, label: "Problem Solving" },
  { icon: Gem, label: "Value Creation" },
];

export default function ProblemSection() {
  return (
    <section className="relative bg-white py-28 sm:py-36">
      <div className="max-w-6xl mx-auto px-6">
        <motion.h2
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.6 }}
          className="text-center text-4xl sm:text-5xl font-semibold tracking-tight text-slate-900 max-w-3xl mx-auto"
        >
          Unlimited Information Does Not Create Unlimited Capability
        </motion.h2>

        <div className="mt-20 grid md:grid-cols-2 gap-6">
          <motion.div
            initial={{ opacity: 0, x: -24 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true, amount: 0.4 }}
            transition={{ duration: 0.6 }}
            className="rounded-3xl border border-slate-200 bg-slate-50 p-10"
          >
            <p className="text-sm font-medium uppercase tracking-widest text-slate-400">
              Everywhere
            </p>
            <h3 className="mt-2 text-2xl font-semibold text-slate-900">Information</h3>

            <div className="mt-8 space-y-4">
              {information.map(({ icon: Icon, label }) => (
                <div key={label} className="flex items-center gap-3 rounded-xl bg-white px-4 py-3 border border-slate-200">
                  <Icon className="h-4.5 w-4.5 text-slate-400" />
                  <span className="text-slate-600">{label}</span>
                </div>
              ))}
            </div>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, x: 24 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true, amount: 0.4 }}
            transition={{ duration: 0.6 }}
            className="rounded-3xl bg-slate-900 p-10 text-white"
          >
            <p className="text-sm font-medium uppercase tracking-widest text-white/40">
              What actually matters
            </p>
            <h3 className="mt-2 text-2xl font-semibold">Capability</h3>

            <div className="mt-8 space-y-4">
              {capability.map(({ icon: Icon, label }) => (
                <div key={label} className="flex items-center gap-3 rounded-xl bg-white/5 px-4 py-3 border border-white/10">
                  <Icon className="h-4.5 w-4.5 text-blue-400" />
                  <span className="text-white/80">{label}</span>
                </div>
              ))}
            </div>
          </motion.div>
        </div>

        <motion.p
          initial={{ opacity: 0, y: 16 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.6 }}
          transition={{ duration: 0.6, delay: 0.15 }}
          className="mt-16 text-center text-lg text-slate-500 max-w-2xl mx-auto"
        >
          Human progress is not determined by what we can access. It is
          determined by what we can understand, remember, apply, and create.
        </motion.p>
      </div>
    </section>
  );
}
