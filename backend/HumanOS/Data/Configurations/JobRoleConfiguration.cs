using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class JobRoleConfiguration : IEntityTypeConfiguration<JobRole>
{
    public void Configure(EntityTypeBuilder<JobRole> builder)
    {
        builder.ToTable("JobRole", "dbo");

        builder.HasKey(x => x.JobRoleId)
            .HasName("PK_JobRole");

        builder.Property(x => x.JobRoleId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.DepartmentId);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.LevelLabel)
            .HasMaxLength(150);

        builder.Property(x => x.Purpose)
            .HasMaxLength(2000);

        builder.Property(x => x.Summary)
            .HasMaxLength(2000);

        builder.Property(x => x.ResponsibilitiesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.RequiredExperience)
            .HasMaxLength(2000);

        builder.Property(x => x.RequiredEducation)
            .HasMaxLength(1000);

        builder.Property(x => x.RequiredCertificationsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.RequiredLanguagesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.TechnicalSkillsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.SoftSkillsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.DepartmentProcessesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.HrProcessesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.ToolsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.StartInYourRoleTechnicalSkillsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.StartInYourRoleSoftSkillsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.StartInYourRoleDepartmentProcessesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.StartInYourRoleHrProcessesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.InYourRoleTechnicalSkillsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.InYourRoleSoftSkillsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.InYourRoleDepartmentProcessesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.InYourRoleHrProcessesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.DailyActivitiesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.PerformanceIndicatorsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.ExpectedOutcomesJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

        builder.Property(x => x.WorkSchedule)
            .HasMaxLength(500);

        builder.Property(x => x.WorkModality)
            .HasMaxLength(300);

        builder.Property(x => x.RequiresTravel)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.WorkLocation)
            .HasMaxLength(500);

        builder.Property(x => x.AvailabilityNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.EmploymentType)
            .HasMaxLength(150);

        builder.Property(x => x.SalaryRangeMin)
            .HasColumnType("decimal(12,2)");

        builder.Property(x => x.SalaryRangeMax)
            .HasColumnType("decimal(12,2)");

        builder.Property(x => x.SalaryCurrency)
            .HasMaxLength(10);

        builder.Property(x => x.BenefitsJson)
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("[]")
            .IsRequired();

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

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_JobRole_Tenant");

        builder.HasOne(x => x.Department)
            .WithMany(x => x.JobRoles)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_JobRole_Department");

        builder.HasOne(x => x.ReportsToJobRole)
            .WithMany(x => x.DirectReportRoles)
            .HasForeignKey(x => x.ReportsToJobRoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_JobRole_ReportsToJobRole");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_JobRole_TenantId");

        builder.HasIndex(x => x.DepartmentId)
            .HasDatabaseName("IX_JobRole_DepartmentId");

        builder.HasIndex(x => x.ReportsToJobRoleId)
            .HasDatabaseName("IX_JobRole_ReportsToJobRoleId");
    }
}
