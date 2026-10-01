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

    /// <summary>Only requires the SendGrid API key + sender (not the fixed
    /// notification mailbox) — used when emailing an arbitrary recipient
    /// like the student themselves.</summary>
    public bool IsConfiguredForArbitraryRecipient =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SenderEmail);

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

    public sealed class RecommendedItem
    {
        public required string Title { get; init; }

        public required string Type { get; init; }

        public required string Reason { get; init; }

        public string? Url { get; init; }
    }

    /// <summary>"Recomienda contenido relevante" (2026-09-29) — sends the student their own
    /// generated content-recommendation report, in a plain-text + HTML format they can save
    /// (e.g. as a PDF from their email client). Sent to the student's OWN email, not the
    /// fixed Tec notification mailbox.</summary>
    public async Task SendContentRecommendationsAsync(
        string studentEmail,
        string studentName,
        string title,
        string summary,
        string trendsSummary,
        IReadOnlyList<RecommendedItem> items,
        CancellationToken cancellationToken)
    {
        if (!IsConfiguredForArbitraryRecipient || string.IsNullOrWhiteSpace(studentEmail)) return;

        var subject = $"Tus recomendaciones de contenido — {title}";

        var plainTextItems = string.Join(
            "\n\n",
            items.Select((item, index) =>
                $"{index + 1}. [{item.Type}] {item.Title}\n{item.Reason}" +
                (string.IsNullOrWhiteSpace(item.Url) ? "" : $"\nURL: {item.Url}")));
        var plainText =
            $"Hola {studentName},\n\n{summary}\n\nTendencias del área:\n{trendsSummary}\n\n" +
            $"Contenido recomendado para ti:\n\n{plainTextItems}";

        var htmlItems = string.Join(
            "",
            items.Select(item =>
                $"<li style=\"margin-bottom:12px;\"><span style=\"color:#6b7280;font-size:12px;\">{WebUtility.HtmlEncode(item.Type)}</span><br/>" +
                $"<strong>{WebUtility.HtmlEncode(item.Title)}</strong><br/>" +
                $"{WebUtility.HtmlEncode(item.Reason)}" +
                (string.IsNullOrWhiteSpace(item.Url) ? "" : $"<br/><a href=\"{WebUtility.HtmlEncode(item.Url)}\">{WebUtility.HtmlEncode(item.Url)}</a>") +
                "</li>"));
        var html =
            $"<div style=\"font-family:Arial,sans-serif;color:#172033;\">" +
            $"<h2>{WebUtility.HtmlEncode(title)}</h2>" +
            $"<p>Hola {WebUtility.HtmlEncode(studentName)},</p>" +
            $"<p>{WebUtility.HtmlEncode(summary)}</p>" +
            $"<p><strong>Tendencias del área:</strong> {WebUtility.HtmlEncode(trendsSummary)}</p>" +
            $"<ul>{htmlItems}</ul></div>";

        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(SenderEmail, "Tec Contigo"),
            new EmailAddress(studentEmail, studentName),
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
