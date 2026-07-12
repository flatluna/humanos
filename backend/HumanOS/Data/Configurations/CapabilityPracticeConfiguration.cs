using HumanOS.Models.Practice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityPracticeConfiguration
    : IEntityTypeConfiguration<CapabilityPractice>
{
    public void Configure(
        EntityTypeBuilder<CapabilityPractice> builder)
    {
        builder.ToTable("CapabilityPractice", "dbo");

        builder.HasKey(x => x.CapabilityPracticeId)
            .HasName("PK_CapabilityPractice");

        builder.Property(x => x.CapabilityPracticeId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonCapabilityId)
            .IsRequired();

        builder.Property(x => x.PracticeType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.AssistanceLevel)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.PersonReflection)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.LanguageCode)
            .HasMaxLength(10)
            .HasDefaultValue("en")
            .IsRequired();

        builder.Property(x => x.PracticedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.PersonCapability)
            .WithMany(x => x.Practices)
            .HasForeignKey(x => x.PersonCapabilityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "FK_CapabilityPractice_PersonCapability");

        builder.HasOne(x => x.Language)
            .WithMany(x => x.CapabilityPractices)
            .HasForeignKey(x => x.LanguageCode)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_CapabilityPractice_Language");

        builder.HasIndex(x => x.PersonCapabilityId)
            .HasDatabaseName(
                "IX_CapabilityPractice_PersonCapabilityId");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_CapabilityPractice_AssistanceLevel",
                "[AssistanceLevel] BETWEEN 0 AND 5");
        });
    }
}
