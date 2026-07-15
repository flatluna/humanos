import { HumanEvolutionLevelName } from '../../../types';
import ModuleGenerationCard from './ModuleGenerationCard';
import { ModuleGenerationStatus } from '../../../types';

interface GenerationLevelSectionProps {
  levelName: HumanEvolutionLevelName;
  modules: ModuleGenerationStatus[];
  onRetryModule: (moduleId: string) => Promise<void>;
  isRetrying: boolean;
}

export default function GenerationLevelSection({
  levelName,
  modules,
  onRetryModule,
  isRetrying,
}: GenerationLevelSectionProps) {
  return (
    <div className="mb-8">
      {/* Level Header */}
      <div className="px-4 py-2 bg-blue-100 border-2 border-blue-600 rounded-sm mb-4">
        <p className="text-sm font-bold text-blue-900">NIVEL: {levelName}</p>
      </div>

      {/* Modules */}
      <div className="space-y-4">
        {modules.map((module) => (
          <ModuleGenerationCard
            key={module.id}
            module={module}
            onRetry={onRetryModule}
            isRetrying={isRetrying}
          />
        ))}
      </div>
    </div>
  );
}
