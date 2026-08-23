using System.Text;
using System.Text.Json;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Runtime — real-time Assessment engine for Structured Learning sections
/// (2026-08-10). Mirrors <see cref="AdaptiveAssessmentEngine"/> 1:1
/// (deterministic orchestration in code; the LLM
/// (<see cref="StructuredLearningAssessmentAgent"/>) only proposes
/// question text/OpenEnded grading — this engine ALWAYS decides
/// Correctness thresholds, FinalScore average, Passed cutoff, round
/// chaining). See that class's doc comment for the shared "LLM proposes,
/// code decides" rule — replaces the OLD design where
/// StructuredLearningSectionDesignerAgent baked a fixed set of questions
/// once at generation time.
///
/// Simpler than AdaptiveAssessmentEngine: no per-question cognitive-type
/// rotation (Structured Learning's AssessmentQuestionType is a document-
/// wide MultipleChoice/OpenEnded MODE, not a per-question label), and
/// MultipleChoice grading is fully deterministic in code (never sent to
/// the LLM, since the correct option is already known).
///
/// Keyed directly by StructuredLearningDocumentSectionId — see
/// <see cref="StructuredLearningAssessmentRound"/>'s doc comment for why
/// (no per-student session concept exists yet for Structured Learning).
/// </summary>
public sealed class StructuredLearningAssessmentEngine
{
    private const int DefaultQuestionsPerRound = 2;
    private const int MinQuestionsPerRound = 1;
    private const int MaxQuestionsPerRound = 10;
    private const int PassThreshold = 80;

    private readonly StructuredLearningAssessmentAgent _agent;

    public StructuredLearningAssessmentEngine(StructuredLearningAssessmentAgent agent)
    {
        _agent = agent;
    }

    public sealed class QuestionInfo
    {
        public Guid StructuredLearningAssessmentQuestionAttemptId { get; set; }
        public int QuestionIndex { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
    }

    public sealed class RoundState
    {
        public Guid StructuredLearningAssessmentRoundId { get; set; }
        public int RoundNumber { get; set; }
        public StructuredLearningAssessmentRoundStatus Status { get; set; }
        public int? FinalScore { get; set; }
        public int TotalQuestions { get; set; } = DefaultQuestionsPerRound;

        /// <summary>Set only once the round is Passed/Failed — lets callers
        /// (e.g. the Brain page's forgetting-curve ring) know exactly when
        /// this section was last mastered, same field Capability Graph's own
        /// LastCompletedDate already provides.</summary>
        public DateTime? CompletedDate { get; set; }

        /// <summary>Null once the round is Passed/Failed (nothing left to answer).</summary>
        public QuestionInfo? CurrentQuestion { get; set; }
    }

    public sealed class AnswerGrade
    {
        public StructuredLearningAssessmentAnswerCorrectness Correctness { get; set; }
        public int Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }

    public sealed class SubmitAnswerResult
    {
        public AnswerGrade Grade { get; set; } = null!;
        public bool RoundComplete { get; set; }
        public bool? Passed { get; set; }
        public int? FinalScore { get; set; }

        /// <summary>The next question to ask — either the next question THIS round, or Q1 of an auto-started new round after a Failed round.</summary>
        public QuestionInfo? NextQuestion { get; set; }

        /// <summary>Set only when a new round was auto-started (the round just closed as Failed).</summary>
        public int? NewRoundNumber { get; set; }
        public Guid? NewStructuredLearningAssessmentRoundId { get; set; }
    }

    private sealed class SectionContext
    {
        public string ChapterTitle { get; set; } = string.Empty;
        public string SubchapterTitle { get; set; } = string.Empty;
        public string TeachingContent { get; set; } = string.Empty;
        public StructuredLearningAssessmentQuestionType QuestionType { get; set; }
        public int QuestionsPerRound { get; set; } = DefaultQuestionsPerRound;
    }

    /// <summary>Starts a brand-new round and generates its first question.
    /// Call this the first time a student reaches the Assessment step for
    /// a section (when <see cref="GetActiveRoundAsync"/> returns null).
    /// <paramref name="personId"/> is null only for capabilitystudio's
    /// author/reviewer preview — the real student runtime always passes
    /// it so each student's rounds stay separate.</summary>
    public async Task<RoundState> StartRoundAsync(HumanOsDbContext dbContext, Guid sectionId, Guid? personId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);

