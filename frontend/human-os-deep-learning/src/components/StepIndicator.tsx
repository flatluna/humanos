interface StepIndicatorProps {
  currentStep: number
  totalSteps: number
  labels: string[]
}

/**
 * Top-down horizontal progress stepper (redesign 2026-07-16). Each circle
 * now has a text label underneath — bare numbered circles with no title
 * were confusing (user feedback: "los circulos no tienen titulos eso es
 * confuxo"). Single professional accent color (indigo) instead of mixed
 * blue/green/amber used elsewhere in the old design.
 */
export default function StepIndicator({ currentStep, totalSteps, labels }: StepIndicatorProps) {
  const steps = Array.from({ length: totalSteps }, (_, i) => i + 1)

  return (
    <div className="flex items-start justify-between">
      {steps.map((step) => (
        <div key={step} className="flex items-center flex-1 last:flex-none">
          <div className="flex flex-col items-center gap-1.5 w-20">
            <div
              className={`w-9 h-9 rounded-full flex items-center justify-center font-semibold text-xs transition-colors ${
                step < currentStep
                  ? 'bg-indigo-600 text-white'
                  : step === currentStep
                  ? 'bg-white text-indigo-600 border-2 border-indigo-600'
                  : 'bg-gray-100 text-gray-400 border border-gray-200'
              }`}
            >
              {step < currentStep ? '✓' : step}
            </div>
            <span
              className={`text-[11px] text-center leading-tight ${
                step === currentStep
                  ? 'text-indigo-700 font-semibold'
                  : step < currentStep
                  ? 'text-gray-600 font-medium'
                  : 'text-gray-400'
              }`}
            >
              {labels[step - 1]}
            </span>
          </div>
          {step < totalSteps && (
            <div
              className={`flex-1 h-0.5 mx-1 mt-[18px] ${
                step < currentStep ? 'bg-indigo-600' : 'bg-gray-200'
              }`}
            />
          )}
        </div>
      ))}
    </div>
  )
}
