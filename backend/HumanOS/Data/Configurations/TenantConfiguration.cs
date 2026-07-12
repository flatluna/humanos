using HumanOS.Models.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenant", "dbo");

        builder.HasKey(x => x.TenantId)
            .HasName("PK_Tenant");

        builder.Property(x => x.TenantId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Slug)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Domain)
            .HasMaxLength(255);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.CultureCode)
            .HasMaxLength(10)
            .HasDefaultValue("en-US")
            .IsRequired();

        builder.Property(x => x.TimeZone)
            .HasMaxLength(100)
            .HasDefaultValue("UTC")
            .IsRequired();

        builder.Property(x => x.AzureTenantId)
            .HasMaxLength(100);

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

        builder.HasIndex(x => x.Slug)
            .IsUnique()
            .HasDatabaseName("UX_Tenant_Slug");

        builder.HasIndex(x => x.AzureTenantId)
            .IsUnique()
            .HasFilter("[AzureTenantId] IS NOT NULL")
            .HasDatabaseName("UX_Tenant_AzureTenantId");
    }
}
