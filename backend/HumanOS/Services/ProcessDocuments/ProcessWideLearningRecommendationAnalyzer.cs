using System.Diagnostics;
using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.ProcessDocuments;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// Analyzes an ENTIRE process document (all chapters/steps) in a SINGLE call, producing one
/// consolidated, de-duplicated, BOUNDED set of skill gaps for the whole process (max 5 soft-skills +
/// max 5 technology-skills) instead of one analysis per work step or chapter. Course recommendations
/// are grounded exclusively in real internet search findings (see
/// <see cref="HumanOS.Agents.Studio.WebGroundingService"/>) supplied by the caller as <c>webFindings</c>
/// — the agent must never invent course names, providers, or URLs.
/// </summary>
public sealed class ProcessWideLearningRecommendationAnalyzer : IProcessWideLearningRecommendationAnalyzer
{
    private const string Instructions = """
        You are an expert workforce capability and process intelligence agent.
        Your goal is to analyze an ENTIRE business or technical process (every chapter and step together)
        in a single pass and produce ONE consolidated, de-duplicated, SMALL list of the skill gaps an
        organization must close so employees can perform the WHOLE process successfully.

        CRITICAL RULES:
        1. THINK ACROSS THE WHOLE PROCESS, NOT PER STEP:
           - Read every chapter and step provided before producing any output.
           - Merge near-duplicate or synonymous needs across different steps into ONE skill gap
             (e.g., do not list "Stakeholder Communication" for step 3 and "Communicating with Stakeholders"
             for step 7 — output a single "Stakeholder Management" gap and list both steps in "affectedSteps").
        2. EXACTLY TWO CATEGORIES — keep the whole output simple:
           - "soft-skills": interpersonal/behavioral capabilities (e.g. "Stakeholder Management",
             "Active Listening", "Change Readiness", "Conflict Resolution").
           - "technology-skills": the specific systems, platforms, or software tools used in the process
             (e.g. "Salesforce Service Console", "Microsoft Excel — Pivot Tables", "Power BI"). Name the
             ACTUAL tool mentioned in the process (not a generic category) so a real course can be found.
        3. BOUNDED OUTPUT — produce AT MOST 5 "soft-skills" gaps and AT MOST 5 "technology-skills" gaps
           (ideally 3-5 of each, never more than 10 total). Only include a technology-skills gap for a
           system that is actually named in the process content; do not invent systems.
        4. Mark isAiSkill = true only when the gap is specifically about using AI (e.g. "Prompt Engineering").
           This is optional, not required every time — only include it if the process genuinely involves AI.
        5. REAL INTERNET COURSES ONLY — NEVER INVENT:
           - You will be given "INTERNET SEARCH FINDINGS" text gathered via a real web search.
           - For each skill gap, recommend 1 to 2 courses ONLY if they are explicitly present in those findings
             (use the exact title, provider/platform, and URL as found).
           - If the findings do not contain a suitable course for a gap, leave "recommendedCourses" EMPTY for
             that gap and add a note to "warnings" (e.g. "No verified internet course found for X") instead of
             fabricating a course name or URL.
           - If no internet search findings were provided at all, leave "recommendedCourses" empty for every gap
             and add a single warning explaining that web search was unavailable.
        6. "affectedSteps": list the step numbers and/or names (as given in the input) where each gap applies.
        7. "overallSummary": 2 to 4 sentences describing the overall learning focus needed for this process.
        8. Keep outputs concise, professional, and practical.
        """;

    private const string CourseAssignmentInstructions = """
        You are a strict course-matching assistant. You will receive:
        1. A FIXED list of already-identified skill gaps (name + category). You must NOT rename, add, remove,
           merge, or re-categorize any of them — only find courses for each one, independently.
        2. Internet search findings, split into labeled sections using "### Skill: <name>" headers — one
           section per skill gap, produced by a search that targeted THAT specific skill only.

        CRITICAL RULES:
        1. For each skill gap, look ONLY at its own "### Skill: <name>" section (match by exact name,
           case-insensitive). NEVER use a course found under a different skill's section, even if it seems
           related.
        2. From that skill's own section, extract 1 to 2 REAL courses explicitly present in the text
           (exact title, provider/platform, and URL as found). NEVER invent a title, provider, or URL.
        3. If that skill's section has no section at all, or contains no concrete course (no title/URL),
           return an EMPTY "recommendedCourses" array for it and set "warning" to a short note such as
           "No verified internet course found for <skill name>".
        4. Technology-skills gaps are higher priority than soft-skills gaps: if you must be selective due to
           ambiguous or noisy findings, favor precision (fewer, correct courses) especially for
           technology-skills.
        5. Return exactly one assignment entry per input skill gap, using its exact original "name".
        """;

