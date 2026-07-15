import React, { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getCapabilities } from '../lib/api/capabilitiesApi';
import { CapabilityStatus, CapabilityLevelTag, CapabilitySummary } from '../types';
import CapabilityCard from '../components/capabilities/CapabilityCard';
import CapabilityFilters from '../components/capabilities/CapabilityFilters';
import EmptyState from '../components/capabilities/EmptyState';

/**
 * "Capability Library" — the DESIGNER's list of authored capabilities.
 * This is HumanStudio, the capability-AUTHORING app: no student/learner data
 * (progress, completion %, "Continue") belongs here. Opening a capability
 * (card click or "Edit") always goes straight to Studio — there is no
 * intermediate read-only detail screen for editing ("View content" opens
 * a separate read-only screen instead, see CapabilityDetailPage.tsx).
 *
 * 100% backed by the real backend (GET /capabilities) — no mock/demo data.
 */
const CapabilityLibraryPage: React.FC = () => {
  const navigate = useNavigate();

  const [capabilities, setCapabilities] = useState<CapabilitySummary[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState<CapabilityStatus | 'All'>('All');
  const [levelFilter, setLevelFilter] = useState<CapabilityLevelTag | 'All'>('All');

  useEffect(() => {
    setIsLoading(true);
    getCapabilities()
      .then(setCapabilities)
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'No se pudieron cargar las capabilities.'))
      .finally(() => setIsLoading(false));
  }, []);

  const handleOpenInStudio = () => navigate('/studio');

  const hasAnyFilters = searchQuery.trim() !== '' || statusFilter !== 'All' || levelFilter !== 'All';

  const filteredCapabilities = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();

    return capabilities.filter((capability) => {
      const matchesQuery =
        query === '' ||
        capability.title.toLowerCase().includes(query) ||
        capability.description.toLowerCase().includes(query) ||
        capability.domain.toLowerCase().includes(query);

      const matchesStatus = statusFilter === 'All' || capability.status === statusFilter;

      // "Includes the level" semantics: a capability matches if its declared
      // levels contain the selected one (a capability can span several levels).
      const matchesLevel = levelFilter === 'All' || capability.levels.includes(levelFilter);

      return matchesQuery && matchesStatus && matchesLevel;
    });
  }, [capabilities, searchQuery, statusFilter, levelFilter]);

  const handleClearFilters = () => {
    setSearchQuery('');
    setStatusFilter('All');
    setLevelFilter('All');
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <h2 className="text-3xl font-bold text-gray-900">Capability Library</h2>
        <button
          onClick={() => navigate('/studio')}
          className="inline-flex items-center justify-center px-5 py-2.5 rounded-lg font-medium bg-blue-600 text-white hover:bg-blue-700 transition-all cursor-pointer"
        >
          + New Capability
        </button>
      </div>

      {loadError && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-red-800 text-sm">
          {loadError}
        </div>
      )}

      <div className="flex flex-col sm:flex-row gap-3 sm:items-center sm:justify-between">
        <input
          type="text"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          placeholder="Search by title, description or domain..."
          className="w-full sm:max-w-sm px-3 py-2 border border-gray-300 rounded-lg text-gray-900 placeholder-gray-400"
        />
        <CapabilityFilters
          statusFilter={statusFilter}
          onStatusFilterChange={setStatusFilter}
          levelFilter={levelFilter}
          onLevelFilterChange={setLevelFilter}
        />
      </div>

      {isLoading && <p className="text-gray-600">Loading capabilities...</p>}

      {!isLoading && capabilities.length === 0 && !loadError && (
        <EmptyState
          title="You haven't created any capabilities yet."
          description="Start designing your first capability in Studio."
          actionLabel="Create your first capability"
          onAction={() => navigate('/studio')}
        />
      )}

      {!isLoading && capabilities.length > 0 && filteredCapabilities.length === 0 && (
        <EmptyState
          title="No capabilities match your filters."
          description="Try a different search term or reset the filters."
          actionLabel={hasAnyFilters ? 'Clear filters' : undefined}
          onAction={hasAnyFilters ? handleClearFilters : undefined}
        />
      )}

      {!isLoading && filteredCapabilities.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {filteredCapabilities.map((capability) => (
            <CapabilityCard
              key={capability.capabilityId}
              capability={capability}
              onOpenInStudio={handleOpenInStudio}
              onViewContent={() => navigate(`/capabilities/${capability.capabilityId}`)}
            />
          ))}
        </div>
      )}
    </div>
  );
};

export default CapabilityLibraryPage;

