using HumanOS.Data;
using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// Tracks real student "entries" into a COURSE (2026-10-02) — one
/// <see cref="StructuredLearningEngramSession"/> row per visit (created
/// once when the student opens the course, or the first engram if they
/// land directly on one — NEVER re-created just for moving between
/// engrams within the same visit), with a per-engram-per-step timeline.
/// Lets the admin side report entry count, entry/exit times, total time
/// in the session, and how many distinct engrams were completed. Also the
/// grouping key for Voice/Text "Q&amp;A with Professor" token usage (see
/// <see cref="StructuredLearningEngramUsageService"/>), so cost rolls up
/// per real visit instead of lifetime-per-person.
/// </summary>
public sealed class StructuredLearningEngramSessionService
{
    private readonly HumanOsDbContext _dbContext;

    public StructuredLearningEngramSessionService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Always creates a NEW session row — every call is one more
    /// counted "entry" into this course. Returns the full session (not just
    /// the id) so callers can show the real, server-recorded start time.</summary>
    public async Task<StructuredLearningEngramSession> StartSessionAsync(
        Guid? personId, Guid? documentId, Guid? sectionId, CancellationToken cancellationToken, string? source = null)
    {
        var session = new StructuredLearningEngramSession
        {
            PersonId = personId,
            StructuredLearningDocumentId = documentId,
            StructuredLearningDocumentSectionId = sectionId,
            StartedDate = DateTime.UtcNow,
            LastActivityDate = DateTime.UtcNow,
            Source = source,
        };
        _dbContext.StructuredLearningEngramSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<(int SessionCount, Guid? EngramSessionId, DateTime? StartedDate)> GetCourseSessionSummaryAsync(
        Guid personId, Guid documentId, CancellationToken cancellationToken)
    {
        var sessionsWithActivity = _dbContext.StructuredLearningEngramSessions
            .AsNoTracking()
            .Where(session => session.PersonId == personId &&
                    session.StructuredLearningDocumentId == documentId &&
                    session.StructuredLearningDocumentSectionId == null &&
                    _dbContext.StructuredLearningEngramSessionSteps.Any(
                        step => step.StructuredLearningEngramSessionId == session.StructuredLearningEngramSessionId));

        var sessionCount = await sessionsWithActivity.CountAsync(cancellationToken);
        var latestSession = await sessionsWithActivity
            .OrderByDescending(session => session.StartedDate)
            .ThenByDescending(session => session.StructuredLearningEngramSessionId)
            .Select(session => new { session.StructuredLearningEngramSessionId, session.StartedDate })
            .FirstOrDefaultAsync(cancellationToken);

        return (sessionCount, latestSession?.StructuredLearningEngramSessionId, latestSession?.StartedDate);
    }

    /// <summary>Records this engram's step start time (first call for that
    /// step+engram within the session) and bumps the session's LastActivityDate.</summary>
    public async Task RecordStepStartAsync(Guid sessionId, Guid? sectionId, string stepType, CancellationToken cancellationToken)
    {
        var session = await _dbContext.StructuredLearningEngramSessions
            .FirstOrDefaultAsync(s => s.StructuredLearningEngramSessionId == sessionId, cancellationToken);
        if (session is null) return;

        var now = DateTime.UtcNow;
        session.LastActivityDate = now;

        var openSteps = await _dbContext.StructuredLearningEngramSessionSteps
            .Where(s => s.StructuredLearningEngramSessionId == sessionId && s.CompletedDate == null
                && (s.StructuredLearningDocumentSectionId != sectionId || s.StepType != stepType))
            .ToListAsync(cancellationToken);
        foreach (var openStep in openSteps)
        {
            openStep.CompletedDate = now;
        }

        var existingStep = await _dbContext.StructuredLearningEngramSessionSteps
            .FirstOrDefaultAsync(
                s => s.StructuredLearningEngramSessionId == sessionId && s.StructuredLearningDocumentSectionId == sectionId && s.StepType == stepType,
                cancellationToken);
        if (existingStep is null)
        {
            _dbContext.StructuredLearningEngramSessionSteps.Add(new StructuredLearningEngramSessionStep
            {
                StructuredLearningEngramSessionId = sessionId,
                StructuredLearningDocumentSectionId = sectionId,
                StepType = stepType,
                StartedDate = now,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Records this engram's step completion time. Does NOT close
    /// the whole session anymore (2026-10-02) — one session now spans every
    /// engram the student visits, so finishing one engram's Assessment must
    /// not end the course session. The session only closes via
    /// <see cref="CloseSessionAsync"/>, called when the student explicitly
    /// exits the course.</summary>
    public async Task RecordStepCompleteAsync(Guid sessionId, Guid? sectionId, string stepType, CancellationToken cancellationToken)
    {
        var session = await _dbContext.StructuredLearningEngramSessions
            .FirstOrDefaultAsync(s => s.StructuredLearningEngramSessionId == sessionId, cancellationToken);
        if (session is null) return;

        var now = DateTime.UtcNow;
        session.LastActivityDate = now;

        var step = await _dbContext.StructuredLearningEngramSessionSteps
            .FirstOrDefaultAsync(
                s => s.StructuredLearningEngramSessionId == sessionId && s.StructuredLearningDocumentSectionId == sectionId && s.StepType == stepType
                    && s.ActiveDurationMilliseconds == null,
                cancellationToken);
        if (step is null)
        {
            step = new StructuredLearningEngramSessionStep
            {
                StructuredLearningEngramSessionId = sessionId,
                StructuredLearningDocumentSectionId = sectionId,
                StepType = stepType,
                StartedDate = now,
            };
            _dbContext.StructuredLearningEngramSessionSteps.Add(step);
        }
        step.CompletedDate ??= now;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Persists a cumulative, idempotent visible interval. Heartbeats
    /// retain measured time even if the browser never sends its final event.</summary>
    public async Task RecordStepActivityAsync(Guid sessionId, Guid sectionId, string stepType,
        Guid intervalId, long elapsedMilliseconds, bool stopped, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedMilliseconds);
        var session = await _dbContext.StructuredLearningEngramSessions
            .FirstOrDefaultAsync(s => s.StructuredLearningEngramSessionId == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("Course session not found.");
        var interval = await _dbContext.StructuredLearningEngramSessionSteps
            .FirstOrDefaultAsync(s => s.StructuredLearningEngramSessionStepId == intervalId, cancellationToken);
        if (interval is not null && (interval.StructuredLearningEngramSessionId != sessionId
            || interval.StructuredLearningDocumentSectionId != sectionId || interval.StepType != stepType
            || interval.ActiveDurationMilliseconds == null))
        {
            throw new ArgumentException("The interval does not belong to this step.");
        }

        // Late keepalive requests may arrive after the explicit course close.
        // Their measured duration is still valid; they must not reopen the session.
        var now = DateTime.UtcNow;
        if (interval is null)
        {
            interval = new StructuredLearningEngramSessionStep
            {
                StructuredLearningEngramSessionStepId = intervalId,
                StructuredLearningEngramSessionId = sessionId,
                StructuredLearningDocumentSectionId = sectionId,
                StepType = stepType,
                StartedDate = now.AddMilliseconds(-elapsedMilliseconds),
                ActiveDurationMilliseconds = 0,
            };
            _dbContext.StructuredLearningEngramSessionSteps.Add(interval);
        }
        interval.ActiveDurationMilliseconds = Math.Max(interval.ActiveDurationMilliseconds ?? 0, elapsedMilliseconds);
        if (stopped || session.CompletedDate.HasValue)
        {
            interval.CompletedDate ??= now;
        }
        if (!session.CompletedDate.HasValue) session.LastActivityDate = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (session.CompletedDate is DateTime closed)
        {
            var steps = await _dbContext.StructuredLearningEngramSessionSteps
                .Where(s => s.StructuredLearningEngramSessionId == sessionId)
                .ToListAsync(cancellationToken);
            session.DurationSeconds = StructuredLearningEngramStatsService.CalculateSessionDuration(steps, sessionId, closed);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Closes out the whole course session (2026-10-02) — called
    /// when the student explicitly exits the course ("Exit course"
    /// button). Sets CompletedDate and sums measured step time rather than
    /// wall time, preserving pauses and excluding the course overview.</summary>
    public async Task CloseSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _dbContext.StructuredLearningEngramSessions
            .FirstOrDefaultAsync(s => s.StructuredLearningEngramSessionId == sessionId, cancellationToken);
        if (session is null || session.CompletedDate.HasValue) return;

        var now = DateTime.UtcNow;
        session.CompletedDate = now;
        session.LastActivityDate = now;
        var steps = await _dbContext.StructuredLearningEngramSessionSteps
            .Where(s => s.StructuredLearningEngramSessionId == sessionId)
            .ToListAsync(cancellationToken);
        foreach (var step in steps.Where(s => s.CompletedDate == null))
        {
            step.CompletedDate = now;
        }
        session.DurationSeconds = StructuredLearningEngramStatsService.CalculateSessionDuration(steps, sessionId, now);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
