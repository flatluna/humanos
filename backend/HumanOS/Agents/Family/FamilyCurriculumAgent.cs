using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

namespace HumanOS.Agents.Family;

public sealed class GeneratedCurriculumResult
{
    public string StageKey { get; set; } = string.Empty;

    public string StageName { get; set; } = string.Empty;

    public string TargetAgeRange { get; set; } = string.Empty;

    public List<GeneratedCapabilityItem> Capabilities { get; set; } = [];
}

public sealed class GeneratedCapabilityItem
{
    public string Code { get; set; } = string.Empty;

    public string NameEs { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// Must be one of: 'STEM & Math', 'Language & Literacy', 'Socio-Emotional', 'Critical Thinking', 'Practical Life'
    /// </summary>
    public string Domain { get; set; } = "STEM & Math";

    public string DescriptionEs { get; set; } = string.Empty;

    public string DescriptionEn { get; set; } = string.Empty;

    public string WhyItMattersEs { get; set; } = string.Empty;

    public string WhyItMattersEn { get; set; } = string.Empty;

    /// <summary>
    /// 'Foundation', 'Proficient', 'Advanced', or 'Master'
    /// </summary>
    public string TargetMasteryLevel { get; set; } = "Master";

    public int RecommendedHoursToMaster { get; set; } = 40;

    public List<string> SkillsCovered { get; set; } = [];
}

/// <summary>
/// State-of-the-art Pedagogical Curriculum Architect Agent for Family &amp; Education.
/// Built using Microsoft.Agents.AI with Azure OpenAI (gpt-5-mini).
/// Generates global reference capabilities across human life stages (Kindergarten to Adulthood)
/// grounded in US Common Core, Mexico SEP / LATAM frameworks, and IB standards.
/// </summary>
public sealed class FamilyCurriculumAgent
{
    private const string Instructions = """
        You are a world-class K-12 and human development pedagogical curriculum architect for EngramsIQ / HumanOS Family.
        Your mission is to formulate the core global capabilities (skills) required for academic, cognitive, and socio-emotional mastery at a given human stage or school grade (e.g. Kindergarten, 1st Grade, Middle School, High School, College, Professional Adulthood).

        STANDARDS & PEDAGOGICAL GROUNDING:
        - Integrate the best of US Common Core Standards (Reading, Writing, Math).
        - Integrate Mexican SEP and Latin American official competency frameworks.
        - Integrate International Baccalaureate (IB) learner profile inquiry principles.
        - Ensure 100% bicultural and bilingual parity (Spanish and English).

        CAPABILITY DESIGN RULES:
        1. Produce between 4 and 6 ESSENTIAL capabilities for the requested stage. Each must represent a genuine, masterable capability—not trivial trivia.
        2. Code convention:
           - Kindergarten: KIND-LIT-0X, KIND-NUM-0X, KIND-SEL-0X, KIND-SCI-0X
           - 1st & 2nd Grade: ELEM1-MATH-0X, ELEM1-LIT-0X, ELEM1-HAB-0X
           - 3rd - 5th Grade: ELEM2-MATH-0X, ELEM2-LIT-0X, ELEM2-SCI-0X
           - Middle School: MID-MATH-0X, MID-CODE-0X, MID-COMM-0X
           - High School: HIGH-TEST-0X, HIGH-CALC-0X, HIGH-LEAD-0X
           - College / Adult: UNIV-RES-0X, EXEC-AI-0X, EXEC-WEALTH-0X
        3. Domain must strictly be one of:
           - 'STEM & Math'
           - 'Language & Literacy'
           - 'Socio-Emotional'
           - 'Critical Thinking'
           - 'Practical Life'
        4. "Why It Matters" (whyItMattersEs / whyItMattersEn):
           - Provide a profound, research-backed explanation directed at parents and educators explaining why mastering this capability at this specific age prevents future bottlenecks or unlocks elite academic success.
        5. "Skills Covered":
           - Provide 4 concrete, observable micro-competencies that a teacher, parent, or AI tutor can verify.
        6. Language:
           - Provide accurate, rich, elegant Spanish (nameEs, descriptionEs, whyItMattersEs) and English (nameEn, descriptionEn, whyItMattersEn).

        Output strictly structured JSON adhering to the GeneratedCurriculumResult schema.
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName;

    public FamilyCurriculumAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"] ?? "gpt-5-mini";
        var apiKey = configuration["AzureOpenAIApiKey"];

        _deploymentName = deploymentName;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        _agent = client
            .GetChatClient(deploymentName)
            .AsIChatClient()
            .AsAIAgent(instructions: Instructions, name: "FamilyCurriculumAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>
    /// Executes the agent to generate standard global capabilities for a school grade / life stage.
    /// </summary>
    public async Task<GeneratedCurriculumResult> GenerateCurriculumAsync(
        string stageKey,
        string stageName,
        string ageRange,
        string? additionalFocus = null,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "FamilyCurriculumAgent is not configured. Missing AzureOpenAIEndpoint setting.");
        }

        var prompt = $"""
            GENERATE GLOBAL REFERENCE CAPABILITIES FOR:
            - Life Stage / School Level: {stageName} (StageKey: '{stageKey}')
            - Age Range: {ageRange}
            - Additional Custom Directives / Focus from Admin: {additionalFocus ?? "Standard bilingual excellence (US Common Core + Mexico SEP)"}

            Generate between 4 and 6 foundational capabilities with full bilingual metadata, micro-skills, and 'why it matters' rationales.
            """;

        var response = await _agent.RunAsync<GeneratedCurriculumResult>(prompt, cancellationToken: cancellationToken);

        if (response?.Result is null)
        {
            throw new InvalidOperationException("Agent failed to return a valid curriculum response.");
        }

        response.Result.StageKey = stageKey;
        response.Result.StageName = stageName;
        response.Result.TargetAgeRange = ageRange;

        return response.Result;
    }
}