    private const string CatalogMatchInstructions = """
        You are a strict catalog-matching assistant. You will receive:
        1. A FIXED list of skill gaps (name + category) identified for a process.
        2. The organization's EXISTING training catalog (dbo.Skill): a list of catalog entries, each with
           a "skillId", "name", "description", and "category".

        CRITICAL RULES:
        1. For each skill gap, decide whether an EXISTING catalog entry ALREADY genuinely covers it —
           same real-world skill/tool, not just a loosely related topic. Be conservative: a wrong match is
           worse than no match, because a true match means we will NOT search the internet or create
           anything new for that gap.
        2. Only set "isFromCatalog" = true when you are confident the catalog entry covers the gap. If in
           doubt, set it to false.
        3. When isFromCatalog = true, "matchedSkillId" MUST be copied EXACTLY (same characters) from the
           catalog entry's own "skillId" field, and "matchedSkillName" from its "name" field.
        4. When isFromCatalog = false, leave "matchedSkillId" and "matchedSkillName" null.
        5. Return exactly one entry per input skill gap, using its exact original "name".
        """;

    private readonly AIAgent? _agent;
    private readonly AIAgent? _courseAssignmentAgent;
    private readonly AIAgent? _catalogMatchAgent;
    private readonly ILogger<ProcessWideLearningRecommendationAnalyzer>? _logger;

    public ProcessWideLearningRecommendationAnalyzer(
        IConfiguration configuration,
        ILogger<ProcessWideLearningRecommendationAnalyzer>? logger = null)
    {
        _logger = logger;

        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Follow repository pattern: economy tier gpt-5-mini, fallback to main deployment
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        DeploymentName = deploymentName;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            _courseAssignmentAgent = null;
            _catalogMatchAgent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "ProcessWideLearningRecommendationAnalyzer");

