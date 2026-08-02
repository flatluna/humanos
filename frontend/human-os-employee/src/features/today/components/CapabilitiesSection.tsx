import { motion } from 'framer-motion';
import { Card } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { ProgressBar } from '@/components/ui/ProgressBar';
import type { CapabilityProgress } from '../types';

const LEVEL_LABELS: Record<CapabilityProgress['level'], string> = {
  none: 'Sin empezar',
  basico: 'Básico',
  intermedio: 'Intermedio',
  avanzado: 'Avanzado',
};

interface CapabilitiesSectionProps {
  capabilities: CapabilityProgress[];
}

export function CapabilitiesSection({ capabilities }: CapabilitiesSectionProps) {
  if (capabilities.length === 0) return null;

  return (
    <section aria-labelledby="capabilities-heading" className="mx-auto max-w-6xl px-6 pt-14">
      <h2 id="capabilities-heading" className="text-2xl font-semibold text-slate-900 dark:text-white">
        Tus cursos
      </h2>

      <div className="mt-6 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        {capabilities.map((cap, i) => (
          <motion.div
            key={cap.id}
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true, amount: 0.4 }}
            transition={{ duration: 0.5, delay: i * 0.08 }}
          >
            <Card className="p-6">
              <div className="flex items-center justify-between">
                <h3 className="font-semibold text-slate-900 dark:text-white">{cap.name}</h3>
                <span className="text-sm font-semibold text-slate-500 dark:text-white/50">{cap.progress}%</span>
              </div>

              <div className="mt-3">
                <ProgressBar value={cap.progress} />
              </div>

              <p className="mt-3 text-xs font-medium text-slate-400 dark:text-white/40">{LEVEL_LABELS[cap.level]}</p>

              <div className="mt-4 border-t border-slate-100 pt-4 dark:border-white/10">
                <p className="text-[11px] font-semibold uppercase tracking-widest text-slate-400 dark:text-white/40">
                  Área
                </p>
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {cap.supports.map((s) => (
                    <Badge key={s}>{s}</Badge>
                  ))}
                </div>
              </div>
            </Card>
          </motion.div>
        ))}
      </div>
    </section>
  );
}
