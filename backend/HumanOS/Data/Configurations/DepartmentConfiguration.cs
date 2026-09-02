using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Department", "dbo");

        builder.HasKey(x => x.DepartmentId)
            .HasName("PK_Department");

        builder.Property(x => x.DepartmentId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Code)
            .HasMaxLength(50);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Category)
            .HasMaxLength(100);

        builder.Property(x => x.ContactEmail)
            .HasMaxLength(255);

        builder.Property(x => x.Location)
            .HasMaxLength(255);

        builder.Property(x => x.CostCenter)
            .HasMaxLength(100);

        builder.Property(x => x.MissionStatement)
            .HasMaxLength(2000);

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
            .HasConstraintName("FK_Department_Tenant");

        // Self-referencing tree — Restrict avoids SQL Server's "multiple
        // cascade paths" restriction and prevents a whole subtree from
        // silently disappearing if a parent is ever hard-deleted.
        builder.HasOne(x => x.ParentDepartment)
            .WithMany(x => x.ChildDepartments)
            .HasForeignKey(x => x.ParentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Department_ParentDepartment");

        builder.HasOne(x => x.LeaderPerson)
            .WithMany()
            .HasForeignKey(x => x.LeaderPersonId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_Department_LeaderPerson");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_Department_TenantId");

        builder.HasIndex(x => x.ParentDepartmentId)
            .HasDatabaseName("IX_Department_ParentDepartmentId");
    }
}
