using HumanOS.Models.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HumanOS.Data.Configurations;

public sealed class TenantInvitationConfiguration : IEntityTypeConfiguration<TenantInvitation>
{
    public void Configure(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.ToTable("TenantInvitation", "dbo");

        builder.HasKey(x => x.TenantInvitationId)
            .HasName("PK_TenantInvitation");

        builder.Property(x => x.TenantInvitationId)
            .HasDefaultValueSql("NEWID()");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.NormalizedEmail)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasMaxLength(200);

        builder.Property(x => x.LastName)
            .HasMaxLength(200);

        builder.Property(x => x.OrgRole)
            .HasConversion<int>()
            .HasDefaultValue(PersonOrgRole.Employee)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(TenantInvitationStatus.Pending)
            .IsRequired();

        builder.Property(x => x.CreatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.UpdatedDate)
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(x => x.AcceptedDate)
            .HasColumnType("datetime2");

        builder.Property(x => x.RevokedDate)
            .HasColumnType("datetime2");

        builder.HasOne<Models.Tenancy.Tenant>()
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TenantInvitation_Tenant");

        builder.HasOne<Models.OrgStructure.Department>()
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_TenantInvitation_Department");

        builder.HasOne<Models.OrgStructure.JobRole>()
            .WithMany()
            .HasForeignKey(x => x.JobRoleId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_TenantInvitation_JobRole");

        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(x => x.ManagerId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_TenantInvitation_Manager");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_TenantInvitation_TenantId");

        builder.HasIndex(x => new { x.TenantId, x.NormalizedEmail, x.Status })
            .HasDatabaseName("IX_TenantInvitation_Tenant_NormalizedEmail_Status");
    }
}
