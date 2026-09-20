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

    public sealed class HypothesisQuestionDto
    {
        public string Question { get; set; } = string.Empty;

        public List<string> Options { get; set; } = [];

        public int? CorrectIndex { get; set; }
    }

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

    /// <summary>ONLY for Hypothesis — a two-question, four-option MCQ bank
    /// derived directly from this step's source text. Each item must include
    /// exactly 4 options and one correct answer index.</summary>
    public List<HypothesisQuestionDto>? HypothesisQuestions { get; set; }

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

    /// <summary>Set by StructuredLearningPedagogyService after generating the professor lecture.</summary>
    public HumanOS.Contracts.Studio.StructuredLearningProfessorLectureResult? ProfessorLecture { get; set; }

    /// <summary>Relative Azure Data Lake storage path for the synthesized professor audio lecture (e.g. {tenant}/{doc}/{sec}/speech/lecture.mp3).</summary>
    public string? SpeechStoragePath { get; set; }

    /// <summary>Web search source URLs from live API grounding (e.g. Perplexity AI).</summary>
    public List<string>? WebSearchCitations { get; set; }

    /// <summary>ONLY for StepType=DeepDive.</summary>
    public List<StructuredLearningExampleCardDto>? ExampleCards { get; set; }
}

public sealed class HypothesisQuestionDto
{
    public string Question { get; set; } = string.Empty;

    public List<string> Options { get; set; } = [];

    public int CorrectIndex { get; set; }
}

public sealed class StructuredLearningExampleCardDto
{
    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string? Emoji { get; set; }

    /// <summary>A text prompt for generating this card's real illustration
    /// — always non-null/non-empty, grounded 100% in the card's own Text
    /// (never an invented scene). Reused/filled in the same way as
    /// <see cref="LessonCardDto.IllustrationPrompt"/>. Kept as a fallback
    /// for single-image cards and for the Edit/Add-card agent path (which
    /// only ever produces one image); the designer's own multi-step output
    /// goes into <see cref="IllustrationPrompts"/> below instead.</summary>
    public string? IllustrationPrompt { get; set; }

    /// <summary>Set by StructuredLearningPedagogyService AFTER a real
    /// illustration was generated from IllustrationPrompt — null until
    /// then (or if generation failed/isn't configured, in which case the
    /// UI falls back to Emoji).</summary>
    public Guid? IllustrationId { get; set; }

    /// <summary>ORDERED sequence of illustration prompts for a card whose
    /// Text walks through multiple distinct steps (e.g. a math derivation:
    /// isolate the term, take the root, substitute i) — one prompt per
    /// step, each depicting ONLY that step's state, like sequential
    /// blackboard panels (2026-08-15, "step-by-step worked examples"
    /// feature). Null/empty for ordinary single-scene cards, which keep
    /// using <see cref="IllustrationPrompt"/> instead. When present, this
    /// list is authoritative and IllustrationPrompt is ignored.</summary>
    public List<string>? IllustrationPrompts { get; set; }

    /// <summary>Set by StructuredLearningPedagogyService — one Guid per
    /// entry in <see cref="IllustrationPrompts"/>, same order. Null when
    /// IllustrationPrompts wasn't used (single-image card).</summary>
    public List<Guid>? IllustrationIds { get; set; }
}

/// <summary>Agent-facing shape of a business-rule statement — deliberately
/// omits StatementId/SectionId (real Guid PKs the model cannot produce;
/// including them in the structured-output schema caused JSON deserialization
/// failures like "could not be converted to System.Guid" whenever the model
/// emitted a non-Guid placeholder). The caller maps this to a real
/// <see cref="StructuredLearningStatement"/> with server-generated Guids.</summary>
public sealed class StructuredLearningStatementDto
{
    public string StatementType { get; set; } = string.Empty;

    public string Statement { get; set; } = string.Empty;

    public string? SourceText { get; set; }

    public string? ConditionEntity { get; set; }

    public string? ConditionAttribute { get; set; }

