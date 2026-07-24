import { motion } from "framer-motion";
import { Search, Video, BookOpen, Bot, Files, Brain, Wrench, Target, Puzzle, Gem } from "lucide-react";
import { useTranslation } from "react-i18next";

const information = [
  { icon: Search, key: "searchEngines" },
  { icon: Bot, key: "aiTools" },
  { icon: Video, key: "videos" },
  { icon: BookOpen, key: "courses" },
  { icon: Files, key: "content" },
] as const;

const capability = [
  { icon: Brain, key: "memory" },
  { icon: Wrench, key: "thinking" },
  { icon: Target, key: "application" },
  { icon: Puzzle, key: "problemSolving" },
  { icon: Gem, key: "valueCreation" },
] as const;

export default function ProblemSection() {
  const { t } = useTranslation();

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
          {t("landing.problem.title")}
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
              {t("landing.problem.informationEyebrow")}
            </p>
            <h3 className="mt-2 text-2xl font-semibold text-slate-900">{t("landing.problem.informationTitle")}</h3>

            <div className="mt-8 space-y-4">
              {information.map(({ icon: Icon, key }) => (
                <div key={key} className="flex items-center gap-3 rounded-xl bg-white px-4 py-3 border border-slate-200">
                  <Icon className="h-4.5 w-4.5 text-slate-400" />
                  <span className="text-slate-600">{t(`landing.problem.informationItems.${key}`)}</span>
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
              {t("landing.problem.capabilityEyebrow")}
            </p>
            <h3 className="mt-2 text-2xl font-semibold">{t("landing.problem.capabilityTitle")}</h3>

            <div className="mt-8 space-y-4">
              {capability.map(({ icon: Icon, key }) => (
                <div key={key} className="flex items-center gap-3 rounded-xl bg-white/5 px-4 py-3 border border-white/10">
                  <Icon className="h-4.5 w-4.5 text-blue-400" />
                  <span className="text-white/80">{t(`landing.problem.capabilityItems.${key}`)}</span>
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
          {t("landing.problem.footer")}
        </motion.p>
      </div>
    </section>
  );
}
