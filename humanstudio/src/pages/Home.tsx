import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  WelcomeHeader,
  CurrentLevelCard,
  ContinueLearningSection,
  CreateCourseButton,
} from '../components/home';
import { mockEvolutionProgress, mockActiveCourses } from '../lib/mockData';

const Home: React.FC = () => {
  const navigate = useNavigate();
  const [isLoading] = useState(false);
  const [courses] = useState(mockActiveCourses);

  const handleContinueCourse = (courseId: string) => {
    navigate(`/capabilities/${courseId}`);
  };

  return (
    <div className="space-y-8">
      <WelcomeHeader userName="Jorge" />
      <CurrentLevelCard evolutionProgress={mockEvolutionProgress} />
      <ContinueLearningSection
        courses={courses}
        isLoading={isLoading}
        onContinueCourse={handleContinueCourse}
      />
      <CreateCourseButton />
    </div>
  );
};

export default Home;
