import { motion } from "framer-motion";
import { Layers as LayersIcon, Compass, Trophy, Briefcase, Radar, Sparkles, type LucideIcon } from "lucide-react";

interface Layer {
  icon: LucideIcon;
  title: string;
  description: string;
}

const layers: Layer[] = [
  {
    icon: LayersIcon,
    title: "Foundation",
    description: "Building the cognitive operating system: memory, reading, communication, critical thinking.",
  },
  {
    icon: Compass,
    title: "Exploration",
    description: "Discovering interests, strengths, and passions.",
  },
  {
    icon: Trophy,
    title: "Mastery",
    description: "Transforming interests into capabilities.",
  },
  {
    icon: Briefcase,
    title: "Professional",
    description: "Turning capabilities into value.",
  },
  {
    icon: Radar,
    title: "Frontier",
    description: "Staying relevant as the world changes.",
  },
  {
    icon: Sparkles,
    title: "Creator",
    description: "Contributing knowledge back to others.",
  },
];

export default function Layers() {
  return (
    <section className="py-28 sm:py-36 bg-white">
      <div className="max-w-7xl mx-auto px-6">
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.6 }}
          className="text-center"
        >
          <h2 className="text-4xl sm:text-5xl font-semibold tracking-tight text-slate-900">
            A Lifelong Framework for Human Growth
          </h2>
        </motion.div>

        <div className="mt-16 grid md:grid-cols-2 lg:grid-cols-3 gap-6">
          {layers.map(({ icon: Icon, title, description }, i) => (
            <motion.div
              key={title}
              initial={{ opacity: 0, y: 24 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, amount: 0.4 }}
              transition={{ duration: 0.5, delay: i * 0.07 }}
              className="group rounded-3xl border border-slate-200 bg-slate-50 p-8 transition hover:border-slate-300 hover:shadow-lg hover:shadow-slate-200/60"
            >
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-slate-900 text-white transition group-hover:scale-105">
                <Icon className="h-5.5 w-5.5" />
              </div>
              <h3 className="mt-6 text-xl font-semibold text-slate-900">{title}</h3>
              <p className="mt-3 text-slate-500 leading-relaxed">{description}</p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
