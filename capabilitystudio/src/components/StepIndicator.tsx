import { Check } from 'lucide-react';

interface StepIndicatorProps {
  steps: string[];
  currentStep: number;
}

export default function StepIndicator({ steps, currentStep }: StepIndicatorProps) {
  return (
    <div className="flex items-center justify-center gap-2 sm:gap-4">
      {steps.map((label, index) => {
        const stepNumber = index + 1;
        const isCompleted = stepNumber < currentStep;
        const isActive = stepNumber === currentStep;

        return (
          <div key={label} className="flex items-center gap-2 sm:gap-4">
            <div className="flex flex-col items-center gap-1.5">
              <div
                className={`flex h-9 w-9 items-center justify-center rounded-full border text-sm font-semibold transition-colors ${
                  isCompleted
                    ? 'border-transparent bg-gradient-to-br from-brand-500 to-accent-500 text-[#fff]'
                    : isActive
                    ? 'border-brand-400 bg-brand-500/10 text-brand-300'
                    : 'border-white/10 bg-white/[0.03] text-slate-500'
                }`}
              >
                {isCompleted ? <Check className="h-4.5 w-4.5" /> : stepNumber}
              </div>
              <span className={`hidden text-xs sm:block ${isActive ? 'text-white font-medium' : 'text-slate-500'}`}>
                {label}
              </span>
            </div>
            {stepNumber < steps.length && (
              <div className={`h-px w-8 sm:w-16 ${isCompleted ? 'bg-brand-500' : 'bg-white/10'}`} />
            )}
          </div>
        );
      })}
    </div>
  );
}
