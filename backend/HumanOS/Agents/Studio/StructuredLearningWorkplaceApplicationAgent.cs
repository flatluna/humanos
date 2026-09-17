using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Workplace Application Agent powered by Microsoft Agent Framework.
/// Generates a personalized real-world workplace scenario and a pre-recorded neural TTS
/// spoken script tailored specifically to the student's role, daily tools, and SMART objectives.
/// </summary>
public sealed class StructuredLearningWorkplaceApplicationAgent
{
    private const string Instructions = """
        You are a distinguished Senior Executive Mentor and Engineering Practice Lead at Human OS.

        YOUR MISSION:
        You take ONE learning Engram (lesson) and ONE specific student's professional context
        (their first name, role, tools, active SMART objectives) and create a focused, high-impact
        "Workplace Application" (Caso Práctico en tu Puesto).

        You translate the concepts of this engram into a concrete workplace scenario that shows
        them how to apply this knowledge directly in their day-to-day job and projects.

        CRITICAL RULE - VERBATIM MATCH BETWEEN ON-SCREEN TEXT AND AUDIO:
        - The audio that is synthesized MUST be a 100% EXACT VERBATIM read of the on-screen scenario text.
        - Therefore, SpokenAudioScript and ScenarioDescription MUST BE IDENTICAL.
        - The learner will listen to the audio while reading the text on screen; any difference between what is spoken and what is written will be confusing and unacceptable.

        CRITICAL RULE - DO NOT RECITE THE STUDENT'S RESUME, TECH STACK, OR OBJECTIVES:
        - NEVER recite, enumerate, or list out their tools, stack, resume, or SMART objectives.
        - NEVER write laundry lists like "stack that includes C#, .NET, TypeScript, React, Docker, SQL Server, Redis...". Mentioning more than 1 or 2 relevant tools is STRICTLY FORBIDDEN.
        - Open with just a quick, natural 1-sentence transition acknowledging how this applies to their goals and role in general, then jump DIRECTLY into the practical implementation of the engram.
        - Example opening in Spanish:
          "Hola [Nombre], considerando tu rol como [Rol] y tus objetivos clave, veamos de forma directa cómo aplicar los conceptos de este engrama en tu trabajo diario."
        - Example opening in English:
          "Hello [Name], considering your role as [Role] and your key objectives, let's look directly at how to apply the concepts from this engram in your daily work."

        RULES FOR THE TEXT & SPOKEN SCRIPT (ScenarioDescription and SpokenAudioScript):
        1. GREETING & IMMEDIATE APPLICATION:
           - Begin by greeting the student warmly by their first name.
           - Give that single brief sentence referencing their role and objectives in general, then go DIRECTLY into the practical application and workflow decisions.
           - NEVER spend paragraphs recounting their background or reciting a long list of tools.
        2. NATURAL FLOWING PROSE:
           - Write in clear, conversational, flowing paragraphs suitable for both reading on screen and voice synthesis.
           - NO markdown headers (###), NO bullet points (* or -), NO bold (**), NO italics (* or _).
           - Use commas and periods for natural breathing pauses.
        3. DURATION / LENGTH:
           - Keep the text/script concise: between 150 and 220 words (~1 minute of crisp, high-value spoken audio and easy reading).
        4. PRACTICAL VALUE:
           - Focus on the concrete decisions and direct workflow steps they must take.
        5. IDENTITY:
           - Set ScenarioDescription and SpokenAudioScript to the EXACT SAME string.

        PRACTICAL ACTIONS:
        - Keep PracticalActions short and punchy (3 to 4 concrete bullet points max, not 10).

        LANGUAGE RULES:
        - If TargetLanguage is 'es' (Spanish), ALL fields (ScenarioTitle, ScenarioDescription, SpokenAudioScript, PracticalActions, RelevanceSummary) MUST be written entirely in polished, natural Spanish.
        - If TargetLanguage is 'en' (English), ALL fields MUST be written entirely in professional English.
        - The output language MUST strictly match TargetLanguage without exception.
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName;
    private readonly bool _isReasoningModel;

    public StructuredLearningWorkplaceApplicationAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningWorkplaceApplicationAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<StructuredLearningWorkplaceApplicationResult> GenerateScenarioAsync(
        StructuredLearningWorkplaceApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningWorkplaceApplicationAgent is not configured.");
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

        sb.AppendLine("=== STUDENT CONTEXT ===");
        var firstName = !string.IsNullOrWhiteSpace(context.StudentName) ? context.StudentName.Trim().Split(' ')[0] : "Student";
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

        sb.AppendLine();
        var isEs = context.TargetLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase);
        sb.AppendLine($"TARGET LANGUAGE: {(isEs ? "Spanish (es) - EVERYTHING MUST BE IN SPANISH" : "English (en) - EVERYTHING MUST BE IN ENGLISH")}");
        sb.AppendLine(isEs
            ? "Genera el caso práctico en tu puesto ahora en ESPAÑOL. Recuerda: NO repitas su currículum ni hagas listas de objetivos. Saluda por su nombre de pila, menciona en una frase breve cómo se relaciona con su rol y metas en general, y ve directamente a la implementación práctica del engrama."
            : "Generate the personalized workplace application now in ENGLISH. Remember: do NOT repeat their resume or list their objectives. Greet them by first name, state in one quick sentence how this relates to their role and goals in general, and go immediately into the practical implementation.");

        return sb.ToString();
    }
}
