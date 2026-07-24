import { motion } from "framer-motion";
import { useTranslation } from "react-i18next";

const capabilities = [
  { key: "criticalThinking", value: 82 },
  { key: "aiAutomation", value: 61 },
  { key: "communication", value: 74 },
  { key: "problemSolving", value: 68 },
  { key: "leadership", value: 43 },
] as const;

export default function CapabilityGrowth() {
  const { t } = useTranslation();

  return (
    <section className="relative bg-white py-28 sm:py-36">
      <div className="max-w-5xl mx-auto px-6">
        <motion.div
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.6 }}
          className="text-center"
        >
          <h2 className="text-4xl sm:text-5xl font-semibold tracking-tight text-slate-900">
            {t("landing.capabilityGrowth.title")}
          </h2>
          <p className="mt-4 text-lg text-slate-500 max-w-xl mx-auto">
            {t("landing.capabilityGrowth.subtitle")}
          </p>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 32 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.3 }}
          transition={{ duration: 0.7, delay: 0.1 }}
          className="mt-16 rounded-3xl border border-slate-200 bg-slate-50 p-8 sm:p-12"
        >
          <div className="space-y-8">
            {capabilities.map((cap, i) => (
              <div key={cap.key}>
                <div className="flex items-center justify-between mb-2">
                  <span className="font-medium text-slate-800">{t(`landing.capabilityGrowth.items.${cap.key}`)}</span>
                  <span className="text-sm font-semibold text-slate-500">{cap.value}%</span>
                </div>
                <div className="h-2.5 w-full rounded-full bg-slate-200 overflow-hidden">
                  <motion.div
                    initial={{ width: 0 }}
                    whileInView={{ width: `${cap.value}%` }}
                    viewport={{ once: true, amount: 0.6 }}
                    transition={{ duration: 1, delay: 0.15 * i, ease: "easeOut" }}
                    className="h-full rounded-full bg-gradient-to-r from-blue-500 via-teal-400 to-violet-500"
                  />
                </div>
              </div>
            ))}
          </div>
        </motion.div>

        <motion.p
          initial={{ opacity: 0, y: 16 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.6 }}
          transition={{ duration: 0.6, delay: 0.2 }}
          className="mt-12 text-center text-lg text-slate-500 max-w-2xl mx-auto"
        >
          {t("landing.capabilityGrowth.footer")}
        </motion.p>
      </div>
    </section>
  );
}
