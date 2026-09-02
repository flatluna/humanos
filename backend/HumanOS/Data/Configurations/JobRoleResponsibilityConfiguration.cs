using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class JobRoleResponsibilityConfiguration : IEntityTypeConfiguration<JobRoleResponsibility>
{
    public void Configure(EntityTypeBuilder<JobRoleResponsibility> builder)
    {
        builder.ToTable("JobRoleResponsibility", "dbo");

        builder.HasKey(x => new { x.JobRoleId, x.ResponsibilityId })
            .HasName("PK_JobRoleResponsibility");

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.HasOne(x => x.JobRole)
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .HasConstraintName("FK_JobRoleResponsibility_JobRole")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Responsibility)
            .WithMany(r => r.JobRoleResponsibilities)
            .HasForeignKey(x => x.ResponsibilityId)
            .HasConstraintName("FK_JobRoleResponsibility_Responsibility")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ResponsibilityId);
    }
}
