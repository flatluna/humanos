using HumanOS.Models.Culture;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class CulturePrincipleBehaviorConfiguration : IEntityTypeConfiguration<CulturePrincipleBehavior>
{
    public void Configure(EntityTypeBuilder<CulturePrincipleBehavior> builder)
    {
        builder.ToTable("CulturePrincipleBehavior", "dbo");

        builder.HasKey(x => x.CulturePrincipleBehaviorId)
            .HasName("PK_CulturePrincipleBehavior");

        builder.Property(x => x.CulturePrincipleBehaviorId)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(x => x.CulturePrincipleId)
            .IsRequired();

        builder.Property(x => x.BehaviorCode)
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.BehaviorType)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.DescriptionEn)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.DescriptionEs)
            .HasMaxLength(1000);

        builder.Property(x => x.SortOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

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

        // Rule 8: Do not cascade-delete behaviors when a CulturePrinciple is deleted
        builder.HasOne(x => x.CulturePrinciple)
            .WithMany(x => x.Behaviors)
            .HasForeignKey(x => x.CulturePrincipleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CulturePrincipleBehavior_CulturePrinciple");

        builder.HasOne(x => x.CreatedByPerson)
            .WithMany()
            .HasForeignKey(x => x.CreatedByPersonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CulturePrincipleBehavior_CreatedByPerson");

        /*
         * Indexes
         */

        // Rule 3: BehaviorCode must be unique within CulturePrincipleId
        builder.HasIndex(x => new { x.CulturePrincipleId, x.BehaviorCode })
            .IsUnique()
            .HasDatabaseName("UX_CulturePrincipleBehavior_CulturePrincipleId_BehaviorCode");

        // Rule 9: Index on (CulturePrincipleId, BehaviorType, IsActive, SortOrder)
        builder.HasIndex(x => new { x.CulturePrincipleId, x.BehaviorType, x.IsActive, x.SortOrder })
            .HasDatabaseName("IX_CulturePrincipleBehavior_Principle_Type_Active_Sort");

        builder.HasIndex(x => x.CreatedByPersonId)
            .HasDatabaseName("IX_CulturePrincipleBehavior_CreatedByPersonId");

        /*
         * Check constraints
         */

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_CulturePrincipleBehavior_BehaviorCode_NotEmpty",
                "LEN(TRIM([BehaviorCode])) > 0");

            table.HasCheckConstraint(
                "CK_CulturePrincipleBehavior_DescriptionEn_NotEmpty",
                "LEN(TRIM([DescriptionEn])) > 0");

            table.HasCheckConstraint(
                "CK_CulturePrincipleBehavior_SortOrder_NonNegative",
                "[SortOrder] >= 0");

            table.HasCheckConstraint(
                "CK_CulturePrincipleBehavior_BehaviorType",
                "[BehaviorType] IN ('ExpectedBehavior', 'BehaviorToAvoid', 'LeadershipExpectation', 'EmployeeExpectation', 'AITransformationImplication')");
        });
    }
}
