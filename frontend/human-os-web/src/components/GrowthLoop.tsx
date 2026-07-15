import { motion } from "framer-motion";
import { Compass, Repeat, Brain, Rocket, FileCheck2, Award, Gem, type LucideIcon } from "lucide-react";

interface Step {
  icon: LucideIcon;
  label: string;
  description: string;
}

const steps: Step[] = [
  { icon: Compass, label: "Discover", description: "Identify strengths, interests, and the capabilities worth building." },
  { icon: Repeat, label: "Practice", description: "Deliberate repetition that builds real cognitive skill." },
  { icon: Brain, label: "Recall", description: "Active retrieval strengthens memory and consolidation." },
  { icon: Rocket, label: "Apply", description: "Use the capability in real, meaningful contexts." },
  { icon: FileCheck2, label: "Evidence", description: "Capture proof of application and progress over time." },
  { icon: Award, label: "Mastery", description: "Consistent application compounds into true expertise." },
  { icon: Gem, label: "Value", description: "Mastery translates into opportunity, income, and impact." },
];

export default function GrowthLoop() {
  return (
    <section className="py-28 sm:py-36 bg-[#05060a] text-white">
      <div className="max-w-7xl mx-auto px-6">
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.6 }}
          className="text-center"
        >
          <h2 className="text-4xl sm:text-5xl font-semibold tracking-tight">
            How Human Growth Happens
          </h2>
          <p className="mt-4 text-lg text-white/50 max-w-xl mx-auto">
            The operating system for human growth — a continuous loop, not a
            one-time course.
          </p>
        </motion.div>

        <div className="mt-20 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {steps.map(({ icon: Icon, label, description }, i) => (
            <motion.div
              key={label}
              initial={{ opacity: 0, y: 24 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, amount: 0.4 }}
              transition={{ duration: 0.5, delay: i * 0.06 }}
              className="group relative rounded-2xl border border-white/10 bg-white/[0.03] p-6 transition hover:border-white/20 hover:bg-white/[0.06]"
            >
              <div className="flex items-center gap-2 text-xs font-medium text-white/30">
                <span>{String(i + 1).padStart(2, "0")}</span>
              </div>
              <div className="mt-4 flex h-11 w-11 items-center justify-center rounded-xl bg-gradient-to-br from-blue-500/20 to-violet-500/20 border border-white/10">
                <Icon className="h-5 w-5 text-blue-300" />
              </div>
              <h3 className="mt-5 text-lg font-semibold">{label}</h3>
              <p className="mt-2 text-sm leading-relaxed text-white/50">{description}</p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
