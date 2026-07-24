using HumanOS.Models.Programs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class ProgramConfiguration : IEntityTypeConfiguration<LearningProgram>
{
    public void Configure(EntityTypeBuilder<LearningProgram> builder)
    {
        builder.ToTable("Program", "dbo");

        builder.HasKey(x => x.ProgramId)
            .HasName("PK_Program");

        builder.Property(x => x.ProgramId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.Code)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(400)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.Objectives)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Requirements)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.LogoStoragePath)
            .HasMaxLength(1000);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UX_Program_Code");
    }
}
