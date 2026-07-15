import { EvolutionProgress, Course } from '../types';

export const mockEvolutionProgress: EvolutionProgress = {
  currentLevel: 'Exploration',
  nextLevel: 'Mastery',
  percentage: 55,
};

export const mockActiveCourses: Course[] = [
  {
    id: '1',
    title: 'Pensamiento crítico',
    level: 'Exploration',
    completedModules: 3,
    totalModules: 6,
    progress: 50,
  },
  {
    id: '2',
    title: 'Comunicación efectiva',
    level: 'Exploration',
    completedModules: 7,
    totalModules: 10,
    progress: 70,
  },
  {
    id: '3',
    title: 'Resolución de problemas',
    level: 'Foundation',
    completedModules: 2,
    totalModules: 5,
    progress: 40,
  },
];

export const mockEmptyCourses: Course[] = [];
