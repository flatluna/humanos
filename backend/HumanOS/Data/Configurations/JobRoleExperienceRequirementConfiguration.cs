using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class JobRoleExperienceRequirementConfiguration : IEntityTypeConfiguration<JobRoleExperienceRequirement>
{
    public void Configure(EntityTypeBuilder<JobRoleExperienceRequirement> builder)
    {
        builder.ToTable("JobRoleExperienceRequirement", "dbo");

        builder.HasKey(x => new { x.JobRoleId, x.ExperienceRequirementId })
            .HasName("PK_JobRoleExperienceRequirement");

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.HasOne(x => x.JobRole)
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .HasConstraintName("FK_JobRoleExperienceRequirement_JobRole")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ExperienceRequirement)
            .WithMany(e => e.JobRoleExperienceRequirements)
            .HasForeignKey(x => x.ExperienceRequirementId)
            .HasConstraintName("FK_JobRoleExperienceRequirement_ExperienceRequirement")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ExperienceRequirementId);
    }
}
