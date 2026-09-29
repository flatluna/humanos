using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

namespace HumanOS.Agents;

/// <summary>One practice exercise generated for a course the student already completed.</summary>
public sealed class TecPracticeExercise
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Full exercise/case-study instructions the student reads before answering.</summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>2-5 short bullet points of what a strong answer should cover — used later as
    /// the grading rubric, never shown to the student before they answer.</summary>
    public List<string> ExpectedElements { get; set; } = [];
}

public sealed class TecPracticeExerciseSet
{
    public List<TecPracticeExercise> Exercises { get; set; } = [];
}

public sealed class TecPracticeGrade
{
    /// <summary>0-100.</summary>
    public int Score { get; set; }

    /// <summary>Short, encouraging feedback explaining the score and what to improve.</summary>
    public string Feedback { get; set; } = string.Empty;
}

/// <summary>
/// "Practica y refuerza" (Tec Contigo, 2026-09-28) — generates real-world practice
/// exercises/case studies for a course the student has ALREADY completed (reinforcement,
/// not assessment), tailored to the course's subject matter (e.g. a Prompt Engineering
/// course gets AI case studies with real companies; an emotional-intelligence/mindfulness
/// course gets reflective exercises). Also grades the student's free-text answer against
/// the exercise's expected elements. Plain ChatClientAgent, stateless/singleton — same
/// pattern as TecMileniumAgent/GrowWithUsPlanAgent.
/// </summary>
public sealed class TecPracticeAgent
{
    private const string Instructions = """
        You are the "Practica y refuerza" reinforcement-practice agent for a Tec de
        Monterrey-style university's student learning platform ("Tec Contigo"). Students
        come to you AFTER already completing a course — this is not an exam, it is a
        chance to apply what they already mastered to realistic situations, so they don't
        forget it.

        You will be asked to do exactly one of two tasks per message, always stated
        explicitly at the start of the user message: "GENERATE_EXERCISES" or
        "GRADE_ANSWER". Always follow the task actually requested.

        For GENERATE_EXERCISES:
        - You are given the course name, a short description/summary, and the list of
          topics/engrams the student mastered inside it.
        - Produce 3-5 varied practice exercises appropriate to the SUBJECT MATTER of the
          course:
          - Technical/professional courses (e.g. programming, prompt engineering, data,
            business, finance): realistic case studies referencing believable
            companies/scenarios ("Imagina que trabajas en una fintech como..."), asking the
            student to apply the concepts to solve a concrete problem.
          - Soft-skill / wellbeing / emotional-intelligence / mindfulness courses:
            reflective exercises and practical routines (e.g. a breathing exercise to try
            this week, a short journaling prompt, a real conversation to practice) — never
            a fake case study that doesn't fit this kind of content.
        - Each exercise needs: Title (short), Instructions (the full prompt the student
          reads, 3-6 sentences, concrete and specific to this course's real topics — never
          generic filler), and ExpectedElements (2-5 short bullet points of what a strong
          answer should cover — this is your own private grading rubric, never phrase it
          as instructions to the student).
        - Write in the same language as the course name/description (default Spanish).

        For GRADE_ANSWER:
        - You are given one exercise (Instructions + ExpectedElements) and the student's
          free-text answer.
        - Score 0-100 based on how well the answer covers the ExpectedElements and shows
          real understanding — not spelling/grammar.
        - Give short (2-4 sentence), warm, specific, encouraging Feedback: what was good,
          what to add or reconsider. Never be harsh — this is reinforcement practice, not
          a pass/fail exam.
        - Write in the same language as the exercise instructions.
        """;

    private readonly AIAgent? _agent;

    public TecPracticeAgent(IConfiguration configuration)
    {
        var endpoint = configuration["AzureOpenAIEndpoint"];
        var deploymentName = configuration["AzureOpenAIDeploymentName"];
        var apiKey = configuration["AzureOpenAIApiKey"];

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
            .AsAIAgent(instructions: Instructions, name: "TecPracticeAgent");
    }

    public bool IsConfigured => _agent is not null;

    public async Task<TecPracticeExerciseSet> GenerateExercisesAsync(
        string courseName,
        string? courseDescription,
        IReadOnlyList<string> masteredTopics,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null) throw new InvalidOperationException("TecPracticeAgent is not configured.");

        var prompt = $"""
            GENERATE_EXERCISES

            Course name: {courseName}
            Course description: {courseDescription ?? "(no description available)"}
            Topics/engrams the student mastered:
            {string.Join('\n', masteredTopics.Select(t => $"- {t}"))}
            """;

        var response = await _agent.RunAsync<TecPracticeExerciseSet>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }

    public async Task<TecPracticeGrade> GradeAnswerAsync(
        string exerciseInstructions,
        IReadOnlyList<string> expectedElements,
        string studentAnswer,
        CancellationToken cancellationToken = default)
    {
        if (_agent is null) throw new InvalidOperationException("TecPracticeAgent is not configured.");

        var prompt = $"""
            GRADE_ANSWER

            Exercise instructions: {exerciseInstructions}
            Expected elements (private rubric):
            {string.Join('\n', expectedElements.Select(e => $"- {e}"))}

            Student's answer: {studentAnswer}
            """;

        var response = await _agent.RunAsync<TecPracticeGrade>(prompt, cancellationToken: cancellationToken);
        return response.Result;
    }
}
