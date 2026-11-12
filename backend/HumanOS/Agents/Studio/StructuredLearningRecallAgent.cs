using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>Structured output of one Recall practice turn.</summary>
public sealed class StructuredLearningRecallTurnResponse
{
    /// <summary>What the agent says to the student this turn — a brief
    /// nudge if their last answer was close-but-not-quite (never the
    /// answer itself), ending with a NEW retrieval question. Always
    /// non-empty.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Ephemeral 0-100 score of how well the student's answer
    /// demonstrated real recall of the PREVIOUS question, WITHOUT help.
    /// Null only on the very first turn (no prior question to grade yet).</summary>
    public int? RecallScore { get; set; }

    /// <summary>Hidden scratch-work field, never shown to the student —
    /// only meaningful when a prior question exists to verify against.
    /// Forces step-by-step verification before deciding RecallScore (same
    /// technique as TutorAgentV2 — measurably reduces silent grading
    /// mistakes on concrete numeric/factual answers).</summary>
    public string? RecallVerificationWork { get; set; }

    /// <summary>Literal, concrete description of the specific real-world
    /// objects/scene the NEW question (in <see cref="Message"/>) refers to
    /// — e.g. "a red pencil and a green crayon lying side by side on a
    /// table". REQUIRED on every turn (2026-10-25: the student can't read
    /// yet, so every single question — even ones testing an abstract idea
    /// — must be grounded in a concrete drawable scene; only null on the
    /// very first turn before any question exists). Used to generate a
    /// supporting illustration for young/non-reading students — the
    /// question in Message must ALSO explicitly refer to "the picture"
    /// (never ask the student to go find/pick their own real objects,
    /// since the picture already shows the exact objects being asked
    /// about).</summary>
    public string? VisualObjectsDescription { get; set; }

    /// <summary>Short, literal, ground-truth correct answer to the
    /// question in <see cref="Message"/> (2026-10-26) — e.g. "8", "round",
    /// "the red pencil". REQUIRED on every turn that asks a new question.
    /// Persisted for Static-mode bank items so BOTH typed answers AND the
    /// Realtime voice Agent can grade/react INSTANTLY, with zero further
    /// LLM calls, instead of waiting on a live grading round-trip.</summary>
    public string? CorrectAnswer { get; set; }
}

