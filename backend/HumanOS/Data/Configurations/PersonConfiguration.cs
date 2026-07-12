using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Person", "dbo");

        builder.HasKey(x => x.PersonId)
            .HasName("PK_Person");

        builder.Property(x => x.PersonId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.AzureOid)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.AzureTid)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(255);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.LastLoginDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Person_Tenant");

        builder.HasIndex(x => new
            {
                x.AzureTid,
                x.AzureOid
            })
            .IsUnique()
            .HasDatabaseName("UX_Person_AzureTid_AzureOid");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_Person_TenantId");
    }
}
