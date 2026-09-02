using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class JobRoleToolConfiguration : IEntityTypeConfiguration<JobRoleTool>
{
    public void Configure(EntityTypeBuilder<JobRoleTool> builder)
    {
        builder.ToTable("JobRoleTool", "dbo");

        builder.HasKey(x => new { x.JobRoleId, x.ToolId })
            .HasName("PK_JobRoleTool");

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.HasOne(x => x.JobRole)
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .HasConstraintName("FK_JobRoleTool_JobRole")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tool)
            .WithMany(t => t.JobRoleTools)
            .HasForeignKey(x => x.ToolId)
            .HasConstraintName("FK_JobRoleTool_Tool")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ToolId);
    }
}