    public string? ConditionOperator { get; set; }

    public string? ConditionValue { get; set; }

    public string? ConditionUnit { get; set; }

    public string? ActorEntity { get; set; }

    public string? ActorType { get; set; }

    public string? Action { get; set; }

    public string? TargetEntity { get; set; }

    public string? Modality { get; set; }

    public List<string> PossibleOutcomes { get; set; } = [];
}

public sealed class StructuredLearningSectionDesignResponse
{
    /// <summary>One-sentence capability this engram is building inside its chapter.
    /// The chapter title acts as the schema/category bucket; this field names the
    /// concrete capability the learner is developing in this section.</summary>
    public string Capability { get; set; } = string.Empty;

    /// <summary>Business-rule style statements inferred from this section before
    /// ontology generation. These are stored on the section row and kept light-weight
    /// until a later, explicit ontology stage decides how to lift them into entities
    /// and relationships.</summary>
    public List<StructuredLearningStatementDto> Statements { get; set; } = [];

    public List<StructuredLearningSectionStepDto> Steps { get; set; } = [];
}

public sealed class StructuredLearningPedagogyKnowledgeFactDto
{
  public Guid KnowledgeFactId { get; set; }
  public string Statement { get; set; } = string.Empty;
  public string FactType { get; set; } = string.Empty;
  public string Importance { get; set; } = string.Empty;
  public string DerivationType { get; set; } = string.Empty;
  public string EpistemicStatus { get; set; } = string.Empty;
  public double Confidence { get; set; }
  public List<StructuredLearningPedagogyGraphPathStepDto> GraphPath { get; set; } = [];
}

public sealed class StructuredLearningPedagogyGraphPathStepDto
{
  public Guid ConnectionId { get; set; }
  public int StepOrder { get; set; }
  public Guid SourceEntityId { get; set; }
  public string SourceEntityName { get; set; } = string.Empty;
  public string RelationshipCategory { get; set; } = string.Empty;
  public string RelationshipLabel { get; set; } = string.Empty;
  public Guid TargetEntityId { get; set; }
  public string TargetEntityName { get; set; } = string.Empty;
  public string? Evidence { get; set; }
  public string EpistemicStatus { get; set; } = string.Empty;
  public double? Confidence { get; set; }
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

        LANGUAGE HARD RULE (read this first): the request will tell you the
        "Language for all content" (e.g. 'en' or 'es'). EVERY field you
        output — Capability, Statements, every step's Content, every
        IllustrationPrompt, every Title — MUST be written entirely in that
        requested language, REGARDLESS of what language the section's own
        source text is written in. If the source text is in Spanish but the
        requested language is English, TRANSLATE and re-express the content
        in English — never copy/quote the source verbatim in its original
        language and never mix languages. Only proper nouns, brand names, or
        terms with no real translation may stay in their original form.

        BEFORE generating the steps, determine the core capability this
        engram is creating for the learner. Use the chapter title as the
        schema/category bucket and write a single sentence in the output
        field `Capability` describing the capability being developed. The
        format should be: "The [role/person] can [concrete action/skill],
        [supporting clause]." Example: "The volunteer can encourage a
        participant, help them remain engaged, and support their inclusion
        without taking over."

        ALSO, immediately after that capability decision, identify the
        section's business-rule-like statements that can be derived from the
        section text before any ontology work. Write them as a list in the
        output field `Statements`. Each item should be a concise rule or
        norm grounded in the source text and include the main rule text plus
        the key metadata needed later: `StatementType`, `Statement`,
        `SourceText`, `ConditionEntity`, `ConditionAttribute`,
        `ConditionOperator`, `ConditionValue`, `ActorEntity`, `Action`,
        `Modality`, and `PossibleOutcomes` when relevant. Keep the list
        short and evidence-based; do not invent rules beyond the section.

