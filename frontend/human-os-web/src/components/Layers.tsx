import { motion } from "framer-motion";
import { Layers as LayersIcon, Compass, Trophy, Briefcase, Radar, Sparkles, type LucideIcon } from "lucide-react";
import { useTranslation } from "react-i18next";

interface Layer {
  icon: LucideIcon;
  key: "foundation" | "exploration" | "mastery" | "professional" | "frontier" | "creator";
}

const layers: Layer[] = [
  { icon: LayersIcon, key: "foundation" },
  { icon: Compass, key: "exploration" },
  { icon: Trophy, key: "mastery" },
  { icon: Briefcase, key: "professional" },
  { icon: Radar, key: "frontier" },
  { icon: Sparkles, key: "creator" },
];

export default function Layers() {
  const { t } = useTranslation();

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
            {t("landing.layers.title")}
          </h2>
        </motion.div>

        <div className="mt-16 grid md:grid-cols-2 lg:grid-cols-3 gap-6">
          {layers.map(({ icon: Icon, key }, i) => (
            <motion.div
              key={key}
              initial={{ opacity: 0, y: 24 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true, amount: 0.4 }}
              transition={{ duration: 0.5, delay: i * 0.07 }}
              className="group rounded-3xl border border-slate-200 bg-slate-50 p-8 transition hover:border-slate-300 hover:shadow-lg hover:shadow-slate-200/60"
            >
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-slate-900 text-white transition group-hover:scale-105">
                <Icon className="h-5.5 w-5.5" />
              </div>
              <h3 className="mt-6 text-xl font-semibold text-slate-900">{t(`landing.layers.items.${key}.title`)}</h3>
              <p className="mt-3 text-slate-500 leading-relaxed">{t(`landing.layers.items.${key}.description`)}</p>
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
