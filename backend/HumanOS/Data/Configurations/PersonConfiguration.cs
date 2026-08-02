using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Person", "dbo");

        builder.HasKey(x => x.PersonId)
            .HasName("PK_Person");

        builder.Property(x => x.PersonId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId);

        builder.Property(x => x.AzureOid)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.AzureTid)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(255);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.LastLoginDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.OrgRole)
            .HasConversion<int>()
            .HasDefaultValue(PersonOrgRole.Employee)
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
            .HasConstraintName("FK_Person_Tenant");

        builder.HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_Person_Department");

        builder.HasOne(x => x.JobRole)
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_Person_JobRole");

        // Self-referencing "reports to" — Restrict (not Cascade/SetNull)
        // since Person rows are never hard-deleted by any existing code
        // path; explicit to avoid SQL Server's multiple-cascade-paths
        // restriction if that ever changes.
        builder.HasOne(x => x.Manager)
            .WithMany(x => x.DirectReports)
            .HasForeignKey(x => x.ManagerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Person_Manager");

        builder.HasIndex(x => new
            {
                x.AzureTid,
                x.AzureOid
            })
            .IsUnique()
            .HasDatabaseName("UX_Person_AzureTid_AzureOid");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_Person_TenantId");

        builder.HasIndex(x => x.DepartmentId)
            .HasDatabaseName("IX_Person_DepartmentId");

        builder.HasIndex(x => x.JobRoleId)
            .HasDatabaseName("IX_Person_JobRoleId");

        builder.HasIndex(x => x.ManagerId)
            .HasDatabaseName("IX_Person_ManagerId");
    }
}
