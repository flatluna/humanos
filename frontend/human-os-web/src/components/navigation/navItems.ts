import {
  CalendarCheck,
  Map,
  GraduationCap,
  Layers,
  BookmarkCheck,
  TrendingUp,
  Brain,
  Bot,
  type LucideIcon,
} from 'lucide-react';

export interface NavItem {
  to: string;
  icon: LucideIcon;
  labelKey:
    | 'nav.today'
    | 'nav.growthPlan'
    | 'nav.programs'
    | 'nav.myPrograms'
    | 'nav.capabilities'
    | 'nav.myCapabilities'
    | 'nav.myEvolution'
    | 'nav.myBrain'
    | 'nav.agents';
  /** Shown in the nav but not yet navigable (2026-07-25: Growth Plan is
   *  seeing low real-world usage vs. the direct catalog enroll flow, so
   *  it's kept visible as a placeholder for a future lighter-weight,
   *  goal-based AI recommender instead of being removed outright). */
  disabled?: boolean;
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
      { to: '/growth-plan', icon: Map, labelKey: 'nav.growthPlan', disabled: true },
      { to: '/programs/catalog', icon: GraduationCap, labelKey: 'nav.programs' },
      { to: '/programs', icon: BookmarkCheck, labelKey: 'nav.myPrograms' },
      { to: '/capabilities', icon: Layers, labelKey: 'nav.capabilities' },
      { to: '/my-capabilities', icon: BookmarkCheck, labelKey: 'nav.myCapabilities' },
      { to: '/growth', icon: TrendingUp, labelKey: 'nav.myEvolution' },
      { to: '/brain', icon: Brain, labelKey: 'nav.myBrain' },
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
  ALL_NAV_ITEMS.find((item) => item.to === '/today')!,
  ALL_NAV_ITEMS.find((item) => item.to === '/growth-plan')!,
  ALL_NAV_ITEMS.find((item) => item.to === '/capabilities')!,
  ALL_NAV_ITEMS.find((item) => item.to === '/growth')!,
];
