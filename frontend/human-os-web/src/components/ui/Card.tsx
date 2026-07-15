import { type HTMLAttributes } from 'react';
import { clsx } from 'clsx';

export function Card({ className, ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={clsx(
        'rounded-3xl border border-slate-200 bg-white shadow-sm shadow-slate-200/50',
        'dark:border-white/10 dark:bg-white/[0.03] dark:shadow-none',
        className,
      )}
      {...props}
    />
  );
}
