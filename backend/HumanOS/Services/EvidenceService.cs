using HumanOS.Data;

namespace HumanOS.Services;

public sealed class EvidenceService
{
    private readonly HumanOsDbContext _dbContext;

    public EvidenceService(HumanOsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
