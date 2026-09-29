using System.Net;
using System.Text.Json;
using HumanOS.Agents;
using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /people/{personId}/tec-milenium/plan — "Mientras estudias" learning-experience
/// design generator (2026-09-28). Gathers the student's career/semester/grades,
/// "Intereses", Human OS course performance, and the real catalog, optionally searches
/// the live internet for industry/job-market context via <see cref="WebGroundingService"/>,
/// then asks <see cref="TecMileniumAgent"/> to design the plan. Returns each catalog
/// suggestion with its real CapabilityId/StructuredLearningDocumentId already resolved
/// (never the raw model-picked index) so the frontend can call the accept endpoints
/// directly.
/// </summary>
public sealed class GenerateTecMileniumPlanFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HumanOsDbContext _dbContext;
    private readonly TecMileniumContextBuilder _contextBuilder;
    private readonly WebGroundingService _webGrounding;
    private readonly TecMileniumAgent _agent;

    public GenerateTecMileniumPlanFunction(
        HumanOsDbContext dbContext,
        TecMileniumContextBuilder contextBuilder,
        WebGroundingService webGrounding,
        TecMileniumAgent agent)
    {
        _dbContext = dbContext;
        _contextBuilder = contextBuilder;
        _webGrounding = webGrounding;
        _agent = agent;
    }

    public sealed class Request
    {
        public string? Prompt { get; set; }

        public string? LanguageCode { get; set; }
    }

    public sealed class CatalogCourseResult
    {
        public Guid CatalogItemId { get; set; }

        public string CatalogType { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }

    public sealed class PlanResult
    {
        public string Title { get; set; } = string.Empty;

        public string OverallSummary { get; set; } = string.Empty;

        public List<CatalogCourseResult> CatalogCourses { get; set; } = [];

        public List<TecMileniumExternalCourseSuggestion> ExternalCourses { get; set; } = [];
    }

    [Function("GenerateTecMileniumPlan")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/tec-milenium/plan")]
        HttpRequestData request,
        Guid personId,
        FunctionContext executionContext,
        CancellationToken cancellationToken)
    {
        var logger = executionContext.GetLogger<GenerateTecMileniumPlanFunction>();

        if (!_agent.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "TecMileniumAgentNotConfigured",
                "The Mientras estudias plan agent is not yet configured (missing Azure OpenAI settings).",
                cancellationToken);
        }

        var person = await _dbContext.People.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        if (person is null)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.NotFound, "PersonNotFound", "This person was not found.", cancellationToken);
        }

        Request? body = null;
        if (request.Body is { Length: > 0 })
        {
            try
            {
                body = await JsonSerializer.DeserializeAsync<Request>(request.Body, JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return await FunctionResponseFactory.ErrorResponseAsync(
                    request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
            }
        }

        var languageCode = string.IsNullOrWhiteSpace(body?.LanguageCode) ? "es" : body!.LanguageCode!;

        var profile = await _dbContext.Set<HumanOS.Models.People.PersonProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        var studentName = profile?.DisplayName
            ?? (profile is not null ? string.Join(' ', new[] { profile.FirstName, profile.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) : null);
        studentName = string.IsNullOrWhiteSpace(studentName) ? (person.Email ?? "Estudiante") : studentName;

        string? webFindings = null;
        if (_webGrounding.IsConfigured)
        {
            try
            {
                var topic = string.IsNullOrWhiteSpace(body?.Prompt)
                    ? "cursos, certificaciones y tendencias de la industria relevantes para el semestre actual y la preparación laboral"
                    : body!.Prompt!;
                var groundingResult = await _webGrounding.SearchAsync(topic, "Estudiante próximo a egresar", cancellationToken);
                webFindings = groundingResult.Text;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "GenerateTecMileniumPlan: web grounding search failed, continuing without internet findings.");
            }
        }

        var (context, catalogLookup) = await _contextBuilder.BuildAsync(personId, body?.Prompt, languageCode, webFindings, cancellationToken);
        context.StudentName = studentName;

        var proposal = await _agent.GenerateAsync(context, cancellationToken);

        var resolvedCatalogCourses = new List<CatalogCourseResult>();
        foreach (var suggestion in proposal.CatalogCourses)
        {
            if (suggestion.CatalogIndex < 0 || suggestion.CatalogIndex >= catalogLookup.Count) continue;
            var entry = catalogLookup[suggestion.CatalogIndex];
            resolvedCatalogCourses.Add(new CatalogCourseResult
            {
                CatalogItemId = entry.CatalogItemId,
                CatalogType = entry.CatalogType,
                Name = entry.Name,
                Reason = suggestion.Reason,
            });
        }

        var result = new PlanResult
        {
            Title = proposal.Title,
            OverallSummary = proposal.OverallSummary,
            CatalogCourses = resolvedCatalogCourses,
            ExternalCourses = proposal.ExternalCourses,
        };

        return await FunctionResponseFactory.SuccessResponseAsync(request, result, cancellationToken: cancellationToken);
    }
}