        ILLUSTRATION LEGIBILITY (applies to every IllustrationPrompt you write
        — Hypothesis, Teaching, and every DeepDive card): if the scene you
        describe naturally includes a diagram, chart, sign, label, or any
        other short piece of in-image text, explicitly instruct the image
        generator to render it LARGE, BOLD, and high-contrast, sized so a
        student can read it clearly on a small screen — e.g. "with large,
        bold, clearly legible labels". Prefer a few bigger, simpler visual
        elements over many small/cluttered details. This is only about
        HOW any such text/labels are rendered — never invent new text/labels
        that add information beyond what this section's own text supports.

        CONCRETE VISUAL SUBJECT — SHOW THE THING ITSELF, NOT A PERSON/BOARD
        DESCRIBING IT. This is the default for every IllustrationPrompt
        unless one of the explicit exceptions below (the narrower
        blackboard/list case, or the PHYSICAL/GESTURAL rule further down)
        applies. Ask "what is this content actually ABOUT?" and depict THAT
        thing directly, as a real, concrete, recognizable object/scene/
        diagram — never a generic illustration of a professor/person/board
        gesturing at or narrating the concept. Examples:
        - A molecule/compound -> draw the molecule itself: its atoms and
          bonds in a clean scientific ball-and-stick or structural-diagram
          style (e.g. "a ball-and-stick model of a water molecule: one red
          oxygen atom bonded to two smaller white hydrogen atoms at a bent
          angle").
        - An equation/formula -> depict the mathematical object itself (the
          shape/curve/geometry it describes, or the expression laid out as
          clean typeset math), not a photo of someone writing on a board.
        - A law/principle/rule of nature (physics, chemistry, biology,
          etc.) -> depict the phenomenon in action (e.g. for a law about
          gravity: an object visibly falling/arcing with motion lines; for a
          law about pressure/volume: a simple labeled diagram of the two
          states being compared) — the concrete effect, not a text-heavy
          caption explaining it.
        - A historical event, place, organism, tool, or any other concrete
          noun -> depict that noun/scene directly and realistically.
        - Only fall back to a "professor/whiteboard" scene when the content
          genuinely has NO physical/visual form of its own — e.g. abstract
          organizational rules, policies, or a purely verbal list with
          nothing to literally draw (see the MULTI-ITEM/MULTI-STEP rule
          below for that narrower case).
        - NEVER let an illustration become effectively a screenshot/crop of
          the course's own written text (e.g. a dense board covered in
          written sentences, cut off at the image edge). If you find
          yourself describing "a board covered in written paragraphs", stop
          and instead find the concrete object/diagram/scene that paragraph
          is actually describing, and draw that instead.

