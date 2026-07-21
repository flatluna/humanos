using System.Text;
using System.Text.Json;
using HumanOS.Agents.Runtime;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;
using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services;

/// <summary>
/// Runtime — Adaptive Assessment engine (2026-07-18). Drives the dynamic,
/// one-question-at-a-time Assessment stage described in the user's own
/// spec (Memory Paradox: "no queremos preguntas repetidas ni exámenes
/// estáticos"). Deterministic orchestration in code; the LLM
/// (<see cref="AdaptiveAssessmentAgent"/>) only proposes question text and
/// a raw per-question score — this engine ALWAYS decides the objectively-
/// checkable facts (Correctness thresholds, FinalScore average, Passed
/// cutoff, round chaining), same "LLM proposes, code decides" rule as
/// <see cref="AssessmentEvaluator"/>.
///
/// A round always has exactly 5 questions. FinalScore = average of the 5
/// questions' ScoreContribution. Passed = FinalScore &gt;= 80. A Failed
/// round automatically starts a brand-new round (new RoundNumber, 5 new
/// questions) in the SAME call that closes out the failed one — the
/// frontend never has to separately trigger a retry.
///
/// On a Passed round, this engine ALSO writes a
/// <see cref="LearningAssessmentResult"/> row (Score=FinalScore,
/// Passed=true) so <see cref="GraphProgressionEngine"/>'s existing
/// completed-node query (which reads LearningAssessmentResults.Any(a =>
/// a.Passed)) keeps working completely unchanged — zero coupling needed
/// between this engine and graph progression.
/// </summary>
public sealed class AdaptiveAssessmentEngine
{
    private const int QuestionsPerRound = 5;
    private const int PassThreshold = 80;

    /// <summary>Base offset for per-question Assessment illustration blob
    /// indexes (2026-07-20) — never collides with the Studio pipeline's low
    /// sequential Hypothesis/Teaching indexes (1, 2, ...) or
    /// KnowledgeExpansion's fixed index (99). The actual index adds the
    /// node's current illustration count so repeated questions for the same
    /// node never collide with each other either.</summary>
    private const int AssessmentIllustrationBaseIndex = 100;

    private readonly AdaptiveAssessmentAgent _agent;
    private readonly HumanOS.Agents.Studio.GraphIllustrationImageService _imageService;
    private readonly HumanOS.Storage.CapabilityGraphIllustrationStorageService _illustrationStorage;
    private readonly Microsoft.Extensions.Logging.ILogger<AdaptiveAssessmentEngine> _logger;

    public AdaptiveAssessmentEngine(
        AdaptiveAssessmentAgent agent,
        HumanOS.Agents.Studio.GraphIllustrationImageService imageService,
        HumanOS.Storage.CapabilityGraphIllustrationStorageService illustrationStorage,
        Microsoft.Extensions.Logging.ILogger<AdaptiveAssessmentEngine> logger)
    {
        _agent = agent;
        _imageService = imageService;
        _illustrationStorage = illustrationStorage;
        _logger = logger;
    }

    public sealed class QuestionInfo
    {
        public Guid AssessmentQuestionId { get; set; }
        public int QuestionIndex { get; set; }
        public AssessmentQuestionType QuestionType { get; set; }
        public string QuestionText { get; set; } = string.Empty;

        /// <summary>Set only when AdaptiveAssessmentAgent decided this
        /// specific question genuinely benefits from a visual scenario.
        /// Servable via the existing GetIllustrationImageFunction endpoint
        /// (same as any other CapabilityGraphNodeIllustration).</summary>
        public Guid? IllustrationId { get; set; }
    }

    public sealed class RoundState
    {
        public Guid AssessmentRoundId { get; set; }
        public int RoundNumber { get; set; }
        public AssessmentRoundStatus Status { get; set; }
        public int? FinalScore { get; set; }

        /// <summary>Null once the round is Passed/Failed (nothing left to answer).</summary>
        public QuestionInfo? CurrentQuestion { get; set; }
    }

    public sealed class AnswerGrade
    {
        public AssessmentQuestionCorrectness Correctness { get; set; }
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
        public Guid? NewAssessmentRoundId { get; set; }
    }

