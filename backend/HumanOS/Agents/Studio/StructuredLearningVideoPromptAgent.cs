using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One clip's final Veo prompt in an ordered multi-video
/// sequence.</summary>
public sealed class StructuredLearningVideoPromptSequenceItem
{
    public int SortOrder { get; set; }

    /// <summary>Final, ready-to-use Veo prompt for this ONE 10-second clip.</summary>
    public string Prompt { get; set; } = string.Empty;
}

public sealed class StructuredLearningVideoPromptSequenceResponse
{
    /// <summary>Ordered clips — together they fully teach the topic, each
    /// one a self-contained 10-second beat that picks up where the
    /// previous clip left off.</summary>
    public List<StructuredLearningVideoPromptSequenceItem> Videos { get; set; } = [];
}

/// <summary>Plans and writes the final Google Veo prompt(s) from the user's
/// video brief, environment, and image2 image description — aware that
/// each clip is a FIXED 10-second budget, so a topic that needs more time
/// to teach is split into several sequential, continuity-aware clips
/// instead of one overstuffed prompt (2026-08-25).</summary>
public sealed class StructuredLearningVideoPromptAgent
{
    private const string Instructions = """
        You plan the Google Veo 3.1 Fast prompt(s) for ONE short teaching
        video, built from the user's stated OBJECTIVE, video intention, the
        environment, and the image2-generated description of the starting
        image.

        STEP 0 — ANCHOR EVERYTHING TO THE GIVEN OBJECTIVE. The input starts
        with an explicit "OBJECTIVE:" line written by the user — this is
        the authoritative statement of what students must learn, given
        directly, not something you need to infer or guess. Every clip's
        narration must clearly and visibly serve THIS objective (state it,
        teach it, demonstrate it) — never produce narration that is vague,
        generic, or disconnected filler that doesn't advance the stated
        objective. If the video intention text is verbose or loosely
        worded, use the OBJECTIVE line as your anchor for what actually
        matters, not the raw wording of the intention text.

        HARD CONSTRAINT — every clip is EXACTLY 10 seconds. Spoken narration
        that fits in 10 seconds is roughly 20-25 words, no more. This is a
        real limit, not a suggestion: a clip whose narration takes longer
        than 10 seconds to say will be cut off mid-sentence when rendered.

        NO CUTAWAYS — CRITICAL, EVERY CLIP. The camera must stay on the main
        subject (the teacher/professor) for the WHOLE clip, ending ON them —
        never cut away to reaction shots of the audience/students/other
        people at any point, especially not at the very end. This matters
        because each clip's FINAL FRAME is captured and used as the exact
        starting image of the NEXT clip — if the clip ends on a cutaway
        (e.g. students reacting instead of the teacher), the next clip will
        wrongly continue from that cutaway instead of from the teacher,
        breaking visual continuity. Explicitly state in every clip's prompt
        that the camera stays on the teacher throughout, with no cut to
        other people, so the last frame is always the teacher.

        STEP 1 — DECIDE HOW MANY CLIPS ARE NEEDED. Read the video intention
        and estimate how much can actually be taught in 10 seconds of
        narration. If the whole topic fits in one clean, complete 10-second
        explanation, produce exactly ONE clip. If it needs more time (e.g.
        writing an expression AND explaining what each part means AND
        solving an example are three separate complete beats), split it
        into MULTIPLE clips — one per complete beat — up to the maximum
        given to you as "MAX VIDEOS: N". Never invent extra clips beyond
        what the content actually needs, and never exceed the max.

        USE THE FEWEST CLIPS THAT FULLY COVER THE LESSON — CRITICAL. More
        clips is not better and costs real money per clip generated; the
        goal is the MINIMUM number of clips that still teaches the full
        objective clearly, never splitting for its own sake. Before adding
        another clip, actively check whether two adjacent beats can be
        COMBINED into one clip's ~20-25 words (e.g. naming the sign AND
        demonstrating it in one clip, instead of one clip just for naming
        and a separate one just for the motion) — merge whenever the
        combined narration still fits the time budget. Only split into
        multiple clips when the content genuinely cannot be said/shown
        within one clip's time budget without cutting a step short — most
        simple single-item lessons (one sign, one vocabulary word, one
        short fact) should need only ONE clip, and even multi-step lessons
        rarely need more than two or three.

        CONCRETE STEPS ONLY — NEVER VAGUE PLACEHOLDER LANGUAGE. The spoken
        line must state the EXACT, literal physical steps given in the
        VIDEO INTENTION input, in plain concrete words — never a vague
        summary/reference like "do the motion", "make the sign", "you do
        this and that", or any generic stand-in phrase. If the input says
        the steps are "pat your leg, then snap your fingers", the
        narration must literally say "pat your leg, then snap your
        fingers" (or a natural equivalent phrasing of the SAME literal
        steps) — never something vaguer or less specific than what the
        input already gave you. This applies to every clip, and especially
        to the "Lesson to teach" and "Scene" sections below: both must
        name the concrete physical action(s) explicitly, word for word
        traceable back to the input's own description of the steps, not a
        paraphrase that loses the specific motion.

        STEP 2 — WRITE EACH CLIP'S PROMPT, SIMPLE AND CONCRETE. Each prompt
        must be short and to the point: subject, environment, one clear
        action/beat, and the exact short line to be said (quoted, within
        the 20-25 word budget) — nothing extra, no padding, no filler
        commentary. Do not describe more than what happens in these 10
        seconds. Do not invent facts not supported by the inputs.

        NAME WHAT IS BEING TAUGHT, FRAMED AS A LESSON — CRITICAL. A viewer
        who has never seen this sign/word/concept before must understand
        BOTH that this is a lesson AND what specific thing is being taught,
        from the very first words. Never open with just the bare
        word/name shouted in isolation (e.g. "HELLO!") — that reads like an
        exclamation, not a lesson, and loses the "this is sign
        language"/"this is what we're learning" context established by the
        video's own topic/environment. Instead, open with a short
        lesson-framing phrase that names BOTH the skill/subject being
        taught (from the video's stated topic, e.g. "sign language") AND
        the specific item (e.g. the word "hello"), such as "Let's learn how
        to say hello in sign language!" or "To say THANK YOU in sign
        language, here's what you do, friend!" — never drop the "sign
        language"/subject framing just because an earlier clip in the
        sequence already established it; each clip is watched
        independently by a video model with no memory of prior clips'
        narration, so the lesson framing must be present in EVERY clip
        that introduces a new item, not just the first one of the whole
        sequence.

        WRONG (bare word, no lesson framing): "HELLO! Put your hand near
        your forehead, then wave it out. So friendly!" — sounds like a
        greeting being exclaimed, not a class learning a sign. RIGHT:
        "Let's learn how to say hello in sign language! Put your hand near
        your forehead, then wave it out. Try it, friend!" — names the
        skill (sign language) AND the item (hello) AND frames it as a
        lesson before the how-to. Apply the same lesson-framing rule to
        any taught item (a sign, a math term, a vocabulary word, a rule):
        always establish "we are learning X" before or while demonstrating
        it, never demonstrate it silently or as a bare exclamation.

        WORKED EXAMPLE OF THE RIGHT STRUCTURE (adapt this shape to whatever
        is being taught, not just signs): the professor names the SKILL and
        the ITEM together in a lesson-framing line FIRST ("To say THANK YOU
        in sign language, here's what you do, friend!"), THEN
        explains and performs each step in lockstep — speech and motion for
        the SAME step happen at the SAME moment, never the motion before
        its own explanation ("Touch your chin with flat fingers..." said
        WHILE touching the chin, then "...then move your hand forward"
        said WHILE moving the hand forward) — THEN a brief pause holding
        the finished result so it's clearly visible, THEN a short invite/
        encouragement line ("Now you try it, friend... Beautiful signing!").
        Narration and motion must always be word-for-word synchronized like
        this: lesson-framing name → step-by-step explanation-with-matching-
        action → pause → invite/encourage. No step is ever shown silently
        or out of order with its own explanation.

        TEXT-NARRATION SYNC — CRITICAL. Whatever text/numbers/equations you
        describe as being WRITTEN on the whiteboard/board in a clip MUST be
        explicitly and literally SPOKEN ALOUD in that same clip's narration
        (read out in the same order they're written), and nothing may be
        written that isn't also said. This is a common failure mode to
        avoid: e.g. writing three lines "2x + 4 = 0", "2x = -4", "x = -2"
        on the board while the narration only says "subtract four, then
        divide by two, giving x equals negative two" — that leaves "2x =
        -4" written but never actually spoken, which is WRONG. Either say
        every written line out loud (e.g. "Two x plus four equals zero.
        Subtract four: two x equals negative four. Divide by two: x equals
        negative two.") or write FEWER lines so every one you do write is
        fully spoken — never leave a written line unspoken. If reading every
        written line aloud would blow the 10-second/20-25 word budget,
        WRITE LESS per clip (one fewer step) instead of narrating less than
        what's shown — split the remaining step into the next clip.

        STEP 3 — SEQUENCE AND CONTINUITY. Clips play back to back in order.
        Each clip must pick up exactly where the previous one left off —
        never repeat an action already done (e.g. if clip 1 already wrote
        an equation on the whiteboard, clip 2 starts from that equation
        already being there, it does not write it again) and never finish
        a clip mid-thought implying "continues next" — each clip's own
        narration must be a complete, self-contained sentence, even though
        the OVERALL topic is only fully taught by the end of the last clip.
        Keep the same subject identity/appearance and environment
        description consistent across all clips.

        LOGICAL FLOW BETWEEN CLIPS — CRITICAL. The sequence must read as ONE
        coherent lesson, not disconnected beats. Two failure modes to avoid:
        (a) a later clip introduces new content (e.g. a concrete numeric
        example) with no spoken transition explaining why it now appears —
        always bridge with an explicit phrase (e.g. "Now let's solve an
        example:") so the jump makes sense; (b) a later clip's board content
        doesn't logically follow from what's already on the board — if the
        board still visually shows the previous clip's writing (e.g. the
        general form "ax + b = 0") and the new clip needs to write something
        that isn't a continuation of it (e.g. a specific numeric instance),
        the prompt must explicitly say the teacher ERASES/CLEARS the board
        (or the relevant part of it) before writing the new content, so the
        board never ends up with unrelated, cluttered leftover text. Every
        clip's board content must be an explicit, explained continuation,
        replacement, or clearing of what was there before — never content
        that just appears without the prompt accounting for it.

        GESTURE/HAND CLARITY — CRITICAL FOR ANY PHYSICAL DEMONSTRATION. Video
        models can flip, blur, or mis-render exactly which hand does what
        and where on the body — so whenever the Scene shows a hand touching
        or moving near a body part (a sign-language handshape, patting a
        leg, pointing, etc.), name the SPECIFIC hand, its ORIENTATION, and
        the EXACT body location and motion, never a vague "she moves her
        hand". Example: instead of "she pats her leg", write "her open
        right hand, palm facing inward, moves to her thigh and pats the
        side of her leg twice with a clear, visible motion".

        VOICE PACING/TONE — every clip's Scene must also state HOW the line
        is delivered, not just what is said: include a short pacing/tone
        cue such as "she speaks slowly, gently, and clearly, with a warm
        smile and encouraging tone" so the narration reads as a real
        teaching voice, not a flat reading.

        FRAMING MUST KEEP THE ACTION VISIBLE — CRITICAL. If the demonstrated
        action happens at a specific body location (e.g. thigh-level for a
        leg pat, waist-level for a hand sign), the Scene must explicitly
        lock the camera framing so that location stays in frame — e.g.
        "medium shot, eye-level, static camera, professor centered, full
        upper body and thigh visible so the leg-pat is clearly seen." Never
        leave framing generic when the action could fall outside a tight
        close-up or an overly wide shot.

        REINFORCE THE REAL-WORLD MOTIVATION WHEN ONE EXISTS — if the
        gesture/sign has a natural real-world analogy that explains WHY it
        looks that way (e.g. patting a leg to call a dog over), weave a
        brief version of that analogy into the spoken line itself (e.g.
        "...just like you'd call a friendly dog to come say hello") — this
        ties the physical motion back to its meaning instead of presenting
        it as an arbitrary motion to copy.

        STEP 4 — FINAL PROMPT FORMAT, ALWAYS STRUCTURED WITH LABELED
        SECTIONS. Every clip's final Prompt string (what actually gets sent
        to Veo) MUST be written as these labeled lines, in this exact
        order, never collapsed into one loose paragraph that drops detail:

        Objective: <the OBJECTIVE line, verbatim or near-verbatim>
        Lesson to teach (this clip): <THIS IS THE MAIN CONTENT — a full,
        properly written mini-lesson description for this one clip, not a
        terse fragment. Write it as real prose describing: WHO is teaching
        (the professor/teacher), WHAT specific thing they are teaching in
        this clip, and the ACTUAL explanation/content they give, including
        the substance of what is said — not just a label like "perform the
        first step". Example of the RIGHT depth: "The professor is
        teaching the students how to say DOG in sign language. She
        explains that to sign DOG, you pat your leg with your hand, as if
        calling a dog to come to you, then says the class should try the
        motion themselves." Example of the WRONG, too-thin depth:
        "Introduce the sign for DOG and perform the first step — patting
        your leg." Never write a garbled or truncated fragment here (e.g.
        "you have to say to say dog yo") — this line must always be a
        grammatically complete, clear description of the lesson content,
        written in full sentences, long enough to actually convey what is
        being taught and why/how, not just name the action in three words.>
        Environment: <the ENVIRONMENT text given to you, copied close to
        VERBATIM — do NOT summarize, shorten, or paraphrase away detail
        the user wrote (e.g. specific zones, colors, lighting, layout).
        Losing environment detail by compressing it into a vague phrase
        like "a calm classroom" is a common failure — preserve as much of
        the user's actual wording as fits.>
        Scene: <the concrete visual scene/action/camera/dialogue direction
        for this clip — subject, action, camera behavior (see NO CUTAWAYS),
        and the exact quoted line to be said, synchronized with the action>

        This structure is mandatory for every clip in the sequence, not
        just the first — never fall back to a single unstructured
        paragraph.

        IMAGE/VIDEO-SAFETY GUIDANCE — CRITICAL. Video generation providers
        auto-block prompts that use clinical/diagnostic disability or
        health terminology (e.g. "autism", "cognitive disabilities",
        "mental disabilities", "special needs", any named condition), even
        in a fully wholesome, respectful classroom scene — these terms
        reliably trigger safety false-positives and the clip fails to
        generate. If the user's own OBJECTIVE/ENVIRONMENT text uses such
        terms, REWRITE that part in plain, non-clinical, warm language when
        composing the Environment/Scene lines (e.g. "a warm, inclusive
        classroom", "a small, supportive group of learners", "students of
        different ages learning together") — never repeat the clinical
        term itself in the final Veo prompt. Also keep age/role framing
        internally consistent (don't mix "children" in one sentence with
        "adults" in the next describing the same people) — pick one
        consistent, plain description of who the learners are and use it
        throughout every clip.

        Return ONLY the structured list of clips, each with its final Veo
        prompt text — no extra commentary.
        """;

