import StepCard from '@components/StepCard'
import { type RuntimeAssessmentResult, MetricVerificationStatus } from '@/types/runtime'

interface AssessmentStepProps {
  assessment?: RuntimeAssessmentResult
  onContinue: () => void
  isSubmitting?: boolean
}

export default function AssessmentStep({ assessment, onContinue, isSubmitting = false }: AssessmentStepProps) {
  // Use backend assessment if available, otherwise show placeholder
  const criteria = assessment?.successCriteriaResults || [
    { criterion: 'Esperando evaluación...', isSatisfied: false, evidence: '' }
  ]

  const isVerified = assessment?.status === MetricVerificationStatus.Verified

  return (
    <StepCard label="Evaluación">
      <div className="space-y-4 mb-6">
        <p className="text-sm text-gray-500">
          Así es como tu trabajo se alinea con los criterios de éxito:
        </p>

        <div className="space-y-2.5">
          {criteria.map((criterion, idx) => (
            <div
              key={idx}
              className={`p-3 rounded-lg border flex items-center gap-3 ${
                criterion.isSatisfied
                  ? 'bg-indigo-50 border-indigo-100'
                  : 'bg-gray-50 border-gray-200'
              }`}
            >
              <span className={`text-base ${criterion.isSatisfied ? 'text-indigo-600' : 'text-gray-400'}`}>
                {criterion.isSatisfied ? '✓' : '○'}
              </span>
              <div className="flex-1">
                <p className={`text-sm ${criterion.isSatisfied ? 'text-indigo-900 font-medium' : 'text-gray-600'}`}>
                  {criterion.criterion}
                </p>
                {criterion.evidence && (
                  <p className="text-xs text-gray-500 mt-1">{criterion.evidence}</p>
                )}
              </div>
            </div>
          ))}
        </div>

        {assessment && (
          <div
            className={`mt-6 p-4 rounded-lg border ${
              isVerified ? 'bg-indigo-50 border-indigo-100' : 'bg-amber-50 border-amber-100'
            }`}
          >
            <p className={`text-sm font-semibold ${isVerified ? 'text-indigo-700' : 'text-amber-700'}`}>
              {isVerified ? '✓ Verificado' : '⚠ Necesita revisión'}
            </p>
            <p className={`text-sm mt-1 ${isVerified ? 'text-indigo-600' : 'text-amber-600'}`}>
              {assessment.explanation}
            </p>
          </div>
        )}
      </div>

      <button
        onClick={onContinue}
        disabled={isSubmitting}
        className="w-full px-4 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
      >
        {isSubmitting ? 'Procesando...' : 'Continuar a la reflexión →'}
      </button>
    </StepCard>
  )
}
