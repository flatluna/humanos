import React from 'react';
import { ArrowRight } from 'lucide-react';
import { Course } from '../../types';

interface CourseCardProps {
  course: Course;
  onContinue?: (courseId: string) => void;
}

const CourseCard: React.FC<CourseCardProps> = ({ course, onContinue }) => {
  const handleContinueClick = () => {
    if (onContinue) {
      onContinue(course.id);
    }
  };

  return (
    <div className="bg-white rounded-lg shadow p-5 hover:shadow-lg transition-shadow">
      <h3 className="text-lg font-semibold text-gray-900 mb-2">{course.title}</h3>
      
      <p className="text-sm text-gray-600 mb-3">
        <span className="inline-flex items-center gap-1">
          <span>🟦</span>
          <span>Nivel: {course.level}</span>
        </span>
      </p>

      <div className="mb-4">
        <p className="text-sm text-gray-600 mb-2">
          {course.completedModules} de {course.totalModules} módulos
        </p>
        <div className="w-full bg-gray-200 rounded-full h-2 overflow-hidden">
          <div
            className="bg-blue-600 h-full rounded-full transition-all duration-500"
            style={{ width: `${course.progress}%` }}
          />
        </div>
        <p className="text-xs text-gray-500 mt-1">{course.progress}%</p>
      </div>

      <button
        onClick={handleContinueClick}
        className="w-full inline-flex items-center justify-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium"
      >
        Continuar
        <ArrowRight size={18} />
      </button>
    </div>
  );
};

export default CourseCard;
