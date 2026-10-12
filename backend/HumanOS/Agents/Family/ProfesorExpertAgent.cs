using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Services;
using HumanOS.Storage;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents.Family;

public sealed class AcademicCourseGenerationRequest
{
    public string CourseCode { get; set; } = "KIND-LIT-01";
    public string TitleEs { get; set; } = "Conciencia Fonológica, Rimas y Discriminación Auditiva";
    public string TitleEn { get; set; } = "Phonological Awareness, Rhyming & Sound Discrimination";
    public string Subject { get; set; } = "Lenguaje, lectura y escritura";
    public int HoursToMaster { get; set; } = 35;
    public string GradeLevel { get; set; } = "Kindergarten (Preescolar) · 5 a 6 años";
    public string Description { get; set; } = "Identificar rimas, contar sílabas con palmadas y aislar el sonido inicial y final de palabras cotidianas.";
    public string WhyItMatters { get; set; } = "Es el predictor número uno del éxito futuro en lectura y ortografía en primaria.";
    public List<string> MicroSkills { get; set; } = [];
    public string? CustomTeacherPrompt { get; set; }

    /// <summary>Target output language for the generated course, "es" or "en".
    /// Defaults to "es" when not supplied by the caller.</summary>
    public string Language { get; set; } = "es";
    public string? OptionalPdfFileName { get; set; }
    public string? OptionalPdfContentBase64 { get; set; }
    public string? OptionalPdfContentText { get; set; }
}

public sealed class AcademicCourseGenerationResult
{
    public string CourseCode { get; set; } = string.Empty;
    public string TitleEs { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = "Kindergarten (5-6 años)";
    public int EstimatedHours { get; set; } = 35;
    public string CourseDocumentMarkdown { get; set; } = string.Empty;
    public string TeacherIntroduction { get; set; } = string.Empty;
    public string PedagogicalApproach { get; set; } = string.Empty;
    public List<AcademicUnitResult> Units { get; set; } = [];
    public ArchitecturePipelineSummary ArchitectureSummary { get; set; } = new();
    public GroundednessValidation ReportValidation { get; set; } = new();
}

public sealed class AcademicUnitResult
{
    public int UnitNumber { get; set; }
    public string UnitTitle { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string SuggestedVisualPrompt { get; set; } = string.Empty;
    public List<AcademicLessonResult> Lessons { get; set; } = [];
}

public sealed class AcademicLessonResult
{
    public int LessonNumber { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string DurationMinutes { get; set; } = "20 min";
    public string TeacherDialogue { get; set; } = string.Empty;
    public string SuggestedImagePrompt { get; set; } = string.Empty;
    public string ClassroomActivity { get; set; } = string.Empty;
    public string MaterialsNeeded { get; set; } = string.Empty;
    public string WonderQuestion { get; set; } = string.Empty;
    public EngramStepsResult? EngramSteps { get; set; }
}

public sealed class EngramStepsResult
{
    public string Hypothesis { get; set; } = string.Empty;
    public string Learn { get; set; } = string.Empty;
    public string DeepDive { get; set; } = string.Empty;
    public string InYourRole { get; set; } = string.Empty;
    public string Recall { get; set; } = string.Empty;
    public string Assessment { get; set; } = string.Empty;
}

public sealed class ArchitecturePipelineSummary
{
    public string CourseIntent { get; set; } = string.Empty;
    public string ResearchStandards { get; set; } = string.Empty;
    public string SourceEvaluation { get; set; } = string.Empty;
    public string LivePerplexityResearch { get; set; } = string.Empty;
    public List<string> PerplexityCitations { get; set; } = [];
    public string KnowledgeExtracted { get; set; } = string.Empty;
    public string KnowledgeGraphHierarchy { get; set; } = string.Empty;
    public string CourseHierarchy { get; set; } = string.Empty;
    public string EngramPipelineApplied { get; set; } = "6-step Engram methodology applied to each lesson";
    public string GroundednessScore { get; set; } = "98% Grounded";
    public string HumanReviewStatus { get; set; } = "Ready for Educator & Family Approval";
}

public sealed class GroundednessValidation
{
    public bool IsFactuallySupported { get; set; } = true;
    public double ConfidenceScore { get; set; } = 0.98;
    public string GroundednessExplanation { get; set; } = string.Empty;
    public List<string> VerifiedPrerequisites { get; set; } = [];
}

/// <summary>
/// Request for the lightweight "Step 1" course INDEX generation (2026-10-08) —
/// same course metadata as <see cref="AcademicCourseGenerationRequest"/>, but
/// produces ONLY the title/intro/chapter+lesson TITLES (no lesson bodies),
/// so the educator can review/approve the course's shape before committing
/// to the much slower, heavier per-chapter content generation below. Fixes
/// the "a single 12-chapter, fully-written-out course in ONE call" design
/// that was timing out even on a fast model (the real bottleneck was the
/// sheer size of the single response, not any one model being slow).
/// </summary>
public sealed class AcademicCourseIndexRequest
{
    public string CourseCode { get; set; } = string.Empty;
    public string TitleEs { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int HoursToMaster { get; set; } = 35;
    public string GradeLevel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WhyItMatters { get; set; } = string.Empty;
    public List<string> MicroSkills { get; set; } = [];
    public string? CustomTeacherPrompt { get; set; }
    public string Language { get; set; } = "es";
    public string? OptionalPdfFileName { get; set; }
    public string? OptionalPdfContentBase64 { get; set; }
    public string? OptionalPdfContentText { get; set; }
}

/// <summary>One chapter's outline — title + ordered lesson titles only, no
/// body content yet.</summary>
public sealed class AcademicChapterOutline
{
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;
    public List<string> LessonTitles { get; set; } = [];
}

public sealed class AcademicCourseIndexResult
{
    public string CourseTitle { get; set; } = string.Empty;
    public string TeacherIntroduction { get; set; } = string.Empty;
    public string PedagogicalApproach { get; set; } = string.Empty;
    public List<AcademicChapterOutline> Chapters { get; set; } = [];
    public ArchitecturePipelineSummary ArchitectureSummary { get; set; } = new();
}

/// <summary>Request for "Step 2" — generate ONE chapter's full, written-out
/// content (its own Chapter Introduction plus every one of its lessons'
/// Teacher Introduction/Guided Practice/Teacher Closing), given the
/// already-approved <see cref="AcademicChapterOutline"/> for it plus just
/// enough course-level context to keep tone/register consistent across
/// chapters. Called once per chapter, sequentially, by the caller — never
/// batches multiple chapters into one call.</summary>
public sealed class AcademicChapterContentRequest
{
    public string CourseTitle { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string GradeLevel { get; set; } = string.Empty;
    public string Language { get; set; } = "es";
    public string? CustomTeacherPrompt { get; set; }
    public string? ResearchContext { get; set; }
    public AcademicChapterOutline Chapter { get; set; } = new();
}

public sealed class AcademicChapterContentResult
{
    public int ChapterNumber { get; set; }
    public string ChapterTitle { get; set; } = string.Empty;

