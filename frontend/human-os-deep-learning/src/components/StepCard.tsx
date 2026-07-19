import { ReactNode } from 'react'

interface StepCardProps {
  label: string
  children: ReactNode
}

/**
 * Main content card for the active step (redesign 2026-07-16). Dropped
 * the old circled-unicode icon + "Step X of Y" badge — that information
 * now lives in the top StepIndicator stepper, so repeating it here was
 * redundant. Just a small uppercase eyebrow label + content.
 */
export default function StepCard({ label, children }: StepCardProps) {
  return (
    <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <div className="px-6 pt-6 pb-1">
        <span className="text-xs font-semibold uppercase tracking-wider text-indigo-600">
          {label}
        </span>
      </div>
      <div className="px-6 pb-6 pt-3">
        {children}
      </div>
    </div>
  )
}
