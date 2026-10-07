using System.Text;
using Azure.AI.OpenAI;
using Azure.Identity;
using HumanOS.Contracts.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents.Studio;

/// <summary>
/// Professor Lecture Agent powered by the Microsoft Agent Framework (Microsoft.Agents.AI).
/// Takes a single Engram (lesson section) and generates a generic, world-class masterclass
/// spoken lecture delivered by the professor for all students taking this course.
/// The output is pure spoken prose engineered specifically for direct Text-to-Speech (TTS) audio synthesis.
/// </summary>
public sealed class StructuredLearningProfessorLectureAgent
{
    private const string Instructions = """
        You are a distinguished, world-class University Professor and Master Pedagogue at Human OS.

        YOUR MISSION:
        You teach an individual learning Engram (lesson) to the class in a masterclass lecture.
        Your lecture is recorded once for the course curriculum and heard by ALL students.
        Therefore, your lecture MUST be universal, objective, and professional for any learner.
        NEVER include specific student names (e.g. NEVER say "Hola Alejandro", "Hello Alejandro",
        "Student", or "Learner") and NEVER reference an individual student's personal résumé,
        private background, or personal SMART objectives.

        The lecture text you produce will be converted DIRECTLY into an audio speech file via
        Text-to-Speech (TTS). Therefore, your output MUST be written strictly for the ear, not the eye.

        CRITICAL AUDIO / TTS FORMATTING RULES:
        1. PURE SPOKEN PROSE ONLY:
           - NEVER output markdown formatting of any kind: NO headers (###), NO bullet points (* or -),
             NO bold (**), NO italics (* or _), NO numbered markdown lists (1.), and NO brackets.
           - Write everything as natural, flowing, spoken paragraphs that sound conversational and eloquent
             when read aloud by an automated voice.
        2. SPOKEN CADENCE & PACING:
           - Use natural spoken transitions: "First, let us examine...", "Consider how this works in practice...",
             "Notice the distinction here...", "Furthermore...", "En primer lugar...", "Veamos cómo funciona esto en la práctica...".
           - Use commas, em-dashes, and periods thoughtfully to create natural breathing pauses for the voice synthesizer.
        3. UNIVERSAL AUDIENCE ADDRESS (NO PERSONAL NAMES):
           - Greet the audience warmly and universally:
             In Spanish: "Hola y bienvenidos a esta clase...", "Bienvenidos a esta sesión...", "En esta lección vamos a estudiar...".
             In English: "Hello and welcome to this lesson...", "Welcome to this session...", "In this lesson, we will explore...".
           - Speak directly to the listener in second person ("you" / "ustedes" / "tú") without mentioning any personal names.
        4. PEDAGOGICAL GROUNDING & TEACHING:
           - You must thoroughly and clearly teach the core concepts present in the provided Engram Teaching Content.
           - Ground every factual statement strictly in the provided engram content. Never invent facts, formulas,
             or legal claims that the source content does not support.
           - Connect the concepts to real-world industry practice, professional workflows, and practical relevance
             for people working in this domain generally.
        5. VISUAL REFERENCE (WHITEBOARD / DIAGRAM):
           - If an Illustration Description or Whiteboard note is provided, seamlessly refer to it in your spoken
             delivery as if you are standing in front of a whiteboard in a lecture hall (e.g., "Como vemos en el pizarrón...",
             "Fíjense en el diagrama en la pizarra...", "As you see on the board...", "Notice on this diagram...").
           - CRITICAL: explain the MEANING/CONTENT the image conveys, as a professor teaching a concept — NEVER
             describe rendering/typography details such as font, boldness, letter color, letter size, or layout
             mechanics (e.g. NEVER say things like "hay una letra amarilla en negritas" / "there's a bold yellow
             letter"). Those are image-generation instructions, not teaching content, and must be ignored if they
             appear in the Illustration Description. Only speak about the actual concept, object, or diagram being
             depicted.
        6. PROFESSORIAL WRAP-UP:
           - Conclude the class with an inspiring, coherent synthesis of the breakthrough idea and invite the students
             to proceed to the next step.
        7. PRESERVE NAMED ENTITIES EXACTLY (CRITICAL — knowledge graph parity):
           - The engram's knowledge-graph entities (people's roles like "Employee"/"Manager", company/organization
             names, product names, dates, job titles, and named domain concepts) are extracted from the
             AUTHORITATIVE ENGRAM TEACHING CONTENT you are given below. Your spoken lecture is later
             re-scanned to build that same graph, so it MUST mention every such named entity verbatim,
             using the EXACT same wording as the source content (e.g. if the source says "Employees learn a
             repeatable method", your lecture must still say "employees", not silently generalize it to
             "people", "teams", "practitioners", or any other paraphrase).
           - You may still rephrase the surrounding sentence for spoken flow, but never substitute, drop, or
             genericize a named entity, proper noun, company name, product name, date, or concept term that
             appears in the source content — carry it through into the lecture unchanged.
           - This rule does NOT apply to individual student names (still forbidden per rule 3) — it applies to
             entities describing roles, organizations, concepts, dates, and other domain nouns from the source.

        CRITICAL NEGATIVE CONSTRAINTS:
        - NEVER mention or refer to the technology, AI model, prompts, system versions, audio synthesis, or voice gender (e.g., NEVER say "como IA", "en esta grabación"). You are simply a live, warm professor speaking to the class.
        - NEVER mention individual names (e.g. "Alejandro", "Juan", "Student"). The course is generic for all students.

        LANGUAGE CONSISTENCY (HARD RULE):
        - If TargetLanguage is 'es' (Spanish), deliver the ENTIRE lecture in polished, natural, academic Spanish.
        - If TargetLanguage is 'en' (English), deliver the ENTIRE lecture in polished, natural, academic English.
        - Never switch languages or mix languages mid-lecture.

        OUTPUT REQUIREMENTS:
        Return the structured result containing:
        - SpokenLectureScript: The complete, spoken-word lecture ready for audio TTS synthesis.
        - PersonalizedHook: A concise 1-2 sentence statement summarizing the core learning hook of this class.
        - KeyConceptsTaught: The list of distinct academic concepts from the engram covered in the class.
        - PersonalizedConnections: Professional relevance of this lesson to practitioners in this field.
        - EstimatedDurationMinutes: Estimated speaking time in minutes (based on approximately 140 spoken words per minute).
        - WordCount: Exact word count of the SpokenLectureScript.
        - LanguageCode: 'en' or 'es'.
        """;

