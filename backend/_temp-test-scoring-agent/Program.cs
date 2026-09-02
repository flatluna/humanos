using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Contracts.EmployeeScoring;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var settingsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "HumanOS", "local.settings.json");
var raw = new ConfigurationBuilder().AddJsonFile(settingsPath, optional: false).Build();
var values = raw.GetSection("Values").GetChildren().ToDictionary(item => item.Key, item => item.Value);
var configuration = new ConfigurationBuilder().AddInMemoryCollection(values!).Build();

var connString = values["HumanOSDatabase"];
var options = new DbContextOptionsBuilder<HumanOsDbContext>().UseSqlServer(connString).Options;
var db = new HumanOsDbContext(options);

var personId = Guid.Parse("562cb73a-414b-47a8-957e-9f1950ae9622");

Console.WriteLine("=== DIAGNOSTIC START ===");

// 1. Check PlayerProfile
var profileRow = await db.PlayerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == personId);
if (profileRow != null)
{
    Console.WriteLine($"PlayerProfile: TotalPoints={profileRow.TotalPoints}, CurrentLevel={profileRow.CurrentLevel}, CurrentLevelName='{profileRow.CurrentLevelName}'");
}
else
{
    Console.WriteLine("PlayerProfile: NULL");
}

// 2. Check PersonCareerPlan
var plan = await db.PersonCareerPlans.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == personId);
if (plan != null)
{
    Console.WriteLine($"PersonCareerPlan found! Id={plan.PersonCareerPlanId}");
    Console.WriteLine($"TechnicalRoadmapJson: {plan.TechnicalRoadmapJson}");
}
else
{
    Console.WriteLine("PersonCareerPlan: NULL");
}

// 3. Check BadgeService breakdown directly
var badgeService = new BadgeService(db);
var breakdown = await badgeService.GetAssignedEngramsBreakdownAsync(personId);
Console.WriteLine($"Breakdown: TotalAssignedEngrams={breakdown.TotalAssignedEngrams}, TotalCourses={breakdown.TotalCourses}, GraphCourses={breakdown.GraphCoursesCount}, StructuredCourses={breakdown.StructuredCoursesCount}, StructuredEngrams={breakdown.StructuredEngramsCount}");

// 4. Check all PersonCareerPlans in DB and count how many steps have MatchedCapabilityId != null
var allCareerPlans = await db.PersonCareerPlans.AsNoTracking().ToListAsync();
Console.WriteLine($"Total PersonCareerPlans in DB: {allCareerPlans.Count}");
foreach (var cp in allCareerPlans)
{
    int totalSteps = 0;
    int matchedSteps = 0;
    void CheckRoadmap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            using var d = JsonDocument.Parse(json);
            if (d.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in d.RootElement.EnumerateArray())
                {
                    totalSteps++;
                    if ((s.TryGetProperty("MatchedCapabilityId", out var cap) && cap.ValueKind == JsonValueKind.String && Guid.TryParse(cap.GetString(), out _)) ||
                        (s.TryGetProperty("MatchedProgramId", out var prog) && prog.ValueKind == JsonValueKind.String && Guid.TryParse(prog.GetString(), out _)))
                    {
                        matchedSteps++;
                    }
                }
            }
        }
        catch { }
    }
    CheckRoadmap(cp.TechnicalRoadmapJson);
    CheckRoadmap(cp.SoftSkillsRoadmapJson);
    CheckRoadmap(cp.DepartmentProcessesRoadmapJson);
    CheckRoadmap(cp.HrProcessesRoadmapJson);

    Console.WriteLine($"Plan Id={cp.PersonCareerPlanId} | PersonId={cp.PersonId} | TotalSteps={totalSteps} | MatchedSteps={matchedSteps}");
}

// Seed badges now
Console.WriteLine("--- SEEDING BADGES ---");
await CatalogSeeder.SeedBadgesAsync(db);

// Print all badges in DB
var badgesInDb = await db.BadgeCatalogs.AsNoTracking().ToListAsync();
Console.WriteLine($"=== BADGES IN DB COUNT: {badgesInDb.Count} ===");
foreach (var b in badgesInDb.Where(x => x.IsActive).OrderBy(x => x.Category).ThenBy(x => x.RuleThreshold))
{
    Console.WriteLine($"Category='{b.Category}' | Code='{b.Code}' | Name='{b.Name}' | Threshold={b.RuleThreshold} | IsActive={b.IsActive}");
}

// Inspect if there are other courses with active sessions/rounds/rewards for this person
var activeCourseIdsFromRounds = await db.StructuredLearningAssessmentRounds
    .AsNoTracking()
    .Where(r => r.PersonId == personId)
    .Join(db.StructuredLearningDocumentSections.AsNoTracking(), r => r.StructuredLearningDocumentSectionId, s => s.StructuredLearningDocumentSectionId, (r, s) => s.StructuredLearningDocumentId)
    .Distinct()
    .ToListAsync();

Console.WriteLine($"CourseIds from AssessmentRounds: {string.Join(", ", activeCourseIdsFromRounds)}");

var activeCourseIdsFromRewards = await db.RewardLedgerEntries
    .AsNoTracking()
    .Where(r => r.PersonId == personId && r.CourseId != null)
    .Select(r => r.CourseId)
    .Distinct()
    .ToListAsync();

Console.WriteLine($"CourseIds from RewardLedgerEntries: {string.Join(", ", activeCourseIdsFromRewards)}");

Console.WriteLine("=== DIAGNOSTIC END ===");
return;