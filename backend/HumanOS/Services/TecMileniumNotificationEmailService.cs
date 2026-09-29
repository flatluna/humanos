using System.Net;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace HumanOS.Services;

/// <summary>
/// "Mientras estudias" (Tec Contigo, 2026-09-28) — notifies a fixed Tec administration
/// mailbox whenever a student accepts an agent-recommended course that the Tec does NOT
/// yet offer, so demand for course development is visible. Same SendGrid pattern used by
/// NgoAutomateApp's VolunteerApplicationEmailService, ported here with its own settings
/// (this app previously had no SendGrid usage at all). Best-effort: never throws to the
/// caller when unconfigured, only when SendGrid itself rejects the send.
/// </summary>
public sealed class TecMileniumNotificationEmailService
{
    private readonly IConfiguration _configuration;

    public TecMileniumNotificationEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string? ApiKey => _configuration["SendGridApiKey"];

    private string? SenderEmail => _configuration["SendGridSenderEmail"];

    private string? NotificationRecipient => _configuration["TecMileniumNotificationEmail"];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(SenderEmail)
        && !string.IsNullOrWhiteSpace(NotificationRecipient);

    public sealed class RequestedCourse
    {
        public required string Name { get; init; }

        public required string Provider { get; init; }

        public required string Reason { get; init; }

        public string? Url { get; init; }
    }

    /// <summary>Sends ONE consolidated email listing every course the student selected in
    /// a single "Solicitar al Tec" action — never one email per course.</summary>
    public async Task SendExternalCourseRequestsAsync(
        string studentName,
        IReadOnlyList<RequestedCourse> courses,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured || courses.Count == 0) return;

        var subject = courses.Count == 1
            ? $"Solicitud de curso — {courses[0].Name}"
            : $"Solicitud de {courses.Count} cursos — {studentName}";

        var plainTextCourses = string.Join(
            "\n\n",
            courses.Select((course, index) =>
                $"{index + 1}. Curso: {course.Name}\nFuente sugerida: {course.Provider}\nMotivo: {course.Reason}" +
                (string.IsNullOrWhiteSpace(course.Url) ? "" : $"\nURL: {course.Url}")));
        var plainText =
            $"El estudiante {studentName} solicitó que el Tec desarrolle {courses.Count} curso(s) recomendado(s) " +
            $"por el agente de 'Mientras estudias' que aún no existen en el catálogo.\n\n{plainTextCourses}";

        var htmlCourses = string.Join(
            "",
            courses.Select(course =>
                $"<li style=\"margin-bottom:12px;\"><strong>{WebUtility.HtmlEncode(course.Name)}</strong><br/>" +
                $"Fuente sugerida: {WebUtility.HtmlEncode(course.Provider)}<br/>" +
                $"Motivo: {WebUtility.HtmlEncode(course.Reason)}" +
                (string.IsNullOrWhiteSpace(course.Url) ? "" : $"<br/>URL: {WebUtility.HtmlEncode(course.Url)}") +
                "</li>"));
        var html =
            $"<div style=\"font-family:Arial,sans-serif;color:#172033;\">" +
            $"<p>El estudiante <strong>{WebUtility.HtmlEncode(studentName)}</strong> solicitó que el Tec " +
            $"desarrolle los siguientes curso(s) recomendados por el agente de <strong>Mientras estudias</strong> " +
            $"que aún no existen en el catálogo:</p><ul>{htmlCourses}</ul></div>";

        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(SenderEmail, "Tec Contigo"),
            new EmailAddress(NotificationRecipient),
            subject,
            plainText,
            html);

        var response = await new SendGridClient(ApiKey).SendEmailAsync(message, cancellationToken);
        if ((int)response.StatusCode >= 400)
        {
            throw new InvalidOperationException($"SendGrid rejected the email: {response.StatusCode}.");
        }
    }
}
