using System.Net;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// HTTP endpoint for generating the YoungLearner-only "Aprende" Engram Card
/// sequence for one section — see <see cref="StructuredLearningEngramCardService"/>.
/// No-ops (returns an empty Cards array, HTTP 200) for any section whose
/// document is not PedagogicalAudience "YoungLearner" — this endpoint can be
/// safely called for ANY section without risk of altering a "Standard"
/// (enterprise) course's Teaching step.
/// </summary>
public sealed class GenerateStructuredLearningEngramCardsFunction
{
    private readonly StructuredLearningEngramCardService _cardService;
    private readonly ILogger<GenerateStructuredLearningEngramCardsFunction> _logger;

    public GenerateStructuredLearningEngramCardsFunction(
        StructuredLearningEngramCardService cardService,
        ILogger<GenerateStructuredLearningEngramCardsFunction> logger)
    {
        _cardService = cardService;
        _logger = logger;
    }

    [Function("GenerateStructuredLearningEngramCards")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/structured-learning/sections/{sectionId:guid}/engram-cards")]
        HttpRequestData request,
        Guid sectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var cards = await _cardService.GenerateCardsForSectionAsync(sectionId, cancellationToken);

            return await FunctionResponseFactory.SuccessResponseAsync(
                request,
                new GenerateStructuredLearningEngramCardsApiResponse
                {
                    SectionId = sectionId,
                    Cards = cards ?? []
                },
                cancellationToken: cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.NotFound,
                "SectionNotFound",
                ex.Message,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Engram Cards for section {SectionId}", sectionId);
            return await FunctionResponseFactory.ErrorResponseAsync(
                request,
                HttpStatusCode.InternalServerError,
                "EngramCardGenerationFailed",
                ex.Message,
                cancellationToken);
        }
    }
}

/// <summary>Response shape for <see cref="GenerateStructuredLearningEngramCardsFunction"/>.</summary>
public sealed class GenerateStructuredLearningEngramCardsApiResponse
{
    public Guid SectionId { get; set; }

    public List<HumanOS.Models.Capabilities.Studio.EngramCard> Cards { get; set; } = [];
}
