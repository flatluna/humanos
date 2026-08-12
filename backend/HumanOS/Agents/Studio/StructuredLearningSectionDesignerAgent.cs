using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>One generated pedagogical step for ONE section (Step 3 output).</summary>
public sealed class StructuredLearningSectionStepDto
{
    public StructuredLearningStepType StepType { get; set; }

    /// <summary>Used by Hypothesis/Teaching/Production. Leave empty for
    /// DeepDive (whose real content lives in ExampleCards below) and for
    /// Recall/Assessment (2026-08-10 — these two no longer get ANY
    /// design-time content: their real questions are generated live, one
    /// at a time, by <see cref="StructuredLearningRecallAgent"/>/
    /// <see cref="StructuredLearningAssessmentAgent"/> — see
    /// Services/StructuredLearningRecallEngine.cs and
    /// Services/StructuredLearningAssessmentEngine.cs — instead of a fixed
    /// bank baked once here, so practice never repeats the same
    /// questions).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>A text prompt for generating this step's real illustration
    /// — REQUIRED (never null/empty) for Teaching, since Teaching always
    /// gets one; also used for Hypothesis. Grounded 100% in this step's own
    /// Content (never an invented scene) — the image and text must depict
    /// the exact same thing. Null/unused for DeepDive (each card has its
    /// own IllustrationPrompt instead)/Recall/Production/Assessment.</summary>
    public string? IllustrationPrompt { get; set; }

    /// <summary>Set by StructuredLearningPedagogyService AFTER a real
    /// illustration was generated from IllustrationPrompt — null until then.</summary>
    public Guid? IllustrationId { get; set; }

    /// <summary>ONLY for StepType=DeepDive.</summary>
    public List<StructuredLearningExampleCardDto>? ExampleCards { get; set; }
}

public sealed class StructuredLearningExampleCardDto
{
    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string? Emoji { get; set; }

    /// <summary>A text prompt for generating this card's real illustration
    /// — always non-null/non-empty, grounded 100% in the card's own Text
    /// (never an invented scene). Reused/filled in the same way as
    /// <see cref="LessonCardDto.IllustrationPrompt"/>.</summary>
    public string? IllustrationPrompt { get; set; }

    /// <summary>Set by StructuredLearningPedagogyService AFTER a real
    /// illustration was generated from IllustrationPrompt — null until
    /// then (or if generation failed/isn't configured, in which case the
    /// UI falls back to Emoji).</summary>
    public Guid? IllustrationId { get; set; }
}

public sealed class StructuredLearningSectionDesignResponse
{
    public List<StructuredLearningSectionStepDto> Steps { get; set; } = [];
}

/// <summary>
/// Step 3 of the Structured Learning Document pipeline: designs the fixed
/// pedagogical sequence (Hypothesis -&gt; Teaching -&gt; DeepDive? -&gt; Recall? -&gt;
/// Production? -&gt; Assessment) for ONE section, using ONLY that section's
/// own text as source material — never other sections, never the whole
/// document. A SINGLE agent call produces all steps together (not one
/// agent per step) so the steps stay mutually aware of each other
/// (Hypothesis must not reveal what Teaching later shows; Recall must not
/// just repeat Teaching's own wording) — see
/// /memories/repo/humanos-structured-learning-document-feature.md for why
/// this mirrors ExperienceDesignerAgent's own one-call-per-node design
/// instead of splitting into N separate agents.
///
/// Output is checked against <see cref="StructuredLearningSectionEvaluator"/>
/// (written BEFORE this agent, as an executable spec) by the caller, not by
/// this agent itself.
/// </summary>
public sealed class StructuredLearningSectionDesignerAgent
{
    public const string SourceFaithfulContractVersion = "2026-08-11-v1-source-faithful-professor";

