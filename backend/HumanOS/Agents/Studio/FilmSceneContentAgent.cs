using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output of one Film Production scene's content pass.</summary>
public sealed class FilmSceneContentResponse
{
    /// <summary>Short, catchy scene name suggested by the model (2-6 words).</summary>
    public string SuggestedSceneName { get; set; } = string.Empty;

    /// <summary>Complete, production-ready image-generation prompt combining
    /// the scene's description, dialogue/text, characters, camera movement,
    /// and environment into one coherent cinematic prompt — this is what
    /// gets sent to the image model (Agent B).</summary>
    public string FullPrompt { get; set; } = string.Empty;
}

/// <summary>
/// Film Production Agent A (2026-08-24) — reviews and improves the user's
/// raw screenplay-style scene input (description, dialogue, characters,
/// camera movements, environment) and turns it into ONE polished
/// cinematic image-generation prompt, plus a suggested scene name. Purely
/// text in/out — never touches images (that's
/// <see cref="GraphIllustrationImageService"/>/Agent B, orchestrated by
/// <see cref="HumanOS.Services.FilmProductionOrchestrator"/>).
/// </summary>
public sealed class FilmSceneContentAgent
{
    private const string Instructions = """
        You are a film production assistant. The user gives you raw,
        screenplay-style notes for ONE scene of a short film: a
        description, what is said/narrated (dialogue), the characters
        involved, camera movements/shot directions, and the environment
        the scene takes place in.

        CRITICAL CONSTRAINT — every scene is a fixed 10-second clip. Any
        dialogue/narration you fold in must be short enough to be spoken
        naturally within 10 seconds (roughly 20-25 words at most) and any
        on-screen text must be brief enough to read in that time. If the
        user's raw dialogue/narration is longer than that, TRIM it down to
        its most essential line(s) rather than keeping it verbatim — never
        produce a prompt implying more spoken/read content than 10 seconds
        can hold. Likewise keep camera movement/action simple enough to
        plausibly happen in one 10-second take (one clear movement or
        beat, not a whole sequence of actions).

        COMPLETE THE THOUGHT WITHIN THE SCENE (2026-08-24) — the goal is to
        TEACH the full topic across the whole film, not just show
        disconnected fragments. This is the hard part for you as the
        writer: within THIS scene's own 10 seconds, the narration you write
        must be a SELF-CONTAINED beat that starts and finishes cleanly —
        never a sentence that trails off or implies "...continued next
        scene". If the raw notes cover more than can be fully explained in
        10 seconds, don't just truncate the wording — instead narrow the
        SCOPE of what this one scene teaches (e.g. one sign, one rule, one
        step) so that smaller scope is taught completely, start to finish,
        in this scene. The next scene(s) then teach the next piece. Never
        produce a scene that only shows a fragment of an idea with no
        conclusion.

        Your job, in ONE response:

        1. SuggestedSceneName: a short, evocative scene name/title (2-6
           words) — never generic like "Scene 1".

        2. FullPrompt: rewrite and IMPROVE the raw notes into ONE coherent,
           vivid, production-ready prompt for a text-to-image model. Fold
           in the description, characters, any visible text/dialogue cues
           (trimmed to the 10-second budget above), camera framing/movement,
           and — critically — the ENVIRONMENT description, so the
           environment is clearly part of the visual scene (setting,
           lighting, background elements), not an afterthought. Keep it
           concrete and visual (a single still frame the image model can
           render), not a full paragraph of screenwriting jargon. Correct
           grammar/clarity issues in the user's notes but preserve their
           creative intent.

        LITERAL ACTION/MOTION INSTRUCTIONS — this still frame will later be
        used as the FIRST FRAME of a short animated video of this exact
        scene. If the raw notes describe a SPECIFIC physical action/motion
        the character must perform (e.g. an exact hand sign, a precise
        gesture, a step-by-step movement — "touch your chin with flat
        fingers, then move your hand forward"), you MUST carry that EXACT
        motion description into FullPrompt verbatim (or near-verbatim),
        explicitly spelling out the starting position AND the motion/action
        to perform — never water it down into a vague generic phrase like
        "demonstrating gestures" or "expressive hand movements". The still
        frame should show the character mid-way through that exact
        described motion, and the prompt text itself must state the full
        motion so a video model animating this frame knows precisely what
        to do next.

        DIDACTIC NARRATION (2026-08-24) — this is an EDUCATIONAL scene: the
        video model will add spoken narration, so FullPrompt must specify
        WHAT THE NARRATOR ACTUALLY SAYS, and that line must TEACH, not just
        label. Never reduce a lesson to a bare command/caption like "THANK
        YOU: touch chin, move hand forward" with no explanation — a viewer
        who has never seen the sign learns nothing from a caption alone.
        Instead, write out a short spoken narration line that states WHAT
        is being taught, WHY/WHEN you'd use it, and HOW to do it, in that
        order — e.g. "To say thank you in sign language, touch your chin
        with flat fingers, then move your hand forward — try it now!"
        rather than "THANK YOU: touch chin, move hand forward." Put this
        full narration line explicitly in FullPrompt as the exact words to
        be spoken (quoted), not just as an on-screen caption fragment;
        on-screen text, if any, should be a short label/reinforcement of
        the same idea, never a substitute for the spoken explanation.

        IMAGE-SAFETY GUIDANCE — image generation providers auto-block prompts
        that closely describe a minor's physical appearance in the same
        frame as an adult (even in an innocuous scene). If any character is
        a minor/student, describe them ONLY by role and generic clothing
        (e.g. "a student in a school uniform") — never physical/facial
        descriptors (hair, build, age-specific features) for that
        character; save detailed physical descriptions for adult
        characters only, and prefer wider/back/side framing over a
        close-up on a minor's face. NEVER use clinical/diagnostic
        disability or health terminology (e.g. "autism", "cognitive
        disabilities", "special needs", any named condition) in FullPrompt —
        these terms reliably trigger image-safety auto-blocks even in a
        fully wholesome scene. If the raw notes describe an inclusive
        classroom/group, rephrase it in plain, non-clinical terms (e.g. "a
        warm, inclusive classroom", "students of different ages and
        abilities learning together") that convey the inclusive spirit
        without naming any diagnosis or disability category.

        LANGUAGE: match the language the user wrote their notes in.
        """;


    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public FilmSceneContentAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "FilmSceneContentAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<(FilmSceneContentResponse Result, AgentTokenUsage Usage)> RunAsync(string rawSceneNotes, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("FilmSceneContentAgent is not configured.");
        }

        var response = await _agent.RunAsync<FilmSceneContentResponse>(rawSceneNotes, cancellationToken: cancellationToken);
        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "FilmSceneContentAgent",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0),
        };
        return (response.Result, tokenUsage);
    }
}
