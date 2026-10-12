import { FamilyMember, FamilyGoal, ActivityMilestone, CourseOrPath } from '../types';
import { INITIAL_MEMBERS, INITIAL_GOALS, INITIAL_MILESTONES, INITIAL_COURSES } from '../data/mockFamilyData';

export interface FamilyMasterRecord {
  familyId: string;
  familyName: string;
  familySlug: string;
  householdType: string;
  adminFullName: string;
  adminEmail: string;
  adminPhone?: string;
  coAdminFullName?: string;
  coAdminEmail?: string;
  primaryStreet?: string;
  primaryCity?: string;
  primaryState?: string;
  primaryCountry: string;
  secondaryLocation?: string;
  timeZone: string;
  preferredCurrency: 'USD' | 'MXN';
  primaryLanguage: 'es' | 'en';
  isBilingualHousehold: boolean;
  annualEducationBudget?: number;
  digitalSunsetTime?: string;
  familyMissionStatement?: string;
  subscriptionTier: string;
  isActive: boolean;
}

const DEFAULT_FAMILY_MASTER: FamilyMasterRecord = {
  familyId: 'A1000000-0000-0000-0000-000000000001',
  familyName: 'Familia Mendoza-Ríos',
  familySlug: 'mendoza-rios',
  householdType: 'Bicultural',
  adminFullName: 'Carlos Mendoza, M.Sc.',
  adminEmail: 'carlos.mendoza@engrams.family',
  adminPhone: '+1 (512) 555-0192',
  coAdminFullName: 'Dra. Elena Ríos Mendoza',
  coAdminEmail: 'elena.rios@engrams.family',
  primaryStreet: '1204 Oak Creek Blvd',
  primaryCity: 'Austin',
  primaryState: 'TX',
  primaryCountry: 'USA',
  secondaryLocation: 'Ciudad de México (Residencia secundaria)',
  timeZone: 'America/Chicago',
  preferredCurrency: 'USD',
  primaryLanguage: 'es',
  isBilingualHousehold: true,
  annualEducationBudget: 27000.0,
  digitalSunsetTime: '21:00:00',
  familyMissionStatement:
    'Compromiso con el bilingüismo académico, el desarrollo socioemocional, la curiosidad intelectual y el orgullo por nuestras raíces latinoamericanas.',
  subscriptionTier: 'FamilyEnterprise',
  isActive: true,
};

// In-memory or API state store
let currentFamily = { ...DEFAULT_FAMILY_MASTER };
let membersList = [...INITIAL_MEMBERS];
let goalsList = [...INITIAL_GOALS];
let milestonesList = [...INITIAL_MILESTONES];
let coursesList = [...INITIAL_COURSES];

export const familyApi = {
  async getFamilyMaster(): Promise<FamilyMasterRecord> {
    return Promise.resolve({ ...currentFamily });
  },

  async updateFamilyMaster(updates: Partial<FamilyMasterRecord>): Promise<FamilyMasterRecord> {
    currentFamily = { ...currentFamily, ...updates };
    return Promise.resolve({ ...currentFamily });
  },

  async getMembers(): Promise<FamilyMember[]> {
    return Promise.resolve([...membersList]);
  },

  async getGoals(): Promise<FamilyGoal[]> {
    return Promise.resolve([...goalsList]);
  },

  async getMilestones(): Promise<ActivityMilestone[]> {
    return Promise.resolve([...milestonesList]);
  },

  async getCourses(): Promise<CourseOrPath[]> {
    return Promise.resolve([...coursesList]);
  },
};
