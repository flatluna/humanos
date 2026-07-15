import type { LocalizedText } from '@/localization/types';

export interface OrganizationalInitiative {
  id: string;
  name: LocalizedText;
  whyItMattersToOrg: LocalizedText;
  whyItMattersToYou: LocalizedText;
  requiredCapabilities: LocalizedText[];
}
