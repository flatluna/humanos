import { useTranslation } from 'react-i18next';
import { Building2, Clock, User } from 'lucide-react';
import { Badge } from '@/components/ui/Badge';
import type { JobDescriptionSourceType } from '../types';

const ICONS: Record<JobDescriptionSourceType, typeof Building2> = {
  organizationProvided: Building2,
  employeeProvided: User,
  pendingOrganizationReview: Clock,
};

interface SourceTypeBadgeProps {
  sourceType: JobDescriptionSourceType;
}

/** The three-state trust indicator required for every Job Description
 *  Source screen: who the current content came from, or whether an
 *  organization review was requested but hasn't happened yet. */
export function SourceTypeBadge({ sourceType }: SourceTypeBadgeProps) {
  const { t } = useTranslation();
  const Icon = ICONS[sourceType];

  return (
    <Badge tone={sourceType === 'organizationProvided' ? 'accent' : 'neutral'} className="gap-1.5">
      <Icon className="h-3.5 w-3.5" />
      {t(`growthPlan.roleExperience.jobDescriptionSource.sourceTypes.${sourceType}`)}
    </Badge>
  );
}
