export default function LoadingSpinner({ label }: { label?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-slate-400">
      <div className="relative h-10 w-10">
        <div className="absolute inset-0 rounded-full border-2 border-white/10" />
        <div className="absolute inset-0 rounded-full border-2 border-transparent border-t-brand-400 animate-spin" />
      </div>
      {label && <p className="text-sm">{label}</p>}
    </div>
  );
}