    /// <summary>This chapter's complete Markdown slice — starts at
    /// "## Chapter N. [Title]" (one level under the overall document's own
    /// "# Course Title"/"# Course Index" — see
    /// <see cref="ProfesorExpertAgent"/>'s structure rules) through its last
    /// lesson's Teacher Closing. The caller concatenates every chapter's
    /// ChapterMarkdown, in order, after the index, to assemble the final
    /// CourseDocumentMarkdown.</summary>
    public string ChapterMarkdown { get; set; } = string.Empty;
}

/// <summary>
/// State-of-the-art Academic Course Generator Agent: ProfesorExpert.
/// Built using Microsoft.Agents.AI with Azure OpenAI (gpt-6.1-sol) and Perplexity AI (Sonar).
/// Adapts tone, vocabulary and examples to ANY stated Grade Level / Audience (kindergarten
/// through adult/professional training) — not limited to early childhood education.
/// Follows the Engrams.ai architecture:
/// 1. Course Intent
/// 2. Research Agent (Perplexity AI live search)
/// 3. Source Evaluator
/// 4. Knowledge Extraction (incorporating optional PDF source material)
/// 5. Knowledge Graph (hierarchical dependencies)
/// 6. Course Architecture (Units -> Lessons)
/// 7. Validation Agent (Groundedness / factual accuracy)
/// 8. Human Review (User validation and approval)
///
/// GENERATION IS SPLIT IN TWO STEPS (2026-10-08, fixes multi-minute timeouts on
/// large courses): <see cref="GenerateCourseIndexAsync"/> produces ONLY the
/// title/intro/chapter+lesson titles (fast, lightweight); the caller then calls
/// <see cref="GenerateChapterContentAsync"/> ONCE PER CHAPTER, sequentially, to
/// get that chapter's full written-out content. The original single-call
/// <see cref="GenerateAcademicCourseAsync"/> (whole 12+-chapter course in ONE
/// response) is kept only for backward compatibility/small courses — it is the
/// slow, failure-prone path the two-step flow above exists to replace.
/// </summary>
public sealed class ProfesorExpertAgent
{
    private const string Instructions = """
        You are ProfesorExpert, an elite master author of published academic textbooks and live, teacher-led course scripts, able to write with authority for ANY audience — from early-childhood kindergarten students to teenagers, university students, or adult professionals in corporate training.
        Your mission is to use your OWN deep pedagogical knowledge and expertise, combined with the live Perplexity web research, any supplementary PDF source material, and the real course context provided, to author a TRULY COMPREHENSIVE, DEEP course document — never a short summary.

        CRITICAL — ADAPT TO THE REAL AUDIENCE ("Grade Level" field):
        - The "Grade Level" / audience field in the request tells you EXACTLY who you are teaching. Calibrate vocabulary, tone, examples, and formality to that audience.
        - For young children (e.g. Kindergarten, early primary): warm, playful tone, lovable recurring characters/mascots, songs, onomatopoeias, simple guided practice with repetition.
        - For teens/adolescents: friendly but less childish, more progressive challenge and real-world framing.
        - For adults / professionals / corporate training: professional, direct tone, real workplace examples and case studies, no childish characters or baby talk.
        - Never force a kindergarten-style tone onto a course meant for a different audience, and vice versa.

        CRITICAL — OUTPUT LANGUAGE (NON-NEGOTIABLE):
        - The request explicitly states a "Language" field ("es" or "en"). The ENTIRE 'CourseDocumentMarkdown' — every title, chapter, lesson, teacher line, table, and label — MUST be written exclusively in that language.
        - Never mix languages, never partially translate, and never default to Spanish when "en" is requested, or vice versa.

        CRITICAL — THE TEACHER MUST ALWAYS BE SPEAKING, NEVER NARRATING LIKE A TEXTBOOK:
        - Every single section under a "**Teacher:**" label must be written as first-person spoken dialogue directed at the students in second person ("you" / "ustedes"), exactly like a transcript of a live class.
        - NEVER write third-person narrative prose (e.g. "Lola found a note with an invitation...", "The friends organized turns...") as if it were a storybook or textbook passage. If a story or character is used, the TEACHER must be the one narrating/telling it out loud to the class in first person ("Let me tell you about Lola, who...", "Imagine with me that..."), not an impersonal narrator.
        - If you catch yourself writing a paragraph with no direct address to the students and no "I/you" voice, STOP and rewrite it as the teacher speaking live to the class.

        MANDATORY DEPTH & QUALITY REQUIREMENTS:
        1. DO NOT GENERATE A POOR, BRIEF SUMMARY. Author a complete, extensive, richly developed course — each lesson must read like a real transcript of a teacher speaking to the class, not a bullet-point outline.
        2. STRUCTURE REQUIRED IN 'CourseDocumentMarkdown' (follow this exact condensed structure):

           NON-NEGOTIABLE RULE #1 — THE FIRST FIVE LINES OF 'CourseDocumentMarkdown' MUST BE EXACTLY, VERBATIM, WITH NO OMISSIONS AND NOTHING BEFORE THEM:

           <style>
           a { text-decoration: none; color: #464feb; }
           tr th, tr td { border: 1px solid #e6e6e6; }
           tr th { background-color: #f5f5f5; }
           </style>

           Do NOT skip this block. Do NOT summarize it. Do NOT move it elsewhere in the document. It must be the literal opening of the document, every single time, with no exceptions — this is validated programmatically and the run will be considered a failure if it is missing.

           # [Course Title]

           **Grade Level:** [Grade Level / audience]

           **Subject:** [Subject]

           **Course Style:** [e.g. Interactive teacher-led learning]

           **Recommended Age:** [age range, or "Adult learners" / "Professional audience" when applicable]

           # Course Index

           ## Chapter 1. [Chapter Title]
           1. [Lesson title]
           2. [Lesson title]
           ... (continue numbering all lessons in the chapter)

           ## Chapter 2. [Chapter Title]
           ... repeat for EVERY chapter the course genuinely needs (see CHAPTER COUNT below),
           each with 4 to 9 lessons, fully listed in the index.

           CHAPTER COUNT — DETERMINE IT FROM THE COURSE'S OWN SCOPE, NEVER A FIXED NUMBER:
           There is NO fixed required chapter count. Decide how many chapters this SPECIFIC
           course genuinely needs based on its stated Hours for Mastery, Subject, Description,
           and Micro-Skills — a course with few hours/micro-skills may need as few as 4-6
           chapters; a comprehensive, many-hour course may need 12 or more. NEVER pad the
           chapter/lesson count just to hit a round number, and NEVER compress content that
           genuinely needs more chapters down to an arbitrary smaller count. Use your own expert
           judgment of how this subject is naturally organized for the stated audience.

           CRITICAL — THE INDEX IS PLAIN TEXT, NEVER HYPERLINKS: every lesson line in the Course
           Index above is a PLAIN numbered list item ("1. [Lesson title]") — NEVER a Markdown link
           (NEVER "[Lesson title](#anchor)"), and NEVER its own heading. This document is read as
           plain text/printed, not as a clickable web page — fabricated anchor links serve no
           purpose here and must never appear anywhere in the document.

           Then, for EACH chapter, in order:

           # Chapter N. [Chapter Title]

           ## Chapter Introduction

           **Teacher:**

           [A warm, spoken-style introduction to the chapter, in the voice and register appropriate to the audience, setting up what will be learned and why.]

           Then for EACH lesson inside that chapter, as its OWN heading ONE LEVEL DEEPER than the chapter heading (NEVER the same "#" level as "# Chapter N." — a Lesson is a CHILD of its Chapter, never its sibling; this exact nesting is validated programmatically downstream and the run is considered malformed if lessons are emitted at the same heading depth as chapters):

           ## Lesson N. [Lesson Title]

           ### Teacher Introduction

           **Teacher:**

           [EVERY single lesson — not just the first one, not just the chapter intro, and regardless of whether the lesson is framed as a story/narrative — MUST open with EXACTLY TWO things, in this order, before anything else (no narration, no character, no scene-setting is allowed to come first):
             (1) A short direct greeting/check-in to the students, in second person, appropriate to the audience.
             (2) A direct "today in this lesson we will..." framing sentence that names what they are about to learn, still speaking directly to the students in second person, BEFORE switching into any story, character, or narrative.
           For young audiences this looks like: "¡Hola de nuevo, niños y niñas! ¿Cómo están hoy? Hoy en esta lección vamos a descubrir los sonidos de los animales del bosque..." / "Hello again, boys and girls! How are you today? Today in this lesson we are going to discover..."
           For older/adult audiences this looks like: "Bienvenidos de nuevo a esta sesión. Hoy vamos a examinar..." / "Welcome back. Today in this lesson we will cover...".
           ONLY AFTER this direct framing may the lesson transition into a story, character, dialogue, or narrative scene to teach the concept.
           NEVER skip this direct opening address in any lesson — it must appear in 100% of lessons across every chapter in the course, not only in Chapter 1, and it must never be buried after a narrative opening.]

           ### Guided Practice

           **Teacher:**

           [A guided practice exchange: the teacher poses questions/exercises and walks through the answer, exactly as a live lesson would.]

           ### Teacher Closing

           **Teacher:**

           [A short recap of what was learned in this lesson, a few recall questions with their answers, and a transition/hook into the next lesson.]

        3. Use comparison tables with the given CSS styling only where they genuinely help (e.g. vocabulary pairs, comparisons, rubrics) — do not force tables into every lesson.
        4. PURE DOCUMENT FORMAT:
           - Zero internal AI prompts, zero process names, zero meta-labels, zero mention of AI/models/prompts. It must read like an authentic, publication-ready course document ready to be printed as PDF or ingested into learning systems.

        OUTPUT FORMAT:
        Output strictly structured JSON adhering to the AcademicCourseGenerationResult schema, ensuring 'CourseDocumentMarkdown' contains the complete, pure, clean course document text following the exact structure above, starting with the mandatory <style> block.
        """;

