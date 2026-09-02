using HumanOS.Models.Gamification;
using HumanOS.Models.Goals;
using HumanOS.Models.Motivations;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Data;

/// <summary>
/// One-off idempotent catalog seeder for the Growth Plan "Where You Want
/// to Go" step (9 Goals + 7 Motivations, es/en translations matching
/// frontend/human-os-web/src/locales/{es,en}.ts growthPlan.futureDirection.*
/// exactly). Invoked via `dotnet run -- --seed-catalog` (see Program.cs),
/// same convention as RunTest.cs/--run-test. Safe to run multiple times —
/// existing rows (matched by Code) are left untouched.
/// </summary>
public static class CatalogSeeder
{
    private sealed record GoalSeed(
        string Code,
        string Category,
        string NameEn,
        string DescriptionEn,
        string NameEs,
        string DescriptionEs);

    private sealed record MotivationSeed(
        string Code,
        string NameEn,
        string NameEs);

    private static readonly GoalSeed[] Goals =
    [
        new("helpFamily", "CONTRIBUTION",
            "Help my family", "Support my kids or loved ones with school or daily life.",
            "Ayudar a mi familia", "Apoyar a mis hijos u otros seres queridos en su educación o su día a día."),
        new("extraIncome", "VALUE_CREATION",
            "Generate extra income", "Learn something that helps me earn more or start an extra income stream.",
            "Generar un ingreso extra", "Aprender algo que me ayude a ganar más o iniciar un ingreso adicional."),
        new("careerChange", "PROFESSIONAL",
            "Change careers", "Explore a different professional path than the one I'm on now.",
            "Cambiar de carrera", "Explorar un camino profesional distinto al que tengo ahora."),
        new("dailyIndependence", "LIFE",
            "Become more independent", "Handle more things on my own, without depending on others.",
            "Ser más independiente", "Resolver más cosas por mi cuenta, sin depender de otros."),
        new("loveOfLearning", "PERSONAL_GROWTH",
            "Learn just for the love of it", "No specific goal - I just enjoy continuing to learn.",
            "Aprender por el simple gusto de hacerlo", "Sin una meta específica — solo me encanta seguir aprendiendo."),
        new("personalChallenge", "PERSONAL_GROWTH",
            "Take on a personal challenge", "Prepare for something important I have in mind.",
            "Superar un reto personal", "Prepararme para algo importante que tengo en mente."),
        new("wellbeing", "LIFE",
            "Improve my wellbeing", "Build better habits and take better care of myself.",
            "Mejorar mi bienestar", "Desarrollar mejores hábitos y cuidar más de mí mismo(a)."),
        new("ownProject", "VALUE_CREATION",
            "Start my own project", "Build something of my own - a business, an idea, a personal project.",
            "Iniciar mi propio proyecto", "Construir algo propio — un negocio, una idea, un proyecto personal."),
        new("reclaimFocus", "CAPABILITY_DEVELOPMENT",
            "Counteract social media's effects", "Reclaim my focus, memory, and ability to learn deeply.",
            "Contrarrestar el efecto de las redes sociales", "Recuperar mi enfoque, mi memoria y mi capacidad de aprender profundamente."),
    ];

    private static readonly MotivationSeed[] Motivations =
    [
        new("growth", "Personal growth", "Crecimiento personal"),
        new("helpingOthers", "Helping others", "Ayudar a otros"),
        new("independence", "Independence", "Independencia"),
        new("curiosity", "Curiosity", "Curiosidad"),
        new("security", "Stability", "Estabilidad"),
        new("creativity", "Creativity", "Creatividad"),
        new("pride", "Feeling proud of myself", "Sentirme orgulloso(a) de mí"),
    ];

