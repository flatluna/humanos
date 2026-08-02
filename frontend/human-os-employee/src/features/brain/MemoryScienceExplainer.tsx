import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronDown, ChevronUp, CircleDashed, Percent, Zap, FlaskConical } from 'lucide-react';
import { Card } from '@/components/ui/Card';
import { COLOR_STOPS } from './memoryScience';

const STORAGE_KEY = 'humanos-employee-brain-explainer-collapsed';

/** Educational "how does this work" panel shown at the top of /brain.
 *  Explains the ring/percentage/⚡ symbols, the forgetting-curve algorithm
 *  in plain language. Collapsible, remembers the preference in
 *  localStorage so it doesn't nag on every visit. Ported from
 *  human-os-web (2026-08-01), hardcoded to Spanish since this portal has
 *  no language switcher (see useI18n's doc comment in features/capabilities). */
export function MemoryScienceExplainer() {
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  });

  function toggle() {
    setCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem(STORAGE_KEY, next ? '1' : '0');
      } catch {
        /* ignore */
      }
      return next;
    });
  }

  return (
    <Card className="mt-6 overflow-hidden border-accent-500/20 bg-gradient-to-br from-accent-500/5 via-transparent to-brand-500/5 p-0">
      <button type="button" onClick={toggle} className="flex w-full items-center justify-between gap-3 p-4 text-left">
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-accent-500 to-brand-600 text-white shadow shadow-accent-500/30">
            <FlaskConical className="h-4.5 w-4.5" />
          </div>
          <div>
            <p className="text-sm font-semibold text-slate-900 dark:text-white">¿Cómo funciona la memoria aquí?</p>
            {collapsed && <p className="text-xs text-slate-500 dark:text-slate-400">Mostrar explicación</p>}
          </div>
        </div>
        {collapsed ? (
          <ChevronDown className="h-5 w-5 shrink-0 text-slate-400" />
        ) : (
          <ChevronUp className="h-5 w-5 shrink-0 text-slate-400" />
        )}
      </button>

      <AnimatePresence initial={false}>
        {!collapsed && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.25, ease: 'easeInOut' }}
            className="overflow-hidden"
          >
            <div className="space-y-5 px-4 pb-5 pt-1 sm:px-6">
              <div>
                <p className="mb-2 text-xs font-bold uppercase tracking-wide text-accent-500 dark:text-accent-400">
                  Qué significa cada símbolo
                </p>
                <ul className="space-y-2.5">
                  <li className="flex flex-col gap-2 text-sm text-slate-600 dark:text-slate-300">
                    <div className="flex items-start gap-2.5">
                      <CircleDashed className="mt-0.5 h-4 w-4 shrink-0 text-emerald-400" />
                      <span>El anillo de color muestra qué tan fresco tienes ese conocimiento ahora mismo.</span>
                    </div>
                    <div className="ml-6 flex flex-wrap items-center gap-3">
                      {[
                        { stop: COLOR_STOPS[0], label: 'Fuerte' },
                        { stop: COLOR_STOPS[2], label: 'Debilitándose' },
                        { stop: COLOR_STOPS[4], label: 'Olvidado' },
                      ].map(({ stop, label }) => (
                        <span key={stop[1]} className="flex items-center gap-1.5">
                          <span
                            className="h-3.5 w-3.5 shrink-0 rounded-full ring-1 ring-white/20"
                            style={{ background: stop[1] }}
                          />
                          <span className="text-xs text-slate-500 dark:text-slate-400">{label}</span>
                        </span>
                      ))}
                    </div>
                  </li>
                  <li className="flex items-start gap-2.5 text-sm text-slate-600 dark:text-slate-300">
                    <Percent className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" />
                    <span>El porcentaje es tu retención estimada: qué tan probable es que aún lo recuerdes bien.</span>
                  </li>
                  <li className="flex items-start gap-2.5 text-sm text-slate-600 dark:text-slate-300">
                    <Zap className="mt-0.5 h-4 w-4 shrink-0 text-violet-400" />
                    <span>El rayo ⚡ significa que ya lo dominas de forma automática — casi no se olvida.</span>
                  </li>
                </ul>
              </div>

              <div>
                <p className="mb-2 text-xs font-bold uppercase tracking-wide text-brand-500 dark:text-brand-400">
                  El algoritmo
                </p>
                <p className="text-sm leading-relaxed text-slate-600 dark:text-slate-300">
                  Usamos una curva del olvido real (estilo FSRS, el mismo enfoque detrás de Anki): entre más veces
                  has recordado algo correctamente, más tiempo tarda en desvanecerse. Cada tarjeta aquí es un
                  "engrama" — un recuerdo real que ya dominaste — y su anillo se actualiza solo, sin que tengas
                  que volver a estudiar para verlo cambiar.
                </p>
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </Card>
  );
}
