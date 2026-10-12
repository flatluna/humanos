import React, { createContext, useContext, useState } from 'react';
import { FamilyMember, FamilyGoal, ActivityMilestone, CourseOrPath } from '../types';
import { INITIAL_MEMBERS, INITIAL_GOALS, INITIAL_MILESTONES, INITIAL_COURSES } from '../data/mockFamilyData';

interface FamilyContextType {
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
}

const FamilyContext = createContext<FamilyContextType | undefined>(undefined);

export function FamilyProvider({ children }: { children: React.ReactNode }) {
  const [members, setMembers] = useState<FamilyMember[]>(INITIAL_MEMBERS);
  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null);
  const [goals] = useState<FamilyGoal[]>(INITIAL_GOALS);
  const [milestones] = useState<ActivityMilestone[]>(INITIAL_MILESTONES);
  const [courses] = useState<CourseOrPath[]>(INITIAL_COURSES);
  const [activeCurrency, setActiveCurrency] = useState<'USD' | 'MXN'>('USD');
  const [isAiCoachOpen, setIsAiCoachOpen] = useState(false);

  const addMember = (newMember: FamilyMember) => {
    setMembers((prev) => [newMember, ...prev]);
  };

  return (
    <FamilyContext.Provider
      value={{
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
        householdName: 'Familia Mendoza-Ríos',
        householdCities: 'Austin, TX · Ciudad de México',
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
