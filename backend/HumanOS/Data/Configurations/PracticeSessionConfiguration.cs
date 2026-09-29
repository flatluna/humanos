using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PracticeSessionConfiguration : IEntityTypeConfiguration<PracticeSession>
{
    public void Configure(EntityTypeBuilder<PracticeSession> builder)
    {
        builder.ToTable("PracticeSession", "dbo");

        builder.HasKey(x => x.PracticeSessionId)
            .HasName("PK_PracticeSession");

        builder.Property(x => x.PracticeSessionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.CapabilityName)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.ExercisesJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => new { x.PersonId, x.CapabilityId })
            .IsUnique()
            .HasDatabaseName("IX_PracticeSession_PersonId_CapabilityId");
    }
}
