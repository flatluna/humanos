using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class JobRoleCertificationConfiguration : IEntityTypeConfiguration<JobRoleCertification>
{
    public void Configure(EntityTypeBuilder<JobRoleCertification> builder)
    {
        builder.ToTable("JobRoleCertification", "dbo");

        builder.HasKey(x => new { x.JobRoleId, x.CertificationId })
            .HasName("PK_JobRoleCertification");

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.HasOne(x => x.JobRole)
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .HasConstraintName("FK_JobRoleCertification_JobRole")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Certification)
            .WithMany(c => c.JobRoleCertifications)
            .HasForeignKey(x => x.CertificationId)
            .HasConstraintName("FK_JobRoleCertification_Certification")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.CertificationId);
    }
}
