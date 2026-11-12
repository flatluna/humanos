using HumanOS.Models.Culture;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CulturePrincipleConfiguration : IEntityTypeConfiguration<CulturePrinciple>
{
    public void Configure(EntityTypeBuilder<CulturePrinciple> builder)
    {
        builder.ToTable("CulturePrinciple", "dbo");

        builder.HasKey(x => x.CulturePrincipleId)
            .HasName("PK_CulturePrinciple");

        builder.Property(x => x.CulturePrincipleId)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.TenantId);

        builder.Property(x => x.SourceCulturePrincipleId);

        builder.Property(x => x.PrincipleCode)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.NameEn)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.NameEs)
            .HasMaxLength(150);

        builder.Property(x => x.DescriptionEn)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.DescriptionEs)
            .HasMaxLength(1000);

        builder.Property(x => x.OwnerPersonId);

        builder.Property(x => x.VersionNumber)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .HasDefaultValue(CulturePrincipleStatus.Draft)
            .IsRequired();

        builder.Property(x => x.EffectiveFrom)
            .HasColumnType("date");

        builder.Property(x => x.EffectiveTo)
            .HasColumnType("date");

        builder.Property(x => x.ApprovedByPersonId);

        builder.Property(x => x.ApprovedAt)
            .HasColumnType("datetime2(3)");

        builder.Property(x => x.CreatedAt)
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.CreatedByPersonId);

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        /*
         * Foreign keys
         */

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CulturePrinciple_Tenant");

        builder.HasOne(x => x.SourceCulturePrinciple)
            .WithMany(x => x.AdaptedPrinciples)
            .HasForeignKey(x => x.SourceCulturePrincipleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CulturePrinciple_SourceCulturePrinciple");

        builder.HasOne(x => x.OwnerPerson)
            .WithMany()
            .HasForeignKey(x => x.OwnerPersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CulturePrinciple_OwnerPerson");

        builder.HasOne(x => x.ApprovedByPerson)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByPersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CulturePrinciple_ApprovedByPerson");

        builder.HasOne(x => x.CreatedByPerson)
            .WithMany()
            .HasForeignKey(x => x.CreatedByPersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CulturePrinciple_CreatedByPerson");

        /*
         * Indexes
         */

        // Rule 9: Global principles unique by (PrincipleCode, VersionNumber) when TenantId IS NULL
        builder.HasIndex(x => new { x.PrincipleCode, x.VersionNumber })
            .IsUnique()
            .HasFilter("[TenantId] IS NULL")
            .HasDatabaseName("UX_CulturePrinciple_Global_PrincipleCode_Version");

        // Rule 10: Tenant principles unique by (TenantId, PrincipleCode, VersionNumber) when TenantId IS NOT NULL
        builder.HasIndex(x => new { x.TenantId, x.PrincipleCode, x.VersionNumber })
            .IsUnique()
            .HasFilter("[TenantId] IS NOT NULL")
            .HasDatabaseName("UX_CulturePrinciple_Tenant_PrincipleCode_Version");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_CulturePrinciple_TenantId");

        builder.HasIndex(x => x.SourceCulturePrincipleId)
            .HasDatabaseName("IX_CulturePrinciple_SourceCulturePrincipleId");

        builder.HasIndex(x => x.OwnerPersonId)
            .HasDatabaseName("IX_CulturePrinciple_OwnerPersonId");

        builder.HasIndex(x => x.ApprovedByPersonId)
            .HasDatabaseName("IX_CulturePrinciple_ApprovedByPersonId");

        builder.HasIndex(x => x.CreatedByPersonId)
            .HasDatabaseName("IX_CulturePrinciple_CreatedByPersonId");

        /*
         * Check constraints
         */

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_CulturePrinciple_PrincipleCode_NotEmpty",
                "LEN(TRIM([PrincipleCode])) > 0");

            table.HasCheckConstraint(
                "CK_CulturePrinciple_VersionNumber_Positive",
                "[VersionNumber] > 0");

            table.HasCheckConstraint(
                "CK_CulturePrinciple_EffectiveDates",
                "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");

            table.HasCheckConstraint(
                "CK_CulturePrinciple_SourceNotSelf",
                "[SourceCulturePrincipleId] IS NULL OR [SourceCulturePrincipleId] <> [CulturePrincipleId]");

            table.HasCheckConstraint(
                "CK_CulturePrinciple_Status",
                "[Status] IN ('Draft', 'Approved', 'Superseded', 'Retired')");

            table.HasCheckConstraint(
                "CK_CulturePrinciple_ApprovedState",
                "([Status] <> 'Approved') OR ([OwnerPersonId] IS NOT NULL AND [ApprovedByPersonId] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [EffectiveFrom] IS NOT NULL)");
        });
    }
}
