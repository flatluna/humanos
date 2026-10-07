using System.Net;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningAssessmentQuestionDto
{
    public Guid StructuredLearningAssessmentQuestionAttemptId { get; set; }
    public int QuestionIndex { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public List<string>? Options { get; set; }
}

public sealed class StructuredLearningAssessmentRoundStateDto
{
    public Guid StructuredLearningAssessmentRoundId { get; set; }
    public int RoundNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? FinalScore { get; set; }
    public int TotalQuestions { get; set; }
    public DateTime? CompletedDate { get; set; }
    public StructuredLearningAssessmentQuestionDto? CurrentQuestion { get; set; }
}

internal static class StructuredLearningAssessmentApiMappers
{
    public static StructuredLearningAssessmentRoundStateDto ToDto(StructuredLearningAssessmentEngine.RoundState state) => new()
    {
        StructuredLearningAssessmentRoundId = state.StructuredLearningAssessmentRoundId,
        RoundNumber = state.RoundNumber,
        Status = state.Status.ToString(),
        FinalScore = state.FinalScore,
        TotalQuestions = state.TotalQuestions,
        CompletedDate = state.CompletedDate,
        CurrentQuestion = state.CurrentQuestion is null ? null : new StructuredLearningAssessmentQuestionDto
        {
            StructuredLearningAssessmentQuestionAttemptId = state.CurrentQuestion.StructuredLearningAssessmentQuestionAttemptId,
            QuestionIndex = state.CurrentQuestion.QuestionIndex,
            QuestionText = state.CurrentQuestion.QuestionText,
            Options = state.CurrentQuestion.Options
        }
    };
}

public sealed class StartStructuredLearningAssessmentRoundFunction
{
    private readonly StructuredLearningAssessmentEngine _engine;
    private readonly HumanOS.Agents.Studio.StructuredLearningAssessmentAgent _agent;
    private readonly HumanOsDbContext _dbContext;

    public StartStructuredLearningAssessmentRoundFunction(
        StructuredLearningAssessmentEngine engine, HumanOS.Agents.Studio.StructuredLearningAssessmentAgent agent, HumanOsDbContext dbContext)
    {
        _engine = engine;
        _agent = agent;
        _dbContext = dbContext;
    }

    [Function("StartStructuredLearningAssessmentRound")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/assessment/start-round")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "StructuredLearningAssessmentAgentNotConfigured",
                "StructuredLearningAssessmentAgent is not configured (missing Azure OpenAI settings).", cancellationToken);
        }

        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        Guid? personId = Guid.TryParse(query["personId"], out var parsedPersonId) ? parsedPersonId : null;
        Guid? engramSessionId = Guid.TryParse(query["engramSessionId"], out var parsedSessionId) ? parsedSessionId : null;

        try
        {
            var round = await _engine.StartRoundAsync(_dbContext, sectionId, personId, engramSessionId, cancellationToken);
            return await FunctionResponseFactory.SuccessResponseAsync(
                request, StructuredLearningAssessmentApiMappers.ToDto(round), cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "SectionNotFound", ex.Message, cancellationToken);
        }
    }
}
