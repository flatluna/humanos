# Culture Library Foundation — Database & EF Core Model

**Date:** 2026-10-31  
**Database:** Azure SQL `HumanOSDev` (`flatsqlserver.database.windows.net`)  
**Schema:** `dbo`  
**Migration:** `20261031151306_AddCultureLibraryFoundation`

---

## 1. Overview

The Culture Library foundation provides the data structures for managing organizational culture principles and their observable behavioral expectations within the EngramsIQ platform.

Principles can be:
- **Global EngramsIQ principles:** `TenantId = NULL` (system-wide standard principles such as *Growth Mindset*, *Customer First*, *Human Accountability for AI*).
- **Tenant-specific principles:** `TenantId = <TenantGuid>` (custom principles created by a specific customer).
- **Tenant adaptations of global principles:** `TenantId = <TenantGuid>` with `SourceCulturePrincipleId = <GlobalPrincipleGuid>` (a tenant copies and tailors a global principle without modifying the original).

---

## 2. Tables Created

### 2.1 `dbo.CulturePrinciple`

Stores reusable organizational culture principles and their lifecycle status.

| Column | SQL Type | Nullable | Default | Description |
|---|---|---|---|---|
| `CulturePrincipleId` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | Primary Key (`PK_CulturePrinciple`) |
| `TenantId` | `uniqueidentifier` | YES | NULL | FK to `dbo.Tenant(TenantId)` (`FK_CulturePrinciple_Tenant`, Restrict) |
| `SourceCulturePrincipleId` | `uniqueidentifier` | YES | NULL | Self-referencing FK to `dbo.CulturePrinciple` (`FK_CulturePrinciple_SourceCulturePrinciple`, Restrict) |
| `PrincipleCode` | `nvarchar(80)` | NO | - | Stable machine code (e.g. `GROWTH_MINDSET`) |
| `NameEn` | `nvarchar(150)` | NO | - | English principle name |
| `NameEs` | `nvarchar(150)` | YES | NULL | Spanish principle name |
| `DescriptionEn` | `nvarchar(1000)` | NO | - | English description |
| `DescriptionEs` | `nvarchar(1000)` | YES | NULL | Spanish description |
| `OwnerPersonId` | `uniqueidentifier` | YES | NULL | FK to `dbo.Person(PersonId)` (`FK_CulturePrinciple_OwnerPerson`, Restrict) |
| `VersionNumber` | `int` | NO | `1` | Principle version (must be > 0) |
| `Status` | `nvarchar(20)` | NO | `'Draft'` | Allowed: `Draft`, `Approved`, `Superseded`, `Retired` |
| `EffectiveFrom` | `date` | YES | NULL | Required when Status = 'Approved' |
| `EffectiveTo` | `date` | YES | NULL | Must be >= EffectiveFrom if populated |
| `ApprovedByPersonId` | `uniqueidentifier` | YES | NULL | FK to `dbo.Person(PersonId)` (`FK_CulturePrinciple_ApprovedByPerson`, Restrict) |
| `ApprovedAt` | `datetime2(3)` | YES | NULL | Required when Status = 'Approved' |
| `CreatedAt` | `datetime2(3)` | NO | `SYSUTCDATETIME()` | Audit creation timestamp |
| `CreatedByPersonId` | `uniqueidentifier` | YES | NULL | FK to `dbo.Person(PersonId)` (`FK_CulturePrinciple_CreatedByPerson`, Restrict) |
| `UpdatedAt` | `datetime2(3)` | NO | `SYSUTCDATETIME()` | Audit update timestamp |
| `RowVersion` | `rowversion` | NO | - | Optimistic concurrency token |

#### Constraints & Indexes
- **Primary Key:** `PK_CulturePrinciple` on `[CulturePrincipleId]`
- **Check Constraints:**
  - `CK_CulturePrinciple_PrincipleCode_NotEmpty`: `LEN(TRIM([PrincipleCode])) > 0`
  - `CK_CulturePrinciple_VersionNumber_Positive`: `[VersionNumber] > 0`
  - `CK_CulturePrinciple_EffectiveDates`: `[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]`
  - `CK_CulturePrinciple_SourceNotSelf`: `[SourceCulturePrincipleId] IS NULL OR [SourceCulturePrincipleId] <> [CulturePrincipleId]`
  - `CK_CulturePrinciple_Status`: `[Status] IN ('Draft', 'Approved', 'Superseded', 'Retired')`
  - `CK_CulturePrinciple_ApprovedState`: `([Status] <> 'Approved') OR ([OwnerPersonId] IS NOT NULL AND [ApprovedByPersonId] IS NOT NULL AND [ApprovedAt] IS NOT NULL AND [EffectiveFrom] IS NOT NULL)`
