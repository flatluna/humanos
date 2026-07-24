import { motion } from "framer-motion";
import { Compass, Repeat, Brain, Rocket, FileCheck2, Award, Gem, type LucideIcon } from "lucide-react";
import { useTranslation } from "react-i18next";

interface Step {
  icon: LucideIcon;
  key: "discover" | "practice" | "recall" | "apply" | "evidence" | "mastery" | "value";
}

const steps: Step[] = [
  { icon: Compass, key: "discover" },
  { icon: Repeat, key: "practice" },
  { icon: Brain, key: "recall" },
  { icon: Rocket, key: "apply" },
  { icon: FileCheck2, key: "evidence" },
  { icon: Award, key: "mastery" },
  { icon: Gem, key: "value" },
];

export default function GrowthLoop() {
  const { t } = useTranslation();

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
            {t("landing.growthLoop.title")}
          </h2>
          <p className="mt-4 text-lg text-white/50 max-w-xl mx-auto">
            {t("landing.growthLoop.subtitle")}
          </p>
        </motion.div>

        <div className="mt-20 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {steps.map(({ icon: Icon, key }, i) => (
            <motion.div
              key={key}
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
              <h3 className="mt-5 text-lg font-semibold">{t(`landing.growthLoop.steps.${key}.label`)}</h3>
              <p className="mt-2 text-sm leading-relaxed text-white/50">{t(`landing.growthLoop.steps.${key}.description`)}</p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
