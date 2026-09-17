using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.ProcessDocuments;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace HumanOS.Services.ProcessDocuments;

/// <summary>
/// Analyzes one extracted work step using Microsoft Agent Framework and the configured GPT-5 mini model,
/// determining what an employee must be able to do to perform the step successfully.
/// </summary>
public sealed class WorkStepRequirementsAnalyzer : IWorkStepRequirementsAnalyzer
{
    private const string Instructions = """
        You are an expert workforce capability and process intelligence agent.
        Your goal is to analyze ONE extracted business or technical work step and determine what an employee must be able to do to perform that step successfully.

        CRITICAL ARCHITECTURAL CONSTRAINTS ON SKILLS:
        1. A SKILL IS A FULL TRAINING COURSE (MACRO COMPETENCY):
           - In Human OS, every Skill represents an entire training Course or curriculum module (e.g., 'Prompt Engineering', 'Stakeholder Management', 'Discovery Process & Execution', 'Business Rules Architecture').
           - A Skill is NEVER an atomic micro-step, engram, or task!
             * PROHIBITED: Do NOT output micro-steps like 'how to create a one-shot prompt', 'schedule a calendar invite', 'read an engagement doc', 'write interview notes'.
             * CORRECT: Output the macro Skill / Course that teaches that domain (e.g., 'Prompt Engineering', 'Stakeholder Management', 'Discovery Preparation').
        2. REUSE EXISTING CATALOG SKILLS FIRST (TECHNICAL & SOFT SKILLS):
           - You are provided with the organization's official Skill Catalog containing already registered Soft, Technical, Process, and AI skills.
           - If an activity or capability in this step is covered by or aligns with an existing catalog skill, you MUST select and reuse that EXACT Skill name, category, and skillId verbatim.
           - DO NOT invent synonyms or paraphrased skills if a matching skill is already in the catalog (e.g., if "Stakeholder Management" is in the catalog, do NOT invent "Managing Stakeholders" or "Client Communication").
           - ONLY propose a new Skill if the step genuinely requires a distinct macro competency not present in the catalog.
        3. STRICT SEPARATION OF SKILLS VS. TOOLS:
           - Tools, software, applications, platforms, and communication channels (e.g. Email/Outlook, Microsoft Teams, Google Calendar, Excel, Slack, Zoom, Jira, Confluence, Master Document template) are strictly TOOLS, NEVER SKILLS.
           - Place all software, platforms, templates, and utilities exclusively in "requiredTools" as recommended tools that support the employee's work in this step.
        4. "requiredSkillAreas" — Map macro skills to the 4 canonical Human OS corporate categories:
           - "softSkills" (category: "soft-skills"): Interpersonal, communication, leadership, stakeholder management, facilitation, active listening, negotiation.
           - "technicalSkills" (category: "job-skills"): Role-specific technical, analytical, or architectural capabilities needed for this step (e.g. business discovery analysis, data modeling, requirement validation).
           - "departmentProcesses" (category: "department-processes"): Departmental workflows, operational protocols, and procedure standards relevant to the step.
           - "hrProcesses" (category: "hr-processes"): Corporate governance, HR policies, security/compliance rules, or standards of conduct.
           - "aiSkills" (category: "job-skills", isAiSkill: true): MANDATORY AI LEARNING REQUIREMENT. In Human OS, all employees must learn and leverage AI. ALWAYS identify/recommend at least 1 macro AI Skill / Course tailored to accelerate this work step (e.g., 'Prompt Engineering', 'INTRODUCTION TO AI', 'AI-Assisted Business Analysis'). ALL AI SKILLS MUST BE PLACED IN "aiSkills", NEVER IN "technicalSkills"!
           Keep each skill list concise (1 to 2 macro skills per area). Each skill must have "skillId", "name", "category", "reason", and "isAiSkill".
        5. "employeeMustBeAbleTo": Convert activities into a short list (3 to 6) of observable abilities describing what the HUMAN EMPLOYEE must be capable of doing.
        6. "requiredTools": Software applications, platforms, communication tools, and document templates that help the employee execute this step (e.g., Email client, Calendar tool, Video meeting tool, Document editor).
        7. "completionEvidence": Tangible work artifacts or observable results that verify completion based on deliverables and completion conditions.
        8. Keep outputs concise, professional, and practical.
        """;

    private readonly AIAgent? _agent;
    private readonly ILogger<WorkStepRequirementsAnalyzer>? _logger;

