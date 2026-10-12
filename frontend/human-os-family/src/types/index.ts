export type Language = 'es' | 'en';
export type Theme = 'light' | 'dark';

export type FamilyRole = 'father' | 'mother' | 'teen_daughter' | 'son' | 'grandmother' | 'mentor';

export interface FamilyMember {
  id: string;
  name: string;
  preferredName: string;
  role: FamilyRole;
  roleTitleEs: string;
  roleTitleEn: string;
  age: number;
  avatarBg: string;
  initial: string;
  educationLevelEs: string;
  educationLevelEn: string;
  institutionEs: string;
  institutionEn: string;
  primaryLocation: string; // e.g. "Austin, TX / CDMX"
  topFocusEs: string[];
  topFocusEn: string[];
  weeklyHoursLogged: number;
  learningStreakDays: number;
  interests: string[];
  languages: string[];
  targetMilestoneEs: string;
  targetMilestoneEn: string;
}

export interface FamilyGoal {
  id: string;
  titleEs: string;
  titleEn: string;
  descriptionEs: string;
  descriptionEn: string;
  category: 'academic' | 'career' | 'wellness' | 'culture_bilingual' | 'finance';
  assignedMemberIds: string[];
  progressPercent: number;
  dueDate: string;
  status: 'in_progress' | 'completed' | 'planned';
}

export interface ActivityMilestone {
  id: string;
  titleEs: string;
  titleEn: string;
  dateStr: string;
  timeStr: string;
  memberId: string;
  tagEs: string;
  tagEn: string;
  location: string;
  category: 'university' | 'career' | 'school' | 'family_time' | 'health';
}

export interface CourseOrPath {
  id: string;
  titleEs: string;
  titleEn: string;
  institution: string;
  memberId: string;
  targetLevel: string;
  hoursPerWeek: number;
  progressPercent: number;
  badgeEs: string;
  badgeEn: string;
  regionFocus: 'USA' | 'LATAM' | 'Bicultural' | 'Global';
  status: 'active' | 'completed' | 'upcoming';
}
