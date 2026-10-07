using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public class BuddyChatUsageConfiguration : IEntityTypeConfiguration<BuddyChatUsage>
{
    public void Configure(EntityTypeBuilder<BuddyChatUsage> builder)
    {
        builder.HasKey(u => u.BuddyChatUsageId);

        builder.Property(u => u.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasIndex(u => u.PersonId);
    }
}