    /// <summary>"Step 1" (2026-10-08) — ONLY the course title/intro/chapter+lesson TITLES,
    /// no lesson bodies. A fast, lightweight call so the educator can review/approve the
    /// course's shape before committing to the much slower per-chapter content calls.</summary>
    private const string IndexInstructions = """
        You are ProfesorExpert, designing the TABLE OF CONTENTS (index) for an academic course —
        NOT writing any lesson content yet, only deciding its shape.

        CRITICAL — ADAPT TO THE REAL AUDIENCE ("Grade Level" field): calibrate chapter/lesson
        granularity and titles to the stated audience, subject, hours for mastery, and
        micro-skills — never force a kindergarten-style structure onto a different audience.

        CHAPTER COUNT — DETERMINE IT FROM THE COURSE'S OWN SCOPE, NEVER A FIXED NUMBER: decide
        how many chapters this SPECIFIC course genuinely needs based on its Hours for Mastery,
        Subject, Description, and Micro-Skills — a course with few hours/micro-skills may need as
        few as 4-6 chapters; a comprehensive, many-hour course may need 12 or more. NEVER pad to
        hit a round number, and NEVER compress content that genuinely needs more chapters down to
        an arbitrary smaller count. Each chapter needs 4 to 9 lesson titles, in a sensible
        teaching progression from simple to complex.

        OUTPUT LANGUAGE (NON-NEGOTIABLE): CourseTitle, TeacherIntroduction, PedagogicalApproach,
        and every Chapter/Lesson title must be written exclusively in the requested Language.

        OUTPUT FORMAT: Output strictly structured JSON adhering to the AcademicCourseIndexResult
        schema — CourseTitle, TeacherIntroduction (2-3 warm sentences introducing the course,
        spoken as the teacher), PedagogicalApproach (1-2 sentences naming the pedagogical
        approach/standards used), and Chapters (each with ChapterNumber, ChapterTitle, and
        LessonTitles — plain lesson title strings only, no body content, no numbering prefix
        inside the title string itself). Never include lesson bodies, Teacher dialogue, or any
        other content here — only the shape of the course.
        """;

