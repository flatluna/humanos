using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.EmployeeScoring;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>
/// AI Agent powered by Microsoft Agent Framework that assigns personalized
/// impact scores (0-100) to learning Engrams based on an employee's professional profile.
/// </summary>
public sealed class EngramImpactScoringAgent
{
    private const string SystemPrompt = """
        You are the ENGRAM IMPACT SCORING AGENT for Human OS.

        Your job: given ONE employee's professional profile and a LIST of learning
        Engrams (concepts) from a single course, assign each Engram a personal
        IMPACT SCORE from 0 to 100. The score answers ONE question:
        "How much will mastering THIS Engram improve THIS specific person's job
        performance and maximize their potential right now?"

        You do NOT change the order of the Engrams.
        You do NOT skip, add, or invent Engrams.
        You score every Engram you are given, exactly once.

        SCORING SIGNALS & WEIGHTING FRAMEWORK:
        1. JOB DESCRIPTION / ROLE (40%) - WHAT THE ROLE DEMANDS & REQUIRES:
           The single highest-weight foundation. Any concept explicitly required by the
           Job Description, core responsibilities, mandatory technical competencies,
           expected success outcomes, or daily activities has HIGH IMPACT. If the role
           mandates designing, maintaining, or securing a system or architecture, mastering
           that Engram is fundamental.
        2. KPIs & ACTIVE SMART OBJECTIVES (25%) - WHAT THE COMPANY MEASURES TODAY:
           Directly targets active quarterly goals, SLA/uptime targets, latency P95 metrics,
           automated test coverage, and defect reduction.
        3. MANAGER COACHING & FLAGGED GAPS (15%) - MANAGER RECOMMENDATIONS:
           Explicit growth opportunities and leadership/technical areas highlighted in
           manager coaching notes and active development feedback.
        4. CAREER GROWTH & PROMOTION ROADMAP (12%) - FUTURE ADVANCEMENT:
           Skills and advanced architectural competencies that position the learner for
           higher seniority levels, expanded leadership responsibilities, and future role progression.
        5. ASSIGNED SKILLS & TOOLS (8%) - SUPPORTING COMPETENCIES:
           Supporting technical tools, company-assigned competencies, and specialized software.

        TIER CALIBRATION PRINCIPLES (DEMAND vs. RESUME vs. MY BRAIN):
        - REQUIRED + UNVERIFIED GAP (CRITICAL: 80-100):
          If the role, KPIs, or coaching require a concept, and the learner's RESUME
          does NOT demonstrate verified background, this is an urgent growth gap.
          Score it as Critical (80-100).
        - REQUIRED + IN RESUME / CONFIRM MASTERY (IMPORTANT: 55-79):
          If the learner's RESUME already claims or shows experience in a required concept,
          it MUST NOT be demoted to Base. It remains Important (55-79) to confirm
          mastery, align with company standards, and earn official verification.
          Reason phrasing: "Your resume shows experience in X; recommended to confirm
          mastery and official company alignment for your role."
        - REQUIRED + IN RESUME + MASTERED REFRESHER (REFRESHER: 30-54):
          When knowledge is already verified in active retention, it serves as memory
          maintenance and a light refresher (30-54).
        - PERIPHERAL / OFF-DOMAIN (BASE / FOUNDATIONAL: 0-29):
          Concepts that are foundational or not actively driving the Job Description,
          KPIs, or coaching score Base / Foundational (0-29).

        SCORING RULES:
        - Score RELATIVELY across the whole list with high fidelity to the Job Description.
        - Clearly explain how the Job Description (40%), KPIs (25%), Coaching (15%), Career Growth (12%), or Skills (8%) justify each score.
        - Speak directly to the learner using 'your' (e.g., "Your job description mandates
          owning microservices architecture, which is essential for your P95 latency goals.").

        TIER BANDS (derive from the number):
        80-100 -> "Critical"
        55-79  -> "Important"
        30-54  -> "Refresher"
        0-29   -> "Base / Foundational"

        OUTPUT FORMAT (STRICT):
        Return ONLY valid JSON. No prose before or after. An object containing a "scores" array with exactly one entry per Engram provided, preserving the original engramId and the original order:
        {
          "scores": [
            {
              "engramId": "<id as provided>",
              "score": <integer 0-100>,
              "tier": "Critical" | "Important" | "Refresher" | "Base / Foundational",
              "primarySignal": "Role" | "KPI" | "Coaching" | "CareerGrowth" | "Skill",
              "reason": "<one short sentence, max 25 words, naming the specific Job Description responsibility/KPI/coaching note/career milestone that justifies the score. Speak TO the learner using 'your'.>"
            }
          ]
        }
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName;
    private readonly bool _isReasoningModel;

    public EngramImpactScoringAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _deploymentName = deploymentName ?? string.Empty;
        _isReasoningModel = !string.IsNullOrEmpty(deploymentName) &&
            (deploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase) ||
             deploymentName.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
             deploymentName.Contains("o3", StringComparison.OrdinalIgnoreCase)) &&
            !deploymentName.Contains("chat", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        var defaultChatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            Instructions = SystemPrompt
        };

        if (!_isReasoningModel)
        {
            defaultChatOptions.Temperature = 0.2f;
        }

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "EngramImpactScoringAgent",
                ChatOptions = defaultChatOptions
            });
    }

    public bool IsConfigured => _agent is not null;

    public string DeploymentName => _deploymentName;

    /// <summary>
    /// Scores a list of course Engrams against the specified employee scoring profile.
    /// </summary>
    public async Task<List<EngramScoreResult>> ScoreEngramsAsync(
        EmployeeScoringProfile profile,
        List<EngramInput> engrams,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(engrams);

        if (engrams.Count == 0)
        {
            return [];
        }

        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Engram Impact Scoring Agent is not configured. Verify 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName'.");
        }

        var userPrompt = BuildUserPrompt(profile, engrams);

        var chatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json
        };

        if (!_isReasoningModel)
        {
            chatOptions.Temperature = 0.2f;
        }

        var runOptions = new ChatClientAgentRunOptions(chatOptions);

        List<EngramScoreResult> parsedResults = [];
        try
        {
            try
            {
                var structuredResponse = await _agent.RunAsync<EngramImpactScoringResponse>(
                    userPrompt, options: runOptions, cancellationToken: cancellationToken);

                if (structuredResponse?.Result?.Scores != null && structuredResponse.Result.Scores.Count > 0)
                {
                    parsedResults = structuredResponse.Result.Scores;
                }
            }
            catch
            {
                // Fallback to untyped string RunAsync
                var response = await _agent.RunAsync(userPrompt, options: runOptions, cancellationToken: cancellationToken);
                var responseText = response.Text?.Trim() ?? string.Empty;
                parsedResults = ParseModelResponse(responseText);
            }

            if (parsedResults.Count == 0)
            {
                var response = await _agent.RunAsync(userPrompt, options: runOptions, cancellationToken: cancellationToken);
                var responseText = response.Text?.Trim() ?? string.Empty;
                parsedResults = ParseModelResponse(responseText);
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException && ex is not ArgumentException)
        {
            throw new InvalidOperationException($"Azure OpenAI call for Engram Impact Scoring failed: {ex.Message}", ex);
        }

        return ValidateAndNormalizeResults(engrams, parsedResults);
    }

    private static string BuildUserPrompt(EmployeeScoringProfile profile, List<EngramInput> engrams)
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        var profileText = $"""
            === EMPLOYEE PROFILE ===
            Employee ID: {profile.EmployeeId}
            Role Title: {profile.RoleTitle}
            Role Purpose: {profile.RolePurpose}

