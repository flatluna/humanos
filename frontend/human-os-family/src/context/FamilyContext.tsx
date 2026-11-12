import React, { createContext, useContext, useState, useEffect, useCallback, useMemo } from 'react';
import { FamilyMember, FamilyRole, FamilyGoal, ActivityMilestone, CourseOrPath } from '../types';
import { INITIAL_GOALS, INITIAL_MILESTONES, INITIAL_COURSES } from '../data/mockFamilyData';
import { familyApi, type FamilyMasterRecord, type FamilyMemberRecord } from '../api/familyApi';
import { requiresOwnMsalAccount, type PlatformRole } from '../lib/accountRules';
import { useAuth } from '../auth/AuthContext';

function mapMemberRecord(m: FamilyMemberRecord): FamilyMember {
  return {
    id: m.memberId,
    name: m.fullName,
    preferredName: m.preferredName || m.fullName,
    role: (m.familyRole as FamilyRole) || 'father',
    roleTitleEs: m.roleTitleEs || 'Integrante',
    roleTitleEn: m.roleTitleEn || 'Member',
    age: m.age ?? 0,
    requiresOwnAccount: requiresOwnMsalAccount(m.age),
    hasLinkedAccount: Boolean(m.azureOid),
    azureOid: m.azureOid ?? null,
    azureLoginEmail: m.azureLoginEmail ?? null,
    platformRole: m.platformRole ?? 'User',
    avatarBg: m.avatarBg || 'from-emerald-600 to-teal-700',
    initial: (m.preferredName || m.fullName || '?').charAt(0).toUpperCase(),
    educationLevelEs: m.educationLevelEs || '',
    educationLevelEn: m.educationLevelEn || '',
    institutionEs: m.institution || '',
    institutionEn: m.institution || '',
    primaryLocation: m.primaryLocation || '',
    topFocusEs: m.topFocus || [],
    topFocusEn: m.topFocus || [],
    weeklyHoursLogged: m.weeklyHoursTarget ?? 0,
    learningStreakDays: m.learningStreakDays ?? 0,
    interests: [],
    languages: m.languages || [],
    targetMilestoneEs: m.targetMilestoneEs || '',
    targetMilestoneEn: m.targetMilestoneEn || '',
  };
}

interface FamilyContextType {
  family: FamilyMasterRecord | null;
  members: FamilyMember[];
  selectedMemberId: string | null; // null means 'all'
  setSelectedMemberId: (id: string | null) => void;
  addMember: (member: FamilyMember) => void;
  goals: FamilyGoal[];
  milestones: ActivityMilestone[];
  courses: CourseOrPath[];
  activeCurrency: 'USD' | 'MXN';
  setActiveCurrency: (c: 'USD' | 'MXN') => void;
  isAiCoachOpen: boolean;
  setIsAiCoachOpen: (open: boolean) => void;
  householdName: string;
  householdCities: string;
  isLoading: boolean;
  refreshFamily: () => Promise<void>;
  /** Platform role of the currently signed-in member (matched by Azure OID), defaults to 'User' if unlinked/unknown. */
  currentUserRole: PlatformRole;
  currentMember: FamilyMember | null;
}

const FamilyContext = createContext<FamilyContextType | undefined>(undefined);

export function FamilyProvider({ children }: { children: React.ReactNode }) {
  const [family, setFamily] = useState<FamilyMasterRecord | null>(null);
  const [members, setMembers] = useState<FamilyMember[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null);
  const [goals] = useState<FamilyGoal[]>(INITIAL_GOALS);
  const [milestones] = useState<ActivityMilestone[]>(INITIAL_MILESTONES);
  const [courses] = useState<CourseOrPath[]>(INITIAL_COURSES);
  const [activeCurrency, setActiveCurrency] = useState<'USD' | 'MXN'>('USD');
  const [isAiCoachOpen, setIsAiCoachOpen] = useState(false);
  const { account } = useAuth();

  const signedInOid = (account?.idTokenClaims as Record<string, unknown> | undefined)?.oid as string | undefined;
  const currentMember = useMemo(
    () => (signedInOid ? members.find((m) => m.azureOid === signedInOid) ?? null : null),
    [members, signedInOid]
  );
  const currentUserRole: PlatformRole = currentMember?.platformRole ?? 'User';

  const refreshFamily = useCallback(async () => {
    try {
      setIsLoading(true);
      const data = await familyApi.getFamilyMasterData();
      setFamily(data.family);
      setMembers(data.members.map(mapMemberRecord));
    } catch (err) {
      console.error('Failed to load family data from SQL:', err);
      setMembers([]);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    refreshFamily();
  }, [refreshFamily]);

  const addMember = (newMember: FamilyMember) => {
    setMembers((prev) => [newMember, ...prev]);
  };

  const householdName = family?.familyName || 'EngramsIQ Family';
  const householdCities =
    family?.primaryCity && family?.primaryState
      ? `${family.primaryCity}, ${family.primaryState}`
      : family?.primaryCity || family?.primaryState || family?.primaryCountry || '';

  return (
    <FamilyContext.Provider
      value={{
        family,
        members,
        selectedMemberId,
        setSelectedMemberId,
        addMember,
        goals,
        milestones,
        courses,
        activeCurrency,
        setActiveCurrency,
        isAiCoachOpen,
        setIsAiCoachOpen,
        householdName,
        householdCities,
        isLoading,
        refreshFamily,
        currentUserRole,
        currentMember,
      }}
    >
      {children}
    </FamilyContext.Provider>
  );
}

export function useFamily() {
  const context = useContext(FamilyContext);
  if (!context) {
    throw new Error('useFamily must be used within a FamilyProvider');
  }
  return context;
}
