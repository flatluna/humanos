using HumanOS.Models.Capabilities.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

/// <summary>EF configuration for <see cref="FilmProduction"/> and
/// <see cref="FilmScene"/> — see HumanOS/Models/Capabilities/Studio/FilmProduction.cs.</summary>
public sealed class FilmProductionConfiguration : IEntityTypeConfiguration<FilmProduction>
{
    public void Configure(EntityTypeBuilder<FilmProduction> builder)
    {
        builder.ToTable("FilmProduction", "dbo");

        builder.HasKey(x => x.FilmProductionId)
            .HasName("PK_FilmProduction");

        builder.Property(x => x.FilmProductionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Title)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.FinalFilmStoragePath)
            .HasMaxLength(1000);

        builder.Property(x => x.BreakdownModelName)
            .HasMaxLength(200);

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_FilmProduction_TenantId");
    }
}

public sealed class FilmSceneConfiguration : IEntityTypeConfiguration<FilmScene>
{
    public void Configure(EntityTypeBuilder<FilmScene> builder)
    {
        builder.ToTable("FilmScene", "dbo");

        builder.HasKey(x => x.FilmSceneId)
            .HasName("PK_FilmScene");

        builder.Property(x => x.FilmSceneId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Description)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Environment)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.DialogueText).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Characters).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CameraMovements).HasColumnType("nvarchar(max)");
        builder.Property(x => x.CustomImagePrompt).HasColumnType("nvarchar(max)");
        builder.Property(x => x.SuggestedSceneName).HasMaxLength(200);
        builder.Property(x => x.GeneratedPrompt).HasColumnType("nvarchar(max)");
        builder.Property(x => x.AnchorImageStoragePath).HasMaxLength(1000);
        builder.Property(x => x.FinalImageStoragePath).HasMaxLength(1000);
        builder.Property(x => x.ImageModel).HasMaxLength(200);
        builder.Property(x => x.ErrorMessage).HasColumnType("nvarchar(max)");
        builder.Property(x => x.FinalVideoStoragePath).HasMaxLength(1000);
        builder.Property(x => x.VideoModel).HasMaxLength(200);
        builder.Property(x => x.VideoErrorMessage).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ContentModelName).HasMaxLength(200);

        builder.HasIndex(x => new { x.FilmProductionId, x.SortOrder })
            .HasDatabaseName("IX_FilmScene_FilmProductionId_SortOrder");
    }
}
