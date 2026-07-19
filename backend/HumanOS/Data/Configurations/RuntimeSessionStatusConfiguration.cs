using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class RuntimeSessionStatusConfiguration
    : IEntityTypeConfiguration<RuntimeSessionStatus>
{
    public void Configure(EntityTypeBuilder<RuntimeSessionStatus> builder)
    {
        builder.ToTable("RuntimeSessionStatus", "dbo");

        builder.HasKey(x => x.SessionId)
            .HasName("PK_RuntimeSessionStatus");

        builder.Property(x => x.SessionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IsTerminal)
            .IsRequired();

        builder.Property(x => x.FinalStage)
            .HasMaxLength(50);

        builder.Property(x => x.UpdatedDate)
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
