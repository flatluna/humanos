using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// AI editor for traditional-course chapter/lesson/card text. Reuses the
/// same Azure OpenAI AIAgent pattern as the other Studio review agents and
/// returns a single rewritten string to be persisted by the corresponding
/// TraditionalCourseReviewService.
/// </summary>
public sealed class TraditionalCourseReviewAgent
{
    private const string Instructions = """
        You are the Human OS Studio editor for a traditional-course lesson.

        The reviewer is asking you to rewrite a single piece of course content
        for a traditional-course chapter/lesson/card. The request may target the
        text, the image/illustration, or both.

        RULES:
        - Follow the reviewer's instruction exactly. If they say improve, rewrite,
          replace, add, shorten, change tone, insert wording, or provide a new
          paragraph, do that. Do not silently preserve the old wording when the
          user asks for a direct replacement.
        - Keep the content grounded in the course material and do not invent
          new facts, policies, examples, or claims unless the reviewer explicitly
          asks for them.
        - If the reviewer is asking for a direct replacement or gives a full
          paragraph to use, return that new text as UpdatedText. Do not keep
          earlier wording unless the user explicitly says to keep it.
        - If the reviewer asks to change the image, illustration, scene, or visual
          composition, set IllustrationPrompt to a short but concrete prompt for a
          new learning illustration. Keep it focused on the requested change (for
          example: more people, a different scene, a warmer classroom, etc.) while
          staying faithful to the course topic.
        - If the reviewer is only changing text, leave IllustrationPrompt empty.
        - If you are rewriting a title, keep it short, concrete, and clear.
        - If you are rewriting lesson or card content, keep it readable,
          instructional, and in the same language as the current text unless the
          reviewer's instruction explicitly requires a different tone or wording.
        - Do not add markdown, headings, or extra narrative commentary.
        - Return only the updated text in the UpdatedText field and, when relevant,
          a concrete illustration prompt in the IllustrationPrompt field.
        """;

    private readonly AIAgent? _agent;

    public TraditionalCourseReviewAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

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
            .AsAIAgent(instructions: Instructions, name: "TraditionalCourseReviewAgent");
    }

    public bool IsConfigured => _agent is not null;

    public sealed class TraditionalCourseReviewResponse
    {
        public string UpdatedText { get; set; } = string.Empty;

        /// <summary>
        /// Optional image-generation prompt for an illustration update. Leave null
        /// when the reviewer's instruction is purely textual.
        /// </summary>
        public string? IllustrationPrompt { get; set; }
    }

    public async Task<TraditionalCourseReviewResponse> RewriteAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("TraditionalCourseReviewAgent is not configured.");
        }

        var response = await _agent.RunAsync<TraditionalCourseReviewResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
