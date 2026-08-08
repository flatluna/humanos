import { useEffect, useMemo, useRef, useState } from 'react';
import { Check, ChevronDown, Search, X } from 'lucide-react';

export interface ComboboxOption {
  id: string;
  label: string;
  /** Optional secondary text shown right-aligned in the option row (e.g. a count). */
  meta?: string;
}

interface SearchableComboboxProps {
  value: string;
  onChange: (value: string) => void;
  options: ComboboxOption[];
  placeholder?: string;
  /** Shown inside the dropdown when `options` is empty. */
  emptyOptionsText?: string;
  /** Fired (in addition to onChange) when an option is picked from the
   * dropdown — lets a caller capture the option's `id` (e.g. a real
   * record id), not just its display label. */
  onSelect?: (option: ComboboxOption) => void;
  disabled?: boolean;
}

/**
 * Styled, searchable autocomplete input (2026-08-08) — replaces the native
 * `<input list=... /><datalist>` combo, which renders an unstyled browser
 * dropdown that only appears once the user starts typing and can't be
 * themed to match the app (reported as "horrible" / the toggle arrow did
 * nothing until you typed). Still allows free text — picking an option
 * just fills the input, it never blocks a brand-new value.
 */
export default function SearchableCombobox({
  value,
  onChange,
  options,
  placeholder,
  emptyOptionsText,
  onSelect,
  disabled,
}: SearchableComboboxProps) {
  const [open, setOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const filteredOptions = useMemo(() => {
    const query = value.trim().toLowerCase();
    if (!query) return options;
    return options.filter((option) => option.label.toLowerCase().includes(query));
  }, [options, value]);

  useEffect(() => {
    setHighlightedIndex(0);
  }, [filteredOptions.length, open]);

  useEffect(() => {
    if (!open) return;
    function handleClickOutside(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [open]);

  function selectOption(option: ComboboxOption) {
    onChange(option.label);
    onSelect?.(option);
    setOpen(false);
  }

  function clear() {
    onChange('');
    setOpen(true);
    inputRef.current?.focus();
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setOpen(true);
      setHighlightedIndex((i) => Math.min(i + 1, filteredOptions.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setHighlightedIndex((i) => Math.max(i - 1, 0));
    } else if (event.key === 'Enter') {
      if (open && filteredOptions[highlightedIndex]) {
        event.preventDefault();
        selectOption(filteredOptions[highlightedIndex]);
      }
    } else if (event.key === 'Escape') {
      setOpen(false);
    }
  }

  return (
    <div ref={containerRef} className="relative">
      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-500" />
        <input
          ref={inputRef}
          type="text"
          value={value}
          disabled={disabled}
          onChange={(e) => {
            onChange(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          className={`input ${value ? 'pl-9 pr-16' : 'pl-9 pr-9'} disabled:cursor-not-allowed disabled:opacity-50`}
        />
        <div className="absolute right-2 top-1/2 flex -translate-y-1/2 items-center gap-0.5">
          {value && !disabled && (
            <button
              type="button"
              onClick={clear}
              className="rounded-lg p-1 text-slate-500 transition-colors hover:text-slate-300"
              aria-label="Clear"
            >
              <X className="h-4 w-4" />
            </button>
          )}
          <button
            type="button"
            onClick={() => setOpen((prev) => !prev)}
            disabled={disabled}
            className="rounded-lg p-1 text-slate-500 transition-colors hover:text-slate-300 disabled:cursor-not-allowed disabled:opacity-50"
            aria-label="Toggle options"
          >
            <ChevronDown className={`h-4 w-4 transition-transform ${open ? 'rotate-180' : ''}`} />
          </button>
        </div>
      </div>

      {open && (
        <div className="absolute z-20 mt-1.5 max-h-60 w-full overflow-y-auto rounded-xl border border-white/10 bg-slate-900 shadow-xl shadow-black/40">
          {filteredOptions.length === 0 ? (
            <p className="px-4 py-3 text-sm text-slate-500">{emptyOptionsText ?? 'No matches found.'}</p>
          ) : (
            filteredOptions.map((option, index) => {
              const active = index === highlightedIndex;
              const selected = option.label === value;
              return (
                <button
                  key={option.id}
                  type="button"
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => selectOption(option)}
                  onMouseEnter={() => setHighlightedIndex(index)}
                  className={`flex w-full items-center justify-between gap-3 px-4 py-2.5 text-left text-sm transition-colors ${
                    active ? 'bg-brand-500/15 text-white' : 'text-slate-300'
                  }`}
                >
                  <span className="flex min-w-0 items-center gap-2">
                    {selected && <Check className="h-3.5 w-3.5 shrink-0 text-brand-400" />}
                    <span className="truncate">{option.label}</span>
                  </span>
                  {option.meta && <span className="shrink-0 text-xs text-slate-500">{option.meta}</span>}
                </button>
              );
            })
          )}
        </div>
      )}
    </div>
  );
}
