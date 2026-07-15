import { useNavigate } from 'react-router-dom';

interface PublishedActionsProps {
  capabilityId: string;
  onCreateAnother: () => void;
}

export default function PublishedActions({ capabilityId, onCreateAnother }: PublishedActionsProps) {
  const navigate = useNavigate();

  return (
    <div className="flex flex-col sm:flex-row gap-3">
      <button
        onClick={() => navigate(`/capabilities/${capabilityId}`)}
        className="flex-1 inline-flex items-center justify-center px-6 py-3 rounded-lg font-medium bg-blue-600 text-white hover:bg-blue-700 active:bg-blue-800 transition-all cursor-pointer"
      >
        Ver capability
      </button>
      <button
        onClick={onCreateAnother}
        className="flex-1 inline-flex items-center justify-center px-6 py-3 rounded-lg font-medium border-2 border-gray-300 text-gray-700 hover:bg-gray-50 transition-all cursor-pointer"
      >
        Crear otra capability
      </button>
      <button
        onClick={() => navigate('/capabilities')}
        className="flex-1 inline-flex items-center justify-center px-6 py-3 rounded-lg font-medium border-2 border-gray-300 text-gray-700 hover:bg-gray-50 transition-all cursor-pointer"
      >
        Volver a Capability Library
      </button>
    </div>
  );
}
