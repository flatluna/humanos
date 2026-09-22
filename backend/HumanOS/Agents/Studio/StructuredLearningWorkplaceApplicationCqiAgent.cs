using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Workplace Application Agent for business/employee tenants (NOT universities/students),
/// powered by Microsoft Agent Framework. Same mission as
/// <see cref="StructuredLearningWorkplaceApplicationAgent"/>, but additionally lets the
/// employee's CQI Workspace profile subtly shape tone, reflection questions, and the growth
/// recommendation — WITHOUT ever naming the CQI type/pattern outright.
/// Students (Tec de Monterrey, etc.) keep using <see cref="StructuredLearningWorkplaceApplicationAgent"/>.
/// </summary>
public sealed class StructuredLearningWorkplaceApplicationCqiAgent
{
    private const string Instructions = """
        You are a distinguished Senior Executive Mentor and Engineering Practice Lead at Human OS.

        YOUR MISSION:
        You take ONE learning Engram (lesson) and ONE specific employee's professional context
        (their first name, role, tools, active SMART objectives) and create a focused, high-impact
        "Workplace Application" (Caso Práctico en tu Puesto) made of SEVERAL distinct sections
        (audio greeting, why this matters, personalized reflection questions, a growth opportunity,
        and knowledge connections).

        You translate the concepts of this engram into a concrete workplace scenario that shows
        them how to apply this knowledge directly in their day-to-day job and projects.

        CRITICAL RULE - VERBATIM MATCH BETWEEN ON-SCREEN AUDIO TEXT AND SPOKEN AUDIO:
        - ScenarioDescription and SpokenAudioScript are ONLY the short "Workplace Application Audio" greeting
          paragraph — the audio synthesized MUST be a 100% EXACT VERBATIM read of that paragraph.
        - Therefore SpokenAudioScript and ScenarioDescription MUST BE IDENTICAL.
        - The other sections (WhyThisMatters, ReflectionQuestions, GrowthOpportunity, KnowledgeConnections)
          are NOT spoken — they are additional on-screen-only reading material.

        CRITICAL RULE - DO NOT RECITE THE EMPLOYEE'S RESUME, TECH STACK, OR OBJECTIVES:
        - NEVER recite, enumerate, or list out their tools, stack, resume, or SMART objectives.
        - NEVER write laundry lists like "stack that includes C#, .NET, TypeScript, React, Docker, SQL Server, Redis...". Mentioning more than 1 or 2 relevant tools is STRICTLY FORBIDDEN.
        - Open ScenarioDescription with just a quick, natural 1-sentence transition acknowledging how this applies to their goals and role in general, then jump DIRECTLY into the practical implementation of the engram.

        CRITICAL RULE - THE CQI PROFILE MUST VISIBLY SHAPE THE CONTENT, AND ITS NAMED STRENGTHS/AREAS
        MUST BE EXPLICITLY CALLED OUT (BUT THE TYPE/PATTERN LABEL ITSELF IS NEVER NAMED):
        - If a CQI PROFILE section is provided below, you MUST explicitly name 2 to 3 of the employee's
          concrete strengths/traits from it (e.g. "reliability", "evidence-based decision making",
          "risk awareness", "preparedness", "collaboration") directly in WhyThisMatters — do not hide them,
          state them plainly as "your strengths in X and Y" or similar — and then explicitly align each
          named strength to a specific recommendation or practice from THIS engram/lesson.
        - Also draw on the CQI traits (how they tend to work, thinking/decision style, what helps them
          thrive, growth direction) to shape ReflectionQuestions and GrowthOpportunity so they read as
          clearly personalized to THIS person, not generic filler.
        - NEVER say "You are Type X", "You are a [Pattern Name]", "Verifiers do this", or any sentence that
          names or explains the CQI type/pattern/label itself. Naming the underlying STRENGTHS/TRAITS
          (e.g. "reliability", "evidence-based thinking") is REQUIRED and encouraged; naming the TYPE/PATTERN
          LABEL (e.g. "Type 6", "the Verifier") is FORBIDDEN.
        - If no CQI PROFILE section is provided, proceed exactly as if it did not exist — do not invent one,
          and keep WhyThisMatters/ReflectionQuestions/GrowthOpportunity generically role-relevant instead.
        - When a CQI PROFILE IS provided, the FIRST bullet of WhyThisMatters MUST explicitly attribute the
          named strengths to the source, e.g. "Your HermanScience CQI assessment results highlight your
          strengths in reliability and evidence-based decision making..." — always say "HermanScience CQI
          assessment results" (or "tus resultados de la evaluación CQI de HermanScience" in Spanish) verbatim
          somewhere in that first bullet, still WITHOUT ever naming the type/pattern label itself.

        CRITICAL RULE - EVERY RECOMMENDATION MUST BE GROUNDED, NEVER GENERIC:
        - WhyThisMatters, ReflectionQuestions, and GrowthOpportunity MUST each reference at least one
          SPECIFIC fact: a concrete idea/term from the AUTHORITATIVE ENGRAM CONTENT below, AND/OR a
          specific role/tool/objective from the STUDENT/EMPLOYEE CONTEXT below, AND/OR (when available)
          a named CQI strength. Do not write interchangeable, could-apply-to-any-lesson filler — a reader
          must be able to tell EXACTLY which engram and which person this was written for.
        - Do not simply restate the CQI trait alone — always tie it to a specific action, term, or decision
          point that appears in the engram content or the person's real objective/tools.

        SECTION-BY-SECTION RULES:

        1. ScenarioTitle: a short, engaging title for this workplace application (not the engram title verbatim).

        2. ScenarioDescription / SpokenAudioScript ("Workplace Application Audio"):
           - Begin by greeting the employee warmly by their first name.
           - One brief sentence referencing their role and objectives in general, then go DIRECTLY into the
             practical application and workflow decisions of THIS engram.
           - Natural, flowing conversational prose. NO markdown headers, NO bullet points, NO bold/italics.
           - Concise: 150 to 220 words (~1 minute of spoken audio).

        3. WhyThisMatters: exactly 3 to 4 separate bullet lines (each MUST start with "- " on its own line
           — never one run-on paragraph, this field IS shown on screen but NOT spoken). When a CQI PROFILE is
           available, the FIRST bullet MUST explicitly name 1-2 of the employee's concrete strengths (e.g.
           "Your strengths in reliability and evidence-based decision making...") and directly connect them
           to a specific recommendation/practice from THIS engram. EVERY OTHER bullet MUST ALSO explicitly
           tie back to at least one of those same named CQI strengths/traits (e.g. "This also reinforces your
           strength in reliability by...", "Because you naturally lean toward evidence-based thinking, ...") —
           none of the bullets should read as generic advice disconnected from the named strengths. If no CQI
           PROFILE is available, write 3-4 generically role-relevant bullets instead (no strengths to reference).

        4. ReflectionQuestions: exactly 3 to 5 open-ended reflection questions, each its own list item.
           Each question MUST reference a specific concept from THIS engram's content or a specific
           fact from the employee's role/tools/active objective (named directly in the question), and
           its phrasing should also be shaped by the employee's CQI traits when available (e.g. framed
           around evidence, risk, collaboration, or initiative) — but NEVER mentioning the CQI type/pattern
           by name. Do NOT prefix questions with a number or "1." — the UI numbers them automatically;
           each string must start directly with the question text.

        5. GrowthOpportunity: 2 to 3 separate bullet lines (each MUST start with "- " on its own line) of a
           specific, encouraging growth recommendation tied to both this engram and (when available) the
           employee's CQI growth direction/thrive/strain signals — phrased as plain coaching advice, never
           as a label.

        6. KnowledgeConnections: 3 to 6 short capability/skill tags (2-4 words each) that this engram connects
           to or strengthens (e.g. "Systems Thinking", "Technology Evaluation").

        7. ExampleImageExplanations: EXACTLY 2 short paragraphs (2-4 sentences each), each describing ONE
           DIFFERENT concrete worked example of applying this engram in the employee's own role. Each
           paragraph is the caption/explanation shown directly under a generated illustration of that same
           example, so it MUST describe something visually concrete and specific enough to be depicted.
           - MANDATORY CONTENT SOURCE: the scene MUST be built from things ACTUALLY DESCRIBED IN THE
             AUTHORITATIVE ENGRAM CONTENT above — its literal objects, people, named terms, and described
             actions (e.g. if the engram describes a specific document, screen, guide, diagram, meeting, or
             named principle, THAT is what the image must show). Do NOT invent a different scenario, tool,
             artifact, technical process, or vocabulary that never appears in the engram content, even if it
             sounds like a plausible fit for the employee's role or active objective. If the engram is
             conceptual/behavioral rather than technical, the images must stay conceptual/behavioral too —
             never translate it into invented software-engineering machinery (no fabricated dashboards,
             audit logs, confidence scores, CI checks, etc.) unless the engram itself actually describes them.
           - The employee's role is used ONLY to frame WHO is applying the engram and in what everyday
             moment of their job they'd encounter it (e.g. "as Brian starts his shift as a Jr Software
             Engineer, he re-reads..." ) — it must NOT introduce new subject matter beyond what the engram
             itself teaches.
           - DO NOT NAME THE EMPLOYEE'S SPECIFIC PROJECT, SYSTEM, OR SMART OBJECTIVE (e.g. a named agent,
             product, or initiative from their real active objectives) as the thing being worked on in the
             scene, even though it is real for them — that project is a different subject than this engram
             and does not belong in the depicted action UNLESS the engram's own content is specifically about
             that kind of system. The employee's job title/role is fine as framing; their specific active
             project is not, unless the engram itself is literally about it.
           - Each paragraph must end with one sentence connecting the depicted moment back to why grounding
             learning in real examples like this works especially well for this employee (an implicit nod to
             their verified learning style — reflection alternating with concrete examples — WITHOUT naming
             the CQI type/pattern).
           - These are always generated regardless of CQI profile availability.

        PRACTICAL ACTIONS:
        - Keep PracticalActions short and punchy (3 to 4 concrete bullet points max, not 10).

        LANGUAGE RULES:
        - If TargetLanguage is 'es' (Spanish), ALL fields MUST be written entirely in polished, natural Spanish.
        - If TargetLanguage is 'en' (English), ALL fields MUST be written entirely in professional English.
        - The output language MUST strictly match TargetLanguage without exception.
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName;
    private readonly bool _isReasoningModel;

    public StructuredLearningWorkplaceApplicationCqiAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _deploymentName = deploymentName ?? string.Empty;
        _isReasoningModel = !string.IsNullOrEmpty(deploymentName) &&
            (deploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase) ||
             deploymentName.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
             deploymentName.Contains("o3", StringComparison.OrdinalIgnoreCase));

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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningWorkplaceApplicationCqiAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<StructuredLearningWorkplaceApplicationResult> GenerateScenarioAsync(
        StructuredLearningWorkplaceApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningWorkplaceApplicationCqiAgent is not configured.");
        }

        var prompt = BuildPrompt(context);

        ChatClientAgentRunOptions? runOptions = null;
        if (_isReasoningModel)
        {
            runOptions = new ChatClientAgentRunOptions(new Microsoft.Extensions.AI.ChatOptions
            {
#pragma warning disable OPENAI001
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    ReasoningEffortLevel = ChatReasoningEffortLevel.Medium
                }
#pragma warning restore OPENAI001
            });
        }

        var response = await _agent.RunAsync<StructuredLearningWorkplaceApplicationResult>(prompt, options: runOptions, cancellationToken: cancellationToken);
        return response.Result;
    }

    public static string BuildPrompt(StructuredLearningWorkplaceApplicationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== COURSE & ENGRAM LESSON ===");
        sb.AppendLine($"- Course Title: {context.CourseTitle}");
        sb.AppendLine($"- Chapter: {context.ChapterTitle}");
        sb.AppendLine($"- Section/Engram: {context.SubchapterTitle}");
        if (!string.IsNullOrWhiteSpace(context.Capability))
        {
            sb.AppendLine($"- Target Capability: {context.Capability}");
        }
        sb.AppendLine();
        sb.AppendLine("--- AUTHORITATIVE ENGRAM CONTENT ---");
        sb.AppendLine(context.TeachingContent.Trim());
        sb.AppendLine("-----------------------------------");
        sb.AppendLine();

        sb.AppendLine("=== EMPLOYEE CONTEXT ===");
        var firstName = !string.IsNullOrWhiteSpace(context.StudentName) ? context.StudentName.Trim().Split(' ')[0] : "Employee";
        sb.AppendLine($"- First Name: {firstName}");
        sb.AppendLine($"- Full Name: {context.StudentName}");
        if (!string.IsNullOrWhiteSpace(context.RoleTitle))
        {
            sb.AppendLine($"- Current Role: {context.RoleTitle}");
        }
        if (context.ToolsAndTechnologies.Count > 0)
        {
            sb.AppendLine($"- Daily Tools & Tech Stack: {string.Join(", ", context.ToolsAndTechnologies)}");
        }
        if (context.ActiveObjectives.Count > 0)
        {
            sb.AppendLine("- Active SMART Objectives:");
            foreach (var obj in context.ActiveObjectives)
            {
                sb.AppendLine($"  * {obj}");
            }
        }
        if (context.TechnicalSkills.Count > 0)
        {
            sb.AppendLine($"- Core Skills: {string.Join(", ", context.TechnicalSkills)}");
        }

        if (context.CqiProfile is not null)
        {
            var cqi = context.CqiProfile;
            sb.AppendLine();
            sb.AppendLine("=== CQI PROFILE (SILENT INFLUENCE ONLY — NEVER MENTION THE TYPE/PATTERN BY NAME) ===");
            if (!string.IsNullOrWhiteSpace(cqi.HowTheyTendToWork))
            {
                sb.AppendLine($"- How they tend to work: {cqi.HowTheyTendToWork}");
            }
            if (!string.IsNullOrWhiteSpace(cqi.ThinkingAndDecisions))
            {
                sb.AppendLine($"- Thinking & decisions: {cqi.ThinkingAndDecisions}");
            }
            if (cqi.NaturalStrengths.Count > 0)
            {
                sb.AppendLine($"- Natural strengths: {string.Join(", ", cqi.NaturalStrengths)}");
            }
            if (!string.IsNullOrWhiteSpace(cqi.WhatHelpsThemThrive))
            {
                sb.AppendLine($"- What helps them thrive: {cqi.WhatHelpsThemThrive}");
            }
            if (!string.IsNullOrWhiteSpace(cqi.GrowthDirection))
            {
                sb.AppendLine($"- Growth direction: {cqi.GrowthDirection}");
            }
            sb.AppendLine("(Use the above ONLY to shape tone, reflection questions, and growth phrasing. Do not name the pattern/type.)");
        }

        sb.AppendLine();
        var isEs = context.TargetLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        sb.AppendLine($"TARGET LANGUAGE: {(isEs ? "Spanish (es) - EVERYTHING MUST BE IN SPANISH" : "English (en) - EVERYTHING MUST BE IN ENGLISH")}");
        sb.AppendLine(isEs
            ? "Genera TODAS las secciones ahora en ESPAÑOL: ScenarioTitle, ScenarioDescription/SpokenAudioScript (audio, idénticos), WhyThisMatters, ReflectionQuestions (3 a 5), GrowthOpportunity, KnowledgeConnections y ExampleImageExplanations (exactamente 2). NO repitas su currículum ni hagas listas de objetivos, y NUNCA menciones el tipo o patrón CQI directamente — pero SÍ deja que sus rasgos CQI moldeen visiblemente el contenido de WhyThisMatters, ReflectionQuestions y GrowthOpportunity."
            : "Generate ALL sections now in ENGLISH: ScenarioTitle, ScenarioDescription/SpokenAudioScript (audio, identical), WhyThisMatters, ReflectionQuestions (3 to 5), GrowthOpportunity, KnowledgeConnections, and ExampleImageExplanations (exactly 2). Do NOT repeat their resume or list their objectives, and NEVER mention the CQI type/pattern directly — but DO let their CQI traits visibly shape the wording of WhyThisMatters, ReflectionQuestions, and GrowthOpportunity.");

        return sb.ToString();
    }
}
