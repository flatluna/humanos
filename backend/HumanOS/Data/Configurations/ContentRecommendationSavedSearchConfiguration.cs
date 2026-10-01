using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ContentRecommendationSavedSearchConfiguration : IEntityTypeConfiguration<ContentRecommendationSavedSearch>
{
    public void Configure(EntityTypeBuilder<ContentRecommendationSavedSearch> builder)
    {
        builder.ToTable("ContentRecommendationSavedSearch", "dbo");

        builder.HasKey(x => x.ContentRecommendationSavedSearchId)
            .HasName("PK_ContentRecommendationSavedSearch");

        builder.Property(x => x.ContentRecommendationSavedSearchId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.Topic)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ResultJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_ContentRecommendationSavedSearch_Person");

        builder.HasIndex(x => new { x.PersonId, x.CreatedDate })
            .HasDatabaseName("IX_ContentRecommendationSavedSearch_PersonId_CreatedDate");
    }
}