    public WorkStepRequirementsAnalyzer(
        IConfiguration configuration,
        ILogger<WorkStepRequirementsAnalyzer>? logger = null)
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
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(instructions: Instructions, name: "WorkStepRequirementsAnalyzer");
    }

    public bool IsConfigured => _agent is not null;

    public string? DeploymentName { get; }

    public async Task<WorkStepRequirementsAnalysisResult> AnalyzeStepRequirementsAsync(
        string documentId,
        string processName,
        string chapterOrPhase,
        ProcessStep step,
        List<ProcessTable>? relevantTables = null,
        IReadOnlyList<SkillItem>? previouslyIdentifiedSkills = null,
        CancellationToken cancellationToken = default) =>
        await AnalyzeStepRequirementsAsync(
            documentId,
            processName,
            chapterOrPhase,
            step,
            relevantTables,
            previouslyIdentifiedSkills,
            catalogSkills: null,
            cancellationToken);

    public async Task<WorkStepRequirementsAnalysisResult> AnalyzeStepRequirementsAsync(
        string documentId,
        string processName,
        string chapterOrPhase,
        ProcessStep step,
        List<ProcessTable>? relevantTables,
        IReadOnlyList<SkillItem>? previouslyIdentifiedSkills,
        IReadOnlyList<SkillItem>? catalogSkills,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "WorkStepRequirementsAnalyzer is not configured. Set 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName' configuration keys.");
        }

        if (step is null)
        {
            throw new ArgumentNullException(nameof(step));
        }

        var stopwatch = Stopwatch.StartNew();
        var prompt = BuildPrompt(documentId, processName, chapterOrPhase, step, relevantTables, previouslyIdentifiedSkills, catalogSkills);

        try
        {
            var agentResponse = await _agent.RunAsync<WorkStepRequirementsAnalysisResult>(
                prompt,
                cancellationToken: cancellationToken);

            stopwatch.Stop();

            var result = agentResponse.Result ?? throw new InvalidOperationException("The agent returned a null analysis result.");

            // Ensure identifiers and source pages match the input
            result.DocumentId = documentId;
            result.StepId = step.StepId;
            result.StepNumber = step.StepNumber;
            if (string.IsNullOrWhiteSpace(result.WorkStep))
            {
                result.WorkStep = step.StepName;
            }

            if (result.SourcePages == null || result.SourcePages.Count == 0)
            {
                result.SourcePages = step.SourcePages != null ? [.. step.SourcePages] : [];
            }

            // Normalization: Ensure every skill in every area has a canonical, deterministic SkillId (e.g. "skill-soft-stakeholder-management")
            NormalizeSkillIds(result.RequiredSkillAreas, catalogSkills);

            var obs = new WorkStepAnalysisObservability
            {
                DocumentId = documentId,
                StepId = step.StepId,
                ModelDeployment = DeploymentName ?? "unknown",
                Duration = stopwatch.Elapsed,
                Success = true,
                WarningsCount = result.Warnings?.Count ?? 0,
                InputTokens = (int)(agentResponse.Usage?.InputTokenCount ?? 0),
                OutputTokens = (int)(agentResponse.Usage?.OutputTokenCount ?? 0)
            };

            _logger?.LogInformation(
                "Analyzed work step {StepId} in {DurationMs}ms (Deployment: {Model}, Warnings: {Warnings}, InTokens: {InTokens}, OutTokens: {OutTokens})",
                obs.StepId,
                obs.Duration.TotalMilliseconds,
                obs.ModelDeployment,
                obs.WarningsCount,
                obs.InputTokens,
                obs.OutputTokens);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            _logger?.LogError(
                ex,
                "Failed to analyze work step {StepId} for document {DocumentId} after {DurationMs}ms",
                step.StepId,
                documentId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private static string BuildPrompt(
        string documentId,
        string processName,
        string chapterOrPhase,
        ProcessStep step,
        List<ProcessTable>? relevantTables,
        IReadOnlyList<SkillItem>? previouslyIdentifiedSkills,
        IReadOnlyList<SkillItem>? catalogSkills = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following extracted work step from a process document:");
        sb.AppendLine($"Document ID: {documentId}");
        sb.AppendLine($"Process Name: {processName}");
        sb.AppendLine($"Chapter / Phase: {chapterOrPhase}");
        sb.AppendLine($"Step ID: {step.StepId}");
        sb.AppendLine($"Step Number: {step.StepNumber}");
        sb.AppendLine($"Step Name: {step.StepName}");

        if (catalogSkills != null && catalogSkills.Count > 0)
        {
            sb.AppendLine("\n=== ORGANIZATION'S OFFICIAL SKILL CATALOG (Available Technical, Soft, and Process Skills) ===");
            sb.AppendLine("MANDATORY CATALOG INSTRUCTION: Review these existing skills in the company's catalog FIRST. If this work step requires any capability covered by or aligning with these existing skills, you MUST select and REUSE the EXACT skill name, category, and skillId below! Do NOT invent new skill names or synonyms (e.g. if 'Stakeholder Management' is listed, do NOT output 'Managing Stakeholders'). Only propose a new skill if no catalog skill fits:");
            foreach (var cat in catalogSkills)
            {
                var aiTag = cat.IsAiSkill ? " [AI Skill]" : "";
                sb.AppendLine($"- [id: {cat.SkillId}] [{cat.Category}] {cat.Name}{aiTag} (Catalog note: {cat.Reason})");
            }
            sb.AppendLine("=== END OF CATALOG ===\n");
        }

        if (previouslyIdentifiedSkills != null && previouslyIdentifiedSkills.Count > 0)
        {
            sb.AppendLine("\n=== CUMULATIVE PROCESS SKILLS REGISTRY (Already identified in earlier steps of this document) ===");
            sb.AppendLine("CRITICAL INSTRUCTION: To avoid duplicate/synonym skills across steps in the same process, REUSE the EXACT skill name and skillId below if this step requires the same capability! Only create a new skill if it is a genuinely different competency.");
            foreach (var existing in previouslyIdentifiedSkills)
            {
                var aiTag = existing.IsAiSkill ? " [AI Skill]" : "";
                sb.AppendLine($"- [id: {existing.SkillId}] [{existing.Category}] {existing.Name}{aiTag} (Prior context: {existing.Reason})");
            }
            sb.AppendLine("=== END OF REGISTRY ===\n");
        }

        if (!string.IsNullOrWhiteSpace(step.Objective))
        {
            sb.AppendLine($"Objective: {step.Objective}");
        }

        if (step.RequiredActivities.Count > 0)
        {
            sb.AppendLine("Required activities:");
            for (var i = 0; i < step.RequiredActivities.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {step.RequiredActivities[i]}");
            }
        }

        if (step.Substeps.Count > 0)
        {
            sb.AppendLine("Substeps:");
            foreach (var substep in step.Substeps)
            {
                sb.AppendLine($"- {substep.SubstepNumber} {substep.Name}: {substep.Description}");
            }
        }

        if (step.RequiredSkills.Count > 0)
        {
            sb.AppendLine("Required skills from source document:");
            foreach (var skill in step.RequiredSkills)
            {
                sb.AppendLine($"- {skill}");
            }
        }

        if (step.Deliverables.Count > 0)
        {
            sb.AppendLine("Expected deliverables:");
            foreach (var deliverable in step.Deliverables)
            {
                sb.AppendLine($"- {deliverable}");
            }
        }

        if (!string.IsNullOrWhiteSpace(step.CompletionCondition))
        {
            sb.AppendLine($"Completion condition: {step.CompletionCondition}");
        }

        if (step.Checklists.Count > 0)
        {
            sb.AppendLine("Checklists / Criteria:");
            foreach (var chk in step.Checklists)
            {
                sb.AppendLine($"- {chk}");
            }
        }

        if (step.SourcePages.Count > 0)
        {
            sb.AppendLine($"Source pages: {string.Join(", ", step.SourcePages)}");
        }

        if (relevantTables != null && relevantTables.Count > 0)
        {
            sb.AppendLine("Relevant Extracted Tables:");
            foreach (var table in relevantTables)
            {
                sb.AppendLine($"Table Title: {table.Title} (Page {table.SourcePage})");
                if (table.Headers.Count > 0)
                {
                    sb.AppendLine($"Headers: {string.Join(" | ", table.Headers)}");
                }
                foreach (var row in table.Rows.Take(5))
                {
                    sb.AppendLine($"Row: {string.Join(" | ", row)}");
                }
            }
        }

        return sb.ToString();
    }

    private static void NormalizeSkillIds(RequiredSkillAreas areas, IReadOnlyList<SkillItem>? catalogSkills = null)
    {
        if (areas == null) return;

        static string Norm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");
        }

        static bool IsAiRelated(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var lower = name.ToLowerInvariant();
            return lower.Contains("prompt engineering") ||
                   lower.Contains("introduction to ai") ||
                   lower.Contains("ai-assisted") ||
                   lower.Contains("generative ai") ||
                   lower.Contains("artificial intelligence") ||
                   lower.Contains("ia generativa") ||
                   lower.Contains("inteligencia artificial") ||
                   lower.StartsWith("ai ") || lower.EndsWith(" ai") || lower.Contains(" ai ") ||
                   lower.StartsWith("ia ") || lower.EndsWith(" ia") || lower.Contains(" ia ");
        }

        // Migrate any AI skills mistakenly placed into Technical, Business, or Soft areas
        var detectedAi = new List<SkillItem>();
        areas.TechnicalSkills.RemoveAll(s => { if (s.IsAiSkill || IsAiRelated(s.Name)) { s.IsAiSkill = true; detectedAi.Add(s); return true; } return false; });
        areas.BusinessSkills.RemoveAll(s => { if (s.IsAiSkill || IsAiRelated(s.Name)) { s.IsAiSkill = true; detectedAi.Add(s); return true; } return false; });
        areas.SoftSkills.RemoveAll(s => { if (s.IsAiSkill || IsAiRelated(s.Name)) { s.IsAiSkill = true; detectedAi.Add(s); return true; } return false; });
        areas.DepartmentProcesses.RemoveAll(s => { if (s.IsAiSkill || IsAiRelated(s.Name)) { s.IsAiSkill = true; detectedAi.Add(s); return true; } return false; });
        areas.HrProcesses.RemoveAll(s => { if (s.IsAiSkill || IsAiRelated(s.Name)) { s.IsAiSkill = true; detectedAi.Add(s); return true; } return false; });

        foreach (var ai in detectedAi)
        {
            if (!areas.AiSkills.Any(x => string.Equals(x.Name, ai.Name, StringComparison.OrdinalIgnoreCase)))
            {
                areas.AiSkills.Add(ai);
            }
        }

        // Guarantee at least 1 mandatory AI skill
        if (areas.AiSkills.Count == 0)
        {
            areas.AiSkills.Add(new SkillItem
            {
                Name = "AI-Assisted Business Analysis",
                Category = "job-skills",
                IsAiSkill = true,
                Reason = "Mandatory Human OS AI competency: leverage AI tools and agents to analyze, optimize, and execute work processes."
            });
        }

        void AssignId(SkillItem item, string defaultCategory)
        {
            if (string.IsNullOrWhiteSpace(item.Category)) item.Category = defaultCategory;

            // Check if this skill exists in the organization's official catalog
            if (catalogSkills != null && catalogSkills.Count > 0)
            {
                var normName = Norm(item.Name);
                if (!string.IsNullOrEmpty(normName))
                {
                    var match = catalogSkills.FirstOrDefault(cs =>
                        string.Equals(Norm(cs.Name), normName, StringComparison.OrdinalIgnoreCase) ||
                        (Norm(cs.Name).Length > 4 && normName.Contains(Norm(cs.Name))) ||
                        (normName.Length > 4 && Norm(cs.Name).Contains(normName)));

                    if (match != null)
                    {
                        item.IsInCatalog = true;
                        item.Name = match.Name;
                        if (!string.IsNullOrWhiteSpace(match.Category))
                        {
                            item.Category = match.Category;
                        }
                        if (!string.IsNullOrWhiteSpace(match.SkillId))
                        {
                            item.SkillId = match.SkillId;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(item.SkillId))
            {
                var prefix = item.Category switch
                {
                    "soft-skills" => "skill-soft",
                    "department-processes" => "skill-dept",
                    "hr-processes" => "skill-hr",
                    _ => item.IsAiSkill ? "skill-ai" : "skill-job"
                };

                var slug = System.Text.RegularExpressions.Regex.Replace(item.Name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
                if (string.IsNullOrWhiteSpace(slug)) slug = "item";
                item.SkillId = $"{prefix}-{slug}";
            }
        }

        foreach (var s in areas.SoftSkills) AssignId(s, "soft-skills");
        foreach (var s in areas.TechnicalSkills) AssignId(s, "job-skills");
        foreach (var s in areas.BusinessSkills) AssignId(s, "job-skills");
        foreach (var s in areas.DepartmentProcesses) AssignId(s, "department-processes");
        foreach (var s in areas.HrProcesses) AssignId(s, "hr-processes");
        foreach (var s in areas.AiSkills)
        {
            s.IsAiSkill = true;
            AssignId(s, "job-skills");
        }
    }
}
