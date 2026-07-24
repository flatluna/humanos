import { motion } from "framer-motion";
import { Compass, GraduationCap, Brain, ShieldCheck, RefreshCw, Rocket, Heart, type LucideIcon } from "lucide-react";
import { useTranslation } from "react-i18next";

const benefits: { icon: LucideIcon; key: string }[] = [
  { icon: Compass, key: "discoverStrengths" },
  { icon: GraduationCap, key: "developExpertise" },
  { icon: Brain, key: "improveMemory" },
  { icon: ShieldCheck, key: "buildConfidence" },
  { icon: RefreshCw, key: "adaptContinuously" },
  { icon: Rocket, key: "createOpportunities" },
  { icon: Heart, key: "developPurpose" },
];

export default function ForIndividuals() {
  const { t } = useTranslation();

  return (
    <section className="relative py-28 sm:py-36 bg-white overflow-hidden">
      <div className="max-w-6xl mx-auto px-6 grid lg:grid-cols-2 gap-16 items-center">
        <motion.div
          initial={{ opacity: 0, x: -24 }}
          whileInView={{ opacity: 1, x: 0 }}
          viewport={{ once: true, amount: 0.4 }}
          transition={{ duration: 0.6 }}
        >
          <p className="text-sm font-medium uppercase tracking-widest text-blue-600">
            {t("landing.forIndividuals.eyebrow")}
          </p>
          <h2 className="mt-3 text-4xl sm:text-5xl font-semibold tracking-tight text-slate-900">
            {t("landing.forIndividuals.title")}
          </h2>
          <p className="mt-6 text-lg text-slate-500 leading-relaxed max-w-lg">
            {t("landing.forIndividuals.description")}
          </p>
        </motion.div>

        <div className="grid sm:grid-cols-2 gap-4">
          {benefits.map(({ icon: Icon, key }, i) => (
            <motion.div
              key={key}
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, amount: 0.4 }}
              transition={{ duration: 0.5, delay: i * 0.06 }}
              className="flex items-center gap-3 rounded-2xl border border-slate-200 bg-slate-50 px-5 py-4"
            >
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white border border-slate-200">
                <Icon className="h-4.5 w-4.5 text-blue-600" />
              </div>
              <span className="font-medium text-slate-800">{t(`landing.forIndividuals.benefits.${key}`)}</span>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
