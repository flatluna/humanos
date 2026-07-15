import { useParams, useNavigate } from 'react-router-dom';

export function ReviewPage() {
  const { runId } = useParams<{ runId: string }>();
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-50 to-blue-50 py-12 px-4">
      <div className="max-w-2xl mx-auto">
        <div className="bg-white rounded-lg shadow-lg p-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-4">
            GATE 2 — Revisión Final
          </h1>

          <p className="text-gray-600 mb-8">
            Este es el paso final antes de publicar tu capability. El contenido está listo para ser revisado.
          </p>

          <div className="bg-blue-50 border border-blue-200 rounded-lg p-6 mb-8">
            <p className="text-blue-900 font-medium">
              <strong>runId:</strong> {runId}
            </p>
            <p className="text-blue-800 text-sm mt-2">
              En producción, aquí se mostraría el CapabilityPackage completo,
              la TutorKnowledgeBase, y controles de aprobación final.
            </p>
          </div>

          <div className="flex gap-3">
            <button
              onClick={() => navigate(`/studio/runs/${runId}/generation`)}
              className="px-6 py-3 border-2 border-blue-600 text-blue-600 font-medium rounded-lg hover:bg-blue-50 transition-all"
            >
              ← Volver a generación
            </button>

            <button
              onClick={() => navigate('/')}
              className="px-6 py-3 bg-green-600 text-white font-medium rounded-lg hover:bg-green-700 transition-all cursor-pointer"
            >
              ✓ Publicar capability
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
