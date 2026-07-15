import { GoalsHeader } from "@/components/goals/GoalsHeader";
import { GoalsStats } from "@/components/goals/GoalsStats";
import { GoalCard } from "@/components/goals/GoalCard";
import { AchievedGoals } from "@/components/goals/AchievedGoals";
import {
  mockGoals,
  mockGoalCapabilities,
  mockPersonCapabilitiesForGoals,
} from "@/lib/mock-goals";

export default function GoalsPage() {
  const activeGoals = mockGoals.filter((g) => !g.isAchieved);

  return (
    <div className="space-y-8">
      <GoalsHeader />
      
      <GoalsStats
        goals={mockGoals}
        goalCapabilities={mockGoalCapabilities}
        capabilities={mockPersonCapabilitiesForGoals}
      />

      {/* Active Goals Grid */}
      <div className="grid gap-6">
        {activeGoals.map((goal) => (
          <GoalCard
            key={goal.id}
            goal={goal}
            capabilities={mockPersonCapabilitiesForGoals}
            connectedCapabilityIds={mockGoalCapabilities[goal.id] || []}
          />
        ))}
      </div>

      {/* Achieved Goals Section */}
      <div className="mt-12">
        <AchievedGoals goals={mockGoals} />
      </div>
    </div>
  );
}
