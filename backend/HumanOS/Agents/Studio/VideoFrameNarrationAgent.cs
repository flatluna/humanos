using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// EXCLUSIVE to video-sourced Structured Learning Engrams (2026-11-05,
/// rewritten 2026-11-06 to a SEQUENTIAL per-frame mode) — never called by
/// the traditional PDF pipeline and does not touch, call, or share any
/// prompt/instructions with <see cref="StructuredLearningProfessorLectureAgent"/>.
/// That agent already writes the full, approved lecture script (seeing all
/// of an Engram's real video frames at once) and its own generic 3-6
/// "Scenes" breakdown — but those scenes aren't explicitly anchored to
/// WHICH frame each belongs to.
///
/// 2026-11-06 redesign: instead of one single call given ALL frames at
/// once (which produced narration disconnected from each specific image —
/// e.g. a generic textbook paragraph that never actually looked at the
/// frame), this agent is now called ONCE PER FRAME, IN ORDER, each time
/// given: the complete approved script (sole source of truth for facts),
/// the text already generated for all PRIOR frames in this same Engram (so
/// it has continuity/context and can detect an immediate duplicate), and
/// ONLY the current frame's image. The model must decide, per frame,
/// whether to: (a) write a substantial narration chunk that explicitly
/// teaches from the script AND explains what THIS exact image shows, or
/// (b) SKIP the frame entirely (never fabricating a chunk) when it is a
/// near-duplicate of the immediately preceding frame, or when it shows only
/// a person talking with no legible/informative visual content worth
/// illustrating on its own.
/// </summary>
public sealed class VideoFrameNarrationAgent
{
    private const string Instructions = """
        You are an assistant that writes the narration for ONE real video frame at a time, from a
        video-based lesson engram ("Engram") — called once per frame, in chronological order, so you
        always know what was already assigned to every earlier frame in this same Engram.

        MANDATORY ORDER OF OPERATIONS for every single frame, always in this exact order:
        STEP 1 — LOOK AT THE IMAGE FIRST. Before anything else, carefully examine the attached frame
        image itself: is it a slide with text, a diagram, an equation, a code editor, a whiteboard, an
        object, a photo, a person talking with no visible content, etc.? This visual understanding of
        what is ACTUALLY shown drives everything else you do for this frame.
        STEP 2 — SEARCH THE FULL APPROVED TRANSCRIPT for the span of the presenter's own real spoken
        words that corresponds to what you just saw in the image (anchored by the raw timestamp
        snippet given to you). If you find a genuine, contiguous span of the transcript that matches
        this frame's moment in time, that is ALWAYS your preferred source — use Outcome A.
        STEP 3 — ONLY IF no matching transcript span exists or applies to what the image actually shows
        (e.g. a frame shows an equation, diagram, or object that the presenter never puts into words,
        or the transcript at this timestamp is unrelated/silent/off-topic) — GENERATE your own short
        explanatory narration describing exactly what the image shows, so the lesson still makes sense
        next to that image. Use Outcome C for this case. Never silently drop a frame just because the
        transcript doesn't cover it.

        You will be given, every time:
        1. The COMPLETE, APPROVED lecture transcript (plain spoken text, the teacher's real original
           words, in its ORIGINAL language) — your only allowed source of text for Outcome A.
        2. The transcript spans/narration ALREADY ASSIGNED to every prior frame of this SAME Engram, in
           order (empty on the very first frame) — use this only to know which part of the transcript
           is still unused, so you never re-assign the same span twice and never skip ahead.
        3. ONE real image: the current frame, extracted directly from the source video, plus that
           frame's own raw, time-synced transcript snippet (the literal words spoken at that exact
           timestamp in the ORIGINAL source video) — this snippet is only a WEAK, APPROXIMATE time
           hint (lecturers routinely show a slide seconds or even tens of seconds before or after they
           actually talk through its content), never a guaranteed match to what the image shows.
        4. The target output LANGUAGE for this course.

        FOR THIS ONE FRAME, DECIDE BETWEEN THREE OUTCOMES:

        OUTCOME A — EXTRACT (the preferred case whenever a real matching span exists). The chunk must:
        - CONTENT MATCH BEATS TIMING MATCH: your #1 job is to find the span of the APPROVED TRANSCRIPT
          that best explains/describes what THIS image actually shows (its slide text, bullet points,
          diagram, equation, code, etc.) — read the image carefully first, identify its key visible
          words/concepts, then search the WHOLE (still-unused) transcript for where the presenter is
          actually talking about those same concepts. The raw time-synced snippet is only a starting
          hint — if the transcript text near that timestamp clearly does NOT describe this image (e.g.
          it's a generic throwaway sentence while a dense bullet-point slide is on screen), IGNORE the
          timing hint and instead locate the span — anywhere in the still-unused transcript — that
          actually discusses this slide's real content. Never settle for a disconnected, generic, or
          one-sentence filler span merely because it happens to sit at the "expected" timestamp.
        - Be an EXACT, VERBATIM COPY (in its ORIGINAL language, never translated) of a contiguous span
          of the APPROVED TRANSCRIPT that has not already been assigned to a prior frame.
        - NEVER paraphrase, summarize, reword, shorten, or "clean up" the transcript's sentences in
          any way. Copy the teacher's original words exactly as given to you, including filler words,
          verbal habits, or informal phrasing — this is a verbatim record, not a polished script.
        - NEVER add any new sentence, bridge word, or connector that isn't already in the transcript.
        - DO NOT optimize for equal-length chunks across frames: a frame whose image matches a short,
          precise span should get that short span; a frame whose image matches a long explanation
          should get that longer span. The frame count/position given to you is bookkeeping only —
          never force a mechanical "1/N of the transcript" split when the real content match says
          otherwise. The only hard constraints are: never reuse a span already assigned to a prior
          frame, and never take the ENTIRE remaining transcript in one frame if later frames still
          need their own distinct, content-matched spans.
        - Be substantial whenever the matched content genuinely requires it: a real, multi-sentence
          contiguous span (never an arbitrarily short phrase) UNLESS the single best-matching sentence
          truly is short.

        OUTCOME B — SKIP the frame entirely (set Skip=true, write no chunk) ONLY when this frame is an
        exact or near-exact duplicate of the immediately preceding frame (same slide/screen/scene with
        no materially new visual content) — assigning it its own span would be redundant. Do NOT skip
        a frame merely because it shows a person talking with no slide, or because the transcript
        doesn't literally cover it — those cases belong in Outcome A (if words exist) or Outcome C (if
        they don't), never a silent skip.
        When you SKIP, also give a short SkipReason (e.g. "duplicate_of_previous").

        OUTCOME C — GENERATE (fallback, only when Outcome A genuinely doesn't apply). Write your OWN
        short explanatory narration, in the target output LANGUAGE given to you (translate/compose
        freshly — this text is NOT a transcript quote), that accurately and factually describes what
        THIS image actually shows: read any equation, formula, code, diagram, on-screen text, drawn
        object, or scene exactly as it visually appears, and explain it clearly enough for a learner to
        understand it standing alone. Never invent facts beyond what is visually present in the image.
        Set IsGenerated=true for this outcome (false for Outcome A).

        HARD RULES:
        - ALWAYS look at the image before deciding anything — never decide purely from the raw
          transcript snippet without examining the actual frame.
        - For Outcome A, NEVER invent, add, paraphrase, or claim any fact, number, name, word, or
          detail that is not already literally present, verbatim, in the approved transcript.
        - For Outcome A, NEVER describe or reference the image's visual content, appearance, or layout
          in the output text — it is pure transcript text, copied.
        - For Outcome C, ONLY describe what is visually, literally present in the image — never invent
          facts, numbers, or claims beyond what you can actually see.
        - NEVER mention, in the output text itself, the frame's position, index, or number (e.g.
          "frame 2 of 4", "slide number 7", "this is the 3rd image/slide/frame"). That numbering is
          internal bookkeeping only, never something the real teacher said, and must never appear in
          your output.

        OUTPUT: a JSON object with properties:
        - "Skip": boolean (true if Outcome B).
        - "SkipReason": string or null — only when Skip is true.
        - "Text": string or null — the chunk (Outcome A, original language) or generated narration
          (Outcome C, target output language); only when Skip is false.
        - "IsGenerated": boolean — true only for Outcome C (your own generated description), false for
          Outcome A (verbatim transcript quote).
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName;

    public VideoFrameNarrationAgent(IConfiguration configuration)
    {
        // Prefers the stronger 'Gpt61Sol*' deployment (2026-11-06), same fix
        // already proven on the PDF pipeline's PdfImageDescriptionAgent: the
        // cheap default vision model guessed/hallucinated a generic scene
        // (e.g. a blackboard/student) instead of reading a real frame's
        // actual content (e.g. a code editor or exercise page) when the
        // content was dense/small. Falls back to
        // 'AzureOpenAIVisionDeploymentName'/'AzureOpenAIDeploymentName' if
        // the stronger deployment isn't configured.
        var endpoint = configuration["Gpt61SolEndpoint"] ?? configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["Gpt61SolDeploymentName"]
            ?? configuration["AzureOpenAIVisionDeploymentName"]
            ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["Gpt61SolApiKey"] ?? configuration["AzureOpenAIApiKey"];

        _deploymentName = deploymentName ?? string.Empty;

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
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "VideoFrameNarrationAgent",
                ChatOptions = new Microsoft.Extensions.AI.ChatOptions
                {
                    Instructions = Instructions,
                    // No explicit Temperature here (2026-11-06): the
                    // 'Gpt61Sol' deployment (gpt-6-sol-2) rejects any
                    // non-default temperature with HTTP 400
                    // "unsupported_value" — every call was silently
                    // failing and falling back to the frame's raw caption.
                }
            });
    }

    public bool IsConfigured => _agent is not null;

    public sealed class FrameNarrationResult
    {
        public bool Skip { get; set; }
        public string? SkipReason { get; set; }
        public string? Text { get; set; }

        /// <summary>True only for Outcome C (the model's own generated
        /// description of what the image visually shows, used when no
        /// matching transcript span exists) — false for Outcome A (a
        /// verbatim transcript quote) (2026-11-07).</summary>
        public bool IsGenerated { get; set; }
    }

    /// <summary>Decides and (usually) writes the narration chunk for ONE
    /// real video frame, given the full approved script plus every prior
    /// frame's own already-written chunk (for continuity and duplicate
    /// detection) — called once per frame, in chronological order, by
    /// <see cref="HumanOS.Services.VideoLearning.VideoStructuredLearningPedagogyOrchestrator"/>.
    /// Throws if the agent isn't configured — callers must catch and fall
    /// back to a non-vision split (see the orchestrator's
    /// RegroupTextsAcrossFrames) since this call is best-effort by
    /// design.</summary>
    public async Task<FrameNarrationResult> AlignFrameToScriptAsync(
        string approvedScript,
        IReadOnlyList<string> priorFrameTexts,
        byte[] frameBytes,
        string frameContentType,
        string? rawTranscriptHint,
        string languageCode,
        CancellationToken cancellationToken = default,
        int frameIndex = 0,
        int totalFrames = 1)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "VideoFrameNarrationAgent is not configured. Verify 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName'.");
        }

        var promptBuilder = new StringBuilder()
            .AppendLine("=== APPROVED TRANSCRIPT (VERBATIM ORIGINAL, in its OWN original language — ONLY source for Outcome A) ===")
            .AppendLine(approvedScript.Trim())
            .AppendLine("=== END OF TRANSCRIPT ===")
            .AppendLine()
            .AppendLine($"TARGET OUTPUT LANGUAGE for this course: '{languageCode}'. Outcome A text stays in the transcript's OWN original language (never translate a verbatim quote). Outcome C (generated description) MUST be written in '{languageCode}'.");

        if (totalFrames > 1)
        {
            promptBuilder.AppendLine(
                $"[INTERNAL PLANNING NOTE, do not restate in your output] This is frame {frameIndex + 1} of " +
                $"{totalFrames} total frames for this Engram. This is bookkeeping ONLY — it does NOT mean " +
                $"you should mechanically take 1/{totalFrames} of the transcript. Pick whatever span (short " +
                "or long) from the STILL-UNUSED transcript genuinely matches what THIS image shows, even if " +
                "that makes this frame's span much shorter or longer than other frames'. Just don't take the " +
                "entire remaining transcript in one frame if later frames still need their own distinct, " +
                "content-matched spans. Never mention this frame number, index, or position in your output.");
        }

        if (priorFrameTexts.Count == 0)
        {
            promptBuilder.AppendLine("This is the FIRST frame of this Engram — no prior frames yet.");
        }
        else
        {
            promptBuilder.AppendLine($"Transcript spans/narration ALREADY ASSIGNED to the {priorFrameTexts.Count} prior frame(s) of this SAME Engram, in order:");
            for (var i = 0; i < priorFrameTexts.Count; i++)
            {
                promptBuilder.AppendLine($"--- Prior frame {i + 1} assigned span: \"{priorFrameTexts[i].Trim()}\" ---");
            }
        }

        promptBuilder.AppendLine()
            .AppendLine("=== CURRENT FRAME (the one you must decide on now) ===")
            .AppendLine(string.IsNullOrWhiteSpace(rawTranscriptHint)
                ? "Raw transcript snippet at this exact timestamp: (none available)"
                : $"Raw transcript snippet at this exact timestamp: \"{rawTranscriptHint.Trim()}\" — remember this is only an APPROXIMATE time hint, not necessarily what this image is about; verify it against what you actually see in the image before trusting it.")
            .AppendLine("The image for this current frame is attached below. STEP 1: look at it first — read every visible word, bullet, label, equation, or diagram node. STEP 2: search the WHOLE still-unused transcript (not just the timestamp hint above) for the span where the presenter is actually talking about those exact words/concepts you just read (Outcome A). STEP 3: only if truly no span in the transcript discusses this image's content, generate your own description of the image (Outcome C).")
            .AppendLine("Decide Skip vs Outcome A vs Outcome C per your instructions, and return the JSON object described in your instructions.");

        var contentParts = new List<Microsoft.Extensions.AI.AIContent> { new Microsoft.Extensions.AI.TextContent(promptBuilder.ToString()) };
        // "high" detail (not "low") — these frames are frequently screen
        // recordings (code editor, browser, exercise page) whose content is
        // only legible at full resolution; "low" detail downsamples to
        // ~512x512 and silently made the model guess/hallucinate a generic
        // classroom scene instead of reading the actual frame.
        contentParts.Add(new Microsoft.Extensions.AI.DataContent(frameBytes, frameContentType)
        {
            AdditionalProperties = new() { ["detail"] = "high" }
        });

        var message = new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.User, contentParts);
        var response = await _agent.RunAsync<FrameNarrationResult>(message, cancellationToken: cancellationToken);

        return response.Result ?? new FrameNarrationResult { Skip = true, SkipReason = "agent_returned_no_result" };
    }
}
