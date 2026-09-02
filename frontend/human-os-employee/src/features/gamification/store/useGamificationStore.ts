import { create } from 'zustand';
import { getGamificationProfile, type PlayerProfileDto } from '../api/gamificationApi';

interface GamificationStore {
  profile: PlayerProfileDto | null;
  loading: boolean;
  error: string | null;
  fetchProfile: () => Promise<void>;
  updateProfileFromReward: (newTotalPoints: number, newLevel: number, newLevelName: string, streakDays?: number) => void;
}

const DEFAULT_PROFILE: PlayerProfileDto = {
  totalPoints: 0,
  currentLevel: 1,
  currentLevelName: 'Neurona',
  currentStreakDays: 0,
  longestStreakDays: 0,
  streakFreezesAvailable: 1,
  totalEngramsMastered: 0,
  pointsToNextLevel: 500,
  nextLevelName: 'Sinapsis',
  totalAssignedEngrams: 0,
  totalAssignedCourses: 0,
  completedCourses: 0,
  coursesReady: 0,
  coursesPendingGeneration: 0,
};

export const useGamificationStore = create<GamificationStore>((set) => ({
  profile: null,
  loading: false,
  error: null,

  fetchProfile: async () => {
    set({ loading: true, error: null });
    try {
      const data = await getGamificationProfile();
      set((state) => ({
        profile: {
          ...(state.profile ?? DEFAULT_PROFILE),
          ...data,
          // Explicitly preserve career values if server returned undefined/null for any reason
          totalAssignedEngrams: data.totalAssignedEngrams ?? state.profile?.totalAssignedEngrams ?? 0,
          totalAssignedCourses: data.totalAssignedCourses ?? state.profile?.totalAssignedCourses ?? 0,
          completedCourses: data.completedCourses ?? state.profile?.completedCourses ?? 0,
          coursesReady: data.coursesReady ?? state.profile?.coursesReady ?? 0,
          coursesPendingGeneration: data.coursesPendingGeneration ?? state.profile?.coursesPendingGeneration ?? 0,
        },
        loading: false,
      }));
    } catch (err) {
      console.warn('Could not load gamification profile, using fallback:', err);
      set((state) => ({
        profile: state.profile ?? DEFAULT_PROFILE,
        loading: false,
        error: String(err),
      }));
    }
  },

  updateProfileFromReward: (newTotalPoints, newLevel, newLevelName, streakDays) => {
    set((state) => {
      const current = state.profile ?? DEFAULT_PROFILE;
      let pointsToNext = 0;
      let nextName: string | null = null;

      if (newTotalPoints < 500) {
        pointsToNext = 500 - newTotalPoints;
        nextName = 'Sinapsis';
      } else if (newTotalPoints < 1500) {
        pointsToNext = 1500 - newTotalPoints;
        nextName = 'Circuito';
      } else if (newTotalPoints < 3500) {
        pointsToNext = 3500 - newTotalPoints;
        nextName = 'Red Neuronal';
      } else if (newTotalPoints < 7000) {
        pointsToNext = 7000 - newTotalPoints;
        nextName = 'Mente Maestra';
      } else {
        pointsToNext = 0;
        nextName = null;
      }

      return {
        profile: {
          ...current,
          totalPoints: newTotalPoints,
          currentLevel: newLevel,
          currentLevelName: newLevelName,
          currentStreakDays: streakDays !== undefined ? streakDays : current.currentStreakDays,
          pointsToNextLevel: pointsToNext,
          nextLevelName: nextName,
          totalEngramsMastered: (current.totalEngramsMastered ?? 0) + 1,
          // Career plan fields are strictly preserved
          totalAssignedEngrams: current.totalAssignedEngrams ?? 0,
          totalAssignedCourses: current.totalAssignedCourses ?? 0,
          completedCourses: current.completedCourses ?? 0,
          coursesReady: current.coursesReady ?? 0,
          coursesPendingGeneration: current.coursesPendingGeneration ?? 0,
        },
      };
    });

    // Asynchronously refresh full profile from server in background to sync authoritative state
    void getGamificationProfile()
      .then((fresh) => {
        if (fresh) {
          set((state) => ({
            profile: {
              ...(state.profile ?? DEFAULT_PROFILE),
              ...fresh,
              totalAssignedEngrams: fresh.totalAssignedEngrams ?? state.profile?.totalAssignedEngrams ?? 0,
              totalAssignedCourses: fresh.totalAssignedCourses ?? state.profile?.totalAssignedCourses ?? 0,
              completedCourses: fresh.completedCourses ?? state.profile?.completedCourses ?? 0,
              coursesReady: fresh.coursesReady ?? state.profile?.coursesReady ?? 0,
              coursesPendingGeneration: fresh.coursesPendingGeneration ?? state.profile?.coursesPendingGeneration ?? 0,
            },
          }));
        }
      })
      .catch((err) => {
        console.warn('Background profile refresh after reward ignored:', err);
      });
  },
}));
