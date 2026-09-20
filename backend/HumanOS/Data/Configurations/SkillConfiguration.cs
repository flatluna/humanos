using HumanOS.Models.OrgStructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skill", "dbo");

        builder.HasKey(x => x.SkillId)
            .HasName("PK_Skill");

        builder.Property(x => x.SkillId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.AssignmentScope)
            .HasMaxLength(40);

        builder.Ignore(x => x.ApplicableDepartmentIdsJson);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.DocumentStoragePath)
            .HasMaxLength(1000);

        builder.Property(x => x.DocumentFileName)
            .HasMaxLength(300);

        builder.Property(x => x.CreatedDate)
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Category });
    }
}
