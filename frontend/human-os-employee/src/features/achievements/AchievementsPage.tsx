import { Card } from '@/components/ui/Card';
import { BadgeGallery } from '@/features/gamification/components/BadgeGallery';

/** "Mis Logros" / "My Achievements" — Dedicated view for gamification milestones,
 *  unlocked badges, streaks, and progress toward future achievements.
 */
export function AchievementsPage() {
  return (
    <div className="mx-auto max-w-5xl space-y-6 pb-16">
      <Card className="p-6 sm:p-8">
        <BadgeGallery />
      </Card>
    </div>
  );
}
