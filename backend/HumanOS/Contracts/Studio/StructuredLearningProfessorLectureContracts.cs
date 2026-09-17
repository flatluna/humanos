using HumanOS.Contracts.EmployeeScoring;

namespace HumanOS.Contracts.Studio;

/// <summary>
/// Full context provided to <see cref="HumanOS.Agents.Studio.StructuredLearningProfessorLectureAgent"/>
/// to produce a personalized, spoken-word masterclass lecture for a single Engram (section).
/// Combines authoritative course content with the student's complete professional profile.
/// </summary>
public sealed class StructuredLearningProfessorLectureContext
{
    // --- Engram / Course Context ---
    public Guid CourseId { get; set; }

    public string CourseTitle { get; set; } = string.Empty;

    public Guid SectionId { get; set; }

    public string ChapterTitle { get; set; } = string.Empty;

    public string SubchapterTitle { get; set; } = string.Empty;

    public string Capability { get; set; } = string.Empty;

    /// <summary>
    /// The authoritative teaching/lesson content of the engram that the professor must teach.
    /// </summary>
    public string TeachingContent { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the whiteboard / diagram / illustration present in this engram,
    /// enabling the professor to verbally reference visual elements naturally during the lecture.
    /// </summary>
    public string? IllustrationDescription { get; set; }

    public string? DomainOrTopic { get; set; }

    // --- Student / Learner Context ---
    public Guid? StudentId { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public string RoleTitle { get; set; } = string.Empty;

    public string RoleMission { get; set; } = string.Empty;

    public int? YearsOfExperience { get; set; }

    /// <summary>
    /// Professional summary / bio from the employee's CV or profile.
    /// </summary>
    public string ResumeSummary { get; set; } = string.Empty;

    /// <summary>
    /// Work experience entries from the employee's CV.
    /// </summary>
    public List<WorkExperienceInfo> WorkExperience { get; set; } = [];

    /// <summary>
    /// Education entries from the employee's CV.
    /// </summary>
    public List<EducationInfo> Education { get; set; } = [];

    /// <summary>
    /// Certifications held by the employee (from CV).
    /// </summary>
    public List<string> Certifications { get; set; } = [];

    /// <summary>
    /// Skills claimed on the employee's CV.
    /// </summary>
    public List<string> ResumeSkills { get; set; } = [];

    /// <summary>
    /// Technical skills demanded by the employee's role/job description.
    /// </summary>
    public List<string> TechnicalSkills { get; set; } = [];

    /// <summary>
    /// Soft skills associated with the role.
    /// </summary>
    public List<string> SoftSkills { get; set; } = [];

    /// <summary>
    /// Active tools and technologies the student uses in their daily work.
    /// </summary>
    public List<string> ToolsAndTechnologies { get; set; } = [];

    /// <summary>
    /// All active SMART objectives for this employee, including target metrics, quarters, and business value.
    /// </summary>
    public List<ObjectiveInfo> ActiveObjectives { get; set; } = [];

    /// <summary>
    /// Measured performance KPIs for the role.
    /// </summary>
    public List<string> Kpis { get; set; } = [];

    /// <summary>
    /// Manager coaching notes and competency growth focus areas.
    /// </summary>
    public List<CoachingInfo> CoachingNotes { get; set; } = [];

    /// <summary>
    /// Strategic priority tier assigned to this specific engram for this employee (e.g. Critical, Important, Refresher, Base).
    /// </summary>
    public string? EngramImpactTier { get; set; }

    /// <summary>
    /// Personalization rationale explaining why this engram matters for the employee's career progression or goals.
    /// </summary>
    public string? EngramPersonalizationRationale { get; set; }

    /// <summary>
    /// The student's initial prediction/intuition from Step 1 (Hypothesis), if already answered.
    /// </summary>
    public string? StudentInitialHypothesis { get; set; }

    // --- Pedagogical & Delivery Options ---
    /// <summary>
    /// Target language code ("en" or "es"). Defaults to "en" if not specified.
    /// </summary>
    public string TargetLanguage { get; set; } = "en";
}

/// <summary>
/// The structured output produced by <see cref="HumanOS.Agents.Studio.StructuredLearningProfessorLectureAgent"/>.
/// Contains the complete verbatim spoken class ready for Text-to-Speech (TTS) audio synthesis.
/// </summary>
public sealed class StructuredLearningProfessorLectureResult
{
    /// <summary>
    /// The full spoken lecture delivered by the professor. Pure spoken prose without markdown headers,
    /// bullet asterisks, or visual markup — written explicitly for speech synthesis (TTS).
    /// </summary>
    public string SpokenLectureScript { get; set; } = string.Empty;

    /// <summary>
    /// A short 1-2 sentence hook highlighting the personal relevance of this class for the student.
    /// </summary>
    public string PersonalizedHook { get; set; } = string.Empty;

    /// <summary>
    /// List of key academic and conceptual points from the engram taught during this lecture.
    /// </summary>
    public List<string> KeyConceptsTaught { get; set; } = [];

    /// <summary>
    /// The specific connections made between the engram's principles and the student's background
    /// (role, objectives, experience, or tools).
    /// </summary>
    public List<string> PersonalizedConnections { get; set; } = [];

    /// <summary>
    /// Estimated speaking duration in minutes at a deliberate professorial pace (~140 words per minute).
    /// </summary>
    public double EstimatedDurationMinutes { get; set; }

    /// <summary>
    /// Total word count of the spoken lecture script.
    /// </summary>
    public int WordCount { get; set; }

    /// <summary>
    /// Language code of the spoken lecture ("en" or "es").
    /// </summary>
    public string LanguageCode { get; set; } = "en";
}

/// <summary>
/// Request DTO for the professor lecture generation endpoint.
/// </summary>
public sealed class GenerateProfessorLectureApiRequest
{
    /// <summary>
    /// Optional explicit Person ID. If omitted, resolved from the caller's auth identity headers.
    /// </summary>
    public Guid? PersonId { get; set; }

    /// <summary>
    /// Optional explicit language override ("en" or "es"). If omitted, auto-detected from content.
    /// </summary>
    public string? TargetLanguage { get; set; }
}

/// <summary>
/// Response DTO for the professor lecture generation endpoint.
/// </summary>
public sealed class GenerateProfessorLectureApiResponse
{
    public Guid SectionId { get; set; }

    public string ChapterTitle { get; set; } = string.Empty;

    public string SubchapterTitle { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public StructuredLearningProfessorLectureResult Lecture { get; set; } = new();
}
