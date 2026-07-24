import { BookOpen, Sparkles, Utensils, Landmark, ScrollText, Beaker, Cpu, Languages, HeartPulse, Briefcase, Palette, Compass, Hammer, PawPrint, Map, Users, Layers } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

/** Maps a Subject code to an icon + accent gradient for the catalog UI.
 * Falls back to a generic icon for subjects not in this list (the seed
 * migration's 12 codes + the 3 pre-existing ones observed live). */
const SUBJECT_ICONS: Record<string, LucideIcon> = {
  matematicas: Sparkles,
  finanzas: Landmark,
  cocina: Utensils,
  historia: ScrollText,
  ciencia: Beaker,
  tecnologia: Cpu,
  idiomas: Languages,
  'salud-bienestar': HeartPulse,
  negocios: Briefcase,
  'arte-creatividad': Palette,
  'desarrollo-personal': Compass,
  oficios: Hammer,
  animales: PawPrint,
  geografia: Map,
  'recursos-humanos': Users,
};

export function getSubjectIcon(code: string | null | undefined): LucideIcon {
  if (!code) return Layers;
  return SUBJECT_ICONS[code] ?? BookOpen;
}

const SUBJECT_GRADIENTS: Record<string, string> = {
  matematicas: 'from-indigo-500 to-blue-500',
  finanzas: 'from-emerald-500 to-teal-500',
  cocina: 'from-orange-500 to-amber-500',
  historia: 'from-amber-600 to-yellow-600',
  ciencia: 'from-cyan-500 to-sky-500',
  tecnologia: 'from-violet-500 to-purple-500',
  idiomas: 'from-pink-500 to-rose-500',
  'salud-bienestar': 'from-rose-500 to-red-500',
  negocios: 'from-slate-500 to-slate-600',
  'arte-creatividad': 'from-fuchsia-500 to-pink-500',
  'desarrollo-personal': 'from-teal-500 to-emerald-500',
  oficios: 'from-stone-500 to-neutral-600',
  animales: 'from-lime-500 to-green-500',
  geografia: 'from-blue-500 to-cyan-500',
  'recursos-humanos': 'from-purple-500 to-indigo-500',
};

export function getSubjectGradient(code: string | null | undefined): string {
  if (!code) return 'from-brand-500 to-accent-500';
  return SUBJECT_GRADIENTS[code] ?? 'from-brand-500 to-accent-500';
}
