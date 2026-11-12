import { useState } from 'react';
import { Plus } from 'lucide-react';
import type { SkillDto } from '@/api/skillsApi';

/** Dropdown combo box that lets the user manually assign one or more catalog
 * skills (soft or technical) that the LLM did not identify on its own. */
export function CatalogSkillPicker({
  skills,
  existingNames,
  onAdd,
  label,
  searchPlaceholder,
  addBtnLabel,
  cancelBtnLabel,
  noResultsLabel,
  accentClass,
}: {
  skills: SkillDto[];
  existingNames: Set<string>;
  onAdd: (skills: SkillDto[]) => void;
  label: string;
  searchPlaceholder: string;
  addBtnLabel: string;
  cancelBtnLabel: string;
  noResultsLabel: string;
  accentClass: string;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const available = skills.filter((s) => !existingNames.has(s.name.trim().toLowerCase()));
  const filtered = available.filter((s) => s.name.toLowerCase().includes(search.trim().toLowerCase()));

  const toggle = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const close = () => {
    setIsOpen(false);
    setSearch('');
    setSelectedIds(new Set());
  };

  const handleAdd = () => {
    const toAdd = available.filter((s) => selectedIds.has(s.skillId));
    if (toAdd.length > 0) {
      onAdd(toAdd);
    }
    close();
  };

  return (
    <div className="relative">
      <button
        type="button"
        onClick={() => setIsOpen((o) => !o)}
        className={`flex items-center gap-1 rounded-lg border px-2 py-1 text-[10px] font-bold transition-colors ${accentClass}`}
      >
        <Plus className="h-3 w-3" />
        {label}
      </button>
      {isOpen && (
        <>
          <div className="fixed inset-0 z-10" onClick={close} />
          <div className="absolute right-0 z-20 mt-1 w-72 rounded-xl border border-slate-200 bg-white p-2 shadow-lg dark:border-white/10 dark:bg-slate-900">
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={searchPlaceholder}
              autoFocus
              className="mb-2 w-full rounded-lg border border-slate-200 px-2 py-1 text-xs text-slate-900 dark:border-white/10 dark:bg-slate-800 dark:text-white"
            />
            <div className="max-h-48 space-y-0.5 overflow-y-auto">
              {filtered.length === 0 ? (
                <p className="px-2 py-2 text-[11px] text-slate-400 dark:text-white/40">{noResultsLabel}</p>
              ) : (
                filtered.map((s) => (
                  <label
                    key={s.skillId}
                    className="flex cursor-pointer items-center gap-2 rounded px-2 py-1 text-xs text-slate-700 hover:bg-slate-50 dark:text-white/80 dark:hover:bg-white/5"
                  >
                    <input
                      type="checkbox"
                      checked={selectedIds.has(s.skillId)}
                      onChange={() => toggle(s.skillId)}
                    />
                    <span className="truncate">{s.name}</span>
                  </label>
                ))
              )}
            </div>
            <div className="mt-2 flex items-center justify-end gap-3 border-t border-slate-100 pt-2 dark:border-white/10">
              <button type="button" onClick={close} className="text-[11px] font-medium text-slate-500 hover:underline dark:text-white/50">
                {cancelBtnLabel}
              </button>
              <button
                type="button"
                onClick={handleAdd}
                disabled={selectedIds.size === 0}
                className="rounded-lg bg-brand-600 px-2.5 py-1 text-[11px] font-bold text-white disabled:opacity-40"
              >
                {addBtnLabel} {selectedIds.size > 0 ? `(${selectedIds.size})` : ''}
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
