import { motion } from "framer-motion";

export default function Vision() {
  return (
    <section className="relative py-32 sm:py-44 bg-white overflow-hidden">
      <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
        <div className="h-[600px] w-[600px] rounded-full bg-gradient-to-br from-blue-500/10 via-teal-400/10 to-violet-500/10 blur-[100px]" />
      </div>

      <div className="relative max-w-4xl mx-auto px-6 text-center">
        <motion.h2
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.7 }}
          className="text-4xl sm:text-5xl lg:text-6xl font-semibold tracking-tight text-slate-900 leading-[1.1]"
        >
          A World Where AI Amplifies
          <span className="block bg-gradient-to-r from-blue-600 via-teal-500 to-violet-600 bg-clip-text text-transparent">
            Human Potential
          </span>
        </motion.h2>

        <motion.p
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.7, delay: 0.15 }}
          className="mt-8 text-xl text-slate-500 leading-relaxed max-w-2xl mx-auto"
        >
          Human Operative System exists to ensure that artificial
          intelligence becomes an amplifier of human capability rather than a
          substitute for it.
        </motion.p>

        <motion.div
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.6 }}
          transition={{ duration: 0.7, delay: 0.3 }}
          className="mt-14 grid sm:grid-cols-2 gap-6 max-w-xl mx-auto"
        >
          <div className="rounded-2xl border border-slate-200 p-6 text-left">
            <p className="text-sm font-medium uppercase tracking-widest text-slate-400">Not</p>
            <p className="mt-2 text-2xl font-semibold text-slate-400 line-through decoration-slate-300">
              Convenience
            </p>
          </div>
          <div className="rounded-2xl border border-slate-900 bg-slate-900 p-6 text-left">
            <p className="text-sm font-medium uppercase tracking-widest text-white/40">The goal</p>
            <p className="mt-2 text-2xl font-semibold text-white">Capability</p>
          </div>
          <div className="rounded-2xl border border-slate-200 p-6 text-left">
            <p className="text-sm font-medium uppercase tracking-widest text-slate-400">Not</p>
            <p className="mt-2 text-2xl font-semibold text-slate-400 line-through decoration-slate-300">
              Information
            </p>
          </div>
          <div className="rounded-2xl border border-slate-900 bg-slate-900 p-6 text-left">
            <p className="text-sm font-medium uppercase tracking-widest text-white/40">The goal</p>
            <p className="mt-2 text-2xl font-semibold text-white">Transformation</p>
          </div>
        </motion.div>
      </div>
    </section>
  );
}
