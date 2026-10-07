using HumanOS.Models.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public class EngramReviewSessionUsageConfiguration : IEntityTypeConfiguration<EngramReviewSessionUsage>
{
    public void Configure(EntityTypeBuilder<EngramReviewSessionUsage> builder)
    {
        builder.HasKey(u => u.EngramReviewSessionUsageId);

        builder.Property(u => u.CreatedDate)
            .HasDefaultValueSql("GETUTCDATE()")
            .IsRequired();

        builder.HasIndex(u => u.PersonId);
    }
}
