using HumanOS.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class AgentMessageConfiguration
    : IEntityTypeConfiguration<AgentMessage>
{
    public void Configure(EntityTypeBuilder<AgentMessage> builder)
    {
        builder.ToTable("AgentMessage", "dbo");

        builder.HasKey(x => x.AgentMessageId)
            .HasName("PK_AgentMessage");

        builder.Property(x => x.AgentMessageId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.AgentId)
            .IsRequired();

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IsRead)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Agent)
            .WithMany()
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AgentMessage_Agent");

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_AgentMessage_Person");

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AgentMessage_Tenant");

        builder.HasIndex(x => new { x.PersonId, x.CreatedDate })
            .HasDatabaseName("IX_AgentMessage_PersonId_CreatedDate");
    }
}