        var priorRoundNumbers = await dbContext.StructuredLearningAssessmentRounds
            .Where(r => r.StructuredLearningDocumentSectionId == sectionId && r.PersonId == personId)
            .Select(r => r.RoundNumber)
            .ToListAsync(cancellationToken);

        var roundNumber = priorRoundNumbers.Count == 0 ? 1 : priorRoundNumbers.Max() + 1;

        List<string> priorRoundErrors = [];
        if (roundNumber > 1)
        {
            var previousRound = await dbContext.StructuredLearningAssessmentRounds
                .Include(r => r.Questions)
                .Where(r => r.StructuredLearningDocumentSectionId == sectionId && r.PersonId == personId && r.RoundNumber == roundNumber - 1)
                .FirstOrDefaultAsync(cancellationToken);

            priorRoundErrors = previousRound?.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q.ObservedError))
                .Select(q => q.ObservedError!)
                .ToList() ?? [];
        }

        var round = new StructuredLearningAssessmentRound
        {
            StructuredLearningDocumentSectionId = sectionId,
            PersonId = personId,
            RoundNumber = roundNumber,
            Status = StructuredLearningAssessmentRoundStatus.InProgress
        };
        dbContext.StructuredLearningAssessmentRounds.Add(round);

        var question = await GenerateQuestionAsync(context, questionIndex: 1, alreadyAsked: [], errorsObservedSoFar: priorRoundErrors, cancellationToken);
        var questionRow = BuildQuestionRow(round.StructuredLearningAssessmentRoundId, 1, question);
        dbContext.StructuredLearningAssessmentQuestionAttempts.Add(questionRow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RoundState
        {
            StructuredLearningAssessmentRoundId = round.StructuredLearningAssessmentRoundId,
            RoundNumber = round.RoundNumber,
            Status = round.Status,
            TotalQuestions = context.QuestionsPerRound,
            CurrentQuestion = ToQuestionInfo(questionRow)
        };
    }

    /// <summary>Returns the MOST RECENT round for this section (so a page
    /// reload can resume mid-round, or show the last completed round's
    /// pass/fail summary), or null if no round has ever been started.
    /// Filters by <paramref name="personId"/> so each student sees only
    /// their own rounds (null for capabilitystudio's author preview).</summary>
    public async Task<RoundState?> GetActiveRoundAsync(HumanOsDbContext dbContext, Guid sectionId, Guid? personId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var round = await dbContext.StructuredLearningAssessmentRounds
            .Include(r => r.Questions)
            .Where(r => r.StructuredLearningDocumentSectionId == sectionId && r.PersonId == personId)
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null)
        {
            return null;
        }

        var context = await LoadSectionContextAsync(dbContext, sectionId, cancellationToken);

        var currentQuestion = round.Questions
            .Where(q => q.StudentAnswer is null)
            .OrderBy(q => q.QuestionIndex)
            .FirstOrDefault();

        return new RoundState
        {
            StructuredLearningAssessmentRoundId = round.StructuredLearningAssessmentRoundId,
            RoundNumber = round.RoundNumber,
            Status = round.Status,
            FinalScore = round.FinalScore,
            TotalQuestions = context.QuestionsPerRound,
            CompletedDate = round.CompletedDate,
            CurrentQuestion = currentQuestion is null ? null : ToQuestionInfo(currentQuestion)
        };
    }

    /// <summary>
    /// Grades the student's answer to one question, then either generates
    /// the next question THIS round, or — if this was the last question —
    /// closes out the round (Passed/Failed) and, if Failed, immediately
    /// starts a brand-new round with fresh questions in the SAME call.
    /// </summary>
    public async Task<SubmitAnswerResult> SubmitAnswerAsync(
        HumanOsDbContext dbContext, Guid questionAttemptId, string studentAnswer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var question = await dbContext.StructuredLearningAssessmentQuestionAttempts
            .Include(q => q.Round)
            .ThenInclude(r => r.Questions)
            .FirstOrDefaultAsync(q => q.StructuredLearningAssessmentQuestionAttemptId == questionAttemptId, cancellationToken);

        if (question is null)
        {
            throw new InvalidOperationException($"StructuredLearningAssessmentQuestionAttempt {questionAttemptId} not found.");
        }

        if (question.StudentAnswer is not null)
        {
            throw new InvalidOperationException($"StructuredLearningAssessmentQuestionAttempt {questionAttemptId} has already been answered.");
        }

        var round = question.Round;
        var context = await LoadSectionContextAsync(dbContext, round.StructuredLearningDocumentSectionId, cancellationToken);

        int score;
        string feedback;
        string? observedError;

        if (context.QuestionType == StructuredLearningAssessmentQuestionType.MultipleChoice)
        {
            // Deterministic — the correct option is already known, no LLM call needed.
            var options = string.IsNullOrWhiteSpace(question.OptionsJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(question.OptionsJson) ?? [];
            var isCorrect = int.TryParse(studentAnswer, out var selectedIndex) && selectedIndex == question.CorrectOptionIndex;
            score = isCorrect ? 100 : 0;
            var correctOptionText = question.CorrectOptionIndex is >= 0 && question.CorrectOptionIndex < options.Count
                ? options[question.CorrectOptionIndex.Value]
                : null;
            feedback = isCorrect
                ? "¡Correcto!"
                : correctOptionText is null ? "Esa no era la respuesta correcta." : $"Esa no era la respuesta correcta — era: \"{correctOptionText}\".";
            observedError = null;
        }
        else
        {
            var alreadyAnsweredThisRound = round.Questions
                .Where(q => q.StructuredLearningAssessmentQuestionAttemptId != question.StructuredLearningAssessmentQuestionAttemptId && q.StudentAnswer is not null)
                .OrderBy(q => q.QuestionIndex)
                .ToList();

            var gradingPrompt = BuildGradingPrompt(context, question, studentAnswer, alreadyAnsweredThisRound);
            var graded = await _agent.GradeAnswerAsync(gradingPrompt, cancellationToken);
            score = Math.Clamp(graded.Score, 0, 100);
            feedback = graded.Feedback;
            observedError = string.IsNullOrWhiteSpace(graded.ObservedError) ? null : graded.ObservedError;
        }

        var correctness = ToCorrectness(score);
        var now = DateTime.UtcNow;
        question.StudentAnswer = studentAnswer;
        question.ScoreContribution = score;
        question.Correctness = correctness;
        question.Feedback = feedback;
        question.ObservedError = observedError;
        question.AnsweredDate = now;

        var result = new SubmitAnswerResult
        {
            Grade = new AnswerGrade { Correctness = correctness, Score = score, Feedback = feedback }
        };

        var allAnsweredSoFar = round.Questions
            .Where(q => q.StudentAnswer is not null)
            .OrderBy(q => q.QuestionIndex)
            .ToList();

        if (question.QuestionIndex < context.QuestionsPerRound)
        {
            var errorsSoFar = allAnsweredSoFar
                .Where(q => !string.IsNullOrWhiteSpace(q.ObservedError))
                .Select(q => q.ObservedError!)
                .ToList();
            var alreadyAsked = round.Questions.OrderBy(q => q.QuestionIndex).Select(q => q.QuestionText).ToList();

            var nextQuestion = await GenerateQuestionAsync(context, question.QuestionIndex + 1, alreadyAsked, errorsSoFar, cancellationToken);
            var nextQuestionRow = BuildQuestionRow(round.StructuredLearningAssessmentRoundId, question.QuestionIndex + 1, nextQuestion);
            dbContext.StructuredLearningAssessmentQuestionAttempts.Add(nextQuestionRow);

            result.RoundComplete = false;
            result.NextQuestion = ToQuestionInfo(nextQuestionRow);

            await dbContext.SaveChangesAsync(cancellationToken);
            return result;
        }

        // This was the last question — close out the round.
        var allScores = allAnsweredSoFar.Select(q => q.ScoreContribution!.Value).ToList();
        var finalScore = (int)Math.Round(allScores.Average());
        var passed = finalScore >= PassThreshold;

        round.Status = passed ? StructuredLearningAssessmentRoundStatus.Passed : StructuredLearningAssessmentRoundStatus.Failed;
        round.FinalScore = finalScore;
        round.CompletedDate = now;

        result.RoundComplete = true;
        result.Passed = passed;
        result.FinalScore = finalScore;

        if (passed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return result;
        }

        // Failed — auto-start a brand-new round immediately, same call.
        var newRoundNumber = round.RoundNumber + 1;
        var newRound = new StructuredLearningAssessmentRound
        {
            StructuredLearningDocumentSectionId = round.StructuredLearningDocumentSectionId,
            PersonId = round.PersonId,
            RoundNumber = newRoundNumber,
            Status = StructuredLearningAssessmentRoundStatus.InProgress
        };
        dbContext.StructuredLearningAssessmentRounds.Add(newRound);

        var failedRoundErrors = allAnsweredSoFar
            .Where(q => !string.IsNullOrWhiteSpace(q.ObservedError))
            .Select(q => q.ObservedError!)
            .ToList();

        var firstQuestionOfNewRound = await GenerateQuestionAsync(context, questionIndex: 1, alreadyAsked: [], errorsObservedSoFar: failedRoundErrors, cancellationToken);
        var newQuestionRow = BuildQuestionRow(newRound.StructuredLearningAssessmentRoundId, 1, firstQuestionOfNewRound);
        dbContext.StructuredLearningAssessmentQuestionAttempts.Add(newQuestionRow);

        result.NewRoundNumber = newRoundNumber;
        result.NewStructuredLearningAssessmentRoundId = newRound.StructuredLearningAssessmentRoundId;
        result.NextQuestion = ToQuestionInfo(newQuestionRow);

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static StructuredLearningAssessmentAnswerCorrectness ToCorrectness(int score) => score switch
    {
        >= 80 => StructuredLearningAssessmentAnswerCorrectness.Correct,
        >= 40 => StructuredLearningAssessmentAnswerCorrectness.PartiallyCorrect,
        _ => StructuredLearningAssessmentAnswerCorrectness.Incorrect
    };

    private static QuestionInfo ToQuestionInfo(StructuredLearningAssessmentQuestionAttempt row) => new()
    {
        StructuredLearningAssessmentQuestionAttemptId = row.StructuredLearningAssessmentQuestionAttemptId,
        QuestionIndex = row.QuestionIndex,
        QuestionText = row.QuestionText,
        Options = string.IsNullOrWhiteSpace(row.OptionsJson) ? null : JsonSerializer.Deserialize<List<string>>(row.OptionsJson)
    };

    private static StructuredLearningAssessmentQuestionAttempt BuildQuestionRow(
        Guid roundId, int questionIndex, (string Text, List<string>? Options, int? CorrectOptionIndex) question) => new()
    {
        StructuredLearningAssessmentRoundId = roundId,
        QuestionIndex = questionIndex,
        QuestionText = question.Text,
        OptionsJson = question.Options is { Count: > 0 } ? JsonSerializer.Serialize(question.Options) : null,
        CorrectOptionIndex = question.CorrectOptionIndex
    };

    private async Task<(string Text, List<string>? Options, int? CorrectOptionIndex)> GenerateQuestionAsync(
        SectionContext context, int questionIndex, IReadOnlyList<string> alreadyAsked, IReadOnlyList<string> errorsObservedSoFar, CancellationToken cancellationToken)
    {
        var prompt = BuildQuestionGenerationPrompt(context, questionIndex, alreadyAsked, errorsObservedSoFar);
        var response = await _agent.GenerateQuestionAsync(prompt, cancellationToken);

        if (context.QuestionType == StructuredLearningAssessmentQuestionType.MultipleChoice
            && response.Options is { Count: >= 2 } && response.CorrectOptionIndex is not null)
        {
            return (response.QuestionText, response.Options, response.CorrectOptionIndex);
        }

        return (response.QuestionText, null, null);
    }

    private static string BuildQuestionGenerationPrompt(
        SectionContext context, int questionIndex, IReadOnlyList<string> alreadyAsked, IReadOnlyList<string> errorsObservedSoFar)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TASK: Generate a new assessment question (do NOT grade anything).");
        builder.AppendLine("RULE: This is a POST-TEACHING assessment. The student has already seen the teaching for this section.");
        builder.AppendLine("RULE: do not ask literal recall or text-search questions. Never use direct source-reference wording or sentence-lookup phrasing.");
        builder.AppendLine("RULE: The question must test understanding/application after teaching, not read-back comprehension or source-text lookup.");
        builder.AppendLine($"Format required: {context.QuestionType}");
        builder.AppendLine($"Question number: {questionIndex} of {context.QuestionsPerRound}");
        builder.AppendLine();
        builder.AppendLine($"Chapter: {context.ChapterTitle}");
        builder.AppendLine($"Section: {context.SubchapterTitle}");
        builder.AppendLine("SECTION CONTENT:");
        builder.AppendLine(context.TeachingContent);
        builder.AppendLine();

        if (context.QuestionType == StructuredLearningAssessmentQuestionType.MultipleChoice)
        {
            builder.AppendLine("MCQ REQUIREMENTS: produce questions that check understanding of the taught idea, not sentence recall. Each question must have 4 options and exactly one correct answer. Avoid answer choices that are just copied verbatim phrases from the teaching text.");
            builder.AppendLine("If this is the first question in the round, make it a clear, high-quality post-teaching check. If the student has already answered earlier questions in this round, avoid repeating the same angle and focus on a different application or implication.");
        }

        if (alreadyAsked.Count > 0)
        {
            builder.AppendLine("QUESTIONS ALREADY ASKED THIS ROUND (never repeat one of these literally or with trivial rewording):");
            foreach (var q in alreadyAsked)
            {
                builder.AppendLine($"- {q}");
            }
            builder.AppendLine();
        }

        if (errorsObservedSoFar.Count > 0)
        {
            builder.AppendLine("ERRORS/CONFUSIONS OBSERVED SO FAR (prefer revisiting one of these):");
            foreach (var e in errorsObservedSoFar)
            {
                builder.AppendLine($"- {e}");
            }
        }

        return builder.ToString();
    }

    private static string BuildGradingPrompt(
        SectionContext context, StructuredLearningAssessmentQuestionAttempt question, string studentAnswer,
        IReadOnlyList<StructuredLearningAssessmentQuestionAttempt> alreadyAnsweredThisRound)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TASK: Grade the student's answer to the question below (do NOT generate a new question).");
        builder.AppendLine();
        builder.AppendLine($"Chapter: {context.ChapterTitle}");
        builder.AppendLine($"Section: {context.SubchapterTitle}");
        builder.AppendLine("SECTION CONTENT:");
        builder.AppendLine(context.TeachingContent);
        builder.AppendLine();
        builder.AppendLine($"QUESTION: {question.QuestionText}");
        builder.AppendLine($"STUDENT ANSWER: {studentAnswer}");

        if (alreadyAnsweredThisRound.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("OTHER QUESTIONS ALREADY ANSWERED THIS ROUND (context only, do not re-grade these):");
            foreach (var q in alreadyAnsweredThisRound)
            {
                builder.AppendLine($"- {q.QuestionText} -> {q.StudentAnswer} (score {q.ScoreContribution})");
            }
        }

        return builder.ToString();
    }

    private static async Task<SectionContext> LoadSectionContextAsync(HumanOsDbContext dbContext, Guid sectionId, CancellationToken cancellationToken)
    {
        var section = await dbContext.StructuredLearningDocumentSections
            .AsNoTracking()
            .Include(s => s.Document)
            .Include(s => s.Steps)
            .FirstOrDefaultAsync(s => s.StructuredLearningDocumentSectionId == sectionId, cancellationToken)
            ?? throw new InvalidOperationException($"StructuredLearningDocumentSection {sectionId} not found.");

        var teachingStep = section.Steps.FirstOrDefault(s => s.StepType == StructuredLearningStepType.Teaching);
        var document = section.Document;

        var questionsPerRound = Math.Clamp(
            document.AssessmentQuestionsPerRound ?? DefaultQuestionsPerRound, MinQuestionsPerRound, MaxQuestionsPerRound);

        return new SectionContext
        {
            ChapterTitle = section.ChapterTitle,
            SubchapterTitle = section.SubchapterTitle,
            TeachingContent = teachingStep?.Content ?? section.Text,
            QuestionType = document.AssessmentQuestionType,
            QuestionsPerRound = questionsPerRound
        };
    }
}
