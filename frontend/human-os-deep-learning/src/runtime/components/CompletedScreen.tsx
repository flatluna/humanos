import { type RuntimeAssessmentResult, MetricVerificationStatus } from '@/types/runtime'

interface CompletedScreenProps {
  assessment?: RuntimeAssessmentResult
  message: string
  /** Starts a fresh session for the SAME module (fixed 2026-07-17 —
   * explicit user request: "forzar a comenzar la leccion seria buenisimo"
   * after seeing a Requires Revision outcome). */
  onRestartModule?: () => void
  isRestarting?: boolean
}

export default function CompletedScreen({ assessment, message, onRestartModule, isRestarting = false }: CompletedScreenProps) {
  const isVerified = assessment?.status === MetricVerificationStatus.Verified

  return (
    <div className="h-screen flex items-center justify-center bg-white px-8">
      <div className="max-w-2xl w-full">
        {/* Success icon */}
        <div className="text-center mb-8">
          <div className="text-6xl mb-4">🎉</div>
          <h1 className="text-3xl font-bold text-gray-900">Session Completed</h1>
        </div>

        {/* Verification summary */}
        <div className={`${isVerified ? 'bg-green-50 border-green-200' : 'bg-yellow-50 border-yellow-200'} border rounded-lg p-6 mb-8`}>
          <h2 className={`font-semibold ${isVerified ? 'text-green-900' : 'text-yellow-900'} mb-4`}>
            {isVerified ? '✅ Capability Evidence Verified' : '⚠️ Session Requires Review'}
          </h2>
          
          <div className="space-y-3 text-sm">
            {assessment && (
              <>
                <div>
                  <p className="text-gray-600">Target Metric</p>
                  <p className="font-semibold text-gray-900">{assessment.targetMetric}</p>
                </div>
                
                <div>
                  <p className="text-gray-600">Status</p>
                  <p className={`font-semibold ${isVerified ? 'text-green-700' : 'text-yellow-700'}`}>
                    {assessment.status}
                  </p>
                </div>

                {assessment.explanation && (
                  <div>
                    <p className="text-gray-600">Feedback</p>
                    <p className="text-gray-700 mt-1">{assessment.explanation}</p>
                  </div>
                )}
              </>
            )}
            
            {message && (
              <div>
                <p className="text-gray-600">Message</p>
                <p className="text-gray-700">{message}</p>
              </div>
            )}
          </div>
        </div>

        {/* What's next */}
        <div className="bg-gray-50 border border-gray-200 rounded-lg p-6 mb-8">
          <h3 className="font-semibold text-gray-900 mb-4">Next Steps</h3>
          <ul className="space-y-2 text-sm text-gray-700">
            <li>✓ Review your evidence in the Evidence gallery</li>
            <li>✓ Continue to the next module in this capability</li>
            <li>✓ Explore related capabilities</li>
          </ul>
        </div>

        {/* Actions */}
        <div className="flex gap-4">
          {!isVerified && onRestartModule && (
            <button
              onClick={onRestartModule}
              disabled={isRestarting}
              className="flex-1 px-4 py-3 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 font-medium disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isRestarting ? 'Reiniciando…' : '🔁 Reintentar el módulo'}
            </button>
          )}
          <button className="flex-1 px-4 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-medium">
            ← Back to Capabilities
          </button>
          <button className="flex-1 px-4 py-3 bg-gray-200 text-gray-900 rounded-lg hover:bg-gray-300 font-medium">
            View Evidence
          </button>
        </div>
      </div>
    </div>
  )
}