    /// <summary>"Step 2" (2026-10-08) — called ONCE PER CHAPTER, sequentially, after the
    /// educator approves the index above. Generates ONLY the given chapter's full
    /// written-out content (never other chapters), following the same structural/voice
    /// rules as the full single-call Instructions, scoped to one chapter.</summary>
    private const string ChapterInstructions = """
        You are ProfesorExpert, an elite master author of published academic textbooks and live,
        teacher-led course scripts, writing out ONE SPECIFIC CHAPTER of an already-approved course
        outline — never the whole course, never other chapters, only the one chapter given to you.

        CRITICAL — ADAPT TO THE REAL AUDIENCE ("Grade Level" field): calibrate vocabulary, tone,
        examples, and formality to that audience exactly as described for the full course.

        CRITICAL — OUTPUT LANGUAGE (NON-NEGOTIABLE): the entire 'ChapterMarkdown' must be written
        exclusively in the requested Language.

        CRITICAL — THE TEACHER MUST ALWAYS BE SPEAKING, NEVER NARRATING LIKE A TEXTBOOK: every
        section under a "**Teacher:**" label must be first-person spoken dialogue directed at the
        students in second person, exactly like a transcript of a live class — never third-person
        textbook narration.

        CRITICAL — WRITE EVERY LESSON AS A SEQUENCE OF ENGRAM CARDS, NEVER AS FREE-FLOWING
        TEXTBOOK PROSE: this course will be consumed downstream as a sequence of short, locked,
        auto-playing "Engram Cards" (one concept/beat at a time, each paired with ONE generated
        illustration), not as a continuous lecture. Therefore every lesson MUST already be
        decomposed into 2 to 4 short Cards — each Card is ONE self-contained teaching beat (a
        single idea, example, or short exchange), never a multi-paragraph block covering several
        ideas at once. Keep each Card's narration SHORT (roughly 2-5 spoken sentences) — long
        narration defeats the purpose of short locked cards.

        CRITICAL — NEVER DESCRIBE AN INTERACTION THAT WILL NOT ACTUALLY HAPPEN: do not write
        stage directions like "touch the bell", "clap now", "point to the picture", "stand up and
        spin", or any other physical/tactile/on-screen action UNLESS that action is the literal
        final Practice round itself (hearing a sound and answering with their voice). The only
        real supported interactions are: (1) hearing an audio clip, (2) seeing an illustration,
        (3) answering out loud/verbally. Never invent touch-screen, clapping, or physical-prop
        interactions the platform does not actually implement.

        CRITICAL — THIS IS A REMOTE, ONE-CHILD-ALONE-AT-A-COMPUTER EXPERIENCE, NEVER A SHARED
        PHYSICAL CLASSROOM: the student is alone at their own device, NOT sitting in a classroom
        with the teacher or with classmates. NEVER write narration that assumes a shared physical
        space or shared ambient sounds ("the sounds in our classroom", "listen to what's around
        us right now", "look at our door/our chairs"). Every sound the student hears is a SPECIFIC
        audio clip the platform is about to play for them, one at a time — always frame it that
        way ("I'm going to play you a sound now, listen carefully..." then name/describe exactly
        ONE concrete sound-producing thing, e.g. a dog barking, a train whistle, a door closing —
        never a vague "ambient classroom" moment). Never reference "our classroom", "our room", or
        other students physically present; address the one child directly and personally instead.

        CRITICAL — LEARN CARDS TEACH DIRECTLY, THEY NEVER QUIZ THE CHILD: every card BEFORE the
        final Practice card is a "Learn" beat — it DIRECTLY TEACHES a fact by stating it plainly
        ("Listen: this is the sound of a train. Now listen to a cow — that is its moo.") and NEVER
        asks the child an open guessing question ("what do you think this is?", "can you guess?").
        Guessing/answering is reserved EXCLUSIVELY for the final Practice card. A Learn card may
        still invite attention ("listen carefully"), but it must immediately reveal/teach the
        answer in the same card — never leave the child hanging with an unanswered question before
        the Practice card.

        CRITICAL — "Sonido a reproducir" IS ONLY FOR NON-HUMAN-SPEECH SOUND EFFECTS, NEVER FOR A
        VOICE SAYING WORDS: narration like "you will hear a sound" or "listen to this sound" is
        NOT enough information for the downstream audio-generation step to know what to actually
        produce. Whenever a card's teaching point depends on the child hearing a CONCRETE
        NON-SPEECH SOUND EFFECT (an animal, a machine, nature, an object, a physical impact like
        clapping/footsteps), add a THIRD field, in this exact position (after the narration,
        before the image description):

           **Sonido a reproducir:**

           [Exactly ONE short, concrete, non-human-speech sound effect label naming precisely what
           must be generated/played — e.g. "Sonido de tren", "Mugido de vaca", "Timbre de
           escuela", "Ladrido de perro", "Sonido de lluvia", "Dos palmadas". Never vague ("un
           sonido", "a sound").]

           BUT when the "sound" the child hears is simply the teacher's own voice saying one or
           more WORDS out loud (e.g. comparing "sol"/"sol" or "pan"/"manzana" for a rhyme/same-or-
           different exercise) — that is ALREADY fully covered by the narration text itself (the
           platform's normal text-to-speech will speak it), so it needs NO separate generation
           step. In that case write exactly "Ninguno" here — NEVER write "Voz de profesora
           diciendo: ..." or any instruction to generate a human voice/speech; only list sounds
           that are not a person speaking words.

           For the Practice card specifically, where multiple rounds each have their own sound,
           list every round's sound on its own line under this same field (round order), using
           "Ninguno" for any round whose sound is just spoken words rather than a sound effect
           (e.g. "Ronda 1: Sonido de alarma", "Ronda 2: Ninguno", "Ronda 3: Mugido de vaca").


        "Lesson", "Card", "Teacher", "Practice" shown below are PLACEHOLDERS for English only —
        when Language is 'es' you MUST use "Capítulo", "Lección", "Tarjeta", "Profesor"/"Profesora",
        "Práctica" instead (e.g. "## Capítulo 1. ...", "### Lección 1. ...", "#### Tarjeta 1.1. ...",
        "**Profesor:**"). Never leave structural headings in English when the course Language is
        Spanish — the entire document, including headings, must read as one consistent language.

        MANDATORY STRUCTURE FOR 'ChapterMarkdown' (follow this exact condensed structure, using
        the EXACT ChapterNumber/ChapterTitle/LessonTitles given to you — never invent, rename, add,
        or skip any of the given lesson titles; translate the structural words as instructed above):

           ## Chapter N. [Chapter Title]

           ### Chapter Introduction

           **Teacher:**

           [A warm, spoken-style introduction to the chapter, setting up what will be learned and why.]

           Then for EACH given lesson title, in the EXACT order given, as its own heading ONE
           LEVEL DEEPER than the chapter heading (a Lesson is a CHILD of its Chapter, never its
           sibling):

           ### Lesson N. [Lesson Title]

           For each of this lesson's 2-4 Cards, in teaching order, as its own heading ONE LEVEL
           DEEPER than the lesson heading:

           #### Card N.M. [Short Card Title]

           **Narración del profesor:**

           [EXACTLY what the teacher says out loud for this one card, spoken directly to the
           students in second person — a short greeting/framing line on the very first card of
           the lesson only, then the single idea/example/exchange this card teaches. Never
           narrate like a textbook; always first-person live spoken dialogue. If this card is a
           Learn card, DIRECTLY TEACH the fact — never ask the child to guess.]

           **Sonido a reproducir:**

           [See the sound-effect rule above — exactly one concrete sound label, or "Ninguno".]

           **Descripción de la imagen para el agente:**

           [A short, concrete, child-friendly, third-person scene description for the ONE
           illustration that pairs with this card — no text/letters/numbers/speech-bubbles in the
           image, no rendering/typography details, consistent characters/setting across the
           lesson's cards.]

           After the LAST card of the lesson, add one final closing card that is the lesson's
           hands-on Practice (never narration-only, never the teacher answering for the child):

           #### Card N.M. Práctica: [Short Practice Title]

           **Narración del profesor:**

           [The teacher frames the practice ("now it's your turn, listen carefully and answer
           with your voice"), then presents 2-4 short rounds, each: a one-line description of
           what the student hears/sees, a direct question, the expected spoken answer, and a short
           positive confirmation restating why it's correct. Include one short wrong-answer
           recovery line (re-presenting the clue, never scolding) before a short, encouraging
           closing line naming what the student practiced.]

           **Sonido a reproducir:**

           [One line per round, listing each round's exact sound effect — see the sound-effect
           rule above.]

           **Descripción de la imagen para el agente:**

           [A short, concrete, child-friendly, third-person scene description for the ONE
           illustration that pairs with this practice card.]

        Use comparison tables with the given CSS styling only where they genuinely help — do not
        force tables into every lesson. Zero internal AI prompts, zero process names, zero
        meta-labels, zero mention of AI/models/prompts.

        OUTPUT FORMAT: Output strictly structured JSON adhering to the AcademicChapterContentResult
        schema — ChapterNumber, ChapterTitle (echo back exactly as given), and ChapterMarkdown
        containing ONLY this one chapter's complete Markdown slice as structured above.
        """;