    private readonly AIAgent? _agent;
    private readonly string _deploymentName;
    private readonly bool _isReasoningModel;

    public StructuredLearningProfessorLectureAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

        _deploymentName = deploymentName ?? string.Empty;
        _isReasoningModel = !string.IsNullOrEmpty(deploymentName) &&
            (deploymentName.Contains("gpt-5", StringComparison.OrdinalIgnoreCase) ||
             deploymentName.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
             deploymentName.Contains("o3", StringComparison.OrdinalIgnoreCase)) &&
            !deploymentName.Contains("chat", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deploymentName))
        {
            _agent = null;
            return;
        }

        AzureOpenAIClient client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            : new AzureOpenAIClient(new Uri(endpoint), new System.ClientModel.ApiKeyCredential(apiKey));

        var defaultChatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            Instructions = Instructions
        };

        if (!_isReasoningModel)
        {
            defaultChatOptions.Temperature = 0.3f;
        }

        _agent = client
            .GetChatClient(deploymentName)
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "StructuredLearningProfessorLectureAgent",
                ChatOptions = defaultChatOptions
            });
    }

    public bool IsConfigured => _agent is not null;

    public string DeploymentName => _deploymentName;

    /// <summary>Real per-call token usage alongside the structured lecture
    /// result (2026-10-04) — previously discarded entirely, which meant this
    /// agent's LLM cost (one call per Engram, every course) never showed up
    /// anywhere on the cost dashboard/Simulations baseline. See
    /// <see cref="HumanOS.Services.StructuredLearningPedagogyService"/>'s
    /// caller for how this is now persisted via <c>StructuredLearningGenerationUsageService</c>.</summary>
    public sealed class LectureGenerationResult
    {
        public StructuredLearningProfessorLectureResult Result { get; set; } = null!;

        public AgentTokenUsage TokenUsage { get; set; } = null!;
    }

    /// <summary>
    /// Generates a spoken, personalized professor lecture for the specified Engram and student context.
    /// </summary>
    public async Task<LectureGenerationResult> GenerateLectureAsync(
        StructuredLearningProfessorLectureContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_agent is null)
        {
            throw new InvalidOperationException(
                "The Structured Learning Professor Lecture Agent is not configured. Verify 'AzureOpenAIEndpoint' and 'AzureOpenAIDeploymentName'.");
        }

        var prompt = BuildPrompt(context);

        var chatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json
        };

        if (!_isReasoningModel)
        {
            chatOptions.Temperature = 0.3f;
        }

        var response = await _agent.RunAsync<StructuredLearningProfessorLectureResult>(
            prompt,
            cancellationToken: cancellationToken);

        var result = response.Result;

        // Ensure word count and duration are accurate if model didn't calculate them
        if (result is not null && !string.IsNullOrWhiteSpace(result.SpokenLectureScript))
        {
            if (result.WordCount <= 0)
            {
                var words = result.SpokenLectureScript.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
                result.WordCount = words.Length;
            }

            if (result.EstimatedDurationMinutes <= 0 && result.WordCount > 0)
            {
                // Professional spoken pace is ~140 words per minute
                result.EstimatedDurationMinutes = Math.Round(result.WordCount / 140.0, 1);
            }

            if (string.IsNullOrWhiteSpace(result.LanguageCode))
            {
                result.LanguageCode = context.TargetLanguage;
            }
        }

        var usage = response.Usage;
        var tokenUsage = new AgentTokenUsage
        {
            AgentName = "StructuredLearningProfessorLectureAgent",
            ModelName = _deploymentName,
            InputTokens = (int)(usage?.InputTokenCount ?? 0),
            OutputTokens = (int)(usage?.OutputTokenCount ?? 0),
            CachedInputTokens = (int)(usage?.CachedInputTokenCount ?? 0)
        };

        return new LectureGenerationResult
        {
            Result = result ?? new StructuredLearningProfessorLectureResult(),
            TokenUsage = tokenUsage,
        };
    }

    public static string BuildPrompt(StructuredLearningProfessorLectureContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== COURSE & ENGRAM LESSON TO TEACH ===");
        if (!string.IsNullOrWhiteSpace(context.CourseTitle))
        {
            sb.AppendLine($"- Course / Document: {context.CourseTitle.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(context.ChapterTitle))
        {
            sb.AppendLine($"- Chapter: {context.ChapterTitle.Trim()}");
        }
        sb.AppendLine($"- Engram (Subchapter) Title: {context.SubchapterTitle.Trim()}");
        if (!string.IsNullOrWhiteSpace(context.Capability))
        {
            sb.AppendLine($"- Target Capability: {context.Capability.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(context.DomainOrTopic))
        {
            sb.AppendLine($"- Domain / Topic: {context.DomainOrTopic.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(context.IllustrationDescription))
        {
            sb.AppendLine($"- Visual Illustration / Whiteboard Elements: {context.IllustrationDescription.Trim()}");
        }

        sb.AppendLine();
        sb.AppendLine("--- AUTHORITATIVE ENGRAM TEACHING CONTENT (YOUR SOURCE OF TRUTH) ---");
        sb.AppendLine(context.TeachingContent.Trim());
        sb.AppendLine("-------------------------------------------------------------------");
        sb.AppendLine();

        sb.AppendLine("===========================================");
        sb.AppendLine();
        sb.AppendLine($"TARGET DELIVERY LANGUAGE: {(context.TargetLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "Spanish (es)" : "English (en)")}");
        sb.AppendLine("Deliver the professor's spoken class now. MANDATORY RULES:");
        sb.AppendLine("1. UNIVERSAL AUDIENCE ADDRESS: Greet the class warmly and universally (e.g., 'Hola y bienvenidos a esta clase...' or 'Hello and welcome to this lecture...'). NEVER mention any individual student's name (NO 'Alejandro', NO 'Student', NO 'Learner').");
        sb.AppendLine("2. Ground all teaching strictly in the engram teaching content and capability statement. Connect concepts to general real-world industry practice and practical relevance in this field.");
        sb.AppendLine("3. PURE SPOKEN PROSE FOR TEXT-TO-SPEECH (TTS): no markdown formatting of any kind, no bullet points, no numbered lists, no headers, spoken cadence with breathing pauses, complete academic teaching of the source content, and a warm professorial closing.");
        sb.AppendLine("4. PRESERVE NAMED ENTITIES EXACTLY: keep every role name (e.g. 'employees'), company/organization name, product name, date, and named domain concept from the Authoritative Engram Teaching Content verbatim in your lecture — never paraphrase, generalize, or drop them, so the knowledge graph extracted from your lecture matches the source content.");

        return sb.ToString();
    }
}
