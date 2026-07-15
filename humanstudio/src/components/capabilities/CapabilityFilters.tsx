import { CapabilityStatus, CapabilityLevelTag } from '../../types';

export const ALL_STATUSES: CapabilityStatus[] = ['Published', 'Archived'];
export const ALL_LEVELS: CapabilityLevelTag[] = ['Foundation', 'Exploration', 'Mastery'];

const STATUS_FILTER_LABELS: Record<CapabilityStatus, string> = {
  Published: 'Published',
  Archived: 'Archived',
};

interface CapabilityFiltersProps {
  statusFilter: CapabilityStatus | 'All';
  onStatusFilterChange: (status: CapabilityStatus | 'All') => void;
  levelFilter: CapabilityLevelTag | 'All';
  onLevelFilterChange: (level: CapabilityLevelTag | 'All') => void;
}

export default function CapabilityFilters({
  statusFilter,
  onStatusFilterChange,
  levelFilter,
  onLevelFilterChange,
}: CapabilityFiltersProps) {
  return (
    <div className="flex flex-col sm:flex-row gap-3">
      <select
        value={statusFilter}
        onChange={(e) => onStatusFilterChange(e.target.value as CapabilityStatus | 'All')}
        className="px-3 py-2 border border-gray-300 rounded-lg text-gray-900 bg-white"
        aria-label="Filtrar por estado de diseño"
      >
        <option value="All">All statuses</option>
        {ALL_STATUSES.map((status) => (
          <option key={status} value={status}>
            {STATUS_FILTER_LABELS[status]}
          </option>
        ))}
      </select>

      <select
        value={levelFilter}
        onChange={(e) => onLevelFilterChange(e.target.value as CapabilityLevelTag | 'All')}
        className="px-3 py-2 border border-gray-300 rounded-lg text-gray-900 bg-white"
        aria-label="Filtrar por nivel incluido"
      >
        <option value="All">All levels</option>
        {ALL_LEVELS.map((level) => (
          <option key={level} value={level}>
            {level}
          </option>
        ))}
      </select>
    </div>
  );
}
