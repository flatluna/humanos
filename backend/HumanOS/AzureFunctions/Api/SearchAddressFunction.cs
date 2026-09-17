using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace HumanOS.AzureFunctions.Api;

public sealed class SearchAddressFunction
{
    private readonly IConfiguration _configuration;

    public SearchAddressFunction(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [Function("SearchAddress")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "addresses/search")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var query = request.Query["query"]?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidQuery", "Enter at least 3 characters.", cancellationToken);
        }

        var key = _configuration["AzureMapsSubscriptionKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "NotConfigured", "Address search is not configured.", cancellationToken);
        }

        var url = "https://atlas.microsoft.com/search/address/json" +
                  $"?api-version=1.0&subscription-key={Uri.EscapeDataString(key)}&query={Uri.EscapeDataString(query)}&limit=5";

        using var client = new HttpClient();
        using var response = await client.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadGateway, "SearchFailed", "Azure Maps address search failed.", cancellationToken);
        }

        using var document = JsonDocument.Parse(body);
        var results = document.RootElement.GetProperty("results").EnumerateArray().Select(result => new
        {
            entityId = result.TryGetProperty("id", out var idValue) ? idValue.GetString() : null,
            address = result.GetProperty("address").GetProperty("freeformAddress").GetString() ?? string.Empty,
            streetName = result.GetProperty("address").TryGetProperty("streetName", out var streetNameValue) ? streetNameValue.GetString() : null,
            streetNumber = result.GetProperty("address").TryGetProperty("streetNumber", out var streetNumberValue) ? streetNumberValue.GetString() : null,
            city = result.GetProperty("address").TryGetProperty("municipality", out var cityValue) ? cityValue.GetString() : null,
            region = result.GetProperty("address").TryGetProperty("countrySubdivision", out var regionValue) ? regionValue.GetString() : null,
            countryCode = result.GetProperty("address").TryGetProperty("countryCode", out var countryValue) ? countryValue.GetString() : null,
            postalCode = result.GetProperty("address").TryGetProperty("postalCode", out var postalValue) ? postalValue.GetString() : null,
            latitude = result.TryGetProperty("position", out var position) && position.TryGetProperty("lat", out var latValue) ? latValue.GetDecimal() : (decimal?)null,
            longitude = result.TryGetProperty("position", out position) && position.TryGetProperty("lon", out var lonValue) ? lonValue.GetDecimal() : (decimal?)null,
        }).ToList();

        return await FunctionResponseFactory.SuccessResponseAsync(request, results, cancellationToken: cancellationToken);
    }
}
