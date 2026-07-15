import { useState } from 'react';
import { FinalReviewPackage } from '../../../types';
import { OverviewTab } from './OverviewTab';
import { ContentTab } from './ContentTab';
import { ValidationTab } from './ValidationTab';
import { TutorKnowledgeBaseTab } from './TutorKnowledgeBaseTab';

export interface ReviewTabsProps {
  package: FinalReviewPackage;
  onReviewModule?: (moduleId: string) => void;
}

export function ReviewTabs({ package: pkg, onReviewModule }: ReviewTabsProps) {
  const [activeTab, setActiveTab] = useState<'overview' | 'content' | 'validation' | 'kb'>(
    'overview'
  );

  const tabs = [
    { id: 'overview' as const, label: 'Resumen' },
    { id: 'content' as const, label: 'Contenido' },
    { id: 'validation' as const, label: 'Validación' },
    { id: 'kb' as const, label: 'Base del tutor' },
  ];

  return (
    <div className="bg-white border border-gray-200 rounded-lg mb-6">
      {/* Tab buttons */}
      <div className="flex border-b border-gray-200">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`flex-1 py-3 px-4 font-semibold text-sm border-b-2 transition-colors ${
              activeTab === tab.id
                ? 'border-blue-600 text-blue-600'
                : 'border-transparent text-gray-600 hover:text-gray-900'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      <div className="p-6">
        {activeTab === 'overview' && <OverviewTab package={pkg} />}
        {activeTab === 'content' && <ContentTab package={pkg} onReviewModule={onReviewModule} />}
        {activeTab === 'validation' && <ValidationTab package={pkg} />}
        {activeTab === 'kb' && <TutorKnowledgeBaseTab package={pkg} />}
      </div>
    </div>
  );
}
