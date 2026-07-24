import { motion } from "framer-motion";
import { ArrowRight } from "lucide-react";
import { useTranslation } from "react-i18next";

export default function CTA() {
  const { t } = useTranslation();

  return (
    <section className="relative py-28 sm:py-36 bg-[#05060a] text-white overflow-hidden">
      <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
        <div className="h-[500px] w-[500px] rounded-full bg-gradient-to-br from-blue-500/25 via-teal-400/15 to-violet-500/25 blur-[120px]" />
      </div>

      <div className="relative max-w-4xl mx-auto text-center px-6">
        <motion.h2
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.5 }}
          transition={{ duration: 0.7 }}
          className="text-4xl sm:text-5xl lg:text-6xl font-semibold tracking-tight"
        >
          {t("landing.cta.title")}
        </motion.h2>

        <motion.p
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.5 }}
          transition={{ duration: 0.7, delay: 0.1 }}
          className="mt-6 text-lg text-white/50 leading-relaxed"
        >
          {t("landing.cta.description1")}
          <br />
          {t("landing.cta.description2")}
        </motion.p>

        <motion.div
          initial={{ opacity: 0, y: 24 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, amount: 0.5 }}
          transition={{ duration: 0.7, delay: 0.2 }}
        >
          <button className="group mt-10 inline-flex items-center gap-2 rounded-full bg-white px-8 py-4 font-medium text-black transition hover:bg-white/90">
            {t("landing.cta.button")}
            <ArrowRight className="h-4 w-4 transition group-hover:translate-x-0.5" />
          </button>
        </motion.div>
      </div>
    </section>
  );
}
