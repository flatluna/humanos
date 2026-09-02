using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class JobRoleActivityConfiguration : IEntityTypeConfiguration<JobRoleActivity>
{
    public void Configure(EntityTypeBuilder<JobRoleActivity> builder)
    {
        builder.ToTable("JobRoleActivity", "dbo");

        builder.HasKey(x => new { x.JobRoleId, x.ActivityId })
            .HasName("PK_JobRoleActivity");

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.HasOne(x => x.JobRole)
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .HasConstraintName("FK_JobRoleActivity_JobRole")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Activity)
            .WithMany(a => a.JobRoleActivities)
            .HasForeignKey(x => x.ActivityId)
            .HasConstraintName("FK_JobRoleActivity_Activity")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ActivityId);
    }
}
