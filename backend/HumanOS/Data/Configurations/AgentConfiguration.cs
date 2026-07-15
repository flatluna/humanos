using HumanOS.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agent", "dbo");

        builder.HasKey(x => x.AgentId)
            .HasName("PK_Agent");

        builder.Property(x => x.AgentId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.CapabilityId)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Role)
            .HasMaxLength(255);

        builder.Property(x => x.Description)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Capability)
            .WithMany()
            .HasForeignKey(x => x.CapabilityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Agent_Capability");

        // Every capability requires exactly one agent — enforce 1:1.
        builder.HasIndex(x => x.CapabilityId)
            .IsUnique()
            .HasDatabaseName("UX_Agent_CapabilityId");
    }
}
