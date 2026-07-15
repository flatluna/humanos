using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class HumanStateConfiguration
    : IEntityTypeConfiguration<HumanState>
{
    public void Configure(EntityTypeBuilder<HumanState> builder)
    {
        builder.ToTable("HumanState", "dbo");

        builder.HasKey(x => x.HumanStateId)
            .HasName("PK_HumanState");

        builder.Property(x => x.HumanStateId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Streak)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.RecordedAt)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_HumanState_Person");

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_HumanState_Tenant");

        builder.HasIndex(x => new { x.PersonId, x.RecordedAt })
            .HasDatabaseName("IX_HumanState_PersonId_RecordedAt");
    }
}
