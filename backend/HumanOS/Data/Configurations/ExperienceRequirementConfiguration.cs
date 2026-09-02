using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ExperienceRequirementConfiguration : IEntityTypeConfiguration<ExperienceRequirement>
{
    public void Configure(EntityTypeBuilder<ExperienceRequirement> builder)
    {
        builder.ToTable("ExperienceRequirement", "dbo");

        builder.HasKey(x => x.ExperienceRequirementId)
            .HasName("PK_ExperienceRequirement");

        builder.Property(x => x.ExperienceRequirementId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .IsRequired();

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.Name });
    }
}
