import { Check } from 'lucide-react';

interface StepIndicatorProps {
  steps: string[];
  currentStep: number;
}

export default function StepIndicator({ steps, currentStep }: StepIndicatorProps) {
  return (
    <div className="flex flex-col items-center gap-2">
      <div className="flex flex-wrap items-center justify-center gap-x-2 gap-y-3 sm:gap-x-3">
        {steps.map((label, index) => {
          const stepNumber = index + 1;
          const isCompleted = stepNumber < currentStep;
          const isActive = stepNumber === currentStep;

          return (
            <div key={label} className="flex items-center gap-2 sm:gap-3">
              <div className="flex flex-col items-center gap-1">
                <div
                  className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full border text-xs font-semibold transition-colors ${
                    isCompleted
                      ? 'border-transparent bg-gradient-to-br from-brand-500 to-accent-500 text-[#fff]'
                      : isActive
                      ? 'border-brand-400 bg-brand-500/10 text-brand-300'
                      : 'border-white/10 bg-white/[0.03] text-slate-500'
                  }`}
                >
                  {isCompleted ? <Check className="h-4 w-4" /> : stepNumber}
                </div>
                <span
                  className={`hidden max-w-[6.5rem] text-center text-[11px] leading-tight lg:block ${
                    isActive ? 'text-white font-medium' : 'text-slate-500'
                  }`}
                >
                  {label}
                </span>
              </div>
              {stepNumber < steps.length && <div className={`h-px w-4 sm:w-6 ${isCompleted ? 'bg-brand-500' : 'bg-white/10'}`} />}
            </div>
          );
        })}
      </div>
      <p className="text-xs text-slate-500 lg:hidden">
        {currentStep} / {steps.length} — {steps[currentStep - 1]}
      </p>
    </div>
  );
}
