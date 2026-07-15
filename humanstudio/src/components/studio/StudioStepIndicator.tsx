import React from 'react';

interface StudioStepIndicatorProps {
  activeStep?: number;
  /** Overrides the label for step 3 (e.g. "Publicando" while publishing). Defaults to "Curso listo". */
  step3Label?: string;
}

const StudioStepIndicator: React.FC<StudioStepIndicatorProps> = ({
  activeStep = 1,
  step3Label = 'Curso listo',
}) => {
  const steps = [
    { number: 1, label: 'Objetivo', active: activeStep === 1 },
    { number: 2, label: 'Blueprint', active: activeStep === 2 },
    { number: 3, label: step3Label, active: activeStep === 3 },
  ];

  return (
    <div className="mb-8">
      <div className="flex items-center justify-center gap-2 flex-wrap">
        {steps.map((step, index) => (
          <React.Fragment key={step.number}>
            <div className="flex items-center gap-2">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center font-semibold transition-colors ${
                  step.active
                    ? 'bg-purple-600 text-white'
                    : 'bg-gray-200 text-gray-600'
                }`}
              >
                {step.active ? '●' : '○'}
              </div>
              <span
                className={`text-sm font-medium transition-colors ${
                  step.active ? 'text-purple-600' : 'text-gray-500'
                }`}
              >
                {step.number}. {step.label}
              </span>
            </div>
            {index < steps.length - 1 && (
              <div className="hidden sm:block w-8 h-px bg-gray-300 mx-2" />
            )}
          </React.Fragment>
        ))}
      </div>
    </div>
  );
};

export default StudioStepIndicator;