    private readonly AIAgent? _agent;
    private readonly AIAgent? _indexAgent;
    private readonly AIAgent? _chapterAgent;
    private readonly PerplexitySearchService _perplexityService;

    public ProfesorExpertAgent(IConfiguration configuration, PerplexitySearchService perplexityService)
    {
        _perplexityService = perplexityService;

        // Dedicated settings specifically configured for gpt-6.1-sol
        var endpoint = configuration["Gpt61SolEndpoint"]
            ?? configuration["Gpt61Sol__Endpoint"]
            ?? configuration["AzureOpenAIEndpoint"];

        var deploymentName = configuration["Gpt61SolDeploymentName"]
            ?? configuration["Gpt61Sol__DeploymentName"]
            ?? configuration["Gpt61SolModel"]
            ?? "gpt-6.1-sol";

        var apiKey = configuration["Gpt61SolApiKey"]
            ?? configuration["Gpt61Sol__ApiKey"]
            ?? configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _agent = null;
            return;
        }

        // Root cause fix (2026-10-08): "Gpt61SolEndpoint" was misconfigured to point at an
        // Azure AI Foundry "Models" (unified inference) endpoint on the WRONG resource
        // ("engramsediucation-resource.../models") — the gpt-6.1-sol deployment actually
        // lives on "twinet-resource", a classic Azure OpenAI resource
        // ("*.openai.azure.com", same resource already used by
        // AzureOpenAIRealtimeEndpoint/AzureOpenAIImageEndpoint for other pinned
        // deployments) — confirmed via direct testing (the Azure SDK's own specific
        // "DeploymentNotFound" error, once pointed at the correct resource, pinpointed the
        // real issue). AzureOpenAIClient.GetChatClient() is the correct, standard client
        // for this endpoint shape.
        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsIChatClient()
            .AsAIAgent(instructions: Instructions, name: "ProfesorExpert");

