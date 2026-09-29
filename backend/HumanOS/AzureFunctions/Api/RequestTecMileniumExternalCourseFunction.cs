using System.Net;
using System.Text.Json;
using HumanOS.Data;
using HumanOS.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.AzureFunctions.Api;

/// <summary>
/// POST /people/{personId}/tec-milenium/plan/request-external-course — "Mientras estudias"
/// (2026-09-28). Student selects one or several agent-recommended courses that are NOT in
/// the catalog and sends a SINGLE "Solicitar al Tec" request; this automatically emails the
/// Tec ONE consolidated email (via <see cref="TecMileniumNotificationEmailService"/>/
/// SendGrid) listing every selected course's full content. No enrollment happens here —
/// there is nothing to enroll in yet.
/// </summary>
public sealed class RequestTecMileniumExternalCourseFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HumanOsDbContext _dbContext;
    private readonly TecMileniumNotificationEmailService _emailService;

    public RequestTecMileniumExternalCourseFunction(HumanOsDbContext dbContext, TecMileniumNotificationEmailService emailService)
    {
        _dbContext = dbContext;
        _emailService = emailService;
    }

    public sealed class CourseItem
    {
        public string CourseName { get; set; } = string.Empty;

        public string? Provider { get; set; }

        public string? Reason { get; set; }

        public string? Url { get; set; }
    }

    public sealed class Request
    {
        public List<CourseItem> Courses { get; set; } = [];
    }

    [Function("RequestTecMileniumExternalCourse")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "people/{personId:guid}/tec-milenium/plan/request-external-course")]
        HttpRequestData request,
        Guid personId,
        CancellationToken cancellationToken)
    {
        if (!_emailService.IsConfigured)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.ServiceUnavailable, "NotificationEmailNotConfigured",
                "The Tec notification email is not yet configured (missing SendGrid settings).",
                cancellationToken);
        }

        Request? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<Request>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "InvalidJson", "The request body contains invalid JSON.", cancellationToken);
        }

        var courses = body?.Courses.Where(c => !string.IsNullOrWhiteSpace(c.CourseName)).ToList() ?? [];
        if (courses.Count == 0)
        {
            return await FunctionResponseFactory.ErrorResponseAsync(
                request, HttpStatusCode.BadRequest, "CoursesRequired", "At least one course with a 'courseName' is required.", cancellationToken);
        }

        var profile = await _dbContext.Set<HumanOS.Models.People.PersonProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        var person = await _dbContext.People.AsNoTracking().FirstOrDefaultAsync(p => p.PersonId == personId, cancellationToken);
        var studentName = profile?.DisplayName
            ?? (profile is not null ? string.Join(' ', new[] { profile.FirstName, profile.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))) : null);
        studentName = string.IsNullOrWhiteSpace(studentName) ? (person?.Email ?? "Estudiante") : studentName;

        var requestedCourses = courses
            .Select(c => new TecMileniumNotificationEmailService.RequestedCourse
            {
                Name = c.CourseName,
                Provider = string.IsNullOrWhiteSpace(c.Provider) ? "No especificado" : c.Provider,
                Reason = string.IsNullOrWhiteSpace(c.Reason) ? "No especificado" : c.Reason,
                Url = c.Url,
            })
            .ToList();

        await _emailService.SendExternalCourseRequestsAsync(studentName, requestedCourses, cancellationToken);

        return await FunctionResponseFactory.SuccessResponseAsync(request, new { sent = true, courseCount = requestedCourses.Count }, cancellationToken: cancellationToken);
    }
}
