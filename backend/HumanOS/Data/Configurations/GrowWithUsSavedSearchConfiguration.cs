using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class GrowWithUsSavedSearchConfiguration : IEntityTypeConfiguration<GrowWithUsSavedSearch>
{
    public void Configure(EntityTypeBuilder<GrowWithUsSavedSearch> builder)
    {
        builder.ToTable("GrowWithUsSavedSearch", "dbo");

        builder.HasKey(x => x.GrowWithUsSavedSearchId)
            .HasName("PK_GrowWithUsSavedSearch");

        builder.Property(x => x.GrowWithUsSavedSearchId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.Topic)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.PlanJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Embedding)
            .HasColumnType("vector(1536)")
            .IsRequired(false);

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithMany()
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_GrowWithUsSavedSearch_Person");

        builder.HasIndex(x => new { x.PersonId, x.CreatedDate })
            .HasDatabaseName("IX_GrowWithUsSavedSearch_PersonId_CreatedDate");
    }
}