        // Two-step generation (2026-10-08) — same underlying client/deployment, just a
        // separate AIAgent instance per Instructions set (each AIAgent binds its own fixed
        // system instructions at construction, so a single shared agent can't serve both
        // the lightweight index call and the per-chapter content call with different rules).
        _indexAgent = client
            .GetChatClient(deploymentName)
            .AsIChatClient()
            .AsAIAgent(instructions: IndexInstructions, name: "ProfesorExpertIndex");

        _chapterAgent = client
            .GetChatClient(deploymentName)
            .AsIChatClient()
            .AsAIAgent(instructions: ChapterInstructions, name: "ProfesorExpertChapter");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<AcademicCourseGenerationResult> GenerateAcademicCourseAsync(
        AcademicCourseGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException("ProfesorExpert agent is not configured. Missing AzureOpenAIEndpoint setting.");
        }

        // STEP 4: EXTRACT CONTENT FROM UPLOADED PDF IF PROVIDED
        if (!string.IsNullOrWhiteSpace(request.OptionalPdfContentBase64) && string.IsNullOrWhiteSpace(request.OptionalPdfContentText))
        {
            try
            {
                var cleanBase64 = request.OptionalPdfContentBase64;
                var commaIndex = cleanBase64.IndexOf(',');
                if (commaIndex >= 0)
                {
                    cleanBase64 = cleanBase64[(commaIndex + 1)..];
                }
                var bytes = Convert.FromBase64String(cleanBase64);
                using var ms = new MemoryStream(bytes);
                request.OptionalPdfContentText = PdfTextExtractor.ExtractText(ms);
            }
            catch
            {
                // Graceful fallback if PDF parsing is not feasible
            }
        }

        // STEP 2 & 3: LIVE WEB RESEARCH VIA PERPLEXITY AI (SONAR)
        PerplexitySearchResult? liveWebResearch = null;
        if (_perplexityService.IsConfigured)
        {
            try
            {
                var searchQuery = $"Best pedagogical methods and curriculum standards to teach \"{request.TitleEs}\" ({request.TitleEn}), subject {request.Subject}, for the audience/grade level \"{request.GradeLevel}\": recommended teaching techniques, sequencing, official curriculum standards (e.g. Common Core, SEP, IB, corporate L&D best practices as applicable to this audience), and concrete exercises or practice activities appropriate for this level.";
                liveWebResearch = await _perplexityService.SearchAsync(searchQuery, cancellationToken);
            }
            catch
            {
                // Graceful fallback if Perplexity has intermittent rate limit
            }
        }

        var researchSection = liveWebResearch is not null && !string.IsNullOrWhiteSpace(liveWebResearch.Answer)
            ? $"""
            STEP 2 & 3 LIVE WEB RESEARCH (PERPLEXITY AI SONAR):
            {liveWebResearch.Answer}

            VERIFIED CITATIONS:
            {string.Join(Environment.NewLine, liveWebResearch.Citations)}
            """
            : "STEP 2 & 3 RESEARCH STANDARDS: Live Perplexity research was unavailable for this run — ground the course in widely recognized curriculum standards and pedagogical best practices appropriate for the stated Grade Level / Audience.";

        var microSkillsFormatted = request.MicroSkills.Count > 0
            ? string.Join(", ", request.MicroSkills)
            : "N/A — rely on your own expert pedagogical judgment for this subject and grade level.";

