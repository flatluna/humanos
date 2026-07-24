using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonCurrentSituationConfiguration
    : IEntityTypeConfiguration<PersonCurrentSituation>
{
    public void Configure(EntityTypeBuilder<PersonCurrentSituation> builder)
    {
        builder.ToTable("PersonCurrentSituation", "dbo");

        builder.HasKey(x => x.PersonCurrentSituationId)
            .HasName("PK_PersonCurrentSituation");

        builder.Property(x => x.PersonCurrentSituationId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.PersonId)
            .IsRequired();

        builder.Property(x => x.SelectedSubjectCodes)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.SelfAssessedLevelsJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(x => x.Completed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Person)
            .WithOne()
            .HasForeignKey<PersonCurrentSituation>(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_PersonCurrentSituation_Person");

        builder.HasIndex(x => x.PersonId)
            .IsUnique()
            .HasDatabaseName("UX_PersonCurrentSituation_PersonId");
    }
}