        _courseAssignmentAgent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: CourseAssignmentInstructions, name: "ProcessSkillGapCourseAssignmentAgent");

        _catalogMatchAgent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: CatalogMatchInstructions, name: "ProcessSkillGapCatalogMatchAgent");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<ProcessWideLearningRecommendationResult> AnalyzeProcessAsync(
        string documentId,
        string processName,
        List<ProcessChapter> chapters,
        string? webFindings,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "ProcessWideLearningRecommendationAnalyzer is not configured. Set 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' configuration keys.");
        }

        if (chapters is null || chapters.Count == 0)
        {
            throw new ArgumentException("At least one chapter is required.", nameof(chapters));
        }

        var stopwatch = Stopwatch.StartNew();
        var prompt = BuildPrompt(documentId, processName, chapters, webFindings);

        try
        {
            var agentResponse = await _agent.RunAsync<ProcessWideLearningRecommendationResult>(
                prompt,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            var result = agentResponse.Result ?? throw new InvalidOperationException("The agent returned a null analysis result.");

            result.DocumentId = documentId;
            if (string.IsNullOrWhiteSpace(result.ProcessName))
            {
                result.ProcessName = processName;
            }

            _logger?.LogInformation(
                "Analyzed whole process {DocumentId} in {DurationMs}ms (Deployment: {Model}, Gaps: {Gaps}, WebGrounded: {WebGrounded})",
                documentId,
                stopwatch.ElapsedMilliseconds,
                DeploymentName ?? "unknown",
                result.SkillGaps?.Count ?? 0,
                !string.IsNullOrWhiteSpace(webFindings));

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger?.LogError(
                ex,
                "Failed to analyze whole process {DocumentId} after {DurationMs}ms",
                documentId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    public async Task<List<ProcessSkillGapItem>> AssignCoursesToGapsAsync(
        string processName,
        List<ProcessSkillGapItem> gaps,
        string labeledFindings,
        CancellationToken cancellationToken = default)
    {
        if (_courseAssignmentAgent is null)
        {
            throw new InvalidOperationException(
                "ProcessWideLearningRecommendationAnalyzer is not configured. Set 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' configuration keys.");
        }

        if (gaps is null || gaps.Count == 0)
        {
            return gaps ?? [];
        }

        var prompt = BuildCourseAssignmentPrompt(processName, gaps, labeledFindings);

        try
        {
            var agentResponse = await _courseAssignmentAgent.RunAsync<CourseAssignmentResult>(
                prompt,
                cancellationToken: cancellationToken);

            var assignmentResult = agentResponse.Result;
            var assignmentsByName = (assignmentResult?.Assignments ?? [])
                .GroupBy(a => a.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var gap in gaps)
            {
                if (assignmentsByName.TryGetValue(gap.Name.Trim(), out var assignment))
                {
                    gap.RecommendedCourses = assignment.RecommendedCourses ?? [];
                }
            }

            return gaps;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to assign courses to {GapCount} skill gaps for process {ProcessName}", gaps.Count, processName);
            throw;
        }
    }

    public async Task<List<ProcessSkillGapItem>> MatchGapsToCatalogAsync(
        List<ProcessSkillGapItem> gaps,
        List<CatalogSkillEntry> catalogSkills,
        CancellationToken cancellationToken = default)
    {
        if (_catalogMatchAgent is null)
        {
            throw new InvalidOperationException(
                "ProcessWideLearningRecommendationAnalyzer is not configured. Set 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' configuration keys.");
        }

        if (gaps is null || gaps.Count == 0 || catalogSkills is null || catalogSkills.Count == 0)
        {
            return gaps ?? [];
        }

        var prompt = BuildCatalogMatchPrompt(gaps, catalogSkills);

        try
        {
            var agentResponse = await _catalogMatchAgent.RunAsync<CatalogMatchResult>(
                prompt,
                cancellationToken: cancellationToken);

            var matchResult = agentResponse.Result;
            var matchesByName = (matchResult?.Matches ?? [])
                .GroupBy(m => m.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Only trust a match whose matchedSkillId genuinely exists in the catalog we sent.
            var validCatalogIds = catalogSkills.Select(s => s.SkillId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var gap in gaps)
            {
                if (matchesByName.TryGetValue(gap.Name.Trim(), out var match)
                    && match.IsFromCatalog
                    && !string.IsNullOrWhiteSpace(match.MatchedSkillId)
                    && validCatalogIds.Contains(match.MatchedSkillId))
                {
                    gap.IsFromCatalog = true;
                    gap.MatchedSkillId = match.MatchedSkillId;
                    gap.MatchedSkillName = match.MatchedSkillName;
                }
            }

            return gaps;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogError(ex, "Failed to match {GapCount} skill gaps against the catalog", gaps.Count);
            // Non-fatal: treat as "no catalog matches found" so the caller falls back to internet search.
            return gaps;
        }
    }

    private static string BuildCatalogMatchPrompt(List<ProcessSkillGapItem> gaps, List<CatalogSkillEntry> catalogSkills)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SKILL GAPS (match each against the catalog below; do not rename/add/remove) ===");
        foreach (var gap in gaps)
        {
            sb.AppendLine($"- name: \"{gap.Name}\" | category: {gap.Category}");
        }
        sb.AppendLine("=== END OF SKILL GAPS ===");
        sb.AppendLine();
        sb.AppendLine("=== EXISTING CATALOG (dbo.Skill) ===");
        foreach (var skill in catalogSkills)
        {
            sb.AppendLine($"- skillId: \"{skill.SkillId}\" | name: \"{skill.Name}\" | category: {skill.Category} | description: {skill.Description ?? "(none)"}");
        }
        sb.AppendLine("=== END OF EXISTING CATALOG ===");

        return sb.ToString();
    }

    private static string BuildCourseAssignmentPrompt(
        string processName,
        List<ProcessSkillGapItem> gaps,
        string labeledFindings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Process Name: {processName}");
        sb.AppendLine();
        sb.AppendLine("=== FIXED SKILL GAPS (do not rename/add/remove; technology-skills listed first) ===");

        foreach (var gap in gaps.OrderBy(g => g.Category == "technology-skills" ? 0 : 1))
        {
            sb.AppendLine($"- name: \"{gap.Name}\" | category: {gap.Category}");
        }

        sb.AppendLine("=== END OF FIXED SKILL GAPS ===");
        sb.AppendLine();
        sb.AppendLine("=== INTERNET SEARCH FINDINGS (labeled per skill; use ONLY each skill's own section) ===");
        sb.AppendLine(string.IsNullOrWhiteSpace(labeledFindings) ? "(none available for any skill)" : labeledFindings);
        sb.AppendLine("=== END OF INTERNET SEARCH FINDINGS ===");

        return sb.ToString();
    }

    private static string BuildPrompt(
        string documentId,
        string processName,
        List<ProcessChapter> chapters,
        string? webFindings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the FULL following process document (all chapters and steps together):");
        sb.AppendLine($"Document ID: {documentId}");
        sb.AppendLine($"Process Name: {processName}");
        sb.AppendLine();

        foreach (var chapter in chapters)
        {
            sb.AppendLine($"=== Chapter {chapter.ChapterNumber}: {chapter.Title} ===");
            if (!string.IsNullOrWhiteSpace(chapter.Description))
            {
                sb.AppendLine(chapter.Description);
            }

            foreach (var step in chapter.Steps)
            {
                sb.AppendLine($"- Step {step.StepNumber}: {step.StepName}");
                if (!string.IsNullOrWhiteSpace(step.Objective))
                {
                    sb.AppendLine($"  Objective: {step.Objective}");
                }
                if (step.RequiredActivities?.Count > 0)
                {
                    sb.AppendLine("  Activities: " + string.Join("; ", step.RequiredActivities));
                }
                if (step.RequiredSkills?.Count > 0)
                {
                    sb.AppendLine("  Noted required skills: " + string.Join("; ", step.RequiredSkills));
                }
                if (step.Deliverables?.Count > 0)
                {
                    sb.AppendLine("  Deliverables: " + string.Join("; ", step.Deliverables));
                }
            }

            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(webFindings))
        {
            sb.AppendLine("=== INTERNET SEARCH FINDINGS (use ONLY these for course recommendations; do not invent) ===");
            sb.AppendLine(webFindings);
            sb.AppendLine("=== END OF INTERNET SEARCH FINDINGS ===");
        }
        else
        {
            sb.AppendLine("=== INTERNET SEARCH FINDINGS ===");
            sb.AppendLine("(none available — leave recommendedCourses empty for every gap and add a warning)");
            sb.AppendLine("=== END OF INTERNET SEARCH FINDINGS ===");
        }

        return sb.ToString();
    }
}
