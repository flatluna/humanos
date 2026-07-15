import { FinalReviewPackage } from '../../../types';
import { ReviewLevelSection } from './ReviewLevelSection';

export interface ContentTabProps {
  package: FinalReviewPackage;
  onReviewModule?: (moduleId: string) => void;
}

export function ContentTab({ package: pkg, onReviewModule }: ContentTabProps) {
  return (
    <div className="space-y-8">
      {pkg.levels.map((level) => (
        <ReviewLevelSection key={level.id} level={level} onReviewModule={onReviewModule} />
      ))}
    </div>
  );
}
