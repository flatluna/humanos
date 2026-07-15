import { CapabilityStatus } from '../../types';

/** Visual style for each design status badge. */
const STATUS_STYLES: Record<CapabilityStatus, { emoji: string; label: string; className: string }> = {
  Published: { emoji: '🟢', label: 'Published', className: 'bg-green-50 text-green-800 border-green-200' },
  Archived: { emoji: '⚪', label: 'Archived', className: 'bg-gray-100 text-gray-600 border-gray-300' },
};

interface CapabilityStatusBadgeProps {
  status: CapabilityStatus;
}

export default function CapabilityStatusBadge({ status }: CapabilityStatusBadgeProps) {
  const style = STATUS_STYLES[status];

  return (
    <span
      className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold border ${style.className}`}
    >
      <span>{style.emoji}</span>
      {style.label}
    </span>
  );
}
