import {
  CalendarCheck,
  Map,
  Layers,
  Target,
  FileCheck2,
  TrendingUp,
  Bot,
  type LucideIcon,
} from 'lucide-react';

export interface NavItem {
  to: string;
  icon: LucideIcon;
  labelKey:
    | 'nav.today'
    | 'nav.growthPlan'
    | 'nav.capabilities'
    | 'nav.goals'
    | 'nav.evidence'
    | 'nav.myEvolution'
    | 'nav.agents';
}

export interface NavGroup {
  labelKey: 'nav.groupToday' | 'nav.groupGrowth' | 'nav.groupSupport';
  items: NavItem[];
}

/** Single source of truth for primary navigation, shared by the desktop
 *  sidebar, the mobile bottom bar, and the header's current-page title.
 */
export const NAV_GROUPS: NavGroup[] = [
  {
    labelKey: 'nav.groupToday',
    items: [{ to: '/today', icon: CalendarCheck, labelKey: 'nav.today' }],
  },
  {
    labelKey: 'nav.groupGrowth',
    items: [
      { to: '/growth-plan', icon: Map, labelKey: 'nav.growthPlan' },
      { to: '/capabilities', icon: Layers, labelKey: 'nav.capabilities' },
      { to: '/goals', icon: Target, labelKey: 'nav.goals' },
      { to: '/evidence', icon: FileCheck2, labelKey: 'nav.evidence' },
      { to: '/growth', icon: TrendingUp, labelKey: 'nav.myEvolution' },
    ],
  },
  {
    labelKey: 'nav.groupSupport',
    items: [{ to: '/agents', icon: Bot, labelKey: 'nav.agents' }],
  },
];

/** Flat list, used for the mobile bottom bar and route→title lookups. */
export const ALL_NAV_ITEMS: NavItem[] = NAV_GROUPS.flatMap((group) => group.items);

/** The five destinations shown directly in the mobile bottom bar; the
 *  rest surface inside the "More" drawer.
 */
export const MOBILE_BAR_ITEMS: NavItem[] = [
  ALL_NAV_ITEMS[0], // Today
  ALL_NAV_ITEMS[1], // Growth Plan
  ALL_NAV_ITEMS[2], // Capabilities
  ALL_NAV_ITEMS[5], // My Evolution
];
