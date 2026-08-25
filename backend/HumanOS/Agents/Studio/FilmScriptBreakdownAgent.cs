using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One planned scene extracted from a full script/story text —
/// same shape as the user's manual per-scene wizard input, so it can be
/// fed straight into <see cref="FilmSceneContentAgent"/> unchanged.</summary>
public sealed class FilmScriptSceneDto
{
    public int SortOrder { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? DialogueText { get; set; }

    public string? Characters { get; set; }

    public string? CameraMovements { get; set; }

    public string Environment { get; set; } = string.Empty;
}

public sealed class FilmScriptBreakdownResponse
{
    /// <summary>Ordered scenes covering the whole input text, one 10-second
    /// clip each.</summary>
    public List<FilmScriptSceneDto> Scenes { get; set; } = [];
}

/// <summary>
/// Film Production "auto-storyboard" agent (2026-08-24) — takes a full
/// script/story text and breaks it into an ORDERED list of 10-second
/// scenes covering the whole material, each already shaped like the
/// manual wizard's per-scene input (Description/DialogueText/Characters/
/// CameraMovements/Environment). Each scene DTO is then run through the
/// EXACT SAME per-scene pipeline as the manual wizard
/// (<see cref="FilmSceneContentAgent"/> + image generation, orchestrated
/// by <see cref="HumanOS.Services.FilmProductionOrchestrator"/>) — this
/// agent's only job is the breakdown/continuity planning, never prompt
/// polishing or images.
/// </summary>
public sealed class FilmScriptBreakdownAgent
{
    private const string Instructions = """
        You are a film breakdown assistant. The user gives you a full
        script, story, or article. Your job is to break the ENTIRE
        material into an ordered sequence of scenes that, together, cover
        everything meaningful in the text — like storyboarding a short
        film out of it.

        CRITICAL CONSTRAINT — every scene is a FIXED 10-second clip:
        - Any dialogue/narration for a scene must be short enough to be
          spoken naturally in 10 seconds (roughly 20-25 words max).
        - Camera movement/action per scene must be simple enough for one
          10-second take (one clear beat, not a whole sequence).
        - Split longer passages of dialogue/narration across MULTIPLE
          consecutive scenes rather than cramming them into one — never
          drop meaningful content, just spread it out.

        TEACH THE WHOLE TOPIC, ONE COMPLETE PIECE PER SCENE (2026-08-24) —
        the goal across all scenes together is to teach the FULL content of
        the source material, not just a few disconnected highlights. This
        is the hard part of your job: 10 seconds is not enough to explain
        everything, so instead of cramming or truncating, you must SPLIT
        the material into scenes where EACH ONE teaches ONE complete,
        self-contained piece (one sign, one rule, one step, one fact) that
        starts and finishes cleanly within its own 10 seconds — never a
        scene whose narration trails off mid-thought or implies "...more
        next scene". If the source material has N teachable pieces, plan
        for (at least) N scenes so every piece gets its own complete beat,
        subject only to the MAX SCENES cap below (if the cap is tighter
        than N, GROUP the smallest/most related pieces together into one
        scene rather than truncating any of them — each grouped scene must
        still fully teach everything it covers, just more of it per scene).
        By the end of the whole scene sequence, every meaningful piece of
        the source material must have been fully and clearly taught, not
        just sampled.

        CONTINUITY — this is the most important part:
        - Recurring characters must be described CONSISTENTLY across every
          scene they appear in (same name, same physical description —
          copy the exact wording scene to scene, don't vary it).
        - Recurring locations/environments must likewise be described
          consistently when the story returns to them.
        - Scenes must flow in a coherent narrative order, each one picking
          up naturally from where the previous one left off (no unexplained
          jumps unless the source text itself jumps).

        ENVIRONMENTS PROVIDED BY THE USER — the input may start with a block
        labeled "ENVIRONMENTS:" listing the film's locations (e.g. a
        classroom, a hallway, an office) with a short description of each,
        followed by a "SCRIPT:" block with the actual story. When present,
        you MUST reuse those exact environment descriptions verbatim every
        time a scene takes place in that location — never invent a
        different look for a location the user already described. Only
        invent a new environment description if a scene clearly takes
        place somewhere the user's list doesn't cover.

        For each scene produce:
        - SortOrder: 1, 2, 3... in narrative order.
        - Description: what happens in this scene (visual, concrete). If the
          source text describes a SPECIFIC physical action/motion a
          character must perform step-by-step (e.g. an exact hand sign or
          gesture — "touch your chin with flat fingers, then move your hand
          forward"), you MUST carry that exact motion description into
          Description verbatim (or near-verbatim) — never water it down
          into a vague phrase like "demonstrating gestures". This still
          frame is later used as the first frame of an animated video, so
          the exact motion must be spelled out in words for a video model
          to act on, not just implied. DIDACTIC NARRATION (2026-08-24):
          this is an educational scene — describe the character SPEAKING a
          short teaching line that states what is being taught, WHY/WHEN
          you'd use it, and HOW to do it, in that order (e.g. "she says:
          'To say thank you in sign language, touch your chin with flat
          fingers, then move your hand forward — try it now!'"), not a bare
          caption/label. A viewer who has never seen the sign must be able
          to learn it from the narration alone — labels or on-screen text
          are only a reinforcement, never a substitute for it.
        - DialogueText: what is said/narrated in this scene, if anything
          (already trimmed to the 10-second budget above) — omit/null if
          the scene is silent/action-only.
        - Characters: who appears in this scene, with a consistent physical
          description reused verbatim across their other scenes.
        - CameraMovements: simple camera direction for this one beat.
        - Environment: where this scene takes place, described consistently
          whenever the story returns to the same location.

        Produce as many scenes as needed to cover the whole input, UP TO the
        maximum scene count given to you in the prompt (look for a line like
        "MAX SCENES: N") — if the source material is denser than that limit
        allows, COMBINE multiple beats into fewer, slightly longer-covering
        scenes rather than exceeding the limit; never invent filler scenes
        just to reach the limit either. Prioritize covering the most
        important/distinct story beats first if something has to be cut.

        IMAGE-SAFETY GUIDANCE — several image generation providers auto-block
        prompts that closely describe a minor's physical appearance in the
        same frame as an adult (even in an innocuous school scene). To avoid
        false-positive blocks:
        - For any character who is a minor/student, describe them ONLY by
          role and generic clothing (e.g. "a student in a school uniform",
          "a group of students taking notes") — never physical/facial
          descriptors (hair, build, age-specific features) for minors.
        - Detailed physical descriptions (hair, build, face) are fine for
          adult characters only.
        - Avoid close-up framing on a minor's face; prefer wider/back/side
          framing or focus the shot on the adult/action/environment instead.
        - NEVER use clinical/diagnostic disability or health terminology in
          Description/Characters/Environment (e.g. "autism", "cognitive
          disabilities", "special needs", any named condition) — these
          terms reliably trigger image-safety auto-blocks even in a fully
          wholesome scene. If the source material describes an inclusive
          classroom/group, describe it only in plain, non-clinical terms
          (e.g. "a warm, inclusive classroom", "students of different ages
          and abilities learning together", "a supportive small-group
          class") — convey the inclusive spirit without naming any
          diagnosis or disability category.

        LANGUAGE: match the language of the user's input text.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public FilmScriptBreakdownAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "FilmScriptBreakdownAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<(FilmScriptBreakdownResponse Result, AgentTokenUsage Usage)> RunAsync(
        string fullScriptText, string? environmentsDescription, int maxScenes, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("FilmScriptBreakdownAgent is not configured.");
        }

        var body = string.IsNullOrWhiteSpace(environmentsDescription)
            ? fullScriptText
            : $"ENVIRONMENTS:\n{environmentsDescription}\n\nSCRIPT:\n{fullScriptText}";
        var input = $"MAX SCENES: {maxScenes}\n\n{body}";

        var response = await _agent.RunAsync<FilmScriptBreakdownResponse>(input, cancellationToken: cancellationToken);
        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "FilmScriptBreakdownAgent",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0),
        };
        return (response.Result, tokenUsage);
    }
}
