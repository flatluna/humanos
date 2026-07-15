import React from 'react';
import { Course } from '../../types';
import CourseCard from './CourseCard';

interface ContinueLearningSectionProps {
  courses: Course[];
  isLoading?: boolean;
  onContinueCourse?: (courseId: string) => void;
}

const ContinueLearningSection: React.FC<ContinueLearningSectionProps> = ({
  courses,
  isLoading = false,
  onContinueCourse,
}) => {
  if (isLoading) {
    return (
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-gray-900 mb-6">
          Continúa donde quedaste
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {[1, 2, 3].map((i) => (
            <div key={i} className="bg-gray-200 rounded-lg h-56 animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  if (courses.length === 0) {
    return (
      <div className="bg-white rounded-lg shadow p-8 text-center mb-8">
        <p className="text-gray-600 mb-4">
          Todavía no tienes cursos en progreso.
        </p>
        <p className="text-gray-600 mb-6">
          Crea tu primera capacidad a partir de tus objetivos y materiales.
        </p>
        <a
          href="/studio"
          className="inline-block px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium"
        >
          + Crear curso nuevo
        </a>
      </div>
    );
  }

  return (
    <div className="mb-8">
      <h2 className="text-2xl font-bold text-gray-900 mb-6">
        Continúa donde quedaste
      </h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {courses.map((course) => (
          <CourseCard
            key={course.id}
            course={course}
            onContinue={onContinueCourse}
          />
        ))}
      </div>
    </div>
  );
};

export default ContinueLearningSection;