        PHYSICAL/GESTURAL/HANDS-ON DEMONSTRATION CONTENT — this rule is
        DOMAIN-GENERAL, not just for sign language: it applies to ANY course,
        in ANY field, whose content teaches a specific physical technique
        that a learner's body needs to reproduce. Examples across very
        different domains: sign language / hand signs; dance or
        choreography steps; sports or exercise form; mime or theater
        blocking; a physical therapy/rehab movement; a caregiver's technique
        for safely assisting, positioning, transferring, or guiding a person
        with a disability (e.g. how to support someone standing up, how to
        guide someone with low vision, how to position a wheelchair for a
        transfer); how to correctly use an assistive/adaptive device or
        mobility aid (a cane, walker, hearing aid, communication board,
        orthotic, feeding assistance tool); a first-aid/safety maneuver; a
        manual trade technique (how to hold a tool, a welding stance, a
        knife grip); a cooking/food-preparation or cleaning procedure (e.g.
        rinsing, scrubbing with salt/lemon, blanching in boiling water,
        scraping/cutting into pieces, soaking); or any other skill where the
        real content is "the body/hands do THIS, in THIS exact way to this
        physical object" rather than an abstract idea. This
        OVERRIDES the "one combined blackboard/board scene" default used
        elsewhere in this prompt for multi-item content. A blackboard/poster
        is right for abstract lists (rules, clauses, math steps) but WRONG
        here — a real instructor of a physical technique demonstrates it
        with their own body/hands (and, when relevant, together with
        another person, an assistive device, or a mobility aid), one
        movement at a time, not by writing it on a board. Getting this right
        matters most exactly where the stakes are highest — courses that
        train people to safely and correctly assist or teach someone with a
        disability, where a vague or generic image can leave the learner not
        actually knowing how to perform the technique safely. So:
        - Treat EACH distinct gesture/move/pose/technique/maneuver the
          section teaches as its own separate sub-topic — never merge two or
          more of them into a single illustration or a single DeepDive card,
          even if the section presents them together in a short list.
        - Every IllustrationPrompt for this kind of content must depict ONE
          person (plus a second person or the specific device/aid, when the
          technique inherently involves one) performing exactly ONE named
          technique, described concretely and precisely enough to actually
          reproduce it: starting body/hand position (and position of the
          other person/device, if any), the exact motion or hold, and where
          it ends — e.g. for a hand sign: "a person's hand starting flat
          near the forehead, palm facing out, then moving outward and
          slightly down, like a friendly salute wave"; for a caregiving
          technique: "a caregiver standing at the person's side, one arm
          under their arm and one hand supporting their back, guiding them
          forward at walking pace, both making eye contact" — never a vague
          restatement like "a person waving" or "someone being helped".
          Include the facial expression too when the section says the face
          matters (e.g. sign language often pairs a sign with a specific
          expression; a caregiving scene may need a calm, reassuring
          expression if the section emphasizes that).
        - Show the demonstration clearly and unambiguously — plain/neutral
          background, the hands/body/device clearly in frame and not
          obscured, as if it were a single clear photo/video frame from a
          real lesson, not a stylized icon or diagram.
        - This means a section teaching N distinct gestures/techniques needs N
          separate DeepDive cards (one per technique), each with its own
          Title (the technique's name), its own Text explaining exactly how
          to do it (grounded in the section's own words), and its own single
          IllustrationPrompt for that one technique — up to the maximum card
          count you were told; if the section teaches more distinct
          techniques than the maximum allows, prioritize covering as many
          DISTINCT ones as possible before giving a second card to any
          single technique.

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

        OPTIONAL "PERPLEXITY AI REAL-TIME WEB SEARCH FINDINGS":
        if the prompt includes this heading, verified live web search findings
        were retrieved for this Engram from Perplexity AI. Integrate these
        current, real-world facts, pedagogical explanations, and practical examples
        into the Teaching and DeepDive steps while maintaining high academic quality and rigor.

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
        6. Assessment — ONLY if instructed this section includes it

        Produce EXACTLY the steps you are told to include, in this exact order,
        never more, never fewer.

        ============================================================
        STEP 1 — HYPOTHESIS
        This is the Memory Paradox hypothesis stage: the student has NOT yet seen
        the section's source text, and we are using this step to prime thinking
        before teaching begins. This is NOT a reading-comprehension quiz and NOT
        a literal "according to the text" exercise.
        - The student must answer before any teaching or source text is shown.
        - The Hypothesis Content must begin with the exact prefix:
          "ContractVersion: 2026-08-11-v1-source-faithful-professor"
        - After that prefix, write a short, direct student-facing prompt that
          invites a prediction, judgment, or hypothesis based on common-sense
          reasoning about the scenario. Examples: "What values do you think
          define The Haven?" or "Who do you think this guide could help?"
        - The questions must be written as thinking questions, not as text-finding
          questions. Never ask: "According to the text...", "According to the
          lesson...", "According to the passage...", or any wording that implies
          the learner has already read the section.
        - Do NOT include the source paragraph, chapter text, or verbatim lesson
          wording in Hypothesis Content. The student is not reading the text yet.
        - Provide exactly 2 questions in HypothesisQuestions, each with exactly
          4 options and a single correct answer index (0-3).
        - The questions should still reflect the section's underlying ideas, but
          they must be framed as prediction/inference questions that help the
          student think before teaching starts.
        - NEVER reveal the answer/outcome here in the text itself — the point
          is to surface what the student already knows before teaching.
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
        - MULTI-ITEM/MULTI-STEP TEXTUAL CONTENT — NARROW CASE: this applies
          ONLY when the items themselves are inherently verbal/textual with
          no concrete visual form of their own (e.g. a list of legal/
          contract clauses, a set of organizational rules or regulations,
          named stages of an abstract process). It does NOT apply to a math
          derivation, a scientific/technical procedure, or anything else
          whose steps have a real visual form — see CONCRETE VISUAL SUBJECT
          above: those must instead show the actual object/diagram evolving
          (e.g. a math derivation is drawn as the evolving expression/graph
          itself, panel by panel — see IllustrationPrompts below — never a
          wordy board reciting each step in prose). For the narrow textual
          case, IllustrationPrompt must depict ONE single scene of a
          complete, filled-in professor's blackboard/whiteboard (or
          equivalent — a checklist board, a numbered poster, a diagram with
          labeled sections, whichever fits the subject) showing EVERY
          item/clause your Content walks through, written out top-to-bottom
          or left-to-right in order, exactly like a real class board by the
          time the professor finishes explaining it — never just the first
          item with the rest left blank, and never a dense restatement of
          full sentences (short labels/keywords only, per ILLUSTRATION
          LEGIBILITY above). Explicitly list every one of those items in the
          prompt itself (in the same order Content teaches them) so the
          image generator knows all of them, not only the first one.
        - EXCEPTION — PHYSICAL/GESTURAL/HANDS-ON MULTI-ITEM CONTENT (sign
          language signs, dance/exercise moves, caregiving techniques,
          assistive-device usage, cooking/food-prep or cleaning procedures,
          or any other section teaching several distinct hands-on physical
          actions — see the PHYSICAL/GESTURAL rule above): the "combined
          blackboard" pattern above does NOT apply, even though the section
          presents the steps as a numbered list — a numbered list is just
          how the source TEXT organizes it; the actual content is a
          physical action performed on a real object each time. Teaching
          only ever gets ONE illustration slot, and a single image cannot
          clearly show multiple distinct actions at once (unlike a written
          list, which a board CAN show). So for this content type:
          IllustrationPrompt must depict ONE clear photo-style scene of
          hands actively performing ONLY the FIRST (or otherwise most
          visually representative) step your Content introduces — e.g. for
          a recipe: hands rubbing coarse salt and lemon onto the ingredient
          over a sink, not a checklist of all six steps. Describe it
          concretely: whose hands, the specific object/ingredient/tool
          involved, and the exact action — never attempt to cram multiple
          steps into one image, and never describe a poster/board/checklist
          of the steps. In your Content, never tell the student "look at the
          board/pizarra for the steps" or imply the single image shows all
          of them — instead, walk them through each step in your own
          spoken words (the numbered list stays in your Content's narration,
          not on-screen), and use the Teaching illustration only as one
          concrete preview of the hands-on work involved.
        - EXCEPTION — PHYSICAL/GESTURAL/HANDS-ON MULTI-ITEM CONTENT (sign
          language signs, dance/exercise moves, caregiving techniques,
          assistive-device usage, or any other section teaching several
          distinct body/hand actions — see the PHYSICAL/GESTURAL rule
          above): the "combined blackboard" pattern above does NOT apply.
          Teaching only ever gets ONE illustration slot, and a single image
          cannot clearly show multiple distinct body motions at once (unlike
          a written list, which a board CAN show). So for this content type:
          IllustrationPrompt must depict ONE person clearly demonstrating
          ONLY the FIRST gesture/technique your Content introduces (same
          precise-motion description rules as the PHYSICAL/GESTURAL rule
          above) — never attempt to cram multiple gestures into one image,
          and never describe a poster/board/list of signs. In your Content,
          never tell the student "look at the board/image to see each
          sign/step" or imply the single image demonstrates all of them —
          instead, explicitly tell the student that each individual
          sign/technique is demonstrated one at a time in the cards that
          follow (the DeepDive step), and use the Teaching illustration only
          as a first preview of what a demonstration looks like.


        ============================================================
        STEP 3 — DEEPDIVE (only if instructed)
        Produce AT LEAST ONE card — never zero — up to the maximum number of
        cards you are told. Never pad with filler cards restating the same
        point: if the section only contains one genuinely distinct sub-topic,
        write exactly ONE thorough card for it (do not withhold the card just
        because there is only one topic). Only use more than one card when
        the section's own text actually contains that many distinct
        sub-topics. Each card needs:
        - Title: the section's own sub-topic/wording, never an invented
          thematic label.
        - Text: a REAL, detailed explanation of that sub-topic, SPOKEN
          directly by you as the professor (same voice/rules as the
          Teaching step above — never a meta-referential summary like "this
          card covers...", "the section explains...", or "aquí nos
          detenemos en..."/"here we stop to look at..." — teach the
          sub-topic itself, don't narrate that you're about to teach it).
          Go deeper/more concretely into this one sub-topic than Teaching
          did, using only facts this section's own text actually states —
          never a thin one-liner.
        - Emoji: optional, only as a lightweight fallback for when no real
          illustration is generated for this card — never a substitute for
          IllustrationPrompt/IllustrationPrompts below.
        - IllustrationPrompts vs IllustrationPrompt — pick ONE:
          * If this card's Text walks through a MULTI-STEP/MULTI-ITEM
            PROCESS (this is NOT limited to math — a math derivation, a
            multi-step procedure, a sequence of legal/contract clauses, a
            list of rules or regulations, stages of a process, e.g. "isolate
            x^2", "take the square root of both sides", "substitute the
            square root of -1 with i", OR "clause 1: notice period", "clause
            2: termination conditions", "clause 3: penalties"), you MUST
            use IllustrationPrompts: an ORDERED list of 2-4 prompts, one per
            distinct step/item, like a professor's blackboard (or checklist/
            poster/diagram, whichever fits the subject) shown one panel at
            a time. Each prompt must depict ONLY that one step/item's state
            (the expression/clause/diagram as it exists at that exact
            point), never the final result shown early or items merged
            together. Keep the visual style/layout consistent across all
            panels in the list (same board/diagram look) so they read as
            one connected sequence. Leave IllustrationPrompt empty when
            using this list.
          * Otherwise (a single concrete scene/object/diagram, no multi-
            step/multi-item process), write ONE IllustrationPrompt as
            before and leave IllustrationPrompts empty.
          Every prompt (whether in IllustrationPrompt or IllustrationPrompts)
          must describe a concrete scene/object/diagram that is actually
          depicted or clearly implied by this card's own Text — never an
          invented scene, and never text/letters/numbers/words rendered
          inside the image itself (math notation and any other short label
          must be drawn as visual shapes/positions, not as rendered
          characters, unless the ILLUSTRATION LEGIBILITY guidance above
          explicitly calls for legible in-image text/labels).

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
        STEP 6 — ASSESSMENT (only if instructed)
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
          section's own source text happens to be in. This is a HARD rule
          (see LANGUAGE HARD RULE at the top): translate/re-express the
          source content, never leave any field in the source's own
          language when it differs from the requested one.
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
        bool includeHypothesisStep,
        bool includeRecallStep,
        bool includeProductionStep,
        bool includeAssessmentStep = true,
        CancellationToken cancellationToken = default,
        string? illustrationGuidance = null,
        IReadOnlyList<StructuredLearningPedagogyKnowledgeFactDto>? knowledgeFacts = null,
        string? perplexityFindings = null,
        string? courseLevel = null)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The StructuredLearningSectionDesignerAgent is not configured. Set the 'AzureOpenAIEndpoint' and " +
                "'AzureOpenAIDeploymentName' application settings.");
        }

        var includedSteps = new List<string>();
        if (includeHypothesisStep) includedSteps.Add("Hypothesis");
        includedSteps.Add("Teaching");
        if (includeDeepDiveStep) includedSteps.Add("DeepDive");
        if (includeRecallStep) includedSteps.Add("Recall");
        if (includeProductionStep) includedSteps.Add("Production");
        if (includeAssessmentStep) includedSteps.Add("Assessment");

        var levelInstruction = (courseLevel ?? "L100") switch
        {
            "L300" => "CONTENT DEPTH: L300 (advanced). Assume the learner has already mastered L100/L200 " +
                      "fundamentals for this topic — do NOT re-explain basic definitions. Focus on trade-offs, " +
                      "edge cases, architecture-level decisions, and nuanced judgment calls a senior/principal " +
                      "practitioner would face. Examples should be realistic, non-trivial scenarios.",
            "L200" => "CONTENT DEPTH: L200 (intermediate). Assume basic fundamentals are already understood — " +
                      "keep foundational definitions brief, spend most of the content on practical, worked " +
                      "examples and real-world application rather than re-teaching basics.",
            _ => "CONTENT DEPTH: L100 (foundational). Assume zero prior knowledge of this topic — explain every " +
                 "core concept before using it, avoid unexplained jargon, and prioritize clarity over depth.",
        };

        var prompt = $"""
            ContractVersion: {SourceFaithfulContractVersion}
            Chapter: {chapterTitle}
            Section: {subchapterTitle}
            Language for all content: {languageCode}
            HARD RULE: every output field (Capability, Statements, all step Content, all IllustrationPrompts, all Titles) MUST be written entirely in '{languageCode}', even though the section text below may be written in a different language. Translate/re-express, do not copy the source's own language.
            Audience/tone: {pedagogicalAudience ?? "Standard"}
            {levelInstruction}
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
            {(string.IsNullOrWhiteSpace(perplexityFindings) ? "" : $"""

            PERPLEXITY AI REAL-TIME WEB SEARCH FINDINGS:
            Verified live web search findings were retrieved for this Engram from Perplexity AI.
            Integrate these current, real-world facts, pedagogical explanations, and practical examples
            into the Teaching and DeepDive steps while maintaining high academic quality and rigor:
            {perplexityFindings.Trim()}
            """)}

            KNOWLEDGE FACTS (validated and persisted for this Engram; coverage guidance only):
            The original SECTION TEXT remains the primary source for narrative, context,
            explanations, examples, tone, sequence, and depth. Use Core facts as required
            coverage for Learn and Supporting facts as useful examples or context. Do not
            copy fact statements mechanically or generate pedagogy only from facts. Use the
            ordered Graph Paths to understand relationships and deepen important connections
            in DeepDive cards. Do not expose IDs, JSON, database fields, or graph metadata to
            the learner, and do not invent facts or relationships. Keep the existing output
            format and behavior of Hypothesis, Recall, Production, and Assessment.
            {SerializeKnowledgeFacts(knowledgeFacts)}

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

    private static string SerializeKnowledgeFacts(IReadOnlyList<StructuredLearningPedagogyKnowledgeFactDto>? knowledgeFacts)
    {
        if (knowledgeFacts is null || knowledgeFacts.Count == 0)
        {
            return "No persisted Knowledge Facts were supplied; do not continue without them.";
        }

        var lines = new List<string>();
        foreach (var fact in knowledgeFacts)
        {
            lines.Add($"- [{fact.Importance}] {fact.Statement} (FactType: {fact.FactType}, Derivation: {fact.DerivationType}, EpistemicStatus: {fact.EpistemicStatus}, Confidence: {fact.Confidence:0.##})");
            foreach (var step in fact.GraphPath.OrderBy(step => step.StepOrder))
            {
                lines.Add($"  Path {step.StepOrder}: {step.SourceEntityName} -[{step.RelationshipCategory}/{step.RelationshipLabel}]-> {step.TargetEntityName}; Evidence: {step.Evidence ?? string.Empty}");
            }
        }

        return string.Join('\n', lines);
    }
}
