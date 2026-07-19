using HumanOS.Models.Capabilities.Graph;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>
/// Configuración de EF Core para la entidad BlueprintValidationIssue.
/// </summary>
public class BlueprintValidationIssueConfiguration : IEntityTypeConfiguration<BlueprintValidationIssue>
{
    public void Configure(EntityTypeBuilder<BlueprintValidationIssue> builder)
    {
        builder.HasKey(i => i.BlueprintValidationIssueId);

        builder.Property(i => i.Severity)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.Area)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(i => i.Message)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        // FK a BlueprintValidation — relación 1:N
        builder.HasOne(i => i.BlueprintValidation)
            .WithMany(v => v.Issues)
            .HasForeignKey(i => i.BlueprintValidationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.BlueprintValidationId);
    }
}
