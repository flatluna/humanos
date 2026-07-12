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

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Capability_Code");

        builder.HasIndex(x => x.CapabilityDomainId)
            .HasDatabaseName(
                "IX_Capability_CapabilityDomainId");
    }
}
