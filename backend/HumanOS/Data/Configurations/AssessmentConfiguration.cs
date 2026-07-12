using HumanOS.Models.Assessments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class AssessmentConfiguration
    : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.ToTable("Assessment", "dbo");

        builder.HasKey(x => x.AssessmentId)
            .HasName("PK_Assessment");

        builder.Property(x => x.AssessmentId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.AssessmentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PassingScore)
            .HasPrecision(5, 2)
            .HasDefaultValue(70)
            .IsRequired();

        builder.Property(x => x.MaxScore)
            .HasPrecision(5, 2)
            .HasDefaultValue(100)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Capability)
            .WithMany()
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Assessment_Capability");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Assessment_Scores",
                """
                [MaxScore] > 0
                AND [PassingScore] BETWEEN 0 AND [MaxScore]
                """);
        });
    }
}
