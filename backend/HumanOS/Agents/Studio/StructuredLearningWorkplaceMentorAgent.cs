using System.Collections.Concurrent;
using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

public sealed class StructuredLearningWorkplaceMentorTurnContext
{
    public string CourseTitle { get; set; } = string.Empty;
    public string ChapterTitle { get; set; } = string.Empty;
    public string EngramTitle { get; set; } = string.Empty;
    public string TeachingContent { get; set; } = string.Empty;

    public string StudentName { get; set; } = string.Empty;
    public string RoleTitle { get; set; } = string.Empty;
    public List<string> KeyTools { get; set; } = [];
    public List<string> ActiveObjectives { get; set; } = [];

    public string WorkplaceScenarioTitle { get; set; } = string.Empty;
    public string WorkplaceScenarioText { get; set; } = string.Empty;
    public List<string> PracticalActions { get; set; } = [];

    public string TargetLanguage { get; set; } = "en";
}

public sealed class StructuredLearningWorkplaceMentorChatResponse
{
    public string Reply { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

/// <summary>
/// Dedicated conversational mentor agent for the "In your role" workplace application step.
/// Uses Microsoft Agent Framework with stateful AgentSession to maintain multi-turn chat memory.
/// Grounded in the exact engram, student role/objectives, and the generated workplace scenario + audio script.
/// </summary>
public sealed class StructuredLearningWorkplaceMentorAgent
{
    private const string Instructions = """
        You are the student's personal Professor and Engineering Advisor at Human OS.
        You are in an interactive 1-on-1 text conversation helping the student understand and apply
        their custom "Workplace Application" (Caso Práctico en tu Puesto) for this specific Engram.

        YOUR MISSION:
        Answer the student's follow-up questions, dive deeper into implementation details, give
        concrete code/architecture/process advice, troubleshoot potential roadblocks, and explain
        how to execute the scenario and practical actions in their daily work.

        CONTEXT GIVEN TO YOU:
        - The Engram lesson and core teaching concepts.
        - The student's professional profile (first name, role, key tools, active objectives).
        - The EXACT Workplace Scenario text and practical actions they are looking at on screen.

        CRITICAL CONVERSATION & BREVITY RULES (NEVER DUMP A BOOK):
        1. CONCISE & PUNCHY:
           - NEVER write long monolithic essays, comprehensive manuals, or 5-day step-by-step master plans in one turn.
           - Keep your reply short and digestible: 120 to 180 words maximum (1-2 short paragraphs, or 3-4 concise bullet points).
           - Focus only on the core answer to the immediate question asked.
        2. ASK WHAT TO EXPLORE NEXT:
           - Always conclude your message with a brief, conversational 1-sentence follow-up question offering 2 concrete directions the student might want to dive into next.
           - Example: "Would you like me to share a minimal C# code sample for this validation, or should we focus on setting the confidence thresholds first?"
           - Example (es): "¿Te gustaría que veamos un ejemplo de código breve para esto, o prefieres que revisemos primero los umbrales de confianza?"
        3. PERSISTENT MEMORY & CONTINUITY:
           - You are having a continuous multi-turn dialogue. Build upon previous turns naturally without repeating prior explanations.
        4. GROUNDING:
           - Stay grounded in this engram's principles and the workplace scenario.
        5. LANGUAGE CONSISTENCY:
           - Reply in the same language as the student's question, matching the TargetLanguage preference ('es' or 'en').
        """;

    private readonly AIAgent? _agent;
    private readonly bool _isReasoningModel;
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    public StructuredLearningWorkplaceMentorAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningWorkplaceMentorAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<string> AskAsync(
        string sessionId,
        string userMessage,
        StructuredLearningWorkplaceMentorTurnContext context,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningWorkplaceMentorAgent is not configured.");
        }

        var isNewSession = !_sessions.ContainsKey(sessionId);
        var session = _sessions.GetOrAdd(sessionId, _ => _agent.CreateSessionAsync(cancellationToken).GetAwaiter().GetResult());

        string promptToSend;
        if (isNewSession)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== INITIAL GROUNDING CONTEXT FOR THIS MENTORSHIP SESSION ===");
            sb.AppendLine($"- Course: {context.CourseTitle}");
            sb.AppendLine($"- Chapter: {context.ChapterTitle}");
            sb.AppendLine($"- Engram: {context.EngramTitle}");
            sb.AppendLine();
            sb.AppendLine("--- CORE ENGRAM TEACHING ---");
            sb.AppendLine(context.TeachingContent);
            sb.AppendLine("---------------------------");
            sb.AppendLine();
            sb.AppendLine("=== STUDENT PROFILE ===");
            sb.AppendLine($"- Name: {context.StudentName}");
            sb.AppendLine($"- Role: {context.RoleTitle}");
            if (context.KeyTools.Count > 0)
            {
                sb.AppendLine($"- Tools: {string.Join(", ", context.KeyTools)}");
            }
            if (context.ActiveObjectives.Count > 0)
            {
                sb.AppendLine($"- Active Objectives: {string.Join(" | ", context.ActiveObjectives)}");
            }
            sb.AppendLine();
            sb.AppendLine("=== GENERATED WORKPLACE SCENARIO ON SCREEN ===");
            sb.AppendLine($"Title: {context.WorkplaceScenarioTitle}");
            sb.AppendLine($"Scenario Description (Voice Script): {context.WorkplaceScenarioText}");
            if (context.PracticalActions.Count > 0)
            {
                sb.AppendLine("Practical Actions:");
                foreach (var action in context.PracticalActions)
                {
                    sb.AppendLine($"  * {action}");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"TARGET LANGUAGE: {(context.TargetLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "Spanish (es)" : "English (en)")}");
            sb.AppendLine("=============================================================");
            sb.AppendLine();
            sb.AppendLine($"Student's question: {userMessage}");
            promptToSend = sb.ToString();
        }
        else
        {
            promptToSend = userMessage;
        }

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

        var response = await _agent.RunAsync(promptToSend, session, options: runOptions, cancellationToken: cancellationToken);
        return response?.ToString() ?? string.Empty;
    }
}