    public static async Task SeedGoalsAndMotivationsAsync(HumanOsDbContext dbContext)
    {
        var now = DateTime.UtcNow;

        var existingGoalCodes = await dbContext.Goals
            .Select(g => g.Code)
            .ToListAsync();

        foreach (var seed in Goals)
        {
            if (existingGoalCodes.Contains(seed.Code))
            {
                Console.WriteLine($"Skip Goal '{seed.Code}' — already exists.");
                continue;
            }

            var goal = new Goal
            {
                GoalId = Guid.NewGuid(),
                Code = seed.Code,
                Name = seed.NameEn,
                Description = seed.DescriptionEn,
                Category = seed.Category,
                IsActive = true,
                CreatedDate = now,
                UpdatedDate = now,
            };

            dbContext.Goals.Add(goal);

            dbContext.GoalTranslations.Add(new GoalTranslation
            {
                GoalId = goal.GoalId,
                LanguageCode = "en",
                Name = seed.NameEn,
                Description = seed.DescriptionEn,
                CreatedDate = now,
                UpdatedDate = now,
            });

            dbContext.GoalTranslations.Add(new GoalTranslation
            {
                GoalId = goal.GoalId,
                LanguageCode = "es",
                Name = seed.NameEs,
                Description = seed.DescriptionEs,
                CreatedDate = now,
                UpdatedDate = now,
            });

            Console.WriteLine($"Seeded Goal '{seed.Code}'.");
        }

        var existingMotivationCodes = await dbContext.Motivations
            .Select(m => m.Code)
            .ToListAsync();

        foreach (var seed in Motivations)
        {
            if (existingMotivationCodes.Contains(seed.Code))
            {
                Console.WriteLine($"Skip Motivation '{seed.Code}' — already exists.");
                continue;
            }

            var motivation = new Motivation
            {
                MotivationId = Guid.NewGuid(),
                Code = seed.Code,
                Name = seed.NameEn,
                IsActive = true,
                CreatedDate = now,
                UpdatedDate = now,
            };

            dbContext.Motivations.Add(motivation);

            dbContext.MotivationTranslations.Add(new MotivationTranslation
            {
                MotivationId = motivation.MotivationId,
                LanguageCode = "en",
                Name = seed.NameEn,
                CreatedDate = now,
                UpdatedDate = now,
            });

            dbContext.MotivationTranslations.Add(new MotivationTranslation
            {
                MotivationId = motivation.MotivationId,
                LanguageCode = "es",
                Name = seed.NameEs,
                CreatedDate = now,
                UpdatedDate = now,
            });

            Console.WriteLine($"Seeded Motivation '{seed.Code}'.");
        }

        await dbContext.SaveChangesAsync();

        Console.WriteLine("Catalog seeding complete.");
    }

    public sealed record BadgeSeed(
        string Code,
        string Category,
        string Name,
        string Description,
        string IconEmoji,
        string RuleType,
        int RuleThreshold);

    public static readonly BadgeSeed[] InitialBadges =
    [
        // CATEGORY A — EFFORT MILESTONES (cumulative engrams mastered, any course)
        new("first_neuron", "Effort", "First Neuron", "You've mastered your first engram! The journey of knowledge has begun.", "🎯", "engrams_mastered", 1),
        new("effort_10", "Effort", "Apprentice", "You've mastered 10 engrams — laying down strong foundational neural pathways.", "🌱", "engrams_mastered", 10),
        new("effort_25", "Effort", "Practitioner", "You've mastered 25 engrams — your knowledge network is growing strong.", "📘", "engrams_mastered", 25),
        new("effort_50", "Effort", "Expert", "You've mastered 50 engrams — turning continuous effort into deep expertise.", "🎓", "engrams_mastered", 50),
        new("effort_100", "Effort", "Master", "You've mastered 100 engrams — a century of consolidated knowledge.", "🏅", "engrams_mastered", 100),
        new("effort_200", "Effort", "Scholar", "You've mastered 200 engrams — demonstrating exceptional dedication to learning.", "💎", "engrams_mastered", 200),
        new("effort_300", "Effort", "Legend", "You've mastered 300 engrams — an iconic milestone of profound mastery.", "👑", "engrams_mastered", 300),

        // CATEGORY B — COURSE / SKILL PROGRESS (% of a single course)
        new("skill_progress_25", "Progress", "Quarter Way", "You've completed 25% of a course — momentum is building.", "🥉", "course_progress_pct", 25),
        new("skill_progress_50", "Progress", "Halfway There", "You've completed 50% of a course — halfway to complete mastery.", "🥈", "course_progress_pct", 50),
        new("skill_progress_75", "Progress", "Home Stretch", "You've completed 75% of a course — the finish line is in sight.", "🥇", "course_progress_pct", 75),
        new("skill_complete_1", "Progress", "Skill Mastered", "You've completed 100% of a course — a complete skill fully mastered.", "🛡️", "course_completed", 1),

        // CATEGORY C — SKILLS ACCUMULATED (number of fully completed courses/skills)
        new("skills_1", "Skills", "First Skill", "You've fully completed your first entire skill course.", "🏆", "skills_completed", 1),
        new("skills_3", "Skills", "Getting Started", "You've fully completed 3 distinct skill courses across your training plan.", "🌱", "skills_completed", 3),
        new("skills_6", "Skills", "Building Momentum", "You've fully completed 6 distinct skill courses — versatile and capable.", "🚀", "skills_completed", 6),
        new("skills_10", "Skills", "Halfway Expert", "You've fully completed 10 distinct skill courses — deep multi-domain competence.", "🥈", "skills_completed", 10),
        new("skills_15", "Skills", "Almost There", "You've fully completed 15 distinct skill courses — exceptional breadth of knowledge.", "🥇", "skills_completed", 15),
        new("skills_all", "Skills", "Skill Master", "You've completed all assigned skill courses in your learning plan.", "🥷", "skills_completed", 20),

        // CATEGORY D — MASTERY / QUALITY
        new("quality_perfect_10", "Quality", "Perfectionist", "Scored a perfect 100% on 10 assessment rounds.", "🎯", "perfect_scores", 10),
        new("quality_firsttry_10", "Quality", "One-Shot", "Mastered 10 engrams on the very first attempt without needing a retry.", "🧠", "first_try_masteries", 10),
        new("quality_flawless_5", "Quality", "Flawless", "Mastered 5 consecutive engrams on the first attempt with high scores.", "🔥", "flawless_streak", 5),

        // CATEGORY E — CHALLENGE (uses Critical tier)
        new("critical_guardian", "Challenge", "Critical Guardian", "Mastered 10 Critical tier engrams essential for your role.", "🛡️", "critical_mastered", 10),

        // CATEGORY F — HABIT / CONSISTENCY
        new("streak_7", "Habit", "7-Day Streak", "Maintained a 7-day daily learning streak.", "🔥", "streak_days", 7),
        new("streak_30", "Habit", "30-Day Streak", "Maintained a 30-day daily learning streak — a rock-solid habit.", "🌟", "streak_days", 30),
        new("streak_100", "Habit", "100-Day Streak", "Maintained a 100-day daily learning streak — truly remarkable consistency.", "💫", "streak_days", 100),
        new("automatic_pilot", "Habit", "Autopilot", "Consolidated 5 engrams into permanent, automatic memory.", "⚡", "automatic_engrams", 5),
        new("memory_rescuer", "Habit", "Memory Rescuer", "Rescued 10 fading engrams with heroic active memory recall.", "🧠", "memory_rescued", 10),

        // CATEGORY G — LUCK / SURPRISE
        new("lucky_jackpot", "Luck", "Lucky", "Hit a rare 3.0x Jackpot reward multiplier.", "🎲", "jackpot_hit", 1),
        new("lucky_triple", "Luck", "Triple Luck", "Hit 3 Jackpot reward multipliers — fortune favors the dedicated.", "🎰", "jackpot_hit", 3),
    ];

