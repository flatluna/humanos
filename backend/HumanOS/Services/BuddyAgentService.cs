using System.ComponentModel;
using System.Text.Json;
using Azure.Identity;
using HumanOS.Data;
using HumanOS.Models.Learning;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Azure.AI.OpenAI;

namespace HumanOS.Services;

public sealed class BuddyAgentService
{
    private readonly HumanOsDbContext _db;
    private readonly CapabilityEnrollmentService _enrollments;
    private readonly StructuredLearningDocumentEnrollmentService _structuredLearningEnrollments;
    private readonly BadgeService _badges;
    private readonly PerplexitySearchService _web;
    private readonly IChatClient? _chatClient;

    public BuddyAgentService(
        HumanOsDbContext db,
        CapabilityEnrollmentService enrollments,
        StructuredLearningDocumentEnrollmentService structuredLearningEnrollments,
        BadgeService badges,
        PerplexitySearchService web,
        IConfiguration configuration)
    {
        _db = db;
        _enrollments = enrollments;
        _structuredLearningEnrollments = structuredLearningEnrollments;
        _badges = badges;
        _web = web;

        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deployment = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(deployment))
        {
            var client = string.IsNullOrWhiteSpace(apiKey)
                ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
                : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));
            _chatClient = client.GetChatClient(deployment).AsIChatClient();
        }
    }

    public bool IsConfigured => _chatClient is not null;

    /// <summary>One skill = one short instruction + the exact tool subset it
    /// needs. Only the selected skill's tools are ever registered with the
    /// model for a given turn — this is what actually keeps token usage low
    /// (tool schemas + instructions are the expensive, repeated-every-turn
    /// part of the prompt, not the per-call tool RESULTS, which only flow
    /// when a tool is actually invoked).</summary>
    private sealed record BuddySkill(string Id, string Instructions, string[] ToolNames);

    private static readonly BuddySkill[] SkillCatalog =
    [
        new("profile", "Answer using get_my_profile. Never invent department/role/manager facts.",
            ["get_my_profile"]),
        new("learning_progress", "Answer using these tools. For recommendations, cross-check role-required courses against enrolled progress and Engram scores before suggesting anything; never claim a course exists unless a tool confirms it.",
            ["get_my_learning_progress", "get_my_engram_scores", "get_courses_for_my_role"]),
        new("objectives", "Answer using get_my_objectives.",
            ["get_my_objectives"]),
        new("achievements", "Answer using get_my_achievements.",
            ["get_my_achievements"]),
        new("career_plan", "Answer using get_my_career_plan.",
            ["get_my_career_plan"]),
        new("projects", "Answer using get_my_projects.",
            ["get_my_projects"]),
        new("external_content", "Use recommend_external_content. Always disclose results are external/unverified, outside our own catalog.",
            ["recommend_external_content"]),
        new("general", "General company-logistics/support question. Use get_my_profile only if personal context is needed; use search_the_web for public facts.",
            ["get_my_profile"]),
    ];

    public async Task<string> AskAsync(
        Guid personId,
        string message,
        IReadOnlyList<BuddyChatHistoryEntry> history,
        string languageCode,
        CancellationToken cancellationToken)
    {
        if (_chatClient is null)
        {
            throw new InvalidOperationException("Buddy chat is not configured.");
        }

        var skill = SelectSkill(message);

        // Dynamic dispatch (2026-10-04): only the tool NAMES this skill
        // declared are ever instantiated/sent to the model — every other
        // tool's schema/description costs zero tokens this turn. search_the_web
        // rides along as a universal cheap fallback (one tiny tool) except for
        // "external_content", which already owns a richer web tool.
        var toolNames = skill.ToolNames.AsEnumerable();
        if (_web.IsConfigured && skill.Id != "external_content")
        {
            toolNames = toolNames.Append("search_the_web");
        }

        var tools = toolNames
            .Where(name => name != "search_the_web" && name != "recommend_external_content" || _web.IsConfigured)
            .Select(name => BuildTool(name, personId, cancellationToken))
            .ToList();

        var agent = _chatClient.AsAIAgent(
            name: "Buddy",
            instructions: $"""
                You are Buddy, the employee's warm and concise workplace companion.
                Skill: {skill.Id}. {skill.Instructions}
                Only state facts a tool actually returned for THIS employee. Reply in
                the same language as the latest user message.
                """,
            tools: tools);

        var prompt = BuildPrompt(message, history);
        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);

        // Real SQL-backed text-channel usage (2026-10-04) — mirrors
        // BuddySessionUsage's voice-channel tracking so Buddy's text cost is
        // no longer invisible. Best-effort/fire-and-forget: a failure here
        // must never break the employee's actual answer.
        try
        {
            var usage = response.Usage;
            if (usage is not null)
            {
                _db.BuddyChatUsages.Add(new Models.People.BuddyChatUsage
                {
                    PersonId = personId,
                    InputTokens = (int)(usage.InputTokenCount ?? 0),
                    OutputTokens = (int)(usage.OutputTokenCount ?? 0),
                });
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            // Never let usage-tracking failures break the employee's answer.
        }

        return response.Text ?? "I could not produce an answer.";
    }

    /// <summary>Cheap, zero-token local keyword routing — picks WHICH skill (and
    /// therefore which tiny tool subset) gets sent to the model this turn,
    /// instead of always shipping every tool's schema on every call.</summary>
    private static BuddySkill SelectSkill(string message)
    {
        var text = message.ToLowerInvariant();
        string id =
            text.Contains("proyecto") || text.Contains("project") ? "projects" :
            text.Contains("badge") || text.Contains("medalla") || text.Contains("logro") || text.Contains("nivel") || text.Contains("racha") || text.Contains("streak") || text.Contains("puntos") || text.Contains("points") ? "achievements" :
            text.Contains("plan de carrera") || text.Contains("career plan") || text.Contains("brecha") || text.Contains("skill gap") ? "career_plan" :
            text.Contains("objetivo") || text.Contains("objective") || text.Contains("goal") || text.Contains("meta") ? "objectives" :
            text.Contains("externo") || text.Contains("external") || text.Contains("tendenc") || text.Contains("trend") ? "external_content" :
            text.Contains("curso") || text.Contains("recom") || text.Contains("engram") || text.Contains("score") || text.Contains("puntaje") ? "learning_progress" :
            text.Contains("jefe") || text.Contains("manager") || text.Contains("departamento") || text.Contains("rol") ? "profile" :
            "general";

        return SkillCatalog.First(s => s.Id == id);
    }

    /// <summary>Builds exactly ONE tool by name, lazily, closing over this
    /// call's personId/cancellationToken. Every handler below queries the
    /// database live for the authenticated employee — nothing here is
    /// pre-fetched or hardcoded into the prompt.</summary>
    private AITool BuildTool(string name, Guid personId, CancellationToken cancellationToken) => name switch
    {
        "get_my_profile" => AIFunctionFactory.Create(
            () => GetMyProfileAsync(personId, cancellationToken),
            "get_my_profile",
            "Employee's verified profile: department, role, manager."),
        "get_my_learning_progress" => AIFunctionFactory.Create(
            () => GetMyLearningProgressAsync(personId, cancellationToken),
            "get_my_learning_progress",
            "TOTAL enrolled courses (both legacy Capability Graph and Structured Learning pipelines) with status, mastered Engrams and score. Use for 'what courses am I enrolled in/started' questions."),
        "get_my_engram_scores" => AIFunctionFactory.Create(
            () => GetMyEngramScoresAsync(personId, cancellationToken),
            "get_my_engram_scores",
            "Total Engrams mastered (all pipelines) plus each Engram's score/course. Use for 'how many Engrams' questions."),
        "get_courses_for_my_role" => AIFunctionFactory.Create(
            () => GetRoleCoursesAsync(personId, cancellationToken),
            "get_courses_for_my_role",
            "Courses tied to the employee's role skills; marks catalog vs missing."),
        "get_my_objectives" => AIFunctionFactory.Create(
            () => GetMyObjectivesAsync(personId, cancellationToken),
            "get_my_objectives",
            "Active SMART objectives/goals and their progress."),
        "get_my_achievements" => AIFunctionFactory.Create(
            () => GetMyAchievementsAsync(personId, cancellationToken),
            "get_my_achievements",
            "Points, level, streaks, earned and closest-to-unlock badges."),
        "get_my_career_plan" => AIFunctionFactory.Create(
            () => GetMyCareerPlanAsync(personId, cancellationToken),
            "get_my_career_plan",
            "Latest AI career plan: skill gaps and training roadmap."),
        "get_my_projects" => AIFunctionFactory.Create(
            () => GetMyProjectsAsync(personId, cancellationToken),
            "get_my_projects",
            "Assigned hands-on practice projects and their status."),
        "search_the_web" => AIFunctionFactory.Create(
            async ([Description("Search query.")] string query) =>
                JsonSerializer.Serialize(await _web.SearchAsync(query, cancellationToken)),
            "search_the_web",
            "Searches current public info. Never for private company facts."),
        "recommend_external_content" => AIFunctionFactory.Create(
            async ([Description("Topic/interest the employee wants external content on.")] string topic) =>
                JsonSerializer.Serialize(await _web.SearchAsync($"Latest courses, papers, videos, or communities about: {topic}", cancellationToken)),
            "recommend_external_content",
            "External learning content outside our catalog; disclose as external/unverified."),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown Buddy tool name.")
    };

    private static string BuildPrompt(string message, IReadOnlyList<BuddyChatHistoryEntry> history)
    {
        var prior = history.TakeLast(12)
            .Select(item => $"{item.Role}: {item.Text}");
        return $"Conversation:\n{string.Join('\n', prior)}\nUser: {message}";
    }

    private async Task<string> GetMyProfileAsync(Guid personId, CancellationToken cancellationToken)
    {
        var person = await _db.People.AsNoTracking()
            .Include(p => p.Department)
            .Include(p => p.JobRole)
            .Include(p => p.Manager)
            .SingleOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        if (person is null) return "No verified profile was found.";

        var managerName = person.ManagerId.HasValue
            ? await _db.PersonProfiles.AsNoTracking()
                .Where(p => p.PersonId == person.ManagerId.Value)
                .Select(p => p.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        return JsonSerializer.Serialize(new
        {
            name = await _db.PersonProfiles.AsNoTracking().Where(p => p.PersonId == personId)
                .Select(p => p.DisplayName).FirstOrDefaultAsync(cancellationToken),
            person.Email,
            department = person.Department?.Name,
            role = person.JobRole?.Title,
            manager = managerName ?? person.Manager?.Email
        });
    }

    /// <summary>Enrolled courses come from TWO separate pipelines — same dual
    /// source the "Available Courses" page's own "Started only" filter reads
    /// (see AvailableCoursesPage.tsx/enrolledStructuredLearningApi.ts): the
    /// OLD CapabilityGraph pipeline (CapabilityEnrollment) and the Structured
    /// Learning pipeline (StructuredLearningDocumentEnrollment, or implicitly
    /// "started" via any passed Assessment round/reward). A course enrolled
    /// through Structured Learning (e.g. "Prompt Engineering", "IVA Basic
    /// Intro") never produces a CapabilityEnrollment row — querying only that
    /// table is why Buddy previously claimed the employee had no courses.</summary>
    private async Task<string> GetMyLearningProgressAsync(Guid personId, CancellationToken cancellationToken)
    {
        var graphCourses = await _enrollments.GetEnrolledCapabilitiesAsync(personId, cancellationToken);
        var structuredLearningCourses = await _structuredLearningEnrollments.GetEnrolledDocumentsAsync(personId, cancellationToken);

        var combined = graphCourses
            .Select(c => new
            {
                c.Name,
                c.Status,
                totalEngrams = c.TotalNodes,
                masteredEngrams = c.MasteredNodes,
                c.AverageScore
            })
            .Concat(structuredLearningCourses.Select(d => new
            {
                Name = d.DocumentName,
                Status = d.CompletedSectionCount >= d.SectionCount && d.SectionCount > 0 ? "Completed" : "InProgress",
                totalEngrams = d.SectionCount,
                masteredEngrams = d.CompletedSectionCount,
                AverageScore = (int?)null
            }))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            totalEnrolledCourses = combined.Count,
            courses = combined
        });
    }

    /// <summary>Mastered Engrams come from TWO separate pipelines — same
    /// dual-source join the /brain page itself does (see
    /// useKnowledgeNetwork.ts/useBrainGraph.ts): the OLD CapabilityGraph
    /// pipeline (LearningSessionNode+LearningAssessmentResult) and the
    /// Structured Learning pipeline (StructuredLearningAssessmentRound,
    /// Status == Passed, keyed by PersonId directly, no session FK). A
    /// course built with the newer Structured Learning path (e.g. "Prompt
    /// Engineering", "IVA Basic Intro") ONLY ever produces rows in the
    /// second source — querying just LearningSessionNodes would silently
    /// undercount or show zero Engrams for those courses.</summary>
    private async Task<string> GetMyEngramScoresAsync(Guid personId, CancellationToken cancellationToken)
    {
        var graphRows = await _db.LearningSessionNodes.AsNoTracking()
            .Where(n => n.LearningSession!.PersonId == personId)
            .SelectMany(n => n.AssessmentResults.Select(a => new
            {
                Engram = n.CapabilityGraphNode!.Name,
                a.Score,
                a.Passed,
                a.CreatedDate,
                Course = n.LearningSession!.Capability!.Name
            }))
            .ToListAsync(cancellationToken);

        var structuredLearningRows = await _db.StructuredLearningAssessmentRounds.AsNoTracking()
            .Where(r => r.PersonId == personId && r.Status == Models.Capabilities.Studio.StructuredLearningAssessmentRoundStatus.Passed)
            .Select(r => new
            {
                Engram = r.Section.SubchapterTitle,
                Score = r.FinalScore ?? 0,
                Passed = true,
                CreatedDate = r.CompletedDate ?? r.CreatedDate,
                Course = r.Section.Document.DocumentName
            })
            .ToListAsync(cancellationToken);

        var combined = graphRows.Concat(structuredLearningRows)
            .OrderByDescending(x => x.CreatedDate)
            .Take(200)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            totalEngramsMastered = combined.Count,
            engrams = combined
        });
    }

    private async Task<string> GetRoleCoursesAsync(Guid personId, CancellationToken cancellationToken)
    {
        var roleId = await _db.People.AsNoTracking()
            .Where(p => p.PersonId == personId)
            .Select(p => p.JobRoleId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!roleId.HasValue) return "No job role is assigned.";

        var skills = await _db.JobRoleSkills.AsNoTracking()
            .Where(x => x.JobRoleId == roleId.Value)
            .Select(x => x.Skill.Name)
            .ToListAsync(cancellationToken);
        var courses = await _db.Capabilities.AsNoTracking()
            .Where(c => c.IsActive && c.TargetSkill != null && skills.Contains(c.TargetSkill))
            .Select(c => new { c.Name, c.TargetSkill, c.Description })
            .ToListAsync(cancellationToken);
        var documents = await _db.StructuredLearningDocuments.AsNoTracking()
            .Where(d => d.IsPublished && d.TargetSkill != null && skills.Contains(d.TargetSkill))
            .Select(d => new { d.DocumentName, d.TargetSkill, d.ExecutiveSummary })
            .ToListAsync(cancellationToken);
        return JsonSerializer.Serialize(new { requiredSkills = skills, catalogCourses = courses, structuredLearningCourses = documents });
    }

    private async Task<string> GetMyObjectivesAsync(Guid personId, CancellationToken cancellationToken)
    {
        var goals = await _db.PersonGoals.AsNoTracking()
            .Include(pg => pg.Goal)
            .Where(pg => pg.PersonId == personId)
            .OrderByDescending(pg => pg.StartedDate)
            .Select(pg => new
            {
                Title = pg.Goal!.Name,
                pg.Goal!.Description,
                Category = pg.Goal!.Category,
                pg.Status,
                pg.ProgressPercentage,
                pg.StartedDate,
                pg.TargetDate,
                pg.CompletedDate
            })
            .ToListAsync(cancellationToken);
        if (goals.Count == 0) return "No SMART objectives/goals are on file for this employee yet.";
        return JsonSerializer.Serialize(goals);
    }

    private async Task<string> GetMyAchievementsAsync(Guid personId, CancellationToken cancellationToken)
    {
        var player = await _db.PlayerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(pp => pp.PersonId == personId, cancellationToken);
        var totalPoints = player?.TotalPoints ?? 0;
        var (pointsToNextLevel, nextLevelName) = AzureFunctions.Api.GamificationFunction.CalculateNextLevelProgress(totalPoints);

        var badges = await _badges.GetBadgeProgressAsync(personId, cancellationToken);
        var earned = badges.Where(b => b.IsEarned).Select(b => new { b.Name, b.Category, earnedAt = b.EarnedAt });
        var closestLocked = badges.Where(b => !b.IsEarned).OrderByDescending(b => b.ProgressPercent).Take(5)
            .Select(b => new { b.Name, b.ProgressPercent, b.CurrentValue, b.TargetValue });

        return JsonSerializer.Serialize(new
        {
            totalPoints,
            currentLevel = player?.CurrentLevel ?? 1,
            currentLevelName = player?.CurrentLevelName ?? "Neurona",
            pointsToNextLevel,
            nextLevelName,
            currentStreakDays = player?.CurrentStreakDays ?? 0,
            longestStreakDays = player?.LongestStreakDays ?? 0,
            earnedBadges = earned,
            closestLockedBadges = closestLocked
        });
    }

    private async Task<string> GetMyCareerPlanAsync(Guid personId, CancellationToken cancellationToken)
    {
        var plan = await _db.PersonCareerPlans.AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.PersonId == personId, cancellationToken);
        if (plan is null) return "No career development plan has been generated for this employee yet.";

        return JsonSerializer.Serialize(new
        {
            gapSkills = DeserializeStringList(plan.GapSkillsJson),
            technicalRoadmap = plan.TechnicalRoadmapJson,
            softSkillsRoadmap = plan.SoftSkillsRoadmapJson,
            departmentProcessesRoadmap = plan.DepartmentProcessesRoadmapJson,
            hrProcessesRoadmap = plan.HrProcessesRoadmapJson,
            plan.OverallRationale,
            plan.IsAccepted,
            plan.GeneratedDate
        });
    }

    private async Task<string> GetMyProjectsAsync(Guid personId, CancellationToken cancellationToken)
    {
        var projects = await _db.PersonProjects.AsNoTracking()
            .Include(pp => pp.Project)
            .Where(pp => pp.PersonId == personId)
            .OrderByDescending(pp => pp.UpdatedDate)
            .Select(pp => new
            {
                Name = pp.Project!.Name,
                pp.Project!.Description,
                pp.Project!.DifficultyLevel,
                pp.Project!.EstimatedHours,
                pp.Status,
                pp.ProgressPercentage,
                pp.StartedDate,
                pp.CompletedDate
            })
            .ToListAsync(cancellationToken);
        if (projects.Count == 0) return "No hands-on practice projects are assigned to this employee yet.";
        return JsonSerializer.Serialize(projects);
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

public sealed record BuddyChatHistoryEntry(string Role, string Text);