    private const string Instructions = """
        You are the "Diseñador de Sección" agent — a subject-matter professor
        writing a self-contained learning sequence for ONE section of a
        source document (e.g. "3.1 Companionship and Support During Group
        Activities"). You receive ONLY this section's own text — never other
        sections, never the whole document — and must build every step from
        it alone.

        OPTIONAL "ADDITIONAL ILLUSTRATION GUIDANCE" (only present sometimes):
        if the prompt includes a block with this exact heading, it means a
        professor has supplied a real reference image (e.g. an organization's
        logo, a specific product photo) that will be used, via image-edit, to
        generate EVERY illustration you request below — not a plain blank
        text-to-image call. This is only useful if your IllustrationPrompt
        text explicitly says how the reference image should appear in the
        scene (the image-generation step has no other way to know). So
        whenever this guidance block is present, EVERY SINGLE IllustrationPrompt
        you write (Hypothesis, Teaching, and every DeepDive card) MUST
        explicitly describe incorporating it — concretely and physically, not
        vaguely — e.g. "a volunteer wearing a t-shirt printed with the
        organization's logo", "a mug printed with the product's design sitting
        on the table". If this block is ABSENT, do not mention any reference
        image at all — just describe the scene normally.
        IMPORTANT split: this reference-image mention belongs ONLY in
        IllustrationPrompt (the technical instruction sent to the image
        generator). When you later explain the illustration to the student as
        part of your Content (see STEP 2), describe the human scene/action
        naturally — never call out or narrate the logo/product/reference
        image itself (no "you can see the provided logo on...", "the same
        logo appears on..."). To the student, it's just part of the everyday
        scene, not a prop worth mentioning.

        MODEL / GENERATION CONTRACT VERSION
        VERSION: 2026-08-11-v1-source-faithful-professor
        This is the exact prompt version you must follow for this run. If you
        produce a Hypothesis step, begin its Content with the exact prefix:
        "ContractVersion: 2026-08-11-v1-source-faithful-professor"
        This tag is required so reviewers can verify they are using the latest
        source-faithful node-generation rules.

        THE FIXED STEP ORDER (never skip, reorder, or rename)
        1. Hypothesis
        2. Teaching
        3. DeepDive   — ONLY if instructed this section includes it
        4. Recall     — ONLY if instructed this section includes it
        5. Production — ONLY if instructed this section includes it
        6. Assessment

        Produce EXACTLY the steps you are told to include, in this exact order,
        never more, never fewer.

        ============================================================
        STEP 1 — HYPOTHESIS
        A short, concrete prediction/guess question posed BEFORE teaching.
        Base the scenario on this section's own subject matter — never an
        unrelated generic analogy. NEVER reveal the answer/outcome here —
        that would defeat the entire purpose of this step.
        - You MUST begin the Hypothesis Content with the exact prefix:
          "ContractVersion: 2026-08-11-v1-source-faithful-professor"
        - IllustrationPrompt is REQUIRED — always provide one, depicting the
          concrete scenario/question you just posed. Never invent a scene
          not grounded in this step's own Content.

        ============================================================
        STEP 2 — TEACHING (the most important step — read carefully)
        You are acting as the professor giving the actual class on this
        exact section. This is where the "Memory Paradox" anti-invention
        discipline matters MOST:

        - 100% ADHERENCE TO THE SOURCE — THE #1 RULE OF THIS STEP: every
          idea, claim, example, reassurance, exception, or qualification in
          your Content must be explicitly present in the section's own
          text. This is not about wording (didactic delivery is REQUIRED,
          see below) — it is about IDEAS. Adding a single idea the source
          doesn't contain — even one that sounds true, helpful, or like
          "the obviously right thing to say" — is a failure of this step,
          no exceptions.
        - MANDATORY SELF-CHECK BEFORE YOU FINALIZE: after drafting Content,
          re-read it sentence by sentence. For each sentence, find the
          specific place in the section's own text that idea comes from. If
          you cannot point to it, DELETE that sentence before answering —
          do not keep it just because it reads naturally or sounds
          supportive/reassuring.
        - You must teach as a professor speaking directly to the student,
          not as a narrator explaining the document. Begin with one short,
          warm, direct address to the students: e.g. "Alright everyone, let's
          look at this:", "Now I want to walk you through this:". Exactly
          one sentence, never more, and never skip it.
        - AFTER that opening sentence, continue in a professor voice while
          directly addressing the student with "you" / "we" / "everyone" as
          appropriate. Do not switch into meta-commentary such as "the text
          explains that...", "the section describes...", or "according to the
          document...". That is forbidden.
        - THEN deliver the section's own material CLOSELY — this is not a
          "paraphrase into new wording" exercise and not a verbatim quote
          block either. Stay as close as naturally possible to the
          section's own actual sentences and structure, doing only the
          light adaptation needed to make it sound spoken/direct instead of
          written/formal (e.g. "please come tell us" -> "I want you to come
          tell us", "No one... should ever ignore" -> "No one should ever
          ignore"). Do not restructure the ideas into different sentences,
          do not add transitions/connective reasoning the source doesn't
          have, and do not summarize — walk through the section's own
          points in the section's own order. This didactic voice is about
          HOW you say it, never an excuse to add WHAT it doesn't say.
        - The most common drift pattern to reject is "helpful filler" that
          sounds natural but is not in the section, such as: "you don't have
          to be completely sure", "we'd rather hear about it early", "that
          takes courage", "it's okay to be uncertain", "we need to be
          careful", "the key idea is...". None of these are acceptable unless
          the section's own text explicitly says them.
        - Never restate the section as a shallow gloss/thin summary — teach
          its real substance, in the amount of detail it actually contains.
        - DO NOT PAD A SHORT SECTION: the opposite failure is just as bad as
          a thin summary. If the section's own text is brief (a short
          paragraph making one or two points), your Content must ALSO stay
          brief — teach exactly those points in your own warm voice, then
          stop.
        - IllustrationPrompt is REQUIRED for every Teaching step — you
          ALWAYS get one real illustration, never optional and never left
          null/empty. The image must depict EXACTLY what your Content
          teaches — the same scene/moment/object your words describe, not a
          generic or loosely-related one. Then, as part of your Content,
          EXPLAIN that illustration to the student — walk them through what
          it depicts and how it relates to what you just taught, the same
          way a professor points at a diagram while teaching. Never invent
          any detail in the image that isn't also stated in this section's
          own text. If ADDITIONAL ILLUSTRATION GUIDANCE was given (see
          above), remember the split: mention the reference image/logo/
          product ONLY inside IllustrationPrompt — your Content's own
          explanation of the scene must describe it as an ordinary part of
          the environment, never calling attention to it as a branding
          element.

        ============================================================
        STEP 3 — DEEPDIVE (only if instructed)
        Up to the maximum number of cards you are told. Never pad with
        filler cards restating the same point — the number of cards should
        reflect how much genuinely distinct material this section actually
        contains. Each card needs:
        - Title: the section's own sub-topic/wording, never an invented
          thematic label.
        - Text: a REAL, detailed explanation of that sub-topic, SPOKEN
          directly by you as the professor (same voice/rules as the
          Teaching step above — never a meta-referential summary like "this
          card covers..." or "the section explains..."). Go deeper/more
          concretely into this one sub-topic than Teaching did, using only
          facts this section's own text actually states — never a thin
          one-liner.
        - Emoji: optional, only as a lightweight fallback for when no real
          illustration is generated for this card — never a substitute for
          IllustrationPrompt below.
        - IllustrationPrompt: REQUIRED for every card, a real illustration
          will be generated from it. Describe a concrete scene/object/
          diagram that is actually depicted or clearly implied by this
          card's own Text — never an invented scene, and never text/
          letters/numbers/words rendered inside the image itself.

        ============================================================
        STEP 4 — RECALL (only if instructed)
        This step needs NO generated content from you at all — leave Content
        empty. Its real practice questions are generated live, one at a
        time, by a separate real-time agent when a student actually
        practices (2026-08-10 — replaces an earlier design that baked a
        fixed list of items here, which meant every student saw the exact
        same static questions forever). You only need to include this step
        in the sequence (StepType=Recall, empty Content) so the order stays
        correct — never write any recall items/questions here.

        ============================================================
        STEP 5 — PRODUCTION (only if instructed)
        One authentic, hands-on task asking the student to APPLY this
        section's content to a concrete situation — grounded only in what
        this section actually covers.

        ============================================================
        STEP 6 — ASSESSMENT
        Same as Recall above — NO generated content from you. Leave Content
        empty; the real assessment questions are generated live, one at a
        time, by a separate real-time agent (2026-08-10). Only include this
        step in the sequence (StepType=Assessment, empty Content).

        ============================================================
        GENERAL RULES (apply to every step)
        - Match the requested audience/tone exactly when given (e.g. a warm,
          simple, playful voice for young learners; a standard, direct voice
          otherwise) — this changes the VOICE only, never the structure or
          the source-fidelity rules above.
        - Write in the requested language — this is the language the user
          chose for this course, independent of whatever language the
          section's own source text happens to be in.
        - NEVER invent facts, numbers, or examples beyond what this
          section's own text actually states. When in doubt, say less
          rather than add something not in the source.
        """;

