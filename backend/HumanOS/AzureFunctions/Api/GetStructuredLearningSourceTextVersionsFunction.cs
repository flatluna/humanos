using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace HumanOS.AzureFunctions.Api;

public sealed class StructuredLearningSourceTextVersionDto
{
    public int VersionNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Instruction { get; set; }
    public DateTime CreatedDate { get; set; }
}

/// <summary>Read-only version history for a section's source text — see
/// <see cref="StructuredLearningReviewService.GetSourceTextVersionsAsync"/>.</summary>
public sealed class GetStructuredLearningSourceTextVersionsFunction
{
    private readonly StructuredLearningReviewService _service;
    private readonly HumanOsDbContext _dbContext;

    public GetStructuredLearningSourceTextVersionsFunction(StructuredLearningReviewService service, HumanOsDbContext dbContext)
    {
        _service = service;
        _dbContext = dbContext;
    }

    [Function("GetStructuredLearningSourceTextVersions")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "studio/structured-learning/sections/{sectionId:guid}/source-text/versions")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        var versions = await _service.GetSourceTextVersionsAsync(_dbContext, sectionId, cancellationToken);
        return await FunctionResponseFactory.SuccessResponseAsync(request, versions.Select(v => new StructuredLearningSourceTextVersionDto
        {
            VersionNumber = v.VersionNumber,
            Text = v.Text,
            Instruction = v.Instruction,
            CreatedDate = v.CreatedDate
        }).ToList(), cancellationToken: cancellationToken);
    }
}
