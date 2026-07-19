using HumanOS.Agents.Studio;
using HumanOS.Data;
using HumanOS.Models.Capabilities.Graph;

namespace HumanOS.Services;

/// <summary>
/// Persists a <see cref="BlueprintValidationResponse"/> (already hardened by
/// <see cref="BlueprintValidationGuard"/>) as a new, APPEND-ONLY
/// <see cref="BlueprintValidation"/> row plus its child
/// <see cref="BlueprintValidationIssue"/>/<see cref="BlueprintValidationMetric"/>
/// rows — same shape/style as <c>NodeExperienceBlueprintPersistenceService</c>.
/// </summary>
public sealed class BlueprintValidationPersistenceService
{
    public async Task<BlueprintValidation> PersistAsync(
        HumanOsDbContext dbContext,
        Guid nodeExperienceBlueprintId,
        BlueprintValidationResponse response,
        AgentTokenUsage tokenUsage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(tokenUsage);

        var validation = new BlueprintValidation
        {
            BlueprintValidationId = Guid.NewGuid(),
            NodeExperienceBlueprintId = nodeExperienceBlueprintId,
            Status = response.Status,
            Score = response.Score,
            InputTokens = tokenUsage.InputTokens,
            OutputTokens = tokenUsage.OutputTokens,
            TotalTokens = tokenUsage.TotalTokens,
            CreatedDate = DateTime.UtcNow
        };

        foreach (var issue in response.Issues)
        {
            validation.Issues.Add(new BlueprintValidationIssue
            {
                BlueprintValidationIssueId = Guid.NewGuid(),
                BlueprintValidationId = validation.BlueprintValidationId,
                Severity = BlueprintValidationIssueSeverity.Error,
                Area = issue.Area,
                Message = issue.Message
            });
        }

        foreach (var warning in response.Warnings)
        {
            validation.Issues.Add(new BlueprintValidationIssue
            {
                BlueprintValidationIssueId = Guid.NewGuid(),
                BlueprintValidationId = validation.BlueprintValidationId,
                Severity = BlueprintValidationIssueSeverity.Warning,
                Area = warning.Area,
                Message = warning.Message
            });
        }

        foreach (var metric in response.Metrics)
        {
            validation.Metrics.Add(new BlueprintValidationMetric
            {
                BlueprintValidationMetricId = Guid.NewGuid(),
                BlueprintValidationId = validation.BlueprintValidationId,
                MetricName = metric.MetricName,
                MetricValue = metric.MetricValue
            });
        }

        dbContext.BlueprintValidations.Add(validation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return validation;
    }
}
