import { FinalReviewLevel } from '../../../types';
import { ReviewModuleCard } from './ReviewModuleCard';

export interface ReviewLevelSectionProps {
  level: FinalReviewLevel;
  onReviewModule?: (moduleId: string) => void;
}

export function ReviewLevelSection({ level, onReviewModule }: ReviewLevelSectionProps) {
  return (
    <div className="mb-8">
      <div className="mb-4">
        <h4 className="text-lg font-bold text-blue-600">🟦 NIVEL: {level.level}</h4>
        <p className="text-sm text-gray-600 mt-1">{level.transformation}</p>
      </div>

      <div className="space-y-2">
        {level.modules.map((module) => (
          <ReviewModuleCard key={module.id} module={module} onReview={onReviewModule} />
        ))}
      </div>
    </div>
  );
}
