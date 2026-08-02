using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonCareerPlanConfiguration : IEntityTypeConfiguration<PersonCareerPlan>
{
    public void Configure(EntityTypeBuilder<PersonCareerPlan> builder)
    {
        builder.ToTable("PersonCareerPlan", "dbo");

        builder.HasKey(x => x.PersonCareerPlanId)
            .HasName("PK_PersonCareerPlan");

        builder.Property(x => x.PersonCareerPlanId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.JobRoleId)
            .IsRequired();

        builder.Property(x => x.GapSkillsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.TechnicalRoadmapJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.SoftSkillsRoadmapJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.DepartmentProcessesRoadmapJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.HrProcessesRoadmapJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.OverallRationale)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.GeneratedModel)
            .HasMaxLength(100);

        builder.Property(x => x.GeneratedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.IsAccepted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.AcceptedDate)
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
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PersonCareerPlan_Person");

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_PersonCareerPlan_PersonId");
    }
}
