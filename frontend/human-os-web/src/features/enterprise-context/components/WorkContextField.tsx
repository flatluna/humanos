interface WorkContextFieldProps {
  label: string;
  value: string;
  /** Renders the value larger/bolder for the most important field (Current Role). */
  prominent?: boolean;
}

/** A single label/value pair rendered with `<dt>`/`<dd>` definition-list
 *  semantics inside the cohesive Work Context summary card.
 */
export function WorkContextField({ label, value, prominent = false }: WorkContextFieldProps) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase tracking-wide text-slate-400 dark:text-white/40">{label}</dt>
      <dd
        className={
          prominent
            ? 'mt-1 text-xl font-semibold text-slate-900 dark:text-white'
            : 'mt-1 text-sm font-medium text-slate-700 dark:text-white/80'
        }
      >
        {value}
      </dd>
    </div>
  );
}