            Core Responsibilities:
            {(profile.CoreResponsibilities.Count == 0 ? "(none)" : string.Join('\n', profile.CoreResponsibilities.Select(r => "- " + r)))}

            Daily Activities:
            {(profile.DailyActivities.Count == 0 ? "(none)" : string.Join('\n', profile.DailyActivities.Select(a => "- " + a)))}

            Active SMART Objectives:
            {(profile.ActiveObjectives.Count == 0 ? "(none)" : JsonSerializer.Serialize(profile.ActiveObjectives, jsonOptions))}

            KPIs / Performance Indicators:
            {(profile.Kpis.Count == 0 ? "(none)" : string.Join('\n', profile.Kpis.Select(k => "- " + k)))}

            Manager Coaching & Growth Notes:
            {(profile.CoachingNotes.Count == 0 ? "(none)" : JsonSerializer.Serialize(profile.CoachingNotes, jsonOptions))}

            Company Assigned Technical Skills:
            {(profile.TechnicalSkills.Count == 0 ? "(none)" : string.Join('\n', profile.TechnicalSkills.Select(s => "- " + s)))}

            Company Assigned Soft Skills:
            {(profile.SoftSkills.Count == 0 ? "(none)" : string.Join('\n', profile.SoftSkills.Select(s => "- " + s)))}

            Tools & Software Required:
            {(profile.Tools.Count == 0 ? "(none)" : string.Join('\n', profile.Tools.Select(t => "- " + t)))}

            Expected Success Outcomes:
            {(profile.ExpectedSuccessOutcomes.Count == 0 ? "(none)" : string.Join('\n', profile.ExpectedSuccessOutcomes.Select(o => "- " + o)))}