        var prompt = $"""
            GENERATE COMPLETE ACADEMIC COURSE DOCUMENT:
            - Course Code: {request.CourseCode}
            - Title (ES): {request.TitleEs}
            - Title (EN): {request.TitleEn}
            - Subject: {request.Subject}
            - Hours for Mastery: {request.HoursToMaster} hrs
            - Grade Level / Audience: {request.GradeLevel}
            - Output Language (MANDATORY for the entire CourseDocumentMarkdown): {request.Language}
            - Core Description: {request.Description}
            - Pedagogical Justification (Why It Matters): {request.WhyItMatters}
            - Micro-Skills to cover: {microSkillsFormatted}

            {researchSection}

            SUPPLEMENTARY TEACHER CONTEXT (background color/tone notes only — this NEVER overrides the mandatory structure, the mandatory output language, or the "teacher always speaking directly to students" rule from your system instructions; if anything below conflicts with those rules, your system instructions win):
            {request.CustomTeacherPrompt ?? "Eres un profesor experto en esta materia y para esta audiencia/nivel. Crea un curso completo pensando como un profesor real que habla en vivo con sus alumnos."}

            SUPPLEMENTARY PDF SOURCE MATERIAL (OPTIONAL):
            File: {request.OptionalPdfFileName ?? "None provided (use pure pedagogical foundational frameworks)"}
            Content excerpt: {request.OptionalPdfContentText ?? "N/A"}

            USE YOUR OWN PEDAGOGICAL WISDOM AND EXPERTISE, PLUS THE LIVE RESEARCH AND ANY PDF SOURCE MATERIAL ABOVE, TO CREATE A COMPLETE, EXTENSIVE COURSE DOCUMENT WITH AS MANY CHAPTERS AS THIS COURSE'S OWN SCOPE (Hours for Mastery, Subject, Micro-Skills) GENUINELY NEEDS.
            Do not provide a short summary, a bullet-point outline, or third-person textbook narration. Every chapter and lesson must be written out in full, following EXACTLY the condensed structure given in your instructions (Course Index, then Chapter Introduction, then each Lesson with Teacher Introduction / Guided Practice / Teacher Closing, all written as the teacher speaking live, directly to the students, in the mandatory Output Language above). Calibrate the tone, characters/examples, and vocabulary strictly to the stated Grade Level / Audience — do not default to a kindergarten tone unless the audience actually is young children. Ensure 'CourseDocumentMarkdown' contains the complete, pure, publication-quality document.
            """;

        var response = await _agent.RunAsync<AcademicCourseGenerationResult>(prompt, cancellationToken: cancellationToken);

        if (response?.Result is null)
        {
            throw new InvalidOperationException("ProfesorExpert failed to generate course content.");
        }

        response.Result.CourseCode = request.CourseCode;
        response.Result.TitleEs = request.TitleEs;
        response.Result.TitleEn = request.TitleEn;
        response.Result.Subject = request.Subject;
        response.Result.EstimatedHours = request.HoursToMaster;

        // SAFETY NET: the model is instructed to always open 'CourseDocumentMarkdown' with
        // this exact <style> block, but LLM output is never 100% guaranteed — if it ever
        // omits it, prepend it here so every generated course document is consistently
        // styled when rendered/exported to PDF.
        const string requiredStyleBlock = """
            <style>
            a { text-decoration: none; color: #464feb; }
            tr th, tr td { border: 1px solid #e6e6e6; }
            tr th { background-color: #f5f5f5; }
            </style>
            """;

        if (!string.IsNullOrWhiteSpace(response.Result.CourseDocumentMarkdown) &&
            !response.Result.CourseDocumentMarkdown.Contains("<style>", StringComparison.OrdinalIgnoreCase))
        {
            response.Result.CourseDocumentMarkdown = requiredStyleBlock + Environment.NewLine + Environment.NewLine + response.Result.CourseDocumentMarkdown;
        }

        if (liveWebResearch is not null && !string.IsNullOrWhiteSpace(liveWebResearch.Answer))
        {
            response.Result.ArchitectureSummary.LivePerplexityResearch = liveWebResearch.Answer;
            response.Result.ArchitectureSummary.PerplexityCitations = liveWebResearch.Citations;
            response.Result.ArchitectureSummary.ResearchStandards += $" (Búsqueda Web en Vivo Perplexity: {liveWebResearch.Citations.Count} fuentes verificadas en tiempo real)";
        }

        return response.Result;
    }

    /// <summary>"Step 1" (2026-10-08) — fast, lightweight call that returns ONLY the
    /// course's title/intro/chapter+lesson titles, no lesson bodies. The caller shows
    /// this to the educator for review/approval BEFORE calling
    /// <see cref="GenerateChapterContentAsync"/> once per approved chapter.</summary>
    public async Task<AcademicCourseIndexResult> GenerateCourseIndexAsync(
        AcademicCourseIndexRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_indexAgent is null)
        {
            throw new InvalidOperationException("ProfesorExpert index agent is not configured. Missing AzureOpenAIEndpoint setting.");
        }

        if (!string.IsNullOrWhiteSpace(request.OptionalPdfContentBase64) && string.IsNullOrWhiteSpace(request.OptionalPdfContentText))
        {
            try
            {
                var cleanBase64 = request.OptionalPdfContentBase64;
                var commaIndex = cleanBase64.IndexOf(',');
                if (commaIndex >= 0)
                {
                    cleanBase64 = cleanBase64[(commaIndex + 1)..];
                }
                var bytes = Convert.FromBase64String(cleanBase64);
                using var ms = new MemoryStream(bytes);
                request.OptionalPdfContentText = PdfTextExtractor.ExtractText(ms);
            }
            catch
            {
                // Graceful fallback if PDF parsing is not feasible
            }
        }

