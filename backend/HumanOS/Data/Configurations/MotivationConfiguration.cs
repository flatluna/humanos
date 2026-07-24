using HumanOS.Models.Motivations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class MotivationConfiguration
    : IEntityTypeConfiguration<Motivation>
{
    public void Configure(EntityTypeBuilder<Motivation> builder)
    {
        builder.ToTable("Motivation", "dbo");

        builder.HasKey(x => x.MotivationId)
            .HasName("PK_Motivation");

        builder.Property(x => x.MotivationId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
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

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Motivation_Code");
    }
}
