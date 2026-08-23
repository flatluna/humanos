using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// Bootstrap-safe GET for a student's previously saved Hypothesis
/// pre-check answers (2026-08-13) — never grades anything, safe on every
/// page mount/reload. See <see cref="StructuredLearningHypothesisService"/>.
/// </summary>
public sealed class GetStructuredLearningHypothesisResponsesFunction
{
    private readonly StructuredLearningHypothesisService _service;
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningHypothesisResponsesFunction(StructuredLearningHypothesisService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningHypothesisResponses")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/hypothesis/responses")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        Guid? personId = Guid.TryParse(query["personId"], out var parsedPersonId) ? parsedPersonId : null;

        var results = await _service.GetAsync(_dbContext, sectionId, personId, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(
            request,
            results.Select(r => new HypothesisResponseDto
            {
                QuestionIndex = r.QuestionIndex,
                SelectedOptionIndex = r.SelectedOptionIndex,
                IsCorrect = r.IsCorrect
            }).ToList(),
            cancellationToken: cancellationToken);
    }
}
