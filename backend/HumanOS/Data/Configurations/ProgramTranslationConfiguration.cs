using HumanOS.Models.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ProgramTranslationConfiguration : IEntityTypeConfiguration<ProgramTranslation>
{
    public void Configure(EntityTypeBuilder<ProgramTranslation> builder)
    {
        builder.ToTable("ProgramTranslation", "dbo");

        builder.HasKey(x => new
        {
            x.ProgramId,
            x.LanguageCode
        })
        .HasName("PK_ProgramTranslation");

        builder.Property(x => x.ProgramId)
            .IsRequired();

        builder.Property(x => x.LanguageCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.Objectives)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Requirements)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Program)
            .WithMany(x => x.Translations)
            .HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ProgramTranslation_Program");

        builder.HasOne(x => x.Language)
            .WithMany()
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProgramTranslation_Language");
    }
}
