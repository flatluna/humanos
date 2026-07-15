import { motion } from "framer-motion";
import { BrainCircuit, Workflow, Radar, Users, Eye, TrendingUp, type LucideIcon } from "lucide-react";

const benefits: { icon: LucideIcon; label: string }[] = [
  { icon: BrainCircuit, label: "Capability intelligence" },
  { icon: Workflow, label: "Workforce adaptability" },
  { icon: Radar, label: "Future readiness" },
  { icon: Users, label: "Human-centered AI adoption" },
  { icon: Eye, label: "Skills visibility" },
  { icon: TrendingUp, label: "Growth measurement" },
];

export default function ForOrganizations() {
  return (
    <section className="relative py-28 sm:py-36 bg-[#05060a] text-white overflow-hidden">
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute top-1/4 right-0 h-[420px] w-[420px] rounded-full bg-blue-600/20 blur-[140px]" />
      </div>

      <div className="relative max-w-6xl mx-auto px-6 grid lg:grid-cols-2 gap-16 items-center">
        <div className="order-2 lg:order-1 grid sm:grid-cols-2 gap-4">
          {benefits.map(({ icon: Icon, label }, i) => (
            <motion.div
              key={label}
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, amount: 0.4 }}
              transition={{ duration: 0.5, delay: i * 0.06 }}
              className="flex items-center gap-3 rounded-2xl border border-white/10 bg-white/[0.04] px-5 py-4"
            >
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white/10 border border-white/10">
                <Icon className="h-4.5 w-4.5 text-blue-300" />
              </div>
              <span className="font-medium text-white/80">{label}</span>
            </motion.div>
          ))}
        </div>

        <motion.div
          initial={{ opacity: 0, x: 24 }}
          whileInView={{ opacity: 1, x: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.6 }}
          className="order-1 lg:order-2"
        >
          <p className="text-sm font-medium uppercase tracking-widest text-blue-400">
            For Organizations
          </p>
          <h2 className="mt-3 text-4xl sm:text-5xl font-semibold tracking-tight">
            Build a More Capable Workforce
          </h2>
          <p className="mt-6 text-lg text-white/50 leading-relaxed max-w-lg">
            Human OS gives organizations real visibility into the
            capabilities of their people — enabling human-centered AI
            adoption and a workforce built to keep adapting.
          </p>
        </motion.div>
      </div>
    </section>
  );
}
