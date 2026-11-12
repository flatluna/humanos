using FluentAssertions;
using HumanOS.Data;
using HumanOS.Models.Culture;
using HumanOS.Models.People;
using HumanOS.Models.Tenancy;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace CultureLibrary.Tests;

public class CultureLibraryFoundationTests
{
    private const string AzureSqlConnectionString =
        "Server=tcp:flatsqlserver.database.windows.net,1433;Initial Catalog=HumanOSDev;Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;Authentication=Active Directory Default;";

    private static HumanOsDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<HumanOsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new HumanOsDbContext(options);
    }

    [Fact]
    public void ModelConfiguration_CulturePrinciple_HasExpectedTableKeysAndConventions()
    {
        using var ctx = CreateInMemoryDbContext();
        var designModel = ctx.GetService<IDesignTimeModel>().Model;
        var entity = designModel.FindEntityType(typeof(CulturePrinciple));
        entity.Should().NotBeNull();

        // Table & Schema
        entity!.GetTableName().Should().Be("CulturePrinciple");
        entity.GetSchema().Should().Be("dbo");

        // Primary Key
        var pk = entity.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().Equal(nameof(CulturePrinciple.CulturePrincipleId));
        pk.GetName().Should().Be("PK_CulturePrinciple");

        // Concurrency token (RowVersion)
        var rowVersionProp = entity.FindProperty(nameof(CulturePrinciple.RowVersion));
        rowVersionProp.Should().NotBeNull();
        rowVersionProp!.IsConcurrencyToken.Should().BeTrue();

        // Foreign keys & Delete behaviors (Must be Restrict to prevent cascade-deletes)
        var tenantFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(CulturePrinciple.TenantId)));
        tenantFk.Should().NotBeNull();
        tenantFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var sourceFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(CulturePrinciple.SourceCulturePrincipleId)));
        sourceFk.Should().NotBeNull();
        sourceFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var ownerFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(CulturePrinciple.OwnerPersonId)));
        ownerFk.Should().NotBeNull();
        ownerFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var approverFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(CulturePrinciple.ApprovedByPersonId)));
        approverFk.Should().NotBeNull();
        approverFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var creatorFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(CulturePrinciple.CreatedByPersonId)));
        creatorFk.Should().NotBeNull();
        creatorFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        // Check constraints
        var checkConstraints = entity.GetCheckConstraints().Select(c => c.Name).ToList();
        checkConstraints.Should().Contain("CK_CulturePrinciple_PrincipleCode_NotEmpty");
        checkConstraints.Should().Contain("CK_CulturePrinciple_VersionNumber_Positive");
        checkConstraints.Should().Contain("CK_CulturePrinciple_EffectiveDates");
        checkConstraints.Should().Contain("CK_CulturePrinciple_SourceNotSelf");
        checkConstraints.Should().Contain("CK_CulturePrinciple_Status");
        checkConstraints.Should().Contain("CK_CulturePrinciple_ApprovedState");

        // Indexes
        var indexes = entity.GetIndexes().ToList();
        var globalIndex = indexes.FirstOrDefault(i => i.GetDatabaseName() == "UX_CulturePrinciple_Global_PrincipleCode_Version");
        globalIndex.Should().NotBeNull();
        globalIndex!.IsUnique.Should().BeTrue();
        globalIndex.GetFilter().Should().Be("[TenantId] IS NULL");

        var tenantIndex = indexes.FirstOrDefault(i => i.GetDatabaseName() == "UX_CulturePrinciple_Tenant_PrincipleCode_Version");
        tenantIndex.Should().NotBeNull();
        tenantIndex!.IsUnique.Should().BeTrue();
        tenantIndex.GetFilter().Should().Be("[TenantId] IS NOT NULL");
    }

    [Fact]
    public void ModelConfiguration_CulturePrincipleBehavior_HasExpectedTableKeysAndConventions()
    {
        using var ctx = CreateInMemoryDbContext();
        var designModel = ctx.GetService<IDesignTimeModel>().Model;
        var entity = designModel.FindEntityType(typeof(CulturePrincipleBehavior));
        entity.Should().NotBeNull();

        // Table & Schema
        entity!.GetTableName().Should().Be("CulturePrincipleBehavior");
        entity.GetSchema().Should().Be("dbo");

        // Primary Key
        var pk = entity.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().Equal(nameof(CulturePrincipleBehavior.CulturePrincipleBehaviorId));
        pk.GetName().Should().Be("PK_CulturePrincipleBehavior");

        // Concurrency token (RowVersion)
        var rowVersionProp = entity.FindProperty(nameof(CulturePrincipleBehavior.RowVersion));
        rowVersionProp.Should().NotBeNull();
        rowVersionProp!.IsConcurrencyToken.Should().BeTrue();

        // Foreign keys & Delete behavior (Restrict - rule 8: do not cascade-delete behaviors)
        var principleFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(CulturePrincipleBehavior.CulturePrincipleId)));
        principleFk.Should().NotBeNull();
        principleFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var creatorFk = entity.GetForeignKeys()
            .FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(CulturePrincipleBehavior.CreatedByPersonId)));
        creatorFk.Should().NotBeNull();
        creatorFk!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        // Check constraints
        var checkConstraints = entity.GetCheckConstraints().Select(c => c.Name).ToList();
        checkConstraints.Should().Contain("CK_CulturePrincipleBehavior_BehaviorCode_NotEmpty");
        checkConstraints.Should().Contain("CK_CulturePrincipleBehavior_DescriptionEn_NotEmpty");
        checkConstraints.Should().Contain("CK_CulturePrincipleBehavior_SortOrder_NonNegative");
        checkConstraints.Should().Contain("CK_CulturePrincipleBehavior_BehaviorType");

        // Indexes
        var indexes = entity.GetIndexes().ToList();
        var codeUniqueIndex = indexes.FirstOrDefault(i => i.GetDatabaseName() == "UX_CulturePrincipleBehavior_CulturePrincipleId_BehaviorCode");
        codeUniqueIndex.Should().NotBeNull();
        codeUniqueIndex!.IsUnique.Should().BeTrue();

        var compositeIndex = indexes.FirstOrDefault(i => i.GetDatabaseName() == "IX_CulturePrincipleBehavior_Principle_Type_Active_Sort");
        compositeIndex.Should().NotBeNull();
        compositeIndex!.Properties.Select(p => p.Name).Should().Equal(
            nameof(CulturePrincipleBehavior.CulturePrincipleId),
            nameof(CulturePrincipleBehavior.BehaviorType),
            nameof(CulturePrincipleBehavior.IsActive),
            nameof(CulturePrincipleBehavior.SortOrder));
    }

    [Fact]
    public async Task InMemoryDbContext_CanAddAndRetrieve_CulturePrincipleAndBehaviors()
    {
        await using var ctx = CreateInMemoryDbContext();

        // 1. Create a Global Principle
        var globalPrinciple = new CulturePrinciple
        {
            CulturePrincipleId = Guid.NewGuid(),
            TenantId = null,
            PrincipleCode = "GROWTH_MINDSET",
            NameEn = "Growth Mindset",
            NameEs = "Mentalidad de Crecimiento",
            DescriptionEn = "We embrace challenges and see effort as the path to mastery.",
            DescriptionEs = "Aceptamos los desafíos y vemos el esfuerzo como el camino a la maestría.",
            VersionNumber = 1,
            Status = CulturePrincipleStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [1, 2, 3, 4]
        };

        // Add an observable behavior to the global principle
        var behavior1 = new CulturePrincipleBehavior
        {
            CulturePrincipleBehaviorId = Guid.NewGuid(),
            CulturePrincipleId = globalPrinciple.CulturePrincipleId,
            BehaviorCode = "APPLY_FEEDBACK",
            BehaviorType = CulturePrincipleBehaviorType.ExpectedBehavior,
            DescriptionEn = "Actively seeks out feedback and incorporates it into daily work.",
            DescriptionEs = "Busca activamente retroalimentación y la incorpora al trabajo diario.",
            SortOrder = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [1, 0, 0, 0]
        };

        // 2. Create a Tenant Principle that copies and customizes the global principle
        var tenantId = Guid.NewGuid();
        var tenantPrinciple = new CulturePrinciple
        {
            CulturePrincipleId = Guid.NewGuid(),
            TenantId = tenantId,
            SourceCulturePrincipleId = globalPrinciple.CulturePrincipleId,
            PrincipleCode = "GROWTH_MINDSET",
            NameEn = "Growth Mindset (Custom)",
            DescriptionEn = "Customized growth mindset principle for tenant.",
            VersionNumber = 1,
            Status = CulturePrincipleStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = [1, 2, 3, 5]
        };

        ctx.CulturePrinciples.AddRange(globalPrinciple, tenantPrinciple);
        ctx.CulturePrincipleBehaviors.Add(behavior1);
        await ctx.SaveChangesAsync();

        // 3. Verify retrieval with navigation
        var retrieved = await ctx.CulturePrinciples
            .Include(p => p.Behaviors)
            .Include(p => p.AdaptedPrinciples)
            .FirstOrDefaultAsync(p => p.CulturePrincipleId == globalPrinciple.CulturePrincipleId);

        retrieved.Should().NotBeNull();
        retrieved!.PrincipleCode.Should().Be("GROWTH_MINDSET");
        retrieved.TenantId.Should().BeNull();
        retrieved.Behaviors.Should().HaveCount(1);
        retrieved.Behaviors.First().BehaviorCode.Should().Be("APPLY_FEEDBACK");
        retrieved.AdaptedPrinciples.Should().HaveCount(1);
        retrieved.AdaptedPrinciples.First().TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task AzureSqlDatabase_CultureTablesExistWithExpectedSchema()
    {
        await using var conn = new SqlConnection(AzureSqlConnectionString);
        await conn.OpenAsync();

        // 1. Verify Tables Exist in dbo schema
        await using var cmdTables = new SqlCommand(
            """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo'
              AND TABLE_NAME IN ('CulturePrinciple', 'CulturePrincipleBehavior')
            ORDER BY TABLE_NAME;
            """, conn);

        var existingTables = new List<string>();
        await using (var reader = await cmdTables.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                existingTables.Add(reader.GetString(0));
            }
        }

        existingTables.Should().Contain("CulturePrinciple");
        existingTables.Should().Contain("CulturePrincipleBehavior");

        // 2. Verify Foreign Keys on CulturePrinciple and CulturePrincipleBehavior
        await using var cmdFks = new SqlCommand(
            """
            SELECT fk.name AS ForeignKeyName, tp.name AS ParentTable, tr.name AS ReferencedTable, fk.delete_referential_action_desc
            FROM sys.foreign_keys fk
            JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
            JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
            WHERE tp.name IN ('CulturePrinciple', 'CulturePrincipleBehavior')
            ORDER BY fk.name;
            """, conn);

        var foreignKeys = new Dictionary<string, (string Parent, string Ref, string DeleteAction)>();
        await using (var reader = await cmdFks.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                foreignKeys[reader.GetString(0)] = (
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)
                );
            }
        }

        foreignKeys.Should().ContainKey("FK_CulturePrinciple_Tenant");
        foreignKeys["FK_CulturePrinciple_Tenant"].DeleteAction.Should().Be("NO_ACTION");

        foreignKeys.Should().ContainKey("FK_CulturePrinciple_SourceCulturePrinciple");
        foreignKeys["FK_CulturePrinciple_SourceCulturePrinciple"].DeleteAction.Should().Be("NO_ACTION");

        foreignKeys.Should().ContainKey("FK_CulturePrinciple_OwnerPerson");
        foreignKeys["FK_CulturePrinciple_OwnerPerson"].DeleteAction.Should().Be("NO_ACTION");

        foreignKeys.Should().ContainKey("FK_CulturePrinciple_ApprovedByPerson");
        foreignKeys["FK_CulturePrinciple_ApprovedByPerson"].DeleteAction.Should().Be("NO_ACTION");

        foreignKeys.Should().ContainKey("FK_CulturePrinciple_CreatedByPerson");
        foreignKeys["FK_CulturePrinciple_CreatedByPerson"].DeleteAction.Should().Be("NO_ACTION");

        foreignKeys.Should().ContainKey("FK_CulturePrincipleBehavior_CulturePrinciple");
        foreignKeys["FK_CulturePrincipleBehavior_CulturePrinciple"].DeleteAction.Should().Be("NO_ACTION");

        foreignKeys.Should().ContainKey("FK_CulturePrincipleBehavior_CreatedByPerson");
        foreignKeys["FK_CulturePrincipleBehavior_CreatedByPerson"].DeleteAction.Should().Be("NO_ACTION");

        // 3. Verify Check Constraints
        await using var cmdChecks = new SqlCommand(
            """
            SELECT cc.name AS ConstraintName
            FROM sys.check_constraints cc
            JOIN sys.tables t ON cc.parent_object_id = t.object_id
            WHERE t.name IN ('CulturePrinciple', 'CulturePrincipleBehavior');
            """, conn);

        var checkNames = new HashSet<string>();
        await using (var reader = await cmdChecks.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                checkNames.Add(reader.GetString(0));
            }
        }

        checkNames.Should().Contain("CK_CulturePrinciple_PrincipleCode_NotEmpty");
        checkNames.Should().Contain("CK_CulturePrinciple_VersionNumber_Positive");
        checkNames.Should().Contain("CK_CulturePrinciple_EffectiveDates");
        checkNames.Should().Contain("CK_CulturePrinciple_SourceNotSelf");
        checkNames.Should().Contain("CK_CulturePrinciple_Status");
        checkNames.Should().Contain("CK_CulturePrinciple_ApprovedState");
        checkNames.Should().Contain("CK_CulturePrincipleBehavior_BehaviorCode_NotEmpty");
        checkNames.Should().Contain("CK_CulturePrincipleBehavior_DescriptionEn_NotEmpty");
        checkNames.Should().Contain("CK_CulturePrincipleBehavior_SortOrder_NonNegative");
        checkNames.Should().Contain("CK_CulturePrincipleBehavior_BehaviorType");

        // 4. Verify Unique & Filtered Indexes
        await using var cmdIndexes = new SqlCommand(
            """
            SELECT i.name AS IndexName, i.is_unique, i.has_filter, i.filter_definition
            FROM sys.indexes i
            JOIN sys.tables t ON i.object_id = t.object_id
            WHERE t.name IN ('CulturePrinciple', 'CulturePrincipleBehavior')
              AND i.name LIKE 'UX_%' OR i.name LIKE 'IX_CulturePrincipleBehavior_Principle_Type_Active_Sort';
            """, conn);

        var indexDict = new Dictionary<string, (bool IsUnique, bool HasFilter, string? Filter)>();
        await using (var reader = await cmdIndexes.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                indexDict[reader.GetString(0)] = (
                    reader.GetBoolean(1),
                    reader.GetBoolean(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)
                );
            }
        }

        indexDict.Should().ContainKey("UX_CulturePrinciple_Global_PrincipleCode_Version");
        indexDict["UX_CulturePrinciple_Global_PrincipleCode_Version"].IsUnique.Should().BeTrue();
        indexDict["UX_CulturePrinciple_Global_PrincipleCode_Version"].HasFilter.Should().BeTrue();

        indexDict.Should().ContainKey("UX_CulturePrinciple_Tenant_PrincipleCode_Version");
        indexDict["UX_CulturePrinciple_Tenant_PrincipleCode_Version"].IsUnique.Should().BeTrue();
        indexDict["UX_CulturePrinciple_Tenant_PrincipleCode_Version"].HasFilter.Should().BeTrue();

        indexDict.Should().ContainKey("UX_CulturePrincipleBehavior_CulturePrincipleId_BehaviorCode");
        indexDict["UX_CulturePrincipleBehavior_CulturePrincipleId_BehaviorCode"].IsUnique.Should().BeTrue();

        indexDict.Should().ContainKey("IX_CulturePrincipleBehavior_Principle_Type_Active_Sort");
    }
}
