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

// Check Knowledge Graph entities, connections, and facts for course 75a4d98f-0dcb-499e-a5d4-09faba01a885 (API + MICROSERVICES)
var apiCourseId = Guid.Parse("75a4d98f-0dcb-499e-a5d4-09faba01a885");

var entityCount = await db.StructuredLearningEntities.AsNoTracking().CountAsync();
var docEntityCount = await db.StructuredLearningEntitySections.AsNoTracking()
    .Where(se => se.Section.StructuredLearningDocumentId == apiCourseId)
    .CountAsync();

var connectionCount = await db.StructuredLearningConnections.AsNoTracking()
    .Where(c => c.StructuredLearningDocumentId == apiCourseId)
    .CountAsync();

var factCount = await db.StructuredLearningKnowledgeFacts.AsNoTracking()
    .Where(f => f.Engram.StructuredLearningDocumentId == apiCourseId)
    .CountAsync();

Console.WriteLine($"=== KNOWLEDGE GRAPH AUDIT FOR API + MICROSERVICES ({apiCourseId}) ===");
Console.WriteLine($"Total Master Entities in DB: {entityCount}");
Console.WriteLine($"Section Entity links for this course: {docEntityCount}");
Console.WriteLine($"StructuredLearningConnections for this course: {connectionCount}");
Console.WriteLine($"StructuredLearningKnowledgeFacts for this course: {factCount}");

// Sample entities if any
var sampleEntities = await db.StructuredLearningEntitySections.AsNoTracking()
    .Where(se => se.Section.StructuredLearningDocumentId == apiCourseId)
    .Include(se => se.Entity)
    .Include(se => se.Section)
    .Take(10)
    .Select(se => new { se.Entity.Name, se.Entity.EntityType, se.Section.SubchapterTitle, se.SourceText })
    .ToListAsync();

Console.WriteLine($"Sample Section Entities ({sampleEntities.Count}):");
foreach (var se in sampleEntities)
{
    Console.WriteLine($"  Entity='{se.Name}' ({se.EntityType}) | Section='{se.SubchapterTitle}' | Quote='{se.SourceText}'");
}

// Sample connections if any
var sampleConnections = await db.StructuredLearningConnections.AsNoTracking()
    .Where(c => c.StructuredLearningDocumentId == apiCourseId)
    .Include(c => c.SourceEntity)
    .Include(c => c.TargetEntity)
    .Take(10)
    .Select(c => new { Source = c.SourceEntity.Name, Target = c.TargetEntity.Name, c.RelationshipLabel, c.RelationshipCategory })
    .ToListAsync();

Console.WriteLine($"Sample Connections ({sampleConnections.Count}):");
foreach (var c in sampleConnections)
{
    Console.WriteLine($"  [{c.Source}] --({c.RelationshipLabel} / {c.RelationshipCategory})--> [{c.Target}]");
}

// Sample facts if any
var sampleFacts = await db.StructuredLearningKnowledgeFacts.AsNoTracking()
    .Where(f => f.Engram.StructuredLearningDocumentId == apiCourseId)
    .Take(5)
    .Select(f => new { f.Statement, f.EpistemicStatus, f.Confidence })
    .ToListAsync();

Console.WriteLine($"Sample Facts ({sampleFacts.Count}):");
foreach (var f in sampleFacts)
{
    Console.WriteLine($"  Statement='{f.Statement}' | Status='{f.EpistemicStatus}' | Conf={f.Confidence}");
}

// Check other courses in DB to see if entities/connections exist across DB
var allDocConnections = await db.StructuredLearningConnections.AsNoTracking().CountAsync();
var allDocFacts = await db.StructuredLearningKnowledgeFacts.AsNoTracking().CountAsync();
Console.WriteLine($"DB-Wide: Total Connections={allDocConnections}, Total Facts={allDocFacts}");

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