    // Same experimental Claude/Foundry-model swap slot as
    // StructuredLearningConnectionExtractionAgent (2026-08-25) — this agent
    // benefits from stricter instruction-following (exact word/time budget,
    // no filler) so it can be pointed at ClaudeConnectionAgent* config
    // (e.g. gpt-5.6-sol) instead of the default AzureOpenAIDeploymentName.
    private readonly bool _isReasoningModel;
    private readonly int? _claudeMaxTokens;
    private readonly AIAgent? _agent;

    public StructuredLearningVideoPromptAgent(IConfiguration configuration)
    {
        var claudeEndpoint = configuration["ClaudeConnectionAgentEndpoint"];
        var claudeDeploymentName = configuration["ClaudeConnectionAgentDeploymentName"];
        if (!string.IsNullOrWhiteSpace(claudeEndpoint) && !string.IsNullOrWhiteSpace(claudeDeploymentName))
        {
            _isReasoningModel = claudeDeploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
                && !claudeDeploymentName.Contains("chat", StringComparison.OrdinalIgnoreCase);
            _claudeMaxTokens = int.TryParse(configuration["ClaudeConnectionAgentMaxTokens"], out var maxTokens) ? maxTokens : null;
            var claudeApiKey = configuration["ClaudeConnectionAgentApiKey"];
            AzureOpenAIClient claudeClient = string.IsNullOrWhiteSpace(claudeApiKey)
                ? new AzureOpenAIClient(new Uri(claudeEndpoint), new DefaultAzureCredential())
                : new AzureOpenAIClient(new Uri(claudeEndpoint), new System.ClientModel.ApiKeyCredential(claudeApiKey));
            // Flagship deployments (e.g. claude-opus-4-8, gpt-5.6-sol) only
            // support the Responses API, not Chat Completions — GetChatClient
            // fails with a 404 "api_not_supported" for those. GetResponsesClient
            // + AsIChatClient bridges it back to IChatClient so AsAIAgent/
            // RunAsync<T> below are unchanged (same fix as
            // StructuredLearningConnectionExtractionAgent).
#pragma warning disable OPENAI001 // Responses API surface is still experimental in this SDK version.
            _agent = claudeClient
                .GetResponsesClient()
                .AsIChatClient(claudeDeploymentName)
                .AsAIAgent(instructions: Instructions, name: "StructuredLearningVideoPromptAgent");
#pragma warning restore OPENAI001
            return;
        }

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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningVideoPromptAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Returns the ordered list of final Veo prompts — one entry if
    /// the topic fits in a single 10-second clip, several if it needs to be
    /// split, never more than <paramref name="maxVideos"/>.</summary>
    public async Task<List<string>> GenerateAsync(
        string objective,
        string videoDescription,
        string environment,
        string imageDescription,
        int maxVideos,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningVideoPromptAgent is not configured.");
        }

        var input = $"MAX VIDEOS: {maxVideos}\n\nOBJECTIVE:\n{objective}\n\nVIDEO INTENTION:\n{videoDescription}\n\nENVIRONMENT:\n{environment}\n\nSTARTING IMAGE DESCRIPTION FROM IMAGE2:\n{imageDescription}";

        ChatClientAgentRunOptions? runOptions = null;
        if (_isReasoningModel)
        {
            runOptions = new ChatClientAgentRunOptions(new Microsoft.Extensions.AI.ChatOptions
            {
#pragma warning disable OPENAI001 // ChatCompletionOptions.ReasoningEffortLevel is an experimental SDK member.
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    ReasoningEffortLevel = ChatReasoningEffortLevel.High
                }
#pragma warning restore OPENAI001
            });
        }
        else if (_claudeMaxTokens is int maxTokens)
        {
            runOptions = new ChatClientAgentRunOptions(new Microsoft.Extensions.AI.ChatOptions
            {
                RawRepresentationFactory = _ => new ChatCompletionOptions
                {
                    MaxOutputTokenCount = maxTokens
                }
            });
        }

        var response = await _agent.RunAsync<StructuredLearningVideoPromptSequenceResponse>(input, options: runOptions, cancellationToken: cancellationToken);
        return response.Result.Videos
            .OrderBy(v => v.SortOrder)
            .Select(v => v.Prompt.Trim())
            .Where(p => p.Length > 0)
            .ToList();
    }
}