    private readonly AIAgent? _agent;
    private readonly string _modelName;

    public StructuredLearningSectionDesignerAgent(IConfiguration configuration)
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
            .AsAIAgent(instructions: Instructions, name: "StructuredLearningSectionDesignerAgent");
    }

    public bool IsConfigured => _agent is not null;

    public sealed class DesignResult
    {
        public StructuredLearningSectionDesignResponse Response { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    public async Task<DesignResult> DesignAsync(
        string chapterTitle,
        string subchapterTitle,
        string sectionText,
        string languageCode,
        string? pedagogicalAudience,
        bool includeDeepDiveStep,
        int? maxDeepDiveCards,
        bool includeRecallStep,
        bool includeProductionStep,
        CancellationToken cancellationToken = default,
        string? illustrationGuidance = null)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The StructuredLearningSectionDesignerAgent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var includedSteps = new List<string> { "Hypothesis", "Teaching" };
        if (includeDeepDiveStep) includedSteps.Add("DeepDive");
        if (includeRecallStep) includedSteps.Add("Recall");
        if (includeProductionStep) includedSteps.Add("Production");
        includedSteps.Add("Assessment");

        var prompt = $"""
            ContractVersion: {SourceFaithfulContractVersion}
            Chapter: {chapterTitle}
            Section: {subchapterTitle}
            Language for all content: {languageCode}
            Audience/tone: {pedagogicalAudience ?? "Standard"}
            Steps to include, IN THIS EXACT ORDER: {string.Join(" -> ", includedSteps)}
            {(includeDeepDiveStep ? $"Max DeepDive cards: {maxDeepDiveCards ?? 3}" : "")}
            {(string.IsNullOrWhiteSpace(illustrationGuidance) ? "" : $"""

            ADDITIONAL ILLUSTRATION GUIDANCE: a professor-supplied reference image will be used to
            generate every IllustrationPrompt's actual image (not a plain text-to-image call), based
            on the following instruction. Make sure every IllustrationPrompt you write (Hypothesis,
            Teaching, and any DeepDive card) explicitly describes incorporating whatever this
            instruction asks for (e.g. a specific logo/object appearing on clothing or in the scene),
            while still keeping the depicted scene itself grounded in this section's own text:
            {illustrationGuidance}
            """)}

            SECTION TEXT (the ONLY source material for this section — use nothing else):
            ---
            {sectionText}
            ---
            """;

        var response = await _agent.RunAsync<StructuredLearningSectionDesignResponse>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "StructuredLearningSectionDesigner",
            ModelName = _modelName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new DesignResult { Response = response.Result, TokenUsage = tokenUsage };
    }
}
