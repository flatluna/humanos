using HumanOS.Contracts.Programs;
using HumanOS.Data;
using HumanOS.Models.Programs;
using Microsoft.EntityFrameworkCore;

namespace HumanOS.Services;

/// <summary>
/// CRUD for Programs (curated Capability sequences). Manual curation only
/// — no LLM/agent pipeline, unlike Capability creation. Capability
/// selection/sequencing is a full-replace operation (the wizard always
/// submits the complete desired list), same "simplest correct thing"
/// choice as ProgramCapabilityConfiguration's unique (ProgramId,SortOrder)
/// index.
/// </summary>
public sealed class ProgramService
{
    private readonly HumanOsDbContext _dbContext;

    public ProgramService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProgramResponse>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var programs = await _dbContext.Programs
            .AsNoTracking()
            .Include(p => p.ProgramCapabilities)
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.UpdatedDate)
            .ToListAsync(cancellationToken);

        return programs.Select(ToResponse).ToList();
    }

    public async Task<ProgramDetailResponse?> GetByIdAsync(
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.Programs
            .AsNoTracking()
            .Include(p => p.ProgramCapabilities)
                .ThenInclude(pc => pc.Capability)
                    .ThenInclude(c => c.Subject)
            .Include(p => p.ProgramCapabilities)
                .ThenInclude(pc => pc.Capability)
                    .ThenInclude(c => c.Levels)
            .Include(p => p.ProgramCapabilities)
                .ThenInclude(pc => pc.Capability)
                    .ThenInclude(c => c.CapabilityGraph)
                        .ThenInclude(g => g!.Nodes)
            .SingleOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);

        if (program is null)
        {
            return null;
        }

        var response = ToResponse(program);
        var detail = new ProgramDetailResponse
        {
            ProgramId = response.ProgramId,
            Code = response.Code,
            Name = response.Name,
            Description = response.Description,
            Objectives = response.Objectives,
            Requirements = response.Requirements,
            HasLogo = response.HasLogo,
            IsActive = response.IsActive,
            CapabilityCount = response.CapabilityCount,
            CreatedDate = response.CreatedDate,
            UpdatedDate = response.UpdatedDate,
            Capabilities = [.. program.ProgramCapabilities
                .OrderBy(pc => pc.SortOrder)
                .Select(pc => new ProgramCapabilityResponse
                {
                    ProgramCapabilityId = pc.ProgramCapabilityId,
                    CapabilityId = pc.CapabilityId,
                    CapabilityCode = pc.Capability.Code,
                    CapabilityName = pc.Capability.Name,
                    SubjectCode = pc.Capability.Subject?.Code,
                    SortOrder = pc.SortOrder,
                    IsRequired = pc.IsRequired,
                    PhaseLabel = pc.PhaseLabel,
                    Objectives = pc.Objectives,
                    Requirements = pc.Requirements,
                    LevelCount = pc.Capability.Levels.Count,
                    NodeCount = pc.Capability.CapabilityGraph?.Nodes.Count ?? 0,
                    HasCoverImage = !string.IsNullOrEmpty(pc.Capability.CapabilityGraph?.CoverImageStoragePath),
                })]
        };

        return detail;
    }

    public async Task<ProgramResponse> CreateAsync(
        SaveProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var program = new LearningProgram
        {
            ProgramId = Guid.NewGuid(),
            Code = $"PROG-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Objectives = request.Objectives?.Trim(),
            Requirements = request.Requirements?.Trim(),
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
        };

        _dbContext.Programs.Add(program);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(program);
    }

    public async Task<ProgramResponse?> UpdateAsync(
        Guid programId,
        SaveProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.Programs
            .Include(p => p.ProgramCapabilities)
            .SingleOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);

        if (program is null)
        {
            return null;
        }

        program.Name = request.Name.Trim();
        program.Description = request.Description?.Trim();
        program.Objectives = request.Objectives?.Trim();
        program.Requirements = request.Requirements?.Trim();
        program.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(program);
    }

    /// <summary>Replaces this Program's entire capability sequence with the
    /// submitted list — deletes any ProgramCapability rows not present in
    /// the request, updates the rest, and inserts new ones.</summary>
    public async Task<bool> UpdateCapabilitiesAsync(
        Guid programId,
        UpdateProgramCapabilitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        var program = await _dbContext.Programs
            .Include(p => p.ProgramCapabilities)
            .SingleOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);

        if (program is null)
        {
            return false;
        }

        var requestedIds = request.Capabilities.Select(c => c.CapabilityId).ToHashSet();

        var toRemove = program.ProgramCapabilities
            .Where(pc => !requestedIds.Contains(pc.CapabilityId))
            .ToList();
        foreach (var pc in toRemove)
        {
            program.ProgramCapabilities.Remove(pc);
        }

        foreach (var entry in request.Capabilities)
        {
            var existing = program.ProgramCapabilities.FirstOrDefault(pc => pc.CapabilityId == entry.CapabilityId);
            if (existing is not null)
            {
                existing.SortOrder = entry.SortOrder;
                existing.IsRequired = entry.IsRequired;
                existing.PhaseLabel = entry.PhaseLabel?.Trim();
            }
            else
            {
                program.ProgramCapabilities.Add(new ProgramCapability
                {
                    ProgramCapabilityId = Guid.NewGuid(),
                    ProgramId = programId,
                    CapabilityId = entry.CapabilityId,
                    SortOrder = entry.SortOrder,
                    IsRequired = entry.IsRequired,
                    PhaseLabel = entry.PhaseLabel?.Trim(),
                    CreatedDate = DateTime.UtcNow,
                });
            }
        }

        program.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid programId, CancellationToken cancellationToken = default)
    {
        var deleted = await _dbContext.Programs
            .Where(p => p.ProgramId == programId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    /// <summary>Attaches an existing Capability to a Program's sequence —
    /// the "connect a capability to a program" direction of the flow
    /// (Program is created top-down first, empty; Capabilities are
    /// attached to it afterward, either at Capability-creation time or
    /// later from the Capability's own detail page). Idempotent: attaching
    /// an already-linked Capability is a no-op. Returns false if the
    /// Program doesn't exist.
    ///
    /// <paramref name="sortOrder"/> is the designer's own EXPLICIT choice
    /// of position (1..N, e.g. "#8") when set — gaps are allowed and never
    /// forced to be contiguous (the designer may create capability #8
    /// before #6 exists). Null falls back to the original auto-append
    /// behavior (current max + 1), used by the plain "attach to end"
    /// entry point (<see cref="AttachCapabilityAsync"/>). If the requested
    /// slot was already taken by another Capability by the time this
    /// actually runs (a rare race — the frontend already hides taken
    /// slots), it falls back to auto-append rather than failing the whole
    /// Capability-creation pipeline over a sequence-number collision.
    ///
    /// Static + takes an explicit <see cref="HumanOsDbContext"/> (rather
    /// than using <c>_dbContext</c>) so it can also be called from
    /// <see cref="PdfCapabilityGraphPipelineService"/>, which is a
    /// Singleton and creates its own short-lived DbContext instances via
    /// IDbContextFactory rather than having a Scoped ProgramService
    /// injected.</summary>
    public static async Task<bool> AttachCapabilityToEndAsync(
        HumanOsDbContext dbContext,
        Guid programId,
        Guid capabilityId,
        CancellationToken cancellationToken = default,
        int? sortOrder = null,
        string? objectives = null,
        string? requirements = null)
    {
        var program = await dbContext.Programs.FindAsync([programId], cancellationToken);
        if (program is null)
        {
            return false;
        }

        var alreadyLinked = await dbContext.ProgramCapabilities
            .AnyAsync(pc => pc.ProgramId == programId && pc.CapabilityId == capabilityId, cancellationToken);
        if (alreadyLinked)
        {
            return true;
        }

        var resolvedSortOrder = sortOrder;
        if (resolvedSortOrder is int requestedSortOrder)
        {
            var slotTaken = await dbContext.ProgramCapabilities
                .AnyAsync(pc => pc.ProgramId == programId && pc.SortOrder == requestedSortOrder, cancellationToken);
            if (slotTaken)
            {
                resolvedSortOrder = null;
            }
        }

        if (resolvedSortOrder is null)
        {
            var maxSortOrder = await dbContext.ProgramCapabilities
                .Where(pc => pc.ProgramId == programId)
                .Select(pc => (int?)pc.SortOrder)
                .MaxAsync(cancellationToken);
            resolvedSortOrder = (maxSortOrder ?? 0) + 1;
        }

        dbContext.ProgramCapabilities.Add(new ProgramCapability
        {
            ProgramCapabilityId = Guid.NewGuid(),
            ProgramId = programId,
            CapabilityId = capabilityId,
            SortOrder = resolvedSortOrder.Value,
            IsRequired = true,
            Objectives = objectives?.Trim() is { Length: > 0 } trimmedObjectives ? trimmedObjectives : null,
            Requirements = requirements?.Trim() is { Length: > 0 } trimmedRequirements ? trimmedRequirements : null,
            CreatedDate = DateTime.UtcNow,
        });

        program.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> AttachCapabilityAsync(Guid programId, Guid capabilityId, CancellationToken cancellationToken = default) =>
        AttachCapabilityToEndAsync(_dbContext, programId, capabilityId, cancellationToken);

    /// <summary>Unlinks a Capability from a Program (the Capability itself
    /// is untouched). Returns false if no such link exists.</summary>
    public async Task<bool> DetachCapabilityAsync(
        Guid programId,
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _dbContext.ProgramCapabilities
            .Where(pc => pc.ProgramId == programId && pc.CapabilityId == capabilityId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }

    /// <summary>Every Program a given Capability currently belongs to —
    /// powers the "Programas" section on the Capability's own detail page
    /// (a Capability may belong to zero, one, or several Programs).</summary>
    public async Task<IReadOnlyList<CapabilityProgramMembershipResponse>> GetProgramsForCapabilityAsync(
        Guid capabilityId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProgramCapabilities
            .AsNoTracking()
            .Where(pc => pc.CapabilityId == capabilityId)
            .Include(pc => pc.Program)
            .OrderBy(pc => pc.Program.Name)
            .Select(pc => new CapabilityProgramMembershipResponse
            {
                ProgramId = pc.ProgramId,
                ProgramCode = pc.Program.Code,
                ProgramName = pc.Program.Name,
                SortOrder = pc.SortOrder,
                IsRequired = pc.IsRequired,
                PhaseLabel = pc.PhaseLabel,
                Objectives = pc.Objectives,
                Requirements = pc.Requirements,
            })
            .ToListAsync(cancellationToken);
    }

    private static ProgramResponse ToResponse(LearningProgram program) => new()
    {
        ProgramId = program.ProgramId,
        Code = program.Code,
        Name = program.Name,
        Description = program.Description,
        Objectives = program.Objectives,
        Requirements = program.Requirements,
        HasLogo = !string.IsNullOrEmpty(program.LogoStoragePath),
        IsActive = program.IsActive,
        CapabilityCount = program.ProgramCapabilities.Count,
        CreatedDate = program.CreatedDate,
        UpdatedDate = program.UpdatedDate,
    };
}
