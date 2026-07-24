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
}
