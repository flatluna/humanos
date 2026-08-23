using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HumanOS.AzureFunctions.Api;

public sealed class ExchangeStudioLaunchFunction
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExchangeStudioLaunchFunction> _logger;

    public ExchangeStudioLaunchFunction(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ExchangeStudioLaunchFunction> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [Function("ExchangeStudioLaunch")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "studio/launch/exchange")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var input = await JsonSerializer.DeserializeAsync<LaunchInput>(request.Body, cancellationToken: cancellationToken);
        if (input is null || string.IsNullOrWhiteSpace(input.Code))
        {
            var bad = request.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { code = "MissingCode", error = "A launch code is required." }, cancellationToken);
            return bad;
        }

        var baseUrl = _configuration["NgoBackendBaseUrl"] ?? "http://localhost:7072/api";
        using var outgoing = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/studio-launch/exchange")
        {
            Content = JsonContent.Create(new { code = input.Code.Trim() }),
        };

        try
        {
            using var upstream = await _httpClientFactory.CreateClient().SendAsync(outgoing, cancellationToken);
            var body = await upstream.Content.ReadAsByteArrayAsync(cancellationToken);
            var response = request.CreateResponse(upstream.StatusCode);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.Body.WriteAsync(body, cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not exchange Studio launch code with NGO");
            var error = request.CreateResponse(HttpStatusCode.BadGateway);
            await error.WriteAsJsonAsync(new { code = "NgoUnavailable", error = "The NGO launch service is unavailable." }, cancellationToken);
            return error;
        }
    }

    private sealed record LaunchInput(string? Code);
}
