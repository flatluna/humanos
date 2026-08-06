using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityConfiguration
    : IEntityTypeConfiguration<Capability>
{
    public void Configure(EntityTypeBuilder<Capability> builder)
    {
        builder.ToTable("Capability", "dbo");

        builder.HasKey(x => x.CapabilityId)
            .HasName("PK_Capability");

        builder.Property(x => x.CapabilityId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityDomainId)
            .IsRequired();

        builder.Property(x => x.Code)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.ContentLanguageCode)
            .HasMaxLength(10)
            .HasDefaultValue("es")
            .IsRequired();

        builder.Property(x => x.PedagogicalAudience)
            .HasMaxLength(30);

        builder.Property(x => x.Category)
            .HasMaxLength(30);

        builder.Property(x => x.TargetSkill)
            .HasMaxLength(400);

        builder.Property(x => x.ConsentAcceptedByName)
            .HasMaxLength(320);

        builder.Property(x => x.ConsentAcceptedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.CapabilityDomain)
            .WithMany(x => x.Capabilities)
            .HasForeignKey(x => x.CapabilityDomainId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_Capability_CapabilityDomain");

        builder.HasOne(x => x.Subject)
            .WithMany(x => x.Capabilities)
            .HasForeignKey(x => x.SubjectId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName(
                "FK_Capability_Subject");

        builder.HasOne<HumanOS.Models.OrgStructure.JobRole>()
            .WithMany()
            .HasForeignKey(x => x.TargetJobRoleId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName(
                "FK_Capability_TargetJobRole");

        builder.HasOne<HumanOS.Models.People.Person>()
            .WithMany()
            .HasForeignKey(x => x.OwnerPersonId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName(
                "FK_Capability_OwnerPerson");

        builder.HasOne<HumanOS.Models.Tenancy.Tenant>()
            .WithMany()
            .HasForeignKey(x => x.OwnerTenantId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName(
                "FK_Capability_OwnerTenant");

        builder.HasOne<HumanOS.Models.People.Person>()
            .WithMany()
            .HasForeignKey(x => x.ConsentAcceptedByPersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_Capability_ConsentAcceptedByPerson");

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Capability_Code");

        builder.HasIndex(x => x.CapabilityDomainId)
            .HasDatabaseName(
                "IX_Capability_CapabilityDomainId");

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName(
                "IX_Capability_SubjectId");

        builder.HasIndex(x => x.Category)
            .HasDatabaseName(
                "IX_Capability_Category");

        builder.HasIndex(x => x.TargetJobRoleId)
            .HasDatabaseName(
                "IX_Capability_TargetJobRoleId");

        builder.HasIndex(x => x.OwnerPersonId)
            .HasDatabaseName(
                "IX_Capability_OwnerPersonId");

        builder.HasIndex(x => x.OwnerTenantId)
            .HasDatabaseName(
                "IX_Capability_OwnerTenantId");

        builder.HasIndex(x => x.ConsentAcceptedByPersonId)
            .HasDatabaseName(
                "IX_Capability_ConsentAcceptedByPersonId");
    }
}
