using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Agents.Studio;
using HumanOS.Contracts.People;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>The structured résumé/CV fields the extraction agent proposes
/// — a pure PROPOSAL for an HRAdmin to review (see
/// ExtractPersonResumeFunction and the EmployeeDetailPage before/after
/// review UI), never written directly to
/// <see cref="HumanOS.Models.People.PersonResume"/>.</summary>
public sealed class ResumeExtractionProposal
{
    public StudentAcademicProfile? StudentAcademicProfile { get; set; }

    public string? Address { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? ProfessionalObjective { get; set; }

    public string? ProfessionalSummary { get; set; }

    public List<string> Skills { get; set; } = [];

    public List<string> WorkExperience { get; set; } = [];

    public List<string> Education { get; set; } = [];

    public List<string> Certifications { get; set; } = [];

    public List<string> Languages { get; set; } = [];
}

/// <summary>Result of an extraction call: the proposal plus the token
/// usage of the call that produced it (observability only) — same shape
/// as JobRoleDescriptionGenerationResult.</summary>
public sealed class ResumeExtractionResult
{
    public ResumeExtractionProposal Proposal { get; set; } = null!;

    public AgentTokenUsage TokenUsage { get; set; } = null!;
}

/// <summary>
/// Extracts structured résumé/CV fields (address, LinkedIn, professional
/// objective/summary, skills, work experience, education, certifications,
/// languages) from a person's raw pasted résumé text, via Microsoft Agent
/// Framework (https://learn.microsoft.com/en-us/agent-framework/) — same
/// <c>Microsoft.Agents.AI.OpenAI</c> Azure OpenAI Chat Completion provider
/// as every other agent in this codebase.
///
/// Purely extractive, same rationale as <see cref="JobDescriptionExtractionAgent"/>:
/// bounded field extraction from already-provided text ("only extract what's
/// explicitly present, never invent") — a good candidate for the cheaper
/// "economy" deployment, not the main one. Deliberately has NO tools
/// (unlike <see cref="JobRoleDescriptionAgent"/>'s search_similar_job_roles):
/// there is no organizational data relevant to extracting one person's OWN
/// résumé, so a plain ChatClientAgent is enough.
///
/// Singleton, same rationale as every other Studio/agent-framework agent in
/// this codebase (holds no per-request state — only a configured AIAgent
/// instance).
///
/// TODO: Set the "AzureOpenAIEndpoint" and "AzureOpenAIDeploymentName"
/// application settings once real credentials are provided — see
/// <see cref="JobDescriptionExtractionAgent"/>.
/// </summary>
public sealed class ResumeExtractionAgent
{
    private const string Instructions = """
        You are an expert HR and talent intelligence agent that extracts structured résumé/CV information from raw résumé text for an enterprise HR platform.

        Your goal is to accurately parse and decompose the text into structured, high-quality JSON fields.

        ⚠️ CRITICAL: WORK EXPERIENCE MUST ALWAYS BE SEPARATE BY COMPANY ⚠️
        
        THE #1 MISTAKE TO AVOID:
        ❌ WRONG (creates a single "chorizo" entry):
           workExperience: ["Company A — Role (2022-2026): Description 1. Company B — Role (2020-2022): Description 2. Company C — Role (2018-2020): Description 3."]
        
        ✅ CORRECT (each company is its own item):
           workExperience: [
             "Company A — Role (2022-2026): Description 1",
             "Company B — Role (2020-2022): Description 2",
             "Company C — Role (2018-2020): Description 3"
           ]

        ALGORITHM FOR PARSING DENSE, FLOWING RESUME TEXT:
        1. Look for company name patterns: Proper nouns (all caps or Title Case) often at the start of a sentence or after punctuation
        2. Immediately after company name, expect: Job title(s), then date range in format like "(YYYY-YYYY)" or "(YYYY–YYYY)"
        3. Everything after the date until the NEXT company name/date pattern belongs to that company
        4. Split at company boundaries — NEVER concatenate multiple companies into one array element
        5. Common company name indicators: "—", "●", "-", ".", or newline before company name
        6. Common date patterns: (1999-2005), (2020–2022), (2022–present)

        FIELD SPECIFICATIONS:

        1. Address:
           - Location (City, State, Country) extracted or inferred from the text (e.g., "Ciudad de México, México", "Austin, TX, USA").

        2. LinkedInUrl:
           - Full or normalized LinkedIn profile URL (e.g., "https://linkedin.com/in/username").

        3. ProfessionalSummary:
           - A coherent 2-4 sentence executive summary synthesizing the person's professional title, years of experience, core domains of expertise, and highest-impact strengths based on the résumé.

        4. ProfessionalObjective:
           - A sharp 1-2 sentence statement describing their career direction, leadership value, and target professional impact based on their profile.

        5. Skills (List of strings):
           - Decompose ALL skills, technologies, frameworks, methodologies, and tools into INDIVIDUAL items.
           - Each array element = ONE distinct skill (e.g., "Azure", not "Azure, Python, Docker")
           - NEVER keep comma-separated or semicolon-separated skills in a single array element
           
           ❌ WRONG: skills: ["Azure, Python, Docker, Kubernetes"]
           ✅ CORRECT: skills: ["Azure", "Python", "Docker", "Kubernetes"]
           
           - If skill list appears as comma-separated text in source, SPLIT into individual items
           - Remove duplicates
           - Keep skill name as stated (preserve exact terminology from resume)

        6. WorkExperience (List of strings):
           MOST IMPORTANT: Each company is ONE array element. NEVER concatenate companies.
           
           - Extract EVERY distinct company/employer as its OWN SEPARATE list item.
           - Company boundaries are marked by:
             * NEW company name (proper noun) + job title + date range
             * Example boundary pattern: "Company Name — Job Title (YYYY-YYYY):" marks the START of a new entry
           - Format each company entry: "COMPANY NAME — Job Title(s) (Start-End): Full description and achievements"
           - Keep ALL content (responsibilities, achievements, clients served, impact metrics) for a company TOGETHER in one string
           - NEVER split a single company's description across multiple array elements
           - NEVER combine two different companies into one array element (this is the "chorizo" mistake)
           
           Examples of CORRECT separation:
           - "Microsoft — Cloud Solution Architect (2022-2026): Advised enterprise clients... led migration of 8000+ cores..."
           - "Flatbit Inc. — Data Science Architect (2020-2022): Managed Azure big data platform... coordinating teams..."
           - "North Highland — Master Practitioner (2017-2020): Developed enterprise Azure AI framework..."
           
           - If a company has multiple distinct positions, create separate entries:
             * "Company X — Role A (2020-2021): Responsibilities A"
             * "Company X — Role B (2021-2023): Responsibilities B"
           
           - Infer company boundaries even in dense text: when you see a proper noun name followed by a dash/em-dash and a job title, that's a NEW company

        7. Education (List of strings):
           - Extract EVERY degree, university program, diploma, bootcamp, or major academic study as its OWN SEPARATE entry in the list.
           - Format: "INSTITUTION NAME — Degree / Major / Program (Dates)"
             Example:
             - "Universidad Nacional Autónoma de México (UNAM) — Licenciatura en Ingeniería Electrónica (2014–2018)"
             - "Tecnológico de Monterrey — Maestría en Inteligencia Artificial (2019–2021)"
           - Each institution and degree/program combination is ONE entry, even if there are multiple degrees from the same institution (e.g., "University A — Bachelor in CS" and "University A — Master in AI" are TWO separate list items).
           - If a person attended the same institution for multiple degrees, separate them into distinct items.

        8. Certifications (List of strings):
           - Extract each certification, credential, license, or award as its OWN SEPARATE entry in the list (e.g., "Microsoft Certified: Azure Solutions Architect Expert", "AWS Certified Machine Learning - Specialty").

        9. Languages (List of strings):
           - Extract each spoken/written language with proficiency level if indicated (e.g., "Español (Nativo)", "Inglés (Avanzado / C1)").

          10. StudentAcademicProfile (object):
              - Extract this object when the résumé contains academic history, a transcript, cardex, semesters, subjects, grades, academic average, or distinctions.
              - Institution = the explicitly named university; Degree = the explicitly named program; GraduationYear = the expected or completed year when present; AcademicStage = the stated status such as "Último semestre".
              - CumulativeAverage and AverageScale must preserve the stated numeric average and scale. Distinctions must contain each explicitly stated academic distinction as a separate item.
              - Semesters must contain EVERY semester in the cardex. Each semester must preserve its name, stated average (including a partial/current average), and EVERY subject with its numeric grade.
              - Do not calculate averages, infer missing subjects, or invent semesters. If no academic transcript/cardex is explicitly present, return null or an empty object.

        GENERAL RULES:
        - ABSOLUTE REQUIREMENT: NO CONCATENATED ENTRIES IN ANY ARRAY
          * workExperience[] must have separate items per company (NEVER "Company1 ... Company2 ... Company3" in one string)
          * skills[] must have separate items per skill (NEVER "Python, Java, C#" in one string)
          * education[] must have separate items per degree/institution
          * certifications[] must have separate items per cert
          * languages[] must have separate items per language
        
        - Language fidelity: Preserve source resume language (Spanish input → Spanish output)
        - If field is absent in text (no LinkedIn, no certifications), return null or empty list
        - Normalize dates to "YYYY-YYYY" or "YYYY" format when possible
        - For dense, flowing resume text: split on company name + date patterns to create separate work experience entries
        """;