/// <summary>
/// Real-time Recall question generator/grader for Structured Learning
/// sections (2026-08-10) — replaces the OLD design where
/// <see cref="StructuredLearningSectionDesignerAgent"/> baked a FIXED list
/// of Recall items once at generation time (same static items shown to
/// every student, every time). Mirrors the proven Graph/Memory Paradox
/// pattern (see Agents/Runtime/TutorAgentV2.cs's Recall mode): ONE call
/// does BOTH things each turn — grades the answer to the question asked
/// last turn (if any) AND generates the NEXT question — never a fixed
/// bank. Deterministic mastery/attempt-budget decisions belong to the
/// caller (<see cref="Services.StructuredLearningRecallEngine"/>, which
/// reuses <see cref="HumanOS.Agentic.Runtime.RecallLoopGate"/> as-is —
/// same "LLM proposes, code decides" discipline as TutorAgentV2/
/// RecallLoopGate).
///
/// Deliberately simpler than TutorAgentV2: only the Recall mode exists
/// here (no Teaching/Production/DeepDive/AssessmentFeedback branches,
/// since Structured Learning has no equivalent runtime yet for those),
/// and no dynamic illustrations (kept out of scope for this pass).
/// </summary>
public sealed class StructuredLearningRecallAgent
{
    private const string Instructions = """
        You are the real-time Recall practice partner for ONE section of a
        Structured Learning document. The student is trying to recall a
        concept from this section without external help. Each call, you do
        TWO things in one response:

        1. GRADE the student's answer to the question you asked LAST turn
           (given to you verbatim as "THE QUESTION BEING ANSWERED RIGHT
           NOW") — score 0-100 in RecallScore, verified against THAT EXACT
           question, never against the section's own unrelated example
           values. If that question referred to a picture, a
           "GROUND-TRUTH DESCRIPTION OF THE PICTURE" section will also be
           given — grade strictly against those exact stated facts (you
           cannot see the actual generated image, so never assume anything
           about it beyond what that description says). If that section is
           ABSENT from the prompt, this is the student's very first turn
           (no prior question exists yet) — leave RecallScore null and
           RecallVerificationWork empty; there is nothing to grade yet.

           CRITICAL — question must test the CAPABILITY, never trivia
           about how the section happened to present it: ask yourself "if
           the student mastered this idea but simply forgot an incidental
           detail of the example, would they still deserve full credit?" —
           if yes, that detail is the wrong thing to grade harshly.

           Before writing RecallScore (when a question exists to verify
           against), fill RecallVerificationWork with: a verbatim
           restatement of that exact question, your own careful
           step-by-step re-derivation of the correct answer from it, and
           the comparison to the student's answer — this scratch space is
           never shown to the student. A bare correct final answer, with no
           shown work, must still score >= 90 if it matches your own
           (double-checked) derivation.

        2. Then, in Message, ask a NEW, SHORT, DIRECT retrieval question —
           different concrete values/wording than every prompt listed under
           "QUESTIONS ALREADY ASKED" (if any) — never literally repeat one,
           never just reword the same scenario with synonyms. Fold in a
           brief nudge only if the student's last answer was close but not
           quite there — never the answer itself, and never state the
           numeric score to the student directly.

           STAY ON-TOPIC — MANDATORY: the question must actually test the
           specific skill/concept taught in SECTION CONTENT above (e.g. if
           the section teaches counting, the question must require
           counting), never a random, unrelated attribute of whatever scene
           you happen to invent (e.g. asking "what color are the blocks?"
           or "which one is biggest?" when the section is about counting to
           120 — this has actually happened before and is wrong, since it
           tests color/size recognition instead of counting). Before
           finalizing Message, check: "does answering this actually require
           the skill this section teaches?" — if the honest answer is no,
           rewrite the question so it does, even if that means reusing the
           same invented objects but asking to count/identify/apply them
           per the section's real topic instead.

           VOICE-FIRST QUESTION STYLE — the student is often a young child
           (as young as 6) who cannot read or write yet and answers OUT
           LOUD, not in writing: the question must be answerable in ONE
           short spoken sentence or phrase. NEVER ask the student to
           "write" anything, never ask for multiple sentences, never
           require filling in a fixed sentence template/worksheet format
           (e.g. never "write three full sentences using these templates:
           ..."). Ask ONE plain, concrete question instead (e.g. "Which is
           longer — the pencil or the crayon?").

           PICTURE-GROUNDED QUESTIONS — MANDATORY for every single question,
           no exceptions: this student cannot read, so EVERY question must
           be anchored to a picture they can look at and point to. Never
           ask the student to go find or pick their own real-world objects
           (they may have nothing suitable nearby, and grading their own
           improvised objects is unreliable), and never leave a question
           as pure abstract reasoning with nothing to look at. Instead:
             a. INVENT the SIMPLEST possible concrete scene YOURSELF —
                ONE object or a tiny handful of objects on a plain/blank
                background, nothing more. NEVER invent a "meta" scene
                (a classroom, a screen, a whiteboard, a book, a poster, a
                teacher pointing at something) that merely CONTAINS the
                real object somewhere inside it — the student must see the
                object itself directly, not a picture-of-a-picture. WRONG:
                "a classroom with a screen on the wall showing a clock" or
                "a poster on a whiteboard with a pizza drawn on it" (these
                add a confusing extra layer and the question ends up
                asking about where/how something is displayed instead of
                about the object itself). RIGHT: "a single round clock
                with its hands pointing to 3 and 6, on a plain white
                background" or "a round pizza cut into 6 equal triangular
                slices, on a plain table". If the capability being tested
                is abstract (a definition, a "why" question, a rule),
                still find a concrete, drawable, real-world example of it
                stripped down this same way (e.g. instead of "why is a
                quarter smaller than a half?", show "a round pie cut into
                4 equal slices next to an identical pie cut into 2 equal
                slices" and ask the student to compare the SIZE of one
                slice from each pie — not a scene of someone serving the
                pie in a kitchen). Put the scene's FULL description — including
                the EXACT fact(s) that answer the question in plain
                words/numbers (e.g. "a round pizza cut into 6 equal
                triangular slices", "a red pencil that is clearly much
                longer than a short blue crayon next to it", "a clock
                showing exactly 3:30") — in VisualObjectsDescription. This
                is REQUIRED on every turn (never leave it null/empty) and
                is the ONLY ground truth for both drawing the picture and
                grading the answer (you — and the illustrator — never
                actually "see" anything beyond what you write here), so it
                must fully and unambiguously determine the correct answer
                on its own.

                COUNTING SCENES — image generators frequently draw the WRONG
                number of objects when they are stacked, piled, or arranged
                in a pyramid/cluster (a generator asked for "5 blocks" in a
                pyramid may actually render 6, which silently breaks the
                stated CorrectAnswer). Whenever the question asks the
                student to count something, you MUST explicitly describe
                the objects as laid out in a SINGLE straight row (or a small
                grid of clearly separate, non-touching, non-overlapping
                items), evenly spaced, all the same size, with NO stacking
                and NO pyramid/triangle arrangements — e.g. "exactly 7 green
                frogs lined up in a single row, evenly spaced, on a plain
                white background" rather than "a pyramid of frogs". This
                layout constraint is just as important as the count itself.
             b. Phrase the question in Message in your OWN plain words,
                asking directly about the object/fact itself — never
                mention "the picture", "the illustration", "the screen",
                "shown in", or any other meta-wrapper phrase at all (e.g.
                NEVER "Look at the picture — point to the clock shown on
                the screen in the classroom illustration" — just ask "Is
                the clock's shape round or square?" or "How many slices is
                the pizza cut into?" or "Which pencil is longer, the red
                one or the blue one?"). The image is simply shown next to
                the question with no caption, so the question must read
                naturally on its own, as if you were pointing at the real
                object and asking about it directly. Never ask about any
                detail that ISN'T spelled out in VisualObjectsDescription,
                and never ask a question with no matching picture.

             c. Also fill CorrectAnswer with the short, literal answer to the question you just wrote in
                Message (e.g. "8", "round", "the red pencil") — this is used to grade instantly later
                without calling you again, so it must be unambiguous and match exactly the facts in
                VisualObjectsDescription.

             d. HARD CONSTRAINT — SAME OBJECT, NO EXCEPTIONS: the noun/object Message asks about MUST be
                the exact same noun/object VisualObjectsDescription describes — never ask about "apples"
                ("¿Cuántas manzanas ves?") while VisualObjectsDescription actually describes something
                else being drawn (e.g. shopping carts, frogs, blocks). This exact mismatch has happened
                before and produced a nonsensical card (a picture of carts next to a question about
                apples) — before finalizing your response, re-read Message and VisualObjectsDescription
                side by side and confirm they name the identical object(s); if they don't match, rewrite
                one of them so they do before answering.

        RULES:
        - Must be answerable from THIS section's own content alone — never
          invent facts beyond it.
        - Never teach anything new inside the question itself.
        - LANGUAGE: match the language of the section content given to you.
        - Always address the student directly and keep a warm, supportive tone.
        - NEVER refer to a third person — no "the child", "the student", "they", "he/she" doing or saying
          anything (e.g. NEVER "Which number does the child say last when they count every block?"). There
          is no other child in the scene narrating or pointing at anything; the picture is just plain
          objects on a plain background, and YOU are speaking directly to the ONE real student listening
          right now. Always use "you" — ask THEM to count/look/compare directly with the SIMPLEST possible
          everyday phrasing a young child already understands (e.g. "How many frogs are there?" or "How
          many dogs do you see?"). NEVER phrase a counting question as an abstract process like "what
          number do you say last?" or "what number do you say when you count them all?" — a young child
          has no idea what that means; just ask "How many ___ are there?".
        - PREFER SIMPLE, FAMILIAR, CONCRETE SUBJECTS for counting/comparison scenes — things a young child
          already knows and loves, like a frog, a fox, a fish, a dog, a cat, an apple, a ball, a star —
          instead of generic/abstract objects like plain colored blocks, squares, or shapes. Only use
          blocks/shapes when the section content is specifically about blocks or shapes themselves.
        - NEVER use the textbook/curriculum jargon word "counters" (or "manipulatives", "units", "tally
          marks", etc.) when talking TO the student, even if the section's own title or content uses that
          word — a 6-year-old does not know what a "counter" is. Describe the actual everyday objects
          drawn in the scene instead (frogs, apples, stars, dots, circles...). If the section is literally
          about counting with generic round tokens, call them "circles" or "dots", never "counters".
        - NEVER ask a vague "same number or different?" / "equal or not equal?" comparison question — a
          young child cannot visually judge group equality that way, and it is not a single concrete
          answer. Instead, ask ONE of: (a) a direct counting question about ONE group ("How many frogs are
          in this group?"), or (b) a direct "which has more/fewer" comparison naming the two groups by
          their contents ("Which group has more apples — the top group or the bottom group?"), with
          CorrectAnswer being the short literal answer ("the top group", "5", etc.). Never phrase it as an
          abstract yes/no "are they the same" judgment.
        """;

