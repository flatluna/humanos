import { apiGet } from '@/features/capabilities/api/httpClient';

export interface PlayerProfileDto {
  totalPoints: number;
  currentLevel: number;
  currentLevelName: string;
  currentStreakDays: number;
  longestStreakDays: number;
  streakFreezesAvailable: number;
  totalEngramsMastered: number;
  pointsToNextLevel: number;
  nextLevelName: string | null;
  totalAssignedEngrams?: number;
  totalAssignedCourses?: number;
  completedCourses?: number;
  coursesReady?: number;
  coursesPendingGeneration?: number;
}

export interface BadgeStatusDto {
  code: string;
  name: string;
  description: string;
  iconEmoji: string;
  isEarned: boolean;
  earnedAt: string | null;
  ruleType?: string;
  ruleThreshold?: number;
}

export interface BadgeProgressDto {
  code: string;
  name: string;
  description: string;
  iconEmoji: string;
  category?: string;
  ruleType?: string;
  targetValue: number;
  currentValue: number;
  progressPercent: number;
  isEarned: boolean;
  earnedAt: string | null;
}

export interface RewardHistoryDto {
  engramId: string;
  pointsAwarded: number;
  wasSurprise: boolean;
  surpriseType?: string;
  reason?: string;
  earnedAt: string;
}

/**
 * Retrieves the current gamification status of the authenticated student.
 */
export async function getGamificationProfile(): Promise<PlayerProfileDto> {
  return apiGet<PlayerProfileDto>('/gamification/profile');
}

/**
 * Retrieves all badges from the catalog marking earned status for the student.
 */
export async function getGamificationBadges(): Promise<BadgeStatusDto[]> {
  return apiGet<BadgeStatusDto[]>('/gamification/badges');
}

/**
 * Retrieves all badges with per-badge progress metrics (current value, target, percent, earned status)
 * for the authenticated student.
 */
export async function getBadgeProgress(): Promise<BadgeProgressDto[]> {
  return apiGet<BadgeProgressDto[]>('/gamification/badges/progress');
}

/**
 * Retrieves recent reward ledger entries for the student.
 */
export async function getGamificationRecentRewards(limit: number = 20): Promise<RewardHistoryDto[]> {
  return apiGet<RewardHistoryDto[]>(`/gamification/rewards/recent?limit=${encodeURIComponent(limit)}`);
}
