import type { ReactNode } from 'react';

interface SubjectPillProps {
  label: string;
  active: boolean;
  onClick: () => void;
  icon?: ReactNode;
}

export default function SubjectPill({ label, active, onClick, icon }: SubjectPillProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex shrink-0 items-center gap-1.5 rounded-full border px-3.5 py-2 text-sm font-medium transition-colors ${
        active
          ? 'border-transparent bg-gradient-to-r from-brand-500 to-accent-500 text-[#fff] shadow-md shadow-brand-500/20'
          : 'border-white/10 bg-white/[0.03] text-slate-300 hover:border-white/20 hover:bg-white/[0.07]'
      }`}
    >
      {icon}
      {label}
    </button>
  );
}
