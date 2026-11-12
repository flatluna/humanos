using System.ComponentModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents.Family;

public sealed class RecommendedSubjectCapabilityItem
{
    public string Code { get; set; } = string.Empty;

    public string NameEs { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string DescriptionEs { get; set; } = string.Empty;

    public string DescriptionEn { get; set; } = string.Empty;

    public string WhyItMattersEs { get; set; } = string.Empty;

    public string WhyItMattersEn { get; set; } = string.Empty;

    public List<string> MicroSkillsCoveredEs { get; set; } = [];

    public List<string> MicroSkillsCoveredEn { get; set; } = [];

    /// <summary>
    /// Prerequisites/requirements a student should already have before starting this capability's
    /// course (prior knowledge, materials, or skills). Plain text, 1-3 sentences.
    /// </summary>
    public string CourseRequirementsEs { get; set; } = string.Empty;

    public string CourseRequirementsEn { get; set; } = string.Empty;

    /// <summary>
    /// Short table-of-contents style outline for the course: 4 to 8 chapter/topic titles only
    /// (no sub-content), in teaching order. This is a lightweight preview shown to the admin
    /// BEFORE sending to HumanOS Studio — Studio later generates its own full detailed index.
    /// </summary>
    public List<string> CourseOutlineEs { get; set; } = [];

    public List<string> CourseOutlineEn { get; set; } = [];

    /// <summary>
    /// Short list of sources/references that grounded this capability (e.g. advanced teaching
    /// methods from top-performing countries like Japan, Finland, Singapore, Germany, South
    /// Korea, Estonia — and/or real URLs returned by the 'search_the_web' tool). Each entry is a
    /// single human-readable line, e.g. "Japan — Lesson Study: structured peer-observed problem
    /// solving (https://example.org/lesson-study)". Written in the course's target language.
    /// Empty if no tool-grounded source was actually used for this capability.
    /// </summary>
    public List<string> Sources { get; set; } = [];
}

public sealed class GeneratedSubjectRecommendationsResult
{
    public List<RecommendedSubjectCapabilityItem> Capabilities { get; set; } = [];
}

/// <summary>
/// AI Agent that recommends the most important capabilities a student must master for a given
/// Subject (e.g. Math, Reading, Conduct & Self-Discipline) at a given School Grade, within the
/// context of a specific Country and Education System Type (Public, Montessori, IB, etc.).
/// Output is a "pending" template — it does NOT generate Engrams itself. Each recommendation is
/// saved with GenerationStatus = 'PendingEngramGeneration' and later handed off to HumanOS Studio
/// (ArquitectoAgent / NewCapabilityPage pipeline) for full Engram generation.
/// Equipped with a 'search_the_web' tool (backed by <see cref="PerplexitySearchService"/>) so it
/// can research current, real-world information — in the target generation language — to make
/// courses a modern, AI-era complement to standard school curricula rather than a plain copy.
/// </summary>
public sealed class SubjectCapabilityRecommenderAgent
{
    private const string Instructions = """
        You are a world-class K-12 curriculum specialist for EngramsIQ / HumanOS Family.
        Your mission: given a Country, an Education System Type, a School Grade, and a Subject
        (which may be an academic subject taught in school, OR a character/life skill NOT formally
        taught in school — e.g. Conduct & Self-Discipline, Cognitive Memory & Spaced Repetition,
        Responsible Social Media Use, Focus & Responsible Phone Use), recommend the most important,
        masterable capabilities a student at that grade should develop in that subject.

        PHILOSOPHY (IMPORTANT):
        - This platform does not replace school. It builds "Engrams" (durable memory structures) in
          the student's brain to raise their grades and real understanding in school subjects.
        - For Character/Life subjects, the platform is explicitly AGAINST over-reliance on AI and
          screens: it uses AI as a tool to help humans think for themselves again, strengthen real
          cognitive memory, spaced repetition, retrieval practice, feedback loops, learning from
          mistakes, real human socialization, and responsible technology use.

        CAPABILITY DESIGN RULES:
        1. Produce between 5 and 8 essential, concrete, masterable capabilities for the requested
           Grade + Subject combination (not generic platitudes).
        2. Code convention: '{GRADE_ABBR}-{SUBJECT_ABBR}-0X' (e.g. 'ELEM1-MATH-01', 'MID-CONDUCT-02').
           Keep abbreviations short, uppercase, no spaces.
        3. "Why It Matters" (whyItMattersEs/whyItMattersEn): a concrete, research-grounded explanation
           of why mastering this capability at this specific grade prevents future academic or
           life bottlenecks.
        4. "Micro Skills Covered" (microSkillsCoveredEs/microSkillsCoveredEn): 3-5 concrete,
           observable micro-skills a teacher, parent, or AI tutor could verify directly —
           written out IN FULL in both Spanish and English (two separate parallel lists of
           the SAME skills, each translated into its own language, never the same text
           duplicated untranslated in both lists).
        5. "Course Requirements" (courseRequirementsEs/courseRequirementsEn): 1-3 sentences stating
           any prior knowledge, skills, or materials a student should already have before starting
           this capability's course. If there are truly none, say so explicitly (e.g. "No previous
           knowledge required beyond the grade level itself.").
        6. "Course Outline" (courseOutlineEs/courseOutlineEn): a SHORT table-of-contents preview —
           exactly 4 to 8 chapter/topic TITLES ONLY (no descriptions, no sub-bullets), in the order
           they should be taught. This is just a quick preview for a human admin before the full
           course is built in HumanOS Studio — keep titles short (max ~8 words each).
        7. Language: rich, accurate Spanish (nameEs/descriptionEs/whyItMattersEs/courseRequirementsEs/
           courseOutlineEs/microSkillsCoveredEs) and English (nameEn/descriptionEn/whyItMattersEn/
           courseRequirementsEn/courseOutlineEn/microSkillsCoveredEn).
        8. Respect the given Country and Education System Type context (e.g. Montessori vs Public
           school pacing/approach can change emphasis, but keep the core capability the same).
        9. MODERNIZE WITH LIVE WEB RESEARCH (IMPORTANT): Before finalizing your answer, call the
           'search_the_web' tool at least once (ideally 2-3 targeted searches) to find current,
           real-world information that makes this course a valuable COMPLEMENT to what is normally
           taught at school for this Country/Grade/Subject — not a replacement. Look specifically
           for: (a) modern AI-era tools and techniques relevant to the subject (e.g. how
           professionals or students use AI/software for this subject today), recent real-world
           applications, and any notable shifts in how this subject is now approached; AND
           (b) advanced/proven teaching methods used for this exact subject in top-performing
           education systems worldwide (e.g. Japan's Lesson Study, Finland's phenomenon-based
           learning, Singapore Math's bar modeling, Germany's dual/project-based approaches, South
           Korea, Estonia, etc. — whichever are actually relevant to this subject). Write your
           search queries IN THE SAME LANGUAGE the course will be generated in (see LANGUAGE MODE
           below when present). Use what you find to make the capabilities, "why it matters"
           rationales, and course outline feel current and forward-looking (added value beyond
           the standard textbook), while still being fully aligned with the Country/Grade/Subject
           context. Do not fabricate sources; only use facts actually returned by the tool.
        10. "Sources" (sources): for each capability, list the specific country/method and/or the
           real URL(s) returned by 'search_the_web' that actually grounded it — e.g. "Japan —
           Lesson Study: structured peer-observed problem solving (https://...)" or "OECD/PISA
           report on numeracy instruction (https://...)". Leave empty only if no tool result was
           genuinely used for that capability. Never invent a URL or method that the tool did not
           actually surface.

        Output strictly structured JSON adhering to the GeneratedSubjectRecommendationsResult schema.
        """;