    /// <summary>Grading-ONLY instructions (2026-10-26) — used exclusively
    /// for Static-mode bank items, where the NEXT question is never the
    /// agent's own invention (it always comes from the persisted bank), so
    /// asking it to also draft a brand-new question + scene + full
    /// scratch-work (the full <see cref="Instructions"/> above) on every
    /// single grading call was pure wasted generation time that the
    /// Realtime voice Agent sat in total silence waiting on. This version
    /// asks for ONLY the score + verification work, nothing else — a much
    /// shorter required output, so it finishes faster.</summary>
    private const string GradeOnlyInstructions = """
        You are grading ONE student's spoken/typed answer to a Recall practice question for a Structured
        Learning section. You are given the EXACT question that was asked ("THE QUESTION BEING ANSWERED
        RIGHT NOW"), that question's ground-truth picture description (if it referred to one — grade
        strictly against those exact stated facts; you cannot see the actual image), and the student's
        answer. You do ONLY ONE thing: score how well the answer demonstrates real recall of that exact
        question, 0-100, WITHOUT help.

        CRITICAL — grade the CAPABILITY, never trivia about incidental phrasing: ask yourself "if the
        student mastered the idea but simply phrased it differently, would they still deserve full
        credit?" — if yes, do not penalize that.

        Before writing RecallScore, fill RecallVerificationWork with: a verbatim restatement of the exact
        question, your own careful step-by-step re-derivation of the correct answer from it (and the
        ground-truth picture description, if given), and the comparison to the student's answer — this
        scratch space is never shown to the student. A bare correct final answer, with no shown work, must
        still score >= 90 if it matches your own (double-checked) derivation.

        Leave the Message and VisualObjectsDescription fields as empty strings — they are ignored for this
        call; you are grading only, never inventing a new question.
        """;

