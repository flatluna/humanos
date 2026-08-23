using System.Text.Json;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Persists a student's answers to a Structured Learning section's
/// Hypothesis pre-check questions (2026-08-13) — replaces the OLD
/// frontend-only design where "Save responses" just flipped local React
/// state and stashed answers in localStorage, never reaching the backend.
/// Fully deterministic (no LLM call): each question already carries its
/// own CorrectIndex from generation time (StructuredLearningSectionStep
/// .StepDataJson), so grading is a simple equality check, same as
/// MultipleChoice grading in <see cref="StructuredLearningAssessmentEngine"/>.
/// </summary>
public sealed class StructuredLearningHypothesisService
{
    private sealed class HypothesisQuestionData
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = [];
        public int? CorrectIndex { get; set; }
    }

    public sealed class HypothesisAnswerInput
    {
        public int QuestionIndex { get; set; }
        public int SelectedOptionIndex { get; set; }
    }

    public sealed class HypothesisResponseResult
    {
        public int QuestionIndex { get; set; }
        public int SelectedOptionIndex { get; set; }
        public bool? IsCorrect { get; set; }
    }

    /// <summary>Replaces any prior answers this student gave for this
    /// section's Hypothesis step with the newly submitted set (a student
    /// re-submitting after changing their mind overwrites, never appends
    /// duplicates).</summary>
    public async Task<List<HypothesisResponseResult>> SubmitAsync(
        HumanOsDbContext dbContext, Guid sectionId, Guid? personId, List<HypothesisAnswerInput> answers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(answers);

        var step = await dbContext.StructuredLearningSectionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId && s.StepType == StructuredLearningStepType.Hypothesis, cancellationToken)
            ?? throw new InvalidOperationException($"Section {sectionId} has no Hypothesis step.");

        var questions = ParseQuestions(step.StepDataJson);

        var priorResponses = await dbContext.StructuredLearningHypothesisResponses
            .Where(r => r.StructuredLearningDocumentSectionId == sectionId && r.PersonId == personId)
            .ToListAsync(cancellationToken);
        dbContext.StructuredLearningHypothesisResponses.RemoveRange(priorResponses);

        var results = new List<HypothesisResponseResult>();
        foreach (var answer in answers)
        {
            bool? isCorrect = null;
            if (answer.QuestionIndex >= 0 && answer.QuestionIndex < questions.Count)
            {
                var correctIndex = questions[answer.QuestionIndex].CorrectIndex;
                if (correctIndex.HasValue)
                {
                    isCorrect = correctIndex.Value == answer.SelectedOptionIndex;
                }
            }

            dbContext.StructuredLearningHypothesisResponses.Add(new StructuredLearningHypothesisResponse
            {
                StructuredLearningDocumentSectionId = sectionId,
                PersonId = personId,
                QuestionIndex = answer.QuestionIndex,
                SelectedOptionIndex = answer.SelectedOptionIndex,
                IsCorrect = isCorrect
            });

            results.Add(new HypothesisResponseResult
            {
                QuestionIndex = answer.QuestionIndex,
                SelectedOptionIndex = answer.SelectedOptionIndex,
                IsCorrect = isCorrect
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return results.OrderBy(r => r.QuestionIndex).ToList();
    }

    /// <summary>Returns this student's previously saved answers (empty if
    /// none yet) — lets a page reload resume showing what was already
    /// answered instead of losing it.</summary>
    public async Task<List<HypothesisResponseResult>> GetAsync(
        HumanOsDbContext dbContext, Guid sectionId, Guid? personId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        return await dbContext.StructuredLearningHypothesisResponses
            .Where(r => r.StructuredLearningDocumentSectionId == sectionId && r.PersonId == personId)
            .OrderBy(r => r.QuestionIndex)
            .Select(r => new HypothesisResponseResult
            {
                QuestionIndex = r.QuestionIndex,
                SelectedOptionIndex = r.SelectedOptionIndex,
                IsCorrect = r.IsCorrect
            })
            .ToListAsync(cancellationToken);
    }

    private static List<HypothesisQuestionData> ParseQuestions(string? stepDataJson)
    {
        if (string.IsNullOrWhiteSpace(stepDataJson)) return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<HypothesisQuestionData>>(
                stepDataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return parsed ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
