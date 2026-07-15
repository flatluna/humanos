import { motion } from "framer-motion";
import { ArrowRight, Sparkles } from "lucide-react";

const flow = ["Mind", "Memory", "Practice", "Recall", "Application", "Capability", "Mastery", "Value"];

export default function Hero() {
  return (
    <section className="relative min-h-screen flex items-center overflow-hidden bg-[#05060a] text-white">
      {/* Ambient background */}
      <div className="pointer-events-none absolute inset-0">
        <div className="absolute -top-32 -left-32 h-[520px] w-[520px] rounded-full bg-blue-600/30 blur-[140px]" />
        <div className="absolute top-1/3 -right-40 h-[480px] w-[480px] rounded-full bg-violet-600/25 blur-[140px]" />
        <div className="absolute bottom-0 left-1/3 h-[420px] w-[420px] rounded-full bg-teal-400/20 blur-[140px]" />
        <div
          className="absolute inset-0 opacity-[0.06]"
          style={{
            backgroundImage:
              "linear-gradient(to right, #fff 1px, transparent 1px), linear-gradient(to bottom, #fff 1px, transparent 1px)",
            backgroundSize: "64px 64px",
          }}
        />
      </div>

      <div className="relative max-w-7xl mx-auto px-6 py-32 grid lg:grid-cols-2 gap-16 items-center">
        <div>
          <motion.div
            initial={{ opacity: 0, y: 16 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6 }}
            className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/5 px-4 py-1.5 text-sm text-white/70 backdrop-blur"
          >
            <Sparkles className="h-3.5 w-3.5 text-blue-400" />
            A new category of human development
          </motion.div>

          <motion.h1
            initial={{ opacity: 0, y: 40 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.7, delay: 0.1 }}
            className="mt-8 text-5xl sm:text-6xl lg:text-7xl font-semibold leading-[1.05] tracking-tight"
          >
            The Human Operating System
            <span className="block bg-gradient-to-r from-blue-400 via-teal-300 to-violet-400 bg-clip-text text-transparent">
              for the Age of AI
            </span>
          </motion.h1>

          <motion.p
            initial={{ opacity: 0, y: 24 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.7, delay: 0.25 }}
            className="mt-8 max-w-xl text-lg text-white/60 leading-relaxed"
          >
            Artificial intelligence is accelerating. Human capability must
            accelerate faster. Human Operative System helps individuals and
            organizations strengthen memory, develop real capabilities, adapt
            continuously, and create meaningful value throughout life.
          </motion.p>

          <motion.div
            initial={{ opacity: 0, y: 24 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.7, delay: 0.4 }}
            className="mt-10 flex flex-wrap gap-4"
          >
            <button className="group inline-flex items-center gap-2 rounded-full bg-white px-7 py-3.5 font-medium text-black transition hover:bg-white/90">
              Start Your Journey
              <ArrowRight className="h-4 w-4 transition group-hover:translate-x-0.5" />
            </button>

            <button className="rounded-full border border-white/15 px-7 py-3.5 font-medium text-white/80 transition hover:border-white/30 hover:text-white">
              Explore the Framework
            </button>
          </motion.div>
        </div>

        {/* Living flow visualization */}
        <motion.div
          initial={{ opacity: 0, scale: 0.94 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ duration: 0.8, delay: 0.2 }}
          className="relative flex items-center justify-center"
        >
          <div className="relative w-full max-w-md aspect-square">
            <div className="absolute inset-0 rounded-full bg-gradient-to-br from-blue-500/20 via-teal-400/10 to-violet-500/20 blur-2xl" />
            <div className="absolute inset-8 rounded-full border border-white/10" />
            <div className="absolute inset-16 rounded-full border border-white/10" />
            <div className="absolute inset-24 rounded-full border border-white/10" />

            {flow.map((label, i) => {
              const angle = (i / flow.length) * 2 * Math.PI - Math.PI / 2;
              const radius = 42;
              const x = 50 + radius * Math.cos(angle);
              const y = 50 + radius * Math.sin(angle);
              return (
                <motion.div
                  key={label}
                  className="absolute -translate-x-1/2 -translate-y-1/2 whitespace-nowrap rounded-full border border-white/10 bg-white/[0.06] px-3 py-1.5 text-xs font-medium text-white/80 backdrop-blur"
                  style={{ left: `${x}%`, top: `${y}%` }}
                  animate={{ y: [0, -6, 0] }}
                  transition={{ duration: 3.5, repeat: Infinity, delay: i * 0.25, ease: "easeInOut" }}
                >
                  {label}
                </motion.div>
              );
            })}

            <div className="absolute inset-0 flex items-center justify-center">
              <motion.div
                animate={{ scale: [1, 1.08, 1], opacity: [0.7, 1, 0.7] }}
                transition={{ duration: 4, repeat: Infinity, ease: "easeInOut" }}
                className="h-20 w-20 rounded-full bg-gradient-to-br from-blue-400 to-violet-500 blur-[2px]"
              />
            </div>
          </div>
        </motion.div>
      </div>
    </section>
  );
}