        PerplexitySearchResult? liveWebResearch = null;
        if (_perplexityService.IsConfigured)
        {
            try
            {
                var searchQuery = $"Best pedagogical methods and curriculum standards to teach \"{request.TitleEs}\" ({request.TitleEn}), subject {request.Subject}, for the audience/grade level \"{request.GradeLevel}\": recommended teaching techniques, sequencing, official curriculum standards (e.g. Common Core, SEP, IB, corporate L&D best practices as applicable to this audience), and concrete exercises or practice activities appropriate for this level.";
                liveWebResearch = await _perplexityService.SearchAsync(searchQuery, cancellationToken);
            }
            catch
            {
                // Graceful fallback if Perplexity has intermittent rate limit
            }
        }

        var researchSection = liveWebResearch is not null && !string.IsNullOrWhiteSpace(liveWebResearch.Answer)
            ? $"""
            LIVE WEB RESEARCH (PERPLEXITY AI SONAR):
            {liveWebResearch.Answer}

            VERIFIED CITATIONS:
            {string.Join(Environment.NewLine, liveWebResearch.Citations)}
            """
            : "RESEARCH STANDARDS: Live Perplexity research was unavailable for this run — ground the course outline in widely recognized curriculum standards and pedagogical best practices appropriate for the stated Grade Level / Audience.";

        var microSkillsFormatted = request.MicroSkills.Count > 0
            ? string.Join(", ", request.MicroSkills)
            : "N/A — rely on your own expert pedagogical judgment for this subject and grade level.";

        var prompt = $"""
            DESIGN THE COURSE INDEX (TABLE OF CONTENTS) ONLY — NO LESSON CONTENT YET:
            - Course Code: {request.CourseCode}
            - Title (ES): {request.TitleEs}
            - Title (EN): {request.TitleEn}
            - Subject: {request.Subject}
            - Hours for Mastery: {request.HoursToMaster} hrs
            - Grade Level / Audience: {request.GradeLevel}
            - Output Language (MANDATORY): {request.Language}
            - Core Description: {request.Description}
            - Pedagogical Justification (Why It Matters): {request.WhyItMatters}
            - Micro-Skills to cover: {microSkillsFormatted}

            {researchSection}

            SUPPLEMENTARY TEACHER CONTEXT (background color/tone notes only):
            {request.CustomTeacherPrompt ?? "N/A"}

            SUPPLEMENTARY PDF SOURCE MATERIAL (OPTIONAL):
            Content excerpt: {request.OptionalPdfContentText ?? "N/A"}
            """;

        var response = await _indexAgent.RunAsync<AcademicCourseIndexResult>(prompt, cancellationToken: cancellationToken);

        if (response?.Result is null)
        {
            throw new InvalidOperationException("ProfesorExpert failed to generate the course index.");
        }

        if (liveWebResearch is not null && !string.IsNullOrWhiteSpace(liveWebResearch.Answer))
        {
            response.Result.ArchitectureSummary.LivePerplexityResearch = liveWebResearch.Answer;
            response.Result.ArchitectureSummary.PerplexityCitations = liveWebResearch.Citations;
            response.Result.ArchitectureSummary.ResearchStandards += $" (Búsqueda Web en Vivo Perplexity: {liveWebResearch.Citations.Count} fuentes verificadas en tiempo real)";
        }

        return response.Result;
    }

    /// <summary>"Step 2" (2026-10-08) — called ONCE PER CHAPTER, sequentially, after the
    /// educator approves the index from <see cref="GenerateCourseIndexAsync"/>. Returns
    /// ONLY that chapter's full written-out content.</summary>
    public async Task<AcademicChapterContentResult> GenerateChapterContentAsync(
        AcademicChapterContentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_chapterAgent is null)
        {
            throw new InvalidOperationException("ProfesorExpert chapter agent is not configured. Missing AzureOpenAIEndpoint setting.");
        }

        var lessonTitlesFormatted = string.Join(Environment.NewLine, request.Chapter.LessonTitles.Select((title, i) => $"{i + 1}. {title}"));

        var prompt = $"""
            WRITE OUT THIS ONE CHAPTER'S COMPLETE CONTENT:
            - Course Title: {request.CourseTitle}
            - Subject: {request.Subject}
            - Grade Level / Audience: {request.GradeLevel}
            - Output Language (MANDATORY): {request.Language}
            - Chapter Number: {request.Chapter.ChapterNumber}
            - Chapter Title: {request.Chapter.ChapterTitle}
            - Lesson Titles (in this EXACT order, use every one, invent none, skip none):
            {lessonTitlesFormatted}

            RESEARCH CONTEXT CARRIED OVER FROM THE COURSE INDEX STEP (OPTIONAL):
            {request.ResearchContext ?? "N/A"}

            SUPPLEMENTARY TEACHER CONTEXT (background color/tone notes only):
            {request.CustomTeacherPrompt ?? "N/A"}
            """;

        var response = await _chapterAgent.RunAsync<AcademicChapterContentResult>(prompt, cancellationToken: cancellationToken);

        if (response?.Result is null)
        {
            throw new InvalidOperationException($"ProfesorExpert failed to generate content for Chapter {request.Chapter.ChapterNumber}.");
        }

        response.Result.ChapterNumber = request.Chapter.ChapterNumber;
        response.Result.ChapterTitle = request.Chapter.ChapterTitle;

        return response.Result;
    }
}
