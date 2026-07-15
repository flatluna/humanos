import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Plus } from 'lucide-react';

const CreateCourseButton: React.FC = () => {
  const navigate = useNavigate();

  const handleClick = () => {
    navigate('/studio');
  };

  return (
    <div className="flex justify-center mt-8">
      <button
        onClick={handleClick}
        className="inline-flex items-center gap-2 px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium text-lg"
      >
        <Plus size={20} />
        Crear curso nuevo
      </button>
    </div>
  );
};

export default CreateCourseButton;
