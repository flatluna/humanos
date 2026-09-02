using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class DepartmentJobRoleConfiguration : IEntityTypeConfiguration<DepartmentJobRole>
{
    public void Configure(EntityTypeBuilder<DepartmentJobRole> builder)
    {
        builder.ToTable("DepartmentJobRole", "dbo");

        builder.HasKey(x => new { x.DepartmentId, x.JobRoleId })
            .HasName("PK_DepartmentJobRole");

        builder.Property(x => x.AssignedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Department)
            .WithMany(x => x.DepartmentJobRoles)
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_DepartmentJobRole_Department");

        builder.HasOne(x => x.JobRole)
            .WithMany(x => x.DepartmentJobRoles)
            .HasForeignKey(x => x.JobRoleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_DepartmentJobRole_JobRole");
    }
}
