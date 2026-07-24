import type { LucideIcon } from 'lucide-react';

interface TypeOptionCardProps {
  icon: LucideIcon;
  title: string;
  description: string;
  selected: boolean;
  disabled?: boolean;
  onClick: () => void;
}

export default function TypeOptionCard({ icon: Icon, title, description, selected, disabled, onClick }: TypeOptionCardProps) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={`relative flex flex-col items-start gap-3 rounded-2xl border p-5 text-left transition-all ${
        disabled
          ? 'cursor-not-allowed border-white/5 bg-white/[0.01] opacity-50'
          : selected
          ? 'border-brand-400/60 bg-brand-500/10 shadow-lg shadow-brand-500/10'
          : 'border-white/10 bg-white/[0.03] hover:border-white/20 hover:bg-white/[0.06]'
      }`}
    >
      {disabled && (
        <span className="absolute right-3 top-3 rounded-full bg-white/10 px-2 py-0.5 text-[10px] font-medium text-slate-300">
          Próximamente
        </span>
      )}
      <div
        className={`flex h-11 w-11 items-center justify-center rounded-xl ${
          selected ? 'bg-gradient-to-br from-brand-500 to-accent-500' : 'bg-white/10'
        }`}
      >
        <Icon className={`h-5.5 w-5.5 ${selected ? 'text-[#fff]' : 'text-white'}`} strokeWidth={2} />
      </div>
      <div>
        <div className="font-semibold text-white">{title}</div>
        <p className="mt-1 text-sm text-slate-400">{description}</p>
      </div>
    </button>
  );
}
