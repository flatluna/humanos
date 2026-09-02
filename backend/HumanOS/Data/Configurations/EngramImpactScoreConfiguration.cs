using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class EngramImpactScoreConfiguration : IEntityTypeConfiguration<EngramImpactScore>
{
    public void Configure(EntityTypeBuilder<EngramImpactScore> builder)
    {
        builder.ToTable("EngramImpactScore", "dbo");

        builder.HasKey(x => x.Id)
            .HasName("PK_EngramImpactScore");

        builder.Property(x => x.Id)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.EmployeeId)
            .IsRequired();

        builder.Property(x => x.CourseId)
            .IsRequired();

        builder.Property(x => x.EngramId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Score)
            .IsRequired();

        builder.Property(x => x.Tier)
            .HasMaxLength(20);

        builder.Property(x => x.PrimarySignal)
            .HasMaxLength(20);

        builder.Property(x => x.Reason)
            .HasMaxLength(300);

        builder.Property(x => x.GeneratedAt)
            .HasColumnType("datetime2")
            .IsRequired();

        builder.HasIndex(x => new { x.EmployeeId, x.CourseId, x.EngramId })
            .IsUnique()
            .HasDatabaseName("UX_EngramImpactScore_Employee_Course_Engram");
    }
}
