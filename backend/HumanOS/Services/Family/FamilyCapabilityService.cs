using System.Data;
using System.Text.Json;
using HumanOS.Contracts.Family;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HumanOS.Services.Family;

/// <summary>
/// Service managing the Family Global and Custom Capabilities and AI-generated course content.
/// Operates on the isolated family.* schema (100% additive, 0 impact on existing corporate tables).
/// </summary>
public sealed class FamilyCapabilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string? _connectionString;
    private readonly ILogger<FamilyCapabilityService> _logger;

    // In-memory fallback repository for local resilience and rapid testing
    private static readonly List<FamilyCapabilityResponse> InMemoryCapabilities = [];
    private static readonly List<SaveCapabilityLearningContentRequest> InMemoryContents = [];
    private static readonly Dictionary<string, SaveAcademicCourseDocumentRequest> InMemoryAcademicCourses = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object SyncLock = new();

    public FamilyCapabilityService(IConfiguration configuration, ILogger<FamilyCapabilityService> logger)
    {
        _connectionString = configuration.GetConnectionString("FamilyDatabase") 
                            ?? configuration["FamilyDatabase"]
                            ?? configuration.GetConnectionString("HumanOSDatabase")
                            ?? configuration["HumanOSDatabase"];
        _logger = logger;
    }

    /// <summary>
    /// Retrieves the global catalog of human development stages (grade levels / life stages),
    /// ordered by DisplayOrder. These are shared across all tenants (no FamilyId on this table).
    /// </summary>
    public async Task<List<HumanDevelopmentStageResponse>> ListStagesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<HumanDevelopmentStageResponse>();

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return results;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT StageId, StageKey, StageNameEs, StageNameEn, AgeRange, CategoryGroup, DisplayOrder, DescriptionEs, DescriptionEn
                FROM family.HumanDevelopmentStages
                ORDER BY DisplayOrder ASC;";

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new HumanDevelopmentStageResponse
                {
                    StageId = reader.GetGuid(0),
                    StageKey = reader.GetString(1),
                    StageNameEs = reader.GetString(2),
                    StageNameEn = reader.GetString(3),
                    AgeRange = reader.GetString(4),
                    CategoryGroup = reader.GetString(5),
                    DisplayOrder = reader.GetInt32(6),
                    DescriptionEs = reader.IsDBNull(7) ? null : reader.GetString(7),
                    DescriptionEn = reader.IsDBNull(8) ? null : reader.GetString(8),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch human development stages from SQL database.");
        }

        return results;
    }

    /// <summary>
    /// Retrieves all capabilities for a given stage (e.g. 'kindergarten'),
    /// combining global reference capabilities (FamilyId IS NULL) and custom ones for the family.
    /// </summary>
    public async Task<List<FamilyCapabilityResponse>> GetStageCapabilitiesAsync(
        string stageKey,
        Guid? familyId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FamilyCapabilityResponse>();

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT 
                        c.CapabilityId,
                        c.Code,
                        s.StageKey,
                        c.NameEs,
                        c.NameEn,
                        c.Domain,
                        c.DescriptionEs,
                        c.DescriptionEn,
                        c.WhyItMattersEs,
                        c.WhyItMattersEn,
                        r.TargetMasteryLevel,
                        r.RecommendedHoursToMaster,
                        c.SkillsCoveredJson,
                        c.FamilyId,
                        c.CreatedDate
                    FROM family.HumanCapabilities c
                    INNER JOIN family.StageCapabilityRequirements r ON c.CapabilityId = r.CapabilityId
                    INNER JOIN family.HumanDevelopmentStages s ON r.StageId = s.StageId
                    WHERE s.StageKey = @StageKey
                      AND (c.FamilyId IS NULL OR c.FamilyId = @FamilyId)
                    ORDER BY c.Code ASC;";

                await using var command = new SqlCommand(sql, connection);
                command.Parameters.Add(new SqlParameter("@StageKey", SqlDbType.NVarChar, 50) { Value = stageKey });
                command.Parameters.Add(new SqlParameter("@FamilyId", SqlDbType.UniqueIdentifier) { Value = (object?)familyId ?? DBNull.Value });

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var skillsJson = reader.IsDBNull(12) ? null : reader.GetString(12);
                    var skillsList = !string.IsNullOrWhiteSpace(skillsJson)
                        ? JsonSerializer.Deserialize<List<string>>(skillsJson, JsonOptions) ?? []
                        : [];

                    results.Add(new FamilyCapabilityResponse
                    {
                        CapabilityId = reader.GetGuid(0),
                        Code = reader.GetString(1),
                        StageKey = reader.GetString(2),
                        NameEs = reader.GetString(3),
                        NameEn = reader.GetString(4),
                        Domain = reader.GetString(5),
                        DescriptionEs = reader.GetString(6),
                        DescriptionEn = reader.GetString(7),
                        WhyItMattersEs = reader.IsDBNull(8) ? null : reader.GetString(8),
                        WhyItMattersEn = reader.IsDBNull(9) ? null : reader.GetString(9),
                        TargetMasteryLevel = reader.GetString(10),
                        RecommendedHoursToMaster = reader.GetInt32(11),
                        SkillsCovered = skillsList,
                        IsGlobal = reader.IsDBNull(13),
                        FamilyId = reader.IsDBNull(13) ? null : reader.GetGuid(13),
                        CreatedDate = reader.GetDateTime(14)
                    });
                }

                if (results.Count > 0)
                {
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch capabilities from SQL database; falling back to in-memory store.");
            }
        }

        // Fallback / In-Memory
        lock (SyncLock)
        {
            var memoryItems = InMemoryCapabilities
                .Where(c => c.StageKey.Equals(stageKey, StringComparison.OrdinalIgnoreCase)
                            && (c.FamilyId == null || c.FamilyId == familyId))
                .ToList();

            return Task.FromResult(memoryItems).Result;
        }
    }

    /// <summary>
    /// Saves or updates a capability (used by the AI Agent and the Admin portal).
    /// </summary>
    public async Task<FamilyCapabilityResponse> SaveCapabilityAsync(
        SaveFamilyCapabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var capabilityId = Guid.NewGuid();
        var skillsJson = JsonSerializer.Serialize(request.SkillsCovered, JsonOptions);

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                // 1. Ensure stage exists
                const string stageSql = "SELECT StageId FROM family.HumanDevelopmentStages WHERE StageKey = @StageKey;";
                await using var stageCmd = new SqlCommand(stageSql, connection);
                stageCmd.Parameters.Add(new SqlParameter("@StageKey", SqlDbType.NVarChar, 50) { Value = request.StageKey });
                var stageIdObj = await stageCmd.ExecuteScalarAsync(cancellationToken);

                Guid stageId;
                if (stageIdObj != null && stageIdObj != DBNull.Value)
                {
                    stageId = (Guid)stageIdObj;
                }
                else
                {
                    stageId = Guid.NewGuid();
                    const string insertStageSql = @"
                        INSERT INTO family.HumanDevelopmentStages (StageId, StageKey, StageNameEs, StageNameEn, AgeRange, CategoryGroup, DisplayOrder)
                        VALUES (@StageId, @StageKey, @StageKey, @StageKey, 'K-12', 'General', 1);";
                    await using var insertStageCmd = new SqlCommand(insertStageSql, connection);
                    insertStageCmd.Parameters.Add(new SqlParameter("@StageId", stageId));
                    insertStageCmd.Parameters.Add(new SqlParameter("@StageKey", request.StageKey));
                    await insertStageCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // 2. Insert or update Capability
                const string capSql = @"
                    MERGE family.HumanCapabilities AS target
                    USING (SELECT @Code AS Code) AS source
                    ON (target.Code = source.Code)
                    WHEN MATCHED THEN
                        UPDATE SET 
                            NameEs = @NameEs,
                            NameEn = @NameEn,
                            Domain = @Domain,
                            DescriptionEs = @DescriptionEs,
                            DescriptionEn = @DescriptionEn,
                            WhyItMattersEs = @WhyItMattersEs,
                            WhyItMattersEn = @WhyItMattersEn,
                            SkillsCoveredJson = @SkillsCoveredJson,
                            FamilyId = @FamilyId
                    WHEN NOT MATCHED THEN
                        INSERT (CapabilityId, Code, NameEs, NameEn, Domain, DescriptionEs, DescriptionEn, WhyItMattersEs, WhyItMattersEn, SkillsCoveredJson, FamilyId)
                        VALUES (@CapabilityId, @Code, @NameEs, @NameEn, @Domain, @DescriptionEs, @DescriptionEn, @WhyItMattersEs, @WhyItMattersEn, @SkillsCoveredJson, @FamilyId)
                    OUTPUT inserted.CapabilityId;";

                await using var capCmd = new SqlCommand(capSql, connection);
                capCmd.Parameters.Add(new SqlParameter("@CapabilityId", capabilityId));
                capCmd.Parameters.Add(new SqlParameter("@Code", request.Code));
                capCmd.Parameters.Add(new SqlParameter("@NameEs", request.NameEs));
                capCmd.Parameters.Add(new SqlParameter("@NameEn", request.NameEn));
                capCmd.Parameters.Add(new SqlParameter("@Domain", request.Domain));
                capCmd.Parameters.Add(new SqlParameter("@DescriptionEs", request.DescriptionEs));
                capCmd.Parameters.Add(new SqlParameter("@DescriptionEn", request.DescriptionEn));
                capCmd.Parameters.Add(new SqlParameter("@WhyItMattersEs", (object?)request.WhyItMattersEs ?? DBNull.Value));
                capCmd.Parameters.Add(new SqlParameter("@WhyItMattersEn", (object?)request.WhyItMattersEn ?? DBNull.Value));
                capCmd.Parameters.Add(new SqlParameter("@SkillsCoveredJson", skillsJson));
                capCmd.Parameters.Add(new SqlParameter("@FamilyId", (object?)request.FamilyId ?? DBNull.Value));

                var actualCapId = (Guid)(await capCmd.ExecuteScalarAsync(cancellationToken) ?? capabilityId);

                // 3. Insert or update Requirement
                const string reqSql = @"
                    MERGE family.StageCapabilityRequirements AS target
                    USING (SELECT @StageId AS StageId, @CapabilityId AS CapabilityId) AS source
                    ON (target.StageId = source.StageId AND target.CapabilityId = source.CapabilityId)
                    WHEN MATCHED THEN
                        UPDATE SET TargetMasteryLevel = @Mastery, RecommendedHoursToMaster = @Hours, IsMandatoryForNextStage = @Mandatory
                    WHEN NOT MATCHED THEN
                        INSERT (RequirementId, StageId, CapabilityId, TargetMasteryLevel, RecommendedHoursToMaster, IsMandatoryForNextStage)
                        VALUES (NEWID(), @StageId, @CapabilityId, @Mastery, @Hours, @Mandatory);";

                await using var reqCmd = new SqlCommand(reqSql, connection);
                reqCmd.Parameters.Add(new SqlParameter("@StageId", stageId));
                reqCmd.Parameters.Add(new SqlParameter("@CapabilityId", actualCapId));
                reqCmd.Parameters.Add(new SqlParameter("@Mastery", request.TargetMasteryLevel));
                reqCmd.Parameters.Add(new SqlParameter("@Hours", request.RecommendedHoursToMaster));
                reqCmd.Parameters.Add(new SqlParameter("@Mandatory", request.IsMandatoryForNextStage));
                await reqCmd.ExecuteNonQueryAsync(cancellationToken);

                capabilityId = actualCapId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist capability to SQL; caching in memory.");
            }
        }

        var response = new FamilyCapabilityResponse
        {
            CapabilityId = capabilityId,
            Code = request.Code,
            StageKey = request.StageKey,
            NameEs = request.NameEs,
            NameEn = request.NameEn,
            Domain = request.Domain,
            DescriptionEs = request.DescriptionEs,
            DescriptionEn = request.DescriptionEn,
            WhyItMattersEs = request.WhyItMattersEs,
            WhyItMattersEn = request.WhyItMattersEn,
            TargetMasteryLevel = request.TargetMasteryLevel,
            RecommendedHoursToMaster = request.RecommendedHoursToMaster,
            SkillsCovered = request.SkillsCovered,
            IsGlobal = request.FamilyId == null,
            FamilyId = request.FamilyId,
            CreatedDate = DateTime.UtcNow
        };

        lock (SyncLock)
        {
            InMemoryCapabilities.RemoveAll(c => c.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase));
            InMemoryCapabilities.Add(response);
        }

        return response;
    }

    /// <summary>
    /// Saves the pedagogical learning content generated by the AI agent for a specific capability.
    /// </summary>
    public async Task<Guid> SaveLearningContentAsync(
        SaveCapabilityLearningContentRequest request,
        CancellationToken cancellationToken = default)
    {
        var contentId = Guid.NewGuid();
        var homeActivitiesJson = JsonSerializer.Serialize(request.HomeActivities, JsonOptions);
        var socraticJson = JsonSerializer.Serialize(request.SocraticQuestions, JsonOptions);

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    INSERT INTO family.CapabilityLearningContent (
                        ContentId, CapabilityId, FamilyId, ModuleTitle,
                        PedagogicalGoal, KidExplanationStory, ParentGuide,
                        HomeActivitiesJson, SocraticQuestionsJson, MasteryRubricJson, IsPublished
                    ) VALUES (
                        @ContentId, @CapabilityId, @FamilyId, @ModuleTitle,
                        @PedagogicalGoal, @KidExplanationStory, @ParentGuide,
                        @HomeActivitiesJson, @SocraticQuestionsJson, @MasteryRubricJson, @IsPublished
                    );";

                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@ContentId", contentId));
                cmd.Parameters.Add(new SqlParameter("@CapabilityId", request.CapabilityId));
                cmd.Parameters.Add(new SqlParameter("@FamilyId", (object?)request.FamilyId ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ModuleTitle", request.ModuleTitle));
                cmd.Parameters.Add(new SqlParameter("@PedagogicalGoal", request.PedagogicalGoal));
                cmd.Parameters.Add(new SqlParameter("@KidExplanationStory", (object?)request.KidExplanationStory ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ParentGuide", (object?)request.ParentGuide ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@HomeActivitiesJson", homeActivitiesJson));
                cmd.Parameters.Add(new SqlParameter("@SocraticQuestionsJson", socraticJson));
                cmd.Parameters.Add(new SqlParameter("@MasteryRubricJson", (object?)request.MasteryRubric ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@IsPublished", request.IsPublished));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist course learning content to SQL; storing in memory.");
            }
        }

        lock (SyncLock)
        {
            InMemoryContents.Add(request);
        }

        return contentId;
    }

    /// <summary>
    /// Persists (upsert, keyed by CourseCode) the complete academic course document authored
    /// by the ProfesorExpert AI agent — this is what makes "Aprobar y Guardar Curso en Base de
    /// Datos" in the Academic Course Generator UI actually durable instead of a no-op toast.
    /// </summary>
    public async Task<SaveAcademicCourseDocumentResponse> SaveAcademicCourseDocumentAsync(
        SaveAcademicCourseDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var documentId = Guid.NewGuid();
        var updatedDate = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    MERGE family.AcademicCourseDocument AS target
                    USING (SELECT @CourseCode AS CourseCode) AS source
                    ON target.CourseCode = source.CourseCode
                    WHEN MATCHED THEN
                        UPDATE SET
                            TitleEs = @TitleEs,
                            TitleEn = @TitleEn,
                            Subject = @Subject,
                            GradeLevel = @GradeLevel,
                            Language = @Language,
                            EstimatedHours = @EstimatedHours,
                            CourseDocumentMarkdown = @CourseDocumentMarkdown,
                            TeacherIntroduction = @TeacherIntroduction,
                            PedagogicalApproach = @PedagogicalApproach,
                            ArchitectureSummaryJson = @ArchitectureSummaryJson,
                            IsApprovedByEducator = @IsApprovedByEducator,
                            EducatorReviewNotes = @EducatorReviewNotes,
                            UpdatedDate = @UpdatedDate
                    WHEN NOT MATCHED THEN
                        INSERT (
                            DocumentId, CourseCode, TitleEs, TitleEn, Subject, GradeLevel, Language,
                            EstimatedHours, CourseDocumentMarkdown, TeacherIntroduction, PedagogicalApproach,
                            ArchitectureSummaryJson, IsApprovedByEducator, EducatorReviewNotes, CreatedDate, UpdatedDate
                        )
                        VALUES (
                            @DocumentId, @CourseCode, @TitleEs, @TitleEn, @Subject, @GradeLevel, @Language,
                            @EstimatedHours, @CourseDocumentMarkdown, @TeacherIntroduction, @PedagogicalApproach,
                            @ArchitectureSummaryJson, @IsApprovedByEducator, @EducatorReviewNotes, @UpdatedDate, @UpdatedDate
                        )
                    OUTPUT inserted.DocumentId;";

                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@DocumentId", documentId));
                cmd.Parameters.Add(new SqlParameter("@CourseCode", request.CourseCode));
                cmd.Parameters.Add(new SqlParameter("@TitleEs", request.TitleEs));
                cmd.Parameters.Add(new SqlParameter("@TitleEn", request.TitleEn));
                cmd.Parameters.Add(new SqlParameter("@Subject", request.Subject));
                cmd.Parameters.Add(new SqlParameter("@GradeLevel", request.GradeLevel));
                cmd.Parameters.Add(new SqlParameter("@Language", request.Language));
                cmd.Parameters.Add(new SqlParameter("@EstimatedHours", request.EstimatedHours));
                cmd.Parameters.Add(new SqlParameter("@CourseDocumentMarkdown", request.CourseDocumentMarkdown));
                cmd.Parameters.Add(new SqlParameter("@TeacherIntroduction", (object?)request.TeacherIntroduction ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@PedagogicalApproach", (object?)request.PedagogicalApproach ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@ArchitectureSummaryJson", (object?)request.ArchitectureSummaryJson ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@IsApprovedByEducator", request.IsApprovedByEducator));
                cmd.Parameters.Add(new SqlParameter("@EducatorReviewNotes", (object?)request.EducatorReviewNotes ?? DBNull.Value));
                cmd.Parameters.Add(new SqlParameter("@UpdatedDate", updatedDate));

                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                var savedId = result is Guid g ? g : documentId;

                lock (SyncLock)
                {
                    InMemoryAcademicCourses[request.CourseCode] = request;
                }

                return new SaveAcademicCourseDocumentResponse
                {
                    DocumentId = savedId,
                    CourseCode = request.CourseCode,
                    UpdatedDate = updatedDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist academic course document to SQL; storing in memory only.");
            }
        }

        lock (SyncLock)
        {
            InMemoryAcademicCourses[request.CourseCode] = request;
        }

        return new SaveAcademicCourseDocumentResponse
        {
            DocumentId = documentId,
            CourseCode = request.CourseCode,
            UpdatedDate = updatedDate
        };
    }

    /// <summary>Retrieves the previously saved academic course document for a given CourseCode, if any.</summary>
    public async Task<AcademicCourseDocumentResponse?> GetAcademicCourseDocumentAsync(
        string courseCode,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT DocumentId, CourseCode, TitleEs, TitleEn, Subject, GradeLevel, Language,
                           EstimatedHours, CourseDocumentMarkdown, TeacherIntroduction, PedagogicalApproach,
                           ArchitectureSummaryJson, IsApprovedByEducator, EducatorReviewNotes, CreatedDate, UpdatedDate
                    FROM family.AcademicCourseDocument
                    WHERE CourseCode = @CourseCode;";

                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@CourseCode", SqlDbType.NVarChar, 50) { Value = courseCode });

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return new AcademicCourseDocumentResponse
                    {
                        DocumentId = reader.GetGuid(0),
                        CourseCode = reader.GetString(1),
                        TitleEs = reader.GetString(2),
                        TitleEn = reader.GetString(3),
                        Subject = reader.GetString(4),
                        GradeLevel = reader.GetString(5),
                        Language = reader.GetString(6),
                        EstimatedHours = reader.GetInt32(7),
                        CourseDocumentMarkdown = reader.GetString(8),
                        TeacherIntroduction = reader.IsDBNull(9) ? null : reader.GetString(9),
                        PedagogicalApproach = reader.IsDBNull(10) ? null : reader.GetString(10),
                        ArchitectureSummaryJson = reader.IsDBNull(11) ? null : reader.GetString(11),
                        IsApprovedByEducator = reader.GetBoolean(12),
                        EducatorReviewNotes = reader.IsDBNull(13) ? null : reader.GetString(13),
                        CreatedDate = reader.GetDateTime(14),
                        UpdatedDate = reader.GetDateTime(15)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch academic course document from SQL; falling back to in-memory store.");
            }
        }

        lock (SyncLock)
        {
            if (InMemoryAcademicCourses.TryGetValue(courseCode, out var memoryRequest))
            {
                return new AcademicCourseDocumentResponse
                {
                    DocumentId = Guid.Empty,
                    CourseCode = memoryRequest.CourseCode,
                    TitleEs = memoryRequest.TitleEs,
                    TitleEn = memoryRequest.TitleEn,
                    Subject = memoryRequest.Subject,
                    GradeLevel = memoryRequest.GradeLevel,
                    Language = memoryRequest.Language,
                    EstimatedHours = memoryRequest.EstimatedHours,
                    CourseDocumentMarkdown = memoryRequest.CourseDocumentMarkdown,
                    TeacherIntroduction = memoryRequest.TeacherIntroduction,
                    PedagogicalApproach = memoryRequest.PedagogicalApproach,
                    ArchitectureSummaryJson = memoryRequest.ArchitectureSummaryJson,
                    IsApprovedByEducator = memoryRequest.IsApprovedByEducator,
                    EducatorReviewNotes = memoryRequest.EducatorReviewNotes,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };
            }
        }

        return null;
    }

    /// <summary>Lightweight listing of every saved academic course document (2026-10-08) —
    /// powers the "Catálogo Académico" sidebar page, deliberately omitting the full
    /// CourseDocumentMarkdown (can be 100K+ chars) to keep the list payload small.</summary>
    public async Task<List<AcademicCourseDocumentSummaryResponse>> ListAcademicCourseDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<AcademicCourseDocumentSummaryResponse>();

        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT CourseCode, TitleEs, TitleEn, Subject, GradeLevel, Language,
                           EstimatedHours, CAST(LEN(CourseDocumentMarkdown) AS INT), IsApprovedByEducator,
                           CreatedDate, UpdatedDate
                    FROM family.AcademicCourseDocument
                    WHERE CourseCode IS NOT NULL AND LTRIM(RTRIM(CourseCode)) <> ''
                    ORDER BY UpdatedDate DESC;";

                await using var cmd = new SqlCommand(sql, connection);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new AcademicCourseDocumentSummaryResponse
                    {
                        CourseCode = reader.GetString(0),
                        TitleEs = reader.GetString(1),
                        TitleEn = reader.GetString(2),
                        Subject = reader.GetString(3),
                        GradeLevel = reader.GetString(4),
                        Language = reader.GetString(5),
                        EstimatedHours = reader.GetInt32(6),
                        CourseDocumentMarkdownLength = reader.GetInt32(7),
                        IsApprovedByEducator = reader.GetBoolean(8),
                        CreatedDate = reader.GetDateTime(9),
                        UpdatedDate = reader.GetDateTime(10)
                    });
                }

                if (results.Count > 0)
                {
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not list academic course documents from SQL; falling back to in-memory store.");
            }
        }

        lock (SyncLock)
        {
            return InMemoryAcademicCourses.Values
                .Where(r => !string.IsNullOrWhiteSpace(r.CourseCode))
                .OrderByDescending(r => r.CourseCode)
                .Select(r => new AcademicCourseDocumentSummaryResponse
                {
                    CourseCode = r.CourseCode,
                    TitleEs = r.TitleEs,
                    TitleEn = r.TitleEn,
                    Subject = r.Subject,
                    GradeLevel = r.GradeLevel,
                    Language = r.Language,
                    EstimatedHours = r.EstimatedHours,
                    CourseDocumentMarkdownLength = r.CourseDocumentMarkdown.Length,
                    IsApprovedByEducator = r.IsApprovedByEducator,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                })
                .ToList();
        }
    }

    /// <summary>Deletes one saved academic course document by CourseCode (2026-10-08) —
    /// lets the "Catálogo Académico" page's delete button clean up test/broken drafts.
    /// Idempotent: deleting a code that doesn't exist is a no-op, not an error.</summary>
    public async Task DeleteAcademicCourseDocumentAsync(string courseCode, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_connectionString))
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                const string sql = "DELETE FROM family.AcademicCourseDocument WHERE CourseCode = @CourseCode;";
                await using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.Add(new SqlParameter("@CourseCode", courseCode));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete academic course document '{CourseCode}' from SQL.", courseCode);
            }
        }

        lock (SyncLock)
        {
            InMemoryAcademicCourses.Remove(courseCode);
        }
    }

    // ==============================================================================
    // Subject Capability Recommender: Countries / EducationSystemTypes / Subjects
    // (global catalogs) and CapabilityRecommendations (AI-generated, pending hand-off
    // to HumanOS Studio for Engram generation)
    // ==============================================================================

    public async Task<List<CountryResponse>> ListCountriesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<CountryResponse>();
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return results;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT CountryId, Code, NameEs, NameEn, DisplayOrder FROM family.Countries ORDER BY DisplayOrder ASC;";
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new CountryResponse
                {
                    CountryId = reader.GetGuid(0),
                    Code = reader.GetString(1),
                    NameEs = reader.GetString(2),
                    NameEn = reader.GetString(3),
                    DisplayOrder = reader.GetInt32(4),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch countries from SQL database.");
        }

        return results;
    }

    public async Task<List<EducationSystemTypeResponse>> ListEducationSystemTypesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<EducationSystemTypeResponse>();
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return results;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT EducationSystemTypeId, Code, NameEs, NameEn, DisplayOrder FROM family.EducationSystemTypes ORDER BY DisplayOrder ASC;";
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new EducationSystemTypeResponse
                {
                    EducationSystemTypeId = reader.GetGuid(0),
                    Code = reader.GetString(1),
                    NameEs = reader.GetString(2),
                    NameEn = reader.GetString(3),
                    DisplayOrder = reader.GetInt32(4),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch education system types from SQL database.");
        }

        return results;
    }

    public async Task<List<SubjectResponse>> ListSubjectsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<SubjectResponse>();
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return results;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = "SELECT SubjectId, Code, NameEs, NameEn, SubjectCategory, DisplayOrder FROM family.Subjects ORDER BY DisplayOrder ASC;";
            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SubjectResponse
                {
                    SubjectId = reader.GetGuid(0),
                    Code = reader.GetString(1),
                    NameEs = reader.GetString(2),
                    NameEn = reader.GetString(3),
                    SubjectCategory = reader.GetString(4),
                    DisplayOrder = reader.GetInt32(5),
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch subjects from SQL database.");
        }

        return results;
    }

    /// <summary>
    /// Retrieves saved AI capability recommendations, optionally filtered by Country / EducationSystemType / Stage / Subject codes.
    /// </summary>
    public async Task<List<CapabilityRecommendationResponse>> ListRecommendationsAsync(
        string? countryCode = null,
        string? educationSystemTypeCode = null,
        string? stageKey = null,
        string? subjectCode = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CapabilityRecommendationResponse>();
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return results;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                SELECT r.RecommendationId, r.Code, c.Code AS CountryCode, e.Code AS EducationSystemTypeCode,
                       s.StageKey, sub.Code AS SubjectCode, s.AgeRange, r.NameEs, r.NameEn, r.DescriptionEs, r.DescriptionEn,
                       r.WhyItMattersEs, r.WhyItMattersEn, r.MicroSkillsEsJson, r.MicroSkillsEnJson,
                       r.CourseRequirementsEs, r.CourseRequirementsEn, r.CourseOutlineEsJson, r.CourseOutlineEnJson,
                       r.GenerationStatus, r.StudioIdeaDraftEs, r.StudioIdeaDraftEn, r.StudioRunId, r.CreatedDate,
                           r.StudioDocumentId, r.Language, r.SourcesJson
                FROM family.CapabilityRecommendations r
                JOIN family.Countries c ON c.CountryId = r.CountryId
                JOIN family.EducationSystemTypes e ON e.EducationSystemTypeId = r.EducationSystemTypeId
                JOIN family.HumanDevelopmentStages s ON s.StageId = r.StageId
                JOIN family.Subjects sub ON sub.SubjectId = r.SubjectId
                WHERE (@CountryCode IS NULL OR c.Code = @CountryCode)
                  AND (@EducationSystemTypeCode IS NULL OR e.Code = @EducationSystemTypeCode)
                  AND (@StageKey IS NULL OR s.StageKey = @StageKey)
                  AND (@SubjectCode IS NULL OR sub.Code = @SubjectCode)
                ORDER BY r.CreatedDate DESC;";

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add(new SqlParameter("@CountryCode", (object?)countryCode ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@EducationSystemTypeCode", (object?)educationSystemTypeCode ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@StageKey", (object?)stageKey ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@SubjectCode", (object?)subjectCode ?? DBNull.Value));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapRecommendation(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch capability recommendations from SQL database.");
        }

        return results;
    }

    /// <summary>
    /// Runs the SubjectCapabilityRecommenderAgent for the given Country + EducationSystemType + Stage + Subject,
    /// then saves each recommended capability as a pending row (GenerationStatus = 'PendingEngramGeneration'),
    /// ready to be handed off to HumanOS Studio.
    /// </summary>
    public async Task<List<CapabilityRecommendationResponse>> GenerateAndSaveRecommendationsAsync(
        HumanOS.Agents.Family.SubjectCapabilityRecommenderAgent agent,
        GenerateSubjectCapabilityRecommendationsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Database connection is not configured.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var (countryId, countryNameEn) = await ReadLookupAsync(connection, "family.Countries", "CountryId", "Code", request.CountryCode, "NameEn", cancellationToken);
        var (eduTypeId, eduTypeNameEn) = await ReadLookupAsync(connection, "family.EducationSystemTypes", "EducationSystemTypeId", "Code", request.EducationSystemTypeCode, "NameEn", cancellationToken);
        var (stageId, stageNameEn) = await ReadLookupAsync(connection, "family.HumanDevelopmentStages", "StageId", "StageKey", request.StageKey, "StageNameEn", cancellationToken);
        var (subjectId, subjectNameEn) = await ReadLookupAsync(connection, "family.Subjects", "SubjectId", "Code", request.SubjectCode, "NameEn", cancellationToken);

        const string categorySql = "SELECT SubjectCategory FROM family.Subjects WHERE SubjectId = @SubjectId;";
        await using var categoryCmd = new SqlCommand(categorySql, connection);
        categoryCmd.Parameters.Add(new SqlParameter("@SubjectId", subjectId));
        var subjectCategory = (string)(await categoryCmd.ExecuteScalarAsync(cancellationToken) ?? "Academic");

        const string ageRangeSql = "SELECT AgeRange FROM family.HumanDevelopmentStages WHERE StageId = @StageId;";
        await using var ageRangeCmd = new SqlCommand(ageRangeSql, connection);
        ageRangeCmd.Parameters.Add(new SqlParameter("@StageId", stageId));
        var ageRange = (string?)(await ageRangeCmd.ExecuteScalarAsync(cancellationToken));

        var isSingleLanguageMode = !string.IsNullOrWhiteSpace(request.Language);

        var generated = await agent.GenerateRecommendationsAsync(
            countryNameEn, eduTypeNameEn, stageNameEn, subjectNameEn, subjectCategory,
            language: request.Language, cancellationToken: cancellationToken);

        // Guard against the AI occasionally returning the same (English) text in both the
        // Es and En fields for a given capability — e.g. NameEs/DescriptionEs/CourseOutlineEs
        // ends up untranslated, which later renders as English content inside the Spanish UI.
        // Retry the whole generation a couple of times; if it still comes back untranslated,
        // log it loudly so an admin can fix it via the "Editar" UI instead of silently saving
        // mixed-language data.
        // Skipped entirely in single-language mode: there, an identical Es/En pair is the
        // CORRECT, expected output (the course only exists in one language), not a bug.
        const int maxTranslationAttempts = 3;
        for (var attempt = 1; attempt <= maxTranslationAttempts && !isSingleLanguageMode; attempt++)
        {
            var untranslated = generated.Capabilities.Where(HasUntranslatedSpanishFields).ToList();
            if (untranslated.Count == 0)
            {
                break;
            }

            if (attempt == maxTranslationAttempts)
            {
                foreach (var item in untranslated)
                {
                    _logger.LogWarning(
                        "Capability recommendation '{Code}' still has untranslated Spanish fields after {Attempts} generation attempts — review and fix manually via Editar.",
                        item.Code, maxTranslationAttempts);
                }
                break;
            }

            _logger.LogWarning(
                "{Count} capability recommendation(s) came back with untranslated Spanish fields (attempt {Attempt}/{Max}) — regenerating.",
                untranslated.Count, attempt, maxTranslationAttempts);

            generated = await agent.GenerateRecommendationsAsync(
                countryNameEn, eduTypeNameEn, stageNameEn, subjectNameEn, subjectCategory,
                language: request.Language, cancellationToken: cancellationToken);
        }

        var saved = new List<CapabilityRecommendationResponse>();

        foreach (var item in generated.Capabilities)
        {
            var skillsEsJson = JsonSerializer.Serialize(item.MicroSkillsCoveredEs, JsonOptions);
            var skillsEnJson = JsonSerializer.Serialize(item.MicroSkillsCoveredEn, JsonOptions);
            var outlineEsJson = JsonSerializer.Serialize(item.CourseOutlineEs, JsonOptions);
            var outlineEnJson = JsonSerializer.Serialize(item.CourseOutlineEn, JsonOptions);
            var sourcesJson = JsonSerializer.Serialize(item.Sources, JsonOptions);
            var studioIdeaEs = BuildStudioIdeaDraft(item.NameEs, item.DescriptionEs, item.WhyItMattersEs, item.MicroSkillsCoveredEs);
            var studioIdeaEn = BuildStudioIdeaDraft(item.NameEn, item.DescriptionEn, item.WhyItMattersEn, item.MicroSkillsCoveredEn);
            var code = $"{request.CountryCode}-{request.EducationSystemTypeCode}-{request.StageKey}-{request.SubjectCode}-{item.Code}".ToUpperInvariant();

            const string sql = @"
                MERGE family.CapabilityRecommendations AS target
                USING (SELECT @Code AS Code) AS source
                ON (target.Code = source.Code)
                WHEN MATCHED THEN
                    UPDATE SET NameEs = @NameEs, NameEn = @NameEn, DescriptionEs = @DescriptionEs, DescriptionEn = @DescriptionEn,
                               WhyItMattersEs = @WhyItMattersEs, WhyItMattersEn = @WhyItMattersEn,
                               MicroSkillsEsJson = @MicroSkillsEsJson, MicroSkillsEnJson = @MicroSkillsEnJson,
                               CourseRequirementsEs = @CourseRequirementsEs, CourseRequirementsEn = @CourseRequirementsEn,
                               CourseOutlineEsJson = @CourseOutlineEsJson, CourseOutlineEnJson = @CourseOutlineEnJson,
                               StudioIdeaDraftEs = @StudioIdeaDraftEs, StudioIdeaDraftEn = @StudioIdeaDraftEn,
                               Language = @Language, SourcesJson = @SourcesJson, UpdatedDate = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN
                    INSERT (RecommendationId, Code, CountryId, EducationSystemTypeId, StageId, SubjectId, NameEs, NameEn,
                            DescriptionEs, DescriptionEn, WhyItMattersEs, WhyItMattersEn, MicroSkillsEsJson, MicroSkillsEnJson,
                            CourseRequirementsEs, CourseRequirementsEn, CourseOutlineEsJson, CourseOutlineEnJson,
                            StudioIdeaDraftEs, StudioIdeaDraftEn, Language, SourcesJson)
                    VALUES (NEWID(), @Code, @CountryId, @EducationSystemTypeId, @StageId, @SubjectId, @NameEs, @NameEn,
                            @DescriptionEs, @DescriptionEn, @WhyItMattersEs, @WhyItMattersEn, @MicroSkillsEsJson, @MicroSkillsEnJson,
                            @CourseRequirementsEs, @CourseRequirementsEn, @CourseOutlineEsJson, @CourseOutlineEnJson,
                            @StudioIdeaDraftEs, @StudioIdeaDraftEn, @Language, @SourcesJson)
                OUTPUT inserted.RecommendationId;";

            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.Add(new SqlParameter("@Code", code));
            cmd.Parameters.Add(new SqlParameter("@CountryId", countryId));
            cmd.Parameters.Add(new SqlParameter("@EducationSystemTypeId", eduTypeId));
            cmd.Parameters.Add(new SqlParameter("@StageId", stageId));
            cmd.Parameters.Add(new SqlParameter("@SubjectId", subjectId));
            cmd.Parameters.Add(new SqlParameter("@NameEs", item.NameEs));
            cmd.Parameters.Add(new SqlParameter("@NameEn", item.NameEn));
            cmd.Parameters.Add(new SqlParameter("@DescriptionEs", item.DescriptionEs));
            cmd.Parameters.Add(new SqlParameter("@DescriptionEn", item.DescriptionEn));
            cmd.Parameters.Add(new SqlParameter("@WhyItMattersEs", (object?)item.WhyItMattersEs ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@WhyItMattersEn", (object?)item.WhyItMattersEn ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@MicroSkillsEsJson", skillsEsJson));
            cmd.Parameters.Add(new SqlParameter("@MicroSkillsEnJson", skillsEnJson));
            cmd.Parameters.Add(new SqlParameter("@CourseRequirementsEs", (object?)item.CourseRequirementsEs ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@CourseRequirementsEn", (object?)item.CourseRequirementsEn ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@CourseOutlineEsJson", outlineEsJson));
            cmd.Parameters.Add(new SqlParameter("@CourseOutlineEnJson", outlineEnJson));
            cmd.Parameters.Add(new SqlParameter("@StudioIdeaDraftEs", studioIdeaEs));
            cmd.Parameters.Add(new SqlParameter("@StudioIdeaDraftEn", studioIdeaEn));
            cmd.Parameters.Add(new SqlParameter("@Language", (object?)request.Language ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@SourcesJson", sourcesJson));

            var recommendationId = (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);

            saved.Add(new CapabilityRecommendationResponse
            {
                RecommendationId = recommendationId,
                Code = code,
                CountryCode = request.CountryCode,
                EducationSystemTypeCode = request.EducationSystemTypeCode,
                StageKey = request.StageKey,
                SubjectCode = request.SubjectCode,
                AgeRange = ageRange,
                Language = request.Language,
                NameEs = item.NameEs,
                NameEn = item.NameEn,
                DescriptionEs = item.DescriptionEs,
                DescriptionEn = item.DescriptionEn,
                WhyItMattersEs = item.WhyItMattersEs,
                WhyItMattersEn = item.WhyItMattersEn,
                MicroSkillsCoveredEs = item.MicroSkillsCoveredEs,
                MicroSkillsCoveredEn = item.MicroSkillsCoveredEn,
                CourseRequirementsEs = item.CourseRequirementsEs,
                CourseRequirementsEn = item.CourseRequirementsEn,
                CourseOutlineEs = item.CourseOutlineEs,
                CourseOutlineEn = item.CourseOutlineEn,
                Sources = item.Sources,
                GenerationStatus = "PendingEngramGeneration",
                StudioIdeaDraftEs = studioIdeaEs,
                StudioIdeaDraftEn = studioIdeaEn,
                CreatedDate = DateTime.UtcNow,
            });
        }

        return saved;
    }

    /// <summary>
    /// Marks a recommendation as sent to HumanOS Studio, recording the RunId once Studio starts generating the Engram.
    /// </summary>
    public async Task MarkRecommendationSentToStudioAsync(Guid recommendationId, string? studioRunId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = @"
                UPDATE family.CapabilityRecommendations
                SET GenerationStatus = 'SentToStudio', StudioRunId = @StudioRunId, UpdatedDate = SYSUTCDATETIME()
                WHERE RecommendationId = @RecommendationId;";
            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.Add(new SqlParameter("@RecommendationId", recommendationId));
            cmd.Parameters.Add(new SqlParameter("@StudioRunId", (object?)studioRunId ?? DBNull.Value));
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not mark recommendation {RecommendationId} as sent to Studio.", recommendationId);
        }
    }

    /// <summary>
    /// Checks whether Studio has finished creating the Structured Learning Document for this
    /// recommendation — matched via StructuredLearningDocument.SourceCapabilityCode = recommendation.Code
    /// (set by Studio's NewAcademicLearningPage when the educator runs Step 6 "Learning path design").
    /// Family and Studio share the same database, so this is a single same-DB lookup, no cross-service
    /// call needed. If found, flips GenerationStatus to 'Generated' and stores StudioDocumentId so the
    /// UI can show a permanent "View course" link instead of "Send to Studio" from then on.
    /// Returns the (possibly unchanged) current recommendation, or null if it doesn't exist.
    /// </summary>
    public async Task<CapabilityRecommendationResponse?> SyncStudioStatusAsync(Guid recommendationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Already-Generated recommendations never need to look this up again.
            const string checkSql = @"
                SELECT r.Code, r.GenerationStatus, r.StudioDocumentId
                FROM family.CapabilityRecommendations r
                WHERE r.RecommendationId = @RecommendationId;";
            string? code;
            string currentStatus;
            Guid? existingDocumentId;
            await using (var checkCmd = new SqlCommand(checkSql, connection))
            {
                checkCmd.Parameters.Add(new SqlParameter("@RecommendationId", recommendationId));
                await using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return null;
                }
                code = reader.GetString(0);
                currentStatus = reader.GetString(1);
                existingDocumentId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
            }

            if (currentStatus == "Generated" || existingDocumentId.HasValue)
            {
                // Don't blindly trust a previously-recorded StudioDocumentId —
                // the course may have since been deleted in Studio (2026-10-27
                // bug fix: this short-circuit used to never re-check, so a
                // deleted course's "Reopen in Studio"/"View Course" button
                // stayed stuck forever instead of reverting to "Send to
                // Studio"). If the document no longer exists, reset back to
                // pending so the UI offers to (re)create it.
                if (existingDocumentId.HasValue)
                {
                    const string existsSql = @"
                        SELECT 1 FROM dbo.StructuredLearningDocument
                        WHERE StructuredLearningDocumentId = @DocumentId;";
                    bool stillExists;
                    await using (var existsCmd = new SqlCommand(existsSql, connection))
                    {
                        existsCmd.Parameters.Add(new SqlParameter("@DocumentId", existingDocumentId.Value));
                        var scalar = await existsCmd.ExecuteScalarAsync(cancellationToken);
                        stillExists = scalar is not null;
                    }

                    if (!stillExists)
                    {
                        const string resetSql = @"
                            UPDATE family.CapabilityRecommendations
                            SET GenerationStatus = 'PendingEngramGeneration', StudioDocumentId = NULL, UpdatedDate = SYSUTCDATETIME()
                            WHERE RecommendationId = @RecommendationId;";
                        await using var resetCmd = new SqlCommand(resetSql, connection);
                        resetCmd.Parameters.Add(new SqlParameter("@RecommendationId", recommendationId));
                        await resetCmd.ExecuteNonQueryAsync(cancellationToken);

                        return await GetRecommendationByIdAsync(recommendationId, connection, cancellationToken);
                    }
                }

                return await GetRecommendationByIdAsync(recommendationId, connection, cancellationToken);
            }

            const string findDocSql = @"
                SELECT TOP 1 StructuredLearningDocumentId
                FROM dbo.StructuredLearningDocument
                WHERE SourceCapabilityCode = @Code
                ORDER BY CreatedDate DESC;";
            Guid? foundDocumentId = null;
            await using (var findCmd = new SqlCommand(findDocSql, connection))
            {
                findCmd.Parameters.Add(new SqlParameter("@Code", code));
                var result = await findCmd.ExecuteScalarAsync(cancellationToken);
                if (result is Guid guid)
                {
                    foundDocumentId = guid;
                }
            }

            if (foundDocumentId.HasValue)
            {
                const string updateSql = @"
                    UPDATE family.CapabilityRecommendations
                    SET GenerationStatus = 'Generated', StudioDocumentId = @StudioDocumentId, UpdatedDate = SYSUTCDATETIME()
                    WHERE RecommendationId = @RecommendationId;";
                await using var updateCmd = new SqlCommand(updateSql, connection);
                updateCmd.Parameters.Add(new SqlParameter("@RecommendationId", recommendationId));
                updateCmd.Parameters.Add(new SqlParameter("@StudioDocumentId", foundDocumentId.Value));
                await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return await GetRecommendationByIdAsync(recommendationId, connection, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not sync Studio status for recommendation {RecommendationId}.", recommendationId);
            return null;
        }
    }

    private async Task<CapabilityRecommendationResponse?> GetRecommendationByIdAsync(
        Guid recommendationId, SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT r.RecommendationId, r.Code, c.Code AS CountryCode, e.Code AS EducationSystemTypeCode,
                   s.StageKey, sub.Code AS SubjectCode, s.AgeRange, r.NameEs, r.NameEn, r.DescriptionEs, r.DescriptionEn,
                   r.WhyItMattersEs, r.WhyItMattersEn, r.MicroSkillsEsJson, r.MicroSkillsEnJson,
                   r.CourseRequirementsEs, r.CourseRequirementsEn, r.CourseOutlineEsJson, r.CourseOutlineEnJson,
                   r.GenerationStatus, r.StudioIdeaDraftEs, r.StudioIdeaDraftEn, r.StudioRunId, r.CreatedDate,
                   r.StudioDocumentId, r.Language, r.SourcesJson
            FROM family.CapabilityRecommendations r
            JOIN family.Countries c ON c.CountryId = r.CountryId
            JOIN family.EducationSystemTypes e ON e.EducationSystemTypeId = r.EducationSystemTypeId
            JOIN family.HumanDevelopmentStages s ON s.StageId = r.StageId
            JOIN family.Subjects sub ON sub.SubjectId = r.SubjectId
            WHERE r.RecommendationId = @RecommendationId;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@RecommendationId", recommendationId));
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapRecommendation(reader) : null;
    }

    /// <summary>
    /// Updates the editable text fields of a saved capability recommendation (name, description,
    /// why-it-matters, micro-skills). Only allowed while it hasn't been sent to Studio yet, since
    /// once Studio starts generating an Engram from it the text is considered locked-in history.
    /// Returns the updated row, or null if the recommendation doesn't exist.
    /// </summary>
    public async Task<CapabilityRecommendationResponse?> UpdateRecommendationAsync(
        Guid recommendationId,
        UpdateCapabilityRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var skillsEsJson = JsonSerializer.Serialize(request.MicroSkillsCoveredEs ?? [], JsonOptions);
        var skillsEnJson = JsonSerializer.Serialize(request.MicroSkillsCoveredEn ?? [], JsonOptions);
        var outlineEsJson = JsonSerializer.Serialize(request.CourseOutlineEs ?? [], JsonOptions);
        var outlineEnJson = JsonSerializer.Serialize(request.CourseOutlineEn ?? [], JsonOptions);
        var studioIdeaEs = BuildStudioIdeaDraft(request.NameEs, request.DescriptionEs, request.WhyItMattersEs ?? string.Empty, request.MicroSkillsCoveredEs ?? []);
        var studioIdeaEn = BuildStudioIdeaDraft(request.NameEn, request.DescriptionEn, request.WhyItMattersEn ?? string.Empty, request.MicroSkillsCoveredEn ?? []);

        const string sql = @"
            UPDATE family.CapabilityRecommendations
            SET NameEs = @NameEs, NameEn = @NameEn, DescriptionEs = @DescriptionEs, DescriptionEn = @DescriptionEn,
                WhyItMattersEs = @WhyItMattersEs, WhyItMattersEn = @WhyItMattersEn,
                MicroSkillsEsJson = @MicroSkillsEsJson, MicroSkillsEnJson = @MicroSkillsEnJson,
                CourseRequirementsEs = @CourseRequirementsEs, CourseRequirementsEn = @CourseRequirementsEn,
                CourseOutlineEsJson = @CourseOutlineEsJson, CourseOutlineEnJson = @CourseOutlineEnJson,
                StudioIdeaDraftEs = @StudioIdeaDraftEs, StudioIdeaDraftEn = @StudioIdeaDraftEn, UpdatedDate = SYSUTCDATETIME()
            WHERE RecommendationId = @RecommendationId;";

        await using (var cmd = new SqlCommand(sql, connection))
        {
            cmd.Parameters.Add(new SqlParameter("@RecommendationId", recommendationId));
            cmd.Parameters.Add(new SqlParameter("@NameEs", request.NameEs));
            cmd.Parameters.Add(new SqlParameter("@NameEn", request.NameEn));
            cmd.Parameters.Add(new SqlParameter("@DescriptionEs", request.DescriptionEs));
            cmd.Parameters.Add(new SqlParameter("@DescriptionEn", request.DescriptionEn));
            cmd.Parameters.Add(new SqlParameter("@WhyItMattersEs", (object?)request.WhyItMattersEs ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@WhyItMattersEn", (object?)request.WhyItMattersEn ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@MicroSkillsEsJson", skillsEsJson));
            cmd.Parameters.Add(new SqlParameter("@MicroSkillsEnJson", skillsEnJson));
            cmd.Parameters.Add(new SqlParameter("@CourseRequirementsEs", (object?)request.CourseRequirementsEs ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@CourseRequirementsEn", (object?)request.CourseRequirementsEn ?? DBNull.Value));
            cmd.Parameters.Add(new SqlParameter("@CourseOutlineEsJson", outlineEsJson));
            cmd.Parameters.Add(new SqlParameter("@CourseOutlineEnJson", outlineEnJson));
            cmd.Parameters.Add(new SqlParameter("@StudioIdeaDraftEs", studioIdeaEs));
            cmd.Parameters.Add(new SqlParameter("@StudioIdeaDraftEn", studioIdeaEn));
            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (rows == 0)
            {
                return null;
            }
        }

        const string selectSql = @"
            SELECT r.RecommendationId, r.Code, c.Code AS CountryCode, e.Code AS EducationSystemTypeCode,
                   s.StageKey, sub.Code AS SubjectCode, s.AgeRange, r.NameEs, r.NameEn, r.DescriptionEs, r.DescriptionEn,
                   r.WhyItMattersEs, r.WhyItMattersEn, r.MicroSkillsEsJson, r.MicroSkillsEnJson,
                   r.CourseRequirementsEs, r.CourseRequirementsEn, r.CourseOutlineEsJson, r.CourseOutlineEnJson,
                   r.GenerationStatus, r.StudioIdeaDraftEs, r.StudioIdeaDraftEn, r.StudioRunId, r.CreatedDate,
                   r.StudioDocumentId, r.Language, r.SourcesJson
            FROM family.CapabilityRecommendations r
            JOIN family.Countries c ON c.CountryId = r.CountryId
            JOIN family.EducationSystemTypes e ON e.EducationSystemTypeId = r.EducationSystemTypeId
            JOIN family.HumanDevelopmentStages s ON s.StageId = r.StageId
            JOIN family.Subjects sub ON sub.SubjectId = r.SubjectId
            WHERE r.RecommendationId = @RecommendationId;";
        await using var selectCmd = new SqlCommand(selectSql, connection);
        selectCmd.Parameters.Add(new SqlParameter("@RecommendationId", recommendationId));
        await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapRecommendation(reader);
        }

        return null;
    }

    /// <summary>
    /// Permanently deletes a capability recommendation (hard delete — these are just pending AI
    /// drafts, not real course data, so there's no soft-delete/orphan concern like FamilyMember).
    /// Returns false if it didn't exist.
    /// </summary>
    public async Task<bool> DeleteRecommendationAsync(Guid recommendationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return false;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM family.CapabilityRecommendations WHERE RecommendationId = @RecommendationId;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@RecommendationId", recommendationId));
        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    private static CapabilityRecommendationResponse MapRecommendation(SqlDataReader reader)
    {
        var microSkillsEsJson = reader.IsDBNull(13) ? null : reader.GetString(13);
        var microSkillsEnJson = reader.IsDBNull(14) ? null : reader.GetString(14);
        var outlineEsJson = reader.IsDBNull(17) ? null : reader.GetString(17);
        var outlineEnJson = reader.IsDBNull(18) ? null : reader.GetString(18);
        var sourcesJson = reader.IsDBNull(26) ? null : reader.GetString(26);
        return new CapabilityRecommendationResponse
        {
            RecommendationId = reader.GetGuid(0),
            Code = reader.GetString(1),
            CountryCode = reader.GetString(2),
            EducationSystemTypeCode = reader.GetString(3),
            StageKey = reader.GetString(4),
            SubjectCode = reader.GetString(5),
            AgeRange = reader.IsDBNull(6) ? null : reader.GetString(6),
            NameEs = reader.GetString(7),
            NameEn = reader.GetString(8),
            DescriptionEs = reader.GetString(9),
            DescriptionEn = reader.GetString(10),
            WhyItMattersEs = reader.IsDBNull(11) ? null : reader.GetString(11),
            WhyItMattersEn = reader.IsDBNull(12) ? null : reader.GetString(12),
            MicroSkillsCoveredEs = string.IsNullOrWhiteSpace(microSkillsEsJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(microSkillsEsJson, JsonOptions) ?? [],
            MicroSkillsCoveredEn = string.IsNullOrWhiteSpace(microSkillsEnJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(microSkillsEnJson, JsonOptions) ?? [],
            CourseRequirementsEs = reader.IsDBNull(15) ? null : reader.GetString(15),
            CourseRequirementsEn = reader.IsDBNull(16) ? null : reader.GetString(16),
            CourseOutlineEs = string.IsNullOrWhiteSpace(outlineEsJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(outlineEsJson, JsonOptions) ?? [],
            CourseOutlineEn = string.IsNullOrWhiteSpace(outlineEnJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(outlineEnJson, JsonOptions) ?? [],
            GenerationStatus = reader.GetString(19),
            StudioIdeaDraftEs = reader.IsDBNull(20) ? null : reader.GetString(20),
            StudioIdeaDraftEn = reader.IsDBNull(21) ? null : reader.GetString(21),
            StudioRunId = reader.IsDBNull(22) ? null : reader.GetString(22),
            CreatedDate = reader.GetDateTime(23),
            StudioDocumentId = reader.IsDBNull(24) ? null : reader.GetGuid(24),
            Language = reader.IsDBNull(25) ? null : reader.GetString(25),
            Sources = string.IsNullOrWhiteSpace(sourcesJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(sourcesJson, JsonOptions) ?? [],
        };
    }

    private static string BuildStudioIdeaDraft(string name, string description, string whyItMatters, List<string> microSkills)
    {
        var skillsLine = microSkills.Count > 0 ? string.Join(", ", microSkills) : string.Empty;
        return $"{name}. {description} Why it matters: {whyItMatters} Micro-skills covered: {skillsLine}".Trim();
    }

    /// <summary>
    /// Detects whether a capability recommendation's Spanish fields were never actually
    /// translated by the LLM — i.e. they're blank, or byte-for-byte identical to the English
    /// counterpart despite containing real sentence content. A handful of common short words
    /// (titles/codes that are legitimately the same in both languages, e.g. "Geometry") are
    /// allowed to match without tripping the guard; only "long enough to be a real sentence"
    /// fields are compared.
    /// </summary>
    private static bool HasUntranslatedSpanishFields(HumanOS.Agents.Family.RecommendedSubjectCapabilityItem item)
    {
        return IsUntranslatedPair(item.DescriptionEs, item.DescriptionEn)
            || IsUntranslatedPair(item.WhyItMattersEs, item.WhyItMattersEn)
            || IsUntranslatedPair(item.CourseRequirementsEs, item.CourseRequirementsEn)
            || IsUntranslatedOutline(item.CourseOutlineEs, item.CourseOutlineEn)
            || IsUntranslatedOutline(item.MicroSkillsCoveredEs, item.MicroSkillsCoveredEn);
    }

    private static bool IsUntranslatedPair(string? es, string? en)
    {
        if (string.IsNullOrWhiteSpace(en))
        {
            return false;
        }

        // Short strings (titles, codes, single words) can legitimately be identical across
        // languages — only flag longer, sentence-like content as a translation miss.
        if (en.Trim().Length < 25)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(es) || string.Equals(es.Trim(), en.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUntranslatedOutline(List<string> outlineEs, List<string> outlineEn)
    {
        if (outlineEn.Count == 0)
        {
            return false;
        }

        if (outlineEs.Count != outlineEn.Count)
        {
            return outlineEs.Count == 0;
        }

        // The whole outline is considered untranslated only if every chapter title matches —
        // a single coincidentally-identical short title (e.g. "Álgebra" vs "Algebra") is fine.
        return outlineEs.Zip(outlineEn, (es, en) => string.Equals(es?.Trim(), en?.Trim(), StringComparison.OrdinalIgnoreCase))
            .All(isSame => isSame);
    }

    private static async Task<(Guid Id, string NameEn)> ReadLookupAsync(
        SqlConnection connection, string table, string idColumn, string codeColumn, string codeValue, string nameEnColumn, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {idColumn}, {nameEnColumn} FROM {table} WHERE {codeColumn} = @Code;";
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.Add(new SqlParameter("@Code", codeValue));
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"'{codeValue}' not found in {table}.");
        }

        return (reader.GetGuid(0), reader.GetString(1));
    }

    /// <summary>
    /// Looks up the original Family recommendation (country/education system/
    /// grade-stage/subject/age-range) a Studio document was created from, by
    /// its <c>family.CapabilityRecommendations.Code</c> — the same code
    /// Studio stores verbatim on <see cref="HumanOS.Models.Capabilities.Studio.StructuredLearningDocument.SourceCapabilityCode"/>.
    /// Used by GetStructuredLearningDocumentFunction so Studio's course page
    /// can show "how this course was created" (country/grade/age-range) even
    /// for courses with no SubjectId/CapabilityDomainId set in Studio's own
    /// catalog — that metadata only ever lived on the Family side. Returns
    /// null if the code is missing, or no matching recommendation exists
    /// (e.g. documents authored directly in Studio).
    /// </summary>
    public async Task<FamilyCapabilityOriginResponse?> GetRecommendationOriginByCodeAsync(string? code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(_connectionString))
        {
            return null;
        }

        const string sql = @"
            SELECT TOP 1
                c.Code AS CountryCode, c.NameEs AS CountryNameEs, c.NameEn AS CountryNameEn,
                e.Code AS EducationSystemTypeCode, e.NameEs AS EducationSystemTypeNameEs, e.NameEn AS EducationSystemTypeNameEn,
                s.StageKey, s.StageNameEs, s.StageNameEn, s.AgeRange,
                sub.Code AS SubjectCode, sub.NameEs AS SubjectNameEs, sub.NameEn AS SubjectNameEn
            FROM family.CapabilityRecommendations r
            JOIN family.Countries c ON c.CountryId = r.CountryId
            JOIN family.EducationSystemTypes e ON e.EducationSystemTypeId = r.EducationSystemTypeId
            JOIN family.HumanDevelopmentStages s ON s.StageId = r.StageId
            JOIN family.Subjects sub ON sub.SubjectId = r.SubjectId
            WHERE r.Code = @Code;";

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(sql, connection);
            cmd.Parameters.Add(new SqlParameter("@Code", code));
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new FamilyCapabilityOriginResponse
            {
                CountryCode = reader.GetString(0),
                CountryNameEs = reader.GetString(1),
                CountryNameEn = reader.GetString(2),
                EducationSystemTypeCode = reader.GetString(3),
                EducationSystemTypeNameEs = reader.GetString(4),
                EducationSystemTypeNameEn = reader.GetString(5),
                StageKey = reader.GetString(6),
                StageNameEs = reader.GetString(7),
                StageNameEn = reader.GetString(8),
                AgeRange = reader.GetString(9),
                SubjectCode = reader.GetString(10),
                SubjectNameEs = reader.GetString(11),
                SubjectNameEn = reader.GetString(12),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not look up Family recommendation origin for code '{Code}'.", code);
            return null;
        }
    }
}
