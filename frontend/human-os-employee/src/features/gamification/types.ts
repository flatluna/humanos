export interface RewardResultDto {
  alreadyEarned?: boolean;
  AlreadyEarned?: boolean;
  tier?: string;
  Tier?: string;
  pointsBase?: number;
  PointsBase?: number;
  difficultyMultiplier?: number;
  DifficultyMultiplier?: number;
  memoryEffortMultiplier?: number;
  MemoryEffortMultiplier?: number;
  surpriseMultiplier?: number;
  SurpriseMultiplier?: number;
  pointsAwarded?: number;
  PointsAwarded?: number;
  wasSurprise?: boolean;
  WasSurprise?: boolean;
  surpriseType?: 'NONE' | 'BONUS' | 'DOUBLE' | 'JACKPOT' | string;
  SurpriseType?: 'NONE' | 'BONUS' | 'DOUBLE' | 'JACKPOT' | string;
  newTotalPoints?: number;
  NewTotalPoints?: number;
  newLevel?: number;
  NewLevel?: number;
  newLevelName?: string;
  NewLevelName?: string;
  leveledUp?: boolean;
  LeveledUp?: boolean;
}

export interface StreakResultDto {
  currentStreakDays?: number;
  CurrentStreakDays?: number;
  longestStreakDays?: number;
  LongestStreakDays?: number;
  streakFreezesAvailable?: number;
  StreakFreezesAvailable?: number;
  streakChanged?: 'STARTED' | 'ALREADY_COUNTED_TODAY' | 'CONTINUED' | 'FROZEN' | 'BROKEN' | string;
  StreakChanged?: 'STARTED' | 'ALREADY_COUNTED_TODAY' | 'CONTINUED' | 'FROZEN' | 'BROKEN' | string;
  isNewRecord?: boolean;
  IsNewRecord?: boolean;
}

export interface AwardedBadgeDto {
  badgeCode?: string;
  BadgeCode?: string;
  name?: string;
  Name?: string;
  iconEmoji?: string;
  IconEmoji?: string;
  description?: string;
  Description?: string;
  earnedAt?: string;
  EarnedAt?: string;
}

export interface GamificationRewardBundleDto {
  tier?: string;
  Tier?: string;
  reward?: RewardResultDto | null;
  Reward?: RewardResultDto | null;
  streak?: StreakResultDto | null;
  Streak?: StreakResultDto | null;
  newBadges?: AwardedBadgeDto[];
  NewBadges?: AwardedBadgeDto[];
  alreadyEarned?: boolean;
  AlreadyEarned?: boolean;
}
