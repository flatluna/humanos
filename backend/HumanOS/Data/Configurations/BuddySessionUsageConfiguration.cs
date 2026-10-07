using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public class BuddySessionUsageConfiguration : IEntityTypeConfiguration<BuddySessionUsage>
{
    public void Configure(EntityTypeBuilder<BuddySessionUsage> builder)
    {
        builder.HasKey(u => u.BuddySessionUsageId);

        builder.Property(u => u.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasIndex(u => u.PersonId);
    }
}
