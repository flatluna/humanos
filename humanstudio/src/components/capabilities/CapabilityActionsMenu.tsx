import { useEffect, useRef, useState } from 'react';
import { MoreVertical } from 'lucide-react';

interface CapabilityActionsMenuProps {
  onEdit: () => void;
  /** Only present for real published capabilities (GUID capabilityId) —
   * opens the read-only "view real generated content" screen. */
  onViewContent?: () => void;
}

/**
 * The "⋮" per-card actions menu. The real backend has no design-status
 * lifecycle (Draft/InReview), duplicate, or soft-delete endpoints yet —
 * only Edit (open Studio) and View content (real capabilities only) are
 * real, working actions. Clicking the trigger or any menu item stops
 * propagation so it never also triggers the card's own "open in Studio"
 * click handler.
 */
export default function CapabilityActionsMenu({ onEdit, onViewContent }: CapabilityActionsMenuProps) {
  const [isOpen, setIsOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!isOpen) return;

    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    }

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen]);

  const runAndClose = (action: () => void) => {
    action();
    setIsOpen(false);
  };

  return (
    <div
      ref={containerRef}
      className="relative"
      onClick={(e) => e.stopPropagation()}
    >
      <button
        onClick={() => setIsOpen((prev) => !prev)}
        className="p-1.5 rounded-md text-gray-500 hover:bg-gray-100 hover:text-gray-700 cursor-pointer"
        aria-label="Capability actions"
      >
        <MoreVertical size={18} />
      </button>

      {isOpen && (
        <div className="absolute right-0 top-8 z-10 w-48 bg-white border border-gray-200 rounded-lg shadow-lg py-1">
          {onViewContent && (
            <button
              onClick={() => runAndClose(onViewContent)}
              className="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 cursor-pointer"
            >
              View content
            </button>
          )}
          <button
            onClick={() => runAndClose(onEdit)}
            className="w-full text-left px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 cursor-pointer"
          >
            Edit
          </button>
        </div>
      )}
    </div>
  );
}

