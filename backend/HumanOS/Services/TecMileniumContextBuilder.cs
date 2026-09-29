using HumanOS.Data;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>Catalog entry resolved back from a numbered list index — either a standalone
/// Capability or a Structured Learning document, each enrolled via a different service.</summary>
public sealed class TecMileniumCatalogEntry
{
    public required string Name { get; init; }

    public required string CatalogType { get; init; } // "Capability" or "StructuredLearningDocument"

    public required Guid CatalogItemId { get; init; }
}

/// <summary>Gathers everything <see cref="HumanOS.Agents.TecMileniumAgent"/> needs for one
/// student: career/semester/grades, "Intereses", Human OS course performance, and the real
/// catalog rendered as a numbered list (so the agent can only reference courses it was
/// actually shown, never invent a GUID). Read-only, degrades gracefully on missing data —
/// mirrors TecContigoContextBuilder's conventions but tailored to plan generation.</summary>
public sealed class TecMileniumContextBuilder
{
    private readonly HumanOsDbContext _dbContext;
    private readonly PersonAcademicPreferencesService _academicPreferencesService;
    private readonly CapabilityEnrollmentService _capabilityEnrollmentService;
    private readonly PersonResumeService _personResumeService;

    public TecMileniumContextBuilder(
        HumanOsDbContext dbContext,
        PersonAcademicPreferencesService academicPreferencesService,
        CapabilityEnrollmentService capabilityEnrollmentService,
        PersonResumeService personResumeService)
    {
        _dbContext = dbContext;
        _academicPreferencesService = academicPreferencesService;
        _capabilityEnrollmentService = capabilityEnrollmentService;
        _personResumeService = personResumeService;
    }

    public async Task<(Agents.TecMileniumPlanRequestContext Context, List<TecMileniumCatalogEntry> CatalogLookup)> BuildAsync(
        Guid personId,
        string? userPrompt,
        string languageCode,
        string? webFindings,
        CancellationToken cancellationToken)
    {
        var context = new Agents.TecMileniumPlanRequestContext
        {
            UserPrompt = userPrompt,
            LanguageCode = languageCode,
            WebFindings = webFindings,
        };

        var resume = await _personResumeService.GetByPersonIdAsync(personId, cancellationToken);
        var academic = resume.StudentAcademicProfile;
        context.StudentName = personId.ToString();
        context.Career = academic?.Degree;
        context.AcademicStage = academic?.AcademicStage;
        context.CumulativeAverage = academic?.CumulativeAverage;

        var currentSemester = academic?.Semesters.FirstOrDefault(s => System.Text.RegularExpressions.Regex.IsMatch(s.Name ?? "", "actual", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        if (currentSemester is not null)
        {
            foreach (var subject in currentSemester.Subjects)
            {
                if (string.IsNullOrWhiteSpace(subject.Name)) continue;
                var grade = subject.Grade is null ? "en curso" : $"{subject.Grade}/{academic?.AverageScale ?? "10"}";
                context.CurrentSemesterSubjects.Add($"{subject.Name}: {grade}");
            }
        }

        try
        {
            var preferences = await _academicPreferencesService.GetAsync(personId, cancellationToken);
            if (preferences is not null)
            {
                context.AcademicInterests = DeserializeStringList(preferences.AcademicInterestsJson);
                context.LearningPreferences = DeserializeStringList(preferences.LearningPreferencesJson);
                context.AcademicMotivations = DeserializeStringList(preferences.AcademicMotivationsJson);
                context.GrowthAreas = DeserializeStringList(preferences.GrowthAreasJson);
            }
        }
        catch
        {
            // Degrade gracefully — plan can still be generated without saved preferences.
        }

        var enrolledCapabilities = await _capabilityEnrollmentService.GetEnrolledCapabilitiesAsync(personId, cancellationToken);
        var enrolledCapabilityIds = enrolledCapabilities.Select(c => c.CapabilityId).ToHashSet();
        foreach (var course in enrolledCapabilities)
        {
            var scoreText = course.AverageScore.HasValue ? $"{course.AverageScore.Value}/100" : "sin calificación aún";
            context.EnrolledCoursePerformance.Add($"\"{course.Name}\" — Estado: {course.Status}; Score: {scoreText}");
        }

        var enrolledDocumentIds = await _dbContext.StructuredLearningDocumentEnrollments
            .AsNoTracking()
            .Where(e => e.PersonId == personId)
            .Select(e => e.StructuredLearningDocumentId)
            .ToListAsync(cancellationToken);
        var enrolledDocumentIdSet = enrolledDocumentIds.ToHashSet();

        var catalogLookup = new List<TecMileniumCatalogEntry>();

        var structuredLearningCourses = await _dbContext.StructuredLearningDocuments
            .AsNoTracking()
            .Where(d => d.IsPublished && d.IsGlobal)
            .OrderBy(d => d.DocumentName)
            .Select(d => new { d.StructuredLearningDocumentId, d.DocumentName, d.ExecutiveSummary })
            .Take(150)
            .ToListAsync(cancellationToken);

        foreach (var course in structuredLearningCourses)
        {
            if (enrolledDocumentIdSet.Contains(course.StructuredLearningDocumentId)) continue;
            catalogLookup.Add(new TecMileniumCatalogEntry
            {
                Name = course.DocumentName,
                CatalogType = "StructuredLearningDocument",
                CatalogItemId = course.StructuredLearningDocumentId,
            });
            context.NumberedCatalog.Add($"{catalogLookup.Count - 1}: {course.DocumentName} — {Truncate(course.ExecutiveSummary, 140)}");
        }

        var capabilities = await _dbContext.Capabilities
            .AsNoTracking()
            .Where(c => c.IsActive && c.OwnerPersonId == null && c.OwnerTenantId == null)
            .OrderBy(c => c.Name)
            .Select(c => new { c.CapabilityId, c.Name, c.Description, c.ExecutiveSummary })
            .Take(150)
            .ToListAsync(cancellationToken);

        foreach (var capability in capabilities)
        {
            if (enrolledCapabilityIds.Contains(capability.CapabilityId)) continue;
            catalogLookup.Add(new TecMileniumCatalogEntry
            {
                Name = capability.Name,
                CatalogType = "Capability",
                CatalogItemId = capability.CapabilityId,
            });
            context.NumberedCatalog.Add($"{catalogLookup.Count - 1}: {capability.Name} — {Truncate(capability.ExecutiveSummary ?? capability.Description, 140)}");
        }

        return (context, catalogLookup);
    }

    private static List<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(sin descripción)";
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd() + "...";
    }
}
