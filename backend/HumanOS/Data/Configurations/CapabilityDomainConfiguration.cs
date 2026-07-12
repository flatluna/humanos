using HumanOS.Models.Capabilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CapabilityDomainConfiguration
    : IEntityTypeConfiguration<CapabilityDomain>
{
    public void Configure(EntityTypeBuilder<CapabilityDomain> builder)
    {
        builder.ToTable("CapabilityDomain", "dbo");

        builder.HasKey(x => x.CapabilityDomainId)
            .HasName("PK_CapabilityDomain");

        builder.Property(x => x.CapabilityDomainId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_CapabilityDomain_Code");
    }
}
