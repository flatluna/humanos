using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Data;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

public sealed class PracticeExerciseView
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string? StudentAnswer { get; set; }
    public int? Score { get; set; }
    public string? Feedback { get; set; }
}

public sealed class PracticeSessionView
{
    public Guid PracticeSessionId { get; set; }
    public Guid CapabilityId { get; set; }
    public string CapabilityName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public List<PracticeExerciseView> Exercises { get; set; } = [];
}

/// <summary>
/// "Practica y refuerza" (2026-09-28) — generates (on demand, once per person+course) and
/// grades AI reinforcement practice for a course the student already completed. See
/// PracticeSession/PracticeAttempt doc comments. Awards points via GamificationOrchestrator
/// on each graded answer (idempotent per exercise, same ledger as engram mastery).
/// </summary>
public sealed class TecPracticeService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HumanOsDbContext _dbContext;
    private readonly TecPracticeAgent _agent;
    private readonly GamificationOrchestrator _gamificationOrchestrator;

    public TecPracticeService(HumanOsDbContext dbContext, TecPracticeAgent agent, GamificationOrchestrator gamificationOrchestrator)
    {
        _dbContext = dbContext;
        _agent = agent;
        _gamificationOrchestrator = gamificationOrchestrator;
    }

    public bool IsConfigured => _agent.IsConfigured;

    /// <summary>Returns the cached session for this (person, capability) if one already
    /// exists — never regenerates. Null when nothing has been generated yet.</summary>
    public async Task<PracticeSessionView?> GetExistingAsync(Guid personId, Guid capabilityId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.PracticeSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PersonId == personId && s.CapabilityId == capabilityId, cancellationToken);

        return session is null ? null : await ToViewAsync(session, cancellationToken);
    }

    /// <summary>Generates (via AI) a brand-new set of exercises for this course and caches
    /// it — call only when GetExistingAsync returned null, or the student explicitly asks
    /// to regenerate.</summary>
    public async Task<PracticeSessionView> GenerateAsync(
        Guid personId,
        Guid capabilityId,
        string capabilityName,
        string? capabilityDescription,
        IReadOnlyList<string> masteredTopics,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PracticeSessions
            .FirstOrDefaultAsync(s => s.PersonId == personId && s.CapabilityId == capabilityId, cancellationToken);

        var exerciseSet = await _agent.GenerateExercisesAsync(capabilityName, capabilityDescription, masteredTopics, cancellationToken);
        var exercisesJson = JsonSerializer.Serialize(exerciseSet.Exercises);

        if (existing is null)
        {
            existing = new PracticeSession
            {
                PracticeSessionId = Guid.NewGuid(),
                PersonId = personId,
                CapabilityId = capabilityId,
                CapabilityName = capabilityName,
                ExercisesJson = exercisesJson,
                CreatedDate = DateTime.UtcNow,
            };
            _dbContext.PracticeSessions.Add(existing);
        }
        else
        {
            existing.ExercisesJson = exercisesJson;
            existing.CreatedDate = DateTime.UtcNow;
            // Regenerating drops previously graded answers — they belonged to the old exercises.
            var oldAttempts = _dbContext.PracticeAttempts.Where(a => a.PracticeSessionId == existing.PracticeSessionId);
            _dbContext.PracticeAttempts.RemoveRange(oldAttempts);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await ToViewAsync(existing, cancellationToken);
    }

    /// <summary>Grades a student's free-text answer to one exercise, saves/upserts the
    /// attempt, and awards gamification points (idempotent — regrading the same exercise
    /// never double-awards).</summary>
    public async Task<PracticeExerciseView> SubmitAnswerAsync(
        Guid personId,
        Guid practiceSessionId,
        int exerciseIndex,
        string studentAnswer,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.PracticeSessions
            .FirstOrDefaultAsync(s => s.PracticeSessionId == practiceSessionId && s.PersonId == personId, cancellationToken)
            ?? throw new InvalidOperationException("Practice session not found.");

        var exercises = JsonSerializer.Deserialize<List<TecPracticeExercise>>(session.ExercisesJson, JsonOptions) ?? [];
        if (exerciseIndex < 0 || exerciseIndex >= exercises.Count)
        {
            throw new InvalidOperationException("Exercise index out of range.");
        }
        var exercise = exercises[exerciseIndex];

        var grade = await _agent.GradeAnswerAsync(exercise.Instructions, exercise.ExpectedElements, studentAnswer, cancellationToken);

        var attempt = await _dbContext.PracticeAttempts
            .FirstOrDefaultAsync(a => a.PracticeSessionId == practiceSessionId && a.ExerciseIndex == exerciseIndex, cancellationToken);

        var engramId = $"practice-{practiceSessionId}-{exerciseIndex}";
        var reward = await _gamificationOrchestrator.ProcessEngramMasteryAsync(
            personId, session.CapabilityId, engramId, sourceType: "Practice", ct: cancellationToken);

        if (attempt is null)
        {
            attempt = new PracticeAttempt
            {
                PracticeAttemptId = Guid.NewGuid(),
                PracticeSessionId = practiceSessionId,
                ExerciseIndex = exerciseIndex,
            };
            _dbContext.PracticeAttempts.Add(attempt);
        }

        attempt.StudentAnswer = studentAnswer;
        attempt.Score = grade.Score;
        attempt.Feedback = grade.Feedback;
        attempt.PointsAwarded = reward.AlreadyEarned ? 0 : reward.Reward?.PointsAwarded ?? 0;
        attempt.CompletedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PracticeExerciseView
        {
            Index = exerciseIndex,
            Title = exercise.Title,
            Instructions = exercise.Instructions,
            StudentAnswer = attempt.StudentAnswer,
            Score = attempt.Score,
            Feedback = attempt.Feedback,
        };
    }

    private async Task<PracticeSessionView> ToViewAsync(PracticeSession session, CancellationToken cancellationToken)
    {
        var exercises = JsonSerializer.Deserialize<List<TecPracticeExercise>>(session.ExercisesJson, JsonOptions) ?? [];
        var attempts = await _dbContext.PracticeAttempts
            .AsNoTracking()
            .Where(a => a.PracticeSessionId == session.PracticeSessionId)
            .ToDictionaryAsync(a => a.ExerciseIndex, cancellationToken);

        return new PracticeSessionView
        {
            PracticeSessionId = session.PracticeSessionId,
            CapabilityId = session.CapabilityId,
            CapabilityName = session.CapabilityName,
            CreatedDate = session.CreatedDate,
            Exercises = exercises.Select((e, i) =>
            {
                attempts.TryGetValue(i, out var attempt);
                return new PracticeExerciseView
                {
                    Index = i,
                    Title = e.Title,
                    Instructions = e.Instructions,
                    StudentAnswer = attempt?.StudentAnswer,
                    Score = attempt?.Score,
                    Feedback = attempt?.Feedback,
                };
            }).ToList(),
        };
    }
}
