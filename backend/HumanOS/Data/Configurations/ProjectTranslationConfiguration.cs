using HumanOS.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ProjectTranslationConfiguration
    : IEntityTypeConfiguration<ProjectTranslation>
{
    public void Configure(
        EntityTypeBuilder<ProjectTranslation> builder)
    {
        builder.ToTable("ProjectTranslation", "dbo");

        builder.HasKey(x => new
        {
            x.ProjectId,
            x.LanguageCode
        })
        .HasName("PK_ProjectTranslation");

        builder.Property(x => x.ProjectId)
            .IsRequired();

        builder.Property(x => x.LanguageCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_ProjectTranslation_Project");

        builder.HasOne(x => x.Language)
            .WithMany(x => x.ProjectTranslations)
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_ProjectTranslation_Language");
    }
}
