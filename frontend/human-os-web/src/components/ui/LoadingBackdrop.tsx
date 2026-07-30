import type { ComponentType } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Brain } from 'lucide-react';

interface LoadingBackdropProps {
  title: string;
  description: string;
  icon?: ComponentType<{ className?: string }>;
}

/** Full-screen animated loading overlay shown while a page's initial data
 *  is still being fetched — gives clear feedback that something is in
 *  progress instead of leaving the user staring at an empty/stale page.
 *  Shared between /brain and /today (and any future page with a similarly
 *  slow multi-request initial load). */
export function LoadingBackdrop({ title, description, icon: Icon = Brain }: LoadingBackdropProps) {
  return (
    <AnimatePresence>
      <motion.div
        key="loading-backdrop"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm"
      >
        <motion.div
          initial={{ scale: 0.9, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          exit={{ scale: 0.9, opacity: 0 }}
          transition={{ type: 'spring', stiffness: 300, damping: 30 }}
          className="mx-4 max-w-sm rounded-3xl bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 p-8 shadow-2xl border border-white/10"
        >
          <div className="flex flex-col items-center gap-6">
            {/* Animated icon with glowing effect */}
            <div className="relative">
              <motion.div
                animate={{ scale: [1, 1.1, 1] }}
                transition={{ repeat: Infinity, duration: 2 }}
                className="absolute inset-0 rounded-full bg-gradient-to-r from-fuchsia-500 to-indigo-600 opacity-30 blur-xl"
              />
              <div className="relative flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br from-fuchsia-500/20 to-indigo-600/20 border border-fuchsia-500/50">
                <motion.div animate={{ rotate: 360 }} transition={{ repeat: Infinity, duration: 3, ease: 'linear' }}>
                  <Icon className="h-8 w-8 text-fuchsia-400" />
                </motion.div>
              </div>
            </div>

            {/* Title */}
            <div className="text-center">
              <h3 className="text-lg font-semibold text-white">{title}</h3>
              <p className="mt-2 text-sm text-white/60">{description}</p>
            </div>

            {/* Loading dots animation */}
            <div className="flex items-center gap-1">
              {[0, 1, 2].map((i) => (
                <motion.div
                  key={i}
                  animate={{ opacity: [0.3, 1, 0.3] }}
                  transition={{ repeat: Infinity, duration: 1.5, delay: i * 0.2 }}
                  className="h-2 w-2 rounded-full bg-gradient-to-r from-fuchsia-400 to-indigo-400"
                />
              ))}
            </div>
          </div>
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
}
