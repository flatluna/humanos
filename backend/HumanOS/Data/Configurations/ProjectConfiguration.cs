using HumanOS.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ProjectConfiguration
    : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Project", "dbo");

        builder.HasKey(x => x.ProjectId)
            .HasName("PK_Project");

        builder.Property(x => x.ProjectId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.DifficultyLevel)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.EstimatedHours)
            .HasPrecision(6, 2);

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
            .HasConstraintName("FK_Project_Capability");

        builder.HasIndex(x => x.CapabilityId)
            .HasDatabaseName("IX_Project_CapabilityId");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Project_DifficultyLevel",
                "[DifficultyLevel] BETWEEN 1 AND 5");
        });
    }
}
