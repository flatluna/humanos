using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output: the Teaching step's narration after syncing it to a
/// regenerated illustration.</summary>
public sealed class SyncLessonNarrationToIllustrationResponse
{
    /// <summary>The narration text, unchanged in every pedagogical respect except
    /// for the literal scene-descriptive sentences that now match the new
    /// illustration. If nothing in the narration described a visual scene in the
    /// first place, this is IDENTICAL to the input Content.</summary>
    public string UpdatedContent { get; set; } = string.Empty;
}

/// <summary>
/// StructuredLearningLessonNarrationSyncAgent (2026-11-11) — a narrow, surgical
/// escape hatch for when a reviewer regenerates the Teaching step's single
/// "Learn" illustration (see StructuredLearningProfessorLectureService
/// .RegenerateLessonIllustrationAsync) with instructions that change the
/// VISUAL SCENE (e.g. "change the room to an office", "make the professor a
/// woman") — the spoken narration/audio was originally written to describe
/// that exact old scene, so after the image changes they go out of sync
/// (the professor's voice describes a classroom the image no longer shows).
///
/// This agent's ONLY job is to patch the narration's own scene-descriptive
/// sentences (the physical setting, who is present/their described
/// appearance, objects, what's shown on a screen/board, gestures) to match
/// the new illustration's instructions — it must NEVER touch, shorten,
/// reword, or remove any actual pedagogical content (concepts, objectives,
/// explanations, examples). A poor/low-quality regenerated image is still
/// never a reason to degrade the lesson's substance — if there's nothing
/// visual to patch, it returns the Content completely unchanged.
/// </summary>
public sealed class StructuredLearningLessonNarrationSyncAgent
{
    private const string Instructions = """
        You are a narrow text-patching tool inside Human OS Studio's review
        tools. A reviewer just regenerated the single illustration shown
        alongside a recorded professor lecture's spoken narration, applying
        some instructions that changed the VISUAL SCENE (e.g. the room,
        who's present, what's on a screen, props, gestures). The narration
        was written to describe the OLD scene, so it may now contain
        sentences that visually contradict the NEW image.

        You will receive: the narration's CURRENT full text, and the
        instructions that were just applied to regenerate the image.

        YOUR ONLY JOB: rewrite ONLY the literal scene-descriptive sentences/
        phrases in the narration (the physical setting/location, who is
        described as present and their appearance, objects/props, what's
        shown on a screen or whiteboard, gestures) so they match the NEW
        image instructions instead of the old scene.

        ABSOLUTE RULES — violating any of these is a failure:
        - NEVER remove, shorten, simplify, reword, or weaken any actual
          teaching content: concepts, learning objectives, explanations,
          definitions, examples, transitions, or the closing synthesis must
          all remain EXACTLY as they are, word for word.
        - Do NOT "improve" or editorialize the lesson content itself — only
          touch sentences that describe what is physically/visually present
          in the scene.
        - If the image instructions don't actually contradict anything in
          the narration (e.g. the narration never described a concrete
          visual scene to begin with, or the change is purely stylistic/
          colors with nothing in the text to contradict), return the
          Content TRULY UNCHANGED, character for character.
        - Never invent new scene details the image instructions don't
          support — if the instructions say "make it an office", you may
          say "office" where the text said a classroom, but don't invent
          unrelated office props/people not implied by the instructions.
        - Preserve the exact language (Spanish/English/etc.), tone, and
          spoken-lecture cadence (conversational, TTS-ready, no markdown,
          no bullet points) of the original narration.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public StructuredLearningLessonNarrationSyncAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];
        _modelName = deploymentName ?? string.Empty;

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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningLessonNarrationSyncAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<string> SyncNarrationAsync(string currentContent, string imageInstructions, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningLessonNarrationSyncAgent is not configured.");
        }

        var prompt =
            $"Narration's CURRENT full text:\n{currentContent}\n\n" +
            $"Instructions just applied to regenerate the image (the new scene these describe):\n{imageInstructions}";

        var response = await _agent.RunAsync<SyncLessonNarrationToIllustrationResponse>(prompt, cancellationToken: cancellationToken);
        return response.Result.UpdatedContent;
    }
}
