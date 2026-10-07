using HumanOS.Models.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public class TenantServiceQuotaConfiguration : IEntityTypeConfiguration<TenantServiceQuota>
{
    public void Configure(EntityTypeBuilder<TenantServiceQuota> builder)
    {
        builder.HasKey(q => q.TenantServiceQuotaId);

        builder.Property(q => q.QuotaKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(q => q.LimitValue)
            .HasColumnType("decimal(18,2)");

        builder.HasIndex(q => new { q.TenantId, q.QuotaKey }).IsUnique();

        builder.HasOne(q => q.Tenant)
            .WithMany()
            .HasForeignKey(q => q.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