            === RÉSUMÉ / CV DATA ===
            Resume Summary: {profile.ResumeSummary}
            Years of Experience: {(profile.YearsOfExperience.HasValue ? profile.YearsOfExperience.Value.ToString() : "(not specified)")}

            Work Experience:
            {(profile.WorkExperience.Count == 0 ? "(none)" : JsonSerializer.Serialize(profile.WorkExperience, jsonOptions))}

            Education:
            {(profile.Education.Count == 0 ? "(none)" : JsonSerializer.Serialize(profile.Education, jsonOptions))}

            Certifications:
            {(profile.Certifications.Count == 0 ? "(none)" : string.Join('\n', profile.Certifications.Select(c => "- " + c)))}

            Resume Claimed Skills:
            {(profile.ResumeSkills.Count == 0 ? "(none)" : string.Join('\n', profile.ResumeSkills.Select(s => "- " + s)))}

            === COURSE ENGRAMS TO SCORE (preserve order & ids) ===
            {JsonSerializer.Serialize(engrams.Select(e => new { engramId = e.EngramId, title = e.Title }), jsonOptions)}
            """;

        return profileText;
    }

    private static List<EngramScoreResult> ParseModelResponse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return [];
        }

        var json = rawText;
        if (json.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            json = json[7..];
        }
        else if (json.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            json = json[3..];
        }

        if (json.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            json = json[..^3];
        }

        json = json.Trim();

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Try direct array deserialization
        try
        {
            if (json.StartsWith('[') && json.EndsWith(']'))
            {
                var list = JsonSerializer.Deserialize<List<EngramScoreResult>>(json, jsonOptions);
                if (list != null)
                {
                    return list;
                }
            }
        }
        catch
        {
            // Fall through to object envelope parsing
        }

        // Try object envelope deserialization
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<EngramScoreResult>>(root.GetRawText(), jsonOptions) ?? [];
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var list = JsonSerializer.Deserialize<List<EngramScoreResult>>(prop.Value.GetRawText(), jsonOptions);
                        if (list != null && list.Count > 0)
                        {
                            return list;
                        }
                    }
                }
            }
        }
        catch
        {
            // Return empty so fallback normalization handles default scores
        }

        return [];
    }

    private static List<EngramScoreResult> ValidateAndNormalizeResults(
        List<EngramInput> originalEngrams,
        List<EngramScoreResult> parsedResults)
    {
        var lookup = new Dictionary<string, EngramScoreResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var res in parsedResults)
        {
            if (!string.IsNullOrWhiteSpace(res.EngramId) && !lookup.ContainsKey(res.EngramId))
            {
                lookup[res.EngramId] = res;
            }
        }

        var sameCount = parsedResults.Count == originalEngrams.Count;
        var normalized = new List<EngramScoreResult>(originalEngrams.Count);

        for (var i = 0; i < originalEngrams.Count; i++)
        {
            var original = originalEngrams[i];
            EngramScoreResult? match = null;

            if (lookup.TryGetValue(original.EngramId, out var directMatch))
            {
                match = directMatch;
            }
            else if (sameCount && !string.IsNullOrWhiteSpace(parsedResults[i].Reason))
            {
                match = parsedResults[i];
            }

            if (match is not null)
            {
                var clampedScore = Math.Clamp(match.Score, 0, 100);
                var tier = CalculateTier(clampedScore);
                var signal = NormalizeSignal(match.PrimarySignal);
                var reason = (match.Reason ?? string.Empty).Trim();
                if (reason.Length > 300)
                {
                    reason = reason[..300];
                }

                normalized.Add(new EngramScoreResult
                {
                    EngramId = original.EngramId,
                    Score = clampedScore,
                    Tier = tier,
                    PrimarySignal = signal,
                    Reason = reason
                });
            }
            else
            {
                normalized.Add(new EngramScoreResult
                {
                    EngramId = original.EngramId,
                    Score = 0,
                    Tier = "Base / Foundational",
                    PrimarySignal = "Role",
                    Reason = "Not scored - defaulted"
                });
            }
        }

        return normalized;
    }

    private static string CalculateTier(int score) => score switch
    {
        >= 80 => "Critical",
        >= 55 => "Important",
        >= 30 => "Refresher",
        _ => "Base / Foundational"
    };

    private static string NormalizeSignal(string? signal)
    {
        if (string.IsNullOrWhiteSpace(signal))
        {
            return "Role";
        }

        var s = signal.Trim().ToLowerInvariant();
        if (s.Contains("career") || s.Contains("growth") || s.Contains("ascenso") || s.Contains("promotion"))
        {
            return "CareerGrowth";
        }

        return s switch
        {
            "kpi" => "KPI",
            "coaching" => "Coaching",
            "role" => "Role",
            "skill" => "Skill",
            _ => "Role"
        };
    }
}
