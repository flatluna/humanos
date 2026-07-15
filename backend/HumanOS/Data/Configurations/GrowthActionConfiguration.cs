using HumanOS.Models.GrowthActions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class GrowthActionConfiguration
    : IEntityTypeConfiguration<GrowthAction>
{
    public void Configure(EntityTypeBuilder<GrowthAction> builder)
    {
        builder.ToTable("GrowthAction", "dbo");

        builder.HasKey(x => x.GrowthActionId)
            .HasName("PK_GrowthAction");

        builder.Property(x => x.GrowthActionId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.PersonCapabilityId)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.ActionType)
            .HasMaxLength(50);

        builder.Property(x => x.IsCompleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.ScheduledFor)
            .HasColumnType("date");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_GrowthAction_Person");

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_GrowthAction_Tenant");

        builder.HasOne(x => x.PersonCapability)
            .WithMany()
            .HasForeignKey(x => x.PersonCapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_GrowthAction_PersonCapability");

        builder.HasOne(x => x.RecallAttempt)
            .WithMany()
            .HasForeignKey(x => x.RecallAttemptId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_GrowthAction_RecallAttempt");

        builder.HasOne(x => x.Practice)
            .WithMany()
            .HasForeignKey(x => x.PracticeId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_GrowthAction_CapabilityPractice");

        builder.HasOne(x => x.Assessment)
            .WithMany()
            .HasForeignKey(x => x.AssessmentId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_GrowthAction_Assessment");

        builder.HasIndex(x => new { x.PersonId, x.ScheduledFor })
            .HasDatabaseName("IX_GrowthAction_PersonId_ScheduledFor");
    }
}
