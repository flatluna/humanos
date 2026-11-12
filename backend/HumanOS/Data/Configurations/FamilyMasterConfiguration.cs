using HumanOS.Models.Family;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class FamilyMasterConfiguration : IEntityTypeConfiguration<FamilyMaster>
{
    public void Configure(EntityTypeBuilder<FamilyMaster> builder)
    {
        builder.ToTable("FamilyMaster", "dbo");

        builder.HasKey(x => x.FamilyId)
            .HasName("PK_FamilyMaster");

        builder.Property(x => x.FamilyId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.FamilyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.FamilySlug)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.HouseholdType)
            .HasMaxLength(50)
            .HasDefaultValue("Bicultural")
            .IsRequired();

        builder.Property(x => x.AdminFullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AdminEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.AdminPhone)
            .HasMaxLength(50);

        builder.Property(x => x.AdminAzureOid)
            .HasMaxLength(100);

        builder.Property(x => x.CoAdminFullName)
            .HasMaxLength(200);

        builder.Property(x => x.CoAdminEmail)
            .HasMaxLength(255);

        builder.Property(x => x.PrimaryStreet)
            .HasMaxLength(500);

        builder.Property(x => x.PrimaryCity)
            .HasMaxLength(100);

        builder.Property(x => x.PrimaryState)
            .HasMaxLength(100);

        builder.Property(x => x.PrimaryZipCode)
            .HasMaxLength(20);

        builder.Property(x => x.PrimaryCountry)
            .HasMaxLength(100)
            .HasDefaultValue("USA")
            .IsRequired();

        builder.Property(x => x.PrimaryFullAddress)
            .HasMaxLength(1000);

        builder.Property(x => x.Latitude)
            .HasColumnType("decimal(18,7)");

        builder.Property(x => x.Longitude)
            .HasColumnType("decimal(18,7)");

        builder.Property(x => x.SecondaryLocation)
            .HasMaxLength(500);

        builder.Property(x => x.TimeZone)
            .HasMaxLength(100)
            .HasDefaultValue("America/Chicago")
            .IsRequired();

        builder.Property(x => x.PreferredCurrency)
            .HasMaxLength(10)
            .HasDefaultValue("USD")
            .IsRequired();

        builder.Property(x => x.PrimaryLanguage)
            .HasMaxLength(10)
            .HasDefaultValue("es")
            .IsRequired();

        builder.Property(x => x.IsBilingualHousehold)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.AnnualEducationBudget)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.DigitalSunsetTime)
            .HasColumnType("time");

        builder.Property(x => x.SubscriptionTier)
            .HasMaxLength(50)
            .HasDefaultValue("FamilyEnterprise")
            .IsRequired();

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

        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_FamilyMaster_TenantId");

        builder.HasIndex(x => x.FamilySlug)
            .IsUnique()
            .HasDatabaseName("UX_FamilyMaster_FamilySlug");

        builder.HasOne<HumanOS.Models.Tenancy.Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FamilyMaster_Tenant");
    }
}