    public static async Task SeedBadgesAsync(HumanOsDbContext dbContext)
    {
        var existingBadges = await dbContext.BadgeCatalogs.ToListAsync();
        var existingLookup = existingBadges.ToDictionary(b => b.Code, StringComparer.OrdinalIgnoreCase);
        var activeCodesInSeed = InitialBadges.Select(b => b.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedCount = 0;
        var updatedCount = 0;
        var deactivatedCount = 0;

        foreach (var seed in InitialBadges)
        {
            if (existingLookup.TryGetValue(seed.Code, out var existing))
            {
                existing.Category = seed.Category;
                existing.Name = seed.Name;
                existing.Description = seed.Description;
                existing.IconEmoji = seed.IconEmoji;
                existing.RuleType = seed.RuleType;
                existing.RuleThreshold = seed.RuleThreshold;
                existing.IsActive = true;
                updatedCount++;
            }
            else
            {
                var badge = new BadgeCatalog
                {
                    Id = Guid.NewGuid(),
                    Code = seed.Code,
                    Category = seed.Category,
                    Name = seed.Name,
                    Description = seed.Description,
                    IconEmoji = seed.IconEmoji,
                    RuleType = seed.RuleType,
                    RuleThreshold = seed.RuleThreshold,
                    IsActive = true
                };

                dbContext.BadgeCatalogs.Add(badge);
                addedCount++;
            }
        }

        // Deactivate any existing badges that are no longer part of the redesigned catalog (e.g. complete_brain)
        foreach (var existing in existingBadges)
        {
            if (!activeCodesInSeed.Contains(existing.Code) && existing.IsActive)
            {
                existing.IsActive = false;
                deactivatedCount++;
            }
        }

        if (addedCount > 0 || updatedCount > 0 || deactivatedCount > 0)
        {
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Seeded {addedCount} new badges, updated {updatedCount} badges, deactivated {deactivatedCount} obsolete badges in BadgeCatalog.");
        }
        else
        {
            Console.WriteLine("All badges are already present and up to date in BadgeCatalog.");
        }
    }
}
