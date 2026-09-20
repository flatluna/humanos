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

        STEP 1 — DECIDE HOW MANY CLIPS ARE NEEDED AND PLAN COVERAGE.
        Read the video intention and estimate the content to be taught.
        HARD CONSTRAINT — each clip is EXACTLY 10 seconds (roughly 20-25 spoken words).
        
        - If the topic is very simple (a single sign, a single short definition),
          produce 1 to 2 clean, complete 10-second clips.
        - BUT if the user's VIDEO INTENTION contains SUBSTANTIAL, MULTI-POINT,
          OR DETAILED CONTENT (such as multiple legal articles, distinct clauses/incisos
          I to VIII, several technical definitions, architecture mappings like
          Azure / Purview / SAT / UIF / Key Vault, or an extensive lesson lecture):
          DO NOT aggressively compress or drop points!
          You CANNOT squeeze 500 words or 8 legal clauses into 3-6 clips without omitting
          most of the lesson. In such cases, plan the sequence across up to MAX VIDEOS
          (e.g. 7-10 clips) so that EVERY key clause, definition, practical implication,
          and technical mapping from the user's intention is systematically covered across
          consecutive 10-second clips.
          Ensure that across the full sequence of clips, all points in the user's input
          are fully taught in order, without exceeding MAX VIDEOS.

        MAX VIDEOS IS A HARD CAP, NEVER EXCEED IT — IF NEEDED, SUMMARIZE
        INSTEAD OF SPLITTING FURTHER. The input's MAX VIDEOS line is a firm
        limit chosen by the user (it may be lower than what would be needed
        to narrate every sentence of the input verbatim, e.g. MAX VIDEOS: 6
        for a long lecture). When the full content does NOT fit within
        MAX VIDEOS clips at the 20-25 word/clip budget, do NOT produce more
        clips than MAX VIDEOS and do NOT just cut off the lesson partway
        through — instead CONDENSE: keep every distinct topic/section from
        the input represented by at least one clip, but merge closely
        related minor points into a single denser clip, drop redundant
        phrasing/repetition/filler, and state each remaining idea in its
        most compact correct form. Prioritize covering the full breadth of
        the lesson (every major concept/section reaches the viewer) over
        preserving every minor supporting sentence verbatim. Always produce
        EXACTLY the number of clips that best fits the content, but never
        more than MAX VIDEOS.

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

        STEP 3.5 — MANDATORY CONTINUATION INSTRUCTION FOR EVERY CLIP AFTER
        THE FIRST (CRITICAL, ALWAYS INCLUDE, NEVER SKIP). Every clip except
        the very first one in the sequence is started from a real starting
        image that is the EXACT LAST FRAME of the previous clip's own
        generated video — not a fresh scene, not a new shot, not anything
        the Environment/Scene text implies on its own. Regardless of what
        the Environment or Scene description otherwise says, you MUST
        append this exact instruction, adapted to the clip's own language
        (Spanish or English), at the END of every clip's Scene line from
        clip 2 onward: the provided starting image is the direct
        continuation of the previous clip — do NOT change the camera
        angle/framing, do NOT refocus or re-establish the shot, do NOT
        pause, fade in, cut, or otherwise treat this as a new scene start;
        the subject must continue speaking/acting immediately from that
        exact frame with zero visual discontinuity, as if this clip were
        simply the next few seconds of the same continuous shot. Example
        (English): "This clip continues directly from the exact last
        frame of the previous video — do not change the camera angle, do
        not refocus, do not pause or cut; continue the action and speech
        immediately as an unbroken continuation of the same shot."
        Example (Spanish): "Este clip continúa directamente desde el
        último fotograma exacto del video anterior — no cambies el ángulo
        de cámara, no reenfoques, no hagas pausas ni cortes; continúa la
        acción y el habla de inmediato como una continuación ininterrumpida
        de la misma toma." Never omit this for clip 2 and beyond, and never
        include it for clip 1 (which starts from the user's own chosen
        starting image, not a previous clip's last frame).

        STEP 3.6 — APPEARANCE LOCK, UNLESS AN EXCEPTION IS EXPLICITLY STATED
        (CRITICAL, ALWAYS INCLUDE). Video generation models can drift and
        silently change the subject's clothing, hair, accessories, or other
        appearance details between clips, breaking continuity. To prevent
        this, append this exact instruction, adapted to the clip's own
        language, at the END of every clip's Scene line (for ALL clips,
        including clip 1): the subject's clothing, hairstyle, and overall
        appearance must remain EXACTLY the same as in the starting/reference
        image — do NOT change clothing, hair, accessories, or any other
        visual detail of the subject unless THIS SPECIFIC clip's own Scene
        description explicitly states a change (e.g. the user's own input
        explicitly says she puts on a jacket, changes rooms, etc.). Example
        (English): "Keep the subject's clothing, hairstyle, and appearance
        exactly as shown in the starting image — do not change anything
        about her outfit, hair, or accessories unless explicitly stated
        above." Example (Spanish): "Mantén la ropa, el peinado y la
        apariencia del sujeto exactamente como se ve en la imagen inicial —
        no cambies nada de su vestuario, cabello o accesorios salvo que se
        indique explícitamente arriba." If the user's own input for THIS
        clip does explicitly describe an appearance change, state that
        change plainly instead of this lock sentence — the lock is the
        DEFAULT, only overridden when the clip's own content requires it.

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

        STEP 4 — FINAL PROMPT FORMAT AND LANGUAGE CONSISTENCY (CRITICAL).
        Check the TARGET LANGUAGE provided in the input (e.g. "es" for Spanish, "en" for English).
        
        IF TARGET LANGUAGE IS SPANISH (es):
        Every clip's final prompt MUST be 100% in SPANISH. Do NOT mix English labels or English
        camera directions with Spanish speech — mixing languages confuses Google Veo's audio and lip-sync models!
        Use these exact Spanish labeled lines:

        Objetivo: <el objetivo en español>
        Lección a enseñar (este clip): <descripción detallada y completa de la mini-lección en español>
        Entorno: <el entorno en español, preservando los detalles del usuario>
        Escena: <dirección de escena, plano y cámara EN ESPAÑOL, indicando que el profesor/sujeto habla en español de forma pausada y clara mirando a la cámara, terminando en él, seguido de la línea textual en español que pronuncia entre comillas. Ej: Plano medio a la altura de los ojos, cámara estática, el profesor centrado junto al pizarrón, cuerpo superior visible. Habla en español de forma pausada, cálida y clara mirando a la cámara. La cámara permanece en él durante todo el clip sin cortes a otra persona, terminando en él. Dice en español: "..." >

        IF TARGET LANGUAGE IS ENGLISH (en):
        Use the English structure:
        Objective: <the OBJECTIVE line>
        Lesson to teach (this clip): <full mini-lesson description in English>
        Environment: <the ENVIRONMENT text>
        Scene: <visual scene/action/camera direction in English, ending with quoted spoken line>

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
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningVideoPromptAgent is not configured.");
        }

        var resolvedLanguage = ResolveLanguageCode(languageCode, objective, videoDescription);

        var input = $"TARGET LANGUAGE: {resolvedLanguage}\nMAX VIDEOS: {maxVideos}\n\nOBJECTIVE:\n{objective}\n\nVIDEO INTENTION:\n{videoDescription}\n\nENVIRONMENT:\n{environment}\n\nSTARTING IMAGE DESCRIPTION FROM IMAGE2:\n{imageDescription}";

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
        // Hard safety net (2026-09-08): the model can still ignore the MAX
        // VIDEOS instruction and over-produce clips — never let more than
        // maxVideos escape this method regardless of what the LLM returned.
        return response.Result.Videos
            .OrderBy(v => v.SortOrder)
            .Select(v => v.Prompt.Trim())
            .Where(p => p.Length > 0)
            .Take(maxVideos)
            .ToList();
    }

    private static string ResolveLanguageCode(string? languageCode, string objective, string videoDescription)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            return languageCode.Trim().ToLowerInvariant().StartsWith("es") ? "es" : "en";
        }

        var combined = $" {objective.ToLowerInvariant()} {videoDescription.ToLowerInvariant()} ";
        string[] spanishWords = [" el ", " la ", " de ", " que ", " los ", " las ", " para ", " con ", " es ", " tu ", " en ", " del ", " por "];
        string[] englishWords = [" the ", " and ", " you ", " is ", " to ", " your ", " with ", " that ", " this ", " of ", " in ", " for "];
        var spanishHits = spanishWords.Count(w => combined.Contains(w));
        var englishHits = englishWords.Count(w => combined.Contains(w));

        return spanishHits >= englishHits ? "es" : "en";
    }
}