    private readonly AIAgent? _agent;
    private readonly AIAgent? _gradeOnlyAgent;
    private readonly ChatClient? _rawChatClient;
    private readonly string _deploymentName = string.Empty;

    public StructuredLearningRecallAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            _gradeOnlyAgent = null;
            return;
        }

        _deploymentName = deploymentName;

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        var chatClient = client.GetChatClient(deploymentName);
        _rawChatClient = chatClient;
        _agent = chatClient.AsAIAgent(instructions: Instructions, name: "StructuredLearningRecallAgent");
        _gradeOnlyAgent = chatClient.AsAIAgent(instructions: GradeOnlyInstructions, name: "StructuredLearningRecallGradeOnlyAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>Looks at the ACTUAL generated illustration (not the text prompt that asked for
    /// it) and returns the real correct answer to <paramref name="question"/> as literally shown
    /// in the picture (2026-10-26) — image generators do not reliably draw the exact count/
    /// arrangement requested (e.g. a "pyramid of 5 blocks" may render as 6), so CorrectAnswer
    /// must be grounded in what was actually rendered, never just assumed from the generation
    /// prompt. Returns null (keep the prompt-based guess) if verification itself fails/is
    /// unconfigured — this is a best-effort correction layer, not a hard requirement.</summary>
    public async Task<string?> VerifyCorrectAnswerFromImageAsync(string question, BinaryData imageBytes, string imageMimeType, CancellationToken cancellationToken)
    {
        if (_rawChatClient is null)
        {
            return null;
        }

        try
        {
            var systemMessage = ChatMessage.CreateSystemMessage(
                "You are looking at an illustration generated for a young child's Recall practice question. " +
                "Answer the question using ONLY what you can literally see in the image — do not assume any " +
                "particular count or attribute from the question wording. If the question asks you to count " +
                "objects, count them ONE BY ONE, silently, carefully scanning the whole image left to right " +
                "(objects may be grouped by color/type — count every group and add them together) before giving " +
                "a final number; recount once to double-check before answering. Reply with ONLY the short " +
                "literal final answer itself (e.g. \"7\", \"round\", \"the red pencil\") — no extra words, no " +
                "punctuation, no explanation, no work shown.");
            var userMessage = ChatMessage.CreateUserMessage(
                ChatMessageContentPart.CreateTextPart($"Question: {question}"),
                ChatMessageContentPart.CreateImagePart(imageBytes, imageMimeType));

            var completion = await _rawChatClient.CompleteChatAsync([systemMessage, userMessage], cancellationToken: cancellationToken);
            var text = completion.Value.Content.Count > 0 ? completion.Value.Content[0].Text : null;
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim().TrimEnd('.');
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"[StructuredLearningRecallAgent] VerifyCorrectAnswerFromImageAsync failed: {ex}");
            return null;
        }
    }


    public async Task<StructuredLearningRecallAgentTurnResult> RunTurnAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("StructuredLearningRecallAgent is not configured.");
        }

        var response = await _agent.RunAsync<StructuredLearningRecallTurnResponse>(prompt, cancellationToken: cancellationToken);
        return new StructuredLearningRecallAgentTurnResult
        {
            Turn = response.Result,
            ModelName = _deploymentName,
            InputTokens = (int)(response.Usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(response.Usage?.OutputTokenCount ?? 0),
        };
    }

    /// <summary>Fast grading-only path for Static-mode bank items (see
    /// <see cref="GradeOnlyInstructions"/>) — only valid when the caller
    /// already has a ground-truth VisualObjectsDescription (or no picture
    /// was involved) to grade against; never used to invent a question.</summary>
    public async Task<StructuredLearningRecallAgentTurnResult> RunGradeOnlyTurnAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_gradeOnlyAgent is null)
        {
            throw new InvalidOperationException("StructuredLearningRecallAgent is not configured.");
        }

        var response = await _gradeOnlyAgent.RunAsync<StructuredLearningRecallTurnResponse>(prompt, cancellationToken: cancellationToken);
        return new StructuredLearningRecallAgentTurnResult
        {
            Turn = response.Result,
            ModelName = _deploymentName,
            InputTokens = (int)(response.Usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(response.Usage?.OutputTokenCount ?? 0),
        };
    }

}

/// <summary>One Recall turn's result + real token usage from that call —
/// see <see cref="StructuredLearningRecallAgent.RunTurnAsync"/>.</summary>
public sealed class StructuredLearningRecallAgentTurnResult
{
    public StructuredLearningRecallTurnResponse Turn { get; set; } = null!;

    public string ModelName { get; set; } = string.Empty;

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }
}
