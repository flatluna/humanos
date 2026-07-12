using HumanOS.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonProjectConfiguration
    : IEntityTypeConfiguration<PersonProject>
{
    public void Configure(EntityTypeBuilder<PersonProject> builder)
    {
        builder.ToTable("PersonProject", "dbo");

        builder.HasKey(x => x.PersonProjectId)
            .HasName("PK_PersonProject");

        builder.Property(x => x.PersonProjectId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.ProjectId)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(100)
            .HasDefaultValue("NotStarted")
            .IsRequired();

        builder.Property(x => x.ProgressPercentage)
            .HasPrecision(5, 2)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.StartedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.CompletedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PersonProject_Person");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.PersonProjects)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PersonProject_Project");

        builder.HasIndex(x => new
        {
            x.PersonId,
            x.ProjectId
        })
            .IsUnique()
            .HasDatabaseName(
                "UX_PersonProject_PersonId_ProjectId");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_PersonProject_Status",
                """
                [Status] IN
                (
                    'NotStarted',
                    'InProgress',
                    'Paused',
                    'Completed'
                )
                """);

            table.HasCheckConstraint(
                "CK_PersonProject_ProgressPercentage",
                "[ProgressPercentage] BETWEEN 0 AND 100");
        });
    }
}
