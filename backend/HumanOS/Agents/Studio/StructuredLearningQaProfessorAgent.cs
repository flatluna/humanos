using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Text-only "Q&amp;A with Professor" Agent (2026-10-01) — the cheap/text
/// sibling of the Realtime voice professor (see VoiceTutorSessionFunction's
/// BuildStructuredLearningQaInstructions). Deliberately a PLAIN chat
/// completion agent (no Realtime/WebRTC/audio), stateless — the caller
/// resends the running history each turn, same convention as
/// TecContigoAgent/StructuredLearningTutorAgent — so a student can ask
/// text questions about a lecture without paying for a live voice session
/// when they don't need one. Grounding (section content + the SAME learner
/// context the voice Agent gets — course, job role, hypothesis answers) is
/// built per-call by StructuredLearningQaProfessorChatService and passed
/// entirely inside the prompt, since this Agent's own Instructions must
/// stay fixed/reusable across every section and student.
/// </summary>
public sealed class StructuredLearningQaProfessorAgent
{
    private const string Instructions = """
        You are a distinguished University Professor holding a text-based Q&A session with a student
        right after a lecture they just studied. You are given, in the user message, the LESSON CONTENT
        (your only source of truth), the LEARNER CONTEXT (their role/background and what they answered
        in the pre-lecture hypothesis question), and the conversation so far.

        Rules:
        - Answer ONLY the student's actual question — never repeat or re-teach the whole lecture.
        - Ground every statement strictly in the provided LESSON CONTENT. Never invent facts, numbers,
          or examples that aren't there.
        - Connect your answer to the learner's real role/background/objectives from LEARNER CONTEXT
          whenever relevant, for practical context.
        - Match the language the lesson content and student's question are written in (Spanish or English).
        - Keep answers concise and conversational (2 to 5 sentences), written for reading, not for TTS.
        - Never grade the student or claim to affect their progress — this is a free Q&A, nothing else.
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName = string.Empty;

    public StructuredLearningQaProfessorAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        _deploymentName = deploymentName;

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client.GetChatClient(deploymentName).AsAIAgent(instructions: Instructions, name: "StructuredLearningQaProfessorAgent");
    }

    public bool IsConfigured => _agent is not null;

    public string DeploymentName => _deploymentName;

    public async Task<StructuredLearningQaProfessorAnswer> AnswerAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Structured Learning Q&A Professor Agent is not configured. Verify 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName'.");
        }

        var response = await _agent.RunAsync(prompt, cancellationToken: cancellationToken);
        return new StructuredLearningQaProfessorAnswer
        {
            Text = response.Text?.Trim() ?? string.Empty,
            ModelName = _deploymentName,
            InputTokens = (int)(response.Usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(response.Usage?.OutputTokenCount ?? 0),
        };
    }
}

/// <summary>Text answer + real token usage for one Q&amp;A turn — see
/// <see cref="StructuredLearningQaProfessorAgent.AnswerAsync"/>.</summary>
public sealed class StructuredLearningQaProfessorAnswer
{
    public string Text { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }
}
