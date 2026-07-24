using HumanOS.Models.Goals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class GoalConfiguration
    : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goal", "dbo");

        builder.HasKey(x => x.GoalId)
            .HasName("PK_Goal");

        builder.Property(x => x.GoalId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.Category)
            .HasMaxLength(200);

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

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Goal_Code");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Goal_Category",
                """
                [Category] IS NULL
                OR [Category] IN
                (
                    'PERSONAL_GROWTH',
                    'CAPABILITY_DEVELOPMENT',
                    'PROFESSIONAL',
                    'VALUE_CREATION',
                    'CONTRIBUTION',
                    'LIFE'
                )
                """);
        });
    }
}