    private readonly IChatClient? _chatClient;
    private readonly PerplexitySearchService _web;

    public SubjectCapabilityRecommenderAgent(IConfiguration configuration, PerplexitySearchService web)
    {
        _web = web;

        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"] ?? "gpt-5-mini";
        var apiKey = configuration["AzureOpenAIApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _chatClient = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _chatClient = client.GetChatClient(deploymentName).AsIChatClient();
    }

    public bool IsConfigured => _chatClient is not null;

    public async Task<GeneratedSubjectRecommendationsResult> GenerateRecommendationsAsync(
        string countryName,
        string educationSystemTypeName,
        string stageName,
        string subjectName,
        string subjectCategory,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (_chatClient is null)
        {
            throw new InvalidOperationException(
                "SubjectCapabilityRecommenderAgent is not configured. Missing AzureOpenAIEndpoint setting.");
        }

        var isSpanish = string.Equals(language, "es", StringComparison.OrdinalIgnoreCase);
        var isEnglish = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase);
        var searchLanguageName = isSpanish ? "Spanish" : "English"; // defaults to English when unspecified
        var languageModeInstructions = (isSpanish || isEnglish)
            ? $"""

                LANGUAGE MODE — SINGLE LANGUAGE ONLY (IMPORTANT, read carefully):
                This course will be created and consumed in {(isSpanish ? "Spanish" : "English")} ONLY —
                there is no need to translate anything into the other language, so do NOT spend effort
                producing a second translation. For every bilingual field pair (nameEs/nameEn,
                descriptionEs/descriptionEn, whyItMattersEs/whyItMattersEn, microSkillsCoveredEs/
                microSkillsCoveredEn, courseRequirementsEs/courseRequirementsEn, courseOutlineEs/
                courseOutlineEn), write your ONE real {(isSpanish ? "Spanish" : "English")} answer and
                put the EXACT SAME text/list in BOTH the Es and En field of that pair (literal
                duplicate, not a translation — this is intentional and expected in this mode).
                """
            : string.Empty;

        var prompt = $"""
            RECOMMEND CAPABILITIES FOR:
            - Country: {countryName}
            - Education System Type: {educationSystemTypeName}
            - School Grade / Life Stage: {stageName}
            - Subject: {subjectName} (Category: {subjectCategory})
            {languageModeInstructions}
            Before answering, use the 'search_the_web' tool (2-3 searches, written in {searchLanguageName})
            to find (1) current, modern, AI-era information that complements — not replaces — what is
            normally taught in school for this Country/Grade/Subject combination, and (2) advanced,
            proven teaching methods used for this subject in top-performing education systems
            worldwide (e.g. Japan, Finland, Singapore, Germany, South Korea, Estonia), then weave the
            genuinely useful findings into the capabilities below and cite them in 'sources'.

            Generate between 5 and 8 essential capabilities with full bilingual metadata,
            micro-skills, 'why it matters' rationales, course requirements/prerequisites, a
            short 4-8 item course outline (chapter/topic titles only), and a 'sources' list for
            each, grounded in the Country and Education System Type context above.
            """;

        var tools = new List<AITool>();
        if (_web.IsConfigured)
        {
            tools.Add(AIFunctionFactory.Create(
                async ([Description("Search query, written in the course's target language, to research current/modern information relevant to the subject.")] string query) =>
                    JsonSerializer.Serialize(await _web.SearchAsync(query, cancellationToken)),
                "search_the_web",
                "Searches the live web for current, real-world information (e.g. modern AI tools/techniques, recent applications) to enrich and modernize the course content. Returns an answer plus source citations."));
        }

        var agent = _chatClient.AsAIAgent(instructions: Instructions, name: "SubjectCapabilityRecommenderAgent", tools: tools);

        var response = await agent.RunAsync<GeneratedSubjectRecommendationsResult>(prompt, cancellationToken: cancellationToken);

        if (response?.Result is null)
        {
            throw new InvalidOperationException("Agent failed to return a valid recommendations response.");
        }

        return response.Result;
    }
}