- **Unique Filtered Indexes:**
  - `UX_CulturePrinciple_Global_PrincipleCode_Version`: `([PrincipleCode], [VersionNumber])` WHERE `[TenantId] IS NULL`
  - `UX_CulturePrinciple_Tenant_PrincipleCode_Version`: `([TenantId], [PrincipleCode], [VersionNumber])` WHERE `[TenantId] IS NOT NULL`
- **Lookup Indexes:**
  - `IX_CulturePrinciple_TenantId`
  - `IX_CulturePrinciple_SourceCulturePrincipleId`
  - `IX_CulturePrinciple_OwnerPersonId`
  - `IX_CulturePrinciple_ApprovedByPersonId`
  - `IX_CulturePrinciple_CreatedByPersonId`

---

### 2.2 `dbo.CulturePrincipleBehavior`

Stores observable behaviors, leadership expectations, and AI transformation implications associated with a principle.

| Column | SQL Type | Nullable | Default | Description |
|---|---|---|---|---|
| `CulturePrincipleBehaviorId` | `uniqueidentifier` | NO | `NEWSEQUENTIALID()` | Primary Key (`PK_CulturePrincipleBehavior`) |
| `CulturePrincipleId` | `uniqueidentifier` | NO | - | FK to `dbo.CulturePrinciple(CulturePrincipleId)` (`FK_CulturePrincipleBehavior_CulturePrinciple`, Restrict) |
| `BehaviorCode` | `nvarchar(80)` | NO | - | Stable machine code (e.g. `APPLY_FEEDBACK`) |
| `BehaviorType` | `nvarchar(40)` | NO | - | Allowed: `ExpectedBehavior`, `BehaviorToAvoid`, `LeadershipExpectation`, `EmployeeExpectation`, `AITransformationImplication` |
| `DescriptionEn` | `nvarchar(1000)` | NO | - | English description |
| `DescriptionEs` | `nvarchar(1000)` | YES | NULL | Spanish description |
| `SortOrder` | `int` | NO | `0` | Order of presentation (>= 0) |
| `IsActive` | `bit` | NO | `1` | Inactive behaviors remain available for history |
| `CreatedAt` | `datetime2(3)` | NO | `SYSUTCDATETIME()` | Audit creation timestamp |
| `CreatedByPersonId` | `uniqueidentifier` | YES | NULL | FK to `dbo.Person(PersonId)` (`FK_CulturePrincipleBehavior_CreatedByPerson`, Restrict) |
| `UpdatedAt` | `datetime2(3)` | NO | `SYSUTCDATETIME()` | Audit update timestamp |
| `RowVersion` | `rowversion` | NO | - | Optimistic concurrency token |

#### Constraints & Indexes
- **Primary Key:** `PK_CulturePrincipleBehavior` on `[CulturePrincipleBehaviorId]`
- **Check Constraints:**
  - `CK_CulturePrincipleBehavior_BehaviorCode_NotEmpty`: `LEN(TRIM([BehaviorCode])) > 0`
  - `CK_CulturePrincipleBehavior_DescriptionEn_NotEmpty`: `LEN(TRIM([DescriptionEn])) > 0`
  - `CK_CulturePrincipleBehavior_SortOrder_NonNegative`: `[SortOrder] >= 0`
  - `CK_CulturePrincipleBehavior_BehaviorType`: `[BehaviorType] IN ('ExpectedBehavior', 'BehaviorToAvoid', 'LeadershipExpectation', 'EmployeeExpectation', 'AITransformationImplication')`
- **Unique Indexes:**
  - `UX_CulturePrincipleBehavior_CulturePrincipleId_BehaviorCode`: `([CulturePrincipleId], [BehaviorCode])` UNIQUE
- **Composite Index:**
  - `IX_CulturePrincipleBehavior_Principle_Type_Active_Sort`: `([CulturePrincipleId], [BehaviorType], [IsActive], [SortOrder])`
- **Lookup Indexes:**
  - `IX_CulturePrincipleBehavior_CreatedByPersonId`

---

## 3. Entity Framework Core Mapping

- **Models:**
  - `HumanOS.Models.Culture.CulturePrinciple`
  - `HumanOS.Models.Culture.CulturePrincipleBehavior`
  - `HumanOS.Models.Culture.CulturePrincipleStatus`
  - `HumanOS.Models.Culture.CulturePrincipleBehaviorType`
- **Configurations:**
  - `HumanOS.Data.Configurations.CulturePrincipleConfiguration`
  - `HumanOS.Data.Configurations.CulturePrincipleBehaviorConfiguration`
- **DbContext:**
  - `DbSet<CulturePrinciple> CulturePrinciples`
  - `DbSet<CulturePrincipleBehavior> CulturePrincipleBehaviors`
- **Delete Behavior:**
  - `DeleteBehavior.Restrict` configured across all relationships to prevent cascade deletions.

---

## 4. Verification & Tests