    private readonly AIAgent? _agent;

    public ResumeExtractionAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        // Economy tier: this is bounded field extraction from an already-provided
        // text, not open-ended reasoning — same rationale as
        // JobDescriptionExtractionAgent. Falls back to the main deployment if
        // 'AzureOpenAIEconomyDeploymentName' isn't set.
        var deploymentName = configuration["AzureOpenAIEconomyDeploymentName"] ?? configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        DeploymentName = deploymentName;

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
            .AsAIAgent(instructions: Instructions, name: "ResumeExtractionAgent");
    }

    public bool IsConfigured => _agent is not null;

    /// <summary>The deployment/model used for extraction (e.g.
    /// "gpt-5-mini"), recorded on
    /// <see cref="HumanOS.Models.People.PersonResume.ExtractionModel"/> for
    /// audit purposes.</summary>
    public string? DeploymentName { get; }

    public async Task<ResumeExtractionResult> ExtractAsync(
        string resumeText,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Resume extraction agent is not configured. Set the 'AzureOpenAIEndpoint' " +
                "and 'AzureOpenAIDeploymentName' application settings once credentials are provided.");
        }

        var prompt = "Extract the structured résumé/CV fields from the following text:\n\n" + resumeText;

        var response = await _agent.RunAsync<ResumeExtractionProposal>(prompt, cancellationToken: cancellationToken);

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "ResumeExtractionAgent",
            ModelName = DeploymentName!,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0),
        };

        return new ResumeExtractionResult { Proposal = response.Result, TokenUsage = tokenUsage };
    }
}
