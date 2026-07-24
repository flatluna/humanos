import { PREVIEW_MODE_OPTIONS, type PreviewMode } from '../lib/previewMode';

/** Small segmented control for switching between Real/Demo/Edición while previewing a capability as a student. */
export default function PreviewModeSwitcher({
  mode,
  onChange,
}: {
  mode: PreviewMode;
  onChange: (mode: PreviewMode) => void;
}) {
  return (
    <div className="inline-flex items-center gap-1 rounded-full border border-white/10 bg-white/[0.03] p-1">
      {PREVIEW_MODE_OPTIONS.map((option) => (
        <button
          key={option.value}
          type="button"
          title={option.description}
          onClick={() => onChange(option.value)}
          className={`rounded-full px-3 py-1 text-xs font-medium transition ${
            mode === option.value
              ? 'bg-brand-500 text-[#fff] shadow-sm'
              : 'text-slate-400 hover:text-white'
          }`}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}
