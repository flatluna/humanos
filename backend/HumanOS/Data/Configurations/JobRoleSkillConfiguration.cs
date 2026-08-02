using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class JobRoleSkillConfiguration : IEntityTypeConfiguration<JobRoleSkill>
{
    public void Configure(EntityTypeBuilder<JobRoleSkill> builder)
    {
        builder.ToTable("JobRoleSkill", "dbo");

        builder.HasKey(x => new { x.JobRoleId, x.SkillId })
            .HasName("PK_JobRoleSkill");

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.HasOne(x => x.JobRole)
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .HasConstraintName("FK_JobRoleSkill_JobRole")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Skill)
            .WithMany(s => s.JobRoleSkills)
            .HasForeignKey(x => x.SkillId)
            .HasConstraintName("FK_JobRoleSkill_Skill")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SkillId);
    }
}
