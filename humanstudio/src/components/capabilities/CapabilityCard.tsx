import { CapabilitySummary } from '../../types';
import CapabilityStatusBadge from './CapabilityStatusBadge';
import CapabilityActionsMenu from './CapabilityActionsMenu';

function formatRelativeTime(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(diffMs / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;

  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;

  const months = Math.floor(days / 30);
  if (months < 12) return `${months}mo ago`;

  const years = Math.floor(months / 12);
  return `${years}y ago`;
}

interface CapabilityCardProps {
  capability: CapabilitySummary;
  onOpenInStudio: () => void;
  onViewContent?: () => void;
  onDelete?: () => void;
}

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Designer's capability card — NO student/progress data. The whole card (and
 * the "Edit" menu item) opens Studio directly; there is no intermediate
 * detail screen for editing. "View content" (only for capabilities
 * actually published through the real backend — GUID capabilityId) opens
 * a separate read-only screen showing the real generated levels/modules/
 * scripts.
 */
export default function CapabilityCard({
  capability,
  onOpenInStudio,
  onViewContent,
  onDelete,
}: CapabilityCardProps) {
  const isRealCapability = GUID_PATTERN.test(capability.capabilityId);

  return (
    <div
      onClick={onOpenInStudio}
      className="bg-white rounded-lg shadow p-5 hover:shadow-md transition-all cursor-pointer flex flex-col gap-3"
    >
      <div className="flex items-start justify-between gap-2">
        <h3 className="text-lg font-bold text-gray-900">{capability.title}</h3>
        <CapabilityActionsMenu
          onEdit={onOpenInStudio}
          onViewContent={isRealCapability ? onViewContent : undefined}
          onDelete={isRealCapability ? onDelete : undefined}
        />
      </div>

      <p
        className="text-sm text-gray-600"
        style={{
          display: '-webkit-box',
          WebkitLineClamp: 2,
          WebkitBoxOrient: 'vertical',
          overflow: 'hidden',
        }}
      >
        {capability.description}
      </p>

      <p className="text-sm text-gray-700">
        {capability.levels.length} niveles · {capability.moduleCount} módulos
      </p>

      <div className="flex flex-wrap gap-1.5">
        {capability.levels.map((level) => (
          <span
            key={level}
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-blue-50 text-blue-800 text-xs font-medium"
          >
            🟦 {level}
          </span>
        ))}
      </div>

      <div className="flex items-center justify-between mt-1">
        <CapabilityStatusBadge status={capability.status} />
        <span className="text-xs text-gray-400">Edited {formatRelativeTime(capability.updatedAt)}</span>
      </div>
    </div>
  );
}