Executed test suite in `CultureLibrary.Tests`:
- `ModelConfiguration_CulturePrinciple_HasExpectedTableKeysAndConventions`: PASSED
- `ModelConfiguration_CulturePrincipleBehavior_HasExpectedTableKeysAndConventions`: PASSED
- `InMemoryDbContext_CanAddAndRetrieve_CulturePrincipleAndBehaviors`: PASSED
- `AzureSqlDatabase_CultureTablesExistWithExpectedSchema`: PASSED (verified live against Azure SQL `flatsqlserver.database.windows.net / HumanOSDev`)

---

## 5. Enterprise Evolution — Culture CRUD Azure Functions

**Folder:** `HumanOS/backend/HumanOS/EnterpriseEvolution/Culture/`

All new Azure Functions related to the Enterprise Evolution module live under `EnterpriseEvolution/`, grouped by sub-domain (`Culture/` for the Culture Library). Functions use the isolated-worker HTTP trigger pattern (same as `AzureFunctions/Api/*`), inject `IDbContextFactory<HumanOsDbContext>`, and return responses via the shared `HumanOS.AzureFunctions.Api.FunctionResponseFactory` (camelCase JSON).

### 5.1 `CulturePrincipleFunctions.cs`

| Function | Method | Route |
|---|---|---|
| `GetCulturePrinciples` | GET | `enterprise-evolution/culture/principles` (optional `?tenantId=<guid>|global`) |
| `GetCulturePrincipleById` | GET | `enterprise-evolution/culture/principles/{principleId:guid}` |
| `CreateCulturePrinciple` | POST | `enterprise-evolution/culture/principles` |
| `UpdateCulturePrinciple` | PUT | `enterprise-evolution/culture/principles/{principleId:guid}` |
| `DeleteCulturePrinciple` | DELETE | `enterprise-evolution/culture/principles/{principleId:guid}` |

Business rules enforced in code (mirroring the DB check constraints) before hitting the database:
- `VersionNumber` must be greater than zero.
- `Status` must be one of `Draft`, `Approved`, `Superseded`, `Retired`.
- `SourceCulturePrincipleId` must not reference the same principle.
- `EffectiveTo` must not be earlier than `EffectiveFrom`.
- When `Status = Approved`: `OwnerPersonId`, `ApprovedByPersonId`, `ApprovedAt`, and `EffectiveFrom` are required.
- **Delete** is blocked (409 Conflict) unless the principle is `Draft` and is not referenced by another principle's `SourceCulturePrincipleId` (tenant adaptation) — Approved/Superseded/Retired/referenced principles are never physically deleted.
- Unique-index violations (duplicate `PrincipleCode` + `VersionNumber` per tenant/global scope) are caught and returned as `409 Conflict`.

### 5.2 `CulturePrincipleBehaviorFunctions.cs`

| Function | Method | Route |
|---|---|---|
| `GetCulturePrincipleBehaviors` | GET | `enterprise-evolution/culture/principles/{principleId:guid}/behaviors` |
| `GetCulturePrincipleBehaviorById` | GET | `enterprise-evolution/culture/principles/{principleId:guid}/behaviors/{behaviorId:guid}` |
| `CreateCulturePrincipleBehavior` | POST | `enterprise-evolution/culture/principles/{principleId:guid}/behaviors` |
| `UpdateCulturePrincipleBehavior` | PUT | `enterprise-evolution/culture/principles/{principleId:guid}/behaviors/{behaviorId:guid}` |
| `DeleteCulturePrincipleBehavior` | DELETE | `enterprise-evolution/culture/principles/{principleId:guid}/behaviors/{behaviorId:guid}` |

Business rules enforced in code:
- `BehaviorType` must be one of `ExpectedBehavior`, `BehaviorToAvoid`, `LeadershipExpectation`, `EmployeeExpectation`, `AITransformationImplication`.
- `SortOrder` must not be negative.
- Behaviors are always scoped to their parent `principleId` from the route; a mismatched `principleId`/`behaviorId` pair returns `404 Not Found`.
- Unique-index violations (duplicate `BehaviorCode` per principle) are caught and returned as `409 Conflict`.

### 5.3 Manual Verification

Started the Functions host locally (`dotnet run` via the isolated worker) and confirmed, against the live Azure SQL `HumanOSDev` database, that the full CRUD lifecycle works end-to-end:
1. `POST /enterprise-evolution/culture/principles` → `201 Created`.
2. `GET` by id and list → `200 OK`.
3. `POST` a behavior under the principle → `201 Created`.
4. `PUT` with `Status = Approved` but missing required approval fields → `400 Bad Request` (validation correctly blocked it).
5. `PUT` a valid partial update → `200 OK`.
6. `DELETE` the behavior, then the principle (both still `Draft` and unreferenced) → `200 OK` for both.

No APIs/UI/seed data/JSON fields were added beyond the CRUD endpoints themselves, per scope.
