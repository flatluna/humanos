import { useNavigate } from 'react-router-dom';
import { RotateCcw, Home } from 'lucide-react';

interface GenerationActionsProps {
  onRefresh: () => Promise<void>;
  isRefreshing: boolean;
  isComplete: boolean;
  runId: string;
}

export default function GenerationActions({
  onRefresh,
  isRefreshing,
  isComplete,
  runId,
}: GenerationActionsProps) {
  const navigate = useNavigate();

  const handleNavigateToReview = () => {
    navigate(`/studio/runs/${runId}/review`);
  };

  const handleNavigateToHome = () => {
    navigate('/');
  };

  return (
    <div className="flex flex-col sm:flex-row gap-3 mt-8 pt-6 border-t border-gray-200">
      {isComplete ? (
        <>
          <button
            onClick={handleNavigateToReview}
            className="flex-1 inline-flex items-center justify-center gap-2 px-6 py-3 rounded-lg font-medium bg-green-600 text-white hover:bg-green-700 active:bg-green-800 transition-all cursor-pointer"
          >
            ✓ Ir a revisión final
          </button>
          <button
            onClick={handleNavigateToHome}
            className="flex-1 inline-flex items-center justify-center gap-2 px-6 py-3 rounded-lg font-medium border-2 border-gray-300 text-gray-700 hover:bg-gray-50 transition-all cursor-pointer"
          >
            <Home size={18} />
            Ir al dashboard
          </button>
        </>
      ) : (
        <button
          onClick={onRefresh}
          disabled={isRefreshing}
          className={`flex-1 inline-flex items-center justify-center gap-2 px-6 py-3 rounded-lg font-medium transition-all ${
            isRefreshing
              ? 'bg-gray-300 text-gray-600 cursor-not-allowed'
              : 'bg-blue-600 text-white hover:bg-blue-700 active:bg-blue-800 cursor-pointer'
          }`}
        >
          {isRefreshing ? (
            <>
              <RotateCcw size={18} className="animate-spin" />
              Actualizando...
            </>
          ) : (
            <>
              <RotateCcw size={18} />
              Actualizar
            </>
          )}
        </button>
      )}
    </div>
  );
}