    /// <summary>
    /// Starts a brand-new round (RoundNumber = previous rounds for this node + 1)
    /// and generates its first question. Call this the first time a student
    /// reaches the Assessment step for a node (when GetActiveRoundAsync
    /// returns null).
    /// </summary>
    public async Task<RoundState> StartRoundAsync(HumanOsDbContext dbContext, Guid learningSessionNodeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var context = await LoadNodeContextAsync(dbContext, learningSessionNodeId, cancellationToken);

        var priorRoundNumbers = await dbContext.AssessmentRounds
            .Where(r => r.LearningSessionNodeId == learningSessionNodeId)
            .Select(r => r.RoundNumber)
            .ToListAsync(cancellationToken);

        var roundNumber = priorRoundNumbers.Count == 0 ? 1 : priorRoundNumbers.Max() + 1;

        // If this is a retry, pull the observed errors from the immediately
        // preceding (Failed) round so the new questions can specifically
        // target them ("atacar especialmente los errores detectados").
        List<string> priorRoundErrors = [];
        if (roundNumber > 1)
        {
            var previousRound = await dbContext.AssessmentRounds
                .Include(r => r.Questions)
                .Where(r => r.LearningSessionNodeId == learningSessionNodeId && r.RoundNumber == roundNumber - 1)
                .FirstOrDefaultAsync(cancellationToken);

            priorRoundErrors = previousRound?.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q.ObservedError))
                .Select(q => q.ObservedError!)
                .ToList() ?? [];
        }

        var round = new AssessmentRound
        {
            LearningSessionNodeId = learningSessionNodeId,
            RoundNumber = roundNumber,
            Status = AssessmentRoundStatus.InProgress
        };
        dbContext.AssessmentRounds.Add(round);

        var question = await GenerateQuestionAsync(
            dbContext, context, roundNumber, questionIndex: 1, alreadyAskedThisRound: [], errorsObservedSoFar: priorRoundErrors,
            multipleChoiceUsedThisRound: false, cancellationToken);

        var questionRow = new AssessmentQuestion
        {
            AssessmentRoundId = round.AssessmentRoundId,
            QuestionIndex = 1,
            QuestionType = question.Type,
            QuestionText = question.Text,
            IllustrationId = question.IllustrationId
        };
        dbContext.AssessmentQuestions.Add(questionRow);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RoundState
        {
            AssessmentRoundId = round.AssessmentRoundId,
            RoundNumber = round.RoundNumber,
            Status = round.Status,
            FinalScore = null,
            CurrentQuestion = new QuestionInfo
            {
                AssessmentQuestionId = questionRow.AssessmentQuestionId,
                QuestionIndex = 1,
                QuestionType = questionRow.QuestionType,
                QuestionText = questionRow.QuestionText,
                IllustrationId = questionRow.IllustrationId
            }
        };
    }

    /// <summary>
    /// Returns the MOST RECENT round for this node (so a page reload can
    /// resume mid-round, or show the pass/fail summary of the last
    /// completed round), or null if no round has ever been started for
    /// this node yet (caller should call StartRoundAsync).
    /// </summary>
    public async Task<RoundState?> GetActiveRoundAsync(HumanOsDbContext dbContext, Guid learningSessionNodeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var round = await dbContext.AssessmentRounds
            .Include(r => r.Questions)
            .Where(r => r.LearningSessionNodeId == learningSessionNodeId)
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null)
        {
            return null;
        }

        var currentQuestion = round.Questions
            .Where(q => q.StudentAnswer is null)
            .OrderBy(q => q.QuestionIndex)
            .FirstOrDefault();

        return new RoundState
        {
            AssessmentRoundId = round.AssessmentRoundId,
            RoundNumber = round.RoundNumber,
            Status = round.Status,
            FinalScore = round.FinalScore,
            CurrentQuestion = currentQuestion is null ? null : new QuestionInfo
            {
                AssessmentQuestionId = currentQuestion.AssessmentQuestionId,
                QuestionIndex = currentQuestion.QuestionIndex,
                QuestionType = currentQuestion.QuestionType,
                QuestionText = currentQuestion.QuestionText,
                IllustrationId = currentQuestion.IllustrationId
            }
        };
    }

    /// <summary>
    /// Grades the student's answer to one question, then either generates
    /// the next question THIS round, or — if this was question 5 — closes
    /// out the round (Passed/Failed) and, if Failed, immediately starts a
    /// brand-new round with 5 new questions in the SAME call.
    /// </summary>
    public async Task<SubmitAnswerResult> SubmitAnswerAsync(
        HumanOsDbContext dbContext, Guid assessmentQuestionId, string studentAnswer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var question = await dbContext.AssessmentQuestions
            .Include(q => q.AssessmentRound)
            .ThenInclude(r => r!.Questions)
            .FirstOrDefaultAsync(q => q.AssessmentQuestionId == assessmentQuestionId, cancellationToken);

        if (question is null)
        {
            throw new InvalidOperationException($"AssessmentQuestion {assessmentQuestionId} not found.");
        }

        if (question.StudentAnswer is not null)
        {
            throw new InvalidOperationException($"AssessmentQuestion {assessmentQuestionId} has already been answered.");
        }

        var round = question.AssessmentRound!;
        var context = await LoadNodeContextAsync(dbContext, round.LearningSessionNodeId, cancellationToken);

        var alreadyAnsweredThisRound = round.Questions
            .Where(q => q.AssessmentQuestionId != question.AssessmentQuestionId && q.StudentAnswer is not null)
            .OrderBy(q => q.QuestionIndex)
            .ToList();

        var gradingPrompt = BuildGradingPrompt(context, question, studentAnswer, alreadyAnsweredThisRound);
        var graded = await _agent.GradeAnswerAsync(gradingPrompt, cancellationToken);

        var score = Math.Clamp(graded.Score, 0, 100);
        var correctness = ToCorrectness(score);

        var now = DateTime.UtcNow;
        question.StudentAnswer = studentAnswer;
        question.ScoreContribution = score;
        question.Correctness = correctness;
        question.Feedback = graded.Feedback;
        question.ObservedError = string.IsNullOrWhiteSpace(graded.ObservedError) ? null : graded.ObservedError;
        question.AnsweredDate = now;

        var result = new SubmitAnswerResult
        {
            Grade = new AnswerGrade { Correctness = correctness, Score = score, Feedback = graded.Feedback }
        };

        if (question.QuestionIndex < QuestionsPerRound)
        {
            // Still questions left this round — generate the next one, adapting to errors observed so far.
            var errorsSoFar = alreadyAnsweredThisRound
                .Concat([question])
                .Where(q => !string.IsNullOrWhiteSpace(q.ObservedError))
                .Select(q => q.ObservedError!)
                .ToList();
            var alreadyAsked = round.Questions
                .Where(q => q.AssessmentQuestionId != question.AssessmentQuestionId || true)
                .OrderBy(q => q.QuestionIndex)
                .Select(q => q.QuestionText)
                .ToList();
            var mcUsed = round.Questions.Any(q => q.QuestionType == AssessmentQuestionType.MultipleChoice);

            var nextQuestion = await GenerateQuestionAsync(
                dbContext, context, round.RoundNumber, question.QuestionIndex + 1, alreadyAsked, errorsSoFar, mcUsed, cancellationToken);

            var nextQuestionRow = new AssessmentQuestion
            {
                AssessmentRoundId = round.AssessmentRoundId,
                QuestionIndex = question.QuestionIndex + 1,
                QuestionType = nextQuestion.Type,
                QuestionText = nextQuestion.Text,
                IllustrationId = nextQuestion.IllustrationId
            };
            dbContext.AssessmentQuestions.Add(nextQuestionRow);

            result.RoundComplete = false;
            result.NextQuestion = new QuestionInfo
            {
                AssessmentQuestionId = nextQuestionRow.AssessmentQuestionId,
                QuestionIndex = nextQuestionRow.QuestionIndex,
                QuestionType = nextQuestionRow.QuestionType,
                QuestionText = nextQuestionRow.QuestionText,
                IllustrationId = nextQuestionRow.IllustrationId
            };

            await dbContext.SaveChangesAsync(cancellationToken);
            return result;
        }

        // This was question 5 — close out the round.
        var allScores = alreadyAnsweredThisRound.Select(q => q.ScoreContribution!.Value).Concat([score]).ToList();
        var finalScore = (int)Math.Round(allScores.Average());
        var passed = finalScore >= PassThreshold;

        round.Status = passed ? AssessmentRoundStatus.Passed : AssessmentRoundStatus.Failed;
        round.FinalScore = finalScore;
        round.CompletedDate = now;

        result.RoundComplete = true;
        result.Passed = passed;
        result.FinalScore = finalScore;

        if (passed)
        {
            var aggregateFeedback = BuildAggregateFeedback(alreadyAnsweredThisRound.Concat([question]).OrderBy(q => q.QuestionIndex));
            dbContext.LearningAssessmentResults.Add(new Models.Learning.LearningAssessmentResult
            {
                LearningSessionNodeId = round.LearningSessionNodeId,
                Score = finalScore,
                Passed = true,
                Feedback = aggregateFeedback
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            return result;
        }

        // Failed — record it for history, then auto-start a brand-new round immediately.
        var failedFeedback = BuildAggregateFeedback(alreadyAnsweredThisRound.Concat([question]).OrderBy(q => q.QuestionIndex));
        dbContext.LearningAssessmentResults.Add(new Models.Learning.LearningAssessmentResult
        {
            LearningSessionNodeId = round.LearningSessionNodeId,
            Score = finalScore,
            Passed = false,
            Feedback = failedFeedback
        });

        var newRoundNumber = round.RoundNumber + 1;
        var newRound = new AssessmentRound
        {
            LearningSessionNodeId = round.LearningSessionNodeId,
            RoundNumber = newRoundNumber,
            Status = AssessmentRoundStatus.InProgress
        };
        dbContext.AssessmentRounds.Add(newRound);

        var failedRoundErrors = alreadyAnsweredThisRound.Concat([question])
            .Where(q => !string.IsNullOrWhiteSpace(q.ObservedError))
            .Select(q => q.ObservedError!)
            .ToList();

        var firstQuestionOfNewRound = await GenerateQuestionAsync(
            dbContext, context, newRoundNumber, questionIndex: 1, alreadyAskedThisRound: [], errorsObservedSoFar: failedRoundErrors,
            multipleChoiceUsedThisRound: false, cancellationToken);

        var newQuestionRow = new AssessmentQuestion
        {
            AssessmentRoundId = newRound.AssessmentRoundId,
            QuestionIndex = 1,
            QuestionType = firstQuestionOfNewRound.Type,
            QuestionText = firstQuestionOfNewRound.Text,
            IllustrationId = firstQuestionOfNewRound.IllustrationId
        };
        dbContext.AssessmentQuestions.Add(newQuestionRow);

        result.NewRoundNumber = newRoundNumber;
        result.NewAssessmentRoundId = newRound.AssessmentRoundId;
        result.NextQuestion = new QuestionInfo
        {
            AssessmentQuestionId = newQuestionRow.AssessmentQuestionId,
            QuestionIndex = 1,
            QuestionType = newQuestionRow.QuestionType,
            QuestionText = newQuestionRow.QuestionText,
            IllustrationId = newQuestionRow.IllustrationId
        };

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static AssessmentQuestionCorrectness ToCorrectness(int score) => score switch
    {
        >= 80 => AssessmentQuestionCorrectness.Correct,
        >= 40 => AssessmentQuestionCorrectness.PartiallyCorrect,
        _ => AssessmentQuestionCorrectness.Incorrect
    };

    private static string BuildAggregateFeedback(IEnumerable<AssessmentQuestion> questions)
    {
        var builder = new StringBuilder();
        foreach (var q in questions)
        {
            builder.AppendLine($"Pregunta {q.QuestionIndex}: {q.Feedback}");
        }
        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Fixed priority order from the spec (ActiveRecall &gt; Comprehension &gt;
    /// Application &gt; ErrorDetection &gt; Transfer &gt; Production). Used to
    /// DETERMINISTICALLY assign each question's type in code — the LLM was
    /// observed to consistently default to ActiveRecall for all 5 questions
    /// of a round when left free to choose (surface wording varied, but the
    /// underlying task/type never did), which violates the spec's explicit
    /// requirement for genuine variety across the round's question types.
    /// </summary>
    private static readonly AssessmentQuestionType[] TypeRotation =
    [
        AssessmentQuestionType.ActiveRecall,
        AssessmentQuestionType.Comprehension,
        AssessmentQuestionType.Application,
        AssessmentQuestionType.ErrorDetection,
        AssessmentQuestionType.Transfer,
        AssessmentQuestionType.Production
    ];

    /// <summary>
    /// Deterministically picks the type for a given question slot. The
    /// rotation's starting offset shifts by round number, so consecutive
    /// rounds (e.g. a Failed round's automatic retry) don't just repeat the
    /// same 5-of-6 types — over 2 rounds all 6 types get exercised.
    /// </summary>
    private static AssessmentQuestionType GetRequiredType(int roundNumber, int questionIndex)
    {
        var offset = (roundNumber - 1) % TypeRotation.Length;
        var slot = (offset + questionIndex - 1) % TypeRotation.Length;
        return TypeRotation[slot];
    }

    private async Task<(AssessmentQuestionType Type, string Text, Guid? IllustrationId)> GenerateQuestionAsync(
        HumanOsDbContext dbContext, NodeContext context, int roundNumber, int questionIndex, IReadOnlyList<string> alreadyAskedThisRound,
        IReadOnlyList<string> errorsObservedSoFar, bool multipleChoiceUsedThisRound, CancellationToken cancellationToken)
    {
        var requiredType = GetRequiredType(roundNumber, questionIndex);

        // MultipleChoice must stay a rare minority — never allowed for the
        // two types that fundamentally require free recall/construction
        // (ActiveRecall, Production), and capped at 1 per round even then.
        var allowMultipleChoiceSubstitute = !multipleChoiceUsedThisRound
            && requiredType != AssessmentQuestionType.ActiveRecall
            && requiredType != AssessmentQuestionType.Production;

        var prompt = BuildQuestionGenerationPrompt(context, questionIndex, alreadyAskedThisRound, errorsObservedSoFar, requiredType, allowMultipleChoiceSubstitute);
        var response = await _agent.GenerateQuestionAsync(prompt, cancellationToken);

        var illustrationId = await TryGenerateQuestionIllustrationAsync(dbContext, context, response.DiagramPrompt, cancellationToken);

        if (allowMultipleChoiceSubstitute && ParseQuestionType(response.QuestionType) == AssessmentQuestionType.MultipleChoice)
        {
            return (AssessmentQuestionType.MultipleChoice, response.QuestionText, illustrationId);
        }

        // The type itself is decided by CODE, not trusted from the LLM —
        // this guarantees genuine diversity across the round regardless of
        // what the model happens to label its own output as.
        return (requiredType, response.QuestionText, illustrationId);
    }

    /// <summary>
    /// Best-effort, on-demand illustration for ONE Assessment question
    /// (2026-07-20) — mirrors KnowledgeExpansionService.TryGenerateDiagramAsync's
    /// "never throws, never blocks question generation" contract. Only runs
    /// when the agent actually proposed a DiagramPrompt (most questions get
    /// none — this is deliberately the minority case). Derives the tenantId/
    /// capabilityId needed for the blob path from one of the node's EXISTING
    /// illustrations (uploaded with real values at capability-creation
    /// time); skips silently if the node has none yet.
    /// </summary>
    private async Task<Guid?> TryGenerateQuestionIllustrationAsync(
        HumanOsDbContext dbContext, NodeContext context, string? diagramPrompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(diagramPrompt))
        {
            _logger.LogInformation("AdaptiveAssessment illustration: skipped - agent returned no DiagramPrompt for node {NodeId}.", context.NodeId);
            return null;
        }

        if (!_imageService.IsConfigured || !_illustrationStorage.IsConfigured)
        {
            _logger.LogWarning("AdaptiveAssessment illustration: skipped - imageService.IsConfigured={ImageConfigured}, illustrationStorage.IsConfigured={StorageConfigured}.", _imageService.IsConfigured, _illustrationStorage.IsConfigured);
            return null;
        }

        if (context.IllustrationPathSeed is null)
        {
            _logger.LogWarning("AdaptiveAssessment illustration: skipped - node {NodeId} has no existing illustration to derive tenant/capability path from.", context.NodeId);
            return null;
        }

        var segments = context.IllustrationPathSeed.Split('/');
        if (segments.Length < 3 || !Guid.TryParse(segments[0], out var tenantId) || !Guid.TryParse(segments[1], out var capabilityId))
        {
            _logger.LogWarning("AdaptiveAssessment illustration: skipped - could not parse tenant/capability GUIDs from path seed '{PathSeed}'.", context.IllustrationPathSeed);
            return null;
        }

        _logger.LogInformation("AdaptiveAssessment illustration: attempting generation for node {NodeId} with prompt '{Prompt}'.", context.NodeId, diagramPrompt);

        try
        {
            var existingCount = await dbContext.CapabilityGraphNodeIllustrations
                .CountAsync(i => i.CapabilityGraphNodeId == context.NodeId, cancellationToken);
            var imageIndex = AssessmentIllustrationBaseIndex + existingCount;

            var generated = await _imageService.GenerateAsync(diagramPrompt, cancellationToken);
            using var imageStream = generated.ImageBytes.ToStream();

            var storagePath = await _illustrationStorage.UploadIllustrationAsync(
                tenantId, capabilityId, context.NodeId, imageIndex, imageStream, cancellationToken: cancellationToken);

            var illustration = new CapabilityGraphNodeIllustration
            {
                CapabilityGraphNodeIllustrationId = Guid.NewGuid(),
                CapabilityGraphNodeId = context.NodeId,
                StoragePath = storagePath,
                Prompt = diagramPrompt,
                Purpose = IllustrationPurpose.Assessment,
                ImageModel = generated.ImageModel,
                Width = generated.Width,
                Height = generated.Height,
                CreatedDate = DateTime.UtcNow
            };

            dbContext.CapabilityGraphNodeIllustrations.Add(illustration);
            _logger.LogInformation("AdaptiveAssessment illustration: generated successfully, CapabilityGraphNodeIllustrationId={IllustrationId}.", illustration.CapabilityGraphNodeIllustrationId);
            return illustration.CapabilityGraphNodeIllustrationId;
        }
        catch (Exception ex) when (ex.Message.Contains("moderation_blocked", StringComparison.OrdinalIgnoreCase))
        {
            // Expected/correct outcome, not a bug: Azure OpenAI's image
            // safety system rejected this prompt. Logged at Warning (not
            // Error) so it's not confused with an actual pipeline failure.
            _logger.LogWarning("AdaptiveAssessment illustration: skipped - Azure OpenAI's safety system rejected the prompt (moderation_blocked) for node {NodeId}.", context.NodeId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdaptiveAssessment illustration: generation FAILED for node {NodeId}.", context.NodeId);
            return null;
        }
    }

    private static AssessmentQuestionType ParseQuestionType(string raw) =>
        Enum.TryParse<AssessmentQuestionType>(raw, ignoreCase: true, out var parsed) ? parsed : AssessmentQuestionType.ActiveRecall;

    private sealed class NodeContext
    {
        public Guid NodeId { get; set; }
        public string CapabilityName { get; set; } = string.Empty;
        public string NodeName { get; set; } = string.Empty;
        public string? AcademicDefinition { get; set; }
        public string? Interpretation { get; set; }
        public List<string> Examples { get; set; } = [];
        public List<string> Applications { get; set; } = [];
        public List<string> IllustrationCaptions { get; set; } = [];
        public Dictionary<string, string> PriorStepEvidence { get; set; } = [];

        /// <summary>StoragePath of one of the node's EXISTING illustrations
        /// (Hypothesis/Teaching), reused only to derive the tenantId/
        /// capabilityId path segments for a NEW Assessment illustration —
        /// null if the node has no illustrations yet (Assessment images are
        /// then skipped, same as KnowledgeExpansionService).</summary>
        public string? IllustrationPathSeed { get; set; }
    }

    private static async Task<NodeContext> LoadNodeContextAsync(HumanOsDbContext dbContext, Guid learningSessionNodeId, CancellationToken cancellationToken)
    {
        var sessionNode = await dbContext.LearningSessionNodes
            .Include(n => n.LearningSession).ThenInclude(s => s!.Capability)
            .Include(n => n.CapabilityGraphNode).ThenInclude(gn => gn!.Illustrations)
            .Include(n => n.Steps).ThenInclude(s => s.Evidence)
            .FirstOrDefaultAsync(n => n.LearningSessionNodeId == learningSessionNodeId, cancellationToken);

        if (sessionNode is null)
        {
            throw new InvalidOperationException($"LearningSessionNode {learningSessionNodeId} not found.");
        }

        var node = sessionNode.CapabilityGraphNode
            ?? throw new InvalidOperationException($"LearningSessionNode {learningSessionNodeId} has no CapabilityGraphNode loaded.");

        var priorStepEvidence = new Dictionary<string, string>();
        foreach (var stepType in new[] { ExperienceStepType.Hypothesis, ExperienceStepType.Teaching, ExperienceStepType.Recall, ExperienceStepType.Production })
        {
            var step = sessionNode.Steps.FirstOrDefault(s => s.StepType == stepType);
            var latestEvidence = step?.Evidence.OrderByDescending(e => e.CreatedDate).FirstOrDefault();
            if (latestEvidence is not null)
            {
                priorStepEvidence[stepType.ToString()] = latestEvidence.StudentResponse;
            }
        }

        return new NodeContext
        {
            NodeId = node.CapabilityGraphNodeId,
            CapabilityName = sessionNode.LearningSession?.Capability?.Name ?? string.Empty,
            NodeName = node.Name,
            AcademicDefinition = node.AcademicDefinition,
            Interpretation = node.Interpretation,
            Examples = DeserializeStringList(node.ExamplesJson),
            Applications = DeserializeStringList(node.ApplicationsJson),
            IllustrationCaptions = node.Illustrations.Where(i => !string.IsNullOrWhiteSpace(i.Caption)).Select(i => i.Caption!).ToList(),
            PriorStepEvidence = priorStepEvidence,
            IllustrationPathSeed = node.Illustrations.FirstOrDefault()?.StoragePath
        };
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildQuestionGenerationPrompt(
        NodeContext context, int questionIndex, IReadOnlyList<string> alreadyAsked,
        IReadOnlyList<string> errorsObserved, AssessmentQuestionType requiredType, bool allowMultipleChoiceSubstitute)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Question {questionIndex} of 5 for this Assessment round.");
        builder.AppendLine();
        AppendNodeContext(builder, context);

        if (alreadyAsked.Count > 0)
        {
            builder.AppendLine("QUESTIONS ALREADY ASKED THIS ROUND (never repeat these):");
            foreach (var q in alreadyAsked)
            {
                builder.AppendLine($"- {q}");
            }
            builder.AppendLine();
        }

        if (errorsObserved.Count > 0)
        {
            builder.AppendLine("ERRORS/CONFUSIONS OBSERVED SO FAR (consider specifically probing or addressing one of these):");
            foreach (var e in errorsObserved)
            {
                builder.AppendLine($"- {e}");
            }
            builder.AppendLine();
        }

        builder.AppendLine($"REQUIRED QUESTION TYPE FOR THIS QUESTION: {requiredType} — {DescribeQuestionType(requiredType)}");
        builder.AppendLine(
            "This type was already chosen deterministically to guarantee real variety across the round's 5 " +
            "questions. You MUST write a question that genuinely matches this type — do not write another " +
            "ActiveRecall-style \"count/list the objects one by one\" task wearing a different label; the task " +
            "ITSELF must exercise this specific cognitive skill.");

        builder.AppendLine(allowMultipleChoiceSubstitute
            ? "As a RARE exception, if this node's content is exceptionally well suited to a brief multiple-choice format for THIS ONE question, you may use MultipleChoice instead of the required type — but at most once per round, and only if it still demands real reasoning, not trivial recognition."
            : "Do not use a multiple-choice format for this question.");

        return builder.ToString();
    }

    private static string DescribeQuestionType(AssessmentQuestionType type) => type switch
    {
        AssessmentQuestionType.ActiveRecall =>
            "a SHORT, DIRECT retrieval of a specific fact/computation with concrete values — e.g. \"¿Cuánto es 17 + 8?\". NOT a request to explain, define, or justify (that's Comprehension).",
        AssessmentQuestionType.Comprehension =>
            "ONE request to explain or paraphrase the concept in their own words (why it works, not just that it works) — a single ask, not also asking for extra examples.",
        AssessmentQuestionType.Application =>
            "ONE new, concrete real-world scenario with specific values, asking for the single computation/decision that solves it.",
        AssessmentQuestionType.ErrorDetection =>
            "a short (2-3 line) flawed worked example with a specific, plausible mistake, plus ONE clear ask: what's wrong and what's the correct answer.",
        AssessmentQuestionType.Transfer =>
            "ONE new, concrete scenario in an unfamiliar context/unit different from anything used so far in this session, asking for the single computation/decision that solves it.",
        AssessmentQuestionType.Production =>
            "ONE creative task: construct/invent something original (e.g. their own example situation) that demonstrates mastery — not that task plus a separate justification or verification step.",
        _ => "a short, single-focus question that requires genuine reasoning, not passive recognition."
    };

    private static string BuildGradingPrompt(
        NodeContext context, AssessmentQuestion question, string studentAnswer, IReadOnlyList<AssessmentQuestion> priorQuestionsThisRound)
    {
        var builder = new StringBuilder();
        AppendNodeContext(builder, context);

        if (priorQuestionsThisRound.Count > 0)
        {
            builder.AppendLine("EARLIER QUESTIONS/ANSWERS THIS ROUND (context only, do not re-grade):");
            foreach (var q in priorQuestionsThisRound)
            {
                builder.AppendLine($"- Q: {q.QuestionText}");
                builder.AppendLine($"  A: {q.StudentAnswer}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("QUESTION TO GRADE NOW:");
        builder.AppendLine(question.QuestionText);
        builder.AppendLine();
        builder.AppendLine("STUDENT'S ANSWER:");
        builder.AppendLine(studentAnswer);

        return builder.ToString();
    }

    private static void AppendNodeContext(StringBuilder builder, NodeContext context)
    {
        builder.AppendLine($"CAPABILITY: {context.CapabilityName}");
        builder.AppendLine($"NODE: {context.NodeName}");
        if (!string.IsNullOrWhiteSpace(context.AcademicDefinition))
        {
            builder.AppendLine($"ACADEMIC DEFINITION: {context.AcademicDefinition}");
        }
        if (!string.IsNullOrWhiteSpace(context.Interpretation))
        {
            builder.AppendLine($"INTERPRETATION: {context.Interpretation}");
        }
        if (context.Examples.Count > 0)
        {
            builder.AppendLine($"EXAMPLES: {string.Join(" | ", context.Examples)}");
        }
        if (context.Applications.Count > 0)
        {
            builder.AppendLine($"APPLICATIONS: {string.Join(" | ", context.Applications)}");
        }
        if (context.IllustrationCaptions.Count > 0)
        {
            builder.AppendLine($"ILLUSTRATION(S): {string.Join(" | ", context.IllustrationCaptions)}");
        }
        foreach (var (stepName, evidence) in context.PriorStepEvidence)
        {
            if (string.IsNullOrWhiteSpace(evidence))
            {
                continue;
            }
            builder.AppendLine($"{stepName.ToUpperInvariant()} EVIDENCE (context only): {evidence}");
        }
        builder.AppendLine();
    }
}